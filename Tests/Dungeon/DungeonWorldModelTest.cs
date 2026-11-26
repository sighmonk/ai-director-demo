using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectKitchen.Tests.Dungeon;

/// <summary>
/// Validated dungeon mutations: room creation, passage closing and the rollback guarantees of the
/// scene-free world model.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DungeonWorldModelTest
{
    private static readonly Rect2I Bounds = new Rect2I(0, 0, 40, 20);

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

    private static DungeonWorldModel CorridorModel()
    {
        return new DungeonWorldModel(
            Corridor(0, 30, 5),
            Bounds,
            new List<Rect2I> { new Rect2I(0, 4, 6, 3) },
            new List<Vector2I> { new Vector2I(2, 5) })
        {
            PlayerCell = new Vector2I(2, 5)
        };
    }

    #region Room creation

    [TestCase]
    public void TryCreateRoom_ConnectsToTheNearestFloor()
    {
        var model = CorridorModel();
        var area = new Rect2I(10, 7, 5, 5);

        bool created = model.TryCreateRoom(area, RoomOpeningSide.North, out List<Vector2I> added, out string failure);

        AssertBool(created).IsTrue();
        AssertString(failure).IsEqual("");
        // Five by five room plus the one-tile tunnel up to the corridor.
        AssertInt(added.Count).IsEqual(26);
        AssertInt(model.Rooms.Count).IsEqual(2);
        AssertInt(model.RoomCenters.Count).IsEqual(2);

                var center = model.RoomCenters[^1];
                AssertBool(model.IsFloor(center)).IsTrue();
                AssertBool(DungeonTopology.AreReachable(model.Floors, model.GetReferenceCell(), center)).IsTrue();
                AssertBool(model.LastCreatedRoom.HasValue).IsTrue();
                AssertObject(model.LastCreatedRoomCenter).IsEqual(center);
    }

    [TestCase]
    public void TryCreateRoom_OverlappingFloor_IsRejectedWithoutChanges()
    {
        var model = CorridorModel();
        var area = new Rect2I(10, 4, 5, 5); // overlaps the corridor row at y = 5
        int floorsBefore = model.Floors.Count;
        int roomsBefore = model.Rooms.Count;

        bool created = model.TryCreateRoom(area, RoomOpeningSide.North, out List<Vector2I> added, out string failure);

        AssertBool(created).IsFalse();
        AssertBool(failure.Length > 0).IsTrue();
        AssertInt(added.Count).IsEqual(0);
        AssertInt(model.Floors.Count).IsEqual(floorsBefore);
        AssertInt(model.Rooms.Count).IsEqual(roomsBefore);
    }

    [TestCase]
    public void TryCreateRoom_OutsideBounds_IsRejected()
    {
        var model = CorridorModel();
        var area = new Rect2I(36, 10, 5, 5);

        AssertBool(model.TryCreateRoom(area, RoomOpeningSide.North, out _, out string failure)).IsFalse();
        AssertBool(failure.Contains("bounds")).IsTrue();
    }

    [TestCase]
    public void TryCreateRoom_TooSmall_IsRejected()
    {
        var model = CorridorModel();

        AssertBool(model.TryCreateRoom(new Rect2I(10, 10, 2, 5), RoomOpeningSide.North, out _, out _)).IsFalse();
    }

    [TestCase]
    public void TryCreateRoom_OccupiedCell_IsRejected()
    {
        var model = CorridorModel();
        model.SetBlockedCells(new[] { new Vector2I(11, 9) });

        AssertBool(model.TryCreateRoom(new Rect2I(10, 7, 5, 5), RoomOpeningSide.North, out _, out string failure)).IsFalse();
        AssertBool(failure.Contains("occupied")).IsTrue();
    }

    [TestCase]
    public void TryCreateRoom_NoFloorInTunnelRange_IsRejected()
    {
        var model = CorridorModel();

        // The corridor is at y = 5; a room far below has no floor within the tunnel length.
        AssertBool(model.TryCreateRoom(new Rect2I(10, 15, 5, 4), RoomOpeningSide.South, out _, out string failure)).IsFalse();
        AssertBool(failure.Length > 0).IsTrue();
    }

    #endregion

    #region Closing passages

    [TestCase]
    public void TryClosePassage_OnALoop_RemovesTheTiles()
    {
        var ring = Ring(9);
        var model = new DungeonWorldModel(
            ring,
            new Rect2I(0, 0, 9, 9),
            new List<Rect2I>(),
            new List<Vector2I> { new Vector2I(0, 0) })
        {
            PlayerCell = new Vector2I(8, 8)
        };

        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(4, 0) });

        bool closed = model.TryClosePassage(passage.Tiles, out List<Vector2I> closedTiles, out string failure);

        AssertBool(closed).IsTrue();
        AssertString(failure).IsEqual("");
        AssertInt(closedTiles.Count).IsEqual(1);
        AssertBool(model.IsFloor(new Vector2I(4, 0))).IsFalse();
        AssertInt(model.Floors.Count).IsEqual(ring.Count - 1);
    }

    [TestCase]
    public void TryClosePassage_OnlyRoute_IsRejectedAndLeavesTheModelUntouched()
    {
        var line = Corridor(0, 9, 0);
        var model = new DungeonWorldModel(
            line,
            new Rect2I(0, 0, 10, 1),
            new List<Rect2I>(),
            new List<Vector2I> { new Vector2I(0, 0), new Vector2I(9, 0) })
        {
            PlayerCell = new Vector2I(0, 0)
        };

        bool closed = model.TryClosePassage(new[] { new Vector2I(4, 0) }, out List<Vector2I> closedTiles, out string failure);

        AssertBool(closed).IsFalse();
        AssertBool(failure.Contains("disconnect")).IsTrue();
        AssertInt(closedTiles.Count).IsEqual(0);
        AssertBool(model.IsFloor(new Vector2I(4, 0))).IsTrue();
        AssertInt(model.Floors.Count).IsEqual(line.Count);
    }

    [TestCase]
    public void TryClosePassage_CoveringARoomCentre_IsRejected()
    {
        var ring = Ring(9);
        var model = new DungeonWorldModel(
            ring,
            new Rect2I(0, 0, 9, 9),
            new List<Rect2I>(),
            new List<Vector2I> { new Vector2I(0, 0) })
        {
            PlayerCell = new Vector2I(8, 8)
        };

        AssertBool(model.TryClosePassage(new[] { new Vector2I(0, 0) }, out _, out _)).IsFalse();
    }

    [TestCase]
    public void ReopenPassage_RestoresTheExactTiles()
    {
        var ring = Ring(9);
        var model = new DungeonWorldModel(
            ring,
            new Rect2I(0, 0, 9, 9),
            new List<Rect2I>(),
            new List<Vector2I> { new Vector2I(0, 0) })
        {
            PlayerCell = new Vector2I(8, 8)
        };

        var passage = new DungeonPassage(0, new List<Vector2I> { new Vector2I(4, 0) });
        model.TryClosePassage(passage.Tiles, out List<Vector2I> closedTiles, out _);
        model.RestoreFloors(closedTiles);

        AssertInt(model.Floors.Count).IsEqual(ring.Count);
        AssertBool(model.IsFloor(new Vector2I(4, 0))).IsTrue();
    }

    [TestCase]
    public void FindClosablePassages_SingleRoute_ReturnsNothing()
    {
        var line = Corridor(0, 9, 0);
        var model = new DungeonWorldModel(
            line,
            new Rect2I(0, 0, 10, 1),
            new List<Rect2I>(),
            new List<Vector2I> { new Vector2I(0, 0), new Vector2I(9, 0) })
        {
            PlayerCell = new Vector2I(0, 0)
        };

        AssertInt(model.FindClosablePassages().Count).IsEqual(0);
    }

    [TestCase]
    public void FindClosablePassages_Loop_ReturnsPassagesThatKeepConnectivity()
    {
        var ring = Ring(9);
        var model = new DungeonWorldModel(
            ring,
            new Rect2I(0, 0, 9, 9),
            new List<Rect2I>(),
            new List<Vector2I> { new Vector2I(0, 0) })
        {
            PlayerCell = new Vector2I(8, 8)
        };

        var passages = model.FindClosablePassages();

        AssertInt(passages.Count).IsGreater(0);
        foreach (var passage in passages)
        {
            AssertBool(model.TryClosePassage(passage.Tiles, out _, out _)).IsTrue();
            model.RestoreFloors(passage.Tiles);
        }
    }

    #endregion
}
