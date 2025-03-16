using System.Collections;
using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;
using UnityEngine.UIElements;

public class SelectableEntity : MonoBehaviour
{
    [SerializeField] private bool iSselectable = false;

    public SpriteRenderer highlight;
    public Collider2D col;


    void Start()
    {
        
    }

    public void SetSelectable(SelectableParameters parameters)
    {

    }

    public void SetSelectable(bool value)
    {
        iSselectable = value;   
        highlight.enabled = value;
        col.enabled = value;
    }
}
