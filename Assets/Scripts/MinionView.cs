using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
public class MinionView : MonoBehaviour
{
    [SerializeField] private Sprite[] meleeFrames;
    [SerializeField] private Sprite[] rangedFrames;

    [SerializeField] private SpriteRenderer frame;
    [SerializeField] private SpriteRenderer art;

    [SerializeField] private TextMeshProUGUI attacktext;
    [SerializeField] private TextMeshProUGUI healthtext;

    [SerializeField] private SpriteRenderer enemyInline;

    public void UpdateView(CardModal modal)
    {
        if (modal == null) return;

        UpdateAttackText(modal.attack);
        UpdateHealthText(modal.health);
        UpdateFrame(modal);

        art.sprite = modal.art[1];
        enemyInline.gameObject.SetActive(!modal.isPlayerMinion);
    }

    private void UpdateAttackText(int value)
    {
        attacktext.text = value.ToString();
    }
    private void UpdateHealthText(int value)
    {
        healthtext.text = value.ToString();
    }

    private void UpdateFrame(CardModal modal)
    {
        if(frame == null) return;

        if(modal.range == 1)
        {
            int index = Mathf.Min((int)(modal.defHealth / 5f), meleeFrames.Length - 1);
            frame.sprite = meleeFrames[index];
        }
        else
        {
            int index = Mathf.Min((int)(modal.defHealth / 5f), rangedFrames.Length - 1);
            frame.sprite = rangedFrames[index];

        }
    }

    public void FadeOutArtImage(float dur)
    {
        art.DOFade(0f, dur);
        enemyInline.DOFade(0f, dur);

    }

}
