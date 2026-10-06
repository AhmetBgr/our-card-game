using UnityEngine;

/// <summary>A whole-number modifier in [min, max].</summary>
public abstract class IntModifierSO : GameModifierSO
{
    public int min = 1;
    public int max = 5;
    public int defaultValue = 1;

    [Tooltip("Row text; {0} is the value.")]
    public string format = "x{0}";

    public override ModifierValueKind Kind => ModifierValueKind.Int;
    public override float DefaultValue => Mathf.Clamp(defaultValue, min, max);
    public override float Clamp(float value) => Mathf.Clamp(Mathf.RoundToInt(value), min, max);
    public override string FormatValue(float value) => string.Format(format, Mathf.RoundToInt(value));
}
