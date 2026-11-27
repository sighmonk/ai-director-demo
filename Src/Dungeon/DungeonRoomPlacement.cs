using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A free rectangle that can become a runtime room, together with the side whose opening reaches
/// the existing floor tile it was anchored to.
/// </summary>
public readonly struct RoomCandidate
{
    public RoomCandidate(Rect2I area, RoomOpeningSide openingSide)
    {
        Area = area;
        OpeningSide = openingSide;
    }

    public Rect2I Area { get; }

    public RoomOpeningSide OpeningSide { get; }
}

/// <summary>
/// Finds places where a new room can be carved without overlapping existing geometry. Scene-free so
/// the Director action can be tested without a rendered dungeon.
/// </summary>
public static class DungeonRoomPlacement
{
    private static readonly RoomOpeningSide[] SideOrder =
    {
        RoomOpeningSide.North,
        RoomOpeningSide.East,
        RoomOpeningSide.South,
        RoomOpeningSide.West
    };

    /// <summary>
    /// Candidate squares of <paramref name="minSide"/>..<paramref name="maxSide"/> tiles, anchored so
    /// the midpoint of one side is directly adjacent to an existing floor tile (a room or corridor).
    /// The area must stay one tile inside the dungeon bounds, contain no floor or occupied cell, and
    /// its centre must be at least <paramref name="minDistanceFromPlayer"/> tiles from the player.
    /// </summary>
    /// <remarks>The result is deterministic: floors are sorted, sides and sizes are enumerated in a
    /// fixed order and duplicate rectangles are dropped.</remarks>
    public static List<RoomCandidate> FindCandidates(
        IReadOnlyCollection<Vector2I> floors,
        Rect2I bounds,
        IReadOnlyCollection<Vector2I> blocked,
        Vector2I playerCell,
        int minSide,
        int maxSide,
        int minDistanceFromPlayer)
    {
        var candidates = new List<RoomCandidate>();
        if (floors == null || floors.Count == 0) return candidates;

        int lowestSide = Math.Max(DungeonWorldModel.MinRoomSide, minSide);
        int highestSide = Math.Max(lowestSide, maxSide);
        if (highestSide >= Math.Max(bounds.Size.X, bounds.Size.Y)) return candidates;

        var floorSet = floors as HashSet<Vector2I> ?? new HashSet<Vector2I>(floors);
        var blockedSet = blocked == null
            ? new HashSet<Vector2I>()
            : blocked as HashSet<Vector2I> ?? new HashSet<Vector2I>(blocked);

        var orderedFloors = new List<Vector2I>(floorSet);
        orderedFloors.Sort(CompareCells);

        var seen = new HashSet<(int X, int Y, int Side)>();

        foreach (Vector2I floor in orderedFloors)
        {
            foreach (RoomOpeningSide side in SideOrder)
            {
                for (int sideLength = lowestSide; sideLength <= highestSide; sideLength++)
                {
                    Rect2I area = AnchorRoom(floor, side, sideLength);
                    if (!seen.Add((area.Position.X, area.Position.Y, sideLength))) continue;

                    if (!HasPerimeterInsideBounds(area, bounds)) continue;
                    if (!IsAreaFree(area, floorSet, blockedSet)) continue;
                    if (IsTooCloseToPlayer(area, playerCell, minDistanceFromPlayer)) continue;

                    candidates.Add(new RoomCandidate(area, side));
                }
            }
        }

        return candidates;
    }

    private static Rect2I AnchorRoom(Vector2I floor, RoomOpeningSide side, int sideLength)
    {
        int half = sideLength / 2;
        Vector2I position;
        switch (side)
        {
            case RoomOpeningSide.North:
                position = new Vector2I(floor.X - half, floor.Y + 1);
                break;
            case RoomOpeningSide.South:
                position = new Vector2I(floor.X - half, floor.Y - sideLength);
                break;
            case RoomOpeningSide.West:
                position = new Vector2I(floor.X + 1, floor.Y - half);
                break;
            default:
                position = new Vector2I(floor.X - sideLength, floor.Y - half);
                break;
        }

        return new Rect2I(position, new Vector2I(sideLength, sideLength));
    }

    private static bool HasPerimeterInsideBounds(Rect2I area, Rect2I bounds)
    {
        return bounds.HasPoint(area.Position - Vector2I.One)
            && bounds.HasPoint(area.Position + area.Size);
    }

    private static bool IsAreaFree(Rect2I area, HashSet<Vector2I> floors, HashSet<Vector2I> blocked)
    {
        for (int y = area.Position.Y; y < area.Position.Y + area.Size.Y; y++)
        {
            for (int x = area.Position.X; x < area.Position.X + area.Size.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (floors.Contains(cell)) return false;
                if (blocked.Contains(cell)) return false;
            }
        }

        return true;
    }

    private static bool IsTooCloseToPlayer(Rect2I area, Vector2I playerCell, int minDistanceFromPlayer)
    {
        if (minDistanceFromPlayer <= 0) return false;

        var centre = new Vector2I(
            area.Position.X + (area.Size.X / 2),
            area.Position.Y + (area.Size.Y / 2));

        long dx = centre.X - playerCell.X;
        long dy = centre.Y - playerCell.Y;
        return (dx * dx) + (dy * dy) < (long)minDistanceFromPlayer * minDistanceFromPlayer;
    }

    private static int CompareCells(Vector2I left, Vector2I right)
    {
        if (left.X != right.X) return left.X.CompareTo(right.X);
        return left.Y.CompareTo(right.Y);
    }
}
