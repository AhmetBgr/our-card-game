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

    [Header("Grade thresholds (minimum total score for each grade)")]
    public int gradeS = 3000;
    public int gradeA = 2200;
    public int gradeB = 1500;
    public int gradeC = 800;
    // below gradeC => "D"

    public string GradeFor(int score)
    {
        if (score >= gradeS) return "S";
        if (score >= gradeA) return "A";
        if (score >= gradeB) return "B";
        if (score >= gradeC) return "C";
        return "D";
    }
}
