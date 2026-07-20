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

    [Tooltip("Live runtime value shown over the icon. See PassiveBadgeSource.")]
    public PassiveBadgeSource badgeSource = PassiveBadgeSource.None;

    [Tooltip("Key read from HeroRuntime counters. Only used when badgeSource is Counter.")]
    public string badgeCounterKey;

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
    /// is the authored icon + passiveName/description, always active, with a badge resolved from
    /// `badgeSource`. Passives whose live state the enum can't express override this — see
    /// CollisionDamageAuraHeroPassiveSO.
    ///
    /// MUST be pure. It is called from the view layer at arbitrary times, OUTSIDE any ActionHolder
    /// scope: never enqueue verbs, never touch ActionHolder selection, never mutate runtime state.
    /// `runtime` is null for a hero that was never registered, so every override must handle that.
    /// </summary>
    public virtual HeroPassiveDisplay GetDisplay(HeroRuntime runtime)
    {
        if (!showIndicator || icon == null) return HeroPassiveDisplay.Hidden;

        return new HeroPassiveDisplay(icon, passiveName, description, ResolveBadge(runtime));
    }

    /// <summary>Declarative badge resolution, so an authored asset needs no C# subclass to show state.</summary>
    protected string ResolveBadge(HeroRuntime runtime)
    {
        if (runtime == null) return null;

        switch (badgeSource)
        {
            case PassiveBadgeSource.AppliedAttackBonus:
                return runtime.appliedAttackBonus > 0 ? "+" + runtime.appliedAttackBonus : null;

            case PassiveBadgeSource.Counter:
                if (string.IsNullOrEmpty(badgeCounterKey)) return null;
                int value = runtime.GetCounter(badgeCounterKey);
                return value != 0 ? value.ToString() : null;

            default:
                return null;
        }
    }
}
