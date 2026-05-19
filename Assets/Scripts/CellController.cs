using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellController : MonoBehaviour
{
    public SelectableEntity selectable;

    private void OnMouseDown()
    {
        ActionHolder.selectedcell = transform;
        //selectable.SetSelectable(false);
    }
}
