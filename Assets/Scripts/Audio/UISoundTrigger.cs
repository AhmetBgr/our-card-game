using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover and click sounds for one interactive UI element.
///
/// Usually not placed by hand -- <see cref="UISoundBinder"/> adds one to every Button, Toggle and Slider
/// as it appears. Add it in the inspector only to override which sounds an element makes.
///
/// The click sound plays on the PRESS, not on the click. Several controls act on pointer-down
/// (CardButtonHandler, HeroButtonHandler) and rebuild or destroy the list they sit in, so the release
/// never lands on them; and a Button whose onClick closes its own panel or disables itself runs before
/// this component, which read back as a blocked click. At press time nothing has reacted yet.
/// </summary>
[DisallowMultipleComponent]
public class UISoundTrigger : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    [Tooltip("Played when the pointer enters. None = silent on hover.")]
    public GameSound hoverSound = GameSound.UIHover;

    [Tooltip("Played on click. None = silent on click.")]
    public GameSound clickSound = GameSound.UIClick;

    [Tooltip("When this element is non-interactable, play the error sound on click instead of staying silent, so a dead button still tells the player something.")]
    public bool soundOnBlockedClick = true;

    private Selectable _selectable;
    private bool _selectableResolved;

    /// <summary>
    /// The Selectable this decorates, if any. Cached on first use rather than in Awake, because the
    /// binder adds this component to objects that are already awake.
    /// </summary>
    private Selectable Selectable
    {
        get
        {
            if (!_selectableResolved)
            {
                _selectableResolved = true;
                _selectable = GetComponent<Selectable>();
            }

            return _selectable;
        }
    }

    private bool IsInteractable => Selectable == null || (Selectable.interactable && Selectable.IsActive());

    public void OnPointerEnter(PointerEventData eventData)
    {
        // A hover sound on a control that cannot be pressed is noise; the pointer passes over plenty of
        // greyed-out rows in the deck panels.
        if (!IsInteractable) return;

        Play(hoverSound);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Buttons only fire on the left button; a right-click that does nothing should not sound like it did.
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

        if (IsInteractable) Play(clickSound);
        else if (soundOnBlockedClick) Play(GameSound.UIError);
    }

    private static void Play(GameSound id)
    {
        if (id == GameSound.None) return;

        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        manager.Play(id);
    }
}
