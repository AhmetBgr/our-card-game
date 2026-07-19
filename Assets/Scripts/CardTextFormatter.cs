using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Turns a plain card description into a TextMeshPro rich-text string by wrapping configured
/// keywords, trigger phrases and numbers in &lt;color&gt;/&lt;b&gt; tags.
///
/// A single compiled Regex (cached per config) finds every token in one pass, so tags can never
/// nest or overlap. Keyword alternatives are ordered longest-first, so a multi-word phrase like
/// "On Death" wins over a bare "Death". Matching is case-insensitive but the original casing of
/// the matched text is preserved in the output.
///
/// A number that sits directly next to a stat keyword (only whitespace between, e.g. "+2 Attack",
/// "Deal 3 damage") adopts that stat's color instead of the generic number color, so the amount
/// and its stat read as one unit. A stat pair like "+2/+2" or "-2/-1" is split: the left number
/// takes the Attack color and the right number the Health color (the "X/Y" = attack/health idiom).
///
/// Text wrapped in underscores in the description (_like this_) is rendered italic. This is an
/// authoring marker for any phrase, independent of keywords.
/// </summary>
public static class CardTextFormatter
{
    // The number token matches bare ints and stat pairs: 3, +2, -1, +1/+1, 2/3.
    private const string NumberPattern = @"[+\-]?\d+(?:/[+\-]?\d+)?";

    // Author italic marker: _text_ -> <i>text</i>. Non-greedy run of non-underscore chars, so an odd
    // stray underscore is left as-is and paired markers on the same line each italicize independently.
    private static readonly Regex ItalicMarker = new Regex(@"_([^_]+)_");

    // Resolved color + bold for one matched keyword. Trigger phrases (grouped, one color) and stat
    // keywords (each its own color) both collapse to this, so matches are handled uniformly.
    // isStat marks stat keywords so an adjacent number can borrow their style.
    private struct Style
    {
        public Color color;
        public bool bold;
        public bool isStat;
        public Style(Color color, bool bold, bool isStat)
        {
            this.color = color;
            this.bold = bold;
            this.isStat = isStat;
        }
    }

    // Cache the compiled regex + lookup so we don't rebuild them every card render. Keyed by the
    // config instance so a different (or re-tuned) config rebuilds cleanly.
    private static CardTextHighlightConfig cachedConfig;
    private static Regex cachedRegex;
    private static Dictionary<string, Style> cachedLookup;

    // Styles for the two halves of an "X/Y" stat pair, resolved from the Attack/Health stat keywords.
    private static Style cachedAttackStyle;
    private static Style cachedHealthStyle;
    private static bool hasAttackStyle;
    private static bool hasHealthStyle;

    private static Style NumberStyle => new Style(cachedConfig.numberColor, cachedConfig.numberBold, false);

    public static string Format(string desc, CardTextHighlightConfig config)
    {
        if (string.IsNullOrEmpty(desc) || config == null)
            return desc;

        // Convert author italic markers (_like this_) to <i> tags first, BEFORE keyword matching:
        // '_' is a regex word char, so a \b keyword boundary would fail inside _..._. Turning the
        // markers into tags (whose <> are non-word chars) lets italic and highlighting compose.
        string text = ApplyItalicMarkers(desc);

        EnsureCompiled(config);

        if (cachedRegex == null)
            return text; // No keywords configured and numbers off -> italics only.

        MatchCollection matches = cachedRegex.Matches(text);
        if (matches.Count == 0)
            return text;

        // Resolve the style of every token up front: a configured keyword uses its own style; any
        // other match is a number and starts on the generic number style (may be reassigned below).
        int count = matches.Count;
        var tokens = new Match[count];
        var styles = new Style[count];
        var isNumber = new bool[count];
        var numberStyle = new Style(config.numberColor, config.numberBold, false);
        for (int i = 0; i < count; i++)
        {
            tokens[i] = matches[i];
            if (cachedLookup.TryGetValue(matches[i].Value.ToLowerInvariant(), out var style))
                styles[i] = style;
            else
            {
                styles[i] = numberStyle;
                isNumber[i] = true;
            }
        }

        // A number adopts the color of a stat keyword it directly touches (only whitespace between).
        // Prefer the following keyword ("+2 Attack", "3 damage"); fall back to a preceding one
        // ("Heal 5") so trailing amounts pick up their stat too.
        for (int i = 0; i < count; i++)
        {
            if (!isNumber[i]) continue;

            if (i + 1 < count && styles[i + 1].isStat && OnlyWhitespaceBetween(text, tokens[i], tokens[i + 1]))
                styles[i] = styles[i + 1];
            else if (i - 1 >= 0 && styles[i - 1].isStat && OnlyWhitespaceBetween(text, tokens[i - 1], tokens[i]))
                styles[i] = styles[i - 1];
        }

        // Rebuild the string, copying the gaps verbatim and wrapping each token.
        var sb = new StringBuilder(text.Length + count * 24);
        int cursor = 0;
        for (int i = 0; i < count; i++)
        {
            Match m = tokens[i];
            sb.Append(text, cursor, m.Index - cursor);
            // A stat pair ("X/Y") is split into Attack/Health colors; anything else uses its own style.
            if (!(isNumber[i] && TryAppendStatPair(sb, m.Value)))
                AppendWrapped(sb, m.Value, styles[i]);
            cursor = m.Index + m.Length;
        }
        sb.Append(text, cursor, text.Length - cursor);
        return sb.ToString();
    }

    // Turns _underscored_ spans into <i>...</i>. Cheap early-out when the text has no underscore.
    private static string ApplyItalicMarkers(string desc)
    {
        if (desc.IndexOf('_') < 0)
            return desc;
        return ItalicMarker.Replace(desc, "<i>$1</i>");
    }

    // Renders an "X/Y" stat pair as Attack-colored X, a plain slash, and Health-colored Y.
    // Returns false (and appends nothing) when the value is a plain number with no slash.
    private static bool TryAppendStatPair(StringBuilder sb, string value)
    {
        int slash = value.IndexOf('/');
        if (slash < 0)
            return false;

        AppendWrapped(sb, value.Substring(0, slash), hasAttackStyle ? cachedAttackStyle : NumberStyle);
        sb.Append('/');
        AppendWrapped(sb, value.Substring(slash + 1), hasHealthStyle ? cachedHealthStyle : NumberStyle);
        return true;
    }

    // True if the text between two adjacent (earlier, later) matches is empty or all whitespace.
    private static bool OnlyWhitespaceBetween(string desc, Match earlier, Match later)
    {
        int start = earlier.Index + earlier.Length;
        int end = later.Index;
        for (int i = start; i < end; i++)
            if (!char.IsWhiteSpace(desc[i]))
                return false;
        return true;
    }

    private static void EnsureCompiled(CardTextHighlightConfig config)
    {
        // cachedLookup is always populated once we've compiled for a config (even if the resulting
        // regex is null), so it's a reliable "already compiled for this config" sentinel.
        if (ReferenceEquals(cachedConfig, config) && cachedLookup != null)
            return;

        cachedConfig = config;
        cachedLookup = new Dictionary<string, Style>();

        // Collect every keyword, longest-first, so phrases beat the single words they contain.
        var keywords = new List<string>();

        var trigger = config.triggerPhrases;
        if (trigger != null && trigger.keywords != null)
            foreach (var kw in trigger.keywords)
                AddKeyword(kw, new Style(trigger.color, trigger.bold, false), keywords);

        if (config.statKeywords != null)
            foreach (var stat in config.statKeywords)
                if (stat != null)
                    AddKeyword(stat.keyword, new Style(stat.color, stat.bold, true), keywords);

        // Resolve the pair-half colors from the Attack/Health stat keywords (case-insensitive).
        hasAttackStyle = cachedLookup.TryGetValue("attack", out cachedAttackStyle);
        hasHealthStyle = cachedLookup.TryGetValue("health", out cachedHealthStyle);

        keywords.Sort((a, b) => b.Length.CompareTo(a.Length));

        var alternatives = new List<string>();
        foreach (var kw in keywords)
            alternatives.Add($@"\b{Regex.Escape(kw)}\b");

        if (config.highlightNumbers)
            alternatives.Add(NumberPattern);

        if (alternatives.Count == 0)
        {
            cachedRegex = null;
            return;
        }

        cachedRegex = new Regex(string.Join("|", alternatives), RegexOptions.IgnoreCase);
    }

    private static void AddKeyword(string keyword, Style style, List<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return;

        string key = keyword.ToLowerInvariant();
        if (cachedLookup.ContainsKey(key))
            return; // First definition wins on duplicates.

        cachedLookup[key] = style;
        keywords.Add(keyword);
    }

    private static void AppendWrapped(StringBuilder sb, string content, Style style)
    {
        string hex = ColorUtility.ToHtmlStringRGB(style.color);
        if (style.bold) sb.Append("<b>");
        sb.Append("<color=#").Append(hex).Append('>').Append(content).Append("</color>");
        if (style.bold) sb.Append("</b>");
    }
}
