using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CardController : MonoBehaviour// , IPointerEnterHandler,IPointerExitHandler, IPointerDownHandler
{
    public CardModal modal;
    public CardView view;

    public CardTEst card;
    public Transform curslot;
    void Start()
    {
        modal.UpdateModal(card);
        view.UpdateView(modal);
        curslot = transform.parent;
    }
    public IEnumerator CanPlay(Agent owner, Action<bool> result)
    {
        if (modal.cost > owner.availibleMana)
        {
            result?.Invoke(false);
            yield break;
        }

        GameManager.Instance.TestCard(this);

        if (GameManager.Instance.isTestingFailed)
        {
            result?.Invoke(false);
            yield break;
        }

        result?.Invoke(true);

        yield break; 
    }

    /*public IEnumerator Play(Agent agent)
    {
        yield return StartCoroutine(GameManager.Instance.PlayCard(this, agent));

        //yield return null;
    }*/

    public void OnPointerDown()
    {
        if (!modal.isPlayerMinion) return;

        StartCoroutine(GameManager.Instance.PlayCard(this, GameManager.Instance.player));
    }

    public void OnPointerEnter()
    {
        if (!modal.isPlayerMinion) return;

        GameManager.Instance.player.handManager.AddToTopView(transform);
        transform.localPosition += Vector3.up *220;
        transform.localScale = Vector3.one * 1.4f;
    }

    public void OnPointerExit()
    {
        if (!modal.isPlayerMinion) return;

        GameManager.Instance.player.handManager.AddToHand(transform, curslot);
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
    }
}
