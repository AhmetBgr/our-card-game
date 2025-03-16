using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public interface ISelectable
{
    public SelectionType SelectionType { get; }

    public abstract void OnSelect(); 
    public abstract void SetSelectable(SelectableParameters parameters);
    public abstract void SetSelectable(bool value);


}

public class SelectableParameters
{
    public SelectionType Type { get; }
    public int row;
    public int range;
    public int rangeOrigin;
}

