using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum GridEntityType
{
    Obj, FloorObj, SpaceLess, CellObj
}

public class GridEntity : MonoBehaviour
{
    public GridEntityType type;
    public Vector3 WorldPos;
    public bool dontAddAtTheStart = false;


    protected Vector2Int gridIndex;

    // Set once the entity is permanently removed from the grid (e.g. a dead minion that is still
    // playing its death animation before being destroyed). A detached entity is unsubscribed from
    // GridChanged and refuses to re-register, so its stale reference can never reclaim a cell that
    // another entity has since moved into. See MinionController.Die().
    private bool detached = false;



    // Start is called before the first frame update
    protected virtual void Start()
    {
        WorldPos = transform.position;
        if (dontAddAtTheStart) return;

        AddToGridCell();
    }

    protected virtual void OnEnable()
    {
        if (detached) return;
        GridManager.GridChanged += AddToGridCell;
    }

    protected virtual void OnDisable()
    {
        GridManager.GridChanged -= AddToGridCell;
    }

    public virtual void AddToGridCell()
    {
        if (detached) return;

        gridIndex = GridManager.Instance.PosToGridIndex(WorldPos);

        if (type == GridEntityType.Obj)
        {
            GridManager.Instance.AddObjectToCell(gridIndex.x, gridIndex.y, gameObject);

        }
        else if(type == GridEntityType.FloorObj)
        {
            GridManager.Instance.AddFloorObjectToCell(gridIndex.x, gridIndex.y, gameObject);
        }
        else if (type == GridEntityType.CellObj)
        {
            GridManager.Instance.AddCellObjectToCell(gridIndex.x, gridIndex.y, gameObject);
        }
        else
        {
            GridManager.Instance.AddSpacelessObjectToCell(gridIndex.x, gridIndex.y, gameObject);
        }
    }
    
    public void RemoveFromGridCell()
    {
        if (type == GridEntityType.Obj)
        {
            GridManager.Instance.RemoveObjectFromCell(gridIndex.x, gridIndex.y, this.gameObject);

        }
        else if (type == GridEntityType.FloorObj)
        {
            GridManager.Instance.RemoveFloorObjectFromCell(gridIndex.x, gridIndex.y, this.gameObject);
        }
        else if (type == GridEntityType.CellObj)
        {
            GridManager.Instance.RemoveCellObjectFromCell(gridIndex.x, gridIndex.y, gameObject);
        }
        else
        {
            GridManager.Instance.RemoveSpacelessObjectFromCell(gridIndex.x, gridIndex.y, this.gameObject);
        }

    }

    // Permanently remove this entity from the grid: clear the cell it currently occupies AND stop it
    // from ever re-registering on the next GridChanged. Used for a dying minion, whose GameObject
    // lingers for its death animation before being destroyed. Without the unsubscribe, that lingering
    // entity re-adds itself (with its old WorldPos) whenever InvokeGridChanged fires — e.g. when another
    // minion advances into the cell it just died on — either stealing that cell's obj slot or, once
    // destroyed, leaving a stale reference there that occupancy checks (cell.obj == null) read as empty.
    public void DetachFromGrid()
    {
        if (detached) return;
        detached = true;

        RemoveFromGridCell();
        GridManager.GridChanged -= AddToGridCell;
    }

    public Vector2Int GetGridIndex()
    {
        return GridManager.Instance.PosToGridIndex(WorldPos);
    }

}

