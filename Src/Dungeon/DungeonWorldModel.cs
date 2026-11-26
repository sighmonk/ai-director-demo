using Godot;
using System.Collections.Generic;
using System.Linq;

public enum RoomOpeningSide
{
    North,
    South,
    West,
    East
}

/// <summary>
/// Mutable, scene-free model of the dungeon. Mutations are validated and verified for reachability
/// before they are committed, so a rejected mutation leaves the model untouched.
/// </summary>
public sealed class DungeonWorldModel
{
    public const int MinRoomSide = 3;
    public const int MaxTunnelLength = 40;
    private static readonly Vector2I[] Directions =
    {
        new Vector2I(1, 0),
        new Vector2I(-1, 0),
        new Vector2I(0, 1),
        new Vector2I(0, -1)
    };

    private readonly HashSet<Vector2I> _floors;
    private readonly HashSet<Vector2I> _blocked = new HashSet<Vector2I>();

    public DungeonWorldModel(
        IEnumerable<Vector2I> floorTiles,
        Rect2I bounds,
        IEnumerable<Rect2I> rooms,
        IEnumerable<Vector2I> roomCenters,
        IEnumerable<Vector2I> blockedCells = null)
    {
        _floors = new HashSet<Vector2I>(floorTiles);
        Bounds = bounds;
        Rooms = new List<Rect2I>(rooms);
        RoomCenters = new List<Vector2I>(roomCenters);

        if (blockedCells != null)
        {
            foreach (Vector2I cell in blockedCells) _blocked.Add(cell);
        }
    }

    public Rect2I Bounds { get; }

    public IReadOnlyCollection<Vector2I> Floors => _floors;

    public List<Rect2I> Rooms { get; }

    public List<Vector2I> RoomCenters { get; }

    /// <summary>Last room accepted by <see cref="TryCreateRoom"/>, in tile coordinates.</summary>
    public Rect2I? LastCreatedRoom { get; private set; }

    public Vector2I LastCreatedRoomCenter { get; private set; }

    public Vector2I PlayerCell { get; set; }

    /// <summary>Cells occupied by the player or an enemy; mutations may not cover them.</summary>
    public IReadOnlyCollection<Vector2I> BlockedCells => _blocked;

    public bool IsFloor(Vector2I cell) => _floors.Contains(cell);

    public bool IsBlocked(Vector2I cell) => _blocked.Contains(cell);

    public bool IsInsideBounds(Vector2I cell) => Bounds.HasPoint(cell);

    public void SetBlockedCells(IEnumerable<Vector2I> cells)
    {
        _blocked.Clear();
        if (cells == null) return;

        foreach (Vector2I cell in cells) _blocked.Add(cell);
    }

    public Vector2I GetReferenceCell()
    {
        if (_floors.Contains(PlayerCell)) return PlayerCell;
        if (RoomCenters.Count > 0) return RoomCenters[0];
        return _floors.Count > 0 ? _floors.First() : Vector2I.Zero;
    }

    /// <summary>
    /// Carves a room and a single tunnel to the nearest existing floor on
    /// <paramref name="openingSide"/>. Rejects overlapping, out-of-bounds and occupied areas and
    /// rolls the carve back when the room would not be reachable.
    /// </summary>
    public bool TryCreateRoom(
        Rect2I floorArea,
        RoomOpeningSide openingSide,
        out List<Vector2I> addedFloors,
        out string failure)
    {
        addedFloors = new List<Vector2I>();
        failure = string.Empty;
        LastCreatedRoom = null;

        if (floorArea.Size.X < MinRoomSide || floorArea.Size.Y < MinRoomSide)
        {
            failure = $"Room must be at least {MinRoomSide} x {MinRoomSide} tiles.";
            return false;
        }

        // Keep one tile of wall around the room inside the dungeon bounds.
        if (!IsInsideBounds(floorArea.Position - Vector2I.One) || !IsInsideBounds(floorArea.Position + floorArea.Size))
        {
            failure = "Room would leave the dungeon bounds.";
            return false;
        }

        var roomCells = new List<Vector2I>(floorArea.Size.X * floorArea.Size.Y);
        for (int y = floorArea.Position.Y; y < floorArea.Position.Y + floorArea.Size.Y; y++)
        {
            for (int x = floorArea.Position.X; x < floorArea.Position.X + floorArea.Size.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (_floors.Contains(cell))
                {
                    failure = $"Area overlaps existing floor at {cell}.";
                    return false;
                }

                if (_blocked.Contains(cell))
                {
                    failure = $"Area is occupied by an entity at {cell}.";
                    return false;
                }

                roomCells.Add(cell);
            }
        }

        if (!TryBuildTunnel(floorArea, openingSide, out List<Vector2I> tunnel, out failure)) return false;

        foreach (Vector2I cell in roomCells) _floors.Add(cell);
        foreach (Vector2I cell in tunnel) _floors.Add(cell);
        addedFloors.AddRange(roomCells);
        addedFloors.AddRange(tunnel);

        var newCenter = new Vector2I(
            floorArea.Position.X + (floorArea.Size.X / 2),
            floorArea.Position.Y + (floorArea.Size.Y / 2));

        var targets = new List<Vector2I>(RoomCenters) { newCenter };
        if (!DungeonTopology.AreAllReachable(_floors, GetReferenceCell(), targets))
        {
            foreach (Vector2I cell in addedFloors) _floors.Remove(cell);
            addedFloors.Clear();
            failure = "Room would not be reachable from the rest of the dungeon.";
            return false;
        }

        Rooms.Add(floorArea);
        RoomCenters.Add(newCenter);
        LastCreatedRoom = floorArea;
        LastCreatedRoomCenter = newCenter;
        return true;
    }

    /// <summary>
    /// Replaces a passage with walls when the rest of the dungeon stays fully reachable.
    /// </summary>
    public bool TryClosePassage(
        IReadOnlyList<Vector2I> passageTiles,
        out List<Vector2I> closedTiles,
        out string failure)
    {
        closedTiles = new List<Vector2I>();
        failure = string.Empty;

        if (passageTiles == null || passageTiles.Count == 0)
        {
            failure = "Passage has no tiles.";
            return false;
        }

        foreach (Vector2I cell in passageTiles)
        {
            if (!_floors.Contains(cell))
            {
                failure = $"Tile {cell} is not a floor tile.";
                return false;
            }

            if (_blocked.Contains(cell))
            {
                failure = $"Tile {cell} is occupied by an entity.";
                return false;
            }
        }

        var remaining = new HashSet<Vector2I>(_floors);
        foreach (Vector2I cell in passageTiles) remaining.Remove(cell);

        Vector2I reference = GetReferenceCell();
        if (!remaining.Contains(reference))
        {
            failure = "Closing the passage would cover the player or the reference room.";
            return false;
        }

        foreach (Vector2I center in RoomCenters)
        {
            if (!remaining.Contains(center))
            {
                failure = "Closing the passage would cover a room centre.";
                return false;
            }
        }

        if (!DungeonTopology.AreAllReachable(remaining, reference, RoomCenters))
        {
            failure = "Closing the passage would disconnect the dungeon.";
            return false;
        }

        foreach (Vector2I cell in passageTiles)
        {
            _floors.Remove(cell);
            closedTiles.Add(cell);
        }

        return true;
    }

    /// <summary>Restores the exact tiles removed by a previous close.</summary>
    public void RestoreFloors(IEnumerable<Vector2I> tiles)
    {
        if (tiles == null) return;
        foreach (Vector2I cell in tiles) _floors.Add(cell);
    }

    /// <summary>Passages that keep every room centre reachable when closed.</summary>
    public List<DungeonPassage> FindClosablePassages()
    {
        var closable = new List<DungeonPassage>();
        if (RoomCenters.Count == 0) return closable;

        Vector2I reference = GetReferenceCell();
        foreach (DungeonPassage passage in DungeonTopology.FindPassages(_floors, Rooms))
        {
            if (passage.Tiles.Contains(reference)) continue;
            if (DungeonTopology.IsClosable(_floors, passage, reference, RoomCenters)) closable.Add(passage);
        }

        return closable;
    }

    private bool TryBuildTunnel(
        Rect2I floorArea,
        RoomOpeningSide openingSide,
        out List<Vector2I> tunnel,
        out string failure)
    {
        tunnel = new List<Vector2I>();
        failure = string.Empty;

        int centerX = floorArea.Position.X + (floorArea.Size.X / 2);
        int centerY = floorArea.Position.Y + (floorArea.Size.Y / 2);

        Vector2I start;
        Vector2I direction;
        switch (openingSide)
        {
            case RoomOpeningSide.North:
                start = new Vector2I(centerX, floorArea.Position.Y);
                direction = new Vector2I(0, -1);
                break;
            case RoomOpeningSide.South:
                start = new Vector2I(centerX, floorArea.Position.Y + floorArea.Size.Y - 1);
                direction = new Vector2I(0, 1);
                break;
            case RoomOpeningSide.West:
                start = new Vector2I(floorArea.Position.X, centerY);
                direction = new Vector2I(-1, 0);
                break;
            default:
                start = new Vector2I(floorArea.Position.X + floorArea.Size.X - 1, centerY);
                direction = new Vector2I(1, 0);
                break;
        }

        Vector2I current = start;
        for (int step = 0; step < MaxTunnelLength; step++)
        {
            current += direction;

            if (!IsInsideBounds(current))
            {
                failure = "Room has no room for a tunnel inside the dungeon bounds.";
                return false;
            }

            if (_floors.Contains(current)) return true;
            if (_blocked.Contains(current))
            {
                failure = $"Tunnel would run through an occupied tile at {current}.";
                return false;
            }

            tunnel.Add(current);
        }

        failure = "No existing floor found within the tunnel length.";
        return false;
    }
}
