using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public class CardController : MonoBehaviour
{
    public CardModal modal;
    public CardView view;
    public CardSO card;
    public DraggableItem draggableItem;

    public CardHandLayout handLayout;

    public bool canPeek = true;
    public bool isPeeking = false;

    // Hand slot this card was in when the drag began, captured so a cancelled drag
    // (or an unplayable drop) returns it to its original position.
    private int _returnIndex = -1;
    public int ReturnIndex => _returnIndex;

    void Start()
    {


        DraggableItem.DragStarted += OnDragStarted;
        DraggableItem.DragEnded += OnDragEnded;
        DraggableItem.DragCancelled += OnDragCancelled;
    }

    private void OnDestroy()
    {
        DraggableItem.DragStarted -= OnDragStarted;
        DraggableItem.DragEnded -= OnDragEnded;
        DraggableItem.DragCancelled -= OnDragCancelled;
    }

    private void Update()
    {
        if (!modal.isPlayerMinion) return;
        view.UpdateGearSpeed(modal);
    }
    public void Initialize(Agent owner, bool isPlayerCard)
    {
        modal.UpdateModal(card, owner, isPlayerCard);
        view.UpdateView(modal);
    }
    public IEnumerator CanPlay(Agent owner, Action<bool> result)
    {
        if (modal.cost > owner.availibleMana)
        {
            result?.Invoke(false);
            yield break;
        }

        yield return StartCoroutine(GameManager.Instance.TestCard(this));

        if (GameManager.Instance.isTestingFailed)
        {
            result?.Invoke(false);
            yield break;
        }

        result?.Invoke(true);
    }

    // Kept for the UnityEvent binding on Card.prefab; play-on-click is handled via drag.
    public void OnPointerDown() { }

    public void OnPointerEnter()
    {
        if (!modal.isPlayerMinion || !canPeek) return;
        if (handLayout == null || !handLayout.BeginPeek(this)) return;

        canPeek = false;
        isPeeking = true;

        transform.SetParent(handLayout.transform.parent);
        transform.SetSiblingIndex(handLayout.transform.parent.childCount - 1);
        transform.localRotation = Quaternion.identity;
        float peekY = handLayout.peekPosition != null
            ? handLayout.transform.parent.InverseTransformPoint(handLayout.peekPosition.position).y
            : -533f;
        transform.localPosition = new Vector3(transform.localPosition.x, peekY, transform.localPosition.z);
        transform.localScale = Vector3.one * 1.5f;
    }

    public void OnPointerExit()
    {
        if (!modal.isPlayerMinion || !isPeeking) return;

        transform.localScale = Vector3.one;
        handLayout.EndPeek();

        canPeek = true;
        isPeeking = false;
    }

    public void EnablePeek()
    {
        canPeek = true;
        isPeeking = false;
    }

    private void OnDragEnded(Transform draggedItem)
    {
        if (draggedItem == transform) return;
        EnablePeek();
    }

    private void OnDragCancelled(Transform draggedItem)
    {
        // Other cards just re-enable peeking, mirroring OnDragEnded.
        if (draggedItem != transform)
        {
            EnablePeek();
            return;
        }

        // Put the dragged card back into the hand fan at its original slot.
        if (handLayout != null)
        {
            handLayout.CancelPeek();
            // Guard against re-adding a card that's somehow still in the list, so the
            // fan never ends up with a duplicate (which throws off every card's angle).
            handLayout.RemoveCard(transform);
            int index = _returnIndex >= 0
                ? Mathf.Clamp(_returnIndex, 0, handLayout.cards.Count)
                : 0;
            handLayout.InsertCardAt(transform, index);
            draggableItem.ParentAfterDrag = handLayout.transform;
        }
        _returnIndex = -1;

        transform.DOComplete();
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        EnablePeek();
    }

    private void OnDragStarted(Transform draggedItem)
    {
        if (draggedItem == transform)
        {
            // This card is the one being dragged — take it out of the hand fan and
            // remember its slot so a cancelled drag can restore it. Doing this here
            // (whether or not the card was peeking) guarantees it leaves the layout's
            // `cards` list exactly once, so reinserting it on cancel can't duplicate it.
            if (handLayout != null)
            {
                if (isPeeking)
                {
                    _returnIndex = handLayout.PeekIndex;
                    handLayout.CancelPeek();
                }
                else
                {
                    _returnIndex = handLayout.RemoveCard(transform);
                }
            }
            transform.localScale = Vector3.one;
            canPeek = false;
            isPeeking = false;
            return;
        }

        // Another card started dragging — if we were mid-peek, settle back into the
        // fan (EndPeek reinserts us) instead of being left orphaned.
        if (isPeeking && handLayout != null)
        {
            transform.localScale = Vector3.one;
            handLayout.EndPeek();
        }
        canPeek = false;
        isPeeking = false;
    }
}
