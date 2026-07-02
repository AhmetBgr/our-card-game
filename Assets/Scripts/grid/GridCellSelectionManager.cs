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

    // When true (minion-summon cell pick), hovering a cell occupied by a minion previews the push it
    // will receive when the new minion lands there. _pushPreviewMinion tracks whose arrow is showing.
    private bool _previewOccupantPush;
    private MinionController _pushPreviewMinion;

    // The card driving this cell pick (if any), so every minion under the hovered area can preview whether
    // the card would kill it (skull). _skullPreviewMinions tracks whose skulls are currently lit.
    private CardSO _sourceCard;
    private readonly List<MinionController> _skullPreviewMinions = new List<MinionController>();

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
        Func<Vector2Int, IEnumerable<Vector2Int>> hoverAreaProvider,
        bool previewOccupantPush = false,
        CardSO sourceCard = null)
    {
        // Starting a cell selection preempts any active minion/attack selection (mutual preempt with
        // SelectionManager, which calls EndSelection() here when it begins a minion/attack request).
        SelectionManager.Instance.Cancel();

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
        _previewOccupantPush = previewOccupantPush;
        _sourceCard = sourceCard;
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
        _previewOccupantPush = false;
        _sourceCard = null;
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
            PreviewOccupantDeath(areaIndex);
        }

        // Summoning onto an occupied cell pushes the occupant forward — preview that push arrow.
        if (_previewOccupantPush) ShowPushPreview(index);
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

        foreach (var minion in _skullPreviewMinions)
        {
            if (minion != null) minion.HideCardDeathPreview();
        }
        _skullPreviewMinions.Clear();

        ClearPushPreview();
    }

    // If the card driving this selection would kill the minion occupying `index`, light its skull. Tracked
    // so ClearHoverPreview turns it back off when the hover moves. Non-damage cards (or empty cells) do
    // nothing, so this is safe to run for every cell-pick regardless of card type.
    private void PreviewOccupantDeath(Vector2Int index)
    {
        if (_sourceCard == null) return;
        if (GridManager.Instance == null || GridManager.Instance.IsOutSideOfGrid(index)) return;

        var cell = GridManager.Instance.GetCell(index);
        if (cell.obj != null && cell.obj.TryGetComponent(out MinionController occupant))
        {
            occupant.ShowCardDeathPreview(_sourceCard);
            _skullPreviewMinions.Add(occupant);
        }
    }

    // Show the push arrow on the minion currently occupying the hovered summon cell (if any). It gets
    // pushed forward when the new minion lands; _SelectCell only offers cells whose occupant is pushable,
    // so the arrow is white (push lands) here.
    private void ShowPushPreview(Vector2Int index)
    {
        if (GridManager.Instance == null) return;
        if (GridManager.Instance.IsOutSideOfGrid(index)) return;

        var cell = GridManager.Instance.GetCell(index);
        if (cell.obj != null && cell.obj.TryGetComponent(out MinionController occupant))
        {
            occupant.ShowPushArrow();
            _pushPreviewMinion = occupant;
        }
    }

    private void ClearPushPreview()
    {
        if (_pushPreviewMinion != null)
        {
            _pushPreviewMinion.HideMoveArrow();
            _pushPreviewMinion = null;
        }
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

