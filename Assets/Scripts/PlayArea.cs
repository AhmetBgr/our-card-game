using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayArea : MonoBehaviour, IDropHandler
{
    public Transform cardPos;
    public virtual void OnDrop(PointerEventData eventData)
    {
        CardController droppedItem = eventData.pointerDrag.GetComponent<CardController>();

        if (droppedItem == null)
        {
            Debug.Log("cant play");

            return;
        }

        bool canPlay = false;
        Debug.Log("testing: ");
        StartCoroutine(droppedItem.CanPlay(GameManager.Instance.player, result =>
        {
            canPlay = result;
            Debug.Log("canplay result: " + result);
        }));
        GameManager.Instance.player.cardHandLayout.RemoveCard(GameManager.Instance.player.cardHandLayout.cardplaceholder, false);
        GameManager.Instance.player.cardHandLayout.cardplaceholder.SetParent(transform.parent.parent);
        if (canPlay && !GameManager.Instance.isPlayingCard)
        {
            Debug.Log("shoıuld play");
            droppedItem.isPeeking = false;
            droppedItem.canPeek = false;


            droppedItem.draggableItem.ParentAfterDrag = transform;
            droppedItem.transform.DOMove(cardPos.position, 0.25f);
            droppedItem.transform.DOScale(Vector3.one, 0.25f);

            StartCoroutine(GameManager.Instance.PlayCard(droppedItem, GameManager.Instance.player));


        }
        else
        {
            droppedItem.EnablePeek();
        }
        
        // play card





    }
}
