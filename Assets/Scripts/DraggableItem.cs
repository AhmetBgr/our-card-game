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

    public void OnDrag(PointerEventData eventData)
    {
        if (!Interactable) return;

        transform.position = eventData.position;
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
