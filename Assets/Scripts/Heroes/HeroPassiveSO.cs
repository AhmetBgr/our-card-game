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

    [Header("Indicator")]
    [Tooltip("Shown on the hero's passive indicator row. Leave empty to render no indicator.")]
    public Sprite icon;

    [Tooltip("Uncheck for passives that should stay invisible to the player.")]
    public bool showIndicator = true;

    [Tooltip("Which indicator shape this passive draws: bare icon, aura outline, or countdown ring.")]
    public PassiveIndicatorType indicatorType = PassiveIndicatorType.Other;

    [Tooltip("Where the countdown value comes from. Only used when indicatorType is Counter.")]
    public PassiveCounterSource counterSource = PassiveCounterSource.RuntimeCounter;

    [Tooltip("Key read from HeroRuntime counters. Only used when counterSource is RuntimeCounter.")]
    public string counterKey;

    [Tooltip("Value the counter counts down FROM. Drives the ring's fill. Only used when indicatorType is Counter.")]
    [Min(1)] public int counterMax = 1;

    [Tooltip("On completing a block, carry the leftover into the next one instead of restarting at max. " +
             "Keeps the ring in step with a passive that stacks off a running total (see Raging Blood).")]
    public bool counterOverflows = false;

    public abstract HeroPassiveTrigger Trigger { get; }

    /// <summary>Extra gate beyond the trigger itself. Evaluated before any ActionHolder state is set up.</summary>
    public virtual bool Matches(in HeroPassiveContext ctx) => true;

    /// <summary>
    /// When true, a minion that attacks the hero owning this passive takes no counter-attack
    /// (retaliation) back. Checked in MinionController.Attack via GameManager.HeroSuppressesCounterAttack.
    /// This is a standing property of the passive, not tied to whether its trigger effect ran.
    /// </summary>
    public virtual bool SuppressesCounterAttack => false;

    /// <summary>
    /// Called inline from GameManager's trigger coroutine (e.g. InvokeOnMinionTookDamageActions) *after*
    /// the ActionHolder registers are set for this hero, so verbs enqueued here land on the queue
    /// GameManager is already draining. Same contract as minion.modal.OnDeath.Invoke().
    /// </summary>
    public abstract void Run(in HeroPassiveContext ctx);

    /// <summary>
    /// Aura hook: called (pure side effect, NO ActionHolder scope / triggered action) when a minion is
    /// summoned, and for minions already on the board when this passive's hero is registered. Continuous
    /// aura passives override it to stamp per-minion stats (e.g. collision damage) on the minions they
    /// affect. `heroOwner` is the owner of the hero carrying this passive, so friendly/enemy is decidable.
    /// Default no-op, so trigger-based passives ignore it. Must stay side-effect-only: it runs mid-summon,
    /// outside any triggered-action scope, so it must not enqueue verbs or touch ActionHolder selection.
    /// </summary>
    public virtual void ApplyAuraOnSummon(MinionController minion, Agent heroOwner) { }

    /// <summary>
    /// What the hero's indicator row should render for this passive right now. The base implementation
    /// is the authored icon + passiveName/description, always active, drawn as `indicatorType` with a
    /// countdown resolved from the HeroRuntime counter. Passives whose live state that can't express
    /// override this — see CollisionDamageAuraHeroPassiveSO.
    ///
    /// MUST be pure. It is called from the view layer at arbitrary times, OUTSIDE any ActionHolder
    /// scope: never enqueue verbs, never touch ActionHolder selection, never mutate runtime state.
    /// `runtime` is null for a hero that was never registered, so every override must handle that.
    /// </summary>
    public virtual HeroPassiveDisplay GetDisplay(HeroRuntime runtime)
    {
        if (!showIndicator || icon == null) return HeroPassiveDisplay.Hidden;

        ResolveCounter(runtime, out int value, out int max, out int progress, out bool fromProgress);
        return new HeroPassiveDisplay(icon, passiveName, description, indicatorType,
            value, max, progress, fromProgress);
    }

    /// <summary>
    /// Declarative countdown resolution, so an authored asset needs no C# subclass to show live state.
    /// The counter counts DOWN from counterMax toward 0, and the ring drains with it: fill is
    /// value/counterMax, so a nearly-empty ring means the passive is about to fire.
    /// </summary>
    protected void ResolveCounter(HeroRuntime runtime, out int value, out int max,
        out int progress, out bool fromProgress)
    {
        value = 0;
        max = Mathf.Max(1, counterMax);
        progress = 0;
        fromProgress = false;

        if (indicatorType != PassiveIndicatorType.Counter || runtime == null) return;

        switch (counterSource)
        {
            case PassiveCounterSource.HealthLostToNextStack:
            {
                // Report the raw total only. The ring restarts at max on each completed block rather
                // than tracking progress modulo max, so the remaining count depends on where the last
                // reset happened — state this method is forbidden to hold. The view resolves it.
                MinionController hero = runtime.hero;
                if (hero == null || hero.modal == null) { value = max; return; }

                progress = Mathf.Max(0, hero.modal.defHealth - hero.modal.health);
                fromProgress = true;
                value = max; // Placeholder; the view overwrites this with the resolved remainder.
                break;
            }

            default:
            {
                if (string.IsNullOrEmpty(counterKey)) return;

                // A runtime that never had the key set reads 0 from GetCounter, which would render an
                // already-spent counter. Treat that as "full", so a passive that has not started
                // counting shows its max rather than a false about-to-proc.
                value = runtime.HasCounter(counterKey)
                    ? Mathf.Clamp(runtime.GetCounter(counterKey), 0, max)
                    : max;
                break;
            }
        }
    }
}
