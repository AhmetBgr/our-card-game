using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cell shapes a card can cover, one function per shape: given the cell the caster picked, it
/// returns every index that pick actually affects.
///
/// These exist to be shared. Each selection verb in ActionHolder used to spell its shape out twice —
/// once in the lambda handed to GridCellSelectionManager.BeginSelection for the player's hover
/// highlight, and again in the areaIndexes set built after a cell is chosen — so the highlight and the
/// effect were free to drift apart. Worse, neither copy was reachable by the AI, which was left
/// guessing at a 3x3 block that matched none of the real shapes.
///
/// Indexes are returned unclamped and may fall outside the board; callers filter with
/// GridManager.IsInsideGrid (the silent check — these are expected misses, not errors).
/// </summary>
public static class CellFootprints
{
    /// <summary>Just the chosen cell. Summon placement, single-target cell effects.</summary>
    public static readonly Func<Vector2Int, IEnumerable<Vector2Int>> Single =
        center => new[] { center };

    /// <summary>The chosen cell plus its four orthogonal neighbours. Used by _SelectSmallArea.</summary>
    public static readonly Func<Vector2Int, IEnumerable<Vector2Int>> Plus =
        center => new[]
        {
            center,
            new Vector2Int(center.x + 1, center.y),
            new Vector2Int(center.x - 1, center.y),
            new Vector2Int(center.x, center.y + 1),
            new Vector2Int(center.x, center.y - 1),
        };

    /// <summary>
    /// The chosen cell and the two cells below it. Used by _SelectCollumn — note it runs one way
    /// (decreasing y) rather than spanning the column, so it is not symmetric about the pick.
    /// </summary>
    public static readonly Func<Vector2Int, IEnumerable<Vector2Int>> ThreeDown =
        center => new[]
        {
            center,
            new Vector2Int(center.x, center.y - 1),
            new Vector2Int(center.x, center.y - 2),
        };

    /// <summary>Every cell sharing the chosen cell's column, however tall the board is.</summary>
    public static readonly Func<Vector2Int, IEnumerable<Vector2Int>> FullColumn = center =>
    {
        List<Vector2Int> column = new List<Vector2Int>();

        GridManager grid = GridManager.Instance;
        if (grid == null) return column;

        for (int y = 0; y < grid.GridHeight; y++)
            column.Add(new Vector2Int(center.x, y));

        return column;
    };
}
