using UnityEngine;

/// <summary>
/// Inspector-editable definition of which words in a card description get highlighted and how.
/// Loaded at render time by <see cref="CardTextFormatter"/> and applied in <see cref="CardView"/>.
/// The card .asset files stay plain text; all styling lives here so it can be tuned without
/// recompiling. Keyword matching is case-insensitive and multi-word phrases are allowed.
/// </summary>
[CreateAssetMenu(menuName = "Cards/Text Highlight Config", fileName = "CardTextHighlightConfig")]
public class CardTextHighlightConfig : ScriptableObject
{
    [System.Serializable]
    public class HighlightGroup
    {
        [Tooltip("Words/phrases to highlight. Matched case-insensitively; multi-word phrases allowed.")]
        public string[] keywords;

        [Tooltip("Off = leave these words in whatever color the label itself is set to (the prefab's text color). Bold still applies.")]
        public bool useColor = true;
        public Color color = Color.white;
        public bool bold = true;
    }

    // A single keyword with its own color, so each stat word can be styled independently.
    [System.Serializable]
    public class KeywordStyle
    {
        [Tooltip("Word/phrase to highlight. Matched case-insensitively; multi-word phrases allowed.")]
        public string keyword;

        [Tooltip("Off = leave this word in whatever color the label itself is set to (the prefab's text color). Bold still applies.")]
        public bool useColor = true;
        public Color color = Color.white;
        public bool bold = true;
    }

    [Header("Trigger phrases (share one color)")]
    public HighlightGroup triggerPhrases = new HighlightGroup();

    [Header("Stat keywords (each has its own color)")]
    public KeywordStyle[] statKeywords;

    [Header("Numbers (3, +1/+1, +2, ...)")]
    public bool highlightNumbers = true;

    [Tooltip("Off = leave numbers in whatever color the label itself is set to (the prefab's text color). Bold still applies. Note this only covers numbers that do NOT sit next to a stat keyword — those adopt the stat's own color and toggle.")]
    public bool useNumberColor = true;
    public Color numberColor = Color.cyan;
    public bool numberBold = true;
}
