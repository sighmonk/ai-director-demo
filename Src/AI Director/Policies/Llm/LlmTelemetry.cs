using System;
using System.IO;
using System.Text.Json;

public sealed class LlmSessionSummary
{
    public string SessionId { get; init; } = "";

    public int Calls { get; init; }

    public int Failures { get; init; }

    public int Proposed { get; init; }

    public int Legal { get; init; }

    public int DeadlineMisses { get; init; }

    public long PromptTokens { get; init; }

    public long CompletionTokens { get; init; }

    public double CostUsd { get; init; }

    public double LegalRate => Proposed > 0 ? (double)Legal / Proposed : 0.0;

    public double FailureRate => Calls > 0 ? (double)Failures / Calls : 0.0;

    public bool IsFlagged { get; init; }
}

/// <summary>
/// JSONL telemetry for one arm of one session: every decision (latency, tokens, cost, legality,
/// deadline miss, retries) and a bounded sample of canonical states for the offline agreement
/// measurement. Writes are serialised, so a decision can be consumed on the main thread safely.
/// </summary>
public sealed class LlmTelemetry
{
    private readonly object _gate = new();
    private readonly LlmPolicyOptions _options;
    private readonly string _sessionId;
    private readonly string? _path;

    private int _calls;
    private int _failures;
    private int _proposed;
    private int _legal;
    private int _deadlineMisses;
    private int _samples;
    private long _promptTokens;
    private long _completionTokens;
    private double _costUsd;

    public LlmTelemetry(LlmPolicyOptions options, string sessionId)
    {
        _options = options;
        _sessionId = sessionId;
        _path = options.TelemetryPath;
    }

    public string SessionId => _sessionId;

    public void RecordDecision(
        string arm,
        double gameTime,
        string stateName,
        string? proposedAction,
        string? executedAction,
        bool legal,
        LlmRejectionReason reason,
        double latencyMs,
        bool deadlineMiss,
        int promptTokens,
        int completionTokens,
        int retries,
        bool failed)
    {
        double cost = ((promptTokens / 1_000_000.0) * _options.InputCostPerMillionTokensUsd)
                    + ((completionTokens / 1_000_000.0) * _options.OutputCostPerMillionTokensUsd);

        lock (_gate)
        {
            _calls++;
            if (failed) _failures++;
            if (proposedAction != null) _proposed++;
            if (legal) _legal++;
            if (deadlineMiss) _deadlineMisses++;
            _promptTokens += promptTokens;
            _completionTokens += completionTokens;
            _costUsd += cost;

            Append(new
            {
                type = "decision",
                session = _sessionId,
                arm,
                game_time = Math.Round(gameTime, 3),
                state = stateName,
                proposed = proposedAction,
                executed = executedAction,
                legal,
                rejection = reason == LlmRejectionReason.None ? null : reason.ToString().ToLowerInvariant(),
                latency_ms = Math.Round(latencyMs, 1),
                deadline_miss = deadlineMiss,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                retries,
                failed,
                cost_usd = Math.Round(cost, 6)
            });
        }
    }

    /// <summary>Stores up to 50 canonical prompts for the offline agreement measurement.</summary>
    public void RecordSample(string arm, double gameTime, string prompt, string stateJson)
    {
        lock (_gate)
        {
            if (_samples >= 50) return;
            _samples++;

            Append(new
            {
                type = "state_sample",
                session = _sessionId,
                arm,
                game_time = Math.Round(gameTime, 3),
                prompt_hash = prompt.GetHashCode().ToString("x8", System.Globalization.CultureInfo.InvariantCulture),
                prompt,
                state = stateJson
            });
        }
    }

    public LlmSessionSummary GetSummary()
    {
        lock (_gate)
        {
            return new LlmSessionSummary
            {
                SessionId = _sessionId,
                Calls = _calls,
                Failures = _failures,
                Proposed = _proposed,
                Legal = _legal,
                DeadlineMisses = _deadlineMisses,
                PromptTokens = _promptTokens,
                CompletionTokens = _completionTokens,
                CostUsd = _costUsd,
                IsFlagged = _calls > 0 && (double)_failures / _calls > _options.FailedCallFlagRate
            };
        }
    }

    public void WriteSummary(string arm)
    {
        LlmSessionSummary summary = GetSummary();
        lock (_gate)
        {
            Append(new
            {
                type = "session_summary",
                session = _sessionId,
                arm,
                calls = summary.Calls,
                failures = summary.Failures,
                proposed = summary.Proposed,
                legal = summary.Legal,
                legal_rate = Math.Round(summary.LegalRate, 4),
                failure_rate = Math.Round(summary.FailureRate, 4),
                deadline_misses = summary.DeadlineMisses,
                prompt_tokens = summary.PromptTokens,
                completion_tokens = summary.CompletionTokens,
                cost_usd = Math.Round(summary.CostUsd, 6),
                flagged = summary.IsFlagged
            });
        }
    }

    private void Append(object record)
    {
        if (string.IsNullOrWhiteSpace(_path)) return;

        string path = _path!;
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.AppendAllText(path, JsonSerializer.Serialize(record) + "\n");
    }
}
