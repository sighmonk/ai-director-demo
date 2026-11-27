using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Filters corridor passages that may be closed from the current game state: the exits of the room
/// the player stands in and passages whose closure would strand an enemy are removed.
/// </summary>
public static class DungeonPassageSelection
{
    public static List<DungeonPassage> Select(
        IEnumerable<DungeonPassage> passages,
        IReadOnlyCollection<Vector2I> floors,
        IReadOnlyCollection<Rect2I> rooms,
        Vector2I playerCell,
        Vector2I referenceCell,
        IReadOnlyCollection<Vector2I> enemyCells)
    {
        var selected = new List<DungeonPassage>();
        if (passages == null) return selected;

        Rect2I? playerRoom = FindRoomContaining(rooms, playerCell);

        foreach (DungeonPassage passage in passages)
        {
            if (playerRoom.HasValue && TouchesRoom(passage, playerRoom.Value)) continue;
            if (WouldStrandEnemy(floors, passage, referenceCell, enemyCells)) continue;

            selected.Add(passage);
        }

        return selected;
    }

    /// <summary>Room rectangle that contains the cell, or null when the player is in a corridor.</summary>
    public static Rect2I? FindRoomContaining(IReadOnlyCollection<Rect2I> rooms, Vector2I cell)
    {
        if (rooms == null) return null;
        foreach (Rect2I room in rooms)
        {
            if (room.HasPoint(cell)) return room;
        }

        return null;
    }

    /// <summary>
    /// True when the passage crosses the one-tile band around the room, which is where its exits
    /// live. Passages are never inside room interiors, so only the exits match.
    /// </summary>
    public static bool TouchesRoom(DungeonPassage passage, Rect2I room)
    {
        if (passage == null) return false;
        var expanded = new Rect2I(room.Position - Vector2I.One, room.Size + new Vector2I(2, 2));

        foreach (Vector2I tile in passage.Tiles)
        {
            if (expanded.HasPoint(tile)) return true;
        }

        return false;
    }

    /// <summary>
    /// Simulates the closure and checks that every living enemy stays reachable from the reference
    /// cell. An enemy standing on a passage tile counts as stranded.
    /// </summary>
    public static bool WouldStrandEnemy(
        IReadOnlyCollection<Vector2I> floors,
        DungeonPassage passage,
        Vector2I referenceCell,
        IReadOnlyCollection<Vector2I> enemyCells)
    {
        if (enemyCells == null || enemyCells.Count == 0) return false;
        if (passage == null || passage.Tiles.Count == 0) return false;

        var remaining = new HashSet<Vector2I>(floors);
        foreach (Vector2I tile in passage.Tiles) remaining.Remove(tile);

        var reachable = DungeonTopology.ReachableFrom(remaining, referenceCell);
        foreach (Vector2I enemyCell in enemyCells)
        {
            if (!reachable.Contains(enemyCell)) return true;
        }

        return false;
    }
}
