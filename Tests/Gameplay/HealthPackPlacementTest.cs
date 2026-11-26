using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System;
using System.Collections.Generic;

namespace ProjectKitchen.Tests.Gameplay;

/// <summary>
/// Placement rules for health packs: distance ring around the player, floor/reachability filtering
/// and the player cell exclusion.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class HealthPackPlacementTest
{
    private const int TileSize = 16;
    private static readonly Vector2I PlayerCell = new Vector2I(0, 0);

    private static List<Vector2I> Candidates(params Vector2I[] cells) => new List<Vector2I>(cells);

    #region Distance ring

    [TestCase]
    public void IsWithinRing_PlayerCell_IsRejected()
    {
        AssertBool(HealthPackPlacement.IsWithinRing(PlayerCell, PlayerCell, 0f, 500f, TileSize)).IsFalse();
    }

    [TestCase]
    public void IsWithinRing_InsideBounds_IsAccepted()
    {
        // 10 tiles along X = 160 px.
        var cell = new Vector2I(10, 0);

        AssertBool(HealthPackPlacement.IsWithinRing(cell, PlayerCell, 100f, 200f, TileSize)).IsTrue();
    }

    [TestCase]
    public void IsWithinRing_BoundaryValues_AreInclusive()
    {
        var atMin = new Vector2I(0, 5);   // 80 px
        var atMax = new Vector2I(0, -10); // 160 px

        AssertBool(HealthPackPlacement.IsWithinRing(atMin, PlayerCell, 80f, 160f, TileSize)).IsTrue();
        AssertBool(HealthPackPlacement.IsWithinRing(atMax, PlayerCell, 80f, 160f, TileSize)).IsTrue();
    }

    [TestCase]
    public void IsWithinRing_TooCloseOrTooFar_IsRejected()
    {
        var tooClose = new Vector2I(2, 0);  // 32 px
        var tooFar = new Vector2I(30, 0);   // 480 px

        AssertBool(HealthPackPlacement.IsWithinRing(tooClose, PlayerCell, 80f, 160f, TileSize)).IsFalse();
        AssertBool(HealthPackPlacement.IsWithinRing(tooFar, PlayerCell, 80f, 160f, TileSize)).IsFalse();
    }

    #endregion

    #region Selection

    [TestCase]
    public void TrySelectCell_NoCandidates_ReturnsFalse()
    {
        var random = new Random(7);

        bool selected = HealthPackPlacement.TrySelectCell(
            Candidates(), PlayerCell, 0f, 500f, TileSize, _ => true, random, out _);

        AssertBool(selected).IsFalse();
    }

    [TestCase]
    public void TrySelectCell_SkipsUnacceptableCandidates()
    {
        var wall = new Vector2I(6, 0);
        var floor = new Vector2I(10, 0);
        var random = new Random(3);

        bool selected = HealthPackPlacement.TrySelectCell(
            Candidates(wall, floor),
            PlayerCell,
            80f,
            200f,
            TileSize,
            cell => cell == floor,
            random,
            out Vector2I result);

        AssertBool(selected).IsTrue();
        AssertObject(result).IsEqual(floor);
    }

    [TestCase]
    public void TrySelectCell_SkipsCandidatesOutsideTheRing()
    {
        var tooFar = new Vector2I(40, 0);
        var inRange = new Vector2I(12, 0);
        var random = new Random(11);

        bool selected = HealthPackPlacement.TrySelectCell(
            Candidates(tooFar, inRange),
            PlayerCell,
            80f,
            200f,
            TileSize,
            _ => true,
            random,
            out Vector2I result);

        AssertBool(selected).IsTrue();
        AssertObject(result).IsEqual(inRange);
    }

    [TestCase]
    public void TrySelectCell_NoAcceptableCandidate_ReturnsFalse()
    {
        var random = new Random(5);

        bool selected = HealthPackPlacement.TrySelectCell(
            Candidates(new Vector2I(10, 0), new Vector2I(12, 0)),
            PlayerCell,
            80f,
            200f,
            TileSize,
            _ => false,
            random,
            out _);

        AssertBool(selected).IsFalse();
    }

    [TestCase]
    public void TrySelectCell_SameSeed_PicksSameCell()
    {
        var candidates = Candidates(
            new Vector2I(10, 0),
            new Vector2I(0, 10),
            new Vector2I(-10, 0),
            new Vector2I(0, -10),
            new Vector2I(8, 8));

        bool first = HealthPackPlacement.TrySelectCell(
            candidates, PlayerCell, 40f, 200f, TileSize, _ => true, new Random(2024), out Vector2I firstCell);

        bool second = HealthPackPlacement.TrySelectCell(
            candidates, PlayerCell, 40f, 200f, TileSize, _ => true, new Random(2024), out Vector2I secondCell);

        AssertBool(first).IsTrue();
        AssertBool(second).IsTrue();
        AssertObject(firstCell).IsEqual(secondCell);
    }

    #endregion
}
