using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The declarative passive: fires on a trigger, gates on a filter, and runs a UnityEvent of
/// ActionHolder verbs. Authored entirely in the Inspector — see HeroPassiveSOEditor for the
/// one-click wiring of the shipped passives.
/// </summary>
[CreateAssetMenu(fileName = "HeroPassive", menuName = "Cards/Hero Passive")]
public class TriggeredHeroPassiveSO : HeroPassiveSO
{
    /// <summary>
    /// The shared Assets/Resources/ActionHolder.asset. A UnityEvent persistent call stores its target
    /// object, so the editor needs this reference to wire `actions`. Resolved the same way CardSO does.
    /// </summary>
    public ActionHolder actionHolder;

    [SerializeField] private HeroPassiveTrigger trigger = HeroPassiveTrigger.HeroTookDamage;

    [Tooltip("Only used by the EveryNOwnerTurns trigger.")]
    [SerializeField] private int everyNTurns = 1;

    [Tooltip("Gates on the minion that triggered this passive (who died / was summoned / collided).")]
    [SerializeField] private MinionFilter subjectFilter;

    [Tooltip("When set, a minion that attacks this hero takes no counter-attack back. Only meaningful " +
             "with the HeroAttacked trigger; queried by MinionController.Attack independently of `actions`.")]
    [SerializeField] private bool suppressesCounterAttack = false;

    [Tooltip("Standing change to this hero's OWN attack, applied once when the hero is registered and " +
             "never re-applied. Negative for a drawback (the Summoner's -2). Independent of the trigger " +
             "and of `actions`; 0 means the passive leaves the statline alone.")]
    [SerializeField] private int heroAttackModifier = 0;

    [Tooltip("ActionHolder verbs, run in order. Selection verbs first, then the effect.")]
    public UnityEvent actions;

    public override HeroPassiveTrigger Trigger => trigger;

    public override bool SuppressesCounterAttack => suppressesCounterAttack;

    /// <summary>
    /// Stamps heroAttackModifier onto the hero's own attack. Deliberately NOT clamped at 0: a hero whose
    /// base attack is under 2, or that is debuffed further later, is meant to end up negative. Negative
    /// attack is harmless downstream — TakeDamage floors the damage it deals at 0, so a negative-attack
    /// strike heals nobody, it just does nothing.
    /// </summary>
    public override void ApplyToOwnHero(HeroRuntime runtime)
    {
        if (heroAttackModifier == 0) return;
        if (runtime == null || runtime.hero == null || runtime.hero.modal == null) return;

        MinionController hero = runtime.hero;
        hero.modal.attack += heroAttackModifier;

        // Re-seed rather than diff: this lands during SetupGame, and a red "-2" flash over the hero
        // before the first turn reads as an incoming enemy debuff rather than a printed drawback.
        hero.view.UpdateViewWithoutStatFlash(hero.modal);
    }

    private void Awake()
    {
        if (actionHolder == null)
            actionHolder = Resources.Load<ActionHolder>("ActionHolder");
    }

    public override bool Matches(in HeroPassiveContext ctx)
    {
        if (trigger == HeroPassiveTrigger.EveryNOwnerTurns)
        {
            int n = Mathf.Max(1, everyNTurns);
            if (ctx.ownerTurnNumber <= 0 || ctx.ownerTurnNumber % n != 0) return false;
        }

        // Triggers with no subject (turn boundaries) have nothing to filter.
        if (ctx.subject == null) return true;

        return subjectFilter.Matches(ctx.subject, ctx.owner);
    }

    public override void Run(in HeroPassiveContext ctx) => actions?.Invoke();
}
