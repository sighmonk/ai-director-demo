/// <summary>
/// Decides which eligible Director action to execute. The Director owns filtering
/// (type, cost, cooldown) and the points economy; a policy only picks from the candidates.
/// </summary>
public interface IActionPolicy
{
    string Name { get; }

    /// <returns>The action to execute, or null when the policy declines to act.</returns>
    DirectorAction? SelectAction(ActionSelectionContext context);
}
