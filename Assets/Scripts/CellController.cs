using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellController : MonoBehaviour
{
    public SelectableEntity selectable;

    private void OnMouseDown()
    {
        if (selectable != null && !selectable.IsSelectable) return;

        if (GridCellSelectionManager.Instance != null && GridCellSelectionManager.Instance.HasActiveSession)
        {
            GridCellSelectionManager.Instance.OnCellClicked(transform);
            return;
        }

        ActionHolder.selectedcell = transform;
    }

    private void OnMouseEnter()
    {
        if (GridCellSelectionManager.Instance == null || !GridCellSelectionManager.Instance.HasActiveSession) return;

        Vector2Int index = GridManager.Instance.PosToGridIndex(transform.position);
        GridCellSelectionManager.Instance.OnCellHoverEnter(index);
    }

    private void OnMouseExit()
    {
        if (GridCellSelectionManager.Instance == null || !GridCellSelectionManager.Instance.HasActiveSession) return;

        Vector2Int index = GridManager.Instance.PosToGridIndex(transform.position);
        GridCellSelectionManager.Instance.OnCellHoverExit(index);
    }
}
