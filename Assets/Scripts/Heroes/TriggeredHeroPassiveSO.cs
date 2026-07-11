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

    [Tooltip("ActionHolder verbs, run in order. Selection verbs first, then the effect.")]
    public UnityEvent actions;

    public override HeroPassiveTrigger Trigger => trigger;

    public override bool SuppressesCounterAttack => suppressesCounterAttack;

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
