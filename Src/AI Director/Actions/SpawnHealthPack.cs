using Godot;

/// <summary>
/// Benefit action that spawns a health pack on a reachable floor tile near the player.
/// The action fails without spending points when no valid tile exists.
/// </summary>
public partial class SpawnHealthPack : DirectorAction
{
    [Export] public float SpawnDistanceMin = 150f;
    [Export] public float SpawnDistanceMax = 450f;
    /// <summary>Preferred minimum distance from every living enemy (0 disables the preference).</summary>
    [Export] public float EnemyAggroAvoidRadius = 250f;

    public SpawnHealthPack()
    {
        ActionType = ActionType.Benefit;
    }

    protected override bool OnExecute()
    {
        var pickups = GetNodeOrNull<PickupManager>("/root/PickupManager");
        if (pickups == null)
        {
            GD.PrintErr("[Director] SpawnHealthPack rejected: PickupManager autoload is missing.");
            return false;
        }

        if (!pickups.TrySpawnNearPlayer(SpawnDistanceMin, SpawnDistanceMax, EnemyAggroAvoidRadius))
        {
            GD.Print("[Director] SpawnHealthPack rejected: no reachable floor tile available.");
            return false;
        }

        GD.Print($"[Director] Spawning a health pack near the player! (Cost: {Cost})");
        return true;
    }
}
