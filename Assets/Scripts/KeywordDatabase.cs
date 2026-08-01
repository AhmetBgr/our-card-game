using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Answers "which keyword mechanics does this card description mention?" for the hover tooltip
/// system. Definitions live in <see cref="CardTextHighlightConfig.tooltipKeywords"/> — the same
/// Resources-loaded asset that drives text highlighting, so tooltips and highlighting can never
/// drift apart.
///
/// Matching mirrors <see cref="CardTextFormatter"/>: one compiled case-insensitive regex over all
/// match phrases, longest-first so "On Death" beats a bare "Death", \b-bounded. The formatter's own
/// regex is not reused because it also matches numbers and stat-styling words and maps to styles,
/// not definitions.
///
/// Results are cached per description string (a card's tooltip list never changes while its text
/// doesn't), so hover after the first costs a dictionary lookup.
/// </summary>
public static class KeywordDatabase
{
    private static readonly IReadOnlyList<CardTextHighlightConfig.KeywordDefinition> Empty =
        new CardTextHighlightConfig.KeywordDefinition[0];

    private static CardTextHighlightConfig config;
    private static bool configSearched;

    // Compiled matcher, keyed by the config instance so a re-tuned/reloaded config rebuilds cleanly.
    private static CardTextHighlightConfig compiledFor;
    private static Regex phraseRegex;
    private static Dictionary<string, CardTextHighlightConfig.KeywordDefinition> phraseLookup;

    private static readonly Dictionary<string, IReadOnlyList<CardTextHighlightConfig.KeywordDefinition>> resultCache =
        new Dictionary<string, IReadOnlyList<CardTextHighlightConfig.KeywordDefinition>>();

    /// <summary>
    /// The keyword definitions mentioned in <paramref name="desc"/>, deduped, in first-appearance
    /// order. Empty (never null) when the desc is empty or mentions no known keyword.
    /// </summary>
    public static IReadOnlyList<CardTextHighlightConfig.KeywordDefinition> GetKeywords(string desc)
    {
        if (string.IsNullOrEmpty(desc))
            return Empty;

        EnsureCompiled();
        if (phraseRegex == null)
            return Empty;

        if (resultCache.TryGetValue(desc, out var cached))
            return cached;

        // Author italic markers (_like this_) are stripped before matching: '_' is a regex word
        // char, so a \b boundary would fail against a phrase butted up to a marker.
        string text = desc.IndexOf('_') >= 0 ? desc.Replace("_", " ") : desc;

        List<CardTextHighlightConfig.KeywordDefinition> found = null;
        foreach (Match m in phraseRegex.Matches(text))
        {
            if (!phraseLookup.TryGetValue(m.Value.ToLowerInvariant(), out var def))
                continue;

            found ??= new List<CardTextHighlightConfig.KeywordDefinition>();
            if (!found.Contains(def))
                found.Add(def);
        }

        IReadOnlyList<CardTextHighlightConfig.KeywordDefinition> result = found ?? Empty;
        resultCache[desc] = result;
        return result;
    }

    private static void EnsureCompiled()
    {
        if (!configSearched || config == null)
        {
            config = Resources.Load<CardTextHighlightConfig>("CardTextHighlightConfig");
            configSearched = true;
        }

        if (config == null)
            return;

        // phraseLookup doubles as the "already compiled for this config" sentinel.
        if (ReferenceEquals(compiledFor, config) && phraseLookup != null)
            return;

        compiledFor = config;
        phraseLookup = new Dictionary<string, CardTextHighlightConfig.KeywordDefinition>();
        resultCache.Clear();

        var phrases = new List<string>();
        if (config.tooltipKeywords != null)
        {
            foreach (var def in config.tooltipKeywords)
            {
                if (def == null || def.matchPhrases == null)
                    continue;

                foreach (var phrase in def.matchPhrases)
                {
                    if (string.IsNullOrWhiteSpace(phrase))
                        continue;

                    string key = phrase.ToLowerInvariant();
                    if (phraseLookup.ContainsKey(key))
                        continue; // First definition wins on duplicate phrases.

                    phraseLookup[key] = def;
                    phrases.Add(phrase);
                }
            }
        }

        if (phrases.Count == 0)
        {
            phraseRegex = null;
            return;
        }

        // Longest-first so multi-word phrases beat the single words they contain.
        phrases.Sort((a, b) => b.Length.CompareTo(a.Length));

        var alternatives = new string[phrases.Count];
        for (int i = 0; i < phrases.Count; i++)
            alternatives[i] = @"\b" + Regex.Escape(phrases[i]) + @"\b";

        phraseRegex = new Regex(string.Join("|", alternatives), RegexOptions.IgnoreCase);
    }
}
