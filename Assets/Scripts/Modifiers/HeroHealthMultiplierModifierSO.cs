using UnityEngine;

/// <summary>
/// Scales one side's hero max health. One class, one asset per side: the side is authored on the asset
/// so the player and opponent rows are the same code with a different field.
/// </summary>
[CreateAssetMenu(fileName = "HeroHealthMultiplier", menuName = "Game Modes/Modifiers/Hero Health Multiplier")]
public class HeroHealthMultiplierModifierSO : FloatModifierSO
{
    [Tooltip("Whose hero this scales.")]
    public SelectionSide side = SelectionSide.Player;

    public override void OnHeroInitialized(MinionController hero, SelectionSide heroSide, float value)
    {
        if (heroSide != side || hero.modal == null) return;
        if (Mathf.Approximately(value, 1f)) return;

        // Scaled off the authored max so the multiplier is a multiplier, not a compounding +=.
        int hp = Mathf.Max(1, Mathf.RoundToInt(hero.modal.defHealth * value));
        hero.modal.defHealth = hp;
        hero.modal.health = hp;
    }
}
