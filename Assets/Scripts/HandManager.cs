using UnityEngine;

// Legacy slot-based hand container. The card layout/positioning has moved to CardHandLayout;
// this class is kept only for the info-card display used by MinionController on hover, plus
// a few slot helpers still referenced from the scene.
public class HandManager : MonoBehaviour
{
    public Transform canvas;
    public Transform hand;
    public Transform topview;

    public CardController infoCard;

    public void UpdateSlots()
    {
        foreach (Transform item in transform)
        {
            if (item.childCount == 0)
                item.gameObject.SetActive(false);
        }
    }

    public void ShowInfoCard(CardSO card)
    {
        infoCard.gameObject.SetActive(true);
        infoCard.card = card;
        infoCard.modal.UpdateModal(card, null);
        infoCard.modal.isPlayerMinion = true;
        infoCard.view.UpdateView(infoCard.modal);
    }

    public void HideInfoCard()
    {
        infoCard.gameObject.SetActive(false);
    }

    public void AddToTopView(Transform card)
    {
        card.transform.SetParent(canvas);
    }

    public void AddToHand(Transform card, Transform slot)
    {
        card.transform.SetParent(slot);
        card.localPosition = Vector3.zero;
        card.localScale = Vector3.one;
        slot.gameObject.SetActive(true);
    }

    public Transform GetEmptyHandSlot()
    {
        foreach (Transform item in hand)
        {
            if (item.childCount == 0 && !item.gameObject.activeSelf)
                return item;
        }
        return null;
    }

    public Transform gettopviewslot()
    {
        foreach (Transform item in topview)
        {
            if (item.childCount == 0)
                return item;
        }
        return null;
    }
}
