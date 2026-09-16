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

    /// <summary>
    /// A SPELL going off, alongside <see cref="CardPlay"/> (which every card makes). The pair is the
    /// mirror of a minion card, which gets CardPlay plus <see cref="MinionSummon"/> -- a spell leaves
    /// nothing on the board, so without this its play is the only card play that lands with no second
    /// sound behind it. Minion cards never make this one.
    ///
    /// Cued to the spell GOING OFF (GameManager.OnCardEffectsStarting) -- after the player has answered
    /// whatever it asked them to target, just before the first thing it does -- unlike CardPlay, which
    /// rides the commit once every last action has finished.
    /// </summary>
    SpellPlay = 10,

    /// <summary>
    /// A spell sitting in the play area with its prompt up, waiting to be told what to point at. The
    /// held half of the pair: it starts when the card lands and the targeting begins, and is stopped
    /// by whichever way the wait ends -- <see cref="SpellPlay"/> when the pick is made, or the card
    /// going back to hand when the play is backed out of. Meant for something sustained (a charge, a
    /// hum); if the clip loops, it loops until one of those two stops it.
    ///
    /// Only for a spell that actually ASKS for something. A spell that just goes off never waits, so
    /// it never makes this sound.
    /// </summary>
    SpellPending = 11,

    /// <summary>
    /// A card being FORGED: the upgraded copy popping into the play area after the card that made it has
    /// been played, before it flies off to the deck. Its own sound rather than a second <see cref="CardPlay"/>
    /// because nothing else in the game hands you a permanently better card, and the moment has a whole
    /// animation of its own -- the pop, the hold, the flight -- that would otherwise pass in silence.
    ///
    /// The morph is three separate blows, and each gets its own sound so the forge can be scored as a
    /// sequence -- a first strike, a heavier second, a final one -- rather than one clip laid over the
    /// whole thing. Cued to the blow LANDING (CardView.ForgeStageStruck), not to the morph starting, so
    /// each lands exactly with the punch it belongs to.
    ///
    /// Stage 1 keeps id 12 -- it is the original CardForged, renamed now that its two siblings sit
    /// beside it, so whatever was mapped there stays the first strike.
    ///
    /// Only the MORPH makes these. The plain pop-and-fly has no stages, so it is silent -- which also
    /// means the opponent's forges are silent, since that is the only animation the opponent uses. Any
    /// stage with no clip assigned is simply silent, so these can be filled in one at a time.
    /// </summary>
    CardForgeStage1 = 12,

    /// <summary>The second blow: the card's frame, name and art becoming the upgrade's.</summary>
    CardForgeStage2 = 13,

    /// <summary>The third blow: the description changing. The only stage that does not punch, so this
    /// is the one place the sound is carrying the beat on its own.</summary>
    CardForgeStage3 = 14,

    // The board
    MinionSummon = 20,
    MinionMove = 21,
    MinionCollide = 22,

    /// <summary>A hit that is not a minion's attack: a spell, a collision, the empty-deck clock. Keeps
    /// id 23 -- it is the original MinionHit, renamed now that the attack kinds sit beside it.</summary>
    MinionHitEffect = 23,

    /// <summary>Taking a melee strike, and <see cref="MinionHitRanged"/> an arrow. Chosen by the
    /// DamageSource the hit carried, and played when the damage NUMBER lands, not when it was dealt.</summary>
    MinionHitMelee = 28,
    MinionHitRanged = 29,
    MinionDeath = 24,

    /// <summary>
    /// Picking a minion up to attack with -- the click that lights its targets. Split by reach exactly as
    /// the strike is, because a sword coming off the shoulder and a bow being drawn are different sounds.
    /// Melee keeps id 25, the original MinionSelect, so whatever was mapped there stays the melee one.
    /// </summary>
    MinionSelectMelee = 25,
    MinionSelectRanged = 30,

    /// <summary>A minion's own strike landing. Split by reach rather than by one MinionAttack, because
    /// a sword swing and a loosed arrow are different sounds -- the binder picks by modal.range.</summary>
    MinionAttackMelee = 26,
    MinionAttackRanged = 27,

    /// <summary>
    /// A unit's stats going UP -- the green "+n" overlay, attack or health. Heroes included: a hero IS
    /// a minion as far as the board is concerned (HeroController derives from MinionController and uses
    /// the same view), so a buff on one sounds like a buff on the other, exactly as the MinionHit*
    /// sounds already cover both.
    ///
    /// Cued to the overlay appearing, not to the stat mutating -- the flash is deferred, and the sound
    /// has to land with what the player sees.
    /// </summary>
    MinionBuff = 31,

    /// <summary>
    /// The mirror of <see cref="MinionBuff"/>: stats going DOWN, the red "-n" attack overlay. Heroes
    /// included, same reasoning. Losing HEALTH is not this -- that is damage, and it already has the
    /// MinionHit* family; this is the debuff that leaves a unit weaker rather than hurt.
    /// </summary>
    MinionDebuff = 32,

    // Heroes.
    //
    // Reserved rather than used: heroes take damage through the same MinionHit* sounds their minions do,
    // so there is nothing mapped to HeroHit. The ids stay put so mapping one later is an edit to the
    // library asset alone.
    HeroHit = 40,
    HeroDeath = 41,

    // Turn flow
    TurnStart = 60,

    /// <summary>
    /// The end-turn switch straining under a press, before the hold has been held long enough. On its
    /// own — released early — this IS the "that did not take" sound.
    /// </summary>
    TurnEndHoldStart = 65,

    /// <summary>The switch snapping over and the turn actually changing hands.</summary>
    TurnEnd = 61,

    /// <summary>
    /// The opponent handing the turn back — cued to the switch animation throwing to the player. The
    /// counterpart to <see cref="TurnEnd"/>, which is the player's own throw.
    /// </summary>
    OpponentTurnEnd = 66,

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
    MusicMatch = 121,

    // ---------------------------------------------------------------------------------------------
    // Ambience.
    //
    // The one family nothing in the game fires. Every other id here is a REACTION -- something the
    // player or the board did -- and is played by GameAudioBinder off the event that caused it. These
    // have no cause: the room is making them whether or not anything is happening, which is exactly
    // what makes a silent turn feel like a place rather than a paused screen. AmbienceDirector owns
    // them, and it is the only thing that plays them.
    //
    // Split by SCENE rather than by what the sound is, mirroring MusicMenu/MusicMatch: the menu and the
    // arena are two different rooms, and the director swaps between them on a scene load.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Room tone for the menu, and for anything that is not the match (the custom-game screen included).
    /// Meant for ONE long recording rather than a variation set -- the director never restarts it, so a
    /// second clip here would only ever be heard if the first were stopped.
    /// </summary>
    AmbienceMenu = 140,

    /// <summary>Room tone for the match. Same one-clip rule as <see cref="AmbienceMenu"/>.</summary>
    AmbienceMatch = 141,

    /// <summary>
    /// The extra layers of the menu's room tone, playing ON TOP of <see cref="AmbienceMenu"/> rather than
    /// instead of it -- every layer sounds at once, each on its own dedicated source, so each can be
    /// levelled and swapped on its own asset. Same one-clip rule as the first layer, and a layer with
    /// nothing mapped is simply silent.
    /// </summary>
    AmbienceMenu2 = 142,
    AmbienceMenu3 = 143,

    /// <summary>The match's extra layers. Same rules as <see cref="AmbienceMenu2"/>.</summary>
    AmbienceMatch2 = 144,
    AmbienceMatch3 = 145,

    AmbienceMenu4 = 146,
    AmbienceMatch4 = 147
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

    [Header("Output")]
    [Tooltip("Multiplies every sound on every bus, on top of the player's volume settings and each sound's own level. " +
             "For making the whole game louder without touching either. Unity caps AudioSource.volume at 1, so a " +
             "sound already at full volume with the sliders high can't get louder -- the gain lifts the quiet ones.")]
    [Range(1f, 8f)]
    public float outputGain = 1f;

    [Header("Automatic UI sounds")]
    [Tooltip("Give every Button, Toggle and Slider -- scene UI and UI spawned at runtime alike -- hover/click sounds automatically, without a component on each one. See UISoundBinder.")]
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
    private void OnValidate()
    {
        _byId = null;

        // So outputGain can be tuned by ear during play: the looping beds are already sounding and would
        // otherwise keep their old level until a slider moved. One-shots pick it up on their next play.
        if (Application.isPlaying && AudioManager.Exists) AudioManager.Instance.RefreshBedVolumes();
    }

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
