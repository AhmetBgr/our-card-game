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
    public int highScore;         // best score ever recorded, including this match
    public int previousHighScore; // best as it stood before this match was counted
    public bool isNewHighScore;   // this match beat the previous best

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

    // Grade thresholds and labels copied out of the config at score time. Kept by value (not as a
    // ScoreConfig reference) so the running-grade lookup stays valid even if the config instance was a
    // temporary one that has since been destroyed - see MatchStatsView's editor preview.
    private int gradeS, gradeA, gradeB, gradeC, gradeD, gradeE;
    private string gradeSLabel = "S", gradeALabel = "A", gradeBLabel = "B", gradeCLabel = "C",
                   gradeDLabel = "D", gradeELabel = "E", gradeFLabel = "F";

    // Grade for an arbitrary (partial) score, used while the breakdown counts up row by row.
    public string GradeForScore(int score)
    {
        if (score >= gradeS) return gradeSLabel;
        if (score >= gradeA) return gradeALabel;
        if (score >= gradeB) return gradeBLabel;
        if (score >= gradeC) return gradeCLabel;
        if (score >= gradeD) return gradeDLabel;
        if (score >= gradeE) return gradeELabel;
        return gradeFLabel;
    }

    // Inspector-authored formats go through here so a stray brace in the asset falls back to the bare
    // number instead of throwing FormatException in the middle of the end-game panel.
    private static string Format(string format, object arg)
    {
        if (string.IsNullOrEmpty(format)) return arg.ToString();

        try { return string.Format(format, arg); }
        catch (System.FormatException) { return arg.ToString(); }
    }

    public void ComputeScore(ScoreConfig cfg)
    {
        Breakdown = new List<ScoreRow>();
        int total = 0;

        gradeS = cfg.gradeS;
        gradeA = cfg.gradeA;
        gradeB = cfg.gradeB;
        gradeC = cfg.gradeC;
        gradeD = cfg.gradeD;
        gradeE = cfg.gradeE;
        gradeSLabel = cfg.gradeSLabel;
        gradeALabel = cfg.gradeALabel;
        gradeBLabel = cfg.gradeBLabel;
        gradeCLabel = cfg.gradeCLabel;
        gradeDLabel = cfg.gradeDLabel;
        gradeELabel = cfg.gradeELabel;
        gradeFLabel = cfg.gradeFLabel;

        void Add(string label, string value, int points)
        {
            Breakdown.Add(new ScoreRow(label, value, points));
            total += points;
        }

        Add(cfg.resultLabel, won ? cfg.victoryValue : cfg.defeatValue, won ? cfg.baseWinScore : cfg.baseLossScore);
        Add(cfg.enemyKilledLabel, enemyMinionsKilled.ToString(), enemyMinionsKilled * cfg.perEnemyKilled);
        Add(cfg.friendlyLostLabel, friendlyMinionsDied.ToString(), friendlyMinionsDied * cfg.perFriendlyLost);
        Add(cfg.cardsPlayedLabel, cardsPlayed.ToString(), cardsPlayed * cfg.perCardPlayed);
        Add(cfg.upgradedCardsLabel, upgradedCardsPlayed.ToString(), upgradedCardsPlayed * cfg.perUpgradedCard);
        Add(cfg.heroHpLabel, heroFinalHp.ToString(), Mathf.Max(0, heroFinalHp) * cfg.perHeroHpRemaining);
        Add(cfg.turnsLabel, playerTurns.ToString(), cfg.turnStartValue + playerTurns * cfg.perTurnPenalty);
        Add(cfg.boardControlLabel, maxFriendlyAlive.ToString(), maxFriendlyAlive * cfg.perMaxBoardMinion);
        Add(cfg.longestMinionLabel, Format(cfg.turnsSurvivedFormat, longestMinionAge), longestMinionAge * cfg.perLongestAgeTurn);

        Add(cfg.manaEfficiencyLabel, Format(cfg.percentFormat, Mathf.RoundToInt(ManaEfficiency * 100f)),
            Mathf.RoundToInt(ManaEfficiency * cfg.manaEfficiencyWeight));
        Add(cfg.overkillEfficiencyLabel, Format(cfg.percentFormat, Mathf.RoundToInt(OverkillEfficiency * 100f)),
            Mathf.RoundToInt(OverkillEfficiency * cfg.overkillEfficiencyWeight));

        if (maxKillsInOneTurn >= 4) Add(cfg.multiKillLabel, Format(cfg.multiKillFormat, maxKillsInOneTurn), cfg.multiKill4Bonus);
        else if (maxKillsInOneTurn == 3) Add(cfg.multiKillLabel, Format(cfg.multiKillFormat, 3), cfg.multiKill3Bonus);
        else if (maxKillsInOneTurn == 2) Add(cfg.multiKillLabel, Format(cfg.multiKillFormat, 2), cfg.multiKill2Bonus);

        if (won)
        {
            if (flawless) Add(cfg.flawlessLabel, cfg.flawlessValue, cfg.flawlessBonus);
            if (comeback) Add(cfg.comebackLabel, cfg.comebackValue, cfg.comebackBonus);
            if (oneTurnKill) Add(cfg.oneTurnKillLabel, cfg.oneTurnKillValue, cfg.oneTurnKillBonus);
        }

        // Deliberately not floored at zero: a match that earns more penalties than points scores below
        // zero, and the panel counts down to it row by row rather than snapping back up to 0 at the end.
        TotalScore = total;
        Grade = cfg.GradeFor(total);
    }
}
