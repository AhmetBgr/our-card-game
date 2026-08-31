using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover and click sounds for one interactive UI element.
///
/// Usually not placed by hand -- <see cref="UISoundBinder"/> adds one to every Button, Toggle and Slider
/// as a scene loads. Add it in the inspector only to override which sounds an element makes.
///
/// Clicks are caught through IPointerClickHandler rather than Button.onClick so the same component covers
/// Toggles, Sliders and plain images, and so a click on a disabled-but-present control still reports.
/// </summary>
[DisallowMultipleComponent]
public class UISoundTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
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

    public void OnPointerClick(PointerEventData eventData)
    {
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
