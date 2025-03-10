using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinionController : MonoBehaviour
{
    public CardModal modal;

    public MinionView view;
    public CardTEst card;

    // Start is called before the first frame update
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
}
