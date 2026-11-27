using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System;
using System.Collections.Generic;

namespace ProjectKitchen.Tests.Dungeon;

/// <summary>
/// Passage selection for the close-passage Director action: player-room exits, enemy stranding and
/// deterministic filtering.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DungeonPassageSelectionTest
{
    private static HashSet<Vector2I> Corridor(int fromX, int toX, int y)
    {
        var floors = new HashSet<Vector2I>();
        for (int x = fromX; x <= toX; x++) floors.Add(new Vector2I(x, y));
        return floors;
    }

    private static HashSet<Vector2I> Ring(int size)
    {
        var floors = new HashSet<Vector2I>();
        for (int i = 0; i < size; i++)
        {
            floors.Add(new Vector2I(i, 0));
            floors.Add(new Vector2I(i, size - 1));
            floors.Add(new Vector2I(0, i));
            floors.Add(new Vector2I(size - 1, i));
        }

        return floors;
    }

    [TestCase]
    public void Select_ExcludesTheExitsOfThePlayerRoom()
    {
        var floors = Corridor(0, 10, 2);
        var rooms = new List<Rect2I> { new Rect2I(0, 0, 5, 5) };
        var passages = new List<DungeonPassage>
        {
            new DungeonPassage(0, new List<Vector2I> { new Vector2I(5, 2) }),
            new DungeonPassage(1, new List<Vector2I> { new Vector2I(9, 2) })
        };

        var selected = DungeonPassageSelection.Select(
            passages, floors, rooms, new Vector2I(2, 2), new Vector2I(2, 2), Array.Empty<Vector2I>());

        AssertInt(selected.Count).IsEqual(1);
        AssertInt(selected[0].Id).IsEqual(1);
    }

    [TestCase]
    public void Select_ExcludesAPassageThatWouldStrandAnEnemy()
    {
        var floors = Corridor(0, 9, 0);
        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(4, 0) });

        var selected = DungeonPassageSelection.Select(
            new List<DungeonPassage> { passage },
            floors,
            Array.Empty<Rect2I>(),
            new Vector2I(0, 0),
            new Vector2I(0, 0),
            new List<Vector2I> { new Vector2I(9, 0) });

        AssertInt(selected.Count).IsEqual(0);
    }

    [TestCase]
    public void Select_KeepsAPassageWithAnAlternateRouteForTheEnemy()
    {
        var floors = Ring(9);
        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(4, 0) });

        var selected = DungeonPassageSelection.Select(
            new List<DungeonPassage> { passage },
            floors,
            Array.Empty<Rect2I>(),
            new Vector2I(0, 0),
            new Vector2I(0, 0),
            new List<Vector2I> { new Vector2I(8, 8) });

        AssertInt(selected.Count).IsEqual(1);
    }

    [TestCase]
    public void Select_NoPassages_ReturnsEmpty()
    {
        var selected = DungeonPassageSelection.Select(
            new List<DungeonPassage>(),
            Corridor(0, 9, 0),
            Array.Empty<Rect2I>(),
            Vector2I.Zero,
            Vector2I.Zero,
            Array.Empty<Vector2I>());

        AssertInt(selected.Count).IsEqual(0);
    }
}
