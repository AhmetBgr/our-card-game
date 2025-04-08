using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private GameObject[] minionTypeIconObjects;

    //[SerializeField] private Image _iconRenderer;
    [SerializeField] private GameObject cardBack;

    [SerializeField] private TextMeshProUGUI nametext;
    [SerializeField] private TextMeshProUGUI desctext;
    [SerializeField] private TextMeshProUGUI attacktext;
    [SerializeField] private TextMeshProUGUI healthtext;
    [SerializeField] private TextMeshProUGUI costText;

    public void UpdateView(CardModal card)
    {
        if (card == null) return;
        UpdateTexts(card);
        art.sprite = card.art;

        costText.transform.parent.gameObject.SetActive(card.isPlayerMinion);
        cardBack.SetActive(!card.isPlayerMinion);


        minionTypeIconObjects[0].transform.parent.gameObject.SetActive(card.isPlayerMinion);

        attacktext.transform.parent.gameObject.SetActive(card.health > 0 && card.attack > 0 && card.isPlayerMinion);
        healthtext.transform.parent.gameObject.SetActive(card.health > 0 && card.isPlayerMinion);


    }

    private void UpdateTexts(CardModal card)
    {
        nametext.text = card.name;
        desctext.text = card.desc;

        attacktext.text = card.attack.ToString();
        healthtext.text = card.health.ToString();
        costText.text = card.cost.ToString();
        int selectedIcon = 0;

        if(card.range == 1)
        {
            selectedIcon = 0;
        }
        else if(card.range == 2)
        {
            selectedIcon = 1;
        }
        else if (card.range == 3)
        {
            selectedIcon = 2;
        }
        for(int i = 0; i < minionTypeIconObjects.Length; i++) 
        {
            minionTypeIconObjects[i].SetActive(selectedIcon == i);
        }
    }

}
