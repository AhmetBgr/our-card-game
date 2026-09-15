using UnityEngine;

/// <summary>
/// Which volume bus a sound is mixed on. Buses are scaled independently by <see cref="AudioManager"/>
/// so the player can turn the music down without losing the game's feedback, and vice versa.
/// </summary>
public enum AudioBus
{
    Sfx,
    Ui,
    Music,

    /// <summary>
    /// Room tone and other beds: sounds that are always there rather than reactions to anything. Its own
    /// bus because it is the first thing a player turns down, and nothing else should go down with it.
    ///
    /// Added at the END -- the values are serialized by number in every SoundEffect asset, so inserting
    /// it anywhere else would silently re-bus every authored sound.
    /// </summary>
    Ambient
}

/// <summary>How a multi-clip sound picks its next clip.</summary>
public enum SoundPlayOrder
{
    /// <summary>Uniformly random. Can repeat the same clip twice in a row.</summary>
    Random,

    /// <summary>Random, but never the clip that just played. The right default for a variation set.</summary>
    RandomNoRepeat,

    InOrder,
    Reverse
}

/// <summary>
/// One authored sound: a set of interchangeable clips plus how to play them (volume, pitch, jitter,
/// bus, and how hard to throttle it).
///
/// A SoundEffect is a *description*, not a player. It never owns an AudioSource -- <see cref="AudioManager"/>
/// hands it a pooled voice. That split is what lets the same asset play from ten places at once, and lets
/// the manager cap and steal voices without a sound asset holding a stale reference to one.
///
/// Authoring lives entirely in the inspector (see SoundEffectEditor); call sites only ever say
/// <c>sound.Play()</c> or ask the library for a <see cref="GameSound"/>.
/// </summary>
[CreateAssetMenu(fileName = "New Sound", menuName = "Audio/Sound Effect")]
public class SoundEffect : ScriptableObject
{
    [Tooltip("Interchangeable takes of the same sound. More than one turns on the variation picked by Play Order.")]
    public AudioClip[] clips;

    [Tooltip("Volume bus this mixes on. UI stays audible when the player pulls SFX down for a busy board.")]
    public AudioBus bus = AudioBus.Sfx;

    public SoundPlayOrder playOrder = SoundPlayOrder.RandomNoRepeat;

    [Range(0f, 1f)]
    public float volume = 1f;

    public bool useRandomVolume;

    [Tooltip("Min/max volume when Use Random Volume is on. Keep the spread small; large jumps read as a bug.")]
    public Vector2 volumeRandom = new Vector2(0.9f, 1f);

    [Range(-3f, 3f)]
    public float pitch = 1f;

    public bool useRandomPitch;

    [Tooltip("Min/max pitch when Use Random Pitch is on. Around 5 percent is enough to stop repeats sounding machine-gunned.")]
    public Vector2 pitchRandom = new Vector2(0.95f, 1.05f);

    public bool loop;

    [Tooltip("Seconds to wait before this sound is heard, added on top of any delay the call site asks for. For a sound that has to land with a beat in an animation rather than on the frame the code fired.")]
    [Min(0f)]
    public float delay;

    [Tooltip("Fade this out when something stops it, instead of cutting. A sustained sound chopped mid-cycle is an audible click; a short one-shot is usually better cut. Has no effect on a sound left to finish on its own.")]
    public bool fadeOut;

    [Tooltip("Seconds the fade takes. Long enough to hide the cut, short enough that a stop still feels immediate.")]
    [Min(0.01f)]
    public float fadeOutDuration = 0.08f;

    [Range(0f, 1f)]
    [Tooltip("0 = flat 2D (everything the UI does). 1 = positioned in the world, so board sounds pan with the cell they came from.")]
    public float spatialBlend;

    [Range(0, 256)]
    [Tooltip("Lower wins when every voice is busy. Leave 128 unless this sound must never be dropped.")]
    public int priority = 128;

    [Header("Throttling")]
    [Tooltip("Ignore repeat requests that arrive within this many seconds. A board where six minions die at once would otherwise stack six copies of the same clip and clip the mix.")]
    [Min(0f)]
    public float minRetriggerInterval = 0.04f;

    [Tooltip("Hard cap on copies of THIS sound playing at once. 0 = unlimited.")]
    [Min(0)]
    public int maxConcurrent = 4;

    [Tooltip("Each extra copy already playing scales the next one down by this much, so a burst swells instead of distorting. 1 = no falloff.")]
    [Range(0.1f, 1f)]
    public float stackVolumeFalloff = 0.75f;

    // ---------------------------------------------------------------------------------------------
    // Runtime state. Deliberately not serialized: the original stored its play cursor in a
    // [SerializeField], which meant every play in the editor dirtied the .asset on disk and the
    // sequence carried over between sessions. These reset with the asset load, and again in OnEnable
    // for the domain-reload-off case where a ScriptableObject can outlive a play session.
    // ---------------------------------------------------------------------------------------------
    [System.NonSerialized] private int _playIndex;
    [System.NonSerialized] private int _lastPlayedIndex = -1;
    [System.NonSerialized] private float _lastPlayTime = float.NegativeInfinity;
    [System.NonSerialized] private int _liveVoices;

    private void OnEnable()
    {
        _playIndex = 0;
        _lastPlayedIndex = -1;
        _lastPlayTime = float.NegativeInfinity;
        _liveVoices = 0;
    }

    public bool HasClips => clips != null && clips.Length > 0;

    /// <summary>
    /// Whether any slot actually holds a clip. Stricter than <see cref="HasClips"/>, which only asks
    /// whether the array has a slot in it -- an asset authored ahead of its audio has exactly one, and
    /// it is empty.
    ///
    /// For call sites that have to CHOOSE based on the answer rather than just play: an empty asset is
    /// silence either way, but a caller swapping between two sounds needs to know that the one it is
    /// about to switch to would say nothing.
    /// </summary>
    public bool HasAudibleClip
    {
        get
        {
            if (clips == null) return false;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null) return true;
            }

            return false;
        }
    }

    /// <summary>Copies playing right now, as tracked by <see cref="AudioManager"/>.</summary>
    public int LiveVoices => _liveVoices;

    /// <summary>
    /// Plays through the shared <see cref="AudioManager"/>. The convenience entry point -- call sites
    /// say <c>sound.Play()</c> and never touch the manager.
    /// </summary>
    public AudioSource Play(float delay = 0f) => AudioManager.Instance.Play(this, delay);

    /// <summary>Plays positioned in the world. Only audible as positioned if <see cref="spatialBlend"/> is above 0.</summary>
    public AudioSource PlayAt(Vector3 worldPosition, float delay = 0f) =>
        AudioManager.Instance.PlayAt(this, worldPosition, delay);

    /// <summary>
    /// Stops every copy of this sound that is currently playing, fading it out if the asset says to.
    /// Pass a duration to override that, or 0 to force a hard cut.
    /// </summary>
    public void Stop(float fadeOverride = UseAssetFade) => AudioManager.Instance.StopSound(this, fadeOverride);

    /// <summary>
    /// Passed as a fade duration to mean "whatever the asset is set to". A real duration (0 included)
    /// overrides it, so a caller that genuinely needs a hard cut can still ask for one.
    /// </summary>
    public const float UseAssetFade = -1f;

    /// <summary>
    /// The seconds to wait before this sound starts: the asset's own <see cref="delay"/> plus whatever the
    /// call site asked for. Added rather than overridden, so a call site that needs to line a sound up with
    /// its own animation still keeps the offset the asset was authored with.
    /// </summary>
    public float ResolveDelay(float requested = 0f) => Mathf.Max(0f, delay) + Mathf.Max(0f, requested);

    /// <summary>Turns a requested fade duration into the seconds to actually fade for.</summary>
    public float ResolveFadeOut(float requested = UseAssetFade)
    {
        if (requested >= 0f) return requested;

        return fadeOut ? Mathf.Max(0.01f, fadeOutDuration) : 0f;
    }

    // ---------------------------------------------------------------------------------------------
    // Throttling -- asked by AudioManager before it spends a voice.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether a new copy is allowed right now. Uses unscaled time so sounds keep their spacing while
    /// the game is paused or a slow-motion tween is running.
    /// </summary>
    public bool CanPlayNow()
    {
        if (!HasClips) return false;

        if (minRetriggerInterval > 0f && Time.unscaledTime - _lastPlayTime < minRetriggerInterval)
            return false;

        if (maxConcurrent > 0 && _liveVoices >= maxConcurrent)
            return false;

        return true;
    }

    /// <summary>Called by the manager the moment a voice starts on this sound.</summary>
    internal void NotifyVoiceStarted()
    {
        _lastPlayTime = Time.unscaledTime;
        _liveVoices++;
    }

    /// <summary>Called by the manager when one of this sound's voices is freed.</summary>
    internal void NotifyVoiceStopped()
    {
        if (_liveVoices > 0) _liveVoices--;
    }

    // ---------------------------------------------------------------------------------------------
    // Resolving one playback
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The clip to play next, advancing the cursor. Null only when <see cref="clips"/> is empty or holds
    /// nothing but null entries (an authoring slip that must not throw at a call site).
    /// </summary>
    public AudioClip NextClip()
    {
        if (!HasClips) return null;

        // A single clip has no ordering to do, and skipping the switch keeps RandomNoRepeat from
        // looking for an alternative that does not exist.
        if (clips.Length == 1) return clips[0];

        int index;

        switch (playOrder)
        {
            case SoundPlayOrder.Random:
                index = Random.Range(0, clips.Length);
                break;

            case SoundPlayOrder.RandomNoRepeat:
                index = Random.Range(0, clips.Length - 1);
                // Fold the excluded slot out of the range instead of re-rolling, so the pick stays O(1)
                // and every other clip keeps an equal share.
                if (_lastPlayedIndex >= 0 && index >= _lastPlayedIndex) index++;
                if (index >= clips.Length) index = 0;
                break;

            case SoundPlayOrder.Reverse:
                index = _playIndex;
                _playIndex = (_playIndex + clips.Length - 1) % clips.Length;
                break;

            default: // InOrder
                index = _playIndex;
                _playIndex = (_playIndex + 1) % clips.Length;
                break;
        }

        _lastPlayedIndex = index;

        AudioClip clip = clips[index];

        // Tolerate holes in the array: fall back to the first clip that is actually assigned rather
        // than handing the manager a null and playing silence.
        if (clip == null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                _lastPlayedIndex = i;
                return clips[i];
            }
        }

        return clip;
    }

    public float ResolveVolume()
    {
        float value = useRandomVolume ? Random.Range(volumeRandom.x, volumeRandom.y) : volume;
        return Mathf.Clamp01(value);
    }

    public float ResolvePitch()
    {
        float value = useRandomPitch ? Random.Range(pitchRandom.x, pitchRandom.y) : pitch;
        // A pitch of exactly 0 is silence that never finishes, which would pin a pooled voice forever.
        return Mathf.Approximately(value, 0f) ? 1f : value;
    }

    /// <summary>
    /// Configures <paramref name="source"/> to play one instance of this sound and starts it.
    ///
    /// Assigns the clip and calls Play() rather than PlayOneShot: a pooled voice is owned exclusively
    /// for the length of the sound, so the manager can read isPlaying to know when it is free again --
    /// and PlayOneShot ignores <see cref="loop"/>, which silently broke looping sounds in the original.
    /// </summary>
    /// <param name="volumeScale">Bus and stack scaling the manager has already worked out.</param>
    /// <param name="playReverse">Plays the clip backwards (negative pitch, cursor started at the end).</param>
    /// <param name="startDelay">
    /// Seconds to hold the voice before it sounds. The manager leaves this at 0 and does its own waiting so
    /// it can keep the voice reserved; the editor preview passes the resolved delay so a designer hears the
    /// sound with the timing it was authored with.
    /// </param>
    public void ApplyTo(AudioSource source, float volumeScale = 1f, bool playReverse = false, float startDelay = 0f)
    {
        if (source == null) return;

        AudioClip clip = NextClip();
        if (clip == null) return;

        source.clip = clip;
        source.loop = loop;
        source.volume = ResolveVolume() * Mathf.Max(0f, volumeScale);
        source.priority = priority;
        source.spatialBlend = spatialBlend;

        float resolvedPitch = ResolvePitch();
        source.pitch = playReverse ? -Mathf.Abs(resolvedPitch) : resolvedPitch;

        // Reverse playback has to start at the far end of the clip, a hair inside it -- exactly
        // clip.length is out of range.
        source.time = source.pitch < 0f ? Mathf.Max(0f, clip.length - 0.01f) : 0f;

        if (startDelay > 0f) source.PlayDelayed(startDelay);
        else source.Play();
    }

#if UNITY_EDITOR
    // ---------------------------------------------------------------------------------------------
    // Inspector preview.
    //
    // One previewer shared by every SoundEffect asset, created on first use. The original built a hidden
    // GameObject in OnEnable of EVERY sound asset -- so merely having the assets loaded littered the
    // scene with previewers, and OnDisable threw if one had not been made yet.
    // ---------------------------------------------------------------------------------------------
    private static AudioSource _previewer;

    private static AudioSource Previewer
    {
        get
        {
            if (_previewer == null)
            {
                _previewer = UnityEditor.EditorUtility
                    .CreateGameObjectWithHideFlags("AudioPreview", HideFlags.HideAndDontSave, typeof(AudioSource))
                    .GetComponent<AudioSource>();
                _previewer.playOnAwake = false;
            }

            return _previewer;
        }
    }

    public void PlayPreview(bool playReverse = false) => ApplyTo(Previewer, 1f, playReverse, ResolveDelay());

    public void StopPreview()
    {
        if (_previewer != null) _previewer.Stop();
    }

    public bool IsPreviewing => _previewer != null && _previewer.isPlaying && _previewer.clip != null;
#endif
}
