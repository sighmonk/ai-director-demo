using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectKitchen.Tests.Dungeon;

/// <summary>
/// Reachability, bridge detection and corridor cross-section analysis on hand-built floor layouts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DungeonTopologyTest
{
    private static HashSet<Vector2I> Line(int length)
    {
        var floors = new HashSet<Vector2I>();
        for (int x = 0; x < length; x++) floors.Add(new Vector2I(x, 0));
        return floors;
    }

    /// <summary>Border ring of a square: two independent routes between any two border tiles.</summary>
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

    private static string Signature(IEnumerable<Vector2I> tiles)
        => string.Join("|", tiles.Select(tile => $"{tile.X},{tile.Y}").OrderBy(text => text));

    #region Reachability

    [TestCase]
    public void ReachableFrom_Line_VisitsEveryTile()
    {
        var floors = Line(6);

        var reachable = DungeonTopology.ReachableFrom(floors, new Vector2I(0, 0));

        AssertInt(reachable.Count).IsEqual(6);
    }

    [TestCase]
    public void AreAllReachable_DisconnectedSection_ReturnsFalse()
    {
        var floors = Line(3);
        floors.Add(new Vector2I(10, 0));
        var targets = new[] { new Vector2I(2, 0), new Vector2I(10, 0) };

        AssertBool(DungeonTopology.AreAllReachable(floors, new Vector2I(0, 0), targets)).IsFalse();
    }

    [TestCase]
    public void AreAllReachable_StartOutsideFloors_ReturnsFalse()
    {
        var floors = Line(3);

        AssertBool(DungeonTopology.AreAllReachable(floors, new Vector2I(50, 50), new[] { new Vector2I(1, 0) })).IsFalse();
    }

    #endregion

    #region Bridges

    [TestCase]
    public void FindBridges_SingleTileCorridor_EveryInternalEdgeIsABridge()
    {
        var bridges = DungeonTopology.FindBridges(Line(5));

        AssertInt(bridges.Count).IsEqual(4);
    }

    [TestCase]
    public void FindBridges_Loop_HasNoBridges()
    {
        var bridges = DungeonTopology.FindBridges(Ring(4));

        AssertInt(bridges.Count).IsEqual(0);
    }

    [TestCase]
    public void FindBridges_IsDeterministic()
    {
        var floors = Line(6);
        floors.Add(new Vector2I(3, 1));

        var first = DungeonTopology.FindBridges(floors);
        var second = DungeonTopology.FindBridges(floors);

        AssertInt(first.Count).IsEqual(second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            AssertObject(first[i]).IsEqual(second[i]);
        }
    }

    #endregion

    #region Passages

    [TestCase]
    public void FindPassages_TwoWideCorridor_ReturnsCrossSectionsOfTwoTiles()
    {
        var floors = new HashSet<Vector2I>();
        for (int x = 4; x <= 19; x++)
        {
            floors.Add(new Vector2I(x, 1));
            floors.Add(new Vector2I(x, 2));
        }

        var rooms = new List<Rect2I> { new Rect2I(0, 0, 4, 4), new Rect2I(20, 0, 4, 4) };

        var passages = DungeonTopology.FindPassages(floors, rooms);

        AssertInt(passages.Count).IsGreater(0);
        AssertBool(passages.All(passage => passage.Tiles.Count == 2)).IsTrue();
        AssertBool(passages.All(passage => passage.Tiles.All(tile => !rooms.Any(room => room.HasPoint(tile))))).IsTrue();
    }

    [TestCase]
    public void FindPassages_SkippedInsideRooms()
    {
        var floors = new HashSet<Vector2I>();
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++) floors.Add(new Vector2I(x, y));
        }

        var rooms = new List<Rect2I> { new Rect2I(0, 0, 6, 6) };

        var passages = DungeonTopology.FindPassages(floors, rooms);

        AssertInt(passages.Count).IsEqual(0);
    }

    [TestCase]
    public void FindPassages_IsDeterministic()
    {
        var floors = new HashSet<Vector2I>();
        for (int x = 4; x <= 19; x++)
        {
            floors.Add(new Vector2I(x, 1));
            floors.Add(new Vector2I(x, 2));
        }

        var rooms = new List<Rect2I>();

        var first = DungeonTopology.FindPassages(floors, rooms);
        var second = DungeonTopology.FindPassages(floors, rooms);

        AssertInt(first.Count).IsEqual(second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            AssertString(Signature(first[i].Tiles)).IsEqual(Signature(second[i].Tiles));
        }
    }

    #endregion

    #region Closability

    [TestCase]
    public void IsClosable_OnlyRoute_ReturnsFalse()
    {
        var floors = Line(6);
        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(3, 0) });
        var centers = new[] { new Vector2I(0, 0), new Vector2I(5, 0) };

        AssertBool(DungeonTopology.IsClosable(floors, passage, new Vector2I(0, 0), centers)).IsFalse();
    }

    [TestCase]
    public void IsClosable_AlternativeRoute_ReturnsTrue()
    {
        var floors = Ring(9);
        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(4, 0) });
        var centers = new[] { new Vector2I(0, 0) };

        AssertBool(DungeonTopology.IsClosable(floors, passage, new Vector2I(8, 8), centers)).IsTrue();
    }

    [TestCase]
    public void IsClosable_PassageCoversReferenceCell_ReturnsFalse()
    {
        var floors = Ring(9);
        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(8, 8) });

        AssertBool(DungeonTopology.IsClosable(floors, passage, new Vector2I(8, 8), new[] { new Vector2I(0, 0) })).IsFalse();
    }

    [TestCase]
    public void IsClosable_PassageCoversRoomCentre_ReturnsFalse()
    {
        var floors = Ring(9);
        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(0, 0) });

        AssertBool(DungeonTopology.IsClosable(floors, passage, new Vector2I(8, 8), new[] { new Vector2I(0, 0) })).IsFalse();
    }

    #endregion
}
