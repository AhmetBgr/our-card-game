using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// One passive's icon on the hero's indicator row. Dumb view: it renders whatever HeroPassiveDisplay
/// it is handed and knows nothing about specific passives, so a new passive needs no code here.
///
/// Lives on Assets/Prefabs/UI/PassiveIndicator.prefab, instantiated by HeroPassiveIndicatorView.
/// </summary>
public class HeroPassiveIndicator : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject badgeRoot;
    [SerializeField] private TMP_Text badgeText;

    [Tooltip("Icon tint while the passive is doing something.")]
    [SerializeField] private Color activeTint = Color.white;

    [Tooltip("Icon tint while the passive is present but dormant.")]
    [SerializeField] private Color dormantTint = new Color(1f, 1f, 1f, 0.35f);

    [Header("Proc Flash")]
    [SerializeField] private float procScale = 1.35f;
    [SerializeField] private float procPunchDuration = 0.12f;
    [SerializeField] private float procSettleDuration = 0.18f;

    /// <summary>The passive this slot renders. Set by Bind; used to route proc flashes.</summary>
    public HeroPassiveSO Passive { get; private set; }

    private Tween _procTween;

    public void Bind(HeroPassiveSO passive, in HeroPassiveDisplay display)
    {
        Passive = passive;
        Apply(display);
    }

    public void Apply(in HeroPassiveDisplay display)
    {
        gameObject.SetActive(display.visible);
        if (!display.visible) return;

        if (iconImage != null)
        {
            iconImage.sprite = display.icon;
            iconImage.color = display.isActive ? activeTint : dormantTint;
        }

        bool hasBadge = !string.IsNullOrEmpty(display.badge);
        if (badgeRoot != null) badgeRoot.SetActive(hasBadge);
        if (hasBadge && badgeText != null) badgeText.text = display.badge;
    }

    /// <summary>
    /// Pop the icon when its passive fires. Pure view feedback — safe to call from inside GameManager's
    /// dispatch loop because it touches nothing but this transform.
    /// </summary>
    public void PlayProcFlash()
    {
        if (!isActiveAndEnabled) return;

        // Same hygiene as MinionView/MinionController: kill the previous tween before restarting, so
        // rapid re-procs can't leave the icon stuck mid-scale.
        _procTween?.Kill();
        transform.localScale = Vector3.one;

        _procTween = DOTween.Sequence()
            .Append(transform.DOScale(Vector3.one * procScale, procPunchDuration).SetEase(Ease.OutBack))
            .Append(transform.DOScale(Vector3.one, procSettleDuration).SetEase(Ease.InOutSine));
    }

    private void OnDisable()
    {
        _procTween?.Kill();
        _procTween = null;
        transform.localScale = Vector3.one;
    }
}
