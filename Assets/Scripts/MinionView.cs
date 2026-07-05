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

    [SerializeField] private Transform damageIndicator;
    [SerializeField] private SpriteRenderer damageIndicatorBg;
    [SerializeField] private TextMeshProUGUI damageIndicatorText;
    [SerializeField] private CanvasGroup damageIndicatorGroup;
    [SerializeField] private float damageIndicatorHoldDuration = 0.4f;

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

    // Fast pop-in of the damage number, held briefly, then faded out (via CanvasGroup, with the
    // world-space background synced to the same alpha) and hidden.
    public void PlayDamageIndicator(int damage)
    {
        damageIndicator.DOKill();
        damageIndicatorGroup.DOKill();

        damageIndicatorText.text = "-" + damage;
        damageIndicatorGroup.alpha = 1f;
        damageIndicatorBg.color = new Color(damageIndicatorBg.color.r, damageIndicatorBg.color.g, damageIndicatorBg.color.b, 1f);

        damageIndicator.gameObject.SetActive(true);
        damageIndicator.localScale = Vector3.zero;
        damageIndicator.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                DOVirtual.DelayedCall(damageIndicatorHoldDuration, () =>
                {
                    damageIndicatorGroup.DOFade(0f, 0.18f)
                        .OnUpdate(() => damageIndicatorBg.color = new Color(
                            damageIndicatorBg.color.r, damageIndicatorBg.color.g, damageIndicatorBg.color.b, damageIndicatorGroup.alpha))
                        .OnComplete(() => damageIndicator.gameObject.SetActive(false));
                });
            });
    }

}
