using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinionController : MonoBehaviour, ISelectable
{
    public SelectableEntity selectable;
    public CardModal modal;

    public MinionView view;
    public CardTEst card;
    public GridEntity gridEntity;

    public SelectionType selectionType;

    public SelectableParameters parameters;

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

    // Update is called once per frame
    void Update()
    {
        
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
}
