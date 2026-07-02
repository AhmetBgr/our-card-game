/// <summary>
/// What hovering a minion currently means, so the hover feedback (range highlight, weapon image,
/// push arrow, ...) can be chosen from one place. Driven by the active <see cref="SelectionManager"/>
/// request (an attack pick is <see cref="ToAttack"/>, a card's minion pick uses the card's declared
/// <c>selectionIntent</c>) or, with no active request, defaults to <see cref="SeeRange"/>.
///
/// <see cref="ToSelectGenerally"/> is intentionally value 0: it is the benign default for any card
/// that picks a minion without a more specific intent (and for assets saved before this field existed).
/// </summary>
public enum HoverIntent
{
    ToSelectGenerally = 0,
    SeeRange = 1,
    ToAttack = 2,
    ToPush = 3,
}
