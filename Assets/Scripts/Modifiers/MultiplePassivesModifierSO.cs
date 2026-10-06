using UnityEngine;

/// <summary>
/// While on, each side's setup panel shows a passive picker under the hero carousel and the match
/// registers the picked passives on top of the hero's own.
/// </summary>
[CreateAssetMenu(fileName = "MultiplePassives", menuName = "Game Modes/Modifiers/Multiple Passives")]
public class MultiplePassivesModifierSO : BoolModifierSO
{
    public override bool EnablesExtraPassives(float value) => IsOn(value);
}
