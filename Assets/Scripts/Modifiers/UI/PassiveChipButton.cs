using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One passive in the passive picker: its icon, a frame while picked, and a lock while it is the
/// selected hero's own (always on, not a choice). Hovering shows name and description in the tooltip.
/// </summary>
public class PassiveChipButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private UITooltipTrigger tooltip;

    [Tooltip("Icon tint while the passive is not picked.")]
    [SerializeField] private Color unselectedTint = new Color(1f, 1f, 1f, 0.45f);

    // Same lazy Resources load CardView and HeroPassiveIndicator use, so the picker's tooltip is
    // highlighted from the one config the match uses and the two can never drift apart. Null-safe:
    // a missing asset renders plain text.
    private static CardTextHighlightConfig highlightConfig;
    private static CardTextHighlightConfig HighlightConfig =>
        highlightConfig != null ? highlightConfig : (highlightConfig = Resources.Load<CardTextHighlightConfig>("CardTextHighlightConfig"));

    public HeroPassiveSO Passive { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsSelected { get; private set; }

    public Action<PassiveChipButton> OnClicked;

    public void SetPassive(HeroPassiveSO passive)
    {
        Passive = passive;

        if (icon != null) icon.sprite = passive.icon;
        if (tooltip != null) tooltip.Message = TooltipFor(passive);

        SetState(locked: false, selected: false);
    }

    /// <summary>
    /// Points this chip's tooltip at a fixed spot in the scene rather than beside the chip. Set by
    /// the picker when it spawns the chip: the prefab cannot hold the reference itself, because the
    /// spot is a scene object and the chip is a prefab.
    /// </summary>
    public void SetTooltipPoint(Transform point)
    {
        if (tooltip != null) tooltip.Point = point;
    }

    public void SetState(bool locked, bool selected)
    {
        IsLocked = locked;
        IsSelected = selected;

        bool on = locked || selected;
        if (selectedFrame != null) selectedFrame.SetActive(on);
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
        if (icon != null) icon.color = on ? Color.white : unselectedTint;
    }

    /// <summary>
    /// The hover text for one passive, highlighted exactly like the passive tooltip on the hero's
    /// indicator in the match (see HeroPassiveIndicator.OnMouseEnter): the description goes through
    /// CardTextFormatter, so keywords, numbers and _italics_ read the same on both surfaces. The
    /// name stays as a heading because the chip itself carries no label.
    /// </summary>
    private static string TooltipFor(HeroPassiveSO passive)
    {
        string body = CardTextFormatter.Format(passive.description, HighlightConfig);
        return string.IsNullOrEmpty(body) ? passive.passiveName : $"<b>{passive.passiveName}</b>\n{body}";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        OnClicked?.Invoke(this);
    }
}
