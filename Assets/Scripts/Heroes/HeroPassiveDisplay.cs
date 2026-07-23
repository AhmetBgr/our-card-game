using UnityEngine;

/// <summary>
/// What kind of indicator a passive draws. Chosen per-passive on the asset, and the only thing that
/// decides which authored child of PassiveIndicator.prefab lights up — the view switches on this and
/// nothing else, so a new passive picks its look without touching view code.
///
/// This replaced the old PassiveBadgeSource enum. That one described where a *number* came from,
/// which conflated "has a number" with "looks like a countdown"; the art now distinguishes an aura
/// (a standing effect, outlined) from a counter (a countdown ring that drains toward a proc), and
/// those are different shapes, not different number sources.
/// </summary>
public enum PassiveIndicatorType
{
    /// <summary>Bare icon. No outline, no ring — the passive is present and that is all to say.</summary>
    Other,

    /// <summary>A standing effect. Lights the AuraOutline child; dims via isActive when dormant.</summary>
    Aura,

    /// <summary>
    /// A countdown toward a proc. Lights the Counter child: text shows the remaining count and the
    /// radial fill drains 1 → 0 as it approaches zero. See HeroPassiveSO.counterKey / counterMax.
    /// </summary>
    Counter,
}

/// <summary>
/// Where a Counter-type passive's remaining count comes from. Same reasoning as the indicator type
/// itself: authored TriggeredHeroPassiveSO assets are ScriptableObject *assets*, not C# subclasses,
/// so they can never override GetDisplay. This enum lets one show a live countdown without being
/// repointed at a new script guid — which would put its authored UnityEvent at risk.
/// </summary>
public enum PassiveCounterSource
{
    /// <summary>HeroRuntime.GetCounter(counterKey), counting down to 0. Needs something to write it.</summary>
    RuntimeCounter,

    /// <summary>
    /// Health the hero must still lose before the next stack of a "per N Health lost" passive lands.
    /// Derived from the hero's own health, so nothing has to maintain a counter — see Raging Blood,
    /// whose counterMax must match the healthPerAttack argument on its ScaleAttackWithHealthLost call.
    /// </summary>
    HealthLostToNextStack,
}

/// <summary>
/// What one passive wants its indicator to look like right now. Produced by HeroPassiveSO.GetDisplay
/// and consumed by HeroPassiveIndicator — the whole seam between passive logic and passive UI, so a
/// new passive needs no view code of its own.
///
/// A readonly struct because it is rebuilt on every Refresh (several times per turn) and discarded;
/// it must not allocate or hold references that outlive the frame. `default` means "render nothing",
/// so a GetDisplay that bails early is safe. Note that default(PassiveIndicatorType) is Other, which
/// is deliberately the harmless "just the icon" case.
/// </summary>
public readonly struct HeroPassiveDisplay
{
    /// <summary>False renders no indicator at all for this passive (no icon assigned, or opted out).</summary>
    public readonly bool visible;

    public readonly Sprite icon;

    /// <summary>Which authored child lights up. See PassiveIndicatorType.</summary>
    public readonly PassiveIndicatorType type;

    /// <summary>Remaining count shown in the counter ring. Only read when type is Counter.</summary>
    public readonly int counterValue;

    /// <summary>What the counter counts down FROM. Only read when type is Counter.</summary>
    public readonly int counterMax;

    /// <summary>
    /// Monotonic accumulated progress (for Berserker, total health lost). Only meaningful when
    /// counterFromProgress is true.
    ///
    /// The raw total is handed over rather than a pre-chewed remainder because the ring does NOT
    /// simply track progress modulo max: on completing a block it discards the leftover and restarts
    /// at max. That reset policy needs a baseline that survives across frames, which GetDisplay
    /// cannot hold without mutating state — so the view owns it. See HeroPassiveIndicatorView.
    /// </summary>
    public readonly int counterProgress;

    /// <summary>
    /// True when the counter is driven by accumulated progress (drain-punch-restart-at-max), false
    /// when counterValue is already the remaining count. RuntimeCounter is the latter: whatever writes
    /// it owns its own resets, so the view leaves it alone and punches on the plain 0 edge.
    /// </summary>
    public readonly bool counterFromProgress;

    /// <summary>
    /// Radial fill 0..1 for the counter ring, where 1 is "full countdown left" and 0 is "procs now".
    /// Derived, so it can never disagree with the number rendered beside it.
    /// </summary>
    public float counterFill => counterMax > 0 ? Mathf.Clamp01(counterValue / (float)counterMax) : 0f;

    /// <summary>False dims the icon: the passive is present but currently doing nothing.</summary>
    public readonly bool isActive;

    /// <summary>Passive name. Not rendered on the board row today; carried for a future tooltip.</summary>
    public readonly string title;

    /// <summary>Passive description, RAW. Formatting is the view's job (see CardTextFormatter).</summary>
    public readonly string body;

    public HeroPassiveDisplay(Sprite icon, string title, string body,
        PassiveIndicatorType type = PassiveIndicatorType.Other,
        int counterValue = 0, int counterMax = 0,
        int counterProgress = 0, bool counterFromProgress = false,
        bool isActive = true, bool visible = true)
    {
        this.icon = icon;
        this.title = title;
        this.body = body;
        this.type = type;
        this.counterValue = counterValue;
        this.counterMax = counterMax;
        this.counterProgress = counterProgress;
        this.counterFromProgress = counterFromProgress;
        this.isActive = isActive;
        this.visible = visible;
    }

    /// <summary>The "render nothing" display. Equals default(HeroPassiveDisplay).</summary>
    public static HeroPassiveDisplay Hidden => default;

    // Copy-with helpers so a GetDisplay override stays a one-liner over base.GetDisplay(runtime).
    public HeroPassiveDisplay WithCounter(int value, int max)
        => new HeroPassiveDisplay(icon, title, body, type, value, max, counterProgress, counterFromProgress, isActive, visible);

    public HeroPassiveDisplay WithActive(bool value)
        => new HeroPassiveDisplay(icon, title, body, type, counterValue, counterMax, counterProgress, counterFromProgress, value, visible);

    /// <summary>
    /// True when nothing the indicator renders has changed, so Refresh can skip the push.
    /// counterProgress is part of this on purpose: a hit of exactly counterMax leaves the remaining
    /// count identical, and skipping it would swallow the proc animation entirely.
    /// </summary>
    public bool SameAs(in HeroPassiveDisplay other)
        => visible == other.visible
        && isActive == other.isActive
        && icon == other.icon
        && type == other.type
        && counterValue == other.counterValue
        && counterMax == other.counterMax
        && counterProgress == other.counterProgress
        && counterFromProgress == other.counterFromProgress;
}
