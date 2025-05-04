using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckViewHandler : MonoBehaviour
{
    public List<Image> cards = new List<Image>();
    public Sprite cardBack;
    public Sprite upgradedCardBack;
    public TextMeshProUGUI cardCountText;
    public Image topcard;
    private int originalTopCardIndexAsChild;

    void Start()
    {
    }

    public void UpdateView(int cardCount, bool isTopCardUpgraded)
    {

        if(topcard != null)
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

        if (topcard == null) return;

        originalTopCardIndexAsChild = topcard.transform.GetSiblingIndex();
        topcard.transform.SetSiblingIndex(transform.childCount-2);
        topcard.sprite = isTopCardUpgraded ? upgradedCardBack : cardBack;   

        cardCountText.transform.parent.position = topcard.transform.position;   
    }
}
