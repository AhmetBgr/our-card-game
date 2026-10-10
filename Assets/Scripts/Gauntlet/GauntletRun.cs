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
///
/// Later enemies are rolled with blessings (<see cref="GauntletBlessing"/>), which GameManager reads
/// through the same kind of no-op-outside-a-run hooks: <see cref="OwnPassivesFor"/>,
/// <see cref="ExtraPassivesFor"/>, <see cref="StartingManaFor"/>, <see cref="ExtraStartingCardsFor"/>
/// and <see cref="SummonStartingMinion"/>.
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
    private static readonly List<GauntletBlessing> _enemyBlessings = new List<GauntletBlessing>();
    private static readonly List<HeroPassiveSO> _enemyOwnPassives = new List<HeroPassiveSO>();
    private static readonly List<HeroPassiveSO> _enemyExtraPassives = new List<HeroPassiveSO>();
    private static CardSO _enemyStartingMinion;

    /// <summary>Stronger Minions: enemy summons still owed the buff this battle.</summary>
    private static int _strongerMinionsLeft;

    /// <summary>
    /// A mana curve for a 15-card upgraded deck. Upgraded cards cost 2 to 7, so
    /// <see cref="DeckSO.ManaCurve"/> (0 to 5) doesn't fit them.
    /// </summary>
    private static readonly (int[] costs, int count)[] UpgradedManaCurve =
    {
        (new[] { 2, 3 }, 4),
        (new[] { 4 }, 4),
        (new[] { 5 }, 4),
        (new[] { 6, 7 }, 3),
    };
    private const int UpgradedManaCurveSize = 15;

    /// <summary>The enemy rolled for the next battle, or null until <see cref="PrepareNextBattle"/> runs.</summary>
    public static HeroSO EnemyHero => _enemyHero;

    /// <summary>The blessings rolled for the next battle's enemy, in roll order. Empty until it is rolled.</summary>
    public static IReadOnlyList<GauntletBlessing> EnemyBlessings => _enemyBlessings;

    /// <summary>Every passive the next enemy plays with: its own (one maybe forged), then the extras.</summary>
    public static List<HeroPassiveSO> EnemyPassives
    {
        get
        {
            var all = new List<HeroPassiveSO>(_enemyOwnPassives);
            all.AddRange(_enemyExtraPassives);
            return all;
        }
    }

    /// <summary>The minion the next enemy starts with on the board (Starting Minion), or null.</summary>
    public static CardSO EnemyStartingMinion => _enemyStartingMinion;

    /// <summary>Enemy hero health after the encounter's multiplier and Bonus Health.</summary>
    public static int EnemyStartingHealth(int printedHealth)
    {
        var encounter = CurrentEncounter;
        int hp = encounter != null
            ? Mathf.Max(1, Mathf.RoundToInt(printedHealth * encounter.enemyHealthMultiplier))
            : printedHealth;
        if (HasEnemyBlessing(GauntletBlessing.BonusHealth)) hp += Config.bonusHealth;
        return hp;
    }

    public static bool HasEnemyBlessing(GauntletBlessing blessing) => _enemyBlessings.Contains(blessing);

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
        ClearEnemy();
    }

    private static void ClearEnemy()
    {
        _enemyHero = null;
        _enemyDeck.Clear();
        _enemyBlessings.Clear();
        _enemyOwnPassives.Clear();
        _enemyExtraPassives.Clear();
        _enemyStartingMinion = null;
        StopStrongerMinions();
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
    /// Rolls the enemy for the next battle: random hero, its blessings, then the deck, passives and
    /// starting minion those blessings call for. The Draft scene calls it once the player has a hero, so
    /// the upcoming enemy can be previewed before the fight.
    /// </summary>
    public static void PrepareNextBattle()
    {
        var encounter = CurrentEncounter;
        if (!IsActive || encounter == null) return;

        ClearEnemy();

        var heroes = new List<HeroSO>(HeroDatabase.Instance.AllHeroes);
        heroes.RemoveAll(h => h == null);
        if (Config.avoidMirrorHero && heroes.Count > 1) heroes.Remove(Hero);
        _enemyHero = heroes.Count > 0 ? heroes[Random.Range(0, heroes.Count)] : null;

        if (_enemyHero != null)
            foreach (var passive in _enemyHero.passives)
                if (passive != null && !_enemyOwnPassives.Contains(passive)) _enemyOwnPassives.Add(passive);

        _enemyBlessings.AddRange(RollBlessings(encounter.enemyBlessings));

        if (HasEnemyBlessing(GauntletBlessing.ForgedPassive)) ForgeRandomOwnPassive();

        int extraPassives = _enemyBlessings.FindAll(b => b == GauntletBlessing.ExtraPassive).Count;
        if (extraPassives > 0) _enemyExtraPassives.AddRange(RollExtraPassives(extraPassives));

        if (HasEnemyBlessing(GauntletBlessing.StartingMinion))
        {
            var minions = BaseCardPool().FindAll(c => c.cost == Config.startingMinionCost && c.health > 0);
            _enemyStartingMinion = minions.Count > 0 ? minions[Random.Range(0, minions.Count)] : null;
        }

        _enemyDeck.AddRange(HasEnemyBlessing(GauntletBlessing.UpgradedDeck)
            ? GenerateEnemyDeck(Config.upgradedDeckSize, UpgradedCardPool(), UpgradedManaCurve, UpgradedManaCurveSize, allowDuplicates: true)
            : GenerateEnemyDeck(encounter.enemyDeckSize, BaseCardPool(), DeckSO.ManaCurve, 10, allowDuplicates: false));
    }

    // ---------------------------------------------------------------- Blessings

    /// <summary>
    /// <paramref name="count"/> distinct blessings from the pool, except that Extra Passive can come up
    /// to <see cref="GauntletConfigSO.extraPassiveMaxStacks"/> times. Blessings that would do nothing on
    /// the rolled enemy (Forged Passive on a hero with nothing to forge) are left out.
    /// </summary>
    private static List<GauntletBlessing> RollBlessings(int count)
    {
        var bag = new List<GauntletBlessing>();
        foreach (var blessing in Config.blessingPool)
        {
            if (bag.Contains(blessing) || !CanBless(blessing)) continue;

            int copies = blessing == GauntletBlessing.ExtraPassive ? Config.extraPassiveMaxStacks : 1;
            for (int i = 0; i < copies; i++) bag.Add(blessing);
        }

        return TakeRandom(bag, count, distinct: true);
    }

    private static bool CanBless(GauntletBlessing blessing)
    {
        switch (blessing)
        {
            case GauntletBlessing.ForgedPassive:
                return _enemyOwnPassives.Exists(p => p.forgedVersion != null);
            case GauntletBlessing.ExtraPassive:
                return ExtraPassivePool().Count > 0;
            case GauntletBlessing.StartingMinion:
                return BaseCardPool().Exists(c => c.cost == Config.startingMinionCost && c.health > 0);
            default:
                return true;
        }
    }

    private static void ForgeRandomOwnPassive()
    {
        var forgeable = new List<int>();
        for (int i = 0; i < _enemyOwnPassives.Count; i++)
            if (_enemyOwnPassives[i].forgedVersion != null) forgeable.Add(i);
        if (forgeable.Count == 0) return;

        int index = forgeable[Random.Range(0, forgeable.Count)];
        _enemyOwnPassives[index] = _enemyOwnPassives[index].forgedVersion;
    }

    /// <summary>Base passives the enemy hero doesn't already have, in either its base or forged form.</summary>
    private static List<HeroPassiveSO> ExtraPassivePool()
    {
        var pool = new List<HeroPassiveSO>();
        if (HeroDatabase.Instance == null) return pool;

        foreach (var passive in HeroDatabase.Instance.AllPassives)
        {
            if (passive == null || passive.isForged) continue;
            if (_enemyOwnPassives.Exists(own => own == passive || own == passive.forgedVersion)) continue;
            pool.Add(passive);
        }
        return pool;
    }

    private static List<HeroPassiveSO> RollExtraPassives(int count) =>
        TakeRandom(ExtraPassivePool(), Mathf.Min(count, MatchModifiers.MaxExtraPassives), distinct: true);

    /// <summary>
    /// The passives a side's hero plays with in place of its HeroSO's own list, or null to keep that
    /// list. Only the enemy differs: Forged Passive swaps one of them for its forged version.
    /// </summary>
    public static IReadOnlyList<HeroPassiveSO> OwnPassivesFor(SelectionSide side)
    {
        if (!IsActive || side != SelectionSide.Opponent || _enemyHero == null) return null;
        return _enemyOwnPassives;
    }

    /// <summary>The extra passives a side plays with this battle (Extra Passive), or null for none.</summary>
    public static IReadOnlyList<HeroPassiveSO> ExtraPassivesFor(SelectionSide side)
    {
        if (!IsActive || side != SelectionSide.Opponent || _enemyExtraPassives.Count == 0) return null;
        return _enemyExtraPassives;
    }

    /// <summary>A side's max mana on its first turn: <paramref name="standard"/>, or more with Starting Mana.</summary>
    public static int StartingManaFor(SelectionSide side, int standard)
    {
        if (!IsActive || side != SelectionSide.Opponent || !HasEnemyBlessing(GauntletBlessing.StartingMana)) return standard;
        return Mathf.Max(standard, Config.blessedStartingMana);
    }

    /// <summary>Cards a side draws on top of the normal starting hand (Bigger Hand).</summary>
    public static int ExtraStartingCardsFor(SelectionSide side)
    {
        if (!IsActive || side != SelectionSide.Opponent || !HasEnemyBlessing(GauntletBlessing.BiggerHand)) return 0;
        return Config.extraStartingCards;
    }

    /// <summary>
    /// Starting Minion: puts the rolled minion on a random free tile of the enemy's spawn row. Runs at
    /// match setup, after the heroes' passives register so their auras stamp it like any other summon.
    /// </summary>
    public static void SummonStartingMinion(Agent enemy)
    {
        if (!IsActive || enemy == null || _enemyStartingMinion == null || GameManager.Instance == null) return;

        // Same row convention as ActionHolder.SpawnRowOf: the player summons on row 2, the opponent on 0.
        int spawnRow = enemy == GameManager.Instance.player ? 2 : 0;
        var free = new List<Transform>();
        foreach (var cell in GridManager.Instance.GetGrid())
            if (cell.index.y == spawnRow && cell.cellObj != null && cell.obj == null)
                free.Add(cell.cellObj.transform);
        if (free.Count == 0) return;

        GameManager.Instance.SummonMinion(_enemyStartingMinion, free[Random.Range(0, free.Count)].position, enemy);
    }

    /// <summary>Player-facing name of a blessing.</summary>
    public static string BlessingName(GauntletBlessing blessing)
    {
        switch (blessing)
        {
            case GauntletBlessing.StrongerMinions: return "Stronger Minions";
            case GauntletBlessing.UpgradedDeck: return "Upgraded Deck";
            case GauntletBlessing.ExtraPassive: return "Extra Passive";
            case GauntletBlessing.BonusHealth: return "Bonus Health";
            case GauntletBlessing.StartingMinion: return "Vanguard";
            case GauntletBlessing.BiggerHand: return "Bigger Hand";
            case GauntletBlessing.StartingMana: return "Head Start";
            case GauntletBlessing.ForgedPassive: return "Forged Passive";
            default: return blessing.ToString();
        }
    }

    /// <summary>What a blessing does, with the config's numbers (and the rolled minion) filled in.</summary>
    public static string BlessingDescription(GauntletBlessing blessing)
    {
        var c = Config;
        switch (blessing)
        {
            case GauntletBlessing.StrongerMinions:
                return $"Its first {c.strongerMinionsCount} minions get +{c.strongerMinionsBonus}/+{c.strongerMinionsBonus}";
            case GauntletBlessing.UpgradedDeck:
                return $"Its deck is {c.upgradedDeckSize} upgraded cards";
            case GauntletBlessing.ExtraPassive:
                return "Its hero has a random extra passive";
            case GauntletBlessing.BonusHealth:
                return $"Its hero has +{c.bonusHealth} health";
            case GauntletBlessing.StartingMinion:
                return _enemyStartingMinion != null
                    ? $"It starts with {_enemyStartingMinion.cardName} on the board"
                    : $"It starts with a {c.startingMinionCost}-mana minion on the board";
            case GauntletBlessing.BiggerHand:
                return $"Its starting hand has {c.extraStartingCards} extra cards";
            case GauntletBlessing.StartingMana:
                return $"It starts at {c.blessedStartingMana} mana";
            case GauntletBlessing.ForgedPassive:
                return "Its hero's passive is forged";
            default:
                return string.Empty;
        }
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

    /// <summary>
    /// Scales the enemy hero's health by the encounter's multiplier (plus Bonus Health), and arms
    /// Stronger Minions for this battle. Runs before passives register.
    /// </summary>
    public static void ApplyEnemySetup(MinionController hero)
    {
        StopStrongerMinions();

        var encounter = CurrentEncounter;
        if (!IsActive || encounter == null || hero == null || hero.modal == null) return;

        int hp = EnemyStartingHealth(hero.modal.defHealth);
        hero.modal.defHealth = hp;
        hero.modal.health = hp;

        if (hero.view != null) hero.view.UpdateViewWithoutStatFlash(hero.modal);

        if (HasEnemyBlessing(GauntletBlessing.StrongerMinions))
        {
            _strongerMinionsLeft = Config.strongerMinionsCount;
            GameManager.OnMinionSummoned += BuffEnemySummon;
        }
    }

    private static void StopStrongerMinions()
    {
        _strongerMinionsLeft = 0;
        GameManager.OnMinionSummoned -= BuffEnemySummon;
    }

    /// <summary>Stronger Minions: +N/+N on each of the enemy's first summons this battle.</summary>
    private static void BuffEnemySummon(MinionController minion)
    {
        var game = GameManager.Instance;
        if (!IsActive || game == null || minion == null || minion.modal == null || minion.owner != game.opponent) return;

        int bonus = Config.strongerMinionsBonus;
        minion.modal.attack += bonus;
        minion.modal.health += bonus;
        minion.modal.defHealth += bonus;
        if (minion.view != null) minion.view.UpdateView(minion.modal);

        if (--_strongerMinionsLeft <= 0) StopStrongerMinions();
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

        // The next battle gets a freshly rolled enemy.
        ClearEnemy();

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
        StopStrongerMinions();
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

    private static List<CardSO> BaseCardPool() => CardPool(upgraded: false);

    private static List<CardSO> UpgradedCardPool() => CardPool(upgraded: true);

    private static List<CardSO> CardPool(bool upgraded)
    {
        var all = DeckDatabase.Instance != null
            ? DeckDatabase.Instance.AllCards
            : new List<CardSO>(Resources.LoadAll<CardSO>("Cards"));

        var pool = new List<CardSO>(all.Count);
        foreach (var card in all)
            if (card != null && card.isUpgraded == upgraded && !(card is HeroSO)) pool.Add(card);
        return pool;
    }

    /// <summary>
    /// A random deck of <paramref name="size"/> out of <paramref name="all"/>, shaped like
    /// SaveManager.GenerateRandomDeck: <paramref name="curve"/> (authored for a
    /// <paramref name="curveSize"/>-card deck) scaled to the size, then padded at random. Without
    /// <paramref name="allowDuplicates"/>, a card repeats only once the pool runs out.
    /// </summary>
    private static List<CardSO> GenerateEnemyDeck(int size, List<CardSO> all, (int[] costs, int count)[] curve,
        int curveSize, bool allowDuplicates)
    {
        var pool = new List<CardSO>(all);
        var deck = new List<CardSO>(size);
        float scale = size / (float)curveSize;

        foreach (var tier in curve)
        {
            int want = Mathf.RoundToInt(tier.count * scale);
            var tierCards = pool.FindAll(c => System.Array.IndexOf(tier.costs, c.cost) >= 0);
            while (want > 0 && deck.Count < size && tierCards.Count > 0)
            {
                int index = Random.Range(0, tierCards.Count);
                CardSO card = tierCards[index];
                deck.Add(card);
                want--;

                if (allowDuplicates) continue;
                tierCards.RemoveAt(index);
                pool.Remove(card);
            }
        }

        while (deck.Count < size && all.Count > 0)
        {
            if (!allowDuplicates && pool.Count > 0)
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
