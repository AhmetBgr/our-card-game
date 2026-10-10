using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A buff the enemy of a Forged in Battle battle may be rolled with. Later battles roll more of them
/// (<see cref="GauntletConfigSO.Encounter.enemyBlessings"/>); <see cref="GauntletRun"/> applies them.
/// </summary>
public enum GauntletBlessing
{
    /// <summary>The first few minions the enemy summons get +1/+1.</summary>
    StrongerMinions,
    /// <summary>The enemy's deck is made of upgraded cards, on a balanced curve, duplicates allowed.</summary>
    UpgradedDeck,
    /// <summary>A random extra passive. The only blessing that can be rolled more than once.</summary>
    ExtraPassive,
    /// <summary>Extra health on the enemy hero.</summary>
    BonusHealth,
    /// <summary>The enemy starts with a random minion already on its spawn row.</summary>
    StartingMinion,
    /// <summary>Extra cards in the enemy's starting hand.</summary>
    BiggerHand,
    /// <summary>The enemy's mana ramp starts higher.</summary>
    StartingMana,
    /// <summary>One of the enemy hero's own passives is swapped for its forged version.</summary>
    ForgedPassive,
}

/// <summary>
/// Tuning for the Forged in Battle mode (the draft gauntlet): the player picks a hero and drafts a small
/// deck out of base cards, then fights a fixed sequence of AI opponents, drafting more cards after each
/// win. Loaded from <see cref="ResourcePath"/>, like the other Resources-backed configs.
/// </summary>
[CreateAssetMenu(fileName = "GauntletConfig", menuName = "Game Modes/Gauntlet Config")]
public class GauntletConfigSO : ScriptableObject
{
    public const string ResourcePath = "Gauntlet/GauntletConfig";

    [Serializable]
    public class Encounter
    {
        [Tooltip("Shown on the draft screen before this battle.")]
        public string label = "Battle";

        [Tooltip("Multiplier on the enemy hero's printed health (and max health). 1 = full health.")]
        [Min(0.01f)] public float enemyHealthMultiplier = 1f;

        [Tooltip("Cards in the enemy's randomly generated base-card deck.")]
        [Min(1)] public int enemyDeckSize = 10;

        [Tooltip("Random blessings (enemy buffs) rolled for this battle, from the blessing pool.")]
        [Min(0)] public int enemyBlessings = 0;
    }

    [Header("Draft")]
    [Tooltip("Heroes offered at the start of a run; the player keeps one.")]
    [Min(1)] public int heroChoiceCount = 3;

    [Tooltip("Cards drafted before the first battle.")]
    [Min(1)] public int initialDeckSize = 5;

    [Tooltip("Highest mana cost offered in the opening draft (before the first battle). Picks after a win " +
             "are not capped. 0 = no cap.")]
    [Min(0)] public int initialDraftMaxCost = 4;

    [Tooltip("Cards offered per pick; the player keeps one.")]
    [Min(1)] public int cardChoiceCount = 3;

    [Tooltip("When on, a card already in the deck can be offered (and picked) again.")]
    public bool allowDuplicates = true;

    [Tooltip("When on, the options of a single pick are all different cards.")]
    public bool distinctOptionsPerPick = true;

    [Header("Between battles")]
    [Tooltip("Cards added to the deck after each won battle (one pick each).")]
    [Min(0)] public int cardsAddedAfterWin = 2;

    [Tooltip("Flat health restored to the player's hero after a won battle. 0 = no healing. Never above max health.")]
    [Min(0)] public int healAfterWin = 0;

    [Tooltip("Health restored after a won battle as a fraction of max health, added on top of the flat heal.")]
    [Range(0f, 1f)] public float healAfterWinPercent = 0f;

    [Tooltip("Carry the hero's attack (buffs included) into the next battle. Off = back to printed attack.")]
    public bool keepHeroAttack = true;

    [Tooltip("Carry passive state (granted bonuses, passive counters) into the next battle.")]
    public bool keepPassiveState = true;

    [Header("Enemies")]
    [Tooltip("Fought in order. Clearing the last one wins the run.")]
    public List<Encounter> encounters = new List<Encounter>
    {
        new Encounter { label = "Battle 1", enemyHealthMultiplier = 0.33f, enemyDeckSize = 10, enemyBlessings = 0 },
        new Encounter { label = "Battle 2", enemyHealthMultiplier = 0.5f, enemyDeckSize = 10, enemyBlessings = 1 },
        new Encounter { label = "Battle 3", enemyHealthMultiplier = 0.75f, enemyDeckSize = 10, enemyBlessings = 2 },
        new Encounter { label = "Final Battle", enemyHealthMultiplier = 1f, enemyDeckSize = 10, enemyBlessings = 3 },
    };

    [Tooltip("When on, an enemy never uses the same hero the player picked (if another hero exists).")]
    public bool avoidMirrorHero = false;

    [Header("Blessings")]
    [Tooltip("Blessings an encounter can roll. Each is rolled at most once per battle, except Extra Passive, " +
             "which can stack up to extraPassiveMaxStacks times.")]
    public List<GauntletBlessing> blessingPool = new List<GauntletBlessing>
    {
        GauntletBlessing.StrongerMinions,
        GauntletBlessing.UpgradedDeck,
        GauntletBlessing.ExtraPassive,
        GauntletBlessing.BonusHealth,
        GauntletBlessing.StartingMinion,
        GauntletBlessing.BiggerHand,
        GauntletBlessing.StartingMana,
        GauntletBlessing.ForgedPassive,
    };

    [Tooltip("How many times Extra Passive can be rolled for one battle.")]
    [Range(1, MatchModifiers.MaxExtraPassives)] public int extraPassiveMaxStacks = 2;

    [Tooltip("Stronger Minions: how many of the enemy's first summoned minions are buffed.")]
    [Min(1)] public int strongerMinionsCount = 3;

    [Tooltip("Stronger Minions: attack and health added to each of them.")]
    [Min(1)] public int strongerMinionsBonus = 1;

    [Tooltip("Upgraded Deck: cards in the enemy's upgraded deck (replaces the encounter's deck size).")]
    [Min(1)] public int upgradedDeckSize = 15;

    [Tooltip("Bonus Health: added to the enemy hero's health after the encounter's multiplier.")]
    [Min(1)] public int bonusHealth = 5;

    [Tooltip("Starting Minion: mana cost of the random base minion the enemy starts with on the board.")]
    [Min(0)] public int startingMinionCost = 3;

    [Tooltip("Bigger Hand: extra cards in the enemy's starting hand.")]
    [Min(1)] public int extraStartingCards = 2;

    [Tooltip("Starting Mana: max mana the enemy has on its first turn.")]
    [Min(1)] public int blessedStartingMana = 3;

    [Header("Scenes")]
    public string draftSceneName = "Draft";
    public string gameSceneName = "Game";
    public string menuSceneName = "MainMenu";

    private static GauntletConfigSO _cached;

    /// <summary>The config asset, or a default-valued instance if the asset is missing.</summary>
    public static GauntletConfigSO Load()
    {
        if (_cached != null) return _cached;

        _cached = Resources.Load<GauntletConfigSO>(ResourcePath);
        if (_cached == null)
        {
            Debug.LogWarning($"[Gauntlet] No config at Resources/{ResourcePath}; using defaults.");
            _cached = CreateInstance<GauntletConfigSO>();
        }
        return _cached;
    }
}
