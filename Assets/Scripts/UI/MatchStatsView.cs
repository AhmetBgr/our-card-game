using System.Collections.Generic;
using System.Linq;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;

// Sits on a victory/defeat panel. When the panel opens, Show() pulls the finalized MatchStats from
// StatsTracker, then fills the rank, score, and a multi-line breakdown body. Purely presentational.
//
// In play mode the breakdown reveals itself one line at a time (lowest-scoring row first). Rank and
// score are visible the whole time: they start at the lowest grade / zero and climb as each line
// lands, punching whenever their displayed value changes.
public class MatchStatsView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [Tooltip("Optional. Shows the persisted best score, this run included.")]
    [SerializeField] private TextMeshProUGUI highestScoreText;
    [Tooltip("Optional. Enabled only when this run beat the previous best. Its own copy and styling are " +
             "used as authored - this only switches it on and off.")]
    [SerializeField] private GameObject newRecordText;
    [Tooltip("Optional. The 'click to skip' prompt: on only while the reveal is actually skippable.")]
    [SerializeField] private GameObject skipHint;

    [Header("Text formats ({0} is replaced by the value)")]
    [Tooltip("Row labels and values live on the ScoreConfig asset, not here.")]
    [SerializeField] private string rankFormat = "Rank {0}";
    [SerializeField] private string highestScoreFormat = "Highest: {0}";

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

    [Header("Line-by-line reveal")]
    [Tooltip("Reveal the breakdown one line at a time. When off, everything appears at once.")]
    [SerializeField] private bool animateReveal = true;
    [Tooltip("Extra wait before the first line lands, on top of the delay the popup itself was opened with.")]
    [SerializeField] private float revealStartDelay = 0.25f;
    [Tooltip("Seconds between two revealed lines.")]
    [SerializeField] private float revealInterval = 0.22f;
    [Tooltip("How long the score number takes to count up to its new value after a line lands.")]
    [SerializeField] private float scoreCountDuration = 0.15f;

    [Header("Line appear animation")]
    [Tooltip("How long a single line takes to fade and rise into place. 0 = pop in instantly.")]
    [SerializeField] private float lineAppearDuration = 0.28f;
    [Tooltip("Pixels below its final position a line starts from. 0 = fade only.")]
    [SerializeField] private float lineRiseDistance = 12f;
    [SerializeField] private Ease lineAppearEase = Ease.OutCubic;

    [Header("Punch animation")]
    [Tooltip("Scale punch applied to the score text every time the score changes.")]
    [SerializeField] private float scorePunchStrength = 0.25f;
    [Tooltip("Scale punch applied to the rank text every time the rank letter changes.")]
    [SerializeField] private float rankPunchStrength = 0.45f;
    [SerializeField] private float punchDuration = 0.3f;
    [SerializeField] private int punchVibrato = 8;
    [Range(0f, 1f)]
    [SerializeField] private float punchElasticity = 0.7f;

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
    // transparent keeps every glyph and size tag intact but drives the alpha to zero, so a not-yet-
    // revealed row still occupies its exact space in the layout.
    private static string ApplyStyle(string content, Color color, bool bold, bool italic, float size, bool transparent = false)
    {
        string hex = ColorUtility.ToHtmlStringRGB(color) + (transparent ? "00" : "");
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

    // Reveal state. Only meaningful while a reveal sequence is running.
    private Sequence revealSequence;
    private Tween scoreCountTween;
    private readonly List<MatchStats.ScoreRow> revealRows = new List<MatchStats.ScoreRow>();
    private int revealedRowCount;   // how many of revealRows are drawn opaque
    private MatchStats revealStats; // stats being revealed; non-null only while a reveal is in flight
    private float[] rowStartTimes;  // unscaled time each row began its appear animation (+inf = not yet)
    private TMP_MeshInfo[] cachedMeshInfo; // pristine glyph geometry to animate away from
    private bool animatingRows;
    private int revealedScore;      // running (unclamped) sum of the rows revealed so far
    private int displayedScore;     // what the score text currently reads
    private string displayedGrade;
    private Vector3 scoreBaseScale = Vector3.one;
    private Vector3 rankBaseScale = Vector3.one;

    private void Awake()
    {
        // Cached so a punch that gets killed mid-flight can be restored exactly.
        if (scoreText != null) scoreBaseScale = scoreText.transform.localScale;
        if (rankText != null) rankBaseScale = rankText.transform.localScale;
    }

    private void OnDisable()
    {
        StopReveal();
    }

    private void LateUpdate()
    {
        if (!animatingRows) return;
        // The pass that finds every row finished also writes the pristine geometry back, so the text is
        // left exactly as TMP laid it out.
        animatingRows = ApplyRowAnimation();
    }

    public void Show(bool won, float openDelay = 0f)
    {
        var tracker = StatsTracker.Instance;
        if (tracker == null) return;

        MatchStats stats = tracker.BuildStats(won);
        if (stats == null) return;

        StopReveal();

        if (animateReveal && Application.isPlaying) StartReveal(stats, openDelay);
        else ApplyToTexts(stats);
    }

    // True while the panel is still counting itself up, i.e. while there is something worth skipping.
    public bool IsRevealing => revealStats != null;

    // Fast-forwards straight to the finished panel: every row opaque, the final score and rank, and the
    // high-score line. Score and rank still punch if the jump changed them. No-op once the reveal is done.
    public void SkipReveal()
    {
        if (!IsRevealing) return;

        MatchStats stats = revealStats;
        revealStats = null;
        SetSkipHintVisible(false);

        revealSequence?.Kill();
        revealSequence = null;
        scoreCountTween?.Kill();
        scoreCountTween = null;

        // Dropping the per-row start times and the cached geometry ends the appear animation; the text is
        // then rebuilt from scratch so no half-faded or half-lifted glyphs survive the jump.
        rowStartTimes = null;
        cachedMeshInfo = null;
        animatingRows = false;
        revealedRowCount = revealRows.Count;
        if (bodyText != null)
        {
            bodyText.text = RenderBody(revealedRowCount);
            bodyText.ForceMeshUpdate();
        }

        revealedScore = stats.TotalScore;
        bool scoreChanged = displayedScore != stats.TotalScore;
        displayedScore = stats.TotalScore;
        SetScoreText(displayedScore);
        if (scoreChanged && scoreText != null) Punch(scoreText.transform, scoreBaseScale, scorePunchStrength);

        SetGrade(stats.Grade);
        ApplyHighScoreTexts(stats);
    }

    // Breakdown rows ordered by their point contribution, smallest first, so the panel builds from the
    // penalties up to the big bonuses. OrderBy is stable, so equal-point rows keep their scoring order.
    private static IEnumerable<MatchStats.ScoreRow> SortedRows(MatchStats stats)
    {
        if (stats.Breakdown == null) return new List<MatchStats.ScoreRow>();
        return stats.Breakdown.OrderBy(r => r.points);
    }

    private void StartReveal(MatchStats stats, float openDelay)
    {
        revealStats = stats;
        revealRows.Clear();
        revealRows.AddRange(SortedRows(stats));
        revealedRowCount = 0;
        revealedScore = 0;
        displayedScore = 0;
        displayedGrade = stats.GradeForScore(0); // lowest rank: rank and score are on screen from the start

        rowStartTimes = new float[revealRows.Count];
        for (int i = 0; i < rowStartTimes.Length; i++) rowStartTimes[i] = float.PositiveInfinity;

        // The whole block is written out immediately with every row transparent, so the text never
        // changes height mid-reveal: lines stay exactly where they land and the first one sits at the top.
        SetBodyText(RenderBody(0));
        SetHighestScore(stats.previousHighScore);
        if (newRecordText != null) newRecordText.SetActive(false);
        SetRankText(displayedGrade);
        SetScoreText(0);
        SetSkipHintVisible(true);

        revealSequence = DOTween.Sequence().SetUpdate(true);
        revealSequence.AppendInterval(Mathf.Max(0f, openDelay + revealStartDelay));

        for (int i = 0; i < revealRows.Count; i++)
        {
            revealSequence.AppendCallback(() => RevealNextRow(stats));
            revealSequence.AppendInterval(Mathf.Max(0.01f, revealInterval));
        }

        // Final snap: the clamped official total, its grade, and the high-score line. Clearing the stats
        // ends the skippable window, so the prompt goes away and a later click does nothing.
        revealSequence.AppendCallback(() =>
        {
            CountScoreTo(stats.TotalScore);
            SetGrade(stats.Grade);
            ApplyHighScoreTexts(stats);
            revealStats = null;
            SetSkipHintVisible(false);
        });
        revealSequence.OnKill(() => revealSequence = null);
    }

    private void RevealNextRow(MatchStats stats)
    {
        if (revealedRowCount >= revealRows.Count) return;

        var row = revealRows[revealedRowCount];
        rowStartTimes[revealedRowCount] = Time.unscaledTime;
        revealedRowCount++;
        SetBodyText(RenderBody(revealedRowCount));

        revealedScore += row.points;
        // Not floored: penalties sort first, so the score genuinely dips below zero before the positive
        // rows pull it back up, and the number has to agree with the line that just landed.
        CountScoreTo(revealedScore);
        SetGrade(stats.GradeForScore(revealedScore));
    }

    // Tweens the score number to its new value and punches the text, but only when it actually moved:
    // zero-point rows leave the score alone and must not trigger an animation.
    private void CountScoreTo(int target)
    {
        if (scoreText == null || target == displayedScore) return;

        scoreCountTween?.Kill();
        int from = displayedScore;
        displayedScore = target;

        float dur = Mathf.Min(scoreCountDuration, revealInterval);
        if (dur <= 0f)
        {
            SetScoreText(target);
        }
        else
        {
            scoreCountTween = DOVirtual.Float(from, target, dur, v => SetScoreText(Mathf.RoundToInt(v)))
                .SetUpdate(true);
        }

        Punch(scoreText.transform, scoreBaseScale, scorePunchStrength);
    }

    // Pushes new body text and re-caches the glyph geometry the appear animation offsets from. The
    // animation pass runs straight away so a freshly opaque row never flashes at full alpha for a frame.
    private void SetBodyText(string text)
    {
        if (bodyText == null) return;

        bodyText.text = text;
        bodyText.ForceMeshUpdate();
        cachedMeshInfo = bodyText.textInfo != null ? bodyText.textInfo.CopyMeshInfoVertexData() : null;
        animatingRows = true;
        animatingRows = ApplyRowAnimation();
    }

    private float RowProgress(int row)
    {
        if (rowStartTimes == null || row < 0 || row >= rowStartTimes.Length) return 1f;

        float start = rowStartTimes[row];
        if (float.IsPositiveInfinity(start)) return 0f;      // not revealed yet: stays transparent
        if (lineAppearDuration <= 0f) return 1f;
        return Mathf.Clamp01((Time.unscaledTime - start) / lineAppearDuration);
    }

    // Fades and lifts each line's glyphs by editing vertex data, i.e. after TMP has already laid the text
    // out. Rich-text offset tags (<voffset>) were not an option: they push every following line down too.
    // Returns whether any row is still mid-animation.
    private bool ApplyRowAnimation()
    {
        if (bodyText == null || cachedMeshInfo == null) return false;

        var info = bodyText.textInfo;
        if (info == null) return false;

        bool stillAnimating = false;
        int row = 0;

        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            // Hard line breaks survive in characterInfo, so counting them maps every glyph to its row
            // even when a long row soft-wraps onto a second line.
            if (ch.character == '\n') { row++; continue; }
            if (!ch.isVisible) continue;

            float t = RowProgress(row);
            if (t < 1f) stillAnimating = true;

            float eased = t >= 1f ? 1f : DOVirtual.EasedValue(0f, 1f, t, lineAppearEase);
            float dy = -lineRiseDistance * (1f - eased);

            int mat = ch.materialReferenceIndex;
            int v = ch.vertexIndex;
            if (mat >= cachedMeshInfo.Length) continue;

            Vector3[] verts = info.meshInfo[mat].vertices;
            Vector3[] srcVerts = cachedMeshInfo[mat].vertices;
            Color32[] cols = info.meshInfo[mat].colors32;
            Color32[] srcCols = cachedMeshInfo[mat].colors32;
            if (srcVerts == null || v + 3 >= srcVerts.Length) continue;

            for (int k = 0; k < 4; k++)
            {
                verts[v + k] = srcVerts[v + k] + new Vector3(0f, dy, 0f);
                Color32 c = srcCols[v + k];
                c.a = (byte)(c.a * eased);   // rows still queued are already alpha 0 in the source
                cols[v + k] = c;
            }
        }

        bodyText.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        return stillAnimating;
    }

    private void SetGrade(string grade)
    {
        if (grade == displayedGrade) return;
        displayedGrade = grade;
        SetRankText(grade);
        if (rankText != null) Punch(rankText.transform, rankBaseScale, rankPunchStrength);
    }

    private void Punch(Transform t, Vector3 baseScale, float strength)
    {
        if (t == null || strength <= 0f) return;
        t.DOKill();
        t.localScale = baseScale;
        t.DOPunchScale(baseScale * strength, punchDuration, punchVibrato, punchElasticity).SetUpdate(true);
    }

    private void StopReveal()
    {
        revealSequence?.Kill();
        revealSequence = null;
        scoreCountTween?.Kill();
        scoreCountTween = null;
        animatingRows = false;
        cachedMeshInfo = null;
        rowStartTimes = null;
        revealStats = null;
        SetSkipHintVisible(false);

        // Punches are killed mid-flight here, so restore the scales they were tweening away from.
        if (scoreText != null) { scoreText.transform.DOKill(); scoreText.transform.localScale = scoreBaseScale; }
        if (rankText != null) { rankText.transform.DOKill(); rankText.transform.localScale = rankBaseScale; }
    }

    private void SetSkipHintVisible(bool visible)
    {
        if (skipHint != null) skipHint.SetActive(visible);
    }

    private void SetRankText(string grade)
    {
        if (rankText == null) return;

        // Same guard as the config formats: a typo in the field must not throw at game over.
        try { rankText.text = string.Format(rankFormat, grade); }
        catch (System.FormatException) { rankText.text = grade; }
    }

    private void SetScoreText(int value)
    {
        if (scoreText != null) scoreText.text = value.ToString("N0");
    }

    // The highest-score line is on screen for the whole panel. During the count-up it shows the best as
    // it stood before this match: the stored best already includes this run, so on a record run printing
    // it up front would spoil the final total while the score is still climbing. It switches to the new
    // best at the end, which is also when the record flourish appears.
    private void SetHighestScore(int value)
    {
        if (highestScoreText == null) return;

        highestScoreText.text = string.Format(highestScoreFormat, value.ToString("N0"));
        highestScoreText.gameObject.SetActive(true);
    }

    private void ApplyHighScoreTexts(MatchStats stats)
    {
        SetHighestScore(stats.highScore);
        // Enabling the object is what starts its pop-and-pulse; the flourish lives on the object itself.
        if (newRecordText != null) newRecordText.SetActive(stats.isNewHighScore);
    }

    // Non-animated path: the finished panel, used in edit-mode preview and when the reveal is disabled.
    private void ApplyToTexts(MatchStats stats)
    {
        displayedScore = stats.TotalScore;
        displayedGrade = stats.Grade;
        animatingRows = false;
        cachedMeshInfo = null;
        rowStartTimes = null;
        revealStats = null;
        SetSkipHintVisible(false); // nothing is animating on this path, so there is nothing to skip

        SetRankText(stats.Grade);
        SetScoreText(stats.TotalScore);
        if (bodyText != null) bodyText.text = BuildBody(stats);
        ApplyHighScoreTexts(stats);
    }

    private string FormatRow(MatchStats.ScoreRow row, bool visible)
    {
        string col = pointsColumn.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string pts = row.points >= 0 ? "+" + row.points : row.points.ToString();
        Color c = row.points > 0 ? positivePointsColor
                : row.points < 0 ? negativePointsColor
                : neutralPointsColor;

        // Label stays plain default text; only the value carries the inspector style.
        string styledValue = ApplyStyle(row.value, valueTextColor, valueTextBold, valueTextItalic, valueTextFontSize, !visible);
        string styledPts = ApplyStyle(pts, c, pointsBold, pointsItalic, pointsFontSize, !visible);

        // The label has no color tag of its own, so a hidden row has to spell the face color out with a
        // zero alpha - every span is then explicitly transparent and nothing leaks into the next line.
        string label = row.label + ": ";
        if (!visible)
        {
            string faceHex = ColorUtility.ToHtmlStringRGB(bodyText != null ? bodyText.color : Color.white);
            label = $"<color=#{faceHex}00>{label}</color>";
        }

        // <pos> snaps the points to a fixed column so they all line up.
        return $"{label}{styledValue}<pos={col}%>{styledPts}";
    }

    // Draws every row, with the first visibleCount opaque and the rest transparent placeholders.
    private string RenderBody(int visibleCount)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < revealRows.Count; i++)
        {
            sb.AppendLine(FormatRow(revealRows[i], i < visibleCount));
        }
        return sb.ToString().TrimEnd();
    }

    private string BuildBody(MatchStats stats)
    {
        revealRows.Clear();
        revealRows.AddRange(SortedRows(stats));
        return RenderBody(revealRows.Count);
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
