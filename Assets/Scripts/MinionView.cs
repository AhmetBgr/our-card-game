using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinionView : MonoBehaviour
{
    [SerializeField] private Sprite[] meleeFrames;
    [SerializeField] private Sprite[] rangedFrames;

    [SerializeField] private SpriteRenderer frame;
    [SerializeField] private SpriteRenderer art;


    [SerializeField] private TextMeshProUGUI attacktext;
    [SerializeField] private TextMeshProUGUI healthtext;

    public void UpdateView(CardModal card)
    {
        if (card == null) return;

        UpdateAttackText(card.attack);
        UpdateHealthText(card.health);
        UpdateFrame(card);

        art.sprite = card.art;
    }

    private void UpdateAttackText(int value)
    {
        attacktext.text = value.ToString();
    }
    private void UpdateHealthText(int value)
    {
        healthtext.text = value.ToString();
    }

    private void UpdateFrame(CardModal card)
    {
        if(card.range == 1)
        {
            frame.sprite = meleeFrames[Mathf.Min((int)(card.defHealth / 5), meleeFrames.Length - 1)];
        }
        else
        {
            frame.sprite = rangedFrames[Mathf.Min((int)(card.defHealth / 5), rangedFrames.Length - 1)];

        }
    }

    
}
