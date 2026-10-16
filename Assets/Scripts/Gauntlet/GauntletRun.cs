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
///
/// Each won battle's end-of-match score is added to the run's <see cref="Xp"/>, which reaches the
/// milestones of the progression track (<see cref="Milestones"/>): one every
/// <see cref="GauntletConfigSO.milestoneXpStep"/> XP, then a final one at
/// <see cref="GauntletConfigSO.finalMilestoneXp"/> rolled from its own pool. The track unlocks after the first win; reached
/// milestones are claimed with <see cref="ClaimMilestone"/>, and must all be claimed before the next
/// battle.
///
/// Winning a battle is itself worth new cards (<see cref="PendingBattleReward"/>), picked on the rewards
/// panel over the end-of-match screen rather than on the track.
/// </summary>
public static class GauntletRun
{
    public enum RunOutcome { InProgress, Won, Lost }

    /// <summary>One milestone of this run's track, with its Random reward already rolled.</summary>
    public class MilestoneState
    {
        public int Xp { get; }
        public MilestoneReward Reward { get; }
        public bool Claimed { get; internal set; }

        /// <summary>The first pick of a card reward, rolled once reached so a preview and the claim agree.</summary>
        internal List<CardSO> cardOptions;
        internal List<HeroPassiveSO> perkOptions;

        public MilestoneState(int xp, MilestoneReward reward)
        {
            Xp = xp;
            Reward = reward;
        }

        public bool Reached => GauntletRun.Xp >= Xp;
    }

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

    /// <summary>Picks owed in the current draft step (5 at the start, 2 for a win or a card milestone), for "pick 2 of 5".</summary>
    public static int PicksThisStep { get; private set; }

    /// <summary>The card picks owed come from an Upgraded Cards milestone.</summary>
    public static bool PickingUpgraded { get; private set; }

    /// <summary>The card pick owed comes from a Duplicate milestone: its options are cards of the deck.</summary>
    public static bool PickingDuplicate { get; private set; }

    /// <summary>A New Perk milestone's pick is owed.</summary>
    public static bool PendingPerkPick { get; private set; }

    /// <summary>
    /// The cards won by the battle just fought are still to be picked. Set by <see cref="OnBattleWon"/>
    /// together with the picks they owe (<see cref="PendingPicks"/>), and handed out by the rewards panel
    /// over the end-of-match screen (BattleRewardsPanel) before it leaves for the Draft scene.
    /// </summary>
    public static bool PendingBattleReward { get; private set; }

    /// <summary>Index of the milestone whose picks are owed, or -1.</summary>
    public static int ActiveMilestone { get; private set; } = -1;

    /// <summary>The options of the pick owed, rolled once so leaving and re-entering the scene can't reroll them.</summary>
    private static List<CardSO> _cardOptions;
    private static List<HeroPassiveSO> _perkOptions;

    public static RunOutcome Outcome { get; private set; }

    /// <summary>The summed end-of-match scores of the battles won so far.</summary>
    public static int Xp { get; private set; }

    /// <summary>The XP the Draft scene's track last showed, so it can animate what was gained since.</summary>
    public static int XpShown { get; set; }

    /// <summary>The track's lock has slid away once already: later visits show it unlocked straight away.</summary>
    public static bool MilestonesRevealed { get; set; }

    /// <summary>What the last instantly applied milestone did ("+5 Max Health"), or null.</summary>
    public static string LastRewardMessage { get; private set; }

    private static readonly List<MilestoneState> _milestones = new List<MilestoneState>();
    public static IReadOnlyList<MilestoneState> Milestones => _milestones;

    /// <summary>The player hero's own passives, one maybe forged by a milestone, and the perks it drafted.</summary>
    private static readonly List<HeroPassiveSO> _playerOwnPassives = new List<HeroPassiveSO>();
    private static readonly List<HeroPassiveSO> _playerExtraPassives = new List<HeroPassiveSO>();

    /// <summary>Attack from Bonus Attack milestones, re-added when the hero's attack isn't carried.</summary>
    private static int _rewardAttack;

    /// <summary>Cards Bigger Hand milestones add to the player's starting hand.</summary>
    private static int _playerExtraStartingCards;

    /// <summary>A battle was won and its score hasn't been added to the XP yet.</summary>
    private static bool _scorePending;

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
        PickingUpgraded = false;
        PickingDuplicate = false;
        PendingPerkPick = false;
        PendingBattleReward = false;
        ActiveMilestone = -1;
        _cardOptions = null;
        _perkOptions = null;
        Outcome = RunOutcome.InProgress;
        Xp = 0;
        XpShown = 0;
        MilestonesRevealed = false;
        LastRewardMessage = null;
        _milestones.Clear();
        _playerOwnPassives.Clear();
        _playerExtraPassives.Clear();
        _rewardAttack = 0;
        _playerExtraStartingCards = 0;
        _scorePending = false;
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

        _playerOwnPassives.Clear();
        _playerExtraPassives.Clear();
        foreach (var passive in hero.passives)
            if (passive != null && !_playerOwnPassives.Contains(passive)) _playerOwnPassives.Add(passive);

        // Rolled now rather than at the start of the run: whether Forge Perk can apply depends on the hero.
        RollMilestones();
    }

    public static void AddDraftedCard(CardSO card)
    {
        if (!IsActive || card == null || PendingPicks <= 0) return;
        _deck.Add(card);
        _cardOptions = null;
        if (PickingDuplicate) LastRewardMessage = $"{card.cardName} duplicated";
        if (--PendingPicks > 0) return;

        PickingUpgraded = false;
        PickingDuplicate = false;
        PendingBattleReward = false;
        ActiveMilestone = -1;
    }

    /// <summary>Heroes offered for the opening pick: distinct, random, never tutorial heroes.</summary>
    public static List<HeroSO> RollHeroOptions()
    {
        var pool = new List<HeroSO>(HeroDatabase.Instance.AllHeroes);
        pool.RemoveAll(h => h == null);
        return TakeRandom(pool, Config.heroChoiceCount, distinct: true);
    }

    /// <summary>The cards offered for the pick owed now. Rolled once per pick.</summary>
    public static List<CardSO> CurrentCardOptions()
    {
        if (_cardOptions == null) _cardOptions = PickingDuplicate ? RollDuplicateOptions() : RollCardOptions(PickingUpgraded);
        return _cardOptions;
    }

    /// <summary>
    /// Cards offered for one draft pick, from the base cards (or the upgraded ones). The opening draft is
    /// capped at <see cref="GauntletConfigSO.initialDraftMaxCost"/>; milestone picks are not.
    /// </summary>
    private static List<CardSO> RollCardOptions(bool upgraded)
    {
        var pool = upgraded ? UpgradedCardPool() : BaseCardPool();
        if (!Config.allowDuplicates) pool.RemoveAll(c => _deck.Contains(c));

        if (!upgraded && EncounterIndex == 0 && Config.initialDraftMaxCost > 0)
            pool.RemoveAll(c => c.cost > Config.initialDraftMaxCost);

        return TakeRandom(pool, Config.cardChoiceCount, Config.distinctOptionsPerPick);
    }

    /// <summary>Distinct cards of the deck offered for a Duplicate pick.</summary>
    private static List<CardSO> RollDuplicateOptions()
    {
        var pool = new List<CardSO>();
        foreach (var card in _deck)
            if (card != null && !pool.Contains(card)) pool.Add(card);
        return TakeRandom(pool, Config.duplicateChoiceCount, distinct: true);
    }

    // ---------------------------------------------------------------- Milestones

    /// <summary>The track opens once the first battle is won.</summary>
    public static bool MilestonesUnlocked => IsActive && (EncounterIndex > 0 || Outcome == RunOutcome.Won);

    /// <summary>A reached milestone is still waiting to be claimed; the next battle can't start until it is.</summary>
    public static bool HasUnclaimedMilestone => _milestones.Exists(IsClaimable);

    private static bool IsClaimable(MilestoneState m) => m.Reached && !m.Claimed && m.Reward != MilestoneReward.None;

    /// <summary>The milestone can be claimed right now: reached, unclaimed, and no other pick under way.</summary>
    public static bool CanClaim(int index) =>
        IsActive && Outcome == RunOutcome.InProgress && MilestonesUnlocked
        && index >= 0 && index < _milestones.Count && IsClaimable(_milestones[index])
        && PendingPicks == 0 && !PendingPerkPick;

    /// <summary>
    /// Claims a reached milestone. Card and perk rewards owe picks (<see cref="PendingPicks"/>,
    /// <see cref="PendingPerkPick"/>) that the Draft scene then shows; the rest apply on the spot and
    /// leave a <see cref="LastRewardMessage"/>.
    /// </summary>
    public static bool ClaimMilestone(int index)
    {
        if (!CanClaim(index)) return false;

        var milestone = _milestones[index];
        milestone.Claimed = true;
        LastRewardMessage = null;

        switch (milestone.Reward)
        {
            case MilestoneReward.UpgradedCards:
                PickingUpgraded = true;
                PendingPicks = PicksThisStep = Config.milestoneUpgradedPicks;
                _cardOptions = MilestoneCardOptions(index);
                ActiveMilestone = index;
                break;

            case MilestoneReward.DuplicateCard:
                var options = MilestoneCardOptions(index);
                if (options == null || options.Count == 0)
                {
                    LastRewardMessage = "No cards to duplicate";
                    break;
                }
                PickingDuplicate = true;
                PendingPicks = PicksThisStep = 1;
                _cardOptions = options;
                ActiveMilestone = index;
                break;

            case MilestoneReward.NewPerk:
                var perks = MilestonePerkOptions(index);
                if (perks.Count == 0)
                {
                    LastRewardMessage = "No new perks left to learn";
                    break;
                }
                _perkOptions = perks;
                PendingPerkPick = true;
                ActiveMilestone = index;
                break;

            case MilestoneReward.ForgePerk:
                var forged = ForgeRandomPlayerPassive();
                LastRewardMessage = forged != null ? $"{forged.passiveName} is forged" : "No perk left to forge";
                break;

            case MilestoneReward.BonusHealth:
                int health = Config.milestoneBonusHealth;
                if (_snapshot != null)
                {
                    _snapshot.maxHealth += health;
                    _snapshot.health += health;
                }
                LastRewardMessage = $"+{health} Hero Health";
                break;

            case MilestoneReward.BonusAttack:
                int attack = Config.milestoneBonusAttack;
                if (_snapshot != null) _snapshot.attack += attack;
                _rewardAttack += attack;
                LastRewardMessage = $"+{attack} Hero Attack";
                break;

            case MilestoneReward.BiggerHand:
                int cards = Config.milestoneExtraStartingCards;
                _playerExtraStartingCards += cards;
                LastRewardMessage = $"+{cards} Starting Hand Size";
                break;
        }
        return true;
    }

    /// <summary>
    /// The first pick an Upgraded Cards milestone offers, or the deck cards a Duplicate milestone offers;
    /// null for any other milestone.
    /// </summary>
    public static List<CardSO> MilestoneCardOptions(int index)
    {
        if (index < 0 || index >= _milestones.Count) return null;

        var milestone = _milestones[index];
        if (milestone.cardOptions != null) return milestone.cardOptions;

        switch (milestone.Reward)
        {
            case MilestoneReward.UpgradedCards: milestone.cardOptions = RollCardOptions(upgraded: true); break;
            case MilestoneReward.DuplicateCard: milestone.cardOptions = RollDuplicateOptions(); break;
            default: return null;
        }
        return milestone.cardOptions;
    }

    /// <summary>The passives a New Perk milestone offers, or null for any other milestone.</summary>
    public static List<HeroPassiveSO> MilestonePerkOptions(int index)
    {
        if (index < 0 || index >= _milestones.Count) return null;

        var milestone = _milestones[index];
        if (milestone.Reward != MilestoneReward.NewPerk) return null;

        if (milestone.perkOptions == null)
            milestone.perkOptions = TakeRandom(PlayerPerkPool(), Config.perkChoiceCount, distinct: true);
        return milestone.perkOptions;
    }

    /// <summary>The passives offered for the perk pick owed now.</summary>
    public static IReadOnlyList<HeroPassiveSO> CurrentPerkOptions =>
        (IReadOnlyList<HeroPassiveSO>)_perkOptions ?? System.Array.Empty<HeroPassiveSO>();

    public static void AddPerk(HeroPassiveSO passive)
    {
        if (!IsActive || !PendingPerkPick || passive == null || _perkOptions == null || !_perkOptions.Contains(passive)) return;

        _playerExtraPassives.Add(passive);
        PendingPerkPick = false;
        _perkOptions = null;
        ActiveMilestone = -1;
    }

    /// <summary>Every passive the player's hero plays with: its own (maybe forged), then its drafted perks.</summary>
    public static List<HeroPassiveSO> PlayerPassives
    {
        get
        {
            var all = new List<HeroPassiveSO>(_playerOwnPassives);
            all.AddRange(_playerExtraPassives);
            return all;
        }
    }

    /// <summary>Player-facing name of a milestone reward.</summary>
    public static string MilestoneRewardName(MilestoneReward reward)
    {
        var c = Config;
        switch (reward)
        {
            case MilestoneReward.UpgradedCards:
                return c.milestoneUpgradedPicks == 1 ? "Upgraded Card" : $"{c.milestoneUpgradedPicks} Upgraded Cards";
            case MilestoneReward.NewPerk: return "New Perk";
            case MilestoneReward.ForgePerk: return "Forge Perk";
            case MilestoneReward.BonusHealth: return $"+{c.milestoneBonusHealth} Health";
            case MilestoneReward.BonusAttack: return $"+{c.milestoneBonusAttack} Attack";
            case MilestoneReward.BiggerHand: return "Bigger Hand";
            case MilestoneReward.DuplicateCard: return "Duplicate";
            case MilestoneReward.None: return "Coming Soon";
            default: return reward.ToString();
        }
    }

    /// <summary>What a milestone reward does, for its preview.</summary>
    public static string MilestoneRewardDescription(MilestoneReward reward)
    {
        var c = Config;
        switch (reward)
        {
            case MilestoneReward.UpgradedCards:
                return c.milestoneUpgradedPicks == 1
                    ? "Draft an upgraded card for your deck"
                    : $"Draft {c.milestoneUpgradedPicks} upgraded cards for your deck";
            case MilestoneReward.NewPerk: return "Draft a new perk for your hero";
            case MilestoneReward.ForgePerk: return "Forge one of your hero's perks at random";
            case MilestoneReward.BonusHealth: return $"Your hero gains +{c.milestoneBonusHealth} health";
            case MilestoneReward.BonusAttack: return $"Your hero gains +{c.milestoneBonusAttack} attack";
            case MilestoneReward.BiggerHand:
                return c.milestoneExtraStartingCards == 1
                    ? "Start every battle with 1 more card in hand"
                    : $"Start every battle with {c.milestoneExtraStartingCards} more cards in hand";
            case MilestoneReward.DuplicateCard: return $"Pick 1 of {c.duplicateChoiceCount} cards from your deck and add a copy of it";
            case MilestoneReward.None: return "Nothing here yet";
            default: return string.Empty;
        }
    }

    /// <summary>
    /// Builds this run's track: <see cref="GauntletConfigSO.milestoneCount"/> milestones, one every
    /// <see cref="GauntletConfigSO.milestoneXpStep"/> XP, each rolled a reward from the random pool, then
    /// the final milestone at <see cref="GauntletConfigSO.finalMilestoneXp"/>, rolled from the final pool.
    /// Each pooled reward comes up once before any repeats; rewards that can't apply to the hero are left out.
    /// </summary>
    private static void RollMilestones()
    {
        _milestones.Clear();

        var random = OfferableRewards(Config.randomRewardPool);

        int next = 0;
        int step = Mathf.Max(1, Config.milestoneXpStep);
        for (int i = 1; i <= Config.milestoneCount; i++)
        {
            var reward = MilestoneReward.None;
            if (random.Count > 0)
            {
                if (next >= random.Count)
                {
                    Shuffle(random);
                    next = 0;
                }
                reward = random[next++];
            }
            _milestones.Add(new MilestoneState(i * step, reward));
        }

        if (Config.finalMilestoneXp > 0)
        {
            var final = OfferableRewards(Config.finalRewardPool);
            _milestones.Add(new MilestoneState(Config.finalMilestoneXp, final.Count > 0 ? final[0] : MilestoneReward.None));
        }
    }

    /// <summary>The pool's distinct rewards that can apply to the hero, shuffled.</summary>
    private static List<MilestoneReward> OfferableRewards(List<MilestoneReward> pool)
    {
        var rewards = new List<MilestoneReward>();
        foreach (var reward in pool)
            if (!rewards.Contains(reward) && CanOfferReward(reward))
                rewards.Add(reward);
        Shuffle(rewards);
        return rewards;
    }

    private static bool CanOfferReward(MilestoneReward reward)
    {
        switch (reward)
        {
            case MilestoneReward.ForgePerk: return _playerOwnPassives.Exists(p => p.forgedVersion != null);
            case MilestoneReward.NewPerk: return PlayerPerkPool().Count > 0;
            default: return true;
        }
    }

    /// <summary>Base passives the player's hero doesn't have in either form, while it has room for another perk.</summary>
    private static List<HeroPassiveSO> PlayerPerkPool()
    {
        var pool = new List<HeroPassiveSO>();
        if (HeroDatabase.Instance == null || _playerExtraPassives.Count >= MatchModifiers.MaxExtraPassives) return pool;

        var owned = PlayerPassives;
        foreach (var passive in HeroDatabase.Instance.AllPassives)
        {
            if (passive == null || passive.isForged) continue;
            if (owned.Exists(own => own == passive || own == passive.forgedVersion)) continue;
            pool.Add(passive);
        }
        return pool;
    }

    /// <summary>Swaps a random forgeable passive of the player's hero (own or perk) for its forged version.</summary>
    private static HeroPassiveSO ForgeRandomPlayerPassive()
    {
        var candidates = new List<(List<HeroPassiveSO> list, int index)>();
        foreach (var list in new[] { _playerOwnPassives, _playerExtraPassives })
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].forgedVersion != null) candidates.Add((list, i));
        if (candidates.Count == 0) return null;

        var (target, at) = candidates[Random.Range(0, candidates.Count)];
        var forged = target[at].forgedVersion;
        target[at] = forged;
        return forged;
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
    /// list: one of them may be forged (the enemy's Forged Passive blessing, the player's Forge Perk).
    /// </summary>
    public static IReadOnlyList<HeroPassiveSO> OwnPassivesFor(SelectionSide side)
    {
        if (!IsActive) return null;
        if (side == SelectionSide.Player) return Hero != null ? _playerOwnPassives : null;
        return _enemyHero != null ? _enemyOwnPassives : null;
    }

    /// <summary>
    /// The extra passives a side plays with this battle (the enemy's Extra Passive blessings, the
    /// player's drafted perks), or null for none.
    /// </summary>
    public static IReadOnlyList<HeroPassiveSO> ExtraPassivesFor(SelectionSide side)
    {
        if (!IsActive) return null;
        var extras = side == SelectionSide.Player ? _playerExtraPassives : _enemyExtraPassives;
        return extras.Count > 0 ? extras : null;
    }

    /// <summary>A side's max mana on its first turn: <paramref name="standard"/>, or more with Starting Mana.</summary>
    public static int StartingManaFor(SelectionSide side, int standard)
    {
        if (!IsActive || side != SelectionSide.Opponent || !HasEnemyBlessing(GauntletBlessing.StartingMana)) return standard;
        return Mathf.Max(standard, Config.blessedStartingMana);
    }

    /// <summary>Cards a side draws on top of the normal starting hand (the Bigger Hand blessing or milestones).</summary>
    public static int ExtraStartingCardsFor(SelectionSide side)
    {
        if (!IsActive) return 0;
        if (side == SelectionSide.Player) return _playerExtraStartingCards;
        return HasEnemyBlessing(GauntletBlessing.BiggerHand) ? Config.extraStartingCards : 0;
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
        else hero.modal.attack += _rewardAttack;

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

        // The score becomes XP on the way out of the battle (LeaveBattle), when the end-of-match panel
        // has finished tallying it, so the XP gained is exactly the score the player was shown.
        _scorePending = true;
        LastRewardMessage = null;

        // The next battle gets a freshly rolled enemy.
        ClearEnemy();

        if (EncounterIndex >= Config.encounters.Count)
        {
            Outcome = RunOutcome.Won;
            return;
        }

        HealSnapshot();
        GrantBattleReward();
    }

    /// <summary>
    /// Owes the won battle's card picks. They are taken on the rewards panel over the end-of-match screen;
    /// should the player get to the Draft scene with them still owed, it asks for them as a draft step.
    /// </summary>
    private static void GrantBattleReward()
    {
        PendingBattleReward = true;
        PendingPicks = PicksThisStep = Mathf.Max(1, Config.battleCardPicks);
        PickingUpgraded = false;
        PickingDuplicate = false;
        ActiveMilestone = -1;
        _cardOptions = null;
    }

    /// <summary>
    /// Adds the won battle's end-of-match score (never below zero) to the run's XP. Idempotent: whoever
    /// gets there first collects it. The rewards panel does, so the milestones the win reaches are
    /// already reached while it lists them; <see cref="LeaveBattle"/> catches every other way out.
    /// </summary>
    public static void CollectScore()
    {
        if (!_scorePending) return;
        _scorePending = false;

        var tracker = StatsTracker.Instance;
        var stats = tracker != null ? tracker.BuildStats(won: true) : null;
        if (stats != null) Xp += Mathf.Max(0, stats.TotalScore);
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
    public static void LeaveBattle()
    {
        CollectScore();
        LoadScene(Config != null ? Config.draftSceneName : "Draft");
    }

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
            attack = Config.keepHeroAttack || Hero == null ? _snapshot.attack : Hero.attack + _rewardAttack;
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
