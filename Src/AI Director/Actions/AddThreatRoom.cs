using Godot;
using System.Collections.Generic;

/// <summary>
/// Negative action that opens a small room next to an existing corridor or room and fills it with
/// enemies. Every enemy is spawned on a floor tile inside the new room.
/// </summary>
public partial class AddThreatRoom : AddRoomAction
{
    [Export(PropertyHint.Range, "1,10,1")] public int EnemiesPerRoom = 3;

    public AddThreatRoom()
    {
        ActionType = ActionType.Negative;
    }

    public override string Kind => "threat";

    protected override bool HasPrerequisites(out string failure)
    {
        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (enemies?.EnemyScenes == null || enemies.EnemyScenes.Count == 0)
        {
            failure = "EnemyManager has no enemy scenes assigned.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    protected override int PlaceContents(DungeonWorldModel model, Rect2I room, Vector2I center)
    {
        List<Vector2I> cells = FloorCellsInside(model, room);
        if (cells.Count == 0) return 0;

        var enemyManager = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (enemyManager == null) return 0;

        int spawned = 0;
        float directorValue = 0f;

        for (int i = 0; i < EnemiesPerRoom; i++)
        {
            // Spread the enemies over the room instead of stacking them on one tile.
            int index = (int)(((i + 0.5f) * cells.Count) / EnemiesPerRoom);
            Vector2I cell = cells[Mathf.Clamp(index, 0, cells.Count - 1)];
            PackedScene scene = enemyManager.EnemyScenes[i % enemyManager.EnemyScenes.Count];

            Enemy? enemy = enemyManager.SpawnEnemyAt(scene, enemyManager.CellCenterGlobal(cell));
            if (enemy == null) continue;

            spawned++;
            directorValue += enemy.DifficultyValue;
        }

        var data = GetNodeOrNull<DataManager>("/root/DataManager");
        data?.RegisterDirectorSpawn(directorValue);
        return spawned;
    }
}