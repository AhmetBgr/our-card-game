using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DeckViewHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Transform Parent;
    public GameObject Shadow;
    public GameObject Shadow2;

    public List<Image> cards = new List<Image>();
    public Sprite cardBack;
    public Sprite upgradedCardBack;
    public TextMeshProUGUI cardCountText;
    public Image topcard;
    private int originalTopCardIndexAsChild;
    private int cardCount;
    void Start()
    {
    }

    public void UpdateView(int cardCount, bool isTopCardUpgraded)
    {
        this.cardCount = cardCount;

        if (topcard != null)
        {
            topcard.transform.SetSiblingIndex(originalTopCardIndexAsChild);
            topcard.sprite = cardBack;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].gameObject.SetActive(i<cardCount);

            if(i == cardCount - 1)
            {
                topcard = cards[i];
            }
        }
        cardCountText.text = cardCount.ToString();
        //Parent.gameObject.SetActive(cardCount > 0);
        Shadow.SetActive(cardCount > 0);
        Shadow2.SetActive(cardCount > 0);

        if (topcard == null)
        {
            return;
        }


        originalTopCardIndexAsChild = topcard.transform.GetSiblingIndex();
        topcard.transform.SetSiblingIndex(transform.childCount-2);
        topcard.sprite = isTopCardUpgraded ? upgradedCardBack : cardBack;   

        cardCountText.transform.parent.position = topcard.transform.position;   
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardCount == 0) return;
        cardCountText.transform.parent.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        cardCountText.transform.parent.gameObject.SetActive(false);
    }
}
