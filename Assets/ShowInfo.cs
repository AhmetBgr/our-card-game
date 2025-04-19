using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowInfo : MonoBehaviour
{
    public CardTEst card;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    protected virtual void OnMouseEnter()
    {
        GameManager.Instance.player.handManager.ShowInfoCard(card);
    }

    protected virtual void OnMouseExit()
    {
        GameManager.Instance.player.handManager.HideInfoCard();
    }
}
