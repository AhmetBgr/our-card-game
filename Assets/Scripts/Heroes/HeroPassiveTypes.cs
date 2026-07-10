using System;

/// <summary>
/// Every trigger the Heroes design sheet needs. Only the ones HeroPassiveSystem currently subscribes
/// to are live; the rest are declared up front so passive assets authored later don't force an enum
/// renumber (which would silently remap every existing .asset).
/// </summary>
public enum HeroPassiveTrigger
{
    HeroTookDamage,
    HeroAttacked,
    OwnerTurnStart,
    OwnerTurnEnd,
    EveryNOwnerTurns,
    AnyMinionDied,
    AnyMinionSummoned,
    AnyMinionCollided,
    OwnerDrewCard,
    MinionMovedOntoTile,
    Continuous,
}

public enum HeroArchetype
{
    None, Berserker, Summoner, Necromancer, Thief, Hunter, Gambler, Witch, Priest
}

/// <summary>Which side a triggering minion must be on, relative to the hero that owns the passive.</summary>
public enum PassiveSide { Any, Friendly, Enemy }

[Serializable]
public struct MinionFilter
{
    public PassiveSide side;

    /// <summary>Minimum attack, 0 = no constraint.</summary>
    public int minAttack;

    /// <summary>
    /// Minimum MAX health (defHealth), 0 = no constraint. Deliberately not current health: on the
    /// AnyMinionDied trigger the subject's health is already &lt;= 0, so "a minion with 3+ health dies"
    /// can only mean its printed health.
    /// </summary>
    public int minMaxHealth;

    public bool Matches(MinionController minion, Agent heroOwner)
    {
        if (minion == null || minion.modal == null) return false;

        switch (side)
        {
            case PassiveSide.Friendly when minion.owner != heroOwner: return false;
            case PassiveSide.Enemy when minion.owner == heroOwner: return false;
        }

        if (minAttack > 0 && minion.modal.attack < minAttack) return false;
        if (minMaxHealth > 0 && minion.modal.defHealth < minMaxHealth) return false;

        return true;
    }
}

/// <summary>
/// What happened, from the point of view of one hero. Built per-hero by HeroPassiveSystem so that
/// PassiveSide.Friendly/Enemy resolve relative to that hero's owner rather than to whose turn it is.
/// </summary>
public readonly struct HeroPassiveContext
{
    /// <summary>The hero that owns the passive being evaluated.</summary>
    public readonly MinionController hero;
    public readonly Agent owner;

    /// <summary>The minion the event is about: who died, was summoned, collided, took damage.</summary>
    public readonly MinionController subject;

    /// <summary>The second party of a collision.</summary>
    public readonly MinionController other;

    /// <summary>Damage dealt, for damage triggers.</summary>
    public readonly int amount;

    /// <summary>How many turns this hero's owner has taken, for EveryNOwnerTurns.</summary>
    public readonly int ownerTurnNumber;

    public HeroPassiveContext(MinionController hero, MinionController subject = null,
        MinionController other = null, int amount = 0, int ownerTurnNumber = 0)
    {
        this.hero = hero;
        this.owner = hero != null ? hero.owner : null;
        this.subject = subject;
        this.other = other;
        this.amount = amount;
        this.ownerTurnNumber = ownerTurnNumber;
    }
}
