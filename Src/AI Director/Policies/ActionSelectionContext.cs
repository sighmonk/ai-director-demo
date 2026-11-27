using System.Collections.Generic;

/// <summary>
/// Immutable snapshot of one Director action selection.
/// </summary>
public sealed class ActionSelectionContext
{
    public ActionSelectionContext(
        IReadOnlyList<DirectorAction> candidates,
        ActionType actionType,
        float availablePoints,
        double currentTime,
        string stateName,
        DirectorGameState? gameState = null)
    {
        Candidates = candidates;
        ActionType = actionType;
        AvailablePoints = availablePoints;
        CurrentTime = currentTime;
        StateName = stateName;
        GameState = gameState;
    }

    /// <summary>Actions that are already eligible for the requested type, cost and cooldown.</summary>
    public IReadOnlyList<DirectorAction> Candidates { get; }

    public ActionType ActionType { get; }

    public float AvailablePoints { get; }

    public double CurrentTime { get; }

    public string StateName { get; }

    /// <summary>Snapshot for policies that prompt a model; null when the policy needs no state.</summary>
    public DirectorGameState? GameState { get; }
}
