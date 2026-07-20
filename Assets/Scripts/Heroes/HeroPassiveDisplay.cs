using UnityEngine;

/// <summary>
/// Where a passive's badge number comes from, for passives authored as plain TriggeredHeroPassiveSO
/// assets. Those are ScriptableObject *assets*, not C# subclasses, so they can never override
/// GetDisplay — this enum is how an authored passive still surfaces live runtime state without
/// needing a bespoke class (and without repointing the asset at a new script guid, which would risk
/// its authored UnityEvent). Anything this can't express should subclass HeroPassiveSO and override
/// GetDisplay instead.
/// </summary>
public enum PassiveBadgeSource
{
    /// <summary>No badge; the icon shows alone.</summary>
    None,

    /// <summary>HeroRuntime.appliedAttackBonus, rendered as "+N". The Berserker's granted attack.</summary>
    AppliedAttackBonus,

    /// <summary>HeroRuntime.GetCounter(badgeCounterKey), rendered as the raw number.</summary>
    Counter,
}

/// <summary>
/// What one passive wants its indicator to look like right now. Produced by HeroPassiveSO.GetDisplay
/// and consumed by HeroPassiveIndicator — the whole seam between passive logic and passive UI, so a
/// new passive needs no view code of its own.
///
/// A readonly struct because it is rebuilt on every Refresh (several times per turn) and discarded;
/// it must not allocate or hold references that outlive the frame. `default` means "render nothing",
/// so a GetDisplay that bails early is safe.
/// </summary>
public readonly struct HeroPassiveDisplay
{
    /// <summary>False renders no indicator at all for this passive (no icon assigned, or opted out).</summary>
    public readonly bool visible;

    public readonly Sprite icon;

    /// <summary>Small number/label over the icon. Null or empty hides the badge.</summary>
    public readonly string badge;

    /// <summary>False dims the icon: the passive is present but currently doing nothing.</summary>
    public readonly bool isActive;

    /// <summary>Passive name. Not rendered on the board row today; carried for a future tooltip.</summary>
    public readonly string title;

    /// <summary>Passive description, RAW. Formatting is the view's job (see CardTextFormatter).</summary>
    public readonly string body;

    public HeroPassiveDisplay(Sprite icon, string title, string body,
        string badge = null, bool isActive = true, bool visible = true)
    {
        this.icon = icon;
        this.title = title;
        this.body = body;
        this.badge = badge;
        this.isActive = isActive;
        this.visible = visible;
    }

    /// <summary>The "render nothing" display. Equals default(HeroPassiveDisplay).</summary>
    public static HeroPassiveDisplay Hidden => default;

    // Copy-with helpers so a GetDisplay override stays a one-liner over base.GetDisplay(runtime).
    public HeroPassiveDisplay WithBadge(string value)
        => new HeroPassiveDisplay(icon, title, body, value, isActive, visible);

    public HeroPassiveDisplay WithActive(bool value)
        => new HeroPassiveDisplay(icon, title, body, badge, value, visible);

    /// <summary>True when nothing the indicator renders has changed, so Refresh can skip the push.</summary>
    public bool SameAs(in HeroPassiveDisplay other)
        => visible == other.visible
        && isActive == other.isActive
        && icon == other.icon
        && badge == other.badge;
}
