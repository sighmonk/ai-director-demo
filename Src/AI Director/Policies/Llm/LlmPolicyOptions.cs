using System;
using System.IO;

/// <summary>
/// Configuration of the LLM policies. The model id is pinned here and recorded with every session;
/// no semantic cache is used.
/// </summary>
public sealed class LlmPolicyOptions
{
    public const string DefaultModel = "deepseek-chat";
    public const string DefaultBaseUrl = "https://api.deepseek.com";

    public string Model { get; init; } = DefaultModel;

    public double Temperature { get; init; }

    public int MaxOutputTokens { get; init; } = 64;

    /// <summary>At most one call per this many game seconds, and only outside Maintain.</summary>
    public double DecisionCadenceSeconds { get; init; } = 4.0;

    /// <summary>Decisions slower than this are executed and logged as <c>deadline_miss</c>.</summary>
    public double DecisionDeadlineSeconds { get; init; } = 0.5;

    public int MaxRetries { get; init; } = 2;

    /// <summary>A session with a failed-call rate above this value is flagged.</summary>
    public double FailedCallFlagRate { get; init; } = 0.05;

    public double InputCostPerMillionTokensUsd { get; init; }

    public double OutputCostPerMillionTokensUsd { get; init; }

    /// <summary>JSONL path; null disables telemetry output (counters still work).</summary>
    public string? TelemetryPath { get; init; }

    public static LlmPolicyOptions FromEnvironment(string? telemetryPath = null)
    {
        return new LlmPolicyOptions
        {
            Model = ReadString("DEEPSEEK_MODEL") ?? DefaultModel,
            Temperature = 0.0,
            MaxOutputTokens = ReadInt("DEEPSEEK_MAX_OUTPUT_TOKENS") ?? 64,
            InputCostPerMillionTokensUsd = ReadDouble("DEEPSEEK_INPUT_USD_PER_MTOK") ?? 0.0,
            OutputCostPerMillionTokensUsd = ReadDouble("DEEPSEEK_OUTPUT_USD_PER_MTOK") ?? 0.0,
            TelemetryPath = telemetryPath
        };
    }

    /// <summary>Base URL for the OpenAI-compatible endpoint; used by <see cref="DeepSeekLlmClient"/>.</summary>
    public static string BaseUrlFromEnvironment()
    {
        return ReadString("DEEPSEEK_BASE_URL") ?? DefaultBaseUrl;
    }

    private static string? ReadString(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ReadInt(string name)
    {
        string? value = ReadString(name);
        return value != null && int.TryParse(value, out int parsed) ? parsed : null;
    }

    private static double? ReadDouble(string name)
    {
        string? value = ReadString(name);
        return value != null && double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double parsed) ? parsed : null;
    }
}
