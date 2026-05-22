using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridCellSelectionManager : MonoBehaviour
{
    public static GridCellSelectionManager Instance { get; private set; }

    private readonly HashSet<Vector2Int> _selectableIndexes = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _hoverPreviewIndexes = new HashSet<Vector2Int>();
    private Func<Vector2Int, IEnumerable<Vector2Int>> _hoverAreaProvider;

    public bool HasActiveSession => _selectableIndexes.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void BeginSelection(
        IEnumerable<Vector2Int> selectableIndexes,
        Func<Vector2Int, IEnumerable<Vector2Int>> hoverAreaProvider)
    {
        EndSelection();

        if (selectableIndexes != null)
        {
            foreach (var index in selectableIndexes)
            {
                _selectableIndexes.Add(index);
                SetCellSelectable(index, true);
            }
        }

        _hoverAreaProvider = hoverAreaProvider;
    }

    public void EndSelection()
    {
        ClearHoverPreview();

        foreach (var index in _selectableIndexes)
        {
            SetCellSelectable(index, false);
        }

        _selectableIndexes.Clear();
        _hoverAreaProvider = null;
    }

    public void OnCellHoverEnter(Vector2Int index)
    {
        if (!HasActiveSession) return;
        if (!_selectableIndexes.Contains(index)) return;
        if (_hoverAreaProvider == null) return;

        ClearHoverPreview();

        IEnumerable<Vector2Int> area = _hoverAreaProvider.Invoke(index);
        if (area == null) return;

        foreach (var areaIndex in area)
        {
            //if (!_selectableIndexes.Contains(areaIndex)) continue;
            _hoverPreviewIndexes.Add(areaIndex);
            SetCellHoverPreview(areaIndex, true);
        }
    }

    public void OnCellHoverExit(Vector2Int index)
    {
        if (!HasActiveSession) return;
        if (!_selectableIndexes.Contains(index)) return;
        ClearHoverPreview();
    }

    public void OnCellClicked(Transform cellTransform)
    {
        if (!HasActiveSession) return;
        if (cellTransform == null) return;

        Vector2Int index = GridManager.Instance.PosToGridIndex(cellTransform.position);
        if (!_selectableIndexes.Contains(index)) return;

        var cellController = cellTransform.GetComponent<CellController>();
        if (cellController != null && cellController.selectable != null && !cellController.selectable.IsSelectable)
        {
            return;
        }

        ActionHolder.selectedcell = cellTransform;
    }

    private void ClearHoverPreview()
    {
        foreach (var index in _hoverPreviewIndexes)
        {
            SetCellHoverPreview(index, false);
        }

        _hoverPreviewIndexes.Clear();
    }

    private static void SetCellSelectable(Vector2Int index, bool value)
    {
        if (GridManager.Instance == null) return;
        if (GridManager.Instance.IsOutSideOfGrid(index)) return;

        var cell = GridManager.Instance.GetCell(index);
        if (cell.cellObj == null) return;

        var controller = cell.cellObj.GetComponent<CellController>();
        if (controller == null || controller.selectable == null) return;

        controller.selectable.SetSelectable(value);
    }

    private static void SetCellHoverPreview(Vector2Int index, bool value)
    {
        if (GridManager.Instance == null) return;
        if (GridManager.Instance.IsOutSideOfGrid(index)) return;

        var cell = GridManager.Instance.GetCell(index);
        if (cell.cellObj == null) return;

        var controller = cell.cellObj.GetComponent<CellController>();
        if (controller == null || controller.selectable == null) return;

        controller.selectable.SetHoverPreview(value);
    }
}

