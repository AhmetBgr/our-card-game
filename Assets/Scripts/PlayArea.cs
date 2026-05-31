using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayArea : Singleton<PlayArea>, IDropHandler
{
    public Transform cardPos;
    public Transform opponentCardPos;

    public virtual void OnDrop(PointerEventData eventData)
    {
        CardController droppedItem = eventData.pointerDrag.GetComponent<CardController>();

        if (droppedItem == null)
        {
            Debug.Log("cant play");

            return;
        }

        // The drag was cancelled (right click) before release — don't play it.
        if (droppedItem.draggableItem != null && !droppedItem.draggableItem.isdragging)
            return;

        var layout = droppedItem.handLayout;
        int originalIndex = layout != null ? layout.cards.IndexOf(droppedItem.transform) : 0;

        bool canPlay = false;
        Debug.Log("testing: ");
        StartCoroutine(droppedItem.CanPlay(GameManager.Instance.player, result =>
        {
            canPlay = result;
            Debug.Log("canplay result: " + result);

            droppedItem.handLayout.CancelPeek();
            Debug.Log($"canplay {canPlay}, isplaying card {GameManager.Instance.isPlayingCard}");
            if (canPlay && !GameManager.Instance.isPlayingCard)
            {
                Debug.Log("shoıuld play");
                droppedItem.isPeeking = false;
                droppedItem.canPeek = false;

                // Take the card out of the layout so its UpdateCardPositions Lerp
                // doesn't fight DOMove and pull the card back to the fan.
                droppedItem.handLayout.RemoveCard(droppedItem.transform);

                droppedItem.draggableItem.ParentAfterDrag = transform;
                droppedItem.transform.DOMove(cardPos.position, 0.25f);
                droppedItem.transform.DOScale(Vector3.one, 0.25f);

                StartCoroutine(GameManager.Instance.PlayCard(droppedItem, GameManager.Instance.player));
            }
            else
            {
                int returnIndex = layout.RemoveCard(droppedItem.transform);
                layout.InsertCardAt(droppedItem.transform, returnIndex >= 0 ? returnIndex : Mathf.Max(originalIndex, 0));
                droppedItem.transform.DOComplete();
                droppedItem.transform.localRotation = Quaternion.identity;
                droppedItem.transform.localScale = Vector3.one;
                droppedItem.draggableItem.ParentAfterDrag = layout.transform;
                droppedItem.EnablePeek();
            }
        }));






    }
}
