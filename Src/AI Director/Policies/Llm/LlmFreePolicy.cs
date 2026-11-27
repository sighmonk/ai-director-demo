using System;

/// <summary>
/// Arm B: the model may propose any id. An invalid proposal is recorded and nothing is executed;
/// the proposal is never repaired silently.
/// </summary>
public sealed class LlmFreePolicy : LlmPolicyBase
{
    public LlmFreePolicy(
        ILlmClient client,
        LlmPolicyOptions options,
        LlmActionCatalog catalog,
        LlmTelemetry telemetry,
        Func<DateTimeOffset>? clock = null)
        : base(client, options, catalog, telemetry, clock)
    {
    }

    public override string Name => "LlmFree";

    protected override DirectorAction? OnInvalidProposal(ActionSelectionContext context, LlmValidationResult result) => null;

    protected override DirectorAction? OnFailedCall(ActionSelectionContext context) => null;
}
