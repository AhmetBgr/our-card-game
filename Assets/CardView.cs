using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image art;

    //[SerializeField] private Image _iconRenderer;
    [SerializeField] private TextMeshProUGUI nametext;
    [SerializeField] private TextMeshProUGUI desctext;
    [SerializeField] private TextMeshProUGUI attacktext;
    [SerializeField] private TextMeshProUGUI healthtext;


    public void UpdateView(CardModal card)
    {
        if (card == null) return;
        UpdateTexts(card);
        art.sprite = card.art;
    }
    
    private void UpdateTexts(CardModal card)
    {
        nametext.text = card.name;
        desctext.text = card.desc;

        attacktext.text = card.attack.ToString();
        healthtext.text = card.health.ToString();

    }

}
