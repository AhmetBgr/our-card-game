using UnityEngine;

// Passive, non-invasive match-stats collector. Mirrors the ActionLogPanel pattern: a scene-scoped
// Singleton that only subscribes to existing gameplay static events and accumulates numbers. It never
// drives or mutates game state. Because it is scene-scoped, it is recreated fresh each match (Replay
// reloads the Game scene), so all counters reset automatically.
public class StatsTracker : Singleton<StatsTracker>
{
    [SerializeField] private ScoreConfig scoreConfig;

    private MatchStats stats = new MatchStats();

    // Running per-match state that is not stored directly on MatchStats.
    private int killsThisTurn;
    private int friendlyAlive;
    private int enemyHeroMaxHp = -1;              // enemy hero HP observed on the first player turn (~full)
    private int enemyHeroHpAtTurnStart = int.MinValue;
    private bool heroMinHpInitialized;
    private bool previousHighScoreCaptured;

    // Mana accumulators. Kept here (not on MatchStats) so BuildStats can recompute the efficiency
    // numbers each call and stay idempotent. The current (last) player turn's granted/spent are
    // tracked separately so the winning turn can be excluded from the efficiency math.
    private int manaGrantedTotal;
    private int manaSpentTotal;
    private int currentTurnGranted;
    private int currentTurnSpent;

    protected override void Awake()
    {
        base.Awake();
        stats = new MatchStats();
    }

    private void OnEnable()
    {
        MinionController.OnDied += OnMinionDied;
        MinionController.OnTookDamage += OnMinionTookDamage;
        GameManager.OnMinionSummoned += OnMinionSummoned;
        GameManager.OnTurnStarted += OnTurnStarted;
        GameManager.OnTurnEnd += OnTurnEnd;
        GameManager.OnCardPlayed += OnCardPlayed;
    }

    private void OnDisable()
    {
        MinionController.OnDied -= OnMinionDied;
        MinionController.OnTookDamage -= OnMinionTookDamage;
        GameManager.OnMinionSummoned -= OnMinionSummoned;
        GameManager.OnTurnStarted -= OnTurnStarted;
        GameManager.OnTurnEnd -= OnTurnEnd;
        GameManager.OnCardPlayed -= OnCardPlayed;
    }

    private static bool IsPlayerOwned(MinionController m)
    {
        return m != null && m.owner != null && GameManager.Instance != null && m.owner == GameManager.Instance.player;
    }

    private void OnCardPlayed(Agent agent, CardSO card)
    {
        if (card == null || agent == null || GameManager.Instance == null) return;
        if (agent != GameManager.Instance.player) return; // stats track the player only

        stats.cardsPlayed++;
        manaSpentTotal += card.cost;
        currentTurnSpent += card.cost;
        if (card.isUpgraded) stats.upgradedCardsPlayed++;
    }

    private void OnMinionSummoned(MinionController minion)
    {
        if (minion == null || minion is HeroController) return;

        if (IsPlayerOwned(minion))
        {
            friendlyAlive++;
            if (friendlyAlive > stats.maxFriendlyAlive) stats.maxFriendlyAlive = friendlyAlive;
        }
    }

    private void OnMinionTookDamage(MinionController minion, int effectiveDamage)
    {
        if (minion == null) return;

        // Hero damage feeds flawless / comeback / min-HP tracking (heroes never fire OnDied).
        if (minion is HeroController)
        {
            if (IsPlayerOwned(minion))
            {
                stats.heroDamageTaken += effectiveDamage;
                TrackPlayerHeroHp(minion.modal != null ? minion.modal.health : 0);
            }
            return;
        }

        if (minion.modal == null) return;

        // Lethal hit: TakeDamage fires this BEFORE Die() and never clamps health, so the negative
        // remainder is exactly the wasted part of this blow and the rest of it is what was needed.
        // Splitting it here rather than in OnMinionDied uses the hit's own damage number, so a heal or
        // buff landing since the previous hit (neither fires OnTookDamage) can't skew the needed half.
        if (minion.modal.health <= 0 && !IsPlayerOwned(minion))
        {
            int overkill = Mathf.Max(0, -minion.modal.health);
            stats.overkillDamage += overkill;
            stats.neededDamage += Mathf.Max(0, effectiveDamage - overkill);
        }
    }

    private void OnMinionDied(MinionController minion)
    {
        if (minion == null || minion is HeroController) return;

        if (IsPlayerOwned(minion))
        {
            stats.friendlyMinionsDied++;
            friendlyAlive = Mathf.Max(0, friendlyAlive - 1);
            // Sample here too: a dying minion is pulled out of owner.minions, so the end-of-turn sweep
            // below would never see the age it reached on the turn it died.
            SampleMinionAge(minion);
        }
        else
        {
            stats.enemyMinionsKilled++;
            killsThisTurn++;
            if (killsThisTurn > stats.maxKillsInOneTurn) stats.maxKillsInOneTurn = killsThisTurn;

            // Overkill is split off the killing blow in OnMinionTookDamage, which TakeDamage fires just
            // before this. A death from a non-damage source spent no damage, so it contributes nothing.
        }
    }

    private void OnTurnStarted(GameState state)
    {
        if (state != GameState.PlayerTurn) return;

        stats.playerTurns++;
        killsThisTurn = 0;

        var gm = GameManager.Instance;
        if (gm == null) return;

        manaGrantedTotal += gm.PlayerMaxMana;
        currentTurnGranted = gm.PlayerMaxMana;
        currentTurnSpent = 0;

        if (gm.opponent != null && gm.opponent.hero != null && gm.opponent.hero.modal != null)
        {
            enemyHeroHpAtTurnStart = gm.opponent.hero.modal.health;
            if (enemyHeroMaxHp < 0) enemyHeroMaxHp = enemyHeroHpAtTurnStart; // first player turn ~ full HP
        }

        if (gm.player != null && gm.player.hero != null && gm.player.hero.modal != null)
            TrackPlayerHeroHp(gm.player.hero.modal.health);
    }

    private void OnTurnEnd(GameState state)
    {
        // Longest-surviving friendly minion: sample ages of the minions still alive.
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null || gm.player.minions == null) return;

        foreach (var m in gm.player.minions)
        {
            SampleMinionAge(m);
        }
    }

    // MinionController.age ticks on GameManager.OnTurnEnd, which fires at the end of *both* turns, so
    // age counts half-rounds. Halving converts it to player turns survived, matching the units of every
    // other stat on the panel. Integer division also absorbs the fact that this tracker is subscribed to
    // OnTurnEnd before any minion, so during the end-of-turn sweep we read age one tick before it ticks.
    private void SampleMinionAge(MinionController m)
    {
        if (m == null) return;

        int turnsSurvived = m.age / 2;
        if (turnsSurvived > stats.longestMinionAge) stats.longestMinionAge = turnsSurvived;
    }

    private void TrackPlayerHeroHp(int hp)
    {
        if (!heroMinHpInitialized)
        {
            stats.heroMinHp = hp;
            heroMinHpInitialized = true;
        }
        else if (hp < stats.heroMinHp)
        {
            stats.heroMinHp = hp;
        }
    }

    // Finalizes end-of-game fields, computes the score, and returns the stats for display.
    public MatchStats BuildStats(bool won)
    {
        var gm = GameManager.Instance;
        stats.won = won;

        // Latched at GameManager.Awake and never flipped mid-scene — MarkTutorialPlayed only writes the
        // save flag — so this still reads true here, after the win condition that ends the tutorial has
        // already retired it.
        stats.isTutorial = GameManager.IsTutorialMatch;

        if (gm != null && gm.player != null && gm.player.hero != null && gm.player.hero.modal != null)
        {
            stats.heroFinalHp = gm.player.hero.modal.health;
            TrackPlayerHeroHp(stats.heroFinalHp);
        }

        // Mana efficiency: on a win, exclude the final (killing) turn — the player rushed lethal and
        // typically leaves mana unspent, which would otherwise unfairly tank efficiency.
        int granted = manaGrantedTotal;
        int spent = manaSpentTotal;
        if (won)
        {
            granted -= currentTurnGranted;
            spent -= currentTurnSpent;
        }
        stats.manaGranted = Mathf.Max(0, granted);
        stats.manaSpent = Mathf.Max(0, spent);

        int threshold = scoreConfig != null ? scoreConfig.comebackHpThreshold : 5;
        stats.flawless = won && stats.heroDamageTaken <= 0;
        stats.comeback = won && heroMinHpInitialized && stats.heroMinHp <= threshold;
        stats.oneTurnKill = won && enemyHeroMaxHp > 0 && enemyHeroHpAtTurnStart == enemyHeroMaxHp;

        ScoreConfig cfg = scoreConfig != null ? scoreConfig : ScriptableObject.CreateInstance<ScoreConfig>();
        stats.ComputeScore(cfg);

        // Persist the best run. BuildStats can run more than once per match (the panel repopulates every
        // time it opens), so OR the flag in - a second call must not clear a record set by the first.
        var save = SaveManager.Instance;
        if (save != null)
        {
            // Read the stored best before committing this run, and only on the first call: by the second
            // one the save already contains this match, which would make "previous" and "current" equal.
            if (!previousHighScoreCaptured)
            {
                stats.previousHighScore = save.HighScore;
                previousHighScoreCaptured = true;
            }

            stats.isNewHighScore |= save.TrySetHighScore(stats.TotalScore);
            stats.highScore = save.HighScore;
        }

        return stats;
    }
}
