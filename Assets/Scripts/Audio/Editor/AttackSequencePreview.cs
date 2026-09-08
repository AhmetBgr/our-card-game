using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only preview of a whole attack, heard the way the game plays it: the click that picks the
/// attacker up, the strike, and the damage number landing -- three separate sounds the player only ever
/// hears as one action.
///
/// Auditioning them one at a time through the SoundEffect inspector's Play button says nothing about
/// whether they work together: the strike and the hit overlap by design, and a level or pitch that reads
/// fine alone can bury the hit or double its transient. This plays the trio spaced exactly as
/// <see cref="MinionController.Attack"/> and <see cref="MinionView"/> space them, so what is heard here is
/// what lands on the board.
///
/// It cannot reuse <see cref="SoundEffect"/>'s previewer: that is a single shared AudioSource, so each
/// step would cut the one before it. This owns a small pool instead and starts every step on its own
/// voice with PlayDelayed, which means the audio engine schedules the whole sequence on one frame -- no
/// coroutine, and no drift from the editor's uneven update.
/// </summary>
public static class AttackSequencePreview
{
    /// <summary>
    /// Two clicks apart: pick the minion up, then click a target. Player-paced, so unlike the offset below
    /// there is no number in the game to match -- this is a representative gap, short enough that the
    /// select still reads as part of the same action.
    /// </summary>
    private const float SelectToAttack = 0.6f;

    /// <summary>
    /// Strike to damage number. MinionController.Attack deals the damage on the same frame it raises
    /// OnAttacked, and MinionView holds the indicator back by damageIndicatorVisualDelay before raising
    /// DamageShown -- which is what the hit sound rides. Mirrors that field's default; if it is retuned on
    /// the minion prefab, retune this with it.
    /// </summary>
    private const float AttackToHit = 0.35f;

    /// <summary>
    /// One sound in a sequence, and how long after the sequence starts the GAME would raise it. Each
    /// sound's own authored <see cref="SoundEffect.delay"/> is added on top, exactly as at runtime.
    /// </summary>
    private readonly struct Step
    {
        public readonly GameSound Id;
        public readonly float Offset;

        public Step(GameSound id, float offset)
        {
            Id = id;
            Offset = offset;
        }
    }

    private static readonly Step[] MeleeSequence =
    {
        new Step(GameSound.MinionSelectMelee, 0f),
        new Step(GameSound.MinionAttackMelee, SelectToAttack),
        new Step(GameSound.MinionHitMelee, SelectToAttack + AttackToHit)
    };

    private static readonly Step[] RangedSequence =
    {
        new Step(GameSound.MinionSelectRanged, 0f),
        new Step(GameSound.MinionAttackRanged, SelectToAttack),
        new Step(GameSound.MinionHitRanged, SelectToAttack + AttackToHit)
    };

    // ---------------------------------------------------------------------------------------------
    // Playback
    // ---------------------------------------------------------------------------------------------

    private static readonly List<AudioSource> Voices = new List<AudioSource>();

    public static void PlayMelee() => Play(MeleeSequence);

    public static void PlayRanged() => Play(RangedSequence);

    private static void Play(Step[] sequence)
    {
        Stop();

        AudioLibrary library = Library;
        if (library == null)
        {
            Debug.LogWarning($"No AudioLibrary at Resources/{AudioLibrary.ResourcePath}, so there is nothing to preview.");
            return;
        }

        for (int i = 0; i < sequence.Length; i++)
        {
            SoundEffect sound = library.Get(sequence[i].Id);

            // A step with nothing mapped is silence in the game too, so it is skipped rather than treated
            // as an error -- the inspector says which ones are missing.
            if (sound == null || !sound.HasClips) continue;

            // Every step gets its own voice, so the overlap between the strike and the hit is audible
            // instead of one cutting the other.
            sound.ApplyTo(VoiceAt(i), 1f, false, sound.ResolveDelay(sequence[i].Offset));
        }
    }

    public static void Stop()
    {
        foreach (AudioSource voice in Voices)
        {
            if (voice != null) voice.Stop();
        }
    }

    /// <summary>
    /// Whether any step is still sounding -- including one only scheduled so far, which reports isPlaying
    /// while it waits out its delay, and so keeps a Stop button live through the gaps.
    /// </summary>
    public static bool IsPlaying
    {
        get
        {
            foreach (AudioSource voice in Voices)
            {
                if (voice != null && voice.isPlaying) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// The pooled voice for step <paramref name="index"/>, made on first use. Hidden and not saved, so it
    /// never shows in the hierarchy or gets written into the open scene; a domain reload drops the objects
    /// and the list rebuilds itself on the next preview.
    /// </summary>
    private static AudioSource VoiceAt(int index)
    {
        while (Voices.Count <= index) Voices.Add(null);

        if (Voices[index] == null)
        {
            Voices[index] = EditorUtility
                .CreateGameObjectWithHideFlags("AttackSequencePreview", HideFlags.HideAndDontSave, typeof(AudioSource))
                .GetComponent<AudioSource>();
            Voices[index].playOnAwake = false;
        }

        return Voices[index];
    }

    // ---------------------------------------------------------------------------------------------
    // Library lookup.
    //
    // AudioManager only exists in play mode, so the library is loaded straight from Resources the same way
    // the manager finds it. Not cached: the asset is edited constantly while sounds are being tuned, and
    // Resources.Load on an already-loaded asset is a dictionary hit.
    // ---------------------------------------------------------------------------------------------

    private static AudioLibrary Library => Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);

    /// <summary>
    /// Whether <paramref name="sound"/> is one of the three sounds in the melee sequence -- what the
    /// SoundEffect inspector asks before offering the sequence button, so it shows up on the assets it is
    /// about and nowhere else.
    /// </summary>
    public static bool IsPartOfMelee(SoundEffect sound) => Contains(MeleeSequence, sound);

    public static bool IsPartOfRanged(SoundEffect sound) => Contains(RangedSequence, sound);

    private static bool Contains(Step[] sequence, SoundEffect sound)
    {
        if (sound == null) return false;

        AudioLibrary library = Library;
        if (library == null) return false;

        foreach (Step step in sequence)
        {
            if (library.Get(step.Id) == sound) return true;
        }

        return false;
    }

    /// <summary>
    /// The steps that have no usable sound behind them, worded for a help box. Null when every step of the
    /// sequence will actually be heard.
    /// </summary>
    public static string MissingSteps(bool ranged)
    {
        AudioLibrary library = Library;
        if (library == null) return $"No AudioLibrary at Resources/{AudioLibrary.ResourcePath}.";

        List<string> missing = new List<string>();

        foreach (Step step in ranged ? RangedSequence : MeleeSequence)
        {
            SoundEffect sound = library.Get(step.Id);
            if (sound == null || !sound.HasClips) missing.Add(step.Id.ToString());
        }

        return missing.Count == 0 ? null : $"Silent in this sequence: {string.Join(", ", missing)}.";
    }

    // ---------------------------------------------------------------------------------------------
    // Menu access, so the sequence can be heard while looking at something other than a sound asset.
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Tools/Audio/Preview Melee Attack")]
    private static void MenuPlayMelee() => PlayMelee();

    [MenuItem("Tools/Audio/Preview Ranged Attack")]
    private static void MenuPlayRanged() => PlayRanged();

    [MenuItem("Tools/Audio/Stop Attack Preview")]
    private static void MenuStop() => Stop();
}
