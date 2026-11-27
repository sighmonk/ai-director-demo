using System;
using System.Text.Json;

public enum LlmRejectionReason
{
    None,
    Malformed,
    UnknownAction,
    NotEligible,
    Cost,
    Cooldown
}

public sealed class LlmValidationResult
{
    private LlmValidationResult(bool isValid, string? actionId, LlmRejectionReason reason, string? detail)
    {
        IsValid = isValid;
        ActionId = actionId;
        Reason = reason;
        Detail = detail;
    }

    public bool IsValid { get; }

    public string? ActionId { get; }

    public LlmRejectionReason Reason { get; }

    public string? Detail { get; }

    public static LlmValidationResult Valid(string actionId) => new(true, actionId, LlmRejectionReason.None, null);

    public static LlmValidationResult Invalid(LlmRejectionReason reason, string detail, string? actionId = null)
        => new(false, actionId, reason, detail);
}

/// <summary>
/// Validates a model response against the catalog and the current selection context. The same
/// validator serves arm B (result recorded) and arm C (result enforced).
/// </summary>
public sealed class LlmActionValidator
{
    private readonly LlmActionCatalog _catalog;

    public LlmActionValidator(LlmActionCatalog catalog)
    {
        _catalog = catalog;
    }

    public LlmValidationResult Validate(string? response, ActionSelectionContext context)
    {
        if (context == null) return LlmValidationResult.Invalid(LlmRejectionReason.NotEligible, "no selection context");
        if (string.IsNullOrWhiteSpace(response)) return LlmValidationResult.Invalid(LlmRejectionReason.Malformed, "empty response");

        string actionId;
        try
        {
            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return LlmValidationResult.Invalid(LlmRejectionReason.Malformed, "response is not an object");

            int properties = 0;
            string? candidate = null;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                properties++;
                if (property.Name != "action" || property.Value.ValueKind != JsonValueKind.String)
                {
                    return LlmValidationResult.Invalid(LlmRejectionReason.Malformed, "expected a single string property \"action\"");
                }

                candidate = property.Value.GetString();
            }

            if (properties != 1 || string.IsNullOrWhiteSpace(candidate))
            {
                return LlmValidationResult.Invalid(LlmRejectionReason.Malformed, "expected a single string property \"action\"");
            }

            actionId = candidate!;
        }
        catch (JsonException)
        {
            return LlmValidationResult.Invalid(LlmRejectionReason.Malformed, "response is not valid JSON");
        }

        if (!_catalog.Contains(actionId))
        {
            return LlmValidationResult.Invalid(LlmRejectionReason.UnknownAction, $"'{actionId}' is not in the catalog", actionId);
        }

        DirectorAction? action = FindCandidate(context, actionId);
        if (action == null)
        {
            return LlmValidationResult.Invalid(LlmRejectionReason.NotEligible, $"'{actionId}' is not eligible in the current state", actionId);
        }

        if (!action.CanExecute(context.AvailablePoints, context.CurrentTime))
        {
            LlmRejectionReason reason = context.AvailablePoints < action.Cost
                ? LlmRejectionReason.Cost
                : LlmRejectionReason.Cooldown;
            return LlmValidationResult.Invalid(reason, $"'{actionId}' cannot execute now", actionId);
        }

        return LlmValidationResult.Valid(actionId);
    }

    public static DirectorAction? FindCandidate(ActionSelectionContext context, string actionId)
    {
        foreach (DirectorAction candidate in context.Candidates)
        {
            if (candidate != null && candidate.Name == actionId) return candidate;
        }

        return null;
    }
}
