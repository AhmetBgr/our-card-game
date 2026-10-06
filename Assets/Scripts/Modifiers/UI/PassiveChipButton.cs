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

    public HeroPassiveSO Passive { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsSelected { get; private set; }

    public Action<PassiveChipButton> OnClicked;

    public void SetPassive(HeroPassiveSO passive)
    {
        Passive = passive;

        if (icon != null) icon.sprite = passive.icon;
        if (tooltip != null) tooltip.Message = string.IsNullOrEmpty(passive.description)
            ? passive.passiveName
            : $"<b>{passive.passiveName}</b>\n{passive.description}";

        SetState(locked: false, selected: false);
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        OnClicked?.Invoke(this);
    }
}
