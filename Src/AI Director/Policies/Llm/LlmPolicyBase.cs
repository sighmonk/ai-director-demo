using System;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class LlmCallResult
{
    public LlmResponse? Response { get; init; }

    public bool Failed { get; init; }

    public string? Error { get; init; }

    public int Retries { get; init; }

    public double LatencyMs { get; init; }
}

/// <summary>
/// Shared behaviour of the two LLM arms: one asynchronous call per decision cadence, retries, the
/// deadline metric and JSONL telemetry. The arms differ only in what happens to a rejected proposal.
/// </summary>
public abstract class LlmPolicyBase : IActionPolicy
{
    private readonly ILlmClient _client;
    private readonly LlmPolicyOptions _options;
    private readonly LlmActionValidator _validator;
    private readonly Func<DateTimeOffset> _clock;

    private Task<LlmCallResult>? _pending;
    private double _lastCallTime = double.NegativeInfinity;
    private string _pendingPrompt = string.Empty;

    protected LlmPolicyBase(
        ILlmClient client,
        LlmPolicyOptions options,
        LlmActionCatalog catalog,
        LlmTelemetry telemetry,
        Func<DateTimeOffset>? clock = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _validator = new LlmActionValidator(Catalog);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public abstract string Name { get; }

    public LlmActionCatalog Catalog { get; }

    public LlmTelemetry Telemetry { get; }

    public DirectorAction? SelectAction(ActionSelectionContext context)
    {
        if (context == null) return null;

        if (TryConsumePending(context, out DirectorAction? completed)) return completed;
        if (_pending != null) return null;

        // The Director does not ask for an action in Maintain, but the cadence guard documents the
        // contract for callers that drive the policy directly.
        if (string.Equals(context.StateName, "Maintain", StringComparison.Ordinal)) return null;
        if (context.CurrentTime - _lastCallTime < _options.DecisionCadenceSeconds) return null;

        _lastCallTime = context.CurrentTime;
        _pendingPrompt = BuildPrompt(context);
        _pending = RunWithRetriesAsync(_pendingPrompt);
        Telemetry.RecordSample(Name, context.CurrentTime, _pendingPrompt, BuildStateJson(context));
        return null;
    }

    /// <summary>Bounded arm falls back to the rule policy; free arm does nothing.</summary>
    protected abstract DirectorAction? OnInvalidProposal(ActionSelectionContext context, LlmValidationResult result);

    protected abstract DirectorAction? OnFailedCall(ActionSelectionContext context);

    protected static string BuildStateJson(ActionSelectionContext context)
    {
        DirectorGameState? state = context.GameState;
        return JsonSerializer.Serialize(new
        {
            state = context.StateName,
            action_type = context.ActionType == ActionType.Benefit ? "benefit" : "negative",
            available_points = Math.Round(context.AvailablePoints, 2),
            session_time = Math.Round(context.CurrentTime, 2),
            ratio = state != null ? Math.Round(state.Ratio, 3) : 1.0,
            hp_ratio = state != null ? Math.Round(state.HealthRatio, 3) : 1.0,
            kill_rate = state != null ? Math.Round(state.KillRate, 2) : 0.0,
            damage_rate = state != null ? Math.Round(state.DamageRate, 2) : 0.0,
            living_enemies = state?.LivingEnemies ?? 0
        });
    }

    private string BuildPrompt(ActionSelectionContext context)
    {
        // Static catalog first, dynamic state last: the prefix is provider-cache friendly.
        return Catalog.BuildStaticPrefix()
            + "\nCurrent state:\n"
            + BuildStateJson(context)
            + "\nAnswer with one JSON object: {\"action\":\"<id>\"}.\n";
    }

    private bool TryConsumePending(ActionSelectionContext context, out DirectorAction? action)
    {
        action = null;

        Task<LlmCallResult>? pending = _pending;
        if (pending == null || !pending.IsCompleted) return false;
        _pending = null;

        LlmCallResult result = pending.GetAwaiter().GetResult();
        bool deadlineMiss = result.LatencyMs > _options.DecisionDeadlineSeconds * 1000.0;

        if (result.Failed)
        {
            Telemetry.RecordDecision(
                Name, context.CurrentTime, context.StateName, null, null, false,
                LlmRejectionReason.Malformed, result.LatencyMs, deadlineMiss, 0, 0, result.Retries, true);
            action = OnFailedCall(context);
            return true;
        }

        int promptTokens = result.Response?.PromptTokens ?? 0;
        int completionTokens = result.Response?.CompletionTokens ?? 0;
        LlmValidationResult validation = _validator.Validate(result.Response?.Content, context);

        if (validation.IsValid)
        {
            DirectorAction? candidate = LlmActionValidator.FindCandidate(context, validation.ActionId!);
            if (candidate != null && candidate.CanExecute(context.AvailablePoints, context.CurrentTime))
            {
                Telemetry.RecordDecision(
                    Name, context.CurrentTime, context.StateName, validation.ActionId, candidate.Name, true,
                    LlmRejectionReason.None, result.LatencyMs, deadlineMiss, promptTokens, completionTokens,
                    result.Retries, false);
                action = candidate;
                return true;
            }

            validation = LlmValidationResult.Invalid(
                LlmRejectionReason.NotEligible, "the action is no longer eligible", validation.ActionId);
        }

        Telemetry.RecordDecision(
            Name, context.CurrentTime, context.StateName, validation.ActionId, null, false,
            validation.Reason, result.LatencyMs, deadlineMiss, promptTokens, completionTokens,
            result.Retries, false);
        action = OnInvalidProposal(context, validation);
        return true;
    }

    private async Task<LlmCallResult> RunWithRetriesAsync(string prompt)
    {
        DateTimeOffset started = _clock();
        int retries = 0;

        while (true)
        {
            try
            {
                LlmResponse response = await _client.CompleteAsync(
                    new LlmRequest(_options.Model, prompt, _options.Temperature, _options.MaxOutputTokens))
                    .ConfigureAwait(false);

                return new LlmCallResult
                {
                    Response = response,
                    Retries = retries,
                    LatencyMs = (_clock() - started).TotalMilliseconds
                };
            }
            catch (Exception exception)
            {
                if (retries >= _options.MaxRetries)
                {
                    return new LlmCallResult
                    {
                        Failed = true,
                        Error = exception.Message,
                        Retries = retries,
                        LatencyMs = (_clock() - started).TotalMilliseconds
                    };
                }

                retries++;
            }
        }
    }
}
