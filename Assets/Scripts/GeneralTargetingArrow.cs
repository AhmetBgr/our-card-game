using UnityEngine;

/// <summary>
/// Aiming arrow shown while the player is playing a card that needs a TARGET — picking a minion for a
/// spell, or picking a grid cell (summon position, area effect, etc.). All the rendering lives in
/// <see cref="TargetingArrowBase"/>; this class only answers WHEN it is live and WHERE it runs.
///
/// It self-activates by polling the two selection systems: a non-attack minion pick on
/// <see cref="SelectionManager"/> (<see cref="SelectionManager.HasActiveMinionRequest"/> with an intent
/// other than <see cref="HoverIntent.ToAttack"/>, which is the attack arrow's job) OR a cell pick on
/// <see cref="GridCellSelectionManager.HasActiveSession"/>. The line originates from the card being played
/// (<see cref="GameManager.PlayingCard"/>) — or <see cref="originOverride"/> if you'd rather anchor it to a
/// fixed point — and ends at the cursor, snapping onto a valid minion or a valid selectable cell under it.
/// When the pick ends the arrow hides the same frame.
/// </summary>
public class GeneralTargetingArrow : TargetingArrowBase
{
    [Header("Origin")]
    [Tooltip("Where the line starts. Leave empty to originate from the card being played " +
             "(GameManager.PlayingCard). Assign a Transform to anchor the start to a fixed point instead " +
             "(e.g. a hand/portrait marker).")]
    public Transform originOverride;

    [Tooltip("The origin (played card / override) is a UI object living in a Canvas, so its transform.position " +
             "is a canvas coordinate, not the gameplay world. When ON, that position is converted to a world " +
             "point on the arrow's plane via the camera, matching how the cursor end is derived. Turn OFF only " +
             "if the origin is already a world-space object.")]
    public bool originIsUI = true;

    [Tooltip("Camera that renders the gameplay world (the arrow's plane). Leave empty to use Camera.main — " +
             "which is what the cursor uses, so keep them the same or origin and target won't share a space.")]
    public Camera worldCamera;

    protected override bool TryGetAim(out Vector3 start, out Vector3 end, out bool onTarget)
    {
        start = end = Vector3.zero;
        onTarget = false;

        var gm = GameManager.Instance;
        if (gm == null || !gm.isPlayerTurn) return false;

        var sel = SelectionManager.Instance;
        var cells = GridCellSelectionManager.Instance;

        // A card-driven minion pick (anything but an attack, which the attack arrow owns) or a cell pick.
        bool minionPick = sel != null && sel.HasActiveMinionRequest && sel.ActiveIntent != HoverIntent.ToAttack;
        bool cellPick = cells != null && cells.HasActiveSession;
        if (!minionPick && !cellPick) return false;

        // Origin: an explicit override, else the played card. Converted from canvas space to the gameplay
        // world so it shares the cursor end's space.
        Transform origin = originOverride;
        if (origin == null)
        {
            var card = gm.PlayingCard;
            if (card == null) return false;
            origin = card.transform;
        }
        start = ResolveWorld(origin);

        end = Cursor.instance != null ? (Vector3)Cursor.instance.mouseWorldPos : start;

        // Snap the end onto a valid minion target, or failing that a valid selectable cell, under the cursor.
        var hit = Physics2D.OverlapPoint(end);
        if (hit != null)
        {
            var minion = minionPick ? hit.GetComponentInParent<MinionController>() : null;
            if (minion != null && sel.IsActiveTarget(minion))
            {
                end = minion.transform.position;
                onTarget = true;
            }
            else if (cellPick)
            {
                var cell = hit.GetComponentInParent<CellController>();
                if (cell != null && cell.selectable != null && cell.selectable.IsSelectable)
                {
                    end = cell.transform.position;
                    onTarget = true;
                }
            }
        }

        return true;
    }

    // The origin is usually a UI element in a Canvas, whose transform.position is a canvas coordinate rather
    // than a gameplay-world one. Map it to a screen point (honoring the canvas's render mode: overlay canvases
    // are already in screen space and take a null camera; camera/world canvases need their render camera) and
    // then into the same world space the cursor end uses, so both ends of the line live on the arrow's plane.
    // The base flattens the returned z to foregroundZ, so only the x/y matter here.
    private Vector3 ResolveWorld(Transform origin)
    {
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (!originIsUI || cam == null) return origin.position;

        Canvas canvas = origin.GetComponentInParent<Canvas>();
        Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector3 screen = RectTransformUtility.WorldToScreenPoint(uiCam, origin.position);
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, cam.nearClipPlane));
    }
}
