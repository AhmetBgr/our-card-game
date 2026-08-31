using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The single voice pool and mixer for the whole game.
///
/// Zero-setup, in the same spirit as <see cref="FloatingTextManager"/>: nothing has to exist in a scene.
/// The first call builds the manager, which reads its sound map from Resources/AudioLibrary.asset and
/// survives scene loads, so the menu and the match share one pool and one set of volume settings.
///
/// Voices are child GameObjects rather than a stack of AudioSources on one object. That is what makes
/// positional playback possible at all -- an AudioSource is heard at its transform, so a pile of them on
/// a single manager object can only ever be flat 2D.
///
/// Bus volumes are applied per voice rather than through an AudioMixer. A mixer would mean shipping an
/// .mixer asset whose exposed parameters have to stay in sync with this code, it silently does nothing
/// on a build where the asset failed to load, and SetFloat is ignored while the mixer is in a snapshot
/// transition. Multiplying into <see cref="AudioSource.volume"/> is exact, works identically on WebGL,
/// and lets a single sound duck without touching a global. Assign <see cref="mixerGroup"/> if you later
/// want mixer effects on top; routing and volume are independent.
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    /// <summary>Voices created up front. Enough for the busiest board turn without allocating mid-match.</summary>
    private const int InitialVoices = 12;

    /// <summary>
    /// Hard ceiling on voices. Past this the oldest, least important voice is stolen rather than the pool
    /// growing without bound -- the original had no cap at all, so a stuck loop could add AudioSources
    /// forever.
    /// </summary>
    private const int MaxVoices = 32;

    private const string MasterVolumeKey = "audio.master";
    private const string SfxVolumeKey = "audio.sfx";
    private const string MusicVolumeKey = "audio.music";
    private const string MutedKey = "audio.muted";

    [Tooltip("Sound map to use. Empty = loaded from Resources/AudioLibrary.asset.")]
    [SerializeField] private AudioLibrary library;

    [Tooltip("Optional. Routes every voice through a mixer group so you can hang effects off it. Volume is handled in code either way -- see the class comment.")]
    [SerializeField] private AudioMixerGroup mixerGroup;

    // ---------------------------------------------------------------------------------------------
    // One pooled voice.
    // ---------------------------------------------------------------------------------------------
    private class Voice
    {
        public AudioSource source;

        /// <summary>The sound this voice is playing, so its live-count can be released when the voice frees.</summary>
        public SoundEffect sound;

        /// <summary>Unscaled time the voice started, used to pick which one to steal when the pool is full.</summary>
        public float startedAt;

        /// <summary>Set while a delayed play is pending, so the voice is not handed out twice before it starts.</summary>
        public bool reserved;

        public bool IsBusy => reserved || source.isPlaying;
    }

    private readonly List<Voice> _voices = new List<Voice>();

    /// <summary>The dedicated music voice. Kept out of the pool so a busy board can never steal the music.</summary>
    private AudioSource _musicSource;
    private SoundEffect _musicSound;
    private Coroutine _musicFade;

    private float _masterVolume = 1f;
    private float _sfxVolume = 1f;
    private float _musicVolume = 0.6f;
    private bool _muted;
    private bool _settingsLoaded;

    private static AudioManager _instance;
    private static bool _quitting;

    // Statics survive entering play mode when domain reload is disabled ("Enter Play Mode Options"),
    // which would otherwise leave a dead instance -- or a stuck _quitting -- poisoning the next session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _quitting = false;
    }

    public static AudioManager Instance
    {
        get
        {
            if (_quitting) return null;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AudioManager>();

                // Auto-bootstrap: a scene that never bothered to host one still gets sound.
                if (_instance == null)
                    _instance = new GameObject(nameof(AudioManager)).AddComponent<AudioManager>();
            }

            return _instance;
        }
    }

    /// <summary>
    /// True when a manager exists already. Call sites that fire on teardown use this to avoid
    /// resurrecting the manager just to play a sound nobody will hear.
    /// </summary>
    public static bool Exists => !_quitting && _instance != null;

    public AudioLibrary Library
    {
        get
        {
            if (library == null) library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);

            // Last resort so a missing asset plays silence instead of throwing at a call site that has
            // no business knowing about libraries.
            if (library == null) library = ScriptableObject.CreateInstance<AudioLibrary>();

            return library;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Volume settings. 0..1 throughout; the curve to perceived loudness is applied at the voice.
    // ---------------------------------------------------------------------------------------------

    public float MasterVolume
    {
        get { EnsureSettingsLoaded(); return _masterVolume; }
        set => SetVolume(ref _masterVolume, value, MasterVolumeKey);
    }

    public float SfxVolume
    {
        get { EnsureSettingsLoaded(); return _sfxVolume; }
        set => SetVolume(ref _sfxVolume, value, SfxVolumeKey);
    }

    public float MusicVolume
    {
        get { EnsureSettingsLoaded(); return _musicVolume; }
        set => SetVolume(ref _musicVolume, value, MusicVolumeKey);
    }

    public bool Muted
    {
        get { EnsureSettingsLoaded(); return _muted; }
        set
        {
            EnsureSettingsLoaded();
            if (_muted == value) return;

            _muted = value;
            PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
            PlayerPrefs.Save();

            if (_muted) StopAll();
            else ApplyMusicVolume();
        }
    }

    private void SetVolume(ref float field, float value, string key)
    {
        EnsureSettingsLoaded();

        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(field, value)) return;

        field = value;
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();

        // Music is the one bus that is usually already sounding when the slider moves, so it has to
        // follow live. One-shots pick the new level up on their next play, which is soon enough.
        ApplyMusicVolume();
    }

    /// <summary>
    /// Settings live in PlayerPrefs rather than the JSON save. They have to be readable before
    /// SaveManager exists (the very first UI hover in the menu can beat it), they are per-device rather
    /// than per-profile, and a corrupt save must never cost the player their volume.
    /// </summary>
    private void EnsureSettingsLoaded()
    {
        if (_settingsLoaded) return;
        _settingsLoaded = true;

        _masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        _sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);
        _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
    }

    /// <summary>
    /// The 0..1 slider position turned into a gain. Squared because loudness is perceived roughly
    /// logarithmically -- a linear slider spends most of its travel sounding equally loud, and only
    /// drops off in the last sliver.
    /// </summary>
    private float BusGain(AudioBus bus)
    {
        EnsureSettingsLoaded();

        if (_muted) return 0f;

        float busVolume = bus == AudioBus.Music ? _musicVolume : _sfxVolume;
        float linear = _masterVolume * busVolume;

        return linear * linear;
    }

    // ---------------------------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // The pool has to outlive the scene it was built in, or every scene change would drop the
        // music and re-allocate every voice.
        if (transform.parent == null) DontDestroyOnLoad(gameObject);

        EnsureSettingsLoaded();

        for (int i = 0; i < InitialVoices; i++) CreateVoice();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnApplicationQuit() => _quitting = true;

    private void Update()
    {
        // Release the live-count a sound holds once its voice has finished on its own. Cheap: the pool
        // is a couple of dozen entries and only the ones still carrying a sound are touched.
        for (int i = 0; i < _voices.Count; i++)
        {
            Voice voice = _voices[i];
            if (voice.sound == null || voice.reserved || voice.source.isPlaying) continue;

            voice.sound.NotifyVoiceStopped();
            voice.sound = null;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Playing
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Plays a sound flat (2D). Returns the voice it landed on, or null if the sound was throttled,
    /// muted, or has no clips -- so a caller may hold the result to fade it, but must null-check first.
    /// </summary>
    public AudioSource Play(SoundEffect sound, float delay = 0f) => PlayInternal(sound, null, delay);

    /// <summary>Plays a sound positioned in the world. Audible as positioned only if the sound has spatial blend.</summary>
    public AudioSource PlayAt(SoundEffect sound, Vector3 worldPosition, float delay = 0f) =>
        PlayInternal(sound, worldPosition, delay);

    /// <summary>Plays the sound mapped to <paramref name="id"/> in the library. The call site most game code uses.</summary>
    public AudioSource Play(GameSound id, float delay = 0f) => Play(Library.Get(id), delay);

    /// <summary>Plays the sound mapped to <paramref name="id"/>, positioned in the world.</summary>
    public AudioSource PlayAt(GameSound id, Vector3 worldPosition, float delay = 0f) =>
        PlayAt(Library.Get(id), worldPosition, delay);

    /// <summary>
    /// Plays a bare clip, for the odd case with no authored SoundEffect behind it. Prefer a SoundEffect:
    /// it is the thing a designer can tune without a recompile.
    /// </summary>
    public AudioSource PlayClip(AudioClip clip, AudioBus bus = AudioBus.Sfx, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || _muted) return null;

        Voice voice = AcquireVoice(128);
        if (voice == null) return null;

        voice.sound = null;
        voice.startedAt = Time.unscaledTime;
        voice.source.transform.localPosition = Vector3.zero;

        voice.source.clip = clip;
        voice.source.loop = false;
        voice.source.spatialBlend = 0f;
        voice.source.priority = 128;
        voice.source.volume = Mathf.Clamp01(volume) * BusGain(bus);
        voice.source.pitch = Mathf.Approximately(pitch, 0f) ? 1f : pitch;
        voice.source.time = 0f;
        voice.source.Play();

        return voice.source;
    }

    private AudioSource PlayInternal(SoundEffect sound, Vector3? worldPosition, float delay)
    {
        if (sound == null || !sound.HasClips) return null;

        EnsureSettingsLoaded();
        if (_muted) return null;

        // Throttle before spending a voice, and before the delay: a burst of requests in one frame must
        // be rejected on the spot, not queued up to all fire together a moment later.
        if (!sound.CanPlayNow()) return null;

        Voice voice = AcquireVoice(sound.priority);
        if (voice == null) return null;

        // Claim the sound's slot now, so simultaneous requests in this same frame see the raised count.
        sound.NotifyVoiceStarted();
        voice.sound = sound;
        voice.startedAt = Time.unscaledTime;

        if (delay > 0f)
        {
            // Reserved rather than started, so the voice cannot be handed out again in the meantime.
            voice.reserved = true;
            StartCoroutine(PlayDelayed(voice, sound, worldPosition, delay));
            return voice.source;
        }

        StartVoice(voice, sound, worldPosition);
        return voice.source;
    }

    private IEnumerator PlayDelayed(Voice voice, SoundEffect sound, Vector3? worldPosition, float delay)
    {
        // Unscaled: a delayed sound is feedback on something the player did, and must not stretch out
        // because a tween slowed time down.
        yield return new WaitForSecondsRealtime(delay);

        voice.reserved = false;

        // The manager may have been torn down (scene change, quit) during the wait.
        if (this == null || voice.source == null)
        {
            sound.NotifyVoiceStopped();
            yield break;
        }

        if (_muted)
        {
            sound.NotifyVoiceStopped();
            voice.sound = null;
            yield break;
        }

        StartVoice(voice, sound, worldPosition);
    }

    private void StartVoice(Voice voice, SoundEffect sound, Vector3? worldPosition)
    {
        Transform voiceTransform = voice.source.transform;

        if (worldPosition.HasValue) voiceTransform.position = worldPosition.Value;
        else voiceTransform.localPosition = Vector3.zero;

        voice.source.outputAudioMixerGroup = mixerGroup;

        // Each copy of a sound already playing pulls the next one down, so six deaths in a frame swell
        // rather than sum into distortion. LiveVoices already counts this one, hence the -1.
        float stackScale = 1f;
        int stacked = Mathf.Max(0, sound.LiveVoices - 1);
        for (int i = 0; i < stacked; i++) stackScale *= sound.stackVolumeFalloff;

        sound.ApplyTo(voice.source, BusGain(sound.bus) * stackScale);

        // ApplyTo bails without starting anything if every clip slot turned out to be empty; releasing
        // here keeps the sound's live-count from drifting up forever.
        if (!voice.source.isPlaying)
        {
            sound.NotifyVoiceStopped();
            voice.sound = null;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Stopping
    // ---------------------------------------------------------------------------------------------

    /// <summary>Stops every voice currently playing <paramref name="sound"/>, optionally fading them out.</summary>
    public void StopSound(SoundEffect sound, float fadeOut = 0f)
    {
        if (sound == null) return;

        for (int i = 0; i < _voices.Count; i++)
        {
            if (_voices[i].sound != sound) continue;
            ReleaseVoice(_voices[i], fadeOut);
        }
    }

    /// <summary>Stops one voice handed back by a Play call. Safe if the voice has already been recycled.</summary>
    public void Stop(AudioSource source, float fadeOut = 0f)
    {
        if (source == null) return;

        for (int i = 0; i < _voices.Count; i++)
        {
            if (_voices[i].source != source) continue;
            ReleaseVoice(_voices[i], fadeOut);
            return;
        }

        source.Stop();
    }

    /// <summary>Stops every sound effect. Leaves the music alone -- see <see cref="StopMusic"/>.</summary>
    public void StopAll()
    {
        for (int i = 0; i < _voices.Count; i++) ReleaseVoice(_voices[i], 0f);
    }

    private void ReleaseVoice(Voice voice, float fadeOut)
    {
        if (voice.sound != null)
        {
            voice.sound.NotifyVoiceStopped();
            voice.sound = null;
        }

        voice.reserved = false;

        if (fadeOut > 0f && voice.source.isPlaying) StartCoroutine(FadeOutAndStop(voice.source, fadeOut));
        else voice.source.Stop();
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        float startVolume = source.volume;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (source == null) yield break;

            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        if (source == null) yield break;

        source.Stop();
        source.volume = startVolume;
    }

    // ---------------------------------------------------------------------------------------------
    // Music
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Starts (or crossfades to) a looping track. Re-requesting the track already playing is a no-op, so
    /// a scene that reloads does not restart its own music.
    /// </summary>
    public void PlayMusic(SoundEffect music, float fadeDuration = 1f)
    {
        if (music == null || !music.HasClips) return;
        if (_musicSound == music && _musicSource != null && _musicSource.isPlaying) return;

        _musicSound = music;

        EnsureMusicSource();

        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(CrossfadeMusic(music, fadeDuration));
    }

    public void StopMusic(float fadeDuration = 1f)
    {
        if (_musicSource == null || !_musicSource.isPlaying) return;

        _musicSound = null;

        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(FadeOutAndStop(_musicSource, Mathf.Max(0.01f, fadeDuration)));
    }

    private IEnumerator CrossfadeMusic(SoundEffect music, float fadeDuration)
    {
        float target = music.ResolveVolume() * BusGain(AudioBus.Music);

        if (_musicSource.isPlaying && fadeDuration > 0f)
        {
            float from = _musicSource.volume;
            for (float elapsed = 0f; elapsed < fadeDuration * 0.5f; elapsed += Time.unscaledDeltaTime)
            {
                _musicSource.volume = Mathf.Lerp(from, 0f, elapsed / (fadeDuration * 0.5f));
                yield return null;
            }
        }

        _musicSource.Stop();
        _musicSource.clip = music.NextClip();
        // A music track loops regardless of how the asset is flagged; a track that stopped dead mid-match
        // would read as a bug, and the alternative (authoring every music asset with loop ticked) is a
        // trap waiting for whoever adds the second track.
        _musicSource.loop = true;
        _musicSource.pitch = music.ResolvePitch();
        _musicSource.spatialBlend = 0f;
        _musicSource.volume = fadeDuration > 0f ? 0f : target;
        _musicSource.Play();

        for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            _musicSource.volume = Mathf.Lerp(0f, target, elapsed / fadeDuration);
            yield return null;
        }

        _musicSource.volume = target;
        _musicFade = null;
    }

    private void EnsureMusicSource()
    {
        if (_musicSource != null) return;

        var musicObject = new GameObject("Music");
        musicObject.transform.SetParent(transform, false);

        _musicSource = musicObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        _musicSource.outputAudioMixerGroup = mixerGroup;
    }

    /// <summary>Re-levels the playing track after a slider move, unless a fade is mid-flight and owns the volume.</summary>
    private void ApplyMusicVolume()
    {
        if (_musicSource == null || _musicSound == null || _musicFade != null) return;

        _musicSource.volume = _musicSound.ResolveVolume() * BusGain(AudioBus.Music);
    }

    // ---------------------------------------------------------------------------------------------
    // Voice pool
    // ---------------------------------------------------------------------------------------------

    private Voice CreateVoice()
    {
        var voiceObject = new GameObject($"Voice {_voices.Count}");
        voiceObject.transform.SetParent(transform, false);

        var source = voiceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = mixerGroup;
        // Rolloff only matters once a sound has spatial blend, but setting it here means a positional
        // sound does not need every call site to remember to.
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 40f;

        var voice = new Voice { source = source };
        _voices.Add(voice);

        return voice;
    }

    /// <summary>
    /// A free voice, growing the pool up to <see cref="MaxVoices"/>. At the cap, steals the
    /// longest-running voice that is no more important than the incoming sound; returns null if every
    /// voice is busy with something that outranks it, so a low-priority sound is dropped rather than
    /// cutting off something that matters.
    /// </summary>
    private Voice AcquireVoice(int incomingPriority)
    {
        for (int i = 0; i < _voices.Count; i++)
        {
            if (!_voices[i].IsBusy) return _voices[i];
        }

        if (_voices.Count < MaxVoices) return CreateVoice();

        Voice oldest = null;
        for (int i = 0; i < _voices.Count; i++)
        {
            Voice candidate = _voices[i];

            // Lower priority value = more important, matching AudioSource.priority.
            if (candidate.source.priority < incomingPriority) continue;
            if (candidate.reserved) continue;

            if (oldest == null || candidate.startedAt < oldest.startedAt) oldest = candidate;
        }

        if (oldest == null) return null;

        ReleaseVoice(oldest, 0f);
        return oldest;
    }
}
