using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A two-state button: clicking it flips <see cref="IsOn"/> and swaps the whole sprite set on its
/// <see cref="Button"/>, so the control reads as latched rather than momentary.
///
/// Both states carry a FULL set (normal + highlighted + pressed), not just one sprite. Unity's
/// SpriteSwap transition writes the highlighted/pressed sprites into the graphic's overrideSprite as
/// the pointer moves, so swapping only the normal sprite would be undone the moment the cursor
/// hovered. Flipping <see cref="Selectable.spriteState"/> alongside it keeps every state on-brand.
///
/// The click is taken through <see cref="IPointerClickHandler"/> rather than Button.onClick on
/// purpose: the visual state must survive owners that call onClick.RemoveAllListeners() while wiring
/// themselves up (<see cref="CardSelectionPanel"/> does exactly that). Button.onClick still fires as
/// usual for anyone listening to it.
/// </summary>
[RequireComponent(typeof(Button))]
public class ToggleButton : MonoBehaviour, IPointerClickHandler
{
    /// <summary>The sprites one state of the toggle draws itself with.</summary>
    [Serializable]
    public struct Visuals
    {
        [Tooltip("Resting sprite for this state. Written to the target Image.")]
        public Sprite normal;

        [Tooltip("Hover sprite. Leave empty to reuse Normal.")]
        public Sprite highlighted;

        [Tooltip("Held-down sprite. Leave empty to reuse Normal.")]
        public Sprite pressed;

        [Tooltip("Sprite while the button is non-interactable. Leave empty to reuse Normal.")]
        public Sprite disabled;
    }

    /// <summary>Concrete subclass so the event serialises and can be wired in the inspector.</summary>
    [Serializable]
    public class BoolEvent : UnityEvent<bool> { }

    [Tooltip("Image the sprites are written to. Defaults to the Button's own target graphic.")]
    [SerializeField] private Image targetImage;

    [Tooltip("How the button looks while OFF (the resting state).")]
    [SerializeField] private Visuals off;

    [Tooltip("How the button looks while ON (latched).")]
    [SerializeField] private Visuals on;

    [Tooltip("State the toggle starts in, and the state it shows in edit mode.")]
    [SerializeField] private bool isOn;

    [Tooltip("Raised whenever the state changes, with the new value. Not raised by SetIsOn(value, notify: false).")]
    public BoolEvent onValueChanged = new BoolEvent();

    private Button _button;

    /// <summary>The Button underneath, for callers that need interactable / onClick.</summary>
    public Button Button
    {
        get
        {
            if (_button == null) _button = GetComponent<Button>();
            return _button;
        }
    }

    /// <summary>Current state. Assigning it applies the visuals and raises <see cref="onValueChanged"/>.</summary>
    public bool IsOn
    {
        get => isOn;
        set => SetIsOn(value);
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        ApplyVisuals();
    }

    private void OnEnable()
    {
        // Re-applied on every enable: a pooled or re-shown button may have been left mid-transition.
        ApplyVisuals();
    }

    /// <summary>Flip the state, as a click would. Wire this to an external button if needed.</summary>
    public void Toggle()
    {
        SetIsOn(!isOn);
    }

    /// <summary>
    /// Move to <paramref name="value"/>. Pass <paramref name="notify"/> false to re-sync the visuals
    /// from an owner that already knows the new state — that is what stops a listener that writes
    /// back into the toggle from looping.
    /// </summary>
    public void SetIsOn(bool value, bool notify = true)
    {
        bool changed = isOn != value;
        isOn = value;
        ApplyVisuals();

        if (changed && notify)
            onValueChanged.Invoke(isOn);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
        if (!Button.IsInteractable()) return;

        Toggle();
    }

    /// <summary>Push the current state's sprites onto the Button and its target Image.</summary>
    private void ApplyVisuals()
    {
        Image image = ResolveTargetImage();
        Visuals visuals = isOn ? on : off;

        if (visuals.normal == null) return; // unconfigured state: leave the authored look alone

        if (image != null)
        {
            image.sprite = visuals.normal;

            // SpriteSwap parks the hover/press sprite in overrideSprite. Clearing it makes the new
            // normal sprite visible immediately instead of on the next pointer exit.
            image.overrideSprite = null;
        }

        Button button = Button;
        if (button == null) return;

        button.spriteState = new SpriteState
        {
            highlightedSprite = visuals.highlighted != null ? visuals.highlighted : visuals.normal,
            pressedSprite = visuals.pressed != null ? visuals.pressed : visuals.normal,
            selectedSprite = visuals.highlighted != null ? visuals.highlighted : visuals.normal,
            disabledSprite = visuals.disabled != null ? visuals.disabled : visuals.normal,
        };
    }

    private Image ResolveTargetImage()
    {
        if (targetImage != null) return targetImage;

        Button button = Button;
        targetImage = button != null ? button.targetGraphic as Image : null;
        if (targetImage == null) targetImage = GetComponent<Image>();

        return targetImage;
    }

#if UNITY_EDITOR
    // Lets a designer flick isOn in the inspector and see the swap without entering play mode.
    private void OnValidate()
    {
        _button = GetComponent<Button>();
        ApplyVisuals();
    }
#endif
}
