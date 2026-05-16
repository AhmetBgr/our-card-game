using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
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
    public static List<CardController> selectedCards = new List<CardController>();

    public static MinionController selectedMinion = null;
    public static MinionController selectedTargetMinion = null;
    public static Agent selectedAgent = null;
    public static MinionController thisMinion = null;
    public static CardSO thisCardSO = null;
    public static CardController thisCard = null;

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
            Queue<IEnumerator> curActionsList)
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
            new Queue<IEnumerator>(curActionsList));
    }

    public static event Action<SelectableParameters> OnSelect;
    public static event Action<List<Transform>, CardSO> OnWaitingCellSelect;
    public static event Action<List<MinionController>, CardSO> OnWaitingMinionSelect;

    #region SELECTION

    public void SelectThisAgent()
    {
        //Debug.Log("adding select agent to list: " + curActionsList);

        curActionsList.Enqueue(_SelectThisAgent());
    }
    public IEnumerator _SelectThisAgent()
    {
        selectedAgent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent;
        //Debug.Log("selected agent: " + selectedAgent.name);
        yield return null;

    }
    public void SelectFriendlyAgent()
    {
        //Debug.Log("adding select agent to list: " + curActionsList);

        curActionsList.Enqueue(_SelectThisMinion());

        curActionsList.Enqueue(_SelectFriendlyAgent());
    }
    public IEnumerator _SelectFriendlyAgent()
    {

        selectedAgent = thisMinion.owner;
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
        int rowIndex = 2;
        foreach (var cell in grid)
        {
            if (cell.index.y == rowIndex)
            {
                cell.cellObj.GetComponent<CellController>().selectable.SetSelectable(true);
                selectableCells.Add(cell.cellObj.transform);
            }
            else
            {
                cell.cellObj.GetComponent<CellController>().selectable.SetSelectable(false);
            }
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
        if (cancelRequested) yield break;

        selectedMinions.Clear();

        foreach (var cell in grid)
        {
            cell.cellObj.GetComponent<CellController>()
                .selectable.SetSelectable(false);

            if (cell.obj != null && cell.obj.transform.position.x == selectedcell.position.x)
            {
                selectedMinions.Add(cell.obj.GetComponent<MinionController>());
            }
        }

        //Debug.Log("selected minion at collumn: " + selectedcell.position.x);

    }

    public void SelectThisMinion()
    {
        if (GameManager.Instance.isTesting) return;

        curActionsList.Enqueue(_SelectThisMinion());
    }
    public IEnumerator _SelectThisMinion()
    {
        selectedMinion = thisMinion;

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
        }
        else
        {
            opponent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player;
        }

        curActionsList.Enqueue(_SelectRandomMinion(opponent.minions));
    }
    public void SelectRandomEnemyMinionInRange()
    {
        Agent opponent = null;

        opponent = GameManager.Instance.isPlayerTurn ? GameManager.Instance.opponent : GameManager.Instance.player;

        curActionsList.Enqueue(_SelectRandomMinionInRange(opponent));
    }
    public void SelectRandomFriendlyMinionInRange()
    {
        Agent player = null;

        player = GameManager.Instance.isPlayerTurn ? GameManager.Instance.player : GameManager.Instance.opponent;

        curActionsList.Enqueue(_SelectRandomFriendlyMinionInRange(player));
    }
    public IEnumerator _SelectRandomMinion(List<MinionController> minions)
    {
        selectedMinion = minions[UnityEngine.Random.Range(0, minions.Count)];

        yield return null;
    }
    public IEnumerator _SelectRandomMinionInRange(Agent agent)
    {
        List<MinionController> minionsInRange = new List<MinionController>();
        //Debug.Log("opponent: " + agent.name);

        foreach (var minion in agent.minions)
        {
            //Debug.Log("checking if minion is in range: ");
            if ((minion.transform.position - thisMinion.transform.position).magnitude < thisMinion.modal.range + 1 && minion != thisMinion)
            {
                //Debug.Log("minion is in range: ");

                minionsInRange.Add(minion);
            }
        }

        if (minionsInRange.Count > 0)
        {
            selectedMinion = minionsInRange[UnityEngine.Random.Range(0, minionsInRange.Count)];
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

    public IEnumerator _SelectCell(int rowIndex)
    {
        var grid = GridManager.Instance.GetGrid();
        List<Transform> selectableCells = new List<Transform>();
        if (!GameManager.Instance.isPlayerTurn)
        {
            rowIndex = 2 - rowIndex;
        }
        foreach (var cell in grid)
        {
            if (cell.index.y == rowIndex && cell.obj == null)
            {
                cell.cellObj.GetComponent<CellController>().selectable.SetSelectable(true);
                selectableCells.Add(cell.cellObj.transform);
            }
            else
            {
                cell.cellObj.GetComponent<CellController>().selectable.SetSelectable(false);
            }
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
        if (cancelRequested) yield break;
        selectedCells.Clear();
        selectedCells.Add(selectedcell);
        foreach (var cell in grid)
        {
            cell.cellObj.GetComponent<CellController>()
                .selectable.SetSelectable(false);
        }

        //Debug.Log("selected cell");
    }

    public IEnumerator _SelectCollumn()
    {
        var grid = GridManager.Instance.GetGrid();
        List<Transform> selectableCells = new List<Transform>();
        int rowIndex = 2;
        foreach (var cell in grid)
        {
            if (cell.index.y == rowIndex && cell.obj == null)
            {
                cell.cellObj.GetComponent<CellController>().selectable.SetSelectable(true);
                selectableCells.Add(cell.cellObj.transform);
            }
            else
            {
                cell.cellObj.GetComponent<CellController>().selectable.SetSelectable(false);
            }
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
        if (cancelRequested) yield break;

        selectedCells.Clear();
        selectedCells.Add(selectedcell);

        foreach (var cell in grid)
        {
            cell.cellObj.GetComponent<CellController>()
                .selectable.SetSelectable(false);

            if(cell.cellObj.transform.position.y == selectedcell.position.y)
            {
                selectedCells.Add(cell.cellObj.transform);
            }
        }

        //Debug.Log("selected collumn");
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
        Debug.Log("cost paid");
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
        thisMinion.StartAttack(selectedMinion.owner, selectedMinion);
        thisMinion.isAttackedThisTurn = false;
        yield return null;
    }
    public void DrawCard()
    {
        Debug.Log("try draw card");

        if (GameManager.Instance.isTesting) return;

        var agentToDraw = selectedAgent;
        curActionsList.Enqueue(_DrawCard(agentToDraw));
    }

    public IEnumerator _DrawCard(Agent agentToDraw)
    {
        //yield return null;

        yield return new WaitForSeconds(0.5f);

        Debug.Log("should draw card");
        if (agentToDraw == null)
        {
            Debug.LogWarning("Agent is null, cannot draw card");

        }
        else { 
            agentToDraw.DrawCard();
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
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions( _ChangeMinionAttack(selectedMinion, value));
        //Debug.LogWarning("change attack added to actions");
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
            //Debug.LogWarning("öinion attack changed to :" + minion.card.attack);
        }

        yield return null;
    }
    public IEnumerator _ChangeMinionAttack(MinionController minion, int value)
    {
        minion.card.attack += value;
        //Debug.LogWarning("öinion attack changed to :" + minion.card.attack);
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

        //Debug.LogWarning("change attack added to actions");
    }

    public IEnumerator _ChangeMinionHealth(int value)
    {
        foreach (var minion in selectedMinions)
        {
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
        if (GameManager.Instance.isTesting) return;

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

}


