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

    /// <summary>
    /// The passives this hero actually plays with: the HeroSO's own, plus any extras the setup screen
    /// added (see MatchModifiers.ExtraPassivesEnabled). Everything that dispatches or renders passives
    /// reads THIS list, never heroSO.passives, so the two can differ without anything disagreeing.
    /// </summary>
    public List<HeroPassiveSO> passives = new List<HeroPassiveSO>();

    /// <summary>Turns this hero's owner has started. Drives EveryNOwnerTurns.</summary>
    public int ownerTurnNumber;

    /// <summary>Attack the Berserker passive has already granted. Only ever grows (the bonus is permanent).</summary>
    public int appliedAttackBonus;

    private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

    private readonly HashSet<HeroPassiveSO> _selfStatsApplied = new HashSet<HeroPassiveSO>();

    /// <summary>
    /// Claims the one-time self-stat stamp for a passive (HeroPassiveSO.ApplyToOwnHero): true the first
    /// time it is asked for this hero, false ever after. Register can run more than once for the same
    /// hero, and those stamps are raw += on the statline — without this a re-registration would apply
    /// the Summoner's -2 Attack twice.
    /// </summary>
    public bool ClaimSelfStatApply(HeroPassiveSO passive) => passive != null && _selfStatsApplied.Add(passive);

    public int GetCounter(string key) => _counters.TryGetValue(key, out int v) ? v : 0;

    /// <summary>
    /// Whether the key has ever been written. Distinguishes "counted down to 0" from "never started",
    /// which GetCounter alone cannot — the indicator renders those two states very differently.
    /// </summary>
    public bool HasCounter(string key) => _counters.ContainsKey(key);

    public void SetCounter(string key, int value) => _counters[key] = value;

    /// <summary>Every counter written so far. Read by GauntletRun to carry passive state into the next match.</summary>
    public IReadOnlyDictionary<string, int> Counters => _counters;

    /// <summary>Finds the runtime state for a hero, or null if it was never registered.</summary>
    public static HeroRuntime For(MinionController hero)
        => hero != null ? hero.GetComponent<HeroRuntime>() : null;
}
