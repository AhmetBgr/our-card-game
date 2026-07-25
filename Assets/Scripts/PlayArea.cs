using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayArea : Singleton<PlayArea>, IDropHandler
{
    public Transform cardPos;
    public Transform opponentCardPos;

    [Tooltip("Canvas/CardParent - where a card lives while it is being played. Kept off CardPlayArea " +
             "deliberately: this container sits above the hand layouts but below CardSelectionPanel and " +
             "GameOverPanel, so a played card draws over the board without ever covering a modal.")]
    public Transform cardParent;

    /// <summary>
    /// Where a card being played should be parented. Falls back to this object so a scene that has not
    /// wired <see cref="cardParent"/> keeps the old behaviour instead of dropping the card to the root.
    /// </summary>
    public Transform PlayedCardParent => cardParent != null ? cardParent : transform;

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

                // Reparent HERE, not through ParentAfterDrag. Unity sends OnDrop and then OnEndDrag in
                // the same frame the button is released, but this callback only runs once the CanPlay
                // coroutine finishes — a frame or more later. OnEndDrag has therefore already consumed
                // ParentAfterDrag and put the card back under the hand layout, so assigning it now
                // cannot move anything. ParentAfterDrag is still set so the two agree in the event the
                // play test ever completes without yielding.
                droppedItem.draggableItem.ParentAfterDrag = PlayedCardParent;
                droppedItem.transform.SetParent(PlayedCardParent);
                droppedItem.transform.SetAsLastSibling();

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
