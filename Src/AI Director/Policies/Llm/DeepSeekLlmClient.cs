using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// OpenAI-compatible client for the DeepSeek chat completions endpoint. The API key comes from
/// <c>DEEPSEEK_API_KEY</c> and is never stored in the repository. Tests use a fake client instead.
/// </summary>
public sealed class DeepSeekLlmClient : ILlmClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public DeepSeekLlmClient(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("A DeepSeek API key is required.", nameof(apiKey));

        _apiKey = apiKey;
        _baseUrl = (baseUrl ?? LlmPolicyOptions.BaseUrlFromEnvironment()).TrimEnd('/');
        _http = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient == null;
    }

    /// <param name="httpClient">Optional client for tests; the environment key is still required.</param>
    public static DeepSeekLlmClient FromEnvironment(HttpClient? httpClient = null)
    {
        string? key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new LlmClientException("DEEPSEEK_API_KEY is not set.");
        }

        return new DeepSeekLlmClient(key, LlmPolicyOptions.BaseUrlFromEnvironment(), httpClient);
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = request.Model,
            messages = new[] { new { role = "user", content = request.Prompt } },
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            response_format = new { type = "json_object" }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new LlmClientException($"DeepSeek returned {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        JsonElement choice = root.GetProperty("choices")[0];
        string content = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        int promptTokens = 0;
        int completionTokens = 0;
        if (root.TryGetProperty("usage", out JsonElement usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out JsonElement prompt)) promptTokens = prompt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out JsonElement completion)) completionTokens = completion.GetInt32();
        }

        string model = root.TryGetProperty("model", out JsonElement modelElement)
            ? modelElement.GetString() ?? request.Model
            : request.Model;

        return new LlmResponse(content, model, promptTokens, completionTokens);
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _http.Dispose();
    }
}
