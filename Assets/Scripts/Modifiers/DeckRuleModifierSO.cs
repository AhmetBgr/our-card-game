using UnityEngine;

/// <summary>
/// A toggle that relaxes one deck-building rule. Which rule, and how far, is authored on the asset, so
/// the four deck toggles are four assets of this one class.
/// </summary>
[CreateAssetMenu(fileName = "DeckRule", menuName = "Game Modes/Modifiers/Deck Rule")]
public class DeckRuleModifierSO : BoolModifierSO
{
    public enum Rule
    {
        AllowDuplicates,
        AllowUpgraded,
        /// <summary>Raises the max deck size to <see cref="sizeBound"/>.</summary>
        AllowOversize,
        /// <summary>Lowers the min deck size to <see cref="sizeBound"/>.</summary>
        AllowUndersize
    }

    public Rule rule;

    [Tooltip("The new max (AllowOversize) or min (AllowUndersize) deck size while the toggle is on.")]
    public int sizeBound = 30;

    public override void ModifyDeckRules(ref DeckRules rules, float value)
    {
        if (!IsOn(value)) return;

        switch (rule)
        {
            case Rule.AllowDuplicates: rules.allowDuplicates = true; break;
            case Rule.AllowUpgraded: rules.allowUpgraded = true; break;
            case Rule.AllowOversize: rules.maxSize = Mathf.Max(rules.maxSize, sizeBound); break;
            case Rule.AllowUndersize: rules.minSize = Mathf.Max(1, Mathf.Min(rules.minSize, sizeBound)); break;
        }
    }
}
