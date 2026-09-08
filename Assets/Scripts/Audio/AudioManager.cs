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

    /// <summary>
    /// Whether the game was muted the last time <see cref="OnSettingsChanged"/> looked. A mute has to
    /// cut every voice, and unmuting has to re-level the music, so the transition matters rather than
    /// the value -- and the settings raise one undifferentiated "something changed".
    /// </summary>
    private bool _wasMuted;

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
    //
    // The values themselves belong to GameSettings, which owns loading, persisting and telling anyone
    // who is showing them that they moved. These properties are the audio-side face of that, kept so a
    // call site levelling a bus does not have to know where the number is stored.
    // ---------------------------------------------------------------------------------------------

    public float MasterVolume
    {
        get => GameSettings.MasterVolume;
        set => GameSettings.MasterVolume = value;
    }

    public float SfxVolume
    {
        get => GameSettings.SfxVolume;
        set => GameSettings.SfxVolume = value;
    }

    public float MusicVolume
    {
        get => GameSettings.MusicVolume;
        set => GameSettings.MusicVolume = value;
    }

    /// <summary>
    /// Room tone and other beds. Nothing is authored on this bus yet -- it is wired through so the
    /// slider the player sets today still means the same thing on the day ambience ships.
    /// </summary>
    public float AmbientVolume
    {
        get => GameSettings.AmbientVolume;
        set => GameSettings.AmbientVolume = value;
    }

    public bool Muted
    {
        get => GameSettings.Muted;
        set => GameSettings.Muted = value;
    }

    /// <summary>
    /// Any setting moved. Music is the one bus that is usually already sounding when a slider does, so
    /// it has to follow live; one-shots pick the new level up on their next play, which is soon enough.
    /// </summary>
    private void OnSettingsChanged()
    {
        bool muted = GameSettings.Muted;

        if (muted != _wasMuted)
        {
            _wasMuted = muted;

            // A mute that takes a moment to arrive is a broken mute, so the voices are cut rather than
            // left to finish quietly.
            if (muted) StopAll();
        }

        ApplyMusicVolume();
    }

    /// <summary>
    /// The 0..1 slider position turned into a gain. Squared because loudness is perceived roughly
    /// logarithmically -- a linear slider spends most of its travel sounding equally loud, and only
    /// drops off in the last sliver.
    /// </summary>
    private float BusGain(AudioBus bus)
    {
        if (GameSettings.Muted) return 0f;

        float busVolume;
        switch (bus)
        {
            case AudioBus.Music:
                busVolume = GameSettings.MusicVolume;
                break;
            case AudioBus.Ambient:
                busVolume = GameSettings.AmbientVolume;
                break;
            // Interface sounds ride the SFX bus. They are feedback on the player's own clicks, and a
            // separate slider for them is a setting nobody has ever wanted to move on its own.
            default:
                busVolume = GameSettings.SfxVolume;
                break;
        }

        float linear = GameSettings.MasterVolume * busVolume;

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

        _wasMuted = GameSettings.Muted;
        GameSettings.Changed += OnSettingsChanged;

        for (int i = 0; i < InitialVoices; i++) CreateVoice();
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= OnSettingsChanged;

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
        if (clip == null || GameSettings.Muted) return null;

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

        if (GameSettings.Muted) return null;

        // Throttle before spending a voice, and before the delay: a burst of requests in one frame must
        // be rejected on the spot, not queued up to all fire together a moment later.
        if (!sound.CanPlayNow()) return null;

        // The asset's own delay rides on top of whatever the call site asked for.
        delay = sound.ResolveDelay(delay);

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

        if (GameSettings.Muted)
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

    /// <summary>
    /// Stops every voice currently playing <paramref name="sound"/>, fading out by however long the asset
    /// is set to. Pass a duration to override that, or 0 to force a hard cut.
    /// </summary>
    public void StopSound(SoundEffect sound, float fadeOverride = SoundEffect.UseAssetFade)
    {
        if (sound == null) return;

        float fade = sound.ResolveFadeOut(fadeOverride);

        for (int i = 0; i < _voices.Count; i++)
        {
            if (_voices[i].sound != sound) continue;
            ReleaseVoice(_voices[i], fade);
        }
    }

    /// <summary>Stops one voice handed back by a Play call. Safe if the voice has already been recycled.</summary>
    public void Stop(AudioSource source, float fadeOverride = SoundEffect.UseAssetFade)
    {
        if (source == null) return;

        for (int i = 0; i < _voices.Count; i++)
        {
            if (_voices[i].source != source) continue;

            // The voice knows which sound it is carrying, so a caller holding only an AudioSource still
            // gets that sound's authored fade without having to know what it is playing.
            Voice voice = _voices[i];
            float fade = voice.sound != null ? voice.sound.ResolveFadeOut(fadeOverride)
                                             : Mathf.Max(0f, fadeOverride);
            ReleaseVoice(voice, fade);
            return;
        }

        source.Stop();
    }

    /// <summary>
    /// Stops every sound effect. Leaves the music alone -- see <see cref="StopMusic"/>.
    ///
    /// Hard cut, ignoring per-sound fades: the one caller is muting, and a mute that takes a moment to
    /// arrive is a broken mute.
    /// </summary>
    public void StopAll()
    {
        for (int i = 0; i < _voices.Count; i++) ReleaseVoice(_voices[i], 0f);
    }

    /// <summary><paramref name="fadeOut"/> is an already-resolved duration in seconds; 0 cuts.</summary>
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
