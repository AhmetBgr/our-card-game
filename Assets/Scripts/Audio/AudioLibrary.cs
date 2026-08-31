using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every moment in the game that makes a noise.
///
/// Deliberately a fixed enum rather than free strings: a typo is a compile error, the inspector shows the
/// full list of what is still unassigned, and renaming a sound never breaks a call site. New entries go
/// on the END -- the values are serialized by number in the library asset.
/// </summary>
public enum GameSound
{
    None = 0,

    // Cards in hand
    CardHover = 1,
    CardPickUp = 2,
    CardDrop = 3,
    CardReturn = 4,
    CardPlay = 5,
    CardDraw = 6,
    CardInvalid = 7,
    CardChoiceOpen = 8,
    CardChoiceClose = 9,

    // The board
    MinionSummon = 20,
    MinionMove = 21,
    MinionCollide = 22,
    MinionHit = 23,
    MinionDeath = 24,
    MinionSelect = 25,
    MinionAttack = 26,

    // Heroes
    HeroHit = 40,
    HeroDeath = 41,

    // Turn flow
    TurnStart = 60,
    TurnEnd = 61,
    OpponentTurnStart = 62,
    ManaGain = 63,
    ManaSpend = 64,

    // Outcomes
    Victory = 80,
    Defeat = 81,

    // Interface
    UIHover = 100,
    UIClick = 101,
    UIBack = 102,
    UIToggle = 103,
    UIOpen = 104,
    UIClose = 105,
    UIError = 106,

    // Music
    MusicMenu = 120,
    MusicMatch = 121
}

/// <summary>
/// Maps each <see cref="GameSound"/> to the <see cref="SoundEffect"/> that plays for it.
///
/// This is the piece the original system did not have: there, the controller carried one public
/// SoundEffect field per game event, so adding a sound meant editing the controller, and every scene
/// holding a controller instance had to be re-wired. Here the mapping is one asset at
/// Resources/AudioLibrary.asset that <see cref="AudioManager"/> finds by itself, and a missing entry is
/// silence rather than a crash.
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Audio/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    /// <summary>Resources path the manager loads this from when nothing is assigned in the scene.</summary>
    public const string ResourcePath = "AudioLibrary";

    [Serializable]
    public class Entry
    {
        public GameSound id;
        public SoundEffect sound;
    }

    [Tooltip("One row per sound the game can make. Rows with no sound assigned are silent, which is how a game event stays wired up before its clip exists.")]
    public List<Entry> entries = new List<Entry>();

    [Header("Automatic UI sounds")]
    [Tooltip("Give every Button, Toggle and Slider in a loaded scene hover/click sounds automatically, without a component on each one. See UISoundBinder.")]
    public bool autoBindUISounds = true;

    [Tooltip("Object names containing any of these (case-insensitive) are skipped by the automatic UI binding -- for controls that own their own sound, or should stay silent.")]
    public string[] autoBindNameExclusions = Array.Empty<string>();

    // Built lazily and dropped whenever the asset is edited, so re-mapping an id in the inspector during
    // play does not leave the lookup stale. Same pattern as FloatingTextConfig.
    private Dictionary<GameSound, SoundEffect> _byId;

    /// <summary>
    /// The sound mapped to <paramref name="id"/>, or null if nothing is mapped. Null is a valid answer --
    /// callers pass it straight to the manager, which treats it as silence.
    /// </summary>
    public SoundEffect Get(GameSound id)
    {
        if (id == GameSound.None) return null;

        if (_byId == null) BuildIndex();

        return _byId.TryGetValue(id, out SoundEffect sound) ? sound : null;
    }

    public bool Has(GameSound id) => Get(id) != null;

    private void BuildIndex()
    {
        _byId = new Dictionary<GameSound, SoundEffect>();

        if (entries == null) return;

        foreach (Entry entry in entries)
        {
            if (entry == null || entry.id == GameSound.None || entry.sound == null) continue;

            // Last row wins on a duplicate id, so a row appended to override an earlier one behaves the
            // way whoever appended it expects.
            _byId[entry.id] = entry.sound;
        }
    }

    private void OnEnable() => _byId = null;
    private void OnValidate() => _byId = null;

    /// <summary>Whether a name is opted out of the automatic UI sound pass.</summary>
    public bool IsExcludedFromAutoBind(string objectName)
    {
        if (autoBindNameExclusions == null || string.IsNullOrEmpty(objectName)) return false;

        foreach (string exclusion in autoBindNameExclusions)
        {
            if (string.IsNullOrWhiteSpace(exclusion)) continue;
            if (objectName.IndexOf(exclusion, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }
}
