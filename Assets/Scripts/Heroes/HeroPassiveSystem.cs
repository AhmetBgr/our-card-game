using System.Collections.Generic;

/// <summary>
/// Registry + matcher for hero passives. It does NOT subscribe to any events.
///
/// An earlier version subscribed to the same static events GameManager does (OnTookDamage, OnDied, …)
/// and dispatched passives as their own triggered actions. That races GameManager: GameManager's
/// handler runs first, opens a triggered-action scope, and yields without closing it; the passive
/// dispatch then sees _executingTriggeredActions == true, gets deferred into the pending queue, and is
/// later started from inside GameManager's still-open PushScope — whose Restore() then wipes the
/// ActionHolder registers the passive is mid-selection on. The passive silently selects nothing.
///
/// The robust model is the opposite: GameManager owns the trigger scope, and calls into this registry
/// INLINE from within its existing per-event coroutines (see GameManager.InvokeOnMinionTookDamageActions).
/// Passive verbs enqueue onto GameManager's own action queue and drain in the same single pass — no
/// second triggered action, no race, no clobber. New triggers are added the same way: dispatch inline
/// from the matching GameManager coroutine, never via a parallel event subscription.
/// </summary>
public class HeroPassiveSystem
{
    private readonly List<HeroRuntime> _heroes = new List<HeroRuntime>();

    public IReadOnlyList<HeroRuntime> Heroes => _heroes;

    /// <summary>
    /// Attaches runtime state to a hero whose card is a HeroSO with passives. Heroes without passives
    /// are skipped, so a plain CardSO hero keeps behaving exactly as before.
    /// </summary>
    public void Register(MinionController hero)
    {
        if (hero == null) return;
        if (!(hero.card is HeroSO heroSO) || heroSO.passives == null || heroSO.passives.Count == 0) return;

        HeroRuntime runtime = hero.GetComponent<HeroRuntime>();
        if (runtime == null) runtime = hero.gameObject.AddComponent<HeroRuntime>();

        runtime.hero = hero;
        runtime.heroSO = heroSO;

        if (!_heroes.Contains(runtime)) _heroes.Add(runtime);
    }

    public void Clear() => _heroes.Clear();

    /// <summary>The runtime for a minion if it is a registered hero, else null.</summary>
    public HeroRuntime GetRuntime(MinionController minion)
    {
        if (minion == null) return null;
        for (int i = 0; i < _heroes.Count; i++)
            if (_heroes[i] != null && _heroes[i].hero == minion) return _heroes[i];
        return null;
    }

    /// <summary>
    /// Appends the passives on this hero that fire for the given trigger and pass their filter.
    /// Caller supplies the list to avoid allocating when nothing matches.
    /// </summary>
    public void CollectMatching(HeroRuntime runtime, HeroPassiveTrigger trigger, in HeroPassiveContext ctx,
        List<HeroPassiveSO> into)
    {
        if (runtime == null || runtime.heroSO == null) return;

        List<HeroPassiveSO> passives = runtime.heroSO.passives;
        for (int i = 0; i < passives.Count; i++)
        {
            HeroPassiveSO passive = passives[i];
            if (passive == null || passive.Trigger != trigger) continue;
            if (!passive.Matches(ctx)) continue;
            into.Add(passive);
        }
    }
}
