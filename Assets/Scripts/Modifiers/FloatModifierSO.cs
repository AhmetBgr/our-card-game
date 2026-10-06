using UnityEngine;

/// <summary>A fractional modifier in [min, max], snapped to <see cref="step"/>.</summary>
public abstract class FloatModifierSO : GameModifierSO
{
    public float min = 0.1f;
    public float max = 3f;
    public float step = 0.1f;
    public float defaultValue = 1f;

    [Tooltip("Row text; {0} is the value.")]
    public string format = "x{0:0.0}";

    public override ModifierValueKind Kind => ModifierValueKind.Float;
    public override float DefaultValue => Mathf.Clamp(defaultValue, min, max);

    public override float Clamp(float value)
    {
        if (step > 0f) value = Mathf.Round(value / step) * step;
        return Mathf.Clamp(value, min, max);
    }

    public override string FormatValue(float value) => string.Format(format, value);
}
