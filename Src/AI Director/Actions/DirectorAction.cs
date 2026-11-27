using Godot;
using System;

public enum ActionType
{
    Benefit,    // Helps the player (spends BenefitPoints)
    Negative    // Hurts the player (spends NegativePoints)
}

public abstract partial class DirectorAction : Node
{
    [Export] public ActionType ActionType = ActionType.Negative;
    [Export] public float Cost = 10.0f;
    [Export] public float Weight = 1.0f; // Higher weight = higher chance to be picked
    [Export] public float Cooldown = 5.0f;

    private double _lastUsedTime = -999.0;

    public virtual bool CanExecute(float availablePoints, double currentTime)
    {
        if (availablePoints < Cost) return false;
        if (currentTime < _lastUsedTime + Cooldown) return false;
        return true;
    }

    /// <summary>
    /// Attempts the action. The cooldown and the <c>DirectorActionExecuted</c> notification are only
    /// applied when <see cref="OnExecute"/> reports success, so a rejected action can be retried and
    /// never costs points.
    /// </summary>
    public bool Execute(double currentTime) => TryExecute(currentTime);

    public bool TryExecute(double currentTime)
    {
        if (!OnExecute()) return false;

        _lastUsedTime = currentTime;
        SignalManager.Instance.EmitSignal(nameof(SignalManager.DirectorActionExecuted), Name, Cost);
        return true;
    }

    /// <returns>True when the action had its effect; false leaves it ready to execute again.</returns>
    protected abstract bool OnExecute();
}
