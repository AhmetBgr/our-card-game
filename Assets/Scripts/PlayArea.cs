using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayArea : Singleton<PlayArea>, IDropHandler
{
    public Transform cardPos;
    public Transform opponentCardPos;

    // True from the moment a drop starts resolving (CanPlay test) until it finishes.
    // Set synchronously so a second card dropped during the async test window is
    // rejected instead of being played on top of the first.
    private bool isResolvingDrop = false;

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

        // A card is already resolving or being played — reject this drop. dropHandled
        // is left false so OnEndDrag returns the card to its slot in hand.
        if (isResolvingDrop || GameManager.Instance.isPlayingCard)
            return;

        // Mark the drop as landing on a valid target so OnEndDrag doesn't cancel it.
        if (droppedItem.draggableItem != null)
            droppedItem.draggableItem.dropHandled = true;

        isResolvingDrop = true;

        var layout = droppedItem.handLayout;
        // The card is pulled out of the fan when the drag begins, so its original slot
        // is the index captured on the card rather than a live IndexOf lookup.
        int originalIndex = droppedItem.ReturnIndex;

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

                // PlayCard sets isPlayingCard synchronously, so that flag guards further
                // drops from here on; this drop is done resolving.
                isResolvingDrop = false;
                StartCoroutine(GameManager.Instance.PlayCard(droppedItem, GameManager.Instance.player));
            }
            else
            {
                isResolvingDrop = false;
                // RemoveCard first so an already-listed card can't be inserted twice.
                layout.RemoveCard(droppedItem.transform);
                int insertIndex = Mathf.Clamp(originalIndex >= 0 ? originalIndex : 0, 0, layout.cards.Count);
                layout.InsertCardAt(droppedItem.transform, insertIndex);
                droppedItem.transform.DOComplete();
                droppedItem.transform.localRotation = Quaternion.identity;
                droppedItem.transform.localScale = Vector3.one;
                droppedItem.draggableItem.ParentAfterDrag = layout.transform;
                droppedItem.EnablePeek();
            }
        }));






    }
}
