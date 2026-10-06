using UnityEngine;

/// <summary>An on/off modifier. Stored as 0/1.</summary>
public abstract class BoolModifierSO : GameModifierSO
{
    [Tooltip("State the modifier starts in for a fresh profile.")]
    public bool defaultOn;

    public override ModifierValueKind Kind => ModifierValueKind.Bool;
    public override float DefaultValue => defaultOn ? 1f : 0f;
    public override float Clamp(float value) => value > 0.5f ? 1f : 0f;
    public override string FormatValue(float value) => IsOn(value) ? "On" : "Off";

    public static bool IsOn(float value) => value > 0.5f;
}
