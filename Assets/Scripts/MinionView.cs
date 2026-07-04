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

    // Which team's inline this minion should show, and whether the spawn animation has finished. The
    // inline stays hidden until the pop-in completes, so it doesn't flash at full size mid scale-up.
    private bool _isPlayerMinion;
    private bool _hasAppeared;

    public void UpdateView(CardModal modal)
    {
        if (modal == null) return;

        UpdateAttackText(modal.attack);
        UpdateHealthText(modal.health);
        UpdateFrame(modal);

        art.sprite = modal.minionArt;
        _isPlayerMinion = modal.isPlayerMinion;
        ApplyInlineVisibility();
    }

    // The team inline is only visible once the spawn animation has finished; before then both stay
    // hidden. Later UpdateView calls (e.g. after taking damage) keep it shown, since _hasAppeared sticks.
    private void ApplyInlineVisibility()
    {
        enemyInline.gameObject.SetActive(_hasAppeared && !_isPlayerMinion);
        if (friendlyInline != null)
            friendlyInline.gameObject.SetActive(_hasAppeared && _isPlayerMinion);
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
        /*if(frame == null) return;

        if(modal.range == 1)
        {
            int index = Mathf.Min((int)(modal.defHealth / 5f), meleeFrames.Length - 1);
            frame.sprite = meleeFrames[index];
        }
        else
        {
            int index = Mathf.Min((int)(modal.defHealth / 5f), rangedFrames.Length - 1);
            frame.sprite = rangedFrames[index];

        }*/
    }

    public void PlayAppearAnimation()
    {
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.5f).SetDelay(0.25f).SetEase(Ease.OutBack)
            .OnComplete(RevealInline);
    }

    public void PlayHeroAppearAnimation()
    {
        Vector3 target = transform.localPosition;
        transform.localPosition = target + Vector3.up * heroDropHeight;
        transform.DOLocalMove(target, 1f).SetDelay(heroDropDelay).SetEase(Ease.OutExpo)
            .OnComplete(RevealInline);
    }

    // Called when a spawn animation finishes: the inline becomes eligible to show and is applied now.
    private void RevealInline()
    {
        _hasAppeared = true;
        ApplyInlineVisibility();
    }

    public void FadeOutArtImage(float dur)
    {
        art.DOFade(0f, dur);
        enemyInline.DOFade(0f, dur);

    }

}
