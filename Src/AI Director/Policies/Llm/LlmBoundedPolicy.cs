using System;

/// <summary>
/// Arm C: the same model and catalog, but the validator is binding. A rejected proposal falls back
/// to the rule policy and the rejection reason is recorded.
/// </summary>
public sealed class LlmBoundedPolicy : LlmPolicyBase
{
    private readonly RulePolicy _fallback;

    public LlmBoundedPolicy(
        ILlmClient client,
        LlmPolicyOptions options,
        LlmActionCatalog catalog,
        LlmTelemetry telemetry,
        Func<DateTimeOffset>? clock = null,
        RulePolicy? fallback = null)
        : base(client, options, catalog, telemetry, clock)
    {
        _fallback = fallback ?? new RulePolicy();
    }

    public override string Name => "LlmBounded";

    protected override DirectorAction? OnInvalidProposal(ActionSelectionContext context, LlmValidationResult result)
        => _fallback.SelectAction(context);

    protected override DirectorAction? OnFailedCall(ActionSelectionContext context)
        => _fallback.SelectAction(context);
}
