using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Applies validated dungeon mutations to the live tilemaps: repaints the changed tiles, refreshes
/// the display layer, rebuilds the enemy navigation grid and emits <c>DungeonChanged</c>.
/// Nothing is written when the model rejects a mutation.
/// </summary>
public partial class DungeonMutator : Node2D
{
    private static readonly Vector2I[] NeighbourOffsets =
    {
        new Vector2I(1, 0),
        new Vector2I(-1, 0),
        new Vector2I(0, 1),
        new Vector2I(0, -1)
    };

    [Export] public DungeonGenerator? Generator;

    private DungeonWorldModel? _model;

    public DungeonWorldModel? Model => _model;

    public override void _Ready()
    {
        Generator ??= GetParentOrNull<DungeonGenerator>();
    }

    /// <summary>
    /// Builds the model from the current tilemaps the first time it is needed. Returns null when the
    /// dungeon has not been rendered yet.
    /// </summary>
    public DungeonWorldModel? EnsureModel()
    {
        if (_model != null) return _model;

        Generator ??= GetParentOrNull<DungeonGenerator>();
        if (Generator?.WorldTileMap == null)
        {
            GD.PrintErr("[DungeonMutator] WorldTileMap is not available yet.");
            return null;
        }

        _model = new DungeonWorldModel(
            Generator.GetFloorTiles(),
            Generator.WorldTileMap.GetUsedRect(),
            Generator.GetRoomTileRects(),
            Generator.GetRoomTileCenters());
        SyncBlockedCells();

        return _model;
    }

    /// <summary>
    /// Ensures the model exists and refreshes the cells occupied by the player and living enemies.
    /// </summary>
    public DungeonWorldModel? RefreshModel()
    {
        var model = EnsureModel();
        if (model == null) return null;

        SyncBlockedCells();
        return model;
    }

    public bool TryCreateRoom(
        Rect2I floorArea,
        RoomOpeningSide openingSide,
        out List<Vector2I> addedFloors,
        out string failure)
    {
        addedFloors = new List<Vector2I>();
        if (EnsureModel() == null)
        {
            failure = "Dungeon model is not available.";
            return false;
        }

        SyncBlockedCells();
        if (!_model!.TryCreateRoom(floorArea, openingSide, out addedFloors, out failure)) return false;

        PaintFloors(addedFloors);
        if (_model.LastCreatedRoom.HasValue)
        {
            Generator?.RegisterRuntimeRoom(_model.LastCreatedRoom.Value, _model.LastCreatedRoomCenter);
        }

        RefreshAfterMutation();
        return true;
    }

    public bool TryClosePassage(
        DungeonPassage passage,
        out List<Vector2I> closedTiles,
        out string failure)
    {
        closedTiles = new List<Vector2I>();
        if (passage == null)
        {
            failure = "No passage given.";
            return false;
        }

        if (EnsureModel() == null)
        {
            failure = "Dungeon model is not available.";
            return false;
        }

        SyncBlockedCells();
        if (!_model!.TryClosePassage(passage.Tiles, out closedTiles, out failure)) return false;

        PaintWalls(closedTiles);
        RefreshAfterMutation();
        return true;
    }

    /// <summary>Restores the exact tiles of a closed passage.</summary>
    public bool ReopenPassage(IReadOnlyList<Vector2I> tiles)
    {
        if (tiles == null || tiles.Count == 0) return false;
        if (EnsureModel() == null) return false;

        _model!.RestoreFloors(tiles);
        PaintFloors(tiles);
        RefreshAfterMutation();
        return true;
    }

    public List<DungeonPassage> GetClosablePassages(bool avoidPlayerRoom = false, bool avoidStrandingEnemies = false)
    {
        if (EnsureModel() == null) return new List<DungeonPassage>();

        SyncBlockedCells();
        List<DungeonPassage> passages = _model!.FindClosablePassages();
        if (!avoidPlayerRoom && !avoidStrandingEnemies) return passages;

        Vector2I playerCell = _model.PlayerCell;
        var player = GetNodeOrNull<GameManager>("/root/GameManager")?.Player;
        if (player != null && IsInstanceValid(player)) playerCell = WorldToCell(player.GlobalPosition);

        var enemyCells = avoidStrandingEnemies ? CollectEnemyCells() : new List<Vector2I>();
        var rooms = avoidPlayerRoom ? _model.Rooms : new List<Rect2I>();

        return DungeonPassageSelection.Select(
            passages,
            _model.Floors,
            rooms,
            playerCell,
            _model.GetReferenceCell(),
            enemyCells);
    }

    private List<Vector2I> CollectEnemyCells()
    {
        var cells = new List<Vector2I>();
        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (enemies == null) return cells;

        foreach (Enemy enemy in enemies.ActiveEnemies)
        {
            if (!IsInstanceValid(enemy)) continue;
            cells.Add(WorldToCell(enemy.GlobalPosition));
        }

        return cells;
    }

    private void SyncBlockedCells()
    {
        if (_model == null) return;

        var blocked = new List<Vector2I>();
        var player = GameManager.Instance?.Player;
        Vector2I playerCell = default;
        bool hasPlayer = player != null && IsInstanceValid(player);
        if (hasPlayer)
        {
            playerCell = WorldToCell(player!.GlobalPosition);
            blocked.Add(playerCell);
        }

        foreach (Enemy enemy in EnemyManager.Instance.ActiveEnemies)
        {
            if (!IsInstanceValid(enemy)) continue;
            blocked.Add(WorldToCell(enemy.GlobalPosition));
        }

        _model.SetBlockedCells(blocked);
        if (hasPlayer) _model.PlayerCell = playerCell;
    }

    private Vector2I WorldToCell(Vector2 globalPosition)
    {
        var tileMap = Generator!.WorldTileMap!;
        return tileMap.LocalToMap(tileMap.ToLocal(globalPosition));
    }

    private void PaintFloors(IEnumerable<Vector2I> floors)
    {
        var world = Generator!.WorldTileMap!;
        foreach (Vector2I cell in floors)
        {
            world.SetCell(cell, 0, Generator.FloorAtlasCoord);

            // Keep the new floor sealed: any neighbour that is not floor becomes a wall.
            foreach (Vector2I offset in NeighbourOffsets)
            {
                Vector2I neighbour = cell + offset;
                if (_model != null && _model.IsFloor(neighbour)) continue;
                world.SetCell(neighbour, 0, Generator.WallAtlasCoord);
            }
        }
    }

    private void PaintWalls(IEnumerable<Vector2I> walls)
    {
        var world = Generator!.WorldTileMap!;
        foreach (Vector2I cell in walls)
        {
            world.SetCell(cell, 0, Generator.WallAtlasCoord);
        }
    }

    private void RefreshAfterMutation()
    {
        var generator = Generator!;
        var display = generator.DisplayTileMap;
        var placer = generator.TilePlacer;
        if (display != null && placer != null && generator.WorldTileMap != null)
        {
            placer.UpdateDisplayTiles(generator.WorldTileMap, display);
        }

        EnemyManager.Instance.RebuildNavigation();
        SignalManager.Instance?.EmitSignal(nameof(SignalManager.DungeonChanged));
    }
}
