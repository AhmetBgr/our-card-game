using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds a UI Slider to one of the <see cref="GameSettings"/> volume buses.
///
/// Drop it on a Slider, pick the bus, done -- it seeds itself from the saved setting and writes back on
/// every change. Nothing else needs wiring: the original required a scene reference to the AudioMixer on
/// every slider, plus a hand-written PlayerPrefs key per slider, which is how the menu and pause screens
/// ended up disagreeing about what the volume was.
///
/// It also FOLLOWS the setting rather than only pushing to it, so a slider in the pause menu and one in
/// the title screen -- or a Reset to Defaults elsewhere on the same panel -- can never end up showing
/// something the game is not actually playing at.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public enum Bus
    {
        Master,
        Sfx,
        Music,

        /// <summary>Room tone. Nothing plays on it yet -- see <see cref="AudioBus.Ambient"/>.</summary>
        Ambient
    }

    [Tooltip("Which volume this slider controls.")]
    public Bus bus = Bus.Master;

    [Tooltip("Optional. Shows the level as a percentage beside the slider, so a bus with nothing playing still reads as set to something.")]
    public TMP_Text valueLabel;

    [Tooltip("Sound played as the slider moves, so the player hears what they are setting. Only for the SFX and Master buses -- the music is already audible.")]
    public GameSound previewSound = GameSound.UIHover;

    [Tooltip("Seconds between preview sounds while dragging, so a drag does not machine-gun the clip.")]
    [Min(0f)]
    public float previewInterval = 0.12f;

    private Slider _slider;
    private float _lastPreviewTime = float.NegativeInfinity;

    // Set while the slider is being seeded from the saved value, so the onValueChanged that seeding
    // fires is not mistaken for the player moving it (which would play a preview on every scene load).
    private bool _seeding;

    private void Awake()
    {
        _slider = GetComponent<Slider>();

        // The settings read 0..1; anything else silently rescales the player's setting.
        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        Seed();

        _slider.onValueChanged.AddListener(OnValueChanged);
        GameSettings.Changed += Seed;
    }

    private void OnDisable()
    {
        _slider.onValueChanged.RemoveListener(OnValueChanged);
        GameSettings.Changed -= Seed;
    }

    /// <summary>Pulls the current level onto the handle and the label, without reporting it back.</summary>
    private void Seed()
    {
        if (_slider == null) return;

        _seeding = true;
        _slider.SetValueWithoutNotify(CurrentVolume);
        _seeding = false;

        RefreshLabel();
    }

    private float CurrentVolume
    {
        get
        {
            switch (bus)
            {
                case Bus.Sfx: return GameSettings.SfxVolume;
                case Bus.Music: return GameSettings.MusicVolume;
                case Bus.Ambient: return GameSettings.AmbientVolume;
                default: return GameSettings.MasterVolume;
            }
        }
    }

    private void OnValueChanged(float value)
    {
        if (_seeding) return;

        switch (bus)
        {
            case Bus.Sfx:
                GameSettings.SfxVolume = value;
                break;
            case Bus.Music:
                GameSettings.MusicVolume = value;
                break;
            case Bus.Ambient:
                GameSettings.AmbientVolume = value;
                break;
            default:
                GameSettings.MasterVolume = value;
                break;
        }

        // Not folded into the Changed handler: dragging the handle to a level it is already at raises
        // nothing, and the label must still be right if it was somehow left stale.
        RefreshLabel();

        PlayPreview();
    }

    private void RefreshLabel()
    {
        if (valueLabel == null) return;

        valueLabel.text = Mathf.RoundToInt(CurrentVolume * 100f) + "%";
    }

    /// <summary>
    /// A tick as the slider moves, so a silent bus is not set blind. Rate-limited on unscaled time --
    /// a drag fires onValueChanged every frame, and the sound's own retrigger throttle is tuned for
    /// gameplay bursts, not for this.
    ///
    /// Skipped for the buses that are their own preview: music is already sounding, and ambience will
    /// be, so ticking a UI blip at them would only be a second sound to mistake for the one being set.
    /// </summary>
    private void PlayPreview()
    {
        if (previewSound == GameSound.None) return;
        if (bus == Bus.Music || bus == Bus.Ambient) return;
        if (Time.unscaledTime - _lastPreviewTime < previewInterval) return;

        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        _lastPreviewTime = Time.unscaledTime;
        manager.Play(previewSound);
    }
}
