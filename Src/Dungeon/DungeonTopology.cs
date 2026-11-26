using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A straight cross-section of a corridor: the tiles that have to be replaced with walls to close
/// the passage.
/// </summary>
public sealed class DungeonPassage
{
    public DungeonPassage(int id, IReadOnlyList<Vector2I> tiles)
    {
        Id = id;
        Tiles = tiles;

        Vector2I sum = Vector2I.Zero;
        foreach (Vector2I tile in tiles) sum += tile;
        Center = new Vector2I(sum.X / tiles.Count, sum.Y / tiles.Count);
    }

    public int Id { get; }

    public IReadOnlyList<Vector2I> Tiles { get; }

    public Vector2I Center { get; }
}

/// <summary>
/// Pure dungeon graph analysis over a set of floor tiles: reachability, bridges and corridor
/// cross-sections that can be closed without disconnecting the dungeon.
/// </summary>
public static class DungeonTopology
{
    private static readonly Vector2I[] Directions =
    {
        new Vector2I(1, 0),
        new Vector2I(-1, 0),
        new Vector2I(0, 1),
        new Vector2I(0, -1)
    };

    /// <summary>Tiles reachable from <paramref name="start"/> using four-way movement.</summary>
    public static HashSet<Vector2I> ReachableFrom(IEnumerable<Vector2I> floors, Vector2I start)
    {
        var floorSet = floors as HashSet<Vector2I> ?? new HashSet<Vector2I>(floors);
        var visited = new HashSet<Vector2I>();
        if (!floorSet.Contains(start)) return visited;

        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector2I current = queue.Dequeue();
            foreach (Vector2I direction in Directions)
            {
                Vector2I next = current + direction;
                if (!floorSet.Contains(next)) continue;
                if (!visited.Add(next)) continue;
                queue.Enqueue(next);
            }
        }

        return visited;
    }

    public static bool AreReachable(IReadOnlyCollection<Vector2I> floors, Vector2I from, Vector2I to)
    {
        if (from == to) return true;
        return ReachableFrom(floors, from).Contains(to);
    }

    public static bool AreAllReachable(IReadOnlyCollection<Vector2I> floors, Vector2I from, IEnumerable<Vector2I> targets)
    {
        var reachable = ReachableFrom(floors, from);
        foreach (Vector2I target in targets)
        {
            if (!reachable.Contains(target)) return false;
        }

        return true;
    }

    /// <summary>
    /// Bridges of the tile graph (iterative Tarjan): tile pairs whose single removal disconnects the
    /// floor layout. Sorted for deterministic output.
    /// </summary>
    public static List<(Vector2I A, Vector2I B)> FindBridges(IReadOnlyCollection<Vector2I> floors)
    {
        var bridges = new List<(Vector2I A, Vector2I B)>();
        if (floors == null || floors.Count == 0) return bridges;

        var floorSet = floors as HashSet<Vector2I> ?? new HashSet<Vector2I>(floors);
        var ordered = new List<Vector2I>(floorSet);
        ordered.Sort(CompareCells);

        var discovery = new Dictionary<Vector2I, int>(floorSet.Count);
        var low = new Dictionary<Vector2I, int>(floorSet.Count);
        var parent = new Dictionary<Vector2I, Vector2I>(floorSet.Count);
        int time = 0;

        foreach (Vector2I root in ordered)
        {
            if (discovery.ContainsKey(root)) continue;

            var stack = new Stack<(Vector2I Node, int Direction)>();
            discovery[root] = ++time;
            low[root] = discovery[root];
            stack.Push((root, 0));

            while (stack.Count > 0)
            {
                var (node, directionIndex) = stack.Pop();

                if (directionIndex < Directions.Length)
                {
                    stack.Push((node, directionIndex + 1));

                    Vector2I neighbour = node + Directions[directionIndex];
                    if (!floorSet.Contains(neighbour)) continue;

                    if (!discovery.ContainsKey(neighbour))
                    {
                        parent[neighbour] = node;
                        discovery[neighbour] = ++time;
                        low[neighbour] = discovery[neighbour];
                        stack.Push((neighbour, 0));
                    }
                    else if (!parent.TryGetValue(node, out Vector2I nodeParent) || neighbour != nodeParent)
                    {
                        low[node] = Math.Min(low[node], discovery[neighbour]);
                    }

                    continue;
                }

                if (!parent.TryGetValue(node, out Vector2I parentNode)) continue;

                low[parentNode] = Math.Min(low[parentNode], low[node]);
                if (low[node] > discovery[parentNode])
                {
                    bridges.Add((parentNode, node));
                }
            }
        }

        return bridges;
    }

    /// <summary>
    /// Corridor cross-sections: maximal runs of at most <paramref name="maxWidth"/> floor tiles
    /// perpendicular to a corridor that is at least <paramref name="minCorridorLength"/> tiles long.
    /// Tiles inside a room rectangle are never part of a passage.
    /// </summary>
    public static List<DungeonPassage> FindPassages(
        IReadOnlyCollection<Vector2I> floors,
        IReadOnlyCollection<Rect2I> rooms,
        int maxWidth = 3,
        int minCorridorLength = 4)
    {
        var passages = new List<DungeonPassage>();
        if (floors == null || floors.Count == 0) return passages;

        var floorSet = floors as HashSet<Vector2I> ?? new HashSet<Vector2I>(floors);
        var ordered = new List<Vector2I>(floorSet);
        ordered.Sort(CompareCells);

        var seen = new HashSet<string>();

        foreach (Vector2I tile in ordered)
        {
            if (IsInsideAnyRoom(tile, rooms)) continue;

            foreach (Vector2I corridorDirection in new[] { new Vector2I(1, 0), new Vector2I(0, 1) })
            {
                Vector2I widthDirection = new Vector2I(corridorDirection.Y, corridorDirection.X);

                int width = 1
                    + CountRun(floorSet, tile, widthDirection)
                    + CountRun(floorSet, tile, -widthDirection);
                if (width > maxWidth) continue;

                int length = 1
                    + CountRun(floorSet, tile, corridorDirection)
                    + CountRun(floorSet, tile, -corridorDirection);
                if (length < minCorridorLength) continue;

                int before = CountRun(floorSet, tile, -widthDirection);
                int after = CountRun(floorSet, tile, widthDirection);

                var section = new List<Vector2I>(width);
                for (int offset = -before; offset <= after; offset++)
                {
                    section.Add(tile + (widthDirection * offset));
                }

                section.Sort(CompareCells);
                string signature = Signature(section);
                if (!seen.Add(signature)) continue;

                passages.Add(new DungeonPassage(passages.Count, section));
            }
        }

        return passages;
    }

    /// <summary>
    /// True when replacing the passage tiles with walls keeps the reference cell and every room
    /// centre mutually reachable.
    /// </summary>
    public static bool IsClosable(
        IReadOnlyCollection<Vector2I> floors,
        DungeonPassage passage,
        Vector2I referenceCell,
        IEnumerable<Vector2I> roomCenters)
    {
        if (passage == null || passage.Tiles.Count == 0) return false;

        var remaining = new HashSet<Vector2I>(floors);
        foreach (Vector2I tile in passage.Tiles) remaining.Remove(tile);

        var centers = new List<Vector2I>();
        foreach (Vector2I center in roomCenters)
        {
            if (!remaining.Contains(center)) return false;
            centers.Add(center);
        }

        if (!remaining.Contains(referenceCell)) return false;
        if (centers.Count == 0) return remaining.Count > 0;

        return AreAllReachable(remaining, referenceCell, centers);
    }

    private static int CountRun(HashSet<Vector2I> floors, Vector2I origin, Vector2I direction)
    {
        int count = 0;
        Vector2I current = origin;
        while (true)
        {
            current += direction;
            if (!floors.Contains(current)) return count;
            count++;
        }
    }

    private static bool IsInsideAnyRoom(Vector2I tile, IReadOnlyCollection<Rect2I> rooms)
    {
        if (rooms == null) return false;
        foreach (Rect2I room in rooms)
        {
            if (room.HasPoint(tile)) return true;
        }

        return false;
    }

    private static string Signature(IReadOnlyList<Vector2I> tiles)
    {
        var parts = new string[tiles.Count];
        for (int i = 0; i < tiles.Count; i++) parts[i] = $"{tiles[i].X},{tiles[i].Y}";
        return string.Join("|", parts);
    }

    private static int CompareCells(Vector2I left, Vector2I right)
    {
        if (left.X != right.X) return left.X.CompareTo(right.X);
        return left.Y.CompareTo(right.Y);
    }
}
