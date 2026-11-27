using Godot;

/// <summary>
/// Benefit action that opens a small room next to an existing corridor or room and places one health
/// pack in its centre.
/// </summary>
public partial class AddRewardRoom : AddRoomAction
{
    public AddRewardRoom()
    {
        ActionType = ActionType.Benefit;
    }

    public override string Kind => "reward";

    protected override bool HasPrerequisites(out string failure)
    {
        if (GetNodeOrNull<PickupManager>("/root/PickupManager")?.HealthPackScene == null)
        {
            failure = "PickupManager has no health pack scene assigned.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    protected override int PlaceContents(DungeonWorldModel model, Rect2I room, Vector2I center)
    {
        var pickups = GetNodeOrNull<PickupManager>("/root/PickupManager");
        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (pickups == null || enemies == null) return 0;

        var position = enemies.CellCenterGlobal(center);
        if (pickups.SpawnHealthPackAt(position) == null)
        {
            GD.PrintErr($"[Director] AddRewardRoom: the room was created but the health pack failed.");
            return 0;
        }

        return 1;
    }
}
