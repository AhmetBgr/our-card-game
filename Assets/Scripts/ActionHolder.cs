using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
    public static MinionController selectedMinion = null;

    public static event Action<SelectableParameters> OnSelect;

    public void SelectCell(int rowIndex = 2)
    {
        IEnumerator cor = _SelectCell(rowIndex);
        GameManager.Instance.Addtoactions(cor);
        Debug.LogWarning("selectcell added to actions");

    }
    public IEnumerator _SelectCell(int rowIndex)
    {
        var grid = GridManager.Instance.GetGrid();

        foreach (var cell in grid)
        {

            cell.cellObj.GetComponent<CellController>()
                .selectable.SetSelectable(cell.index.y == rowIndex && cell.obj == null);

        }

        while (selectedcell == null)
        {
            //Debug.Log("selecting cell");

            yield return null;
        }

        foreach (var cell in grid)
        {

            cell.cellObj.GetComponent<CellController>()
                .selectable.SetSelectable(false);

        }

        Debug.Log("selected cell");
    }
    public void summonminion(CardTEst card)
    {
        IEnumerator cor = _summonminion(card);
        GameManager.Instance.Addtoactions( cor);
        Debug.LogWarning("summon added to actions");

    }
    public IEnumerator _summonminion(CardTEst card)
    {
        //Debug.Log("summoning minion");

        
        yield return null;

        GameManager.Instance.SummonMinion(card, selectedcell.position);


        Debug.Log("summonned minion");


    }

    public void ApplyEffect(CardTEst card)
    {
        GameManager.Instance.Addtoactions( _ApplyEffect());
    }
    public IEnumerator _ApplyEffect()
    {

        yield return null;

    }
    public IEnumerator _ChangeMinionAttack( int value)
    {
        selectedMinion.modal.attack += value;
        selectedMinion.view.UpdateView(selectedMinion.modal);
        Debug.LogWarning("öinion attack changed to :" + selectedMinion.card.attack);
        yield return null;

    }
    public IEnumerator _ChangeMinionAttack(MinionController minion, int value)
    {
        minion.card.attack += value;
        Debug.LogWarning("öinion attack changed to :" + minion.card.attack); 
        yield return null;

    }
    public void ChangeMinionAttack(int value)
    {

        GameManager.Instance.Addtoactions( _ChangeMinionAttack(selectedMinion, value));
        Debug.LogWarning("change attack added to actions");


    }
    public void ChangeMinionAttack(MinionController minion, int value)
    {
        GameManager.Instance.Addtoactions(_ChangeMinionAttack(minion, value));
        Debug.LogWarning("change attack added to actions");


    }
    public void ChangeMinionAttackThisTurn(int value)
    {
        GameManager.Instance.Addtoactions(_ChangeMinionAttack(value));
        Debug.LogWarning("change attack added to actions");

        /*selectedMinion.card.OnTurnEnd.AddListener(() =>
        {
            ChangeMinionAttack(selectedMinion, -value);
        });*/
    }
    public IEnumerator _ChangeMinionHealth( int value)
    {
        selectedMinion.modal.health += value;
        selectedMinion.modal.defHealth += value;
        selectedMinion.view.UpdateView(selectedMinion.modal);
        Debug.LogWarning("öinion attack changed to :" + selectedMinion.modal.defHealth);
        yield return null;

    }
    public void ChangeMinionHealth(int value)
    {

        GameManager.Instance.Addtoactions(_ChangeMinionHealth(value));
        Debug.LogWarning("change attack added to actions");


    }
    public void SelectMinion(int typeIndex=0)
    {
        GameManager.Instance.Addtoactions( _SelectMinion());
        Debug.LogWarning("selecminion added to actions");

    }
    public IEnumerator _SelectMinion()
    {
        var grid = GridManager.Instance.GetGrid();

        foreach (var cell in grid)
        {
            cell.obj?.GetComponent<MinionController>()
                .selectable.SetSelectable(true);
        }

        while (selectedMinion == null)
        {
            //Debug.Log("selecting minion");

            yield return null;
        }

        foreach (var cell in grid)
        {
            cell.obj?.GetComponent<MinionController>()
                .selectable.SetSelectable(false);
        }

        Debug.Log("selected minion");


    }
    public void DestroyCard()
    {
        GameManager.Instance.Addtoactions(_SelectMinion());
        Debug.LogWarning("selecminion added to actions");

    }
    public IEnumerator _DestroyCard()
    {
        while (selectedMinion == null)
        {
            Debug.Log("selecting minion");

            yield return null;
        }
        Debug.Log("selected minion");


    }
}


