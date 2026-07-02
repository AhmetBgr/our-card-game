using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;

public class DraggableItem : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public Image Image;
    public bool Interactable = true;
    public bool isdragging = false;

    // Set by PlayArea.OnDrop when the card is released over the play area. If it's
    // still false when OnEndDrag runs, the card was dropped somewhere invalid and
    // the play is cancelled (card returns to its slot in hand).
    [HideInInspector] public bool dropHandled = false;

    [HideInInspector] public Transform ParentAfterDrag;

    public static bool AnyCardDragging { get; private set; }

    public static event Action<Transform> DragStarted;
    public static event Action<Transform> DragEnded;
    public static event Action<Transform> DragCancelled;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!Interactable) return;

        // While a card is being played (resolving in the play area), no card can be
        // dragged — the played card can only be cancelled, not moved.
        if (GameManager.Instance != null && GameManager.Instance.isPlayingCard) return;

        isdragging = true;
        AnyCardDragging = true;
        dropHandled = false;
        ParentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        transform.DORotate(Vector3.zero, 0.5f);
        Image.raycastTarget = false;
        DragStarted?.Invoke(transform);
    }

    private void Update()
    {
        // Right click while dragging cancels the play and returns the card to hand.
        if (isdragging && Input.GetMouseButtonDown(1))
            CancelDrag();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!Interactable || !isdragging) return;

        transform.position = eventData.position;
    }

    private void CancelDrag()
    {
        // Clearing isdragging stops OnDrag from following the cursor and makes
        // OnEndDrag/PlayArea.OnDrop bail out when the left button is finally released.
        isdragging = false;
        AnyCardDragging = false;
        Image.raycastTarget = true;
        DragCancelled?.Invoke(transform);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!Interactable || !isdragging) return;

        isdragging = false;
        AnyCardDragging = false;
        Image.raycastTarget = true;

        // Released outside the play area — cancel the play and send the card back
        // to its original position in hand, mirroring the right-click cancel.
        if (!dropHandled)
        {
            DragCancelled?.Invoke(transform);
            return;
        }

        transform.SetParent(ParentAfterDrag);
        DragEnded?.Invoke(transform);
    }
}
