using System.Threading;
using System.Threading.Tasks;

/// <summary>A single chat completion request. The prompt already contains prefix and state.</summary>
public sealed class LlmRequest
{
    public LlmRequest(string model, string prompt, double temperature, int maxOutputTokens)
    {
        Model = model;
        Prompt = prompt;
        Temperature = temperature;
        MaxOutputTokens = maxOutputTokens;
    }

    public string Model { get; }

    public string Prompt { get; }

    public double Temperature { get; }

    public int MaxOutputTokens { get; }
}

/// <summary>Provider response with the token usage needed for cost accounting.</summary>
public sealed class LlmResponse
{
    public LlmResponse(string content, string model, int promptTokens, int completionTokens)
    {
        Content = content;
        Model = model;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
    }

    public string Content { get; }

    public string Model { get; }

    public int PromptTokens { get; }

    public int CompletionTokens { get; }
}

public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

public sealed class LlmClientException : System.Exception
{
    public LlmClientException(string message) : base(message)
    {
    }
}
