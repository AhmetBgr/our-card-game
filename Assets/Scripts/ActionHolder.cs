using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public enum SelectionType
{
    Any, Minion, Card, Cell
}

[CreateAssetMenu(fileName = "ActionHolder", menuName = "New ActionHolder")]

public class ActionHolder : ScriptableObject
{
    public static bool cancelRequested = false;

    public static Transform selectedcell = null;
    public static List<Transform> selectedCells = new List<Transform>();
    public static List<MinionController> selectedMinions = new List<MinionController>();
    public static List<MinionController> selectedTargetMinions = new List<MinionController>();
    public static List<CardController> selectedCards = new List<CardController>();

    public static MinionController selectedMinion = null;
    public static Agent selectedAgent = null;
    // The minion that just entered play, set by GameManager while broadcasting OnAnyMinionSummoned so
    // reaction verbs (e.g. the crossbow's auto-attack) can target the new arrival specifically.
    public static MinionController summonedMinion = null;
    // The card the player (or AI) picked out of a "choose one of N" prompt — the sink CardChoice resolves
    // into, and what the AI writes directly from OnWaitingCardChoice. Same shape as selectedMinion.
    public static CardSO chosenCard = null;
    public static MinionController thisMinion = null;
    public static CardSO thisCardSO = null;
    public static CardController thisCard = null;

    public static int DiedMinionAmount = 0;

    public static Queue<IEnumerator> curActionsList = new Queue<IEnumerator>();

    public sealed class Snapshot
    {
        private readonly bool _cancelRequested;
        private readonly Transform _selectedCell;
        private readonly List<Transform> _selectedCells;
        private readonly List<MinionController> _selectedMinions;
        private readonly List<CardController> _selectedCards;
        private readonly MinionController _selectedMinion;
        private readonly List<MinionController> _selectedTargetMinions;
        private readonly Agent _selectedAgent;
        private readonly MinionController _thisMinion;
        private readonly CardSO _thisCardSO;
        private readonly CardController _thisCard;
        private readonly CardSO _chosenCard;
        private readonly Queue<IEnumerator> _curActionsList;
        private readonly int _diedMinionAmount = 0;

        internal Snapshot(
            bool cancelRequested,
            Transform selectedCell,
            List<Transform> selectedCells,
            List<MinionController> selectedMinions,
            List<CardController> selectedCards,
            MinionController selectedMinion,
            List<MinionController> selectedTargetMinions,
            Agent selectedAgent,
            MinionController thisMinion,
            CardSO thisCardSO,
            CardController thisCard,
            CardSO chosenCard,
            Queue<IEnumerator> curActionsList, int diedMinionAmount)
        {
            _chosenCard = chosenCard;
            _cancelRequested = cancelRequested;
            _selectedCell = selectedCell;
            _selectedCells = selectedCells;
            _selectedMinions = selectedMinions;
            _selectedCards = selectedCards;
            _selectedMinion = selectedMinion;
            _selectedTargetMinions = selectedTargetMinions;
            _selectedAgent = selectedAgent;
            _thisMinion = thisMinion;
            _thisCardSO = thisCardSO;
            _thisCard = thisCard;
            _curActionsList = curActionsList;
            _diedMinionAmount = diedMinionAmount;
        }

        public void Restore()
        {
            ActionHolder.cancelRequested = _cancelRequested;
            ActionHolder.selectedcell = _selectedCell;
            ActionHolder.selectedCells = new List<Transform>(_selectedCells);
            ActionHolder.selectedMinions = new List<MinionController>(_selectedMinions);
            ActionHolder.selectedCards = new List<CardController>(_selectedCards);
            ActionHolder.selectedMinion = _selectedMinion;
            ActionHolder.selectedTargetMinions = new List<MinionController>(_selectedTargetMinions);
            ActionHolder.selectedAgent = _selectedAgent;
            ActionHolder.thisMinion = _thisMinion;
            ActionHolder.thisCardSO = _thisCardSO;
            ActionHolder.thisCard = _thisCard;
            ActionHolder.chosenCard = _chosenCard;
            ActionHolder.curActionsList = new Queue<IEnumerator>(_curActionsList);
            ActionHolder.DiedMinionAmount = _diedMinionAmount;
        }
    }

    public static Snapshot TakeSnapshot()
    {
        return new Snapshot(
            cancelRequested,
            selectedcell,
            new List<Transform>(selectedCells),
            new List<MinionController>(selectedMinions),
            new List<CardController>(selectedCards),
            selectedMinion,
            new List<MinionController>(selectedTargetMinions),
            selectedAgent,
            thisMinion,
            thisCardSO,
            thisCard,
            chosenCard,
            new Queue<IEnumerator>(curActionsList),
            DiedMinionAmount);
    }

    /// <summary>
    /// Snapshots the current selection state (plus GameManager.isTesting) and restores it on Dispose.
    /// Use with a `using` block around triggered-action execution so a thrown exception or early
    /// return can't leak partial selection state into the outer play.
    /// </summary>
    public static IDisposable PushScope()
    {
        return new Scope(TakeSnapshot(),
            GameManager.Instance != null ? GameManager.Instance.isTesting : false);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Snapshot _snapshot;
        private readonly bool _isTesting;
        private bool _disposed;

        public Scope(Snapshot snapshot, bool isTesting)
        {
            _snapshot = snapshot;
            _isTesting = isTesting;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _snapshot.Restore();
            if (GameManager.Instance != null)
                GameManager.Instance.isTesting = _isTesting;
        }
    }

    /// <summary>
    /// Clears all per-play selection state. Call at the start of each new card-play or triggered-action
    /// execution so callers can't forget a field.
    /// </summary>
    public static void ResetSelections()
    {
        selectedcell = null;
        selectedMinion = null;
        selectedAgent = null;
        summonedMinion = null;
        chosenCard = null;
        selectedMinions.Clear();
        selectedTargetMinions.Clear();
        selectedCells.Clear();
        selectedCards.Clear();
        currentCellFootprint = null;
    }

    /// <summary>
    /// The footprint of the cell selection currently being awaited: hand it a candidate cell index and
    /// it returns every index that choosing that cell would affect. Set immediately before
    /// OnWaitingCellSelect fires and cleared once the selection closes.
    ///
    /// Exists for the AI. The event carries the selectable cells and the source card, neither of which
    /// says what shape the card covers — so MinMaxBrain used to score a guessed 3x3 block and pick area
    /// spells almost at random. It is a static alongside thisCardSO and selectedcell rather than an
    /// extra event arg because every OnWaitingCellSelect handler would otherwise have to change.
    ///
    /// Null means "shape unknown"; scorers should fall back to the chosen cell alone rather than guess.
    /// </summary>
    public static Func<Vector2Int, IEnumerable<Vector2Int>> currentCellFootprint;

    public static event Action<SelectableParameters> OnSelect;
    public static event Action<List<Transform>, CardSO> OnWaitingCellSelect;
    public static event Action<List<MinionController>, CardSO> OnWaitingMinionSelect;
    // Raised when a "choose one of N cards" prompt opens. The player answers through CardChoice/the
    // panel; the AI answers by writing chosenCard straight from its handler, exactly as it does for the
    // cell and minion prompts. Args: the options offered, and the card that asked.
    public static event Action<List<CardSO>, CardSO> OnWaitingCardChoice;

    /// <summary>
    /// Wipes everything on this class that outlives a scene. Called from <see cref="GameManager.Awake"/>,
    /// so every match starts from a blank slate however the previous one ended.
    ///
    /// The events matter most. They are static, and their only subscribers (the AI's PlayTurn) attach for
    /// the duration of one action and detach in a coroutine `finally` — which Unity does NOT run when the
    /// owning object is destroyed. Restarting a match mid-AI-turn therefore used to strand the AI's
    /// handlers here, and they then answered the PLAYER's cell/minion prompts the instant those opened:
    /// the summon highlight flashed and the minion landed wherever a dead agent's brain scored best,
    /// with no chance to click. The AI now also unsubscribes in OnDestroy; this is the backstop that keeps
    /// any future subscriber from reintroducing the same bug.
    ///
    /// Nothing subscribes before GameManager.Awake — the AI only attaches from inside PlayTurn, which
    /// GameManager.Start kicks off — so clearing here cannot drop a live handler.
    /// </summary>
    public static void ResetForNewMatch()
    {
        OnSelect = null;
        OnWaitingCellSelect = null;
        OnWaitingMinionSelect = null;
        OnWaitingCardChoice = null;

        ResetSelections();

        cancelRequested = false;
        thisMinion = null;
        thisCard = null;
        thisCardSO = null;
        DiedMinionAmount = 0;
        curActionsList = new Queue<IEnumerator>();
    }

    #region SELECTION

    private static Transform GetCellTransform(Vector2Int index)
    {
        var cell = GridManager.Instance.GetCell(index);
        return cell.cellObj != null ? cell.cellObj.transform : null;
    }

    public void SelectThisAgent()
    {
        //Debug.Log("adding select agent to list: " + curActionsList);
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_SelectThisAgent());
    }
    public IEnumerator _SelectThisAgent()
    {
        selectedAgent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent;
        Debug.Log("selected agent: " + selectedAgent.name);
        yield return null;

    }
    public void SelectFriendlyAgent()
    {
        Debug.Log("adding select agent to list: " + curActionsList);

        curActionsList.Enqueue(_SelectThisMinion());

        curActionsList.Enqueue(_SelectFriendlyAgent());
    }
    public IEnumerator _SelectFriendlyAgent()
    {

        selectedAgent = thisMinion.owner;
        Debug.Log("selected agent: " + selectedAgent.name);
        yield return null;

    }
    public void SelectOpponentAgent()
    {
        //Debug.Log("adding select agent to list: " + curActionsList);
        curActionsList.Enqueue(_SelectOpponentAgent());
    }
    public IEnumerator _SelectOpponentAgent()
    {
        if (thisCard != null)
        {
            Debug.Log("selecting opponent agent");
            selectedAgent = thisCard.modal.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player; 
        }
        else if(thisMinion != null)
        {
            selectedAgent = thisMinion.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
        }
        Debug.Log("selected agent: " + selectedAgent.name);
        yield return null;

    }

    // Forward direction for the agent currently summoning: the player advances up the grid, the
    // opponent advances down. Occupants are pushed this way regardless of their own allegiance.
    public static Vector3Int SummonerPushDir()
    {
        return GameManager.Instance.isPlayerTurn ? Vector3Int.up : Vector3Int.down;
    }

    /// <summary>
    /// Wording of the tutorial hint shown while the player picks the tile to summon onto. Only
    /// <see cref="_SelectCell"/> passes it: that is the summon placement. The other cell picks below are
    /// spell areas, and a prompt naming a summon would be wrong on every one of them.
    /// </summary>
    public const string SummonCellPrompt = "Select a Tile To Summon";

    public void SelectCell(int rowIndex = 2)
    {
        IEnumerator cor = _SelectCell(rowIndex);
        //GameManager.Instance.Addtoactions(cor);
        curActionsList.Enqueue(cor);
        //Debug.LogWarning("selectcell added to actions");
    }
    public IEnumerator _SelectCell(int rowIndex)
    {
        var grid = GridManager.Instance.GetGrid();
        List<Transform> selectableCells = new List<Transform>();
        HashSet<Vector2Int> selectableIndexes = new HashSet<Vector2Int>();

        selectedcell = null;
        if (!GameManager.Instance.isPlayerTurn)
        {
            rowIndex = 2 - rowIndex;
        }
        foreach (var cell in grid)
        {
            if (cell.index.y != rowIndex) continue;

            // A start cell is selectable if it's empty, OR it's occupied by a minion (friendly or
            // enemy) that can be pushed one cell in the summoner's forward direction (that cell is
            // empty and in-grid). A non-pushable occupant (minion already ahead, or at the grid edge)
            // blocks the cell.
            bool selectable;
            if (cell.obj == null)
            {
                selectable = true;
            }
            else
            {
                var occupant = cell.obj.GetComponent<MinionController>();
                selectable = occupant != null && occupant.CanBePushedForward(SummonerPushDir());
            }

            if (selectable)
            {
                selectableCells.Add(cell.cellObj.transform);
                selectableIndexes.Add(cell.index);
            }
        }

        if (GridCellSelectionManager.Instance != null)
        {
            GridCellSelectionManager.Instance.BeginSelection(
                selectableIndexes,
                CellFootprints.Single,
                previewOccupantPush: true,
                sourceCard: thisCardSO,
                promptMessage: SummonCellPrompt);
        }
        if (GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.curState = Player.State.SelectingCell;
        }
        if (GameManager.Instance.isTesting && selectableCells.Count == 0)
        {
            GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        currentCellFootprint = CellFootprints.Single;
        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }
        currentCellFootprint = null;
        if (GridCellSelectionManager.Instance != null) GridCellSelectionManager.Instance.EndSelection();
        if (cancelRequested) yield break;
        selectedCells.Clear();
        selectedCells.Add(selectedcell);

        //Debug.Log("selected cell");
    }
    public void SelectCollumn()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectCollumn());
    }
    public IEnumerator _SelectCollumn()
    {
        var grid = GridManager.Instance.GetGrid();
        List<Transform> selectableCells = new List<Transform>();
        HashSet<Vector2Int> selectableIndexes = new HashSet<Vector2Int>();

        int rowIndex = 2;
        selectedcell = null;
        foreach (var cell in grid)
        {
            if (cell.index.y == rowIndex)
            {
                selectableCells.Add(cell.cellObj.transform);
                selectableIndexes.Add(cell.index);
            }
        }

        if (GridCellSelectionManager.Instance != null)
        {
            GridCellSelectionManager.Instance.BeginSelection(
                selectableIndexes,
                CellFootprints.ThreeDown,
                sourceCard: thisCardSO);
        }
        if (GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.curState = Player.State.SelectingCell;
        }

        if (GameManager.Instance.isTesting && selectableCells.Count == 0)
        {
            GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        currentCellFootprint = CellFootprints.ThreeDown;
        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }

        currentCellFootprint = null;
        if (GridCellSelectionManager.Instance != null) GridCellSelectionManager.Instance.EndSelection();
        if (cancelRequested) yield break;

        selectedCells.Clear();
        Vector2Int centerIndex = GridManager.Instance.PosToGridIndex(selectedcell.position);

        // Same shape the highlight drew and the AI scored — one definition, so they cannot disagree.
        HashSet<Vector2Int> areaIndexes = new HashSet<Vector2Int>(CellFootprints.ThreeDown(centerIndex));

        foreach (var areaIndex in areaIndexes)
        {
            if (GridManager.Instance.IsOutSideOfGrid(areaIndex)) continue;
            Transform t = GetCellTransform(areaIndex);
            if (t != null) selectedCells.Add(t);
        }

        //Debug.Log("selected collumn");
    }
    public void SelectSmallArea()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectSmallArea());
    }
    public IEnumerator _SelectSmallArea()
    {
        // Select a center cell, then select the surrounding plus-shape (center + 4 orthogonal neighbors).
        var grid = GridManager.Instance.GetGrid();
        List<Transform> selectableCells = new List<Transform>();
        HashSet<Vector2Int> selectableIndexes = new HashSet<Vector2Int>();

        selectedcell = null;

        foreach (var cell in grid)
        {
            if (cell.cellObj == null) continue;

            selectableCells.Add(cell.cellObj.transform);
            selectableIndexes.Add(cell.index);
        }

        if (GridCellSelectionManager.Instance != null)
        {
            GridCellSelectionManager.Instance.BeginSelection(
                selectableIndexes,
                CellFootprints.Plus,
                sourceCard: thisCardSO);
        }

        if (GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.curState = Player.State.SelectingCell;
        }

        if (GameManager.Instance.isTesting && selectableCells.Count == 0)
        {
            GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        currentCellFootprint = CellFootprints.Plus;
        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            yield return null;
        }

        currentCellFootprint = null;
        if (GridCellSelectionManager.Instance != null) GridCellSelectionManager.Instance.EndSelection();
        if (cancelRequested) yield break;

        selectedCells.Clear();
        Vector2Int centerIndex = GridManager.Instance.PosToGridIndex(selectedcell.position);

        // Same shape the highlight drew and the AI scored — one definition, so they cannot disagree.
        HashSet<Vector2Int> areaIndexes = new HashSet<Vector2Int>(CellFootprints.Plus(centerIndex));

        foreach (var areaIndex in areaIndexes)
        {
            if (GridManager.Instance.IsOutSideOfGrid(areaIndex)) continue;
            Transform t = GetCellTransform(areaIndex);
            if (t != null) selectedCells.Add(t);
        }

        Debug.Log("selected cells count: " + selectedCells.Count);
    }

    public void SelectAllMinionsFromCells()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectAllMinionsFromCells());

    }
    public IEnumerator _SelectAllMinionsFromCells()
    {
        selectedMinions.Clear();

        foreach (var item in selectedCells)
        {
            var cell = GridManager.Instance.GetCell(item.transform.position);
            var obj = cell.obj;

            if (obj == null) continue;

            var minion = obj.GetComponent<MinionController>();

            if (minion == null) continue;

            selectedMinions.Add(minion);

        }

        yield return null; 
    }

    public void SelectMinion(int typeIndex = 0)
    {

        //GameManager.Instance.Addtoactions( _SelectMinion());
        curActionsList.Enqueue(_SelectMinion());

        //Debug.LogWarning("selecminion added to actions");
    }


    public IEnumerator _SelectMinion()
    {
        var grid = GridManager.Instance.GetGrid();
        List<MinionController> selectableminions = new List<MinionController>();
        foreach (var cell in grid)
        {
            MinionController minion = cell.obj?.GetComponent<MinionController>();

            if (minion == null) continue;

            selectableminions.Add(minion);
        }

        // The SelectionManager owns highlighting + click routing for the player; it is inert on the
        // AI's turn and while testing, where the result (selectedMinion) is written directly instead.
        // The card declares what hovering a candidate means (e.g. ToPush shows the push arrow).
        HoverIntent intent = thisCardSO != null ? thisCardSO.selectionIntent : HoverIntent.ToSelectGenerally;
        SelectionManager.Instance.BeginMinionRequest(selectableminions, picked => selectedMinion = picked, intent, thisCardSO);

        OnWaitingMinionSelect?.Invoke(selectableminions, thisCardSO);

        if (selectableminions.Count == 0 && GameManager.Instance.isTesting)
        {
            Debug.LogWarning("test failed");

            GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        while (selectedMinion == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting minion");

            yield return null;
        }

        // Tear down the selection regardless of how the loop ended (resolved, cancelled, or testing) so
        // every minion returns to correct resting state — a minion picked here stays attack-capable.
        SelectionManager.Instance.Complete();

        if (cancelRequested) yield break;
        selectedMinions.Clear();
        selectedMinions.Add(selectedMinion);

        //Debug.Log("selected minion");


    }
    public void SelectAllMinionsOfSelectedAgent()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectAllMinionsOfSelectedAgent());
    }
    public IEnumerator _SelectAllMinionsOfSelectedAgent()
    {
        selectedMinions.Clear();
        selectedMinions.AddRange(selectedAgent.minions);

        yield return null;
    }

    public void SelectAllMinionsAsSelectedCell()
    {
        curActionsList.Enqueue(_SelectAllMinionsAsSelectedCell());
    }
    public IEnumerator _SelectAllMinionsAsSelectedCell()
    {
        /*var grid = GridManager.Instance.GetGrid();
        selectedMinions.Clear();
        foreach (var cell in grid)
        {
            if (cell.obj.transform.position.x == selectedcell.position.x)
            {
                selectedMinions.Add(cell.obj.GetComponent<MinionController>());
            }
        }

        yield return null;
        */
        var grid = GridManager.Instance.GetGrid();
        List<Transform> selectableCells = new List<Transform>();
        HashSet<Vector2Int> selectableIndexes = new HashSet<Vector2Int>();
        int rowIndex = 2;
        selectedcell = null;
        foreach (var cell in grid)
        {
            if (cell.index.y == rowIndex)
            {
                selectableCells.Add(cell.cellObj.transform);
                selectableIndexes.Add(cell.index);
            }
        }

        if (GridCellSelectionManager.Instance != null)
        {
            GridCellSelectionManager.Instance.BeginSelection(
                selectableIndexes,
                hovered => selectableIndexes.Where(i => i.x == hovered.x),
                sourceCard: thisCardSO);
        }
        if (GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.curState = Player.State.SelectingCell;
        }
        if (GameManager.Instance.isTesting && selectableCells.Count == 0)
        {
            GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        // FullColumn is what the effect below actually hits, and that is what the AI must score. Note it
        // does NOT match the hover highlight above: selectableIndexes only holds row y == 2, so
        // `Where(i => i.x == hovered.x)` lights exactly one cell while the effect sweeps the whole
        // column. That mismatch predates this and is left alone here rather than quietly changing what
        // the player sees — worth fixing separately.
        currentCellFootprint = CellFootprints.FullColumn;
        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }
        currentCellFootprint = null;
        if (GridCellSelectionManager.Instance != null) GridCellSelectionManager.Instance.EndSelection();
        if (cancelRequested) yield break;

        selectedMinions.Clear();

        foreach (var cell in grid)
        {
            if (cell.obj != null && cell.obj.transform.position.x == selectedcell.position.x)
            {
                selectedMinions.Add(cell.obj.GetComponent<MinionController>());
            }
        }

        //Debug.Log("selected minion at collumn: " + selectedcell.position.x);

    }

    public void SelectThisMinion()
    {
        Debug.Log("select this minion action added ");
        curActionsList.Enqueue(_SelectThisMinion());
    }
    public IEnumerator _SelectThisMinion()
    {
        selectedMinions.Clear();
        selectedMinion = thisMinion;
        if (selectedMinion != null)
            selectedMinions.Add(selectedMinion);

        Debug.Log("minion sohuld be selected");
        yield return null;
    }
    public void SelectAllMinionsInHand()
    {
        if (GameManager.Instance.isTesting) return; 

        curActionsList.Enqueue(_SelectAllMinionsInHand());

    }
    public IEnumerator _SelectAllMinionsInHand()
    {
        selectedCards.Clear();

        foreach (var item in selectedAgent.hand)
        {
            if (item.card.health == 0) continue;

            selectedCards.Add(item);
        }

        yield return null;
    }

    public void SelectRandomMinionFromHand()
    {
        //if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectRandomMinionFromHand());
    }
    public IEnumerator _SelectRandomMinionFromHand()
    {
        selectedCards.Clear();

        if (selectedAgent == null) yield break;

        List<CardController> minionsInHand = new List<CardController>();
        foreach (var item in selectedAgent.hand)
        {
            if (item == null || item.card == null) continue;
            if (item == thisCard) continue; // exclude the card currently being played
            if (item.card.health == 0) continue; // spells have no health; only consider minions

            minionsInHand.Add(item);
        }

        if (minionsInHand.Count > 0)
        {
            selectedCards.Add(minionsInHand[UnityEngine.Random.Range(0, minionsInHand.Count)]);
        }

        yield return null;
    }

    public void SelectAllMinionsAdjacentToThis()
    {
        curActionsList.Enqueue(_SelectAllMinionsAdjacentToThis());
    }
    public IEnumerator _SelectAllMinionsAdjacentToThis()
    {
        selectedMinions.Clear();
        var grid = GridManager.Instance.GetGrid();

        foreach (var cell in grid)
        {
            if (cell.obj != null && (cell.obj.transform.position - thisMinion.transform.position).magnitude == 1)
            {
                selectedMinions.Add(cell.obj.GetComponent<MinionController>());
            }
        }
        yield return null;

    }
    public void SelectRandomEnemyMinion()
    {
        if (GameManager.Instance.isTesting) return;

        Agent opponent = null;

        if(thisMinion != null)
        {
            opponent = thisMinion.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
            Debug.Log("opponent: " + opponent.name);
        }
        else
        {
            opponent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player;
            Debug.Log("opponent: " + opponent.name);

        }

        curActionsList.Enqueue(_SelectRandomMinion(opponent.minions));
    }
    public void SelectRandomEnemyMinionInRange()
    {
        Debug.Log("try add SelectRandomEnemyMinionInRange event ");

        if (GameManager.Instance.isTesting) return;
        Agent opponent = null;

        if (thisMinion != null)
        {

            opponent = thisMinion.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
            Debug.Log("opponent: " + opponent.name);

        }
        else
        {
            opponent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player;
            Debug.Log("opponent: " + opponent.name);

        }
        selectedAgent = opponent;
        curActionsList.Enqueue(_SelectRandomMinionInRange(opponent));
    }
    public void SelectAllEnemyMinionsInRange()
    {
        if (GameManager.Instance.isTesting) return;
        Agent opponent = null;

        if (thisMinion != null)
        {
            opponent = thisMinion.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
        }
        else
        {
            opponent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player;
        }

        curActionsList.Enqueue(_SelectAllEnemyMinionsInRange(opponent));
    }
    public IEnumerator _SelectAllEnemyMinionsInRange(Agent opponent)
    {
        selectedMinions.Clear();

        if (thisMinion == null) yield break;

        foreach (var minion in opponent.minions)
        {
            if (minion == thisMinion) continue;
            if (RangeUtility.IsInRange(thisMinion, minion))
            {
                selectedMinions.Add(minion);
            }
        }

        yield return null;
    }
    public void SelectAllEnemyMinionsInRangeAsTarget()
    {
        if (GameManager.Instance.isTesting) return;
        Agent opponent = null;

        if (thisMinion != null)
        {
            opponent = thisMinion.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
        }
        else
        {
            opponent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player;
        }

        curActionsList.Enqueue(_SelectAllEnemyMinionsInRangeAsTarget(opponent));
    }
    public IEnumerator _SelectAllEnemyMinionsInRangeAsTarget(Agent opponent)
    {
        selectedTargetMinions.Clear();

        if (thisMinion != null) {

            foreach (var minion in opponent.minions)
            {
                if (minion == thisMinion) continue;
                if (RangeUtility.IsInRange(thisMinion, minion))
                {
                    selectedTargetMinions.Add(minion);
                }
            }
            yield return null;
        } 
    }
    public void SelectRandomFriendlyMinionInRange()
    {
        curActionsList.Enqueue(_SelectRandomFriendlyMinionInRange());
    }
    public void SelectRandomFriendlyMinion()
    {
        Agent player = thisMinion != null ? thisMinion.owner
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        curActionsList.Enqueue(_SelectRandomFriendlyMinion(player));
    }
    public IEnumerator _SelectRandomFriendlyMinion(Agent thisAgent)
    {
        selectedMinions.Clear();
        List<MinionController> candidates = new List<MinionController>();
        foreach (var minion in thisAgent.minions)
        {
            if (minion == thisMinion) continue;
            candidates.Add(minion);
        }
        if (candidates.Count > 0)
            selectedMinions.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        yield return null;
    }
    public void SelectPushableMinion()
    {
        Agent agent = thisMinion != null ? thisMinion.owner
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        curActionsList.Enqueue(_SelectPushableMinion(agent));
    }
    public IEnumerator _SelectPushableMinion(Agent agent)
    {
        selectedMinions.Clear();

        bool isPlayer = agent == GameManager.Instance.player;
        Vector3Int frontDir = isPlayer ? Vector3Int.up : Vector3Int.down;

        List<MinionController> candidates = new List<MinionController>();
        foreach (var minion in agent.minions)
        {
            if (minion == thisMinion) continue;
            if (minion.CanBePushedForward(frontDir))
                candidates.Add(minion);
        }

        var indexes = new List<Vector2Int>();
        foreach (var item in candidates)
        {
            var index = GridManager.Instance.PosToGridIndex(item.transform.position);
            indexes.Add(index);
        }

        GridCellSelectionManager.Instance.BeginSelection(indexes, hovered => new[] { hovered }, sourceCard: thisCardSO);

        while (selectedMinion == null)
        {
            yield return null;
        }

        GridCellSelectionManager.Instance.EndSelection();

        yield return null;
    }

    public void SelectAllFriendlyMinions()
    {
        if (GameManager.Instance.isTesting) return;

        Agent player = thisMinion != null ? thisMinion.owner
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        curActionsList.Enqueue(_SelectAllFriendlyMinions(player));
    }
    public IEnumerator _SelectAllFriendlyMinions(Agent thisAgent)
    {
        selectedMinions.Clear();
        foreach (var minion in thisAgent.minions)
        {
            if (minion == thisMinion) continue;
            selectedMinions.Add(minion);
        }
        yield return null;
    }
    public IEnumerator _SelectRandomMinion(List<MinionController> minions)
    {
        selectedMinion = minions[UnityEngine.Random.Range(0, minions.Count)];
        selectedMinions.Add(selectedMinion);
        Debug.Log("selected minionÇ: " + selectedMinion.card.cardName);
        yield return null;
    }
    public IEnumerator _SelectRandomMinionInRange(Agent agent)
    {
        List<MinionController> minionsInRange = new List<MinionController>();
        Debug.Log("opponent: " + agent.name);

        foreach (var minion in agent.minions)
        {
            if (minion == null) continue;
            Debug.Log("checking if minion is in range: ");
            if (RangeUtility.IsInRange(thisMinion, minion) && minion != thisMinion)
            {
                Debug.Log("minion is in range: ");

                minionsInRange.Add(minion);
            }
        }
        selectedMinions.Clear();
        selectedTargetMinions.Clear();

        if (minionsInRange.Count > 0)
        {
            var chosen = minionsInRange[UnityEngine.Random.Range(0, minionsInRange.Count)];
            selectedMinions.Add(chosen);        // consumed by effects like ChangeMinionHealth (e.g. Nailpuncher)
            selectedTargetMinions.Add(chosen);  // consumed by effects like Attack (e.g. Turret)
        }

        yield return null;
    }
    public IEnumerator _SelectRandomFriendlyMinionInRange()
    {
        // Resolved lazily (when this coroutine actually runs) rather than when it's enqueued, so it
        // sees thisMinion as set by this same play's SummonMinion step, not a stale value left over
        // from a previous card/trigger (GameManager.PlayCard doesn't reset thisMinion).
        Agent thisAgent = thisMinion != null
            ? thisMinion.owner
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        List<MinionController> minionsInRange = new List<MinionController>();
        Debug.Log("opponent: " + thisAgent.name);
        selectedMinions.Clear();
        foreach (var minion in thisAgent.minions)
        {
            if (minion == null || minion == thisMinion) continue;
            Debug.Log("checking if minion is in range: ");
            if (RangeUtility.IsInRange(thisMinion, minion))
            {
                Debug.Log("minion is in range: ");

                minionsInRange.Add(minion);
            }
        }

        if(minionsInRange.Count > 0)
        {
            selectedMinions.Add(minionsInRange[UnityEngine.Random.Range(0, minionsInRange.Count)]);
            //selectedMinion = minionsInRange[UnityEngine.Random.Range(0, minionsInRange.Count)];

        }
        yield return null;
    }
    /// <summary>
    /// Picks one random minion orthogonally adjacent to thisMinion and owned by the same agent, into
    /// selectedMinions. Walks the four neighbouring grid cells rather than owner.minions, so the
    /// off-grid hero is never a candidate. Leaves the list empty when nothing qualifies, so a
    /// following verb iterates nothing and the effect fizzles rather than throwing.
    /// </summary>
    public void SelectRandomFriendlyMinionAdjacentToThis()
    {
        curActionsList.Enqueue(_SelectRandomFriendlyMinionAdjacentToThis());
    }
    public IEnumerator _SelectRandomFriendlyMinionAdjacentToThis()
    {
        // Resolved lazily (when this coroutine actually runs) rather than when it's enqueued, so it sees
        // thisMinion as set by this same play's SummonMinion step — same reasoning as
        // _SelectRandomFriendlyMinionInRange.
        selectedMinions.Clear();
        selectedMinion = null;

        if (thisMinion == null) yield break;

        Vector2Int center = GridManager.Instance.PosToGridIndex(thisMinion.transform.position);
        Vector2Int[] offsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        List<MinionController> candidates = new List<MinionController>();
        foreach (var offset in offsets)
        {
            Vector2Int index = center + offset;
            if (GridManager.Instance.IsOutSideOfGrid(index)) continue;

            var obj = GridManager.Instance.GetCell(index).obj;
            if (obj == null) continue;

            var minion = obj.GetComponent<MinionController>();
            if (minion == null || minion == thisMinion) continue;
            if (minion.owner != thisMinion.owner) continue;

            candidates.Add(minion);
        }

        if (candidates.Count > 0)
        {
            selectedMinion = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            selectedMinions.Add(selectedMinion);
        }

        yield return null;
    }

    /// <summary>
    /// thisMinion consumes each selected minion: it gains that minion's current attack and health
    /// (max health grows with it, so the gained health is healable), then the minion is destroyed.
    /// </summary>
    public void AbsorbSelectedMinions()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_AbsorbSelectedMinions());
    }
    public IEnumerator _AbsorbSelectedMinions()
    {
        // Hold our own reference to the absorber instead of reading the static across the kill below.
        // A death dispatches its OnDeath trigger synchronously up to that trigger's first yield, and
        // that path calls ResetSelections() and repoints thisMinion/selectedMinions at the VICTIM
        // (GameManager.InvokeOnMinionDeathActions) — its PushScope only restores them later, after this
        // coroutine has moved on. Reading thisMinion after TakeDamage would buff the corpse.
        var absorber = thisMinion;
        if (absorber == null) yield break;

        var victims = new List<MinionController>(selectedMinions);

        foreach (var victim in victims)
        {
            if (victim == null || victim == absorber) continue;

            Vector3 absorbTarget = absorber.transform.position;

            // Buff before the kill so the victim's own OnDeath trigger observes the absorbed stats.
            absorber.modal.attack += victim.modal.attack;
            absorber.modal.health += victim.modal.health;
            absorber.modal.defHealth += victim.modal.health;
            absorber.view.UpdateView(absorber.modal);

            // Route the kill through TakeDamage (out-damaging armor) rather than a direct destroy, so
            // the victim's OnDeath fires and Die()'s grid/roster bookkeeping runs. This must happen
            // BEFORE the absorb animation moves it: Die() records which cell the minion died on from
            // transform.position (for revive-in-place effects), so a victim already slid onto the
            // absorber's tile would be logged as having died there instead of on its own tile.
            victim.TakeDamage(victim.modal.health + victim.modal.armor);

            // Now that it's dead and off the grid, shrink the corpse into the absorber. The death
            // animation Die() schedules 1s out is a sprite swap with no scale/position curves, so it
            // can't undo this — it just plays out invisibly before DestroySelf cleans the object up.
            victim.view.PlayAbsorbAnimation(absorbTarget, 0.25f);

            yield return new WaitForSeconds(0.25f);
        }
    }

    public void SelectThisMinionsLastTargetAsTarget()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectThisMinionsLastTargetAsTarget());

    }
    public IEnumerator _SelectThisMinionsLastTargetAsTarget()
    {
        selectedTargetMinions.Clear();
        if (thisMinion.LastTarget != null)
            selectedTargetMinions.Add(thisMinion.LastTarget);
        yield return null;
    }

    // Targets the minion that just entered play (see ActionHolder.summonedMinion) ONLY if it belongs to
    // the enemy and sits within thisMinion's range. Otherwise selects nothing, so a following Attack is a
    // no-op. Used on the OnAnyMinionSummoned trigger to build "shoot enemies as they're summoned in range".
    public void SelectSummonedEnemyInRangeAsTarget()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_SelectSummonedEnemyInRangeAsTarget());
    }
    public IEnumerator _SelectSummonedEnemyInRangeAsTarget()
    {
        selectedTargetMinions.Clear();
        if (thisMinion != null && summonedMinion != null
            && summonedMinion.owner != thisMinion.owner
            && RangeUtility.IsInRange(thisMinion, summonedMinion))
        {
            selectedTargetMinions.Add(summonedMinion);
        }
        yield return null;
    }

    public void SelectSpawnCells()
    {
        curActionsList.Enqueue(_SelectSpawnCells());
    }
    public IEnumerator _SelectSpawnCells()
    {
        int rowIndex = 2;
        if (!GameManager.Instance.isPlayerTurn)
            rowIndex = 2 - rowIndex;

        selectedCells.Clear();
        var grid = GridManager.Instance.GetGrid();
        foreach (var cell in grid)
        {
            if (cell.index.y != rowIndex) continue;
            if (cell.cellObj == null) continue;
            selectedCells.Add(cell.cellObj.transform);
        }
        yield return null;
    }

    public void SelectEmptyCells()
    {
        curActionsList.Enqueue(_SelectEmptyCells());
    }
    public IEnumerator _SelectEmptyCells()
    {
        selectedCells.Clear();
        var grid = GridManager.Instance.GetGrid();
        foreach (var cell in grid)
        {
            if (cell.cellObj == null) continue;
            if (cell.obj != null) continue;
            selectedCells.Add(cell.cellObj.transform);
        }
        yield return null;
    }

    public void SelectAllCells()
    {
        curActionsList.Enqueue(_SelectAllCells());
    }
    public IEnumerator _SelectAllCells()
    {
        selectedCells.Clear();
        var grid = GridManager.Instance.GetGrid();
        foreach (var cell in grid)
        {
            if (cell.cellObj == null) continue;
            selectedCells.Add(cell.cellObj.transform);
        }
        yield return null;
    }

    // Filters selectedCells to the row directly adjacent to the enemy hero (hero is off-grid;
    // clamping its grid-y gives the nearest valid row). If selectedcell or thisMinion is set,
    // further narrows to that same X column — matching the active summon position.
    public void FilterSpawnCells()
    {
        curActionsList.Enqueue(_FilterSpawnCells());
    }
    public IEnumerator _FilterSpawnCells()
    {
        Agent enemyAgent = thisMinion != null
            ? (thisMinion.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player)
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player);

        int heroGridY = -Mathf.RoundToInt(enemyAgent.hero.transform.position.y);
        int frontRow = Mathf.Clamp(heroGridY, 0, GridManager.Instance.GridHeight - 1);

        int? spawnX = null;
        if (selectedcell != null)
            spawnX = GridManager.Instance.PosToGridIndex(selectedcell.position).x;
        else if (thisMinion != null)
            spawnX = GridManager.Instance.PosToGridIndex(thisMinion.transform.position).x;

        selectedCells.RemoveAll(t =>
        {
            var idx = GridManager.Instance.PosToGridIndex(t.position);
            if (idx.y != frontRow) return true;
            if (spawnX.HasValue && idx.x != spawnX.Value) return true;
            return false;
        });

        yield return null;
    }

    public void FilterEmptyCells()
    {
        curActionsList.Enqueue(_FilterEmptyCells());
    }
    public IEnumerator _FilterEmptyCells()
    {
        selectedCells.RemoveAll(t =>
        {
            var cell = GridManager.Instance.GetCell(t.position);
            return cell.obj != null;
        });
        yield return null;
    }


    // The spawn row of a given agent. The player summons on row 2, the opponent on row 0 — the same
    // 2 / (2 - rowIndex) convention _SelectCell and _SelectSpawnCells use, but resolved from the AGENT
    // rather than from isPlayerTurn. Hero passives fire on the opponent's turn, so a turn-relative
    // answer would name the wrong row.
    private static int SpawnRowOf(Agent agent)
    {
        return agent == GameManager.Instance.player ? 2 : 0;
    }

    private static Agent EnemyOf(Agent agent)
    {
        return agent == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
    }

    /// <summary>Fills selectedCells with the spawn row of thisMinion's enemy. Owner-relative.</summary>
    public void SelectEnemySpawnCells()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectEnemySpawnCells());
    }
    public IEnumerator _SelectEnemySpawnCells()
    {
        selectedCells.Clear();

        if (thisMinion == null) yield break;

        int rowIndex = SpawnRowOf(EnemyOf(thisMinion.owner));

        foreach (var cell in GridManager.Instance.GetGrid())
        {
            if (cell.index.y != rowIndex) continue;
            if (cell.cellObj == null) continue;
            selectedCells.Add(cell.cellObj.transform);
        }

        Debug.Log($"[HUNTER] _SelectEnemySpawnCells: thisMinion='{thisMinion?.card?.cardName}' rowIndex={rowIndex} -> {selectedCells.Count} cells");
        yield return null;
    }

    /// <summary>
    /// Picks one random minion standing in selectedCells that is hostile to thisMinion, into
    /// selectedMinions. Leaves the list empty when there is no candidate, so a following
    /// ChangeMinionHealth iterates nothing and the effect fizzles rather than throwing.
    /// </summary>
    public void SelectRandomEnemyMinionInSelectedCells()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectRandomEnemyMinionInSelectedCells());
    }
    public IEnumerator _SelectRandomEnemyMinionInSelectedCells()
    {
        selectedMinions.Clear();
        selectedMinion = null;

        if (thisMinion == null) yield break;

        List<MinionController> candidates = new List<MinionController>();
        foreach (var cellTransform in selectedCells)
        {
            if (cellTransform == null) continue;

            var obj = GridManager.Instance.GetCell(cellTransform.position).obj;
            if (obj == null) continue;

            var minion = obj.GetComponent<MinionController>();
            if (minion == null || minion.owner == thisMinion.owner) continue;

            bool isHero = minion.owner != null && minion == minion.owner.hero;
            Debug.Log($"[HUNTER]   candidate cell obj='{minion.card?.cardName}' owner={(minion.owner == GameManager.Instance.player ? "player" : "opp")} isHero={isHero} hp={minion.modal?.health}");
            candidates.Add(minion);
        }

        if (candidates.Count > 0)
        {
            selectedMinion = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            selectedMinions.Add(selectedMinion);
        }

        yield return null;
    }

    #endregion
    public void AtestAction()
    {

    }

    /// <summary>
    /// Berserker: grants thisMinion (a hero) +1 attack per `healthPerAttack` health it has lost.
    ///
    /// The bonus is permanent — healing never takes it back — so we only ever apply the positive delta
    /// between the bonus we owe and the one already granted, tracked on HeroRuntime. Applying a delta
    /// rather than assigning `attack = base + bonus` also means the bonus stacks with, instead of
    /// clobbering, any other attack buff on the hero.
    /// </summary>
    public void ScaleAttackWithHealthLost(int healthPerAttack)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_ScaleAttackWithHealthLost(healthPerAttack));
    }
    public IEnumerator _ScaleAttackWithHealthLost(int healthPerAttack)
    {
        var hero = thisMinion;
        var runtime = HeroRuntime.For(hero);

        if (hero == null || runtime == null)
        {
            Debug.LogWarning("ScaleAttackWithHealthLost: no hero runtime on " + (hero != null ? hero.name : "null"));
            yield break;
        }

        int per = Mathf.Max(1, healthPerAttack);
        int healthLost = Mathf.Max(0, hero.modal.defHealth - hero.modal.health);
        int owedBonus = healthLost / per;

        if (owedBonus > runtime.appliedAttackBonus)
        {
            hero.modal.attack += owedBonus - runtime.appliedAttackBonus;
            runtime.appliedAttackBonus = owedBonus;
            hero.view.UpdateView(hero.modal);
        }

        yield return null;
    }
    public void PushSelectedMinionForward()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_PushSelectedMinionForward());
    }
    public IEnumerator _PushSelectedMinionForward()
    {
        var dir = GameManager.Instance.isPlayerTurn ? Vector3Int.up : Vector3Int.down;

        Vector3Int pos = Vector3Int.RoundToInt(selectedMinion.gridEntity.WorldPos) + dir;
        var canMove = selectedMinion.modal.canMove;
        var age = selectedMinion.age;

        selectedMinion.age = 1;
        selectedMinion.modal.canMove = true;
        Debug.Log("try move minion ");
        var canMoveInfo = selectedMinion.CanMove(pos);
        if (canMoveInfo.CanMove)
        {
            Debug.Log("minion should move");
            selectedMinion.Move(pos);
        }
        else
        {
            selectedMinion.FailedMove(pos, canMoveInfo.CollidedEntity);
        }
        selectedMinion.modal.canMove = canMove;
        selectedMinion.age = age;
        yield return null;
    }
    public void PayCardCost()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_PayCardCost());
    }
    public IEnumerator _PayCardCost()
    {
        selectedAgent.availibleMana -= thisCard.modal.cost;
        Debug.Log("cost paid: " + thisCard.modal.cost);
        yield return null;
    }

    public void CreateCard(CardSO card)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_CreateCard(card));
    }
    public IEnumerator _CreateCard(CardSO card)
    {
        if (card == null)
        {
            Debug.LogWarning("CreateCard called with null CardSO");
            yield break;
        }

        var agent = selectedAgent != null
            ? selectedAgent
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        CardController cardObj = agent.InstantiateCard(card);

        selectedCards.Clear();
        selectedCards.Add(cardObj);

        yield return null;
    }

    public void AddToDeck()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_AddToDeck());
    }
    public IEnumerator _AddToDeck()
    {
        if (selectedCards == null || selectedCards.Count == 0)
        {
            Debug.LogWarning("AddToDeck called with empty selectedCards");
            yield break;
        }

        var agent = selectedAgent != null
            ? selectedAgent
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        var cardsToAdd = new List<CardController>(selectedCards);

        foreach (var card in cardsToAdd)
        {
            if (card == null || card.card == null) continue;

            agent.AddCardToDeck(card);

            yield return new WaitForSeconds(0.5f);
        }
    }

    // Set while a rolled spell is resolving, so a random cast can't roll another random cast and recurse
    // forever. The pool is authored data — nothing stops someone dropping a card that carries this very
    // verb into it, which without this guard would hang the editor rather than fail visibly.
    private static bool _castingRandomSpell = false;

    // Length of the card's turn-into-another-card flip. Shared by the animation and the wait that lets it
    // play out, so the effect can't fire before the card has finished becoming what it rolled.
    private const float TurnIntoAnimationDuration = 0.6f;

    /// <summary>
    /// Turns the card being played INTO a random card from `pool`, then plays that card's effect for free.
    /// The player sees the result but never gets to choose it — for the "pick one of N" variant, see
    /// <see cref="DiscoverFromPool"/>.
    /// </summary>
    public void PlayRandomCardFromPool(CardPoolSO pool)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_PlayRandomCardFromPool(pool));
    }
    public IEnumerator _PlayRandomCardFromPool(CardPoolSO pool)
    {
        CardController casting;
        Agent caster;
        if (!TryBeginTransform(pool, "PlayRandomCardFromPool", out casting, out caster)) yield break;

        CardSO rolled = pool.GetRandom();
        if (rolled == null)
        {
            Debug.LogWarning("PlayRandomCardFromPool: pool '" + pool.name + "' has no usable card");
            yield break;
        }

        Debug.Log("rolled card: " + rolled.cardName);

        yield return GameManager.Instance.StartCoroutine(_TransformIntoAndPlay(rolled, casting, caster));
    }

    /// <summary>
    /// Discover: puts `pool.choiceCount` cards from `pool` in front of the player, turns the card being
    /// played into the one they pick, and plays it for free.
    ///
    /// Two rules make this a real choice rather than a reroll button:
    ///
    /// 1. Only cards that can actually RESOLVE on the current board are offered. The player is committed
    ///    the moment they see the options (rule 2), so offering a dead option — a summon with nowhere to
    ///    land, a column spell with no legal column — would charge them for a choice they never had.
    /// 2. Revealing the options commits the play. There is no back-out on the prompt itself
    ///    (GameManager.CancelPlayingCard is inert while CardChoice has a request open), and cancelling the
    ///    CHOSEN card's targeting fizzles it rather than refunding — see <see cref="_TransformIntoAndPlay"/>.
    ///    Without both, cancel-and-replay hands back a fresh set of options for free, and the randomness
    ///    costs nothing.
    /// </summary>
    public void DiscoverFromPool(CardPoolSO pool)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_DiscoverFromPool(pool));
    }
    public IEnumerator _DiscoverFromPool(CardPoolSO pool)
    {
        CardController casting;
        Agent caster;
        if (!TryBeginTransform(pool, "DiscoverFromPool", out casting, out caster)) yield break;

        List<CardSO> candidates = pool.UsableCards();
        if (candidates.Count == 0)
        {
            Debug.LogWarning("DiscoverFromPool: pool '" + pool.name + "' has no usable card");
            yield break;
        }

        // Narrow to what can resolve right now (rule 1 above). Falling back to the unfiltered pool when
        // NOTHING is playable keeps the card from silently doing nothing on a locked board: the player
        // still gets a choice, it just may fizzle — strictly better than an empty prompt.
        var playable = new List<CardSO>();
        foreach (var candidate in candidates)
        {
            bool canResolve = false;
            yield return GameManager.Instance.StartCoroutine(
                CanResolveNow(candidate, casting, r => canResolve = r));
            if (canResolve) playable.Add(candidate);
        }

        if (playable.Count == 0)
        {
            Debug.LogWarning("DiscoverFromPool: nothing in '" + pool.name + "' can resolve on this board; offering unfiltered");
            playable = candidates;
        }

        CardSO chosen = null;
        yield return GameManager.Instance.StartCoroutine(
            _ChooseFromPool(playable, pool.choiceCount, c => chosen = c));

        if (chosen == null) yield break;

        Debug.Log("discovered card (cast): " + chosen.cardName);

        yield return GameManager.Instance.StartCoroutine(_TransformIntoAndPlay(chosen, casting, caster));
    }

    /// <summary>
    /// Discover into hand: offers `pool.choiceCount` cards and puts the one the player picks into their
    /// hand with `pool.costReduction` knocked off its cost, to be played whenever they like. This is what
    /// "Something Happens" does.
    ///
    /// Unlike <see cref="DiscoverFromPool"/> the options are NOT filtered to what can resolve on the
    /// current board — the card is being banked, not cast, so a summon with nowhere to land right now is
    /// a perfectly good pick for next turn.
    ///
    /// Needs no card in play (thisCard may be null), so a minion trigger can offer a Discover too.
    /// </summary>
    public void DiscoverToHand(CardPoolSO pool)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_DiscoverToHand(pool));
    }
    public IEnumerator _DiscoverToHand(CardPoolSO pool)
    {
        if (pool == null)
        {
            Debug.LogWarning("DiscoverToHand: no pool assigned");
            yield break;
        }

        List<CardSO> candidates = pool.UsableCards();
        if (candidates.Count == 0)
        {
            Debug.LogWarning("DiscoverToHand: pool '" + pool.name + "' has no usable card");
            yield break;
        }

        Agent caster = selectedAgent != null
            ? selectedAgent
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        CardSO chosen = null;
        yield return GameManager.Instance.StartCoroutine(
            _ChooseFromPool(candidates, pool.choiceCount, c => chosen = c));

        if (chosen == null) yield break;

        CardController created = caster.AddCard(chosen);
        if (created == null)
        {
            // AddCard refuses at 7 cards. The pick is lost, as an overdrawn card would be.
            Debug.LogWarning("DiscoverToHand: hand is full, '" + chosen.cardName + "' was discarded");
            yield break;
        }

        // Discounted rather than free: the caster already paid for the card that offered this choice, so
        // that cost comes off the pick. Floored at 0 — a cheap roll is free, never negative.
        created.modal.cost = Mathf.Max(0, created.modal.cost - pool.costReduction);
        // Clear the inherited upgrade for the same reason the transform path does: on resolve GameManager
        // spawns modal.upgradedVerdion into the owner's deck, so a discovered freebie would quietly gift a
        // permanent upgrade the player never earned and the offering card never promised.
        created.modal.upgradedVerdion = null;
        created.view.UpdateView(created.modal);

        Debug.Log("discovered card (to hand): " + chosen.cardName + " at cost " + created.modal.cost
            + " (base " + chosen.cost + " - " + pool.costReduction + ")");

        yield return null;
    }

    /// <summary>
    /// The shared ask: offer up to <paramref name="count"/> distinct cards from <paramref name="candidates"/>
    /// and wait for the pick. CardChoice drives the player's panel and is inert for the AI / dry-run, which
    /// answer by writing chosenCard straight from OnWaitingCardChoice — the same split the cell and minion
    /// prompts use. Reports null through <paramref name="onChosen"/> if the play was cancelled.
    /// </summary>
    private IEnumerator _ChooseFromPool(List<CardSO> candidates, int count, Action<CardSO> onChosen)
    {
        List<CardSO> options = CardPoolSO.PickDistinct(candidates, Mathf.Max(1, count));

        chosenCard = null;
        CardChoice.Instance.Begin(options, "Choose a card", picked => chosenCard = picked);
        OnWaitingCardChoice?.Invoke(options, thisCardSO);

        while (chosenCard == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            yield return null;
        }

        // Tear down regardless of how the wait ended, so a cancelled/aborted play can't strand the panel.
        CardChoice.Instance.Cancel();

        onChosen?.Invoke(cancelRequested ? null : chosenCard);
    }

    /// <summary>
    /// Shared entry guard for the verbs that turn the played card into another one. Resolves the casting
    /// card and the agent, or reports why it can't and returns false.
    /// </summary>
    private bool TryBeginTransform(CardPoolSO pool, string verb, out CardController casting, out Agent caster)
    {
        casting = null;
        caster = null;

        if (_castingRandomSpell)
        {
            Debug.LogWarning(verb + ": already casting a rolled card, skipping to avoid recursion");
            return false;
        }

        if (pool == null)
        {
            Debug.LogWarning(verb + ": no pool assigned");
            return false;
        }

        // The card being played is the one that transforms, so it has to exist: these verbs are only
        // meaningful on a card played from hand, not on a minion trigger (where thisCard is null).
        casting = thisCard;
        if (casting == null || casting.modal == null)
        {
            Debug.LogWarning(verb + ": no card is being played");
            return false;
        }

        // Resolved lazily (when the coroutine runs) so it casts for the agent this play selected.
        caster = selectedAgent != null
            ? selectedAgent
            : (GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent);

        return true;
    }

    /// <summary>
    /// Dry-runs <paramref name="candidate"/>'s OnPlay against the current board and reports whether it
    /// could actually resolve. This is the same isTesting machinery the AI uses to test a card in hand
    /// (GameManager.TestCard), run over a CardSO instead of a CardController: every verb already has an
    /// isTesting branch that flags isTestingFailed instead of touching the board.
    ///
    /// The whole probe sits inside a PushScope, so the registers it overwrites — and isTesting itself —
    /// are restored on the way out and the real play resumes untouched.
    /// </summary>
    private IEnumerator CanResolveNow(CardSO candidate, CardController host, Action<bool> result)
    {
        bool canResolve = false;

        if (candidate == null || candidate.OnPlay == null)
        {
            result?.Invoke(false);
            yield break;
        }

        using (PushScope())
        {
            var probeActions = new Queue<IEnumerator>();

            // Set up EXACTLY as TestCard does — including leaving selectedAgent null, which the verbs'
            // isTesting branches already expect. The owner-relative ones read it off thisCard instead.
            // Deviating here would mean probing down a path the AI's own playability check never takes.
            ResetSelections();
            thisCardSO = candidate;
            thisCard = host;
            curActionsList = probeActions;

            var gm = GameManager.Instance;
            gm.isTestingFailed = false;
            gm.isTesting = true;

            candidate.OnPlay.Invoke();

            while (probeActions.Count > 0)
            {
                yield return gm.StartCoroutine(probeActions.Dequeue());
            }

            canResolve = !gm.isTestingFailed;
        }

        result?.Invoke(canResolve);
    }

    /// <summary>
    /// Turns the card being played into <paramref name="rolled"/> and plays its effect for free.
    ///
    /// The transformed card resolves through its own OnPlay chain, so it picks targets exactly as it would
    /// from hand: the player gets the normal prompt, and the AI answers it through its
    /// OnWaitingMinionSelect / OnWaitingCellSelect handlers. Those handlers score targets from thisCardSO's
    /// aiIntent, which is why the registers point at the TRANSFORMED card rather than the card that cast
    /// it — otherwise the AI would aim a damage spell using a draw spell's intent.
    /// </summary>
    private IEnumerator _TransformIntoAndPlay(CardSO rolled, CardController casting, Agent caster)
    {
        // Spin the card a full turn and become the rolled card at the halfway point, where the flip hides
        // the swap. Waiting the same duration lets the flip finish (and read) before the effect fires.
        casting.view.PlayTurnIntoAnimation(() =>
        {
            // Becoming the rolled card is also what makes thisCard the correct context for the chain
            // below: every spell's OnPlay ends in PayCardCost, which bills thisCard.modal.cost, so the
            // transformed card is billed 0 — the caster already paid for the card that rolled this.
            casting.modal.UpdateModal(rolled, caster, caster.IsPlayer());
            casting.modal.cost = 0;
            // Clear the inherited upgrade: on resolve, GameManager spawns modal.upgradedVerdion into the
            // owner's deck. Left set, rolling a basic spell would quietly gift its upgraded version — an
            // upgrade the player never earned and the casting card never promised.
            casting.modal.upgradedVerdion = null;
            casting.view.UpdateView(casting.modal);
        }, TurnIntoAnimationDuration);

        yield return new WaitForSeconds(TurnIntoAnimationDuration);

        bool cancelled = false;

        _castingRandomSpell = true;
        try
        {
            // Scope the nested play so the rolled card's selections and queue can't leak back into the
            // rest of the casting card's chain; Dispose restores every register overwritten here.
            using (PushScope())
            {
                var nestedActions = new Queue<IEnumerator>();

                // Mirror what PlayCard sets up for a real play. thisMinion is deliberately left alone —
                // PlayCard doesn't reset it either.
                ResetSelections();
                selectedAgent = caster;
                thisCardSO = rolled;
                thisCard = casting;
                curActionsList = nestedActions;

                GameManager.Instance.isTesting = false;
                rolled.OnPlay.Invoke();

                yield return GameManager.Instance.StartCoroutine(
                    GameManager.Instance.ExecuteActions(nestedActions));

                // Read before Dispose restores the snapshot value.
                cancelled = cancelRequested;
            }
        }
        finally
        {
            _castingRandomSpell = false;
        }

        if (cancelled)
        {
            // Commit-at-reveal. The play was decided the moment the card showed what it became, so walking
            // away from the transformed card's targeting FIZZLES it: the card and its mana stay spent and
            // only the unresolved effect is dropped.
            //
            // Left alone, the cancel would reach GameManager.FinishCancelPlayingCard, which refunds the
            // mana and puts the card back in hand — making cancel a free reroll the player can pull until
            // they like the result, which costs the randomness all of its weight.
            Debug.Log("transformed card's targeting cancelled; fizzling (card and mana stay spent)");
            GameManager.Instance.ClearCancelledPlay();
        }
    }

    // True if at least one currently-selected cell could actually receive a summon: empty, or holding a
    // minion pushable in the summoner's forward direction — the same rule GameManager.SummonMinion enforces
    // before it places anything. Used by the summon verbs' dry-run (isTesting) branch so a board-locked
    // summon is reported unplayable (isTestingFailed) instead of a false-positive the AI wastes its turn on.
    private bool AnySelectedCellCanReceiveSummon()
    {
        if (selectedCells.Count == 0) return false;

        Vector3Int pushDir = SummonerPushDir();
        foreach (var cellT in selectedCells)
        {
            // _SelectCell leaves a null placeholder in test mode when a valid cell exists but can't be
            // picked interactively; it already passed its own occupancy gate, so treat it as viable.
            if (cellT == null) return true;

            Vector2Int idx = GridManager.Instance.PosToGridIndex(cellT.position);
            GameObject occupant = GridManager.Instance.GetCell(idx).obj;
            if (occupant == null) return true;
            if (occupant.TryGetComponent(out MinionController m) && m.CanBePushedForward(pushDir)) return true;
        }
        return false;
    }

    // Roll only among base minions (Hexpectations / Overgrowth). AllCards also holds the upgraded
    // variants at the same cost, and these low-cost random summons are meant to hit the plain version.
    public void SummonRandomMinion(int cost)
    {
        curActionsList.Enqueue(_SummonRandomMinion(cost, allowUpgraded: false));
    }

    // Roll among base AND upgraded minions of that cost (Hexcuses). The high-cost brackets are populated
    // almost entirely by upgraded variants — cost 5 has a single base minion (Golem) against four upgraded
    // ones — so excluding them here would make a "random" summon deterministic.
    public void SummonRandomMinionAllowUpgraded(int cost)
    {
        curActionsList.Enqueue(_SummonRandomMinion(cost, allowUpgraded: true));
    }

    public IEnumerator _SummonRandomMinion(int cost, bool allowUpgraded = false)
    {
        if (DeckDatabase.Instance == null || DeckDatabase.Instance.AllCards == null)
        {
            Debug.LogWarning("SummonRandomMinion: DeckDatabase unavailable");
            yield break;
        }

        var candidates = DeckDatabase.Instance.AllCards
            .Where(c => c != null && c.cost == cost && c.health > 0 && (allowUpgraded || !c.isUpgraded))
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.LogWarning("SummonRandomMinion: no minion with cost " + cost);
            yield break;
        }

        if (GameManager.Instance.isTesting)
        {
            // Dry-run only: never instantiate. Fail the test unless a summon could actually land.
            if (!AnySelectedCellCanReceiveSummon()) GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        foreach (var cell in selectedCells)
        {
            CardSO picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            GameManager.Instance.SummonMinion(picked, cell.position);
        }

        yield return null;
    }

    public void SummonMinion(CardSO card)
    {
        //Debug.LogWarning("before summon ");

        IEnumerator cor = _summonminion(card);
        //GameManager.Instance.Addtoactions( cor);
        curActionsList.Enqueue(cor);

        //Debug.LogWarning("summon added to actions");
    }
    public IEnumerator _summonminion(CardSO card)
    {
        yield return null;

        if (GameManager.Instance.isTesting)
        {
            // Dry-run only: never instantiate. Fail the test unless a summon could actually land, so the
            // AI doesn't treat a board-locked summon (spawn area full and unpushable) as a playable move.
            if (!AnySelectedCellCanReceiveSummon()) GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        foreach (var cell in selectedCells)
        {
            //Debug.Log("summoning minion");

            GameManager.Instance.SummonMinion(card, cell.position);

        }
        //Debug.Log("summonned minion");
    }

    /// <summary>
    /// Declarative summon verb: summons `card` on a random tile of the CURRENT context owner's spawn row
    /// (thisMinion's owner — e.g. the attacked hero). A tile qualifies if it is empty OR holds a minion
    /// that can be pushed forward (SummonMinion performs the push). If no tile qualifies, nothing is
    /// summoned. Owner-relative, so it lands on the correct side even when it fires on the enemy's turn
    /// (hero summon-on-attacked passive). Single Object argument, so it is wireable from a UnityEvent.
    ///
    /// Owner is captured now, at wiring-invoke/enqueue time (when thisMinion is still the hero), so a
    /// later verb in the same list — including the summon itself, which retargets thisMinion — cannot
    /// move the summon to the wrong side.
    /// </summary>
    public void SummonMinionOnOwnSpawnRow(CardSO card)
    {
        Agent owner = thisMinion != null ? thisMinion.owner : null;
        curActionsList.Enqueue(_SummonMinionForAgentOnSpawnRow(card, owner));
    }
    public IEnumerator _SummonMinionForAgentOnSpawnRow(CardSO card, Agent owner)
    {
        if (card == null || owner == null) yield break;

        int rowIndex = SpawnRowOf(owner);
        Vector3Int pushDir = owner == GameManager.Instance.player ? Vector3Int.up : Vector3Int.down;

        List<Transform> candidates = new List<Transform>();
        foreach (var cell in GridManager.Instance.GetGrid())
        {
            if (cell.index.y != rowIndex) continue;
            if (cell.cellObj == null) continue;

            if (cell.obj == null)
                candidates.Add(cell.cellObj.transform);
            else if (cell.obj.TryGetComponent(out MinionController occupant) && occupant.CanBePushedForward(pushDir))
                candidates.Add(cell.cellObj.transform);
        }

        if (GameManager.Instance.isTesting)
        {
            // Dry-run only: never instantiate. Fail the test if no spawn tile is free or pushable.
            if (candidates.Count == 0) GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        if (candidates.Count == 0) yield break; // no empty tile and no pushable occupant — skip the summon

        Transform target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        GameManager.Instance.SummonMinion(card, target.position, owner);

        yield return null;
    }
    public void Attack()
    {
        if (GameManager.Instance.isTesting) return;

        //Debug.Log("selected agent2: " + selectedAgent.name);
        //Debug.Log("should add attack to list: ");
        curActionsList.Enqueue(_Attack());
    }

    public IEnumerator _Attack()
    {
        foreach (var target in selectedTargetMinions)
        {
            if (target == null) continue;
            // Capture the attacker: thisMinion is static and this coroutine yields, so a trigger draining
            // in between could re-point it at another reactor.
            var attacker = thisMinion;
            if (attacker == null) continue;
            // A triggered shot doesn't cost the minion its turn attack, so restore the flag to what it was
            // BEFORE the shot rather than clearing it. StartAttack runs synchronously up to its epilogue on
            // the pre-decided-target path, so it has already set isAttackedThisTurn = true by the time we
            // get here; writing an unconditional false would REFUND a manual attack the minion had already
            // spent this turn (e.g. crossbow hits the enemy hero, the Summoner passive spawns a minion, the
            // crossbow reacts to that summon and gets its click back).
            int spentBefore = attacker.attacksMadeThisTurn;
            attacker.StartAttack(target.owner, target);
            attacker.attacksMadeThisTurn = spentBefore;
            yield return new WaitForSeconds(1f);
        }
    }

    // Like Attack, but the struck target takes no counter-attack back (a "clean" reaction shot). Used by
    // the Repaired Crossbow's auto-attack on OnAnyMinionSummoned.
    public void AttackNoCounter()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_AttackNoCounter());
    }

    public IEnumerator _AttackNoCounter()
    {
        foreach (var target in selectedTargetMinions)
        {
            if (target == null) continue;
            // Same save/restore as _Attack — see the comment there for why an unconditional clear refunds
            // an already-spent manual attack.
            var attacker = thisMinion;
            if (attacker == null) continue;
            int spentBefore = attacker.attacksMadeThisTurn;
            attacker.StartAttack(target.owner, target, noCounter: true);
            attacker.attacksMadeThisTurn = spentBefore;
            yield return new WaitForSeconds(1f);
        }
    }

    public void DrawCardForEachDiedMinion()
    {
        Debug.Log("try draw card for each died minion: " + DiedMinionAmount);

        if (GameManager.Instance.isTesting) return;

        var agentToDraw = selectedAgent;
        curActionsList.Enqueue(_DrawCardForEachDiedMinion());


    }
    public IEnumerator _DrawCardForEachDiedMinion()
    {
        //yield return null;
        Debug.Log("should draw card");
        if (selectedAgent == null)
        {
            Debug.LogWarning("Agent is null, cannot draw card");
        }
        else
        {
            // Captured up front: this loop yields, and selectedAgent is shared static state that a
            // sibling triggered pass could move underneath us mid-draw.
            Agent drawer = selectedAgent;

            for (int i = 0; i < DiedMinionAmount; i++)
            {
                yield return new WaitForSeconds(1f);
                // Yielded on: an empty deck turns this into a card choice that has to resolve before the
                // next draw starts, or the second draw would preempt the first one's panel.
                yield return GameManager.Instance.StartCoroutine(drawer.DrawCardRoutine());
            }
        }
    }
    public void DrawCard()
    {
        Debug.Log("try draw card");

        if (GameManager.Instance.isTesting) return;

        var agentToDraw = selectedAgent;
        curActionsList.Enqueue(_DrawCard());
    }

    public IEnumerator _DrawCard()
    {
        //yield return null;
        Debug.Log("should draw card in 0.5 sec");

        yield return new WaitForSeconds(0.5f);

        Debug.Log("should draw card");
        if (selectedAgent == null)
        {
            Debug.LogWarning("Agent is null, cannot draw card");

        }
        else {
            // Yielded on so an empty-deck draw's card choice resolves inside this action rather than
            // racing whatever the card does next.
            yield return GameManager.Instance.StartCoroutine(selectedAgent.DrawCardRoutine());
        }

    }

    public void DrawUpgradedCardFromDeck()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_DrawUpgradedCardFromDeck());
    }

    public IEnumerator _DrawUpgradedCardFromDeck()
    {
        yield return new WaitForSeconds(0.5f);
        Agent agent = selectedAgent;
        if (agent == null) yield break;
        if (agent.hand.Count >= 7) yield break;

        if (agent.deck.Count == 0)
        {
            yield return GameManager.Instance.StartCoroutine(agent.DrawCardRoutine());
            yield break;
        }

        int upgradedIndex = -1;
        List<int> upgradedIndices = new List<int>();
        for (int i = 0; i < agent.deck.Count; i++)
        {
            if (agent.deck[i].isUpgraded)
                upgradedIndices.Add(i);
        }

        if (upgradedIndices.Count > 0)
        {
            upgradedIndex = upgradedIndices[UnityEngine.Random.Range(0, upgradedIndices.Count)];
            CardSO cardSO = agent.deck[upgradedIndex];
            agent.deck.RemoveAt(upgradedIndex);
            agent.AddCard(cardSO);
        }
        else
        {
            yield return GameManager.Instance.StartCoroutine(agent.DrawCardRoutine());
        }
    }

    public void CreateUpgradedCardFromDeck()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_CreateUpgradedCardFromDeck());
    }

    public IEnumerator _CreateUpgradedCardFromDeck()
    {
        yield return new WaitForSeconds(0.5f);
        Agent agent = selectedAgent;
        if (agent == null) yield break;
        if (agent.hand.Count >= 7) yield break;

        List<CardSO> upgraded = new List<CardSO>();
        for (int i = 0; i < agent.deck.Count; i++)
        {
            if (agent.deck[i].isUpgraded)
                upgraded.Add(agent.deck[i]);
        }

        if (upgraded.Count > 0)
        {
            CardSO chosen = upgraded[UnityEngine.Random.Range(0, upgraded.Count)];
            CardController cardObj = agent.AddCard(chosen);
            if (cardObj != null)
                cardObj.modal.upgradedVerdion = null;
        }
        else
        {
            yield return GameManager.Instance.StartCoroutine(agent.DrawCardRoutine());
        }
    }

    /// <summary>
    /// Owes selectedAgent `amount` extra cards at the start of its NEXT turn (Do Nothing). Deliberately
    /// draws nothing now — the debt is banked on the agent and paid by GameManager.DrawTurnStartCards,
    /// which is what lets a spell have a delayed effect without leaving a minion behind to trigger on.
    /// </summary>
    public void DrawExtraCardNextTurn(int amount)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_DrawExtraCardNextTurn(amount));
    }
    public IEnumerator _DrawExtraCardNextTurn(int amount)
    {
        // Resolved lazily so it banks the debt on the agent selected by this play, not a stale one.
        if (selectedAgent == null)
        {
            Debug.LogWarning("DrawExtraCardNextTurn: no selected agent");
            yield break;
        }

        selectedAgent.pendingExtraDraws += amount;
        Debug.Log("owed extra draws next turn: " + selectedAgent.pendingExtraDraws);

        yield return null;
    }

    public void DrawCardFromOpponentDeck()
    {
        Debug.Log("try draw card");

        if (GameManager.Instance.isTesting) return;

        var agentToDraw = selectedAgent;
        curActionsList.Enqueue(_DrawCardFromOpponentDeck());
    }

    public IEnumerator _DrawCardFromOpponentDeck()
    {
        //yield return null;
        Debug.Log("should draw card in 0.5 sec");

        yield return new WaitForSeconds(0.5f);

        Debug.Log("should draw card");
        if (selectedAgent == null)
        {
            Debug.LogWarning("Agent is null, cannot draw card");
        }
        else
        {
            var opponent = selectedAgent.IsPlayer() ? GameManager.Instance.opponent : GameManager.Instance.player;

            Debug.Log("opponent: " + opponent);


            var card = opponent.RemoveRandomCardFromDeck();

            Debug.Log("card to draw: " + card);

            selectedAgent.AddCard(card, opponent.deckViewHandler.transform);
        }

    }
    public void AddMana(int amount)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_AddMana(amount));
    }

    public IEnumerator _AddMana(int amount)
    {
        yield return null;

        selectedAgent.availibleMana += amount;
    }

    public void ChangeMinionsCost(int value)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_ChangeMinionsCost(value));
    }
    public IEnumerator _ChangeMinionsCost(int value)
    {
        foreach (var card in selectedCards)
        {
            card.modal.cost = Mathf.Clamp(card.modal.cost+value, 0, int.MaxValue);
            card.view.UpdateView(card.modal);
        }

        yield return null;
    }
    public void ChangeCardAttack(int value)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_ChangeCardAttack(value));
    }
    public IEnumerator _ChangeCardAttack(int value)
    {
        foreach (var card in selectedCards)
        {
            if (card == null || card.modal == null) continue;

            card.modal.attack += value;
            card.view.UpdateView(card.modal);
        }

        yield return null;
    }
    public void ChangeCardHealth(int value)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_ChangeCardHealth(value));
    }
    public IEnumerator _ChangeCardHealth(int value)
    {
        foreach (var card in selectedCards)
        {
            if (card == null || card.modal == null) continue;

            // Keep defHealth (max health) in sync so the summoned minion reflects the buff.
            card.modal.health += value;
            card.modal.defHealth += value;
            card.view.UpdateView(card.modal);
        }

        yield return null;
    }
    public void ChangeMinionAttack(int value)
    {
        //GameManager.Instance.Addtoactions( _ChangeMinionAttack(selectedMinion, value));
        if (GameManager.Instance.isTesting) return;
        Debug.LogWarning("change attack added to actions");
        curActionsList.Enqueue(_ChangeMinionAttack(value));
    }

    public void ChangeMinionAttack(MinionController minion, int value)
    {
        //GameManager.Instance.Addtoactions(_ChangeMinionAttack(minion, value));
        curActionsList.Enqueue(_ChangeMinionAttack(minion, value));

        //Debug.LogWarning("change attack added to actions");
    }
    public IEnumerator _ChangeMinionAttack(int value)
    {
        foreach (var minion in selectedMinions)
        {
            minion.modal.attack += value;
            minion.view.UpdateView(minion.modal);
            Debug.LogWarning("öinion attack changed to :" + minion.card.attack);
        }

        yield return null;
    }
    public IEnumerator _ChangeMinionAttack(MinionController minion, int value)
    {
        if (minion == null) {

            Debug.LogWarning("minion is null, actions canceled");
            yield break;

        }

        minion.modal.attack += value;
        minion.view.UpdateView(minion.modal);

        if (minion.card != null)
            minion.card.attack = minion.modal.attack;

        yield return null;

    }
    public void ChangeMinionAttackThisTurn(int value)
    {
    }
    public void DoubleMinionAttack()
    {
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions(_ChangeMinionAttack(minion, value));
        curActionsList.Enqueue(_DoubleMinionAttack());

        //Debug.LogWarning("change attack added to actions");
    }
    public IEnumerator _DoubleMinionAttack()
    {
        foreach (var minion in selectedMinions)
        {
            minion.modal.attack += minion.modal.attack;
            minion.view.UpdateView(minion.modal);
        }

        yield return null;
    }
    public void DoubleMinionHealth()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_DoubleMinionHealth());
    }
    public IEnumerator _DoubleMinionHealth()
    {
        foreach (var minion in selectedMinions)
        {
            minion.modal.health += minion.modal.health;
            minion.view.UpdateView(minion.modal);
        }

        yield return null;
    }
    public IEnumerator _ChangeMinionDefHealth(int value)
    {
        foreach (var minion in selectedMinions)
        {
            minion.modal.health += value;
            minion.modal.defHealth += value;
            minion.view.UpdateView(minion.modal);
        }

        yield return null;
    }
    public void ChangeMinionDefHealth(int value)
    {
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions(_ChangeMinionHealth(value));
        curActionsList.Enqueue(_ChangeMinionHealth(value));

        Debug.LogWarning("change health added to actions");
    }

    public IEnumerator _ChangeMinionHealth(int value)
    {
        Debug.Log("try change minions health: " + selectedMinions.Count);
        DiedMinionAmount = 0;
        foreach (var minion in selectedMinions)
        {
            Debug.Log("minion should change health: "+ minion.card.cardName);

            if (value < 0)
            {
                bool isDied = minion.TakeDamage(Mathf.Abs(value));

                if (isDied) { 
             
                    DiedMinionAmount++;

                    yield return new WaitForSeconds(1f);
                }
            }
            else
            {
                minion.modal.health += value;
                minion.view.UpdateView(minion.modal);
            }
        }
        Debug.Log("died minion amount: " + DiedMinionAmount);
        /*if (selectedMinion != null)
        {
            if (value < 0)
            {
                selectedMinion.TakeDamage(Mathf.Abs(value));
            }
            else
            {
                selectedMinion.modal.health += value;
                selectedMinion.view.UpdateView(selectedMinion.modal);
            }
        }*/

        yield return null;
    }
    public void ChangeMinionHealth(int value)
    {
        if (GameManager.Instance.isTesting) { 
            Debug.LogWarning("testing. change health canceled.");
            return;
        }

        Debug.LogWarning("change health added to actions");

        //GameManager.Instance.Addtoactions(_ChangeMinionDefHealth(value));
        curActionsList.Enqueue(_ChangeMinionHealth(value));
    }
    public void ChangeTargetMinionHealth(int value)
    {
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions(_ChangeMinionDefHealth(value));
        curActionsList.Enqueue(_ChangeTargetMinionHealth(value));
    }
    public IEnumerator _ChangeTargetMinionHealth(int value)
    {
        foreach (var minion in selectedTargetMinions)
        {
            if (minion == null) continue;
            if (value < 0)
            {
                minion.TakeDamage(Mathf.Abs(value));
            }
            else
            {
                minion.modal.health += value;
                minion.view.UpdateView(minion.modal);
            }
        }
        yield return new WaitForSeconds(1);
    }
    public void DestroyCard()
    {
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions(_SelectMinion());
        curActionsList.Enqueue(_SelectMinion());

        //Debug.LogWarning("selecminion added to actions");

    }
    public IEnumerator _DestroyCard()
    {
        while (selectedMinion == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting minion");

            yield return null;
        }
        if (cancelRequested) yield break;
       // Debug.Log("selected minion");


    }

    // What the board popup says when a minion loses its move, and which FloatingTextConfig preset draws it.
    private const string CrippledPopupText = "Crippled";
    private const string CrippledPopupStyle = "debuff";

    public void SetCanMove(bool value)
    {
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions(_SelectMinion());
        curActionsList.Enqueue(_SetCanMove(value));
    }

    public IEnumerator _SetCanMove(bool value)
    {
        while (selectedMinion == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting minion");
            yield return null;
        }
        if (cancelRequested) yield break;
        bool wasCrippled = !selectedMinion.modal.canMove;
        selectedMinion.modal.canMove = value;

        if (!value && !selectedMinion.modal.desc.Contains("Crippled"))
        {
            string sep = string.IsNullOrEmpty(selectedMinion.modal.desc) ? "" : "\n";
            selectedMinion.modal.desc += sep + "Crippled.";
        }

        // Announce the moment it lands, not every re-application: crippling an already-crippled minion
        // changes nothing on the board, so it shouldn't flash a popup that says it did.
        if (!value && !wasCrippled)
            selectedMinion.ShowFloatingText(CrippledPopupText, CrippledPopupStyle);
    }

    public void ChangeMinionStatsByOwnership(int value)
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_ChangeMinionStatsByOwnership(value));
    }

    public IEnumerator _ChangeMinionStatsByOwnership(int value)
    {
        Agent caster = thisCard != null ? thisCard.modal.owner : null;
        foreach (var minion in selectedMinions)
        {
            bool isFriendly = caster != null && minion.owner == caster;
            int sign = isFriendly ? 1 : -1;
            int attackChange = sign * value;
            int healthChange = sign * value;

            minion.modal.attack += attackChange;
            if (minion.modal.attack < 0) minion.modal.attack = 0;

            if (healthChange < 0)
            {
                minion.TakeDamage(Mathf.Abs(healthChange));
            }
            else
            {
                minion.modal.health += healthChange;
                minion.modal.defHealth += healthChange;
            }

            minion.view.UpdateView(minion.modal);
        }
        yield return null;
    }

    public void Wait()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_Wait());
    }
    public IEnumerator _Wait( )
    {
        yield return new WaitForSeconds(0.5f);
    }

    public void SwitchSide()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SwitchSide());
    }

    public IEnumerator _SwitchSide()
    {
        selectedMinion.modal.isPlayerMinion = thisMinion.modal.isPlayerMinion;
        selectedMinion.owner.minions.Remove(selectedMinion);
        thisMinion.owner.minions.Add(selectedMinion);
        selectedMinion.owner = thisMinion.owner;

        // The minion just changed hands: clear the attack eligibility/clickability it carried from its
        // former owner, or that player could keep attacking with a minion that is now the enemy's.
        selectedMinion.ClearAttackReadiness();

        selectedMinion.view.UpdateView(selectedMinion.modal);

        yield return null;



    }
    public void AddBonusEventsToMinionsOnTookDamage()
    {
        //if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_AddBonusEventsToMinionsOnTookDamage());
    }

    public IEnumerator _AddBonusEventsToMinionsOnTookDamage()
    {
        // Wires this card's BonusEvents into each selected minion's OnTookDamage trigger.
        // Capture thisCard locally so the listener doesn't read a later-reassigned static.
        var sourceCard = thisCard;
        if (sourceCard == null || sourceCard.modal == null || sourceCard.modal.BonusEvents == null)
        {
            Debug.LogWarning("AddBonusEventsToMinionsOnTookDamage: source card / BonusEvents missing");
            yield break;
        }

        var bonusEvents = sourceCard.modal.BonusEvents;

        foreach (var minion in selectedMinions)
        {
            if (minion == null || minion.modal == null) continue;

            if (minion.modal.OnTookDamage == null)
                minion.modal.OnTookDamage = new UnityEvent();

            minion.modal.OnTookDamage.AddListener(bonusEvents.Invoke);
        }

        yield return null;
    }
    public void HealAgent(int amount)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_HealAgent(amount));
    }

    public IEnumerator _HealAgent(int amount)
    {
        if (selectedAgent == null || selectedAgent.hero == null)
        {
            Debug.LogWarning("HealAgent: no selected agent or hero");
            yield break;
        }

        var hero = selectedAgent.hero;
        hero.modal.health = Mathf.Min(hero.modal.health + amount, hero.modal.defHealth);
        hero.view.UpdateView(hero.modal);
        yield return null;
    }

    public void ChangeHeroAttack(int amount)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_ChangeHeroAttack(amount));
    }

    public IEnumerator _ChangeHeroAttack(int amount)
    {
        if (selectedAgent == null || selectedAgent.hero == null)
        {
            Debug.LogWarning("ChangeHeroAttack: no selected agent or hero");
            yield break;
        }

        var hero = selectedAgent.hero;
        hero.modal.attack += amount;
        hero.view.UpdateView(hero.modal);
        yield return null;
    }

    public void DamageAgent(int amount)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_DamageAgent(amount));
    }

    public IEnumerator _DamageAgent(int amount)
    {
        if (selectedAgent == null || selectedAgent.hero == null)
        {
            Debug.LogWarning("DamageAgent: no selected agent or hero");
            yield break;
        }

        selectedAgent.hero.TakeDamage(amount);
        yield return null;
    }

    public void SwapSelectedMinionWithMinionInFront()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_SwapSelectedMinionWithMinionInFront());
    }
    public IEnumerator _SwapSelectedMinionWithMinionInFront()
    {
        if (selectedMinion == null) yield break;

        Vector3Int dir = SummonerPushDir();
        Vector3Int frontPos = Vector3Int.RoundToInt(selectedMinion.transform.position) + dir;
        Vector2Int frontIdx = GridManager.Instance.PosToGridIndex(frontPos);

        if (GridManager.Instance.IsOutSideOfGrid(frontIdx)) yield break;

        var frontCell = GridManager.Instance.GetCell(frontIdx);
        var frontMinion = frontCell.obj?.GetComponent<MinionController>();

        if (frontMinion == null) yield break;

        selectedMinion.SwapPositionsWith(frontMinion);
        yield return null;
    }

    public void StoreSelectedMinionAsTarget()
    {
        curActionsList.Enqueue(_StoreSelectedMinionAsTarget());
    }
    public IEnumerator _StoreSelectedMinionAsTarget()
    {
        selectedTargetMinions.Clear();
        if (selectedMinion != null)
            selectedTargetMinions.Add(selectedMinion);
        selectedMinion = null;
        yield return null;
    }

    public void SwapSelectedMinionWithTarget()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_SwapSelectedMinionWithTarget());
    }
    public IEnumerator _SwapSelectedMinionWithTarget()
    {
        if (selectedMinion == null || selectedTargetMinions.Count == 0) yield break;
        var target = selectedTargetMinions[0];
        if (target == null || target == selectedMinion) yield break;

        selectedMinion.SwapPositionsWith(target);
        yield return null;
    }

    public void SummonFriendlyMinionsDiedInSelectedCells()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SummonFriendlyMinionsDiedInSelectedCells());
    }

    public IEnumerator _SummonFriendlyMinionsDiedInSelectedCells()
    {
        foreach (var item in selectedCells)
        {
            var cell = GridManager.Instance.GetCell(item.transform.position);
            var obj = cell.obj;

            if (obj != null) continue;

            var cellController = cell.cellObj.GetComponent<CellController>();

            var deadMinionsList = selectedAgent.IsPlayer() ? cellController.PlayerMinionsDiedHere : cellController.EnemyMinionsDiedHere;

            if (deadMinionsList.Count == 0) continue;

            var minionCard = deadMinionsList[deadMinionsList.Count -1];

            GameManager.Instance.SummonMinion(minionCard, cell.cellObj.transform.position);

            yield return new WaitForSeconds(0.5f);
        }

        yield return null;
    }

    public void RemoveSummoningSickness()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_RemoveSummoningSickness());
    }

    public IEnumerator _RemoveSummoningSickness()
    {
        if (thisMinion != null)
            thisMinion.age = 1;
        yield return null;
    }

    public void KillStrongestEnemyMinion()
    {
        if (GameManager.Instance.isTesting) return;
        curActionsList.Enqueue(_KillStrongestEnemyMinion());
    }

    public IEnumerator _KillStrongestEnemyMinion()
    {
        Agent enemy;
        if (thisCard != null)
            enemy = thisCard.modal.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
        else
            enemy = GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player;

        MinionController strongest = null;
        int bestScore = int.MinValue;
        foreach (var m in enemy.minions)
        {
            if (m == null) continue;
            int score = m.modal.attack + m.modal.health;
            if (score > bestScore) { bestScore = score; strongest = m; }
        }

        if (strongest != null)
        {
            strongest.TakeDamage(strongest.modal.health + strongest.modal.armor);
            yield return new WaitForSeconds(1f);
        }
        else
        {
            yield return null;
        }
    }

    public void SummonMinionDoublePush(CardSO card)
    {
        curActionsList.Enqueue(_SummonMinionDoublePush(card, 0));
    }

    public void SummonMinionDoublePushBuff(CardSO card)
    {
        curActionsList.Enqueue(_SummonMinionDoublePush(card, 2));
    }

    private IEnumerator _SummonMinionDoublePush(CardSO card, int attackBuff)
    {
        yield return null;

        if (GameManager.Instance.isTesting)
        {
            if (!AnySelectedCellCanReceiveSummon()) GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        foreach (var cell in selectedCells)
        {
            Vector2Int idx = GridManager.Instance.PosToGridIndex(cell.position);
            GameObject occupantObj = GridManager.Instance.GetCell(idx).obj;

            if (occupantObj != null && occupantObj.TryGetComponent(out MinionController occupant))
            {
                Vector3Int pushDir = SummonerPushDir();

                // First push (selectability guarantees tile 1 is empty)
                occupant.PushForward(pushDir);

                yield return _Wait();

                // Second push attempt
                Vector3Int secondTarget = Vector3Int.RoundToInt(occupant.gridEntity.WorldPos) + pushDir;
                Vector2Int secondIdx = GridManager.Instance.PosToGridIndex(secondTarget);
                if (!GridManager.Instance.IsOutSideOfGrid(secondIdx) && GridManager.Instance.GetCell(secondIdx).obj == null)
                {
                    occupant.PushForward(pushDir);
                }
                else
                {
                    MinionController blocker = null;
                    if (!GridManager.Instance.IsOutSideOfGrid(secondIdx))
                    {
                        var blockerObj = GridManager.Instance.GetCell(secondIdx).obj;
                        if (blockerObj != null) blocker = blockerObj.GetComponent<MinionController>();
                    }
                    occupant.FailedMove((Vector3)pushDir, blocker);
                }

                occupant.TakeDamage(1);

                if (attackBuff > 0 && occupant != null && occupant.modal.health > 0)
                {
                    occupant.modal.attack += attackBuff;
                    occupant.view.UpdateView(occupant.modal);
                }
            }

            GameManager.Instance.SummonMinion(card, cell.position);
        }
    }
}
