using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System;
using System.Collections.Generic;

namespace ProjectKitchen.Tests.Dungeon;

/// <summary>
/// Candidate search for runtime rooms: adjacency, free space, player distance and determinism.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DungeonRoomPlacementTest
{
    private static readonly Rect2I Bounds = new Rect2I(0, 0, 40, 20);

    private static HashSet<Vector2I> Corridor(int fromX, int toX, int y)
    {
        var floors = new HashSet<Vector2I>();
        for (int x = fromX; x <= toX; x++) floors.Add(new Vector2I(x, y));
        return floors;
    }

    private static Vector2I OpeningNeighbour(RoomCandidate candidate)
    {
        Rect2I area = candidate.Area;
        int centerX = area.Position.X + (area.Size.X / 2);
        int centerY = area.Position.Y + (area.Size.Y / 2);

        return candidate.OpeningSide switch
        {
            RoomOpeningSide.North => new Vector2I(centerX, area.Position.Y - 1),
            RoomOpeningSide.South => new Vector2I(centerX, area.Position.Y + area.Size.Y),
            RoomOpeningSide.West => new Vector2I(area.Position.X - 1, centerY),
            _ => new Vector2I(area.Position.X + area.Size.X, centerY)
        };
    }

    [TestCase]
    public void FindCandidates_ReturnsRoomsAdjacentToExistingFloor()
    {
        var floors = Corridor(0, 30, 5);

        var candidates = DungeonRoomPlacement.FindCandidates(
            floors, Bounds, Array.Empty<Vector2I>(), new Vector2I(2, 5), 5, 6, 0);

        AssertInt(candidates.Count).IsGreater(0);
        foreach (RoomCandidate candidate in candidates)
        {
            AssertBool(floors.Contains(OpeningNeighbour(candidate))).IsTrue();
            AssertBool(Bounds.HasPoint(candidate.Area.Position - Vector2I.One)).IsTrue();
            AssertBool(Bounds.HasPoint(candidate.Area.Position + candidate.Area.Size)).IsTrue();

            for (int y = candidate.Area.Position.Y; y < candidate.Area.Position.Y + candidate.Area.Size.Y; y++)
            {
                for (int x = candidate.Area.Position.X; x < candidate.Area.Position.X + candidate.Area.Size.X; x++)
                {
                    AssertBool(floors.Contains(new Vector2I(x, y))).IsFalse();
                }
            }
        }
    }

    [TestCase]
    public void FindCandidates_RejectsRoomsTooCloseToThePlayer()
    {
        var floors = Corridor(0, 30, 5);

        var candidates = DungeonRoomPlacement.FindCandidates(
            floors, Bounds, Array.Empty<Vector2I>(), new Vector2I(15, 5), 5, 5, 20);

        AssertInt(candidates.Count).IsEqual(0);
    }

    [TestCase]
    public void FindCandidates_RejectsOccupiedArea()
    {
        var floors = new HashSet<Vector2I> { new Vector2I(10, 10) };
        var blocked = new List<Vector2I>();
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                var cell = new Vector2I(x, y);
                if (!floors.Contains(cell)) blocked.Add(cell);
            }
        }

        var candidates = DungeonRoomPlacement.FindCandidates(
            floors, new Rect2I(0, 0, 20, 20), blocked, Vector2I.Zero, 5, 5, 0);

        AssertInt(candidates.Count).IsEqual(0);
    }

    [TestCase]
    public void FindCandidates_RejectsAreaWithoutSpaceInsideBounds()
    {
        var floors = new HashSet<Vector2I> { new Vector2I(4, 4) };

        var candidates = DungeonRoomPlacement.FindCandidates(
            floors, new Rect2I(0, 0, 8, 8), Array.Empty<Vector2I>(), Vector2I.Zero, 5, 5, 0);

        AssertInt(candidates.Count).IsEqual(0);
    }

    [TestCase]
    public void FindCandidates_IsDeterministicRegardlessOfFloorOrder()
    {
        var floors = Corridor(0, 30, 5);
        var reversed = new List<Vector2I>(floors);
        reversed.Reverse();

        var first = DungeonRoomPlacement.FindCandidates(floors, Bounds, Array.Empty<Vector2I>(), new Vector2I(2, 5), 5, 6, 4);
        var second = DungeonRoomPlacement.FindCandidates(reversed, Bounds, Array.Empty<Vector2I>(), new Vector2I(2, 5), 5, 6, 4);

        AssertInt(first.Count).IsEqual(second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            AssertObject(first[i].Area).IsEqual(second[i].Area);
            AssertObject(first[i].OpeningSide).IsEqual(second[i].OpeningSide);
        }
    }
}
