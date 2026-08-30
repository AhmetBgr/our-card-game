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

    // The cell the current hover preview was built from, if any. Unity delivers OnMouseEnter on the
    // newly hovered collider BEFORE OnMouseExit on the one just left (see the same note on
    // MinionRangeHandler.ShowTargetsInRange), and board cells tile with no gap between them — so the
    // outgoing cell's exit lands AFTER the incoming cell has already drawn its area, and an
    // unconditional clear there tore the fresh preview straight back down. Result: the area highlight
    // survived only the first hover after the cursor entered the board, and every cell-to-cell move
    // left the board blank. Only the cell that owns the preview may clear it.
    private Vector2Int _hoverOriginIndex;
    private bool _hasHoverOrigin;

    // Freezes the open session's hover and click handling while leaving its highlights on screen. Set by
    // ActionHolder.AwaitCellSelection to hold an AI-chosen cell up long enough to be seen: the session
    // must stay open for the preview to render, which leaves every cell live, and without this the
    // player could hover the preview off the opponent's pick or click a different cell out from under
    // it. Cleared by BeginSelection/EndSelection, so it cannot outlive the pick that set it.
    private bool _inputLocked;

    [Tooltip("Tutorial-only hint shown while a cell pick is open — but only for the picks whose verb " +
             "supplies wording (see the prompt argument on BeginSelection), so a spell's area pick stays " +
             "silent while the summon pick explains itself. Optional: leave empty for no hint at all.")]
    [SerializeField] private TutorialPrompt prompt;

    public bool HasActiveSession => _selectableIndexes.Count > 0;

    /// <summary>See <see cref="_inputLocked"/>. Only meaningful while a session is open.</summary>
    public void SetInputLocked(bool value) => _inputLocked = value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <param name="promptMessage">
    /// Wording for the tutorial hint to show while this pick is open, or null for no hint. Passed by the
    /// verb rather than derived here, because only the verb knows what it is asking the player FOR — the
    /// selectable set alone cannot tell a summon placement apart from a spell's area.
    /// </param>
    public void BeginSelection(
        IEnumerable<Vector2Int> selectableIndexes,
        Func<Vector2Int, IEnumerable<Vector2Int>> hoverAreaProvider,
        bool previewOccupantPush = false,
        CardSO sourceCard = null,
        string promptMessage = null)
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
        _inputLocked = false;

        // After the EndSelection() above, which hides whatever the preempted pick was saying. Show() kills
        // that fade out rather than queueing behind it, so back-to-back picks don't blink.
        if (prompt != null && !string.IsNullOrEmpty(promptMessage)) prompt.Show(promptMessage);
    }

    public void EndSelection()
    {
        ClearHoverPreview();

        // Every teardown path runs through here — resolved, cancelled, or preempted by a minion/attack
        // request — so the hint cannot outlive the pick it belongs to.
        if (prompt != null) prompt.Hide();

        foreach (var index in _selectableIndexes)
        {
            SetCellSelectable(index, false);
        }

        _selectableIndexes.Clear();
        _hoverAreaProvider = null;
        _previewOccupantPush = false;
        _sourceCard = null;
        _inputLocked = false;
    }

    public void OnCellHoverEnter(Vector2Int index)
    {
        if (!HasActiveSession) return;
        if (_inputLocked) return;
        if (!_selectableIndexes.Contains(index)) return;
        if (_hoverAreaProvider == null) return;

        ClearHoverPreview();

        IEnumerable<Vector2Int> area = _hoverAreaProvider.Invoke(index);
        if (area == null) return;

        _hoverOriginIndex = index;
        _hasHoverOrigin = true;

        foreach (var areaIndex in area)
        {
            //if (!_selectableIndexes.Contains(areaIndex)) continue;

            // Footprints come back unclamped, and on this board every cell but the centre has a plus arm
            // hanging off the edge. Filtered here with the SILENT bounds check: IsOutSideOfGrid logs two
            // lines per miss, and each off-grid arm otherwise reached it three times per hover (preview
            // on, death preview, preview off) — console flood and a visible editor hitch on every move.
            if (GridManager.Instance == null || !GridManager.Instance.IsInsideGrid(areaIndex)) continue;

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
        if (_inputLocked) return;
        if (!_selectableIndexes.Contains(index)) return;

        // Not the cell the preview belongs to — its exit arrived after a newer cell already took over.
        // See _hoverOriginIndex.
        if (!_hasHoverOrigin || _hoverOriginIndex != index) return;

        ClearHoverPreview();
    }

    public void OnCellClicked(Transform cellTransform)
    {
        if (!HasActiveSession) return;
        if (_inputLocked) return;
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

    /// <summary>
    /// Route a click that landed on whatever is STANDING on a cell to the cell itself. A minion's
    /// BoxCollider2D is exactly one cell at exactly the cell's z and is never disabled, so during a cell
    /// pick the two colliders are coincident and which one Unity's mouse dispatch returns is decided by
    /// physics broadphase order, not by us. Without this, every click that happened to land on the minion
    /// was silently dropped — and on a corner cell, where only three plus-shape centers exist and all of
    /// them can be occupied, that made the occupant untargetable outright.
    /// Returns true if an active cell session consumed the click.
    /// </summary>
    public bool TryClickCellAt(Vector2Int index)
    {
        Transform cell = ResolveSessionCell(index);
        if (cell == null) return false;

        OnCellClicked(cell);
        return true;
    }

    /// <summary>Hover counterpart of <see cref="TryClickCellAt"/>, so the area preview lights the same
    /// way whichever collider won the pick.</summary>
    public bool TryHoverCellAt(Vector2Int index)
    {
        if (ResolveSessionCell(index) == null) return false;

        OnCellHoverEnter(index);
        return true;
    }

    /// <summary>Hover-exit counterpart of <see cref="TryHoverCellAt"/>.</summary>
    public bool TryHoverExitCellAt(Vector2Int index)
    {
        if (ResolveSessionCell(index) == null) return false;

        OnCellHoverExit(index);
        return true;
    }

    /// <summary>
    /// The cell transform at `index`, but only if it belongs to the active session. Heroes live off the
    /// grid, so their index misses the selectable set and never reaches the bounds check at all — and the
    /// bounds check itself is the SILENT one, since an off-grid probe here is expected, not an error.
    /// </summary>
    private Transform ResolveSessionCell(Vector2Int index)
    {
        if (!HasActiveSession) return null;
        if (!_selectableIndexes.Contains(index)) return null;
        if (GridManager.Instance == null || !GridManager.Instance.IsInsideGrid(index)) return null;

        var cell = GridManager.Instance.GetCell(index);
        return cell.cellObj != null ? cell.cellObj.transform : null;
    }

    private void ClearHoverPreview()
    {
        _hasHoverOrigin = false;

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
        if (GridManager.Instance == null || !GridManager.Instance.IsInsideGrid(index)) return;

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
        if (!GridManager.Instance.IsInsideGrid(index)) return;

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
        if (!GridManager.Instance.IsInsideGrid(index)) return;

        var cell = GridManager.Instance.GetCell(index);
        if (cell.cellObj == null) return;

        var controller = cell.cellObj.GetComponent<CellController>();
        if (controller == null || controller.selectable == null) return;

        controller.selectable.SetSelectable(value);
    }

    private static void SetCellHoverPreview(Vector2Int index, bool value)
    {
        if (GridManager.Instance == null) return;
        if (!GridManager.Instance.IsInsideGrid(index)) return;

        var cell = GridManager.Instance.GetCell(index);
        if (cell.cellObj == null) return;

        var controller = cell.cellObj.GetComponent<CellController>();
        if (controller == null || controller.selectable == null) return;

        controller.selectable.SetHoverPreview(value);
    }
}

