using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The hero's passive indicator. Lives on Hero.prefab, so both PlayerHero and OpponentHero get it;
/// opponent passives are visible on purpose, since hidden opponent passives are exactly the feedback
/// gap this exists to close.
///
/// The component stays on the hero (HeroPassiveSystem and GameManager both reach it via For(hero)),
/// but the thing it draws does not: Agent.SpawnPassiveUI instantiates PassiveUICanvas.prefab under
/// the agent's passiveUIPos anchor and hands the indicator inside it here via AttachIndicator. That
/// keeps the row parked at a fixed board position instead of riding the hero's transform through
/// every lunge, punch and death animation.
///
/// It drives a single indicator, so it renders exactly one passive. Heroes are single-passive today;
/// a hero carrying more only warns and shows the first, until multi-passive layout is designed.
///
/// Discovery is PUSH, not pull. HeroRuntime is AddComponent'ed at runtime by HeroPassiveSystem.Register,
/// so this cannot resolve its data in Start(). It stays inert until Register hands it the runtime —
/// the one moment both prerequisites hold (hero.card is a HeroSO with passives, and the runtime exists).
/// It reads runtime.heroSO.passives rather than HeroSO directly, so it can never disagree with what
/// the system actually registered.
///
/// Everything here is read-only with respect to game state. It must never enqueue ActionHolder verbs
/// or open a triggered-action scope — see the HeroPassiveSystem header for why a second writer races
/// GameManager's scope.
/// </summary>
public class HeroPassiveIndicatorView : MonoBehaviour
{
    [Tooltip("The indicator this view drives. Normally supplied at runtime by Agent.SpawnPassiveUI; an authored reference here is only a fallback for a hero used outside the agent setup.")]
    [SerializeField] private HeroPassiveIndicator indicator;

    private HeroRuntime _runtime;
    private HeroPassiveSO _passive;
    private HeroPassiveDisplay _lastDisplay;

    // Progress total at which the counter last completed a block and restarted at max. Owned here
    // because it is a display decision, not game state — see ResolveProgressCounter.
    private int _counterBaseline;
    private bool _hasCounterBaseline;

    public static HeroPassiveIndicatorView For(MinionController hero)
        => hero != null ? hero.GetComponent<HeroPassiveIndicatorView>() : null;

    // The indicator ships visible in its prefab so it can be laid out in the editor. Hide it before the
    // first frame: a hero with no passives must never flash the placeholder icon.
    private void Awake() => Hide();

    /// <summary>
    /// Points this view at the indicator spawned for the owning agent (see Agent.SpawnPassiveUI), which
    /// lives on a canvas anchored to the board rather than on the hero.
    ///
    /// Order-independent with respect to Bind: if a passive was already bound to a previous indicator,
    /// it is rebound onto the new one, so this can arrive before or after HeroPassiveSystem.Register.
    /// </summary>
    public void AttachIndicator(HeroPassiveIndicator spawned)
    {
        if (spawned == null || spawned == indicator) return;

        // A hero that also carries an authored fallback would otherwise leave it on screen alongside
        // the spawned one, showing the same passive twice.
        if (indicator != null) indicator.gameObject.SetActive(false);

        // Bind() opens with Hide(), which clears _runtime — so read it before rebinding, not after.
        HeroRuntime bound = _runtime;

        indicator = spawned;
        indicator.gameObject.SetActive(false); // stays hidden until something is actually bound to it

        if (bound != null) Bind(bound);
    }

    /// <summary>
    /// Binds the hero's passive to the attached indicator. Idempotent: re-Registering or reloading the
    /// scene rebinds the same object rather than accumulating icons.
    /// </summary>
    public void Bind(HeroRuntime runtime)
    {
        Hide();
        _runtime = runtime;

        if (indicator == null || runtime == null || runtime.heroSO == null) return;

        List<HeroPassiveSO> passives = runtime.heroSO.passives;
        if (passives == null) return;

        HeroPassiveSO first = null;
        int count = 0;
        for (int i = 0; i < passives.Count; i++)
        {
            if (passives[i] == null) continue;
            count++;
            if (first == null) first = passives[i];
        }

        if (count == 0) return; // no passives — indicator stays hidden

        if (count > 1)
        {
            Debug.LogWarning(
                $"[HeroPassiveIndicatorView] '{runtime.heroSO.name}' has {count} passives but the authored " +
                $"indicator renders one. Showing '{first.passiveName}'; multi-passive layout is not built yet.",
                this);
        }

        _passive = first;

        // Apply drives SetActive off display.visible, so a passive with no icon (or one that opted out)
        // leaves the indicator hidden while still staying bound — Refresh can bring it back later.
        HeroPassiveDisplay display = first.GetDisplay(runtime);

        // Establish the reset baseline here rather than on the first Refresh, so a hero that is
        // registered already damaged starts on a full ring instead of animating a proc it never got.
        if (display.type == PassiveIndicatorType.Counter && display.counterFromProgress)
        {
            ResolveProgressCounter(display.counterProgress, display.counterMax, first.counterOverflows,
                out int remaining, out _);
            display = display.WithCounter(remaining, display.counterMax);
        }

        _lastDisplay = display;
        indicator.Bind(first, _lastDisplay);
    }

    /// <summary>
    /// Re-pulls GetDisplay for the bound passive. Cheap to call liberally: it early-outs when nothing
    /// the indicator renders has changed.
    /// </summary>
    public void Refresh()
    {
        if (_runtime == null || _passive == null || indicator == null) return;

        HeroPassiveDisplay display = _passive.GetDisplay(_runtime);

        // Progress-driven counters need resolving before anything can be compared: GetDisplay reports
        // only the running total, and the remaining count depends on this view's reset baseline.
        int cyclesCrossed = 0;
        if (display.type == PassiveIndicatorType.Counter && display.counterFromProgress)
        {
            ResolveProgressCounter(display.counterProgress, display.counterMax, _passive.counterOverflows,
                out int remaining, out cyclesCrossed);
            display = display.WithCounter(remaining, display.counterMax);
        }

        if (cyclesCrossed == 0 && display.SameAs(_lastDisplay)) return;

        bool animateCounter =
            display.type == PassiveIndicatorType.Counter &&
            _lastDisplay.type == PassiveIndicatorType.Counter;

        // A RuntimeCounter is written by someone else, who owns its resets — so the >0 -> 0 edge is the
        // only proc signal available for it. Edge, not level: Refresh early-outs on an unchanged
        // display, so this cannot re-punch while the counter sits at zero. Progress counters never use
        // this path; their punches come from the crossings the ring animates through.
        bool countedDownToZero =
            animateCounter &&
            !display.counterFromProgress &&
            _lastDisplay.counterValue > 0 &&
            display.counterValue == 0;

        _lastDisplay = display;

        if (animateCounter) indicator.ApplyAnimated(display, cyclesCrossed);
        else indicator.Apply(display);

        if (countedDownToZero) indicator.PlayProcFlash();
    }

    /// <summary>
    /// Turns a monotonic running total into "how much is left on the ring". Every completed block of
    /// `max` is a proc; what happens to the leftover past the last one depends on the passive:
    ///
    /// overflow=true  — the remainder carries into the next block. The ring stays in step with a
    ///                  passive that stacks off the running total: it always reads
    ///                  max - (progress % max), so it predicts the next stack correctly. This is what
    ///                  Raging Blood wants, since ScaleAttackWithHealthLost grants healthLost / max.
    /// overflow=false — the remainder is DISCARDED and the ring restarts at max. Easier to read, but
    ///                  it drifts out of step with any passive that stacks off a total.
    ///
    /// Either way the baseline lives here rather than in GetDisplay, which must stay pure.
    /// </summary>
    private void ResolveProgressCounter(int progress, int rawMax, bool overflow,
        out int remaining, out int cycles)
    {
        int max = Mathf.Max(1, rawMax);

        // First read, or the total moved backwards (the hero was healed) — rebase and show a full ring.
        if (!_hasCounterBaseline || progress < _counterBaseline)
        {
            _counterBaseline = progress;
            _hasCounterBaseline = true;
            remaining = max;
            cycles = 0;
            return;
        }

        int elapsed = progress - _counterBaseline;
        cycles = elapsed / max;

        if (cycles > 0)
        {
            // Advance by whole blocks when overflowing, so the leftover survives into the next one;
            // jump the baseline all the way to progress when not, which throws that leftover away.
            _counterBaseline = overflow ? _counterBaseline + (cycles * max) : progress;
        }

        remaining = max - (progress - _counterBaseline);
    }

    /// <summary>Pops the icon when its passive fires. No-op for any passive this view isn't rendering.</summary>
    public void PlayProcFlash(HeroPassiveSO passive)
    {
        if (passive == null || indicator == null) return;
        if (passive != _passive) return;

        // A stacking counter punches from its own ring animation, at the frame the ring empties.
        // Punching here too would fire twice per proc — once on impact, once when the ring crosses
        // zero — and would punch even on a hit that advanced the count without completing a stack.
        // Deferring to the ring means the pop lands exactly when the bonus does, and only then.
        if (_lastDisplay.type == PassiveIndicatorType.Counter &&
            _passive.counterSource == PassiveCounterSource.HealthLostToNextStack)
            return;

        indicator.PlayProcFlash();
    }

    private void Hide()
    {
        if (indicator != null) indicator.gameObject.SetActive(false);

        _passive = null;
        _runtime = null;
        _lastDisplay = default;

        // Drop the baseline too: a rebind is a different hero (or a reloaded scene), and carrying a
        // stale reset point forward would make the first Refresh animate a phantom proc.
        _counterBaseline = 0;
        _hasCounterBaseline = false;
    }
}
