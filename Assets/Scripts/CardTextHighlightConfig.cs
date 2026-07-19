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
        public Color color = Color.white;
        public bool bold = true;
    }

    // A single keyword with its own color, so each stat word can be styled independently.
    [System.Serializable]
    public class KeywordStyle
    {
        [Tooltip("Word/phrase to highlight. Matched case-insensitively; multi-word phrases allowed.")]
        public string keyword;
        public Color color = Color.white;
        public bool bold = true;
    }

    [Header("Trigger phrases (share one color)")]
    public HighlightGroup triggerPhrases = new HighlightGroup();

    [Header("Stat keywords (each has its own color)")]
    public KeywordStyle[] statKeywords;

    [Header("Numbers (3, +1/+1, +2, ...)")]
    public bool highlightNumbers = true;
    public Color numberColor = Color.cyan;
    public bool numberBold = true;
}
