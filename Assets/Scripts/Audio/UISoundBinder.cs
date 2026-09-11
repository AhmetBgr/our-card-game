using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives every interactive UI element its hover and click sound, without anyone having to remember to add
/// a component.
///
/// The alternative -- a UISoundTrigger dropped on each control by hand -- is the version that rots: it is
/// invisible when missing, so a button added six months from now is simply silent and nobody notices
/// until a player does. Binding automatically means new UI is audible by default and staying silent is
/// the thing that takes a deliberate act (an exclusion in the library, or a UISoundTrigger set to None).
///
/// Binds by sweeping the live Selectables every frame rather than walking each scene once as it loads.
/// A load-time pass only ever saw the UI a scene was saved with, so everything built at runtime -- the
/// deck and hero lists, card choice options, dropdown items, anything under DontDestroyOnLoad -- stayed
/// silent. Selectables register themselves on enable, so the sweep sees a control the frame it appears,
/// before the pointer can reach it; once bound, a control costs one TryGetComponent a frame.
/// </summary>
public static class UISoundBinder
{
    private static Selectable[] _buffer = new Selectable[64];

    // Domain reload being off ("Enter Play Mode Options") keeps the previous session's subscription alive,
    // so it is dropped here before Hook adds it back.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Canvas.willRenderCanvases -= Sweep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        // Rides the canvas update rather than a MonoBehaviour's Update: it fires every frame there is UI to
        // draw, needs no GameObject of its own, and runs after layout has enabled whatever it is going to.
        Canvas.willRenderCanvases -= Sweep;
        Canvas.willRenderCanvases += Sweep;
    }

    private static void Sweep()
    {
        // The subscription outlives play mode when domain reload is off, and must not bootstrap an
        // AudioManager into the edit-mode scene.
        if (!Application.isPlaying) return;

        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        AudioLibrary library = manager.Library;
        if (library == null || !library.autoBindUISounds) return;

        if (_buffer.Length < Selectable.allSelectableCount)
            _buffer = new Selectable[Mathf.NextPowerOfTwo(Selectable.allSelectableCount)];

        int count = Selectable.AllSelectablesNoAlloc(_buffer);

        for (int i = 0; i < count; i++)
        {
            Selectable selectable = _buffer[i];
            _buffer[i] = null;

            if (selectable == null) continue;

            // An explicit trigger is an authored override; never second-guess it.
            if (selectable.TryGetComponent(out UISoundTrigger _)) continue;

            if (library.IsExcludedFromAutoBind(selectable.gameObject.name)) continue;

            var trigger = selectable.gameObject.AddComponent<UISoundTrigger>();

            // Cards already sound off through GameAudioBinder (CardHover, CardPickUp, the card choice
            // cues), and an interface click on top of those doubles them. Bound silent rather than
            // skipped, so the sweep settles them once instead of re-inspecting them every frame.
            if (IsCard(selectable))
            {
                trigger.hoverSound = GameSound.None;
                trigger.clickSound = GameSound.None;
                trigger.soundOnBlockedClick = false;
                continue;
            }

            trigger.hoverSound = GameSound.UIHover;
            trigger.clickSound = ClickSoundFor(selectable, library);
        }
    }

    /// <summary>A control that is a card, or part of one: the card preview variants carry a Button.</summary>
    private static bool IsCard(Selectable selectable) =>
        selectable.GetComponentInParent<CardView>(true) != null ||
        selectable.GetComponentInParent<CardModal>(true) != null ||
        selectable.GetComponentInParent<CardController>(true) != null;

    /// <summary>
    /// The click sound that suits a control's shape. A slider clicks on every drag frame, so it gets
    /// nothing. ToggleButton is deliberately a plain click: it is a latched Button (How to Play, Settings),
    /// and reads to the player as pressing a button, not flipping a switch.
    /// </summary>
    private static GameSound ClickSoundFor(Selectable selectable, AudioLibrary library)
    {
        if (selectable is Slider || selectable is Scrollbar) return GameSound.None;

        // A real Toggle gets its own sound once one exists. Until UI_Toggle has a clip it would play
        // silence, so it falls back to the click rather than leaving every toggle mute.
        if (selectable is Toggle)
        {
            SoundEffect toggleSound = library.Get(GameSound.UIToggle);
            if (toggleSound != null && toggleSound.HasClips) return GameSound.UIToggle;
        }

        return GameSound.UIClick;
    }
}
