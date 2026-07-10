using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-hero mutable state for passives. Added via AddComponent when the hero is registered, so
/// Hero.prefab needs no change.
///
/// This exists because ActionHolder is a single shared ScriptableObject: it can hold the *current*
/// selection, but it can never hold anything per-hero and per-match. Anything a passive must remember
/// across invocations (Berserker's granted attack, dodge counts, mark counts) belongs here.
/// </summary>
public class HeroRuntime : MonoBehaviour
{
    public MinionController hero;
    public HeroSO heroSO;

    /// <summary>Turns this hero's owner has started. Drives EveryNOwnerTurns.</summary>
    public int ownerTurnNumber;

    /// <summary>Attack the Berserker passive has already granted. Only ever grows (the bonus is permanent).</summary>
    public int appliedAttackBonus;

    private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

    public int GetCounter(string key) => _counters.TryGetValue(key, out int v) ? v : 0;

    public void SetCounter(string key, int value) => _counters[key] = value;

    /// <summary>Finds the runtime state for a hero, or null if it was never registered.</summary>
    public static HeroRuntime For(MinionController hero)
        => hero != null ? hero.GetComponent<HeroRuntime>() : null;
}
