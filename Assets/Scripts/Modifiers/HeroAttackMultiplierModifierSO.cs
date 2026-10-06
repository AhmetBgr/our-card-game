using UnityEngine;

/// <summary>
/// Multiplies both heroes' attack damage. Applied to the modal before passives register, so a passive
/// that adjusts attack (the Summoner's -2) lands on the scaled value, not under it.
/// </summary>
[CreateAssetMenu(fileName = "HeroAttackMultiplier", menuName = "Game Modes/Modifiers/Hero Attack Multiplier")]
public class HeroAttackMultiplierModifierSO : IntModifierSO
{
    public override void OnHeroInitialized(MinionController hero, SelectionSide side, float value)
    {
        int factor = Mathf.RoundToInt(value);
        if (hero.modal == null || factor == 1) return;

        hero.modal.attack *= factor;
    }
}
