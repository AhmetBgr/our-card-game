using System;
using DG.Tweening;
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
    [Tooltip("Yellow outline shown while the chip holds a FORGED passive, like a forged minion's inline.")]
    [SerializeField] private GameObject forgedInline;

    [Tooltip("The plate an empty slot wears. On the hero panel the empty slots are authored with this " +
             "on and the default frame off; PlayUnlock slides it down to reveal the perk underneath.")]
    [SerializeField] private RectTransform lockedFrame;

    [Tooltip("The frame a filled slot wears. PlayUnlock switches it on, with the new perk's icon, just " +
             "before the locked frame slides away -- so the slide is what reveals them.")]
    [SerializeField] private GameObject defaultFrame;

    [Tooltip("Icon tint while the passive is not picked.")]
    [SerializeField] private Color unselectedTint = new Color(1f, 1f, 1f, 0.45f);

    // Same lazy Resources load CardView and HeroPassiveIndicator use, so the picker's tooltip is
    // highlighted from the one config the match uses and the two can never drift apart. Null-safe:
    // a missing asset renders plain text.
    private static CardTextHighlightConfig highlightConfig;
    private static CardTextHighlightConfig HighlightConfig =>
        highlightConfig != null ? highlightConfig : (highlightConfig = Resources.Load<CardTextHighlightConfig>("CardTextHighlightConfig"));

    /// <summary>
    /// The plate, falling back to a child named "LockedFrame" when the reference was never assigned.
    /// Chips exist as loose scene copies as well as prefab instances -- the rewards loadout's passive
    /// row is three plain GameObjects -- so wiring the prefab does not reach every chip, and an
    /// unwired one would make the unlock a silent no-op.
    /// </summary>
    private RectTransform Plate
    {
        get
        {
            if (lockedFrame == null) lockedFrame = transform.Find("LockedFrame") as RectTransform;
            return lockedFrame;
        }
    }

    /// <summary>The default frame, with the same name fallback as <see cref="Plate"/>.</summary>
    private GameObject DefaultFrame
    {
        get
        {
            if (defaultFrame == null)
            {
                var found = transform.Find("DefaultFrame");
                if (found != null) defaultFrame = found.gameObject;
            }
            return defaultFrame;
        }
    }

    public HeroPassiveSO Passive { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsSelected { get; private set; }

    public Action<PassiveChipButton> OnClicked;

    public void SetPassive(HeroPassiveSO passive)
    {
        Passive = passive;

        if (icon != null) icon.sprite = passive.icon;
        if (tooltip != null) tooltip.Message = TooltipFor(passive);
        ShowForgedInline(passive);

        SetState(locked: false, selected: false);
    }

    /// <summary>
    /// Shows a passive for display only (icon at full colour and its tooltip), with no picked or locked
    /// state. Null clears the icon and the tooltip.
    /// </summary>
    public void ShowPassive(HeroPassiveSO passive)
    {
        Passive = passive;

        if (icon != null)
        {
            icon.gameObject.SetActive(passive != null && passive.icon != null);
            if (passive != null) icon.sprite = passive.icon;
            icon.color = Color.white;
        }
        if (tooltip != null)
        {
            tooltip.Message = passive != null ? TooltipFor(passive) : null;
            tooltip.enabled = passive != null;
        }
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        ShowForgedInline(passive);
    }

    /// <summary>
    /// Shows a hero-panel slot: a slot holding a passive is UNLOCKED -- default frame on, its icon on,
    /// locked frame off -- and an empty slot is LOCKED -- locked frame on, default frame off. The hero
    /// panel's slots are authored in one state or the other, so this is what makes a slot match what it
    /// holds whenever the panel is drawn, rather than only after the unlock animation has run on it.
    /// </summary>
    public void ShowSlot(HeroPassiveSO passive)
    {
        bool unlocked = passive != null;

        var plate = Plate;
        if (plate != null)
        {
            plate.DOKill();
            plate.gameObject.SetActive(!unlocked);
        }
        if (DefaultFrame != null) DefaultFrame.SetActive(unlocked);

        ShowPassive(passive);
    }

    private void ShowForgedInline(HeroPassiveSO passive)
    {
        if (forgedInline != null) forgedInline.SetActive(passive != null && passive.isForged);
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

    /// <summary>
    /// A locked slot taking its new perk. Just before anything moves, the default frame is switched on
    /// and the perk's icon is set -- both underneath the locked frame, which is raised over them for
    /// the duration so neither shows early. Then the locked frame slides down, uncovering the perk,
    /// and scales down to nothing.
    ///
    /// Once it has slid away the locked frame is switched off and put back where it was authored, so
    /// the slot simply reads as filled from then on: <see cref="ShowPassive"/> never touches either
    /// frame, so a later refresh of the hero panel leaves it unlocked.
    ///
    /// Returned rather than left to run, so the caller can keep it; null (with a warning) when the chip
    /// has no locked frame to take off.
    /// </summary>
    public Sequence PlayUnlock(HeroPassiveSO passive, float slideDistance, float slideDuration, float shrinkDuration)
    {
        var plate = Plate;
        if (plate == null)
        {
            Debug.LogWarning($"PassiveChipButton '{name}': nothing to unlock -- no lockedFrame assigned " +
                             "and no child named LockedFrame.", this);
            ShowPassive(passive);
            if (DefaultFrame != null) DefaultFrame.SetActive(true);
            return null;
        }

        plate.DOKill();
        int order = plate.GetSiblingIndex();
        Vector2 home = plate.anchoredPosition;
        Vector3 size = plate.localScale;

        // Over the icon while it slides: authored, it draws under the icon, which would show the new
        // perk the moment it is set instead of the slide revealing it.
        plate.gameObject.SetActive(true);
        plate.SetAsLastSibling();

        // Just before the slide: what it is going to uncover.
        if (DefaultFrame != null) DefaultFrame.SetActive(true);
        ShowPassive(passive);

        var seq = DOTween.Sequence();
        seq.Append(plate.DOAnchorPosY(home.y - slideDistance, slideDuration).SetEase(Ease.OutQuad));
        seq.Append(plate.DOScale(Vector3.zero, shrinkDuration).SetEase(Ease.InQuad));

        seq.OnComplete(() =>
        {
            if (plate == null) return;
            plate.gameObject.SetActive(false);
            plate.SetSiblingIndex(order);
            plate.anchoredPosition = home;
            plate.localScale = size;
        });
        return seq;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        OnClicked?.Invoke(this);
    }
}
