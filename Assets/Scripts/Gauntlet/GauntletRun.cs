using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// State of the Forged in Battle run in progress: the hero and deck the player drafted, which battle is
/// next, and the hero's statline carried out of the last won battle. Static so it crosses the scene
/// loads between the Draft and Game scenes, like <see cref="MatchModifiers"/>. It lives in memory only:
/// quitting the game abandons the run.
///
/// Gameplay asks it three things, all no-ops while no run is active: which hero and deck each side
/// plays with (<see cref="HeroFor"/>, <see cref="DeckFor"/>, read in Agent.ApplySavedSelection), how to
/// adjust the heroes at match setup (<see cref="ApplyEnemySetup"/>, <see cref="RestorePlayerHero"/>),
/// and what to do with the result (<see cref="OnBattleWon"/>, <see cref="OnBattleLost"/>).
/// </summary>
public static class GauntletRun
{
    public enum RunOutcome { InProgress, Won, Lost }

    /// <summary>The player hero's statline and passive state at the end of the last won battle.</summary>
    private class HeroSnapshot
    {
        public int health;
        public int maxHealth;
        public int attack;
        public int appliedAttackBonus;
        public readonly Dictionary<string, int> counters = new Dictionary<string, int>();
    }

    public static bool IsActive { get; private set; }
    public static GauntletConfigSO Config { get; private set; }

    public static HeroSO Hero { get; private set; }
    private static readonly List<CardSO> _deck = new List<CardSO>();
    public static IReadOnlyList<CardSO> Deck => _deck;

    /// <summary>Index into Config.encounters of the battle to be fought next.</summary>
    public static int EncounterIndex { get; private set; }

    /// <summary>Card picks still owed before the next battle can start.</summary>
    public static int PendingPicks { get; private set; }

    /// <summary>Picks owed in the current draft step (5 at the start, 2 after a win), for "pick 2 of 5".</summary>
    public static int PicksThisStep { get; private set; }

    public static RunOutcome Outcome { get; private set; }

    private static HeroSnapshot _snapshot;

    private static HeroSO _enemyHero;
    private static readonly List<CardSO> _enemyDeck = new List<CardSO>();

    public static int EncounterCount => Config != null ? Config.encounters.Count : 0;

    public static GauntletConfigSO.Encounter CurrentEncounter =>
        Config != null && EncounterIndex >= 0 && EncounterIndex < Config.encounters.Count
            ? Config.encounters[EncounterIndex]
            : null;

    // Statics survive between play sessions when domain reload is off; start each session clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession() => Reset();

    private static void Reset()
    {
        IsActive = false;
        Config = null;
        Hero = null;
        _deck.Clear();
        EncounterIndex = 0;
        PendingPicks = 0;
        PicksThisStep = 0;
        Outcome = RunOutcome.InProgress;
        _snapshot = null;
        _enemyHero = null;
        _enemyDeck.Clear();
    }

    /// <summary>Starts a fresh run: no hero yet, and the opening draft owed.</summary>
    public static void StartNew()
    {
        Reset();
        IsActive = true;
        Config = GauntletConfigSO.Load();
        PendingPicks = PicksThisStep = Config.initialDeckSize;
    }

    /// <summary>Abandons the run. Called when the player is back on the main menu.</summary>
    public static void End() => Reset();

    // ---------------------------------------------------------------- Draft

    public static void SetHero(HeroSO hero)
    {
        if (!IsActive || hero == null) return;
        Hero = hero;
    }

    public static void AddDraftedCard(CardSO card)
    {
        if (!IsActive || card == null || PendingPicks <= 0) return;
        _deck.Add(card);
        PendingPicks--;
    }

    /// <summary>Heroes offered for the opening pick: distinct, random, never tutorial heroes.</summary>
    public static List<HeroSO> RollHeroOptions()
    {
        var pool = new List<HeroSO>(HeroDatabase.Instance.AllHeroes);
        pool.RemoveAll(h => h == null);
        return TakeRandom(pool, Config.heroChoiceCount, distinct: true);
    }

    /// <summary>
    /// Cards offered for one draft pick, from the base (non-upgraded) cards only. The opening draft is
    /// capped at <see cref="GauntletConfigSO.initialDraftMaxCost"/>; picks after a win are not.
    /// </summary>
    public static List<CardSO> RollCardOptions()
    {
        var pool = BaseCardPool();
        if (!Config.allowDuplicates) pool.RemoveAll(c => _deck.Contains(c));

        if (EncounterIndex == 0 && Config.initialDraftMaxCost > 0)
            pool.RemoveAll(c => c.cost > Config.initialDraftMaxCost);

        return TakeRandom(pool, Config.cardChoiceCount, Config.distinctOptionsPerPick);
    }

    // ---------------------------------------------------------------- Battles

    /// <summary>
    /// Rolls the enemy for the next battle (random hero, random base-card deck). Call right before
    /// loading the Game scene.
    /// </summary>
    public static void PrepareNextBattle()
    {
        var encounter = CurrentEncounter;
        if (!IsActive || encounter == null) return;

        var heroes = new List<HeroSO>(HeroDatabase.Instance.AllHeroes);
        heroes.RemoveAll(h => h == null);
        if (Config.avoidMirrorHero && heroes.Count > 1) heroes.Remove(Hero);
        _enemyHero = heroes.Count > 0 ? heroes[Random.Range(0, heroes.Count)] : null;

        _enemyDeck.Clear();
        _enemyDeck.AddRange(GenerateEnemyDeck(encounter.enemyDeckSize));
    }

    /// <summary>The hero a side plays with this battle, or null to leave the saved selection alone.</summary>
    public static HeroSO HeroFor(SelectionSide side)
    {
        if (!IsActive) return null;
        return side == SelectionSide.Opponent ? _enemyHero : Hero;
    }

    /// <summary>
    /// The deck a side plays with this battle: always the drafted base cards for the player, since
    /// upgrades earned during a battle are not kept.
    /// </summary>
    public static IReadOnlyList<CardSO> DeckFor(SelectionSide side)
    {
        if (!IsActive) return System.Array.Empty<CardSO>();
        return side == SelectionSide.Opponent ? _enemyDeck : _deck;
    }

    /// <summary>Scales the enemy hero's health by the encounter's multiplier. Runs before passives register.</summary>
    public static void ApplyEnemySetup(MinionController hero)
    {
        var encounter = CurrentEncounter;
        if (!IsActive || encounter == null || hero == null || hero.modal == null) return;

        int hp = Mathf.Max(1, Mathf.RoundToInt(hero.modal.defHealth * encounter.enemyHealthMultiplier));
        hero.modal.defHealth = hp;
        hero.modal.health = hp;

        if (hero.view != null) hero.view.UpdateViewWithoutStatFlash(hero.modal);
    }

    /// <summary>
    /// Puts the statline and passive state carried from the last won battle back onto the player's
    /// hero. Runs AFTER passives register, so it overwrites their one-time self-stat stamp (the
    /// Summoner's -2 Attack) with the carried value, which already has the stamp in it, instead of
    /// stacking it a second time.
    /// </summary>
    public static void RestorePlayerHero(MinionController hero)
    {
        if (!IsActive || _snapshot == null || hero == null || hero.modal == null) return;

        hero.modal.defHealth = _snapshot.maxHealth;
        hero.modal.health = Mathf.Clamp(_snapshot.health, 1, _snapshot.maxHealth);
        if (Config.keepHeroAttack) hero.modal.attack = _snapshot.attack;

        var runtime = HeroRuntime.For(hero);
        if (runtime != null && Config.keepPassiveState)
        {
            // The Berserker's granted bonus only travels with the attack it was granted into; with the
            // attack reset, the passive must be free to grant it again.
            if (Config.keepHeroAttack) runtime.appliedAttackBonus = _snapshot.appliedAttackBonus;
            foreach (var pair in _snapshot.counters) runtime.SetCounter(pair.Key, pair.Value);
        }

        if (hero.view != null) hero.view.UpdateViewWithoutStatFlash(hero.modal);
        HeroPassiveIndicatorView.For(hero)?.Refresh();
    }

    /// <summary>Records the player's hero as it ended the battle and advances the run.</summary>
    public static void OnBattleWon(MinionController playerHero)
    {
        if (!IsActive || Outcome != RunOutcome.InProgress) return;

        Capture(playerHero);
        EncounterIndex++;

        if (EncounterIndex >= Config.encounters.Count)
        {
            Outcome = RunOutcome.Won;
            PendingPicks = PicksThisStep = 0;
            return;
        }

        PendingPicks = PicksThisStep = Config.cardsAddedAfterWin;
        HealSnapshot();
    }

    public static void OnBattleLost()
    {
        if (!IsActive || Outcome != RunOutcome.InProgress) return;
        Outcome = RunOutcome.Lost;
    }

    /// <summary>
    /// Leaves a finished battle for the Draft scene, which shows the next draft step or the run's
    /// result. What the end-of-match Proceed button does during a run.
    /// </summary>
    public static void LeaveBattle() => LoadScene(Config != null ? Config.draftSceneName : "Draft");

    public static void LoadScene(string sceneName)
    {
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.TransitionToScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    /// <summary>Current health / max health / attack the player's hero will start the next battle with.</summary>
    public static bool TryGetCarriedStats(out int health, out int maxHealth, out int attack)
    {
        if (_snapshot != null)
        {
            health = _snapshot.health;
            maxHealth = _snapshot.maxHealth;
            attack = Config.keepHeroAttack || Hero == null ? _snapshot.attack : Hero.attack;
            return true;
        }

        health = maxHealth = attack = 0;
        if (Hero == null) return false;

        health = Hero.health;
        maxHealth = Hero.defHealth > 0 ? Hero.defHealth : Hero.health;
        attack = Hero.attack;
        return true;
    }

    // ---------------------------------------------------------------- Internals

    private static void Capture(MinionController hero)
    {
        if (hero == null || hero.modal == null) return;

        var snapshot = new HeroSnapshot
        {
            maxHealth = Mathf.Max(1, hero.modal.defHealth),
            health = hero.modal.health,
            attack = hero.modal.attack,
        };

        var runtime = HeroRuntime.For(hero);
        if (runtime != null)
        {
            snapshot.appliedAttackBonus = runtime.appliedAttackBonus;
            foreach (var pair in runtime.Counters) snapshot.counters[pair.Key] = pair.Value;
        }

        _snapshot = snapshot;
    }

    private static void HealSnapshot()
    {
        if (_snapshot == null) return;

        int heal = Config.healAfterWin + Mathf.RoundToInt(_snapshot.maxHealth * Config.healAfterWinPercent);
        _snapshot.health = Mathf.Clamp(_snapshot.health + heal, 1, _snapshot.maxHealth);
    }

    private static List<CardSO> BaseCardPool()
    {
        var all = DeckDatabase.Instance != null
            ? DeckDatabase.Instance.AllCards
            : new List<CardSO>(Resources.LoadAll<CardSO>("Cards"));

        var pool = new List<CardSO>(all.Count);
        foreach (var card in all)
            if (card != null && !card.isUpgraded && !(card is HeroSO)) pool.Add(card);
        return pool;
    }

    /// <summary>
    /// A random base-card deck of <paramref name="size"/>, shaped like SaveManager.GenerateRandomDeck:
    /// <see cref="DeckSO.ManaCurve"/> scaled to the size, then padded at random. No duplicates until
    /// the pool runs out.
    /// </summary>
    private static List<CardSO> GenerateEnemyDeck(int size)
    {
        var pool = BaseCardPool();
        Shuffle(pool);

        var deck = new List<CardSO>(size);
        float scale = size / 10f; // ManaCurve is authored for a 10-card deck

        foreach (var tier in DeckSO.ManaCurve)
        {
            int want = Mathf.RoundToInt(tier.count * scale);
            for (int i = pool.Count - 1; i >= 0 && want > 0 && deck.Count < size; i--)
            {
                if (System.Array.IndexOf(tier.costs, pool[i].cost) < 0) continue;

                deck.Add(pool[i]);
                pool.RemoveAt(i);
                want--;
            }
        }

        var all = BaseCardPool();
        while (deck.Count < size && all.Count > 0)
        {
            if (pool.Count > 0)
            {
                int index = Random.Range(0, pool.Count);
                deck.Add(pool[index]);
                pool.RemoveAt(index);
            }
            else
            {
                deck.Add(all[Random.Range(0, all.Count)]);
            }
        }

        return deck;
    }

    private static List<T> TakeRandom<T>(List<T> pool, int count, bool distinct)
    {
        var result = new List<T>(count);
        if (pool.Count == 0) return result;

        if (distinct)
        {
            var copy = new List<T>(pool);
            Shuffle(copy);
            for (int i = 0; i < count && i < copy.Count; i++) result.Add(copy[i]);
        }
        else
        {
            for (int i = 0; i < count; i++) result.Add(pool[Random.Range(0, pool.Count)]);
        }
        return result;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
