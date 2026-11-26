using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Pure placement rules for health packs. The caller owns the tilemap and navigation queries and
/// produces candidate cells; this class decides whether a candidate is acceptable.
/// </summary>
public static class HealthPackPlacement
{
    /// <summary>
    /// True when <paramref name="cell"/> lies inside the distance ring around the player cell.
    /// The player cell itself is never valid.
    /// </summary>
    public static bool IsWithinRing(
        Vector2I cell,
        Vector2I playerCell,
        float minDistancePixels,
        float maxDistancePixels,
        int tileSize)
    {
        if (cell == playerCell) return false;

        float dx = (cell.X - playerCell.X) * (float)tileSize;
        float dy = (cell.Y - playerCell.Y) * (float)tileSize;
        float distance = Mathf.Sqrt((dx * dx) + (dy * dy));

        return distance >= minDistancePixels && distance <= maxDistancePixels;
    }

    /// <summary>
    /// Picks the first acceptable candidate from a random starting index so repeated placements do
    /// not always return the same tile.
    /// </summary>
    public static bool TrySelectCell(
        IReadOnlyList<Vector2I> candidates,
        Vector2I playerCell,
        float minDistancePixels,
        float maxDistancePixels,
        int tileSize,
        Func<Vector2I, bool>? isAcceptable,
        Random random,
        out Vector2I selected)
    {
        selected = default;
        if (candidates == null || candidates.Count == 0) return false;

        int start = random.Next(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2I cell = candidates[(start + i) % candidates.Count];

            if (!IsWithinRing(cell, playerCell, minDistancePixels, maxDistancePixels, tileSize)) continue;
            if (isAcceptable != null && !isAcceptable(cell)) continue;

            selected = cell;
            return true;
        }

        return false;
    }
}
