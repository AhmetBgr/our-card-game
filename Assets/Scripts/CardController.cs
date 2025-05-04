using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CardController : MonoBehaviour// , IPointerEnterHandler,IPointerExitHandler, IPointerDownHandler
{
    public CardModal modal;
    public CardView view;

    public CardSO card;
    public Transform curslot;
    public DraggableItem draggableItem;
    public bool canPeek = true;
    public bool isPeeking = false;

    int indexinhand;
    Transform parent;
    void Start()
    {
        modal.UpdateModal(card);
        view.UpdateView(modal);
        curslot = transform.parent;
        parent = transform.parent;

        //draggableItem.Interactable = modal.isPlayerMinion;

        DraggableItem.DragStarted += DisablePeek;
        DraggableItem.DragEnded += OnDragEnded;

    }
    private void OnDestroy()
    {
        DraggableItem.DragStarted -= DisablePeek;
        DraggableItem.DragEnded -= OnDragEnded;

    }
    private void Update()
    {
        if (!modal.isPlayerMinion) return;
         
        view.UpdateGearSpeed(modal);

        // cancel dragging
        /*if (Input.GetMouseButton(1) && draggableItem.isdragging)
        {
            isPeeking = true;
            draggableItem.OnEndDrag(null);
            OnPointerExit();
            //EnablePeek();   
        }*/
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

        //StartCoroutine(GameManager.Instance.PlayCard(this, GameManager.Instance.player));

    }

    public void OnPointerEnter()
    {
        if (!modal.isPlayerMinion || !canPeek) return;

        canPeek = false;
        isPeeking = true;
        CardHandLayout cardHandLayout = GameManager.Instance.player.cardHandLayout;
        indexinhand = cardHandLayout.RemoveCard(transform);
        cardHandLayout.AddCard(GameManager.Instance.player.cardHandLayout.cardplaceholder, indexinhand);

        transform.SetParent(cardHandLayout.transform.parent);
        transform.SetSiblingIndex(cardHandLayout.transform.parent.childCount -1);
        transform.localRotation = Quaternion.identity;
        transform.localPosition = new Vector3(transform.localPosition.x, -533, transform.localPosition.z);
        transform.localScale = Vector3.one * 1.5f;
    }

    public void OnPointerExit()
    {
        if (!modal.isPlayerMinion || !isPeeking || draggableItem.isdragging) return;

        canPeek = true;
        isPeeking = false;

        //GameManager.Instance.player.handManager.AddToHand(transform, curslot);
        //transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
        Debug.Log("index in hand: " + indexinhand);
        GameManager.Instance.player.cardHandLayout.RemoveCard(GameManager.Instance.player.cardHandLayout.cardplaceholder);
        GameManager.Instance.player.cardHandLayout.cardplaceholder.SetParent(transform.parent.parent);
        GameManager.Instance.player.cardHandLayout.AddCard(transform, indexinhand);

    }

    public void EnablePeek()
    {
        canPeek = true;
        isPeeking = false;

        //DOVirtual.DelayedCall(1.2f, () => canPeek = true);
    }
    private void OnDragEnded(Transform draggedItem)
    {
        if (draggedItem == transform) return;

        EnablePeek();
    }
    void DisablePeek()
    {
        canPeek = false;
        isPeeking = false;
    }
}
