using System;
using System.Collections;
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

    void Start()
    {
        modal.UpdateModal(card);
        view.UpdateView(modal);

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
        transform.localPosition = new Vector3(transform.localPosition.x, -533, transform.localPosition.z);
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

    void DisablePeek()
    {
        if (isPeeking && handLayout != null)
        {
            transform.localScale = Vector3.one;
            handLayout.CancelPeek();
        }
        canPeek = false;
        isPeeking = false;
    }
}
