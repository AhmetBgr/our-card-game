using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    public Transform canvas;

    public Transform hand;
    public Transform topview;

    public CardController infoCard;



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void UpdateSlots()
    {
        foreach (Transform item in transform)
        {
            if(item.childCount == 0)
                item.gameObject.SetActive(false);
        }
    }
    public void ShowInfoCard(CardTEst card)
    {
        infoCard.gameObject.SetActive(true);
        infoCard.card = card;
        infoCard.modal.UpdateModal(card);
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
    public void RemoveFromHand(CardController card)
    {
        Transform slot = card.curslot; 
        Destroy(card.gameObject);
        if(slot != null )
        {
            slot.gameObject.SetActive(false);
        }
    }
    public IEnumerator RemoveFromHand2(Transform card)
    {
        //Transform slot = card.parent;
        Destroy(card.gameObject);
        //slot.gameObject.SetActive(false);

        yield return null;

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
