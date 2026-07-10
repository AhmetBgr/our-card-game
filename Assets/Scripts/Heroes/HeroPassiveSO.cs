using UnityEngine;

/// <summary>
/// A single hero passive, authored as its own asset so it can be swapped between heroes.
///
/// Most passives are TriggeredHeroPassiveSO: a trigger + a filter + a UnityEvent of ActionHolder
/// verbs, wired in the Inspector exactly like CardSO.OnPlay. Passives that need per-hero state or
/// recompute-and-unwind semantics (dodge every third attack, the melee-to-ranged aura) subclass this
/// directly and write C# against HeroRuntime instead.
/// </summary>
public abstract class HeroPassiveSO : ScriptableObject
{
    public string passiveName;
    [TextArea] public string description;

    public abstract HeroPassiveTrigger Trigger { get; }

    /// <summary>Extra gate beyond the trigger itself. Evaluated before any ActionHolder state is set up.</summary>
    public virtual bool Matches(in HeroPassiveContext ctx) => true;

    /// <summary>
    /// Called inline from GameManager's trigger coroutine (e.g. InvokeOnMinionTookDamageActions) *after*
    /// the ActionHolder registers are set for this hero, so verbs enqueued here land on the queue
    /// GameManager is already draining. Same contract as minion.modal.OnDeath.Invoke().
    /// </summary>
    public abstract void Run(in HeroPassiveContext ctx);
}
