using UnityEngine;

// Data-driven scoring weights + grade thresholds for the end-of-match stats system.
// Tune everything here in the inspector; no code changes needed to re-balance scoring.
[CreateAssetMenu(fileName = "ScoreConfig", menuName = "Stats/Score Config")]
public class ScoreConfig : ScriptableObject
{
    [Header("Base score")]
    public int baseWinScore = 1000;
    public int baseLossScore = 200;

    [Header("Per-unit weights")]
    public int perEnemyKilled = 50;
    public int perFriendlyLost = -20;
    public int perCardPlayed = 10;
    public int perUpgradedCard = 25;
    public int perHeroHpRemaining = 15;
    public int turnStartValue = 100;     // turn score starts here, then the per-turn penalty is applied
    public int perTurnPenalty = -5;      // negative: rewards finishing faster
    public int perMaxBoardMinion = 15;
    public int perLongestAgeTurn = 10;

    [Header("Efficiency (weight is multiplied by a 0..1 ratio)")]
    public int manaEfficiencyWeight = 200;
    public int overkillEfficiencyWeight = 100;

    [Header("Multi-kill bonuses (kills in a single turn)")]
    public int multiKill2Bonus = 50;
    public int multiKill3Bonus = 150;
    public int multiKill4Bonus = 400;

    [Header("Win-only bonuses")]
    public int flawlessBonus = 500;
    public int comebackBonus = 500;
    public int oneTurnKillBonus = 750;
    public int comebackHpThreshold = 5;

    [Header("Tutorial")]

    [Tooltip("Added as its own breakdown row on the tutorial match, and only there. Unlike the win-only " +
             "bonuses above it applies whether the tutorial was won or lost — it is a property of WHICH " +
             "match was played, not of how it went. Set it to 0 to score the tutorial like any other match; " +
             "the row is then dropped rather than shown as a pointless zero.")]
    public int tutorialScore = 250;

    [Header("Grade thresholds (minimum total score for each grade)")]
    public int gradeS = 3000;
    public int gradeA = 2200;
    public int gradeB = 1500;
    public int gradeC = 800;
    public int gradeD = 400;
    public int gradeE = 0;
    // below gradeE => the lowest grade

    [Header("Grade labels")]
    public string gradeSLabel = "S";
    public string gradeALabel = "A";
    public string gradeBLabel = "B";
    public string gradeCLabel = "C";
    public string gradeDLabel = "D";
    public string gradeELabel = "E";
    [Tooltip("The floor: shown below the E threshold, including for negative totals.")]
    public string gradeFLabel = "F";

    [Header("Breakdown row labels")]
    public string resultLabel = "Result";
    public string enemyKilledLabel = "Enemy minions killed";
    public string friendlyLostLabel = "Friendly minions lost";
    public string cardsPlayedLabel = "Cards played";
    public string upgradedCardsLabel = "Upgraded cards played";
    public string heroHpLabel = "Hero HP remaining";
    public string turnsLabel = "Turns";
    public string boardControlLabel = "Board control (max minions)";
    public string longestMinionLabel = "Longest-surviving minion";
    public string manaEfficiencyLabel = "Mana efficiency";
    public string overkillEfficiencyLabel = "Overkill efficiency";
    public string multiKillLabel = "Multi-kill";
    public string flawlessLabel = "Flawless win";
    public string comebackLabel = "Comeback";
    public string oneTurnKillLabel = "One turn kill";
    public string tutorialLabel = "Tutorial";

    [Header("Breakdown row values ({0} is replaced by the number)")]
    public string victoryValue = "Victory";
    public string defeatValue = "Defeat";
    public string turnsSurvivedFormat = "{0} turns";
    public string percentFormat = "{0}%";
    public string multiKillFormat = "{0} in a turn";
    public string flawlessValue = "No Damage!";
    public string comebackValue = "Clutch!";
    public string oneTurnKillValue = "OTK!";
    public string tutorialValue = "Tutorial!";

    public string GradeFor(int score)
    {
        if (score >= gradeS) return gradeSLabel;
        if (score >= gradeA) return gradeALabel;
        if (score >= gradeB) return gradeBLabel;
        if (score >= gradeC) return gradeCLabel;
        if (score >= gradeD) return gradeDLabel;
        if (score >= gradeE) return gradeELabel;
        return gradeFLabel;
    }
}
