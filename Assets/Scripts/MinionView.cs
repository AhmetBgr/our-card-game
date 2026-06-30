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
    [SerializeField] private SpriteRenderer friendlyInline;
    [SerializeField] private float heroDropDelay = 0.2f;

    private float heroDropHeight = 10f;

    public void UpdateView(CardModal modal)
    {
        if (modal == null) return;

        UpdateAttackText(modal.attack);
        UpdateHealthText(modal.health);
        UpdateFrame(modal);

        art.sprite = modal.minionArt;
        enemyInline.gameObject.SetActive(!modal.isPlayerMinion);
        if (friendlyInline != null)
            friendlyInline.gameObject.SetActive(modal.isPlayerMinion);
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

    public void PlayAppearAnimation()
    {
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.5f).SetDelay(0.25f).SetEase(Ease.OutBack);
    }

    public void PlayHeroAppearAnimation()
    {
        Vector3 target = transform.localPosition;
        transform.localPosition = target + Vector3.up * heroDropHeight;
        transform.DOLocalMove(target, 1f).SetDelay(heroDropDelay).SetEase(Ease.OutExpo);
    }

    public void FadeOutArtImage(float dur)
    {
        art.DOFade(0f, dur);
        enemyInline.DOFade(0f, dur);

    }

}
