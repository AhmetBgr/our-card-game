using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.DebugUI;

public enum MinionType
{
    Any,Beast
}
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
    public static MinionController selectedTargetMinion = null;
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
        private readonly MinionController _selectedTargetMinion;
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
            MinionController selectedTargetMinion,
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
            _selectedTargetMinion = selectedTargetMinion;
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
            ActionHolder.selectedTargetMinion = _selectedTargetMinion;
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
            selectedTargetMinion,
            selectedAgent,
            thisMinion,
            thisCardSO,
            thisCard,
            new Queue<IEnumerator>(curActionsList), 
            DiedMinionAmount);
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
        selectedAgent = thisMinion.owner == GameManager.Instance.player ? GameManager.Instance.opponent : GameManager.Instance.player;
        Debug.Log("selected agent: " + selectedAgent.name);
        yield return null;

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
            if (cell.index.y == rowIndex && cell.obj == null)
            {
                selectableCells.Add(cell.cellObj.transform);
                selectableIndexes.Add(cell.index);
            }
        }

        if (GridCellSelectionManager.Instance != null)
        {
            GridCellSelectionManager.Instance.BeginSelection(
                selectableIndexes,
                hovered => new[] { hovered });
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

        while (selectedcell == null && !cancelRequested)
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
                });
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

        while (selectedcell == null && !cancelRequested)
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
                });
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

        while (selectedcell == null && !cancelRequested)
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
            minion.selectable.SetSelectable(true);
        }

        if (GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.curState = Player.State.SelectingMinion;

        }

        List<MinionController> friendlyMinions = new List<MinionController>();

        friendlyMinions = selectableminions.Where(minion => (!minion.modal.isPlayerMinion && !GameManager.Instance.isPlayerTurn) || (minion.modal.isPlayerMinion && GameManager.Instance.isPlayerTurn)).ToList();

        OnWaitingMinionSelect?.Invoke(selectableminions, thisCardSO);

        if (selectableminions.Count == 0 && GameManager.Instance.isTesting)
        {
            Debug.LogWarning("test failed");

            GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        while (selectedMinion == null && !cancelRequested)
        {
            //Debug.Log("selecting minion");

            yield return null;
        }
        if (cancelRequested) yield break;
        selectedMinions.Clear();
        selectedMinions.Add(selectedMinion.GetComponent<MinionController>());
        foreach (var cell in grid)
        {
            cell.obj?.GetComponent<MinionController>()
                .selectable.SetSelectable(false);
        }

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
                hovered => selectableIndexes.Where(i => i.x == hovered.x));
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

        while (selectedcell == null && !cancelRequested)
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

        curActionsList.Enqueue(_SelectRandomMinionInRange(opponent));
    }
    public void SelectRandomFriendlyMinionInRange()
    {
        Agent player = null;

        if (thisMinion != null)
        {
            player = thisMinion.owner;
        }
        else
        {
            player = GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent;
        }

        curActionsList.Enqueue(_SelectRandomFriendlyMinionInRange(player));
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
            Debug.Log("checking if minion is in range: ");
            if ((minion.transform.position - thisMinion.transform.position).magnitude < thisMinion.modal.range + 1 && minion != thisMinion)
            {
                Debug.Log("minion is in range: ");

                minionsInRange.Add(minion);
            }
        }
        selectedMinions.Clear();

        if (minionsInRange.Count > 0)
        {
            selectedMinion = minionsInRange[UnityEngine.Random.Range(0, minionsInRange.Count)];
            selectedMinions.Add(selectedMinion);
            Debug.Log("selected minion: " + selectedMinion.card.cardName);

        }
        else
        {
            selectedMinion = null;
        }

            
            yield return null;
    }
    public IEnumerator _SelectRandomFriendlyMinionInRange(Agent thisAgent)
    {
        List<MinionController> minionsInRange = new List<MinionController>();
        Debug.Log("opponent: " + thisAgent.name);
        selectedMinions.Clear();
        foreach (var minion in thisAgent.minions)
        {
            if (minion == thisMinion) continue;
            Debug.Log("checking if minion is in range: ");
            if ((minion.transform.position - thisMinion.transform.position).magnitude < thisMinion.modal.range + 1)
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
    public void SelectThisMinionsLastTargetAsTarget()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectThisMinionsLastTargetAsTarget());

    }
    public IEnumerator _SelectThisMinionsLastTargetAsTarget()
    {
        selectedTargetMinion = thisMinion.LastTarget;
        selectedTargetMinions.Clear();
        selectedTargetMinions.Add(selectedTargetMinion);
        yield return null;
    }


    #endregion
    public void AtestAction()
    {

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
    public void Attack()
    {
        if (GameManager.Instance.isTesting) return;

        //Debug.Log("selected agent2: " + selectedAgent.name);
        //Debug.Log("should add attack to list: ");
        curActionsList.Enqueue(_Attack());
    }

    public IEnumerator _Attack()
    {
        //Debug.Log("should attack minion: " + thisMinion.modal.name + " : " + selectedMinion.modal.name);
        thisMinion.StartAttack(selectedMinion.owner, selectedTargetMinion);
        thisMinion.isAttackedThisTurn = false;
        yield return null;
    }

    public void DrawCardForEachDiedMinion()
    {
        Debug.Log("try draw card for each died minion: " + DiedMinionAmount);

        if (GameManager.Instance.isTesting) return;

        var agentToDraw = selectedAgent;
        for (int i = 0; i < DiedMinionAmount; i++)
        {
            curActionsList.Enqueue(_DrawCard());
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
            card.modal.cost = Mathf.Clamp(card.modal.cost+value, 1, int.MaxValue);
            card.view.UpdateView(card.modal);
        }

        yield return null;
    }
    public void ChangeMinionAttack(int value)
    {
        //GameManager.Instance.Addtoactions( _ChangeMinionAttack(selectedMinion, value));
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
        //GameManager.Instance.Addtoactions(_ChangeMinionAttack(minion, value));
        curActionsList.Enqueue(_DoubleMinionAttack());

        //Debug.LogWarning("change attack added to actions");
    }
    public IEnumerator _DoubleMinionAttack()
    {
        foreach (var minion in selectedMinions)
        {
            minion.modal.attack += minion.modal.attack;
            minion.view.UpdateView(selectedMinion.modal);
            //Debug.LogWarning("öinion attack changed to :" + selectedMinion.card.attack);
        }

        yield return null;
    }
    public IEnumerator _ChangeMinionDefHealth( int value)
    {
        foreach (var minion in selectedMinions)
        {
            minion.modal.health += value;
            minion.modal.defHealth += value;
            minion.view.UpdateView(selectedMinion.modal);
            //Debug.LogWarning("minion health changed to :" + selectedMinion.modal.defHealth);
        }

        /*if (selectedMinion != null)
        {
            selectedMinion.modal.health += value;
            selectedMinion.modal.defHealth += value;
            selectedMinion.view.UpdateView(selectedMinion.modal);
            Debug.LogWarning("minion health changed to :" + selectedMinion.modal.defHealth);
        }*/

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

                if (isDied)
                    DiedMinionAmount++;
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
        if (value < 0)
        {
            selectedTargetMinion.TakeDamage(Mathf.Abs(value));
        }
        else
        {
            selectedTargetMinion.modal.health += value;
            selectedTargetMinion.view.UpdateView(selectedTargetMinion.modal);
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
        while (selectedMinion == null && !cancelRequested)
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
        while (selectedMinion == null && !cancelRequested)
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
        // Adds this card's OnPlay actions to selected minions' OnTookDamage triggers.
        // Requires: ActionHolder.thisCard (current card controller) and selectedMinions (targets).
        if (thisCard == null)
        {
            Debug.LogWarning("AddToMinionsOnTookDamage: thisCard is null");
            yield break;
        }

        if (thisCard.modal == null || thisCard.modal.OnPlay == null)
        {
            Debug.LogWarning("AddToMinionsOnTookDamage: thisCard.modal or thisCard.modal.OnPlay is null");
            yield break;
        }

        /*if (selectedMinion == null || selectedMinion.modal == null)
            continue;*/

        if (selectedMinion.modal.OnTookDamage == null)
            selectedMinion.modal.OnTookDamage = new UnityEvent();

        // Runtime-only wiring: when the minion takes damage, run this card's OnPlay actions.
        selectedMinion.modal.OnTookDamage.AddListener(thisCard.modal.BonusEvents.Invoke);
        //selectedMinion.modal.OnTookDamage = thisCard.modal.BonusEvents;

        Debug.Log("added to minions on took damage events: " + selectedMinion.modal.name);
        /*foreach (var minion in selectedMinions)
        {
            if (minion == null || minion.modal == null)
                continue;

            if (minion.modal.OnTookDamage == null)
                minion.modal.OnTookDamage = new UnityEvent();

            // Runtime-only wiring: when the minion takes damage, run this card's OnPlay actions.
            minion.modal.OnTookDamage.AddListener(thisCard.modal.BonusEvents.Invoke);
        }*/



        yield return null;
    }
}
