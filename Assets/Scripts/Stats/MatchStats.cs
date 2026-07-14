using System.Collections.Generic;
using UnityEngine;

// Plain data holder for one match's tracked metrics. Populated by StatsTracker, then
// ComputeScore() turns the raw metrics into a total score, a letter grade, and an ordered
// breakdown the panels display. No MonoBehaviour / no scene dependency.
public class MatchStats
{
    // --- Raw metrics (filled in by StatsTracker) ---
    public bool won;
    public int playerTurns;
    public int cardsPlayed;
    public int upgradedCardsPlayed;
    public int friendlyMinionsDied;
    public int enemyMinionsKilled;
    public int manaSpent;
    public int manaGranted;
    public int overkillDamage;   // wasted damage past 0 HP on enemy minions
    public int neededDamage;     // enemy HP that actually had to be removed
    public int maxFriendlyAlive; // board control
    public int longestMinionAge; // turns a friendly minion survived
    public int maxKillsInOneTurn;
    public int heroFinalHp;
    public int heroDamageTaken;
    public int heroMinHp;
    public bool flawless;
    public bool comeback;
    public bool oneTurnKill;

    // --- Persisted best (filled in by StatsTracker after scoring, from SaveManager) ---
    public int highScore;        // best score ever recorded, including this match
    public bool isNewHighScore;  // this match beat the previous best

    public float ManaEfficiency =>
        manaGranted > 0 ? Mathf.Clamp01((float)manaSpent / manaGranted) : 0f;

    // 1.0 = every point of lethal damage was needed; lower = more overkill waste.
    public float OverkillEfficiency =>
        (neededDamage + overkillDamage) > 0
            ? Mathf.Clamp01((float)neededDamage / (neededDamage + overkillDamage))
            : 1f;

    public struct ScoreRow
    {
        public string label;
        public string value;
        public int points;
        public ScoreRow(string label, string value, int points)
        {
            this.label = label;
            this.value = value;
            this.points = points;
        }
    }

    public int TotalScore { get; private set; }
    public string Grade { get; private set; }
    public List<ScoreRow> Breakdown { get; private set; }

    public void ComputeScore(ScoreConfig cfg)
    {
        Breakdown = new List<ScoreRow>();
        int total = 0;

        void Add(string label, string value, int points)
        {
            Breakdown.Add(new ScoreRow(label, value, points));
            total += points;
        }

        Add("Result", won ? "Victory" : "Defeat", won ? cfg.baseWinScore : cfg.baseLossScore);
        Add("Enemy minions killed", enemyMinionsKilled.ToString(), enemyMinionsKilled * cfg.perEnemyKilled);
        Add("Friendly minions lost", friendlyMinionsDied.ToString(), friendlyMinionsDied * cfg.perFriendlyLost);
        Add("Cards played", cardsPlayed.ToString(), cardsPlayed * cfg.perCardPlayed);
        Add("Upgraded cards played", upgradedCardsPlayed.ToString(), upgradedCardsPlayed * cfg.perUpgradedCard);
        Add("Hero HP remaining", heroFinalHp.ToString(), Mathf.Max(0, heroFinalHp) * cfg.perHeroHpRemaining);
        Add("Turns", playerTurns.ToString(), cfg.turnStartValue + playerTurns * cfg.perTurnPenalty);
        Add("Board control (max minions)", maxFriendlyAlive.ToString(), maxFriendlyAlive * cfg.perMaxBoardMinion);
        Add("Longest-surviving minion", longestMinionAge + " turns", longestMinionAge * cfg.perLongestAgeTurn);

        Add("Mana efficiency", Mathf.RoundToInt(ManaEfficiency * 100f) + "%",
            Mathf.RoundToInt(ManaEfficiency * cfg.manaEfficiencyWeight));
        Add("Overkill efficiency", Mathf.RoundToInt(OverkillEfficiency * 100f) + "%",
            Mathf.RoundToInt(OverkillEfficiency * cfg.overkillEfficiencyWeight));

        if (maxKillsInOneTurn >= 4) Add("Multi-kill", maxKillsInOneTurn + " in a turn", cfg.multiKill4Bonus);
        else if (maxKillsInOneTurn == 3) Add("Multi-kill", "3 in a turn", cfg.multiKill3Bonus);
        else if (maxKillsInOneTurn == 2) Add("Multi-kill", "2 in a turn", cfg.multiKill2Bonus);

        if (won)
        {
            if (flawless) Add("Flawless win", "no damage!", cfg.flawlessBonus);
            if (comeback) Add("Comeback", "clutch!", cfg.comebackBonus);
            if (oneTurnKill) Add("One turn kill", "OTK!", cfg.oneTurnKillBonus);
        }

        total = Mathf.Max(0, total);
        TotalScore = total;
        Grade = cfg.GradeFor(total);
    }
}
