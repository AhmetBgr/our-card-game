using TMPro;
using UnityEngine;

/// <summary>
/// One row of the modifiers popup: a label, a value caption and (in the subclasses) the control. Rows
/// write straight through to <see cref="MatchModifiers"/> and re-read from it on Changed, so the deck
/// panels behind the popup follow every flick as it happens.
/// </summary>
public abstract class ModifierRowView : MonoBehaviour
{
    [SerializeField] protected TMP_Text label;
    [SerializeField] protected TMP_Text valueLabel;

    [Tooltip("Optional. Its message is set to the modifier's description.")]
    [SerializeField] protected UITooltipTrigger tooltip;

    public GameModifierSO Modifier { get; private set; }

    public virtual void Bind(GameModifierSO modifier)
    {
        Modifier = modifier;

        if (label != null) label.text = modifier.displayName;
        if (tooltip != null) tooltip.Message = modifier.description;

        Refresh();
    }

    /// <summary>Shows the current saved value without reporting it back.</summary>
    public virtual void Refresh()
    {
        if (Modifier == null) return;
        if (valueLabel != null) valueLabel.text = Modifier.FormatValue(MatchModifiers.GetValue(Modifier));
    }
}
