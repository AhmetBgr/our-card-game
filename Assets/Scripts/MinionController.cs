using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using static UnityEditor.PlayerSettings;
public class MinionController : MonoBehaviour, ISelectable
{
    public SelectableEntity selectable;
    public CardModal modal;

    public MinionView view;
    public CardTEst card;
    public GridEntity gridEntity;

    public SelectionType selectionType;

    public SelectableParameters parameters;

    public Vector3Int plannedMoveDir = Vector3Int.zero;
    public bool isPlayerMinion;
    public bool isMovementValidated = false;
    public SelectionType SelectionType { get => SelectionType.Minion; }


    private void OnEnable()
    {
        ActionHolder.OnSelect += SetSelectable;
    }

    private void OnDisable()
    {
        
    }

    void Start()
    {
        modal.UpdateModal(card);

        view.UpdateView(modal);

    }

    private void OnMouseDown()
    {
        ActionHolder.selectedMinion = this;

    }

    public void OnSelect()
    {
        ActionHolder.selectedMinion = this;
    }

    public void SetSelectable(SelectableParameters parameters)
    {
        if(parameters.Type == selectionType && (parameters.row == gridEntity.GetGridIndex().x + 1 | parameters.row == -1))
        {

        }
    }
    public void SetSelectable(bool value)
    {

    }

    public void Move(Vector3Int pos)
    {
        transform.DOMove(pos, 0.25f);
        plannedMoveDir = Vector3Int.zero;
        isMovementValidated = false;
    }
    public void FailedMove(Vector3 dir)
    {
        Debug.Log("failed move");
        transform.DOPunchPosition(dir/20, 0.25f, vibrato: 0).SetEase(Ease.OutCubic);
        plannedMoveDir = Vector3Int.zero;
        isMovementValidated = false;
    }

    public bool CanMove(Vector3Int pos)
    {
        if (GridManager.Instance.IsOutSideOfGrid(GridManager.Instance.PosToGridIndex(pos)))
        {
            Debug.Log("-2: " + GridManager.Instance.PosToGridIndex(pos));

            plannedMoveDir = Vector3Int.zero;
            return false;
        }

        GameObject objectAtDest = GridManager.Instance.GetCell(GridManager.Instance.PosToGridIndex(pos)).obj;

        if (objectAtDest != null) return false;

        return true;
    }
    /*public void PlanMove(Vector3Int dir)
    {
        Vector3Int pos = Vector3Int.RoundToInt(transform.position) + dir;
        Debug.Log("-1 : " + pos + ", name: " + card.name);

        if (GridManager.Instance.IsOutSideOfGrid(GridManager.Instance.PosToGridIndex(pos)))
        {
            Debug.Log("-2: " + GridManager.Instance.PosToGridIndex(pos));

            plannedMoveDir = Vector3Int.zero;
            return;
        }

        MinionController otherMinion = null;
        GridManager.Instance.GetCell(GridManager.Instance.PosToGridIndex(pos)).obj?.TryGetComponent(out otherMinion);
        Debug.Log("-3");

        if (otherMinion != null && !otherMinion.isPlayerMinion)
        {
            Debug.Log("-4");

            plannedMoveDir = Vector3Int.zero;
            return;
        }
        Debug.Log("-5");

        plannedMoveDir = dir;
    }

    public void ValidateMove()
    {
        isMovementValidated = true;

        if (plannedMoveDir == Vector3Int.zero) return;

        Debug.Log("2");

        Vector3Int posToCheck = Vector3Int.RoundToInt(transform.position) + plannedMoveDir;

        GameObject objectAtDest = GridManager.Instance.GetCell(GridManager.Instance.PosToGridIndex(posToCheck)).obj;

        if (objectAtDest == null) return;

        MinionController otherMinion = objectAtDest.GetComponent<MinionController>();
        Debug.Log("3");


        if (otherMinion.plannedMoveDir == -plannedMoveDir) return;


        otherMinion.ValidateMove();
    }

    public bool CanMove()
    {
        Debug.Log("1" + ", name: " + card.name);
        if (isMovementValidated)
        {
            Debug.Log("--: " + (plannedMoveDir != Vector3Int.zero));

            return plannedMoveDir != Vector3Int.zero;
        }
        
        isMovementValidated = true;

        if (plannedMoveDir == Vector3Int.zero) return false;

        Debug.Log("2");

        Vector3Int posToCheck = Vector3Int.RoundToInt(transform.position) + plannedMoveDir;

        GameObject objectAtDest = GridManager.Instance.GetCell(GridManager.Instance.PosToGridIndex(posToCheck)).obj;

        if (objectAtDest == null) return true;

        MinionController otherMinion = objectAtDest.GetComponent<MinionController>();
        Debug.Log("3");
        

        if (otherMinion.plannedMoveDir == - plannedMoveDir) return false;

        Debug.Log("4");
        return otherMinion.CanMove();
    }*/
}
