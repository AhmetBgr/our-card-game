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
            Queue<IEnumerator> curActionsList, int diedMinionAmount)
        {
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
        selectedMinions.Clear();
        selectedTargetMinions.Clear();
        selectedCells.Clear();
        selectedCards.Clear();
    }

    public static event Action<SelectableParameters> OnSelect;
    public static event Action<List<Transform>, CardSO> OnWaitingCellSelect;
    public static event Action<List<MinionController>, CardSO> OnWaitingMinionSelect;



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
                hovered => new[] { hovered },
                previewOccupantPush: true,
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

        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }
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
                hovered => new[]
                {
                    hovered,
                    new Vector2Int(hovered.x, hovered.y - 1),
                    new Vector2Int(hovered.x, hovered.y - 2),
                },
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

        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }

        if (GridCellSelectionManager.Instance != null) GridCellSelectionManager.Instance.EndSelection();
        if (cancelRequested) yield break;

        selectedCells.Clear();
        Vector2Int centerIndex = GridManager.Instance.PosToGridIndex(selectedcell.position);
        HashSet<Vector2Int> areaIndexes = new HashSet<Vector2Int>
        {
            centerIndex,
            new Vector2Int(centerIndex.x, centerIndex.y - 1),
            new Vector2Int(centerIndex.x, centerIndex.y - 2),
        };

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
                hovered => new[]
                {
                    hovered,
                    new Vector2Int(hovered.x + 1, hovered.y),
                    new Vector2Int(hovered.x - 1, hovered.y),
                    new Vector2Int(hovered.x, hovered.y + 1),
                    new Vector2Int(hovered.x, hovered.y - 1),
                },
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

        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            yield return null;
        }

        if (GridCellSelectionManager.Instance != null) GridCellSelectionManager.Instance.EndSelection();
        if (cancelRequested) yield break;

        selectedCells.Clear();
        Vector2Int centerIndex = GridManager.Instance.PosToGridIndex(selectedcell.position);
        HashSet<Vector2Int> areaIndexes = new HashSet<Vector2Int>
        {
            centerIndex,
            new Vector2Int(centerIndex.x + 1, centerIndex.y),
            new Vector2Int(centerIndex.x - 1, centerIndex.y),
            new Vector2Int(centerIndex.x, centerIndex.y + 1),
            new Vector2Int(centerIndex.x, centerIndex.y - 1),
        };

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

        OnWaitingCellSelect?.Invoke(selectableCells, thisCardSO);

        while (selectedcell == null && !cancelRequested && !GameManager.Instance.isTesting)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }
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
            Debug.Log($"[HUNTER] _SelectRandomEnemyMinion: {candidates.Count} candidate(s) -> CHOSE '{selectedMinion.card?.cardName}'");
        }
        else
        {
            Debug.Log($"[HUNTER] _SelectRandomEnemyMinion: 0 candidates from {selectedCells.Count} cells -> passive fizzles");
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

        Vector3Int pos = Vector3Int.RoundToInt(selectedMinion.transform.position) + dir;
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

    public void SummonRandomMinion(int cost)
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SummonRandomMinion(cost));
    }
    public IEnumerator _SummonRandomMinion(int cost)
    {
        if (DeckDatabase.Instance == null || DeckDatabase.Instance.AllCards == null)
        {
            Debug.LogWarning("SummonRandomMinion: DeckDatabase unavailable");
            yield break;
        }

        var candidates = DeckDatabase.Instance.AllCards
            .Where(c => c != null && c.cost == cost && c.health > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.LogWarning("SummonRandomMinion: no minion with cost " + cost);
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

        if (GameManager.Instance.isTesting) return;

        IEnumerator cor = _summonminion(card);
        //GameManager.Instance.Addtoactions( cor);
        curActionsList.Enqueue(cor);

        //Debug.LogWarning("summon added to actions");
    }
    public IEnumerator _summonminion(CardSO card)
    {
        yield return null;

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
        if (GameManager.Instance.isTesting) return;

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
            thisMinion.StartAttack(target.owner, target);
            thisMinion.isAttackedThisTurn = false;
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
            for (int i = 0; i < DiedMinionAmount; i++)
            {
                yield return new WaitForSeconds(1f);
                selectedAgent.DrawCard();
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
            selectedAgent.DrawCard();
        }

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
        selectedMinion.modal.canMove = value;
        //Debug.Log("selected minion");
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
}
