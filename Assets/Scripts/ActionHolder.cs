using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

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
    public static Transform selectedcell = null;
    public static List<Transform> selectedCells = new List<Transform>();
    public static List<MinionController> selectedMinions = new List<MinionController>();

    public static MinionController selectedMinion = null;
    public static Agent selectedAgent = null;
    public static MinionController thisMinion = null;
    public static CardTEst thisCard = null;



    public static Queue<IEnumerator> curActionsList = new Queue<IEnumerator>();

    public static event Action<SelectableParameters> OnSelect;
    public static event Action<List<Transform>, CardTEst> OnWaitingCellSelect;
    public static event Action<List<MinionController>, CardTEst> OnWaitingMinionSelect;

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

        OnWaitingMinionSelect?.Invoke(selectableminions, thisCard);

        if (selectableminions.Count == 0 && GameManager.Instance.isTesting)
        {
            Debug.LogWarning("test failed");

            GameManager.Instance.isTestingFailed = true;
            yield break;
        }

        while (selectedMinion == null)
        {
            //Debug.Log("selecting minion");

            yield return null;
        }
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

        OnWaitingCellSelect?.Invoke(selectableCells, thisCard);

        while (selectedcell == null)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }

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
        curActionsList.Enqueue(_SelectThisMinion());
    }
    public IEnumerator _SelectThisMinion()
    {
        selectedMinion = thisMinion;

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

        curActionsList.Enqueue(_SelectRandomMinionInRange(player));
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
    public IEnumerator _SelectRandomFriendlyMinionInRange(Agent opponent)
    {
        List<MinionController> minionsInRange = new List<MinionController>();
        //Debug.Log("opponent: " + opponent.name);

        foreach (var minion in opponent.minions)
        {
            //Debug.Log("checking if minion is in range: ");
            if ((minion.transform.position - thisMinion.transform.position).magnitude < thisMinion.modal.range + 1)
            {
                //Debug.Log("minion is in range: ");

                minionsInRange.Add(minion);
            }
        }

        if(minionsInRange.Count > 0)
        {
            selectedMinion = minionsInRange[UnityEngine.Random.Range(0, minionsInRange.Count)];

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

        OnWaitingCellSelect?.Invoke(selectableCells, thisCard);

        while (selectedcell == null)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }
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

        OnWaitingCellSelect?.Invoke(selectableCells, thisCard);

        while (selectedcell == null)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }

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

    public void summonminion(CardTEst card)
    {
        //Debug.LogWarning("before summon ");

        if (GameManager.Instance.isTesting) return;

        IEnumerator cor = _summonminion(card);
        //GameManager.Instance.Addtoactions( cor);
        curActionsList.Enqueue(cor);

        //Debug.LogWarning("summon added to actions");
    }
    public IEnumerator _summonminion(CardTEst card)
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
        if (GameManager.Instance.isTesting) return;

        //Debug.Log("selected agent2: " + selectedAgent.name);
        curActionsList.Enqueue(_DrawCard());
    }

    public IEnumerator _DrawCard()
    {
        //Debug.Log("selected agent3: " + selectedAgent.name);

        yield return new WaitForSeconds(0.5f);

        selectedAgent.DrawCard(GameManager.Instance.isPlayerTurn);
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
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions(_ChangeMinionAttack(value));
        curActionsList.Enqueue(_ChangeMinionAttack(value));

        //Debug.LogWarning("change attack added to actions");

        /*selectedMinion.card.OnTurnEnd.AddListener(() =>
        {
            ChangeMinionAttack(selectedMinion, -value);
        });*/
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

    public void DestroyCard()
    {
        if (GameManager.Instance.isTesting) return;

        //GameManager.Instance.Addtoactions(_SelectMinion());
        curActionsList.Enqueue(_SelectMinion());

        //Debug.LogWarning("selecminion added to actions");

    }
    public IEnumerator _DestroyCard()
    {
        while (selectedMinion == null)
        {
            //Debug.Log("selecting minion");

            yield return null;
        }
       // Debug.Log("selected minion");


    }
}


