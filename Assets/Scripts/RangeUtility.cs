using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for the minion attack-range model.
/// Range is measured as Manhattan (taxicab) distance on the grid:
///   range 1 -> 4 orthogonal neighbors (the "+" shape)
///   range 2 -> 4 orthogonal-1, 4 orthogonal-2, 4 diagonal-1 (12-cell diamond)
/// </summary>
public static class RangeUtility
{
    public static int ManhattanDistance(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    public static bool IsInRange(Vector2Int from, Vector2Int to, int range)
        => ManhattanDistance(from, to) <= range;

    // Rounds world position to a grid index using the same mapping as
    // GridManager.PosToGridIndex (x = round(px), y = -round(py)). Rounding makes
    // this robust to small DOPunch animation offsets on transform.position.
    public static Vector2Int ToGridIndex(Vector3 worldPos)
        => new Vector2Int(Mathf.RoundToInt(worldPos.x), -Mathf.RoundToInt(worldPos.y));

    // All grid cells a unit occupies. Heroes are 3 wide (center column ±1) at their
    // row; every other unit is a single cell.
    public static IEnumerable<Vector2Int> GetOccupiedCells(MinionController unit)
    {
        Vector2Int c = ToGridIndex(unit.transform.position);
        if (unit is HeroController)
        {
            yield return new Vector2Int(c.x - 1, c.y);
            yield return c;
            yield return new Vector2Int(c.x + 1, c.y);
        }
        else
        {
            yield return c;
        }
    }

    // Runtime convenience: is `target` within `source`'s attack range?
    // Uses the nearest occupied cell on each side, so a 3-wide hero is reachable
    // from (and can reach) any cell adjacent to its body.
    public static bool IsInRange(MinionController source, MinionController target)
    {
        int range = source.modal.range;
        foreach (var s in GetOccupiedCells(source))
            foreach (var t in GetOccupiedCells(target))
                if (ManhattanDistance(s, t) <= range)
                    return true;
        return false;
    }

    // All non-center grid offsets within Manhattan `range` (for visualization).
    public static IEnumerable<Vector2Int> RangeOffsets(int range)
    {
        for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
                if ((dx != 0 || dy != 0) && Mathf.Abs(dx) + Mathf.Abs(dy) <= range)
                    yield return new Vector2Int(dx, dy);
    }
}
