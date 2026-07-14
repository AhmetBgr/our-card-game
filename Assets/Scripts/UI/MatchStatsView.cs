using System.Text;
using TMPro;
using UnityEngine;

// Sits on a victory/defeat panel. When the panel opens, Show() pulls the finalized MatchStats from
// StatsTracker, then fills the rank, score, and a multi-line breakdown body. Purely presentational.
public class MatchStatsView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [Tooltip("Optional. Shows the persisted best score, or the new-record line when this run beat it.")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    [Header("High score")]
    [SerializeField] private string highScoreFormat = "Best {0}";
    [SerializeField] private string newHighScoreLabel = "New Best!";
    [SerializeField] private Color newHighScoreColor = new Color(1f, 0.82f, 0.25f); // gold

    [Header("Point colors (editable in inspector)")]
    [Tooltip("Color for positive point contributions (e.g. +50).")]
    [SerializeField] private Color positivePointsColor = new Color(0.30f, 0.85f, 0.40f); // green
    [Tooltip("Color for negative point contributions (e.g. -20).")]
    [SerializeField] private Color negativePointsColor = new Color(0.90f, 0.35f, 0.35f); // red
    [Tooltip("Color for zero-point rows.")]
    [SerializeField] private Color neutralPointsColor = new Color(0.60f, 0.63f, 0.66f); // gray

    [Tooltip("Horizontal column (percent of body width) where the points start, so they all line up.")]
    [Range(0f, 100f)]
    [SerializeField] private float pointsColumn = 78f;

    [Header("Points text style (editable in inspector)")]
    [Tooltip("Render the points in bold.")]
    [SerializeField] private bool pointsBold = true;
    [Tooltip("Render the points in italic.")]
    [SerializeField] private bool pointsItalic = false;
    [Tooltip("Font size for the points text. 0 = inherit the body font size.")]
    [SerializeField] private float pointsFontSize = 0f;

    [Header("Value text style (the label stays default body text)")]
    [Tooltip("Color for the row value text.")]
    [SerializeField] private Color valueTextColor = Color.white;
    [Tooltip("Render the value in bold.")]
    [SerializeField] private bool valueTextBold = false;
    [Tooltip("Render the value in italic.")]
    [SerializeField] private bool valueTextItalic = false;
    [Tooltip("Font size for the value text. 0 = inherit the body font size.")]
    [SerializeField] private float valueTextFontSize = 0f;

    // Wraps content in rich-text tags driven by the inspector fields (color, then bold/italic/size).
    private static string ApplyStyle(string content, Color color, bool bold, bool italic, float size)
    {
        string hex = ColorUtility.ToHtmlStringRGB(color);
        string styled = $"<color=#{hex}>{content}</color>";
        if (bold) styled = $"<b>{styled}</b>";
        if (italic) styled = $"<i>{styled}</i>";
        if (size > 0f)
        {
            string s = size.ToString(System.Globalization.CultureInfo.InvariantCulture);
            styled = $"<size={s}>{styled}</size>";
        }
        return styled;
    }

    public void Show(bool won)
    {
        var tracker = StatsTracker.Instance;
        if (tracker == null) return;

        MatchStats stats = tracker.BuildStats(won);
        if (stats == null) return;

        ApplyToTexts(stats);
    }

    private void ApplyToTexts(MatchStats stats)
    {
        if (rankText != null) rankText.text = "Rank " + stats.Grade;
        if (scoreText != null) scoreText.text = "" + stats.TotalScore.ToString("N0");
        if (bodyText != null) bodyText.text = BuildBody(stats);

        if (highScoreText != null)
        {
            string best = string.Format(highScoreFormat, stats.highScore.ToString("N0"));
            highScoreText.text = stats.isNewHighScore
                ? ApplyStyle(newHighScoreLabel + " " + best, newHighScoreColor, true, false, 0f)
                : best;
        }
    }

    private string BuildBody(MatchStats stats)
    {
        var sb = new StringBuilder();
        string col = pointsColumn.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var row in stats.Breakdown)
        {
            string pts = row.points >= 0 ? "+" + row.points : row.points.ToString();
            Color c = row.points > 0 ? positivePointsColor
                    : row.points < 0 ? negativePointsColor
                    : neutralPointsColor;

            // Label stays plain default text; only the value carries the inspector style.
            string styledValue = ApplyStyle(row.value, valueTextColor, valueTextBold, valueTextItalic, valueTextFontSize);
            string styledPts = ApplyStyle(pts, c, pointsBold, pointsItalic, pointsFontSize);

            // <pos> snaps the points to a fixed column so they all line up.
            sb.AppendLine($"{row.label}: {styledValue}<pos={col}%>{styledPts}");
        }
        return sb.ToString().TrimEnd();
    }

#if UNITY_EDITOR
    // Live inspector preview: whenever a field changes in edit mode, repopulate the panel with sample
    // data so styling/layout is visible without entering play mode. Deferred via delayCall because
    // touching other objects (the TMP texts) directly inside OnValidate is not allowed.
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return; // component may have been destroyed before the callback runs
            RenderPreview();
        };
    }

    [ContextMenu("Preview Sample Stats")]
    public void RenderPreview()
    {
        var cfg = ScriptableObject.CreateInstance<ScoreConfig>();
        var sample = new MatchStats
        {
            won = true,
            playerTurns = 7,
            cardsPlayed = 11,
            upgradedCardsPlayed = 3,
            friendlyMinionsDied = 2,
            enemyMinionsKilled = 6,
            manaSpent = 20,
            manaGranted = 24,
            overkillDamage = 4,
            neededDamage = 16,
            maxFriendlyAlive = 4,
            longestMinionAge = 5,
            maxKillsInOneTurn = 3,
            heroFinalHp = 18,
            heroDamageTaken = 12,
            heroMinHp = 4,
            comeback = true,
            highScore = 3120,
            isNewHighScore = true,
        };
        sample.ComputeScore(cfg);
        ApplyToTexts(sample);
        DestroyImmediate(cfg);
    }
#endif
}
