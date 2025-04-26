using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectableEntity : MonoBehaviour
{
    [SerializeField] private bool iSselectable = false;

    public SpriteRenderer highlight;
    public Collider2D col;


    void Start()
    {
        GameManager.OnTurnEnd += ResetSelectable;
    }
    private void OnEnable()
    {
        GameManager.Instance.selectables.Add(this);

    }
    private void OnDisable()
    {
        GameManager.Instance.selectables.Remove(this);

    }
    private void OnDestroy()
    {
        GameManager.OnTurnEnd -= ResetSelectable;
    }

    private void ResetSelectable(GameState state)
    {
        SetSelectable(false);   
    }
    public void SetSelectable(SelectableParameters parameters)
    {

    }

    public void SetSelectable(bool value)
    {
        iSselectable = value;   
        if(highlight != null)
        {
            highlight.enabled = value;

        }
        col.enabled = value;
    }
}
