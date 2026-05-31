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

    [HideInInspector] public Transform ParentAfterDrag;

    public static event Action DragStarted;
    public static event Action<Transform> DragEnded;
    public static event Action<Transform> DragCancelled;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!Interactable) return;

        isdragging = true;
        ParentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        transform.DORotate(Vector3.zero, 0.5f);
        Image.raycastTarget = false;
        DragStarted?.Invoke();
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
        Image.raycastTarget = true;
        DragCancelled?.Invoke(transform);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!Interactable || !isdragging) return;


        transform.SetParent(ParentAfterDrag);
        Image.raycastTarget = true;
        DragEnded?.Invoke(transform);
        isdragging = false;

    }
}
