using UnityEngine;

/// <summary>
/// Aura hero passive: raises the collisionDamage stat of the hero's FRIENDLY minions to `amount`
/// (default 1), so they deal that much to any enemy minion they collide with. The damage itself is
/// applied intrinsically by GameManager.ApplyCollisionDamage from each minion's collisionDamage stat —
/// this passive only stamps the stat, it does not fire on a trigger.
///
/// Subclasses HeroPassiveSO directly rather than being a TriggeredHeroPassiveSO: it is a continuous stat
/// aura, not a trigger + verb list, and the base system has no declarative aura primitive. The grant is
/// applied via ApplyAuraOnSummon as friendly minions are summoned (and to any already on the board when
/// the hero is registered). Because heroes are registered before any minion is summoned, this covers
/// every friendly minion for the whole match.
/// </summary>
[CreateAssetMenu(fileName = "CollisionDamageAura", menuName = "Cards/Hero Passive - Collision Damage Aura")]
public class CollisionDamageAuraHeroPassiveSO : HeroPassiveSO
{
    [Tooltip("Collision damage granted to each friendly minion. The stat is raised to this value, never lowered.")]
    [SerializeField] private int amount = 1;

    // Not trigger-dispatched; the aura is applied through ApplyAuraOnSummon. Continuous is the honest label.
    public override HeroPassiveTrigger Trigger => HeroPassiveTrigger.Continuous;

    // Aura, so nothing runs on a trigger.
    public override void Run(in HeroPassiveContext ctx) { }

    public override void ApplyAuraOnSummon(MinionController minion, Agent heroOwner)
    {
        if (minion == null || minion.modal == null) return;
        if (minion.owner != heroOwner) return; // friendly minions only

        if (minion.modal.collisionDamage < amount)
            minion.modal.collisionDamage = amount;
    }

    /// <summary>
    /// Badge is the granted amount; the icon dims while the aura has nobody to affect. Read-only —
    /// it inspects owner.minions but mutates nothing, per the GetDisplay purity contract.
    /// </summary>
    public override HeroPassiveDisplay GetDisplay(HeroRuntime runtime)
    {
        HeroPassiveDisplay display = base.GetDisplay(runtime);
        if (!display.visible) return display;

        Agent owner = runtime != null && runtime.hero != null ? runtime.hero.owner : null;
        bool hasFriendlyMinions = owner != null && owner.minions != null && owner.minions.Count > 0;

        return display.WithBadge(amount.ToString()).WithActive(hasFriendlyMinions);
    }
}
