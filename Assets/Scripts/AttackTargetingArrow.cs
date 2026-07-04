using UnityEngine;

/// <summary>
/// Aiming arrow shown while a minion is picking an ATTACK target. All the rendering (curve, beads, anchors,
/// tinting, visibility) lives in <see cref="TargetingArrowBase"/>; this class only answers WHEN it is live
/// and WHERE it runs, by polling the public selection state on <see cref="SelectionManager"/>.
///
/// Whenever an attack-target request is live for an attacker this instance serves
/// (<see cref="SelectionManager.ActiveAttacker"/> non-null, <see cref="SelectionManager.ActiveIntent"/> ==
/// <see cref="HoverIntent.ToAttack"/>, and the attacker's range matches <see cref="serves"/>) it draws from
/// the attacker to the cursor, snapping the end onto whatever valid target sits under the cursor. When the
/// request ends (target clicked, cancel, turn end) it hides the same frame.
///
/// Ranged and melee attackers get their OWN instance so each can have its own line, beads and anchors. Use
/// <see cref="serves"/> to pick which attackers an instance responds to (ranged = <c>modal.range &gt;= 2</c>,
/// melee = below that), and turn <see cref="TargetingArrowBase.curvedLine"/> off for the melee arrow so it
/// draws a straight line instead of a lobbed arc.
/// </summary>
public class AttackTargetingArrow : TargetingArrowBase
{
    /// <summary>Which attackers an arrow instance serves, keyed off the attacker's <c>modal.range</c>.</summary>
    public enum AttackerKind
    {
        [Tooltip("Attackers with range >= 2 (the lobbed, curved arrow).")]
        Ranged,
        [Tooltip("Attackers with range < 2 (melee; pair with a straight line).")]
        Melee,
    }

    [Header("Which attacker this arrow serves")]
    [Tooltip("Ranged serves attackers with modal.range >= 2; Melee serves the rest. Give ranged and melee " +
             "their own instance of this component so each keeps its own line, beads and anchors.")]
    public AttackerKind serves = AttackerKind.Ranged;

    protected override bool TryGetAim(out Vector3 start, out Vector3 end, out bool onTarget)
    {
        start = end = Vector3.zero;
        onTarget = false;

        var sel = SelectionManager.Instance;
        var attacker = sel != null ? sel.ActiveAttacker : null;

        // Only for a live attack pick by an attacker this instance serves (ranged vs melee).
        bool rangeMatches = attacker != null && attacker.modal != null
                            && (serves == AttackerKind.Ranged
                                ? attacker.modal.range >= 2
                                : attacker.modal.range < 2);
        if (attacker == null || sel.ActiveIntent != HoverIntent.ToAttack || !rangeMatches)
            return false;

        start = attacker.transform.position;
        end = Cursor.instance != null ? (Vector3)Cursor.instance.mouseWorldPos : start;

        // Snap the end onto a valid target sitting under the cursor.
        var hit = Physics2D.OverlapPoint(end);
        if (hit != null)
        {
            var hovered = hit.GetComponentInParent<MinionController>();
            if (hovered != null && sel.IsActiveTarget(hovered))
            {
                end = hovered.transform.position;
                onTarget = true;
            }
        }

        return true;
    }
}
