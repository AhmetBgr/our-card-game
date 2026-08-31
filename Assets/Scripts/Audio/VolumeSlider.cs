using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds a UI Slider to one of the <see cref="AudioManager"/> volume buses.
///
/// Drop it on a Slider, pick the bus, done -- it seeds itself from the saved setting and writes back on
/// every change. Nothing else needs wiring: the original required a scene reference to the AudioMixer on
/// every slider, plus a hand-written PlayerPrefs key per slider, which is how the menu and pause screens
/// ended up disagreeing about what the volume was.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public enum Bus
    {
        Master,
        Sfx,
        Music
    }

    [Tooltip("Which volume this slider controls.")]
    public Bus bus = Bus.Master;

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

        // The manager reads 0..1; anything else silently rescales the player's setting.
        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        _seeding = true;
        _slider.SetValueWithoutNotify(CurrentVolume);
        _seeding = false;

        _slider.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable() => _slider.onValueChanged.RemoveListener(OnValueChanged);

    private float CurrentVolume
    {
        get
        {
            AudioManager manager = AudioManager.Instance;
            if (manager == null) return 1f;

            switch (bus)
            {
                case Bus.Sfx: return manager.SfxVolume;
                case Bus.Music: return manager.MusicVolume;
                default: return manager.MasterVolume;
            }
        }
    }

    private void OnValueChanged(float value)
    {
        if (_seeding) return;

        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        switch (bus)
        {
            case Bus.Sfx:
                manager.SfxVolume = value;
                break;
            case Bus.Music:
                manager.MusicVolume = value;
                break;
            default:
                manager.MasterVolume = value;
                break;
        }

        PlayPreview(manager);
    }

    /// <summary>
    /// A tick as the slider moves, so a silent bus is not set blind. Rate-limited on unscaled time --
    /// a drag fires onValueChanged every frame, and the sound's own retrigger throttle is tuned for
    /// gameplay bursts, not for this.
    /// </summary>
    private void PlayPreview(AudioManager manager)
    {
        if (previewSound == GameSound.None || bus == Bus.Music) return;
        if (Time.unscaledTime - _lastPreviewTime < previewInterval) return;

        _lastPreviewTime = Time.unscaledTime;
        manager.Play(previewSound);
    }
}
