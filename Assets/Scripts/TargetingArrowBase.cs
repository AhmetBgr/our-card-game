using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared rendering core for the Hearthstone-style aiming arrows. It owns the LineRenderer curve, the
/// traveling beads, and the two end anchors, and draws them from a single <see cref="TryGetAim"/> answer
/// each frame — it holds NO game state and decides nothing about WHEN to show. Subclasses supply that:
/// each frame the base asks <see cref="TryGetAim"/> "is an aim live right now, and if so where does the
/// line start, where does it end, and is the end on a valid target?" and either hides everything or draws
/// the curve between those two points.
///
/// <see cref="AttackTargetingArrow"/> answers from an attack pick (origin = the attacker);
/// <see cref="GeneralTargetingArrow"/> answers from a card's cell/minion pick (origin = the played card).
///
/// The curved body is a code-built LineRenderer (or one you assign). The two END markers are your OWN
/// scene objects: assign a <see cref="startAnchor"/> (drawn on the origin) and an <see cref="endAnchor"/>
/// (the arrowhead, drawn on the target). Each frame this moves them onto the ends of the curve, optionally
/// rotates them to face along it, tints any SpriteRenderers under them, and toggles them active with the
/// arrow. Build them however you like (sprite, particles, children) — this just positions them.
/// </summary>
public abstract class TargetingArrowBase : MonoBehaviour
{
    [Header("End markers (assign your own scene objects; leave empty to skip)")]
    [Tooltip("Transform placed at the START of the line, on the origin (attacker / played card).")]
    public Transform startAnchor;
    [Tooltip("Transform placed at the END of the line, on the target (the arrowhead).")]
    public Transform endAnchor;

    [Header("Traveling beads (assign your own pixel-sprite objects; leave empty to skip)")]
    [Tooltip("Sprites that ride ALONG the curve, start->end, looping. Multiple beads are spaced evenly " +
             "for a flowing-dots chain. Build them however you like; this just positions them.")]
    public Transform[] beads;
    [Tooltip("Target world-space distance between beads. The number of beads shown scales with the arc's " +
             "length so spacing stays roughly constant (a longer arc shows more beads). Always at least 1.")]
    public float beadSpacing = 1.5f;
    [Tooltip("Below this straight origin->target distance, no beads are shown at all — so a short aim " +
             "(cursor right next to the origin) isn't cluttered with dots. 0 disables the cutoff.")]
    public float minBeadDistance = 0f;
    [Tooltip("Bead pool size. The first assigned bead is cloned (keeping its sprite/color) up to this many, " +
             "and each frame only as many as the current arc length needs are shown. Cap for the longest arc.")]
    public int maxBeads = 12;
    [Tooltip("How fast a bead travels along the arc, in world units per second. Constant regardless of the " +
             "arc's length, so a long aim doesn't speed the beads up.")]
    public float beadSpeed = 4f;
    [Tooltip("Tint the beads with the valid/invalid color like the line. Off by default so each bead " +
             "keeps its own sprite color.")]
    public bool tintBeads = false;
    [Tooltip("Rotate each bead to face along the curve tangent as it travels.")]
    public bool rotateBeadsToCurve = true;
    [Tooltip("Degrees added to a bead's facing so its art points along the curve (arrow.png points " +
             "up-right, so -45 aligns its tip with the tangent).")]
    public float beadAngleOffset = -45f;

    [Header("Line body")]
    [Tooltip("Assign your OWN LineRenderer to fully control its look (material, width curve, color gradient, " +
             "caps, sorting, texture). The script only drives its positions. Leave empty to auto-build one " +
             "from the settings below.")]
    public LineRenderer line;
    [Tooltip("Tint the line's start/end color with the valid/invalid color each frame. Turn OFF to keep " +
             "your assigned LineRenderer's own color gradient.")]
    public bool tintLine = true;
    [Tooltip("Material for the AUTO-BUILT line (ignored when you assign your own LineRenderer). Leave empty " +
             "to use a Sprites/Default material.")]
    public Material lineMaterial;

    [Header("Curve")]
    [Tooltip("Bulge the line into a lobbed arc. Turn OFF to draw a straight line from origin to target.")]
    public bool curvedLine = true;
    [Tooltip("Number of sampled segments along the curve (more = smoother).")]
    public int segments = 24;
    [Tooltip("Base upward bulge of the arc, before distance scaling.")]
    public float arcHeight = 0.6f;
    [Tooltip("Arc height grows with origin->target distance up to this cap (world units of extra bulge).")]
    public float maxExtraArc = 1.5f;

    [Header("Line width (taper from origin to head)")]
    public float widthStart = 0.12f;
    public float widthEnd = 0.35f;

    [Header("Colors")]
    [Tooltip("Tint while the cursor is over a valid target.")]
    public Color validColor = new Color(0.55f, 1f, 0.55f, 1f);
    [Tooltip("Tint while the cursor is over empty space or an invalid target.")]
    public Color invalidColor = new Color(1f, 0.95f, 0.7f, 1f);
    [Tooltip("Also tint SpriteRenderers under the END anchor (arrowhead) with the valid/invalid color.")]
    public bool tintAnchors = true;
    [Tooltip("Also tint the START anchor (on the origin). Off by default so the origin-side marker keeps " +
             "its own color and doesn't flash with the aim state.")]
    public bool tintStartAnchor = false;

    [Header("Rendering order")]
    [Tooltip("Sorting layer for the line body. Defaults to the top layer so it draws over minions " +
             "(which live on 'Layer 1'). The anchors keep whatever sorting you set on their own renderers.")]
    public string sortingLayerName = "Layer 3";
    [Tooltip("Sorting order for the line body.")]
    public int sortingOrder = 500;
    [Tooltip("Fixed Z the arrow is drawn at (2D sorting is by layer/order, so this only affects overlap math).")]
    public float foregroundZ = 0f;

    [Header("Anchor rotation")]
    [Tooltip("Rotate the anchors to face along the curve tangent at their end.")]
    public bool rotateAnchorsToCurve = true;
    [Tooltip("Degrees added to the end anchor's facing so its art points along the curve. arrow.png points " +
             "up-right, so -45 makes its tip follow the tangent (matches the projectile VFX).")]
    public float endAngleOffset = -45f;
    [Tooltip("Degrees added to the start anchor's facing. Only matters if the start art is directional.")]
    public float startAngleOffset = -45f;

    private LineRenderer _line;
    private SpriteRenderer[] _startTintTargets;
    private SpriteRenderer[] _endTintTargets;
    private Transform[] _beads;                 // assigned beads plus any clones
    private SpriteRenderer[][] _beadTintTargets;
    private Vector3[] _points;
    private float _beadT;

    protected virtual void Awake()
    {
        _points = new Vector3[Mathf.Max(2, segments) + 1];

        BuildLine();

        _startTintTargets = startAnchor != null ? startAnchor.GetComponentsInChildren<SpriteRenderer>(true) : null;
        _endTintTargets = endAnchor != null ? endAnchor.GetComponentsInChildren<SpriteRenderer>(true) : null;

        BuildBeads();

        SetVisible(false);
    }

    /// <summary>
    /// Answer, for this frame, whether an aim is live and where it runs. Return false to hide the whole
    /// arrow; return true and set <paramref name="start"/>/<paramref name="end"/> in world space, plus
    /// <paramref name="onTarget"/> = true when <paramref name="end"/> is snapped onto a valid target.
    /// </summary>
    protected abstract bool TryGetAim(out Vector3 start, out Vector3 end, out bool onTarget);

    // Use the assigned LineRenderer if there is one (leaving its look untouched), otherwise auto-build one
    // from the inspector settings. Either way the curve math needs world-space positions.
    private void BuildLine()
    {
        _line = line;
        if (_line == null)
        {
            var lineGo = new GameObject("ArrowBody");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 4;
            _line.widthCurve = AnimationCurve.EaseInOut(0f, widthStart, 1f, widthEnd);
            _line.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
            _line.sortingLayerName = sortingLayerName;
            _line.sortingOrder = sortingOrder;
        }

        _line.useWorldSpace = true;      // required: positions are computed in world space
        _line.positionCount = _points.Length;
    }

    protected virtual void LateUpdate()
    {
        if (!TryGetAim(out Vector3 start, out Vector3 end, out bool onTarget))
        {
            SetVisible(false);
            return;
        }

        start.z = foregroundZ;
        end.z = foregroundZ;

        // Control point: for a curved arrow the midpoint is bulged upward, more for longer shots; for a
        // straight line the control sits exactly at the midpoint so the quadratic Bézier is a line.
        Vector3 control = (start + end) * 0.5f;
        if (curvedLine)
        {
            float dist = Vector2.Distance(start, end);
            float extra = Mathf.Min(dist * 0.25f, maxExtraArc);
            control += Vector3.up * (arcHeight + extra);
        }
        control.z = foregroundZ;

        // Sample the quadratic Bézier into the line.
        int n = _points.Length - 1;
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            _points[i] = QuadraticBezier(start, control, end, t);
        }
        _line.positionCount = _points.Length;
        _line.SetPositions(_points);

        Color c = onTarget ? validColor : invalidColor;
        if (tintLine)
        {
            _line.startColor = c;
            _line.endColor = c;
        }

        // End anchor (arrowhead): at the tip, facing the curve tangent there.
        PlaceAnchor(endAnchor, _endTintTargets, end, end - _points[_points.Length - 2], endAngleOffset, c, tintAnchors);
        // Start anchor: at the origin, facing down the line. Tinted only if explicitly opted in.
        PlaceAnchor(startAnchor, _startTintTargets, start, _points[1] - _points[0], startAngleOffset, c, tintAnchors && tintStartAnchor);

        PlaceBeads(start, control, end, c);

        _line.enabled = true;
    }

    // Collect the assigned beads and, if beadCount asks for more, clone the first one (sprite/color and all)
    // until we reach the count, so several identical dots can flow the arc from a single authored template.
    private void BuildBeads()
    {
        var list = new List<Transform>();
        if (beads != null)
            foreach (var b in beads)
                if (b != null) list.Add(b);

        if (list.Count > 0)
        {
            Transform template = list[0];
            int target = Mathf.Max(maxBeads, list.Count);
            for (int i = list.Count; i < target; i++)
            {
                var clone = Instantiate(template, template.parent);
                clone.name = template.name + "_" + i;
                list.Add(clone);
            }
        }

        _beads = list.ToArray();
        _beadTintTargets = new SpriteRenderer[_beads.Length][];
        for (int i = 0; i < _beads.Length; i++)
            _beadTintTargets[i] = _beads[i] != null ? _beads[i].GetComponentsInChildren<SpriteRenderer>(true) : null;
    }

    // Advance a shared phase and ride each bead along the exact curve, spaced evenly, looping start->end.
    // The number actually shown scales with the arc length (constant spacing) — surplus beads are hidden.
    private void PlaceBeads(Vector3 p0, Vector3 control, Vector3 p1, Color tint)
    {
        if (_beads == null || _beads.Length == 0) return;

        // Too short an aim: hide every bead so a target right next to the origin isn't cluttered with dots.
        if (minBeadDistance > 0f && Vector3.Distance(p0, p1) < minBeadDistance)
        {
            foreach (var bead in _beads)
                if (bead != null && bead.gameObject.activeSelf) bead.gameObject.SetActive(false);
            return;
        }

        // Arc length from the sampled polyline, so bead count tracks how long the curve currently is.
        float arcLen = 0f;
        for (int i = 1; i < _points.Length; i++)
            arcLen += Vector3.Distance(_points[i - 1], _points[i]);
        int shown = Mathf.Clamp(Mathf.RoundToInt(arcLen / Mathf.Max(0.01f, beadSpacing)), 1, _beads.Length);

        // Advance in world-space: dividing by the arc length turns beadSpeed into constant units/second,
        // so a bead travels at the same visual speed whether the arc is short or long.
        _beadT += Time.deltaTime * beadSpeed / Mathf.Max(0.01f, arcLen);
        _beadT -= Mathf.Floor(_beadT); // keep in [0,1)

        for (int i = 0; i < _beads.Length; i++)
        {
            var bead = _beads[i];
            if (bead == null) continue;

            // Only the first `shown` beads ride the arc; the rest of the pool stays hidden.
            if (i >= shown)
            {
                if (bead.gameObject.activeSelf) bead.gameObject.SetActive(false);
                continue;
            }

            float t = _beadT + (float)i / shown;
            t -= Mathf.Floor(t);

            bead.position = QuadraticBezier(p0, control, p1, t);
            if (rotateBeadsToCurve)
            {
                Vector3 tangent = QuadraticTangent(p0, control, p1, t);
                float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + beadAngleOffset;
                bead.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            if (tintBeads && _beadTintTargets != null && i < _beadTintTargets.Length)
                TintRgb(_beadTintTargets[i], tint);
            if (!bead.gameObject.activeSelf) bead.gameObject.SetActive(true);
        }
    }

    private void PlaceAnchor(Transform anchor, SpriteRenderer[] tintTargets, Vector3 pos, Vector3 tangent, float angleOffset, Color tint, bool applyTint)
    {
        if (anchor == null) return;
        anchor.position = pos;
        if (rotateAnchorsToCurve)
        {
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + angleOffset;
            anchor.rotation = Quaternion.Euler(0f, 0f, angle);
        }
        if (applyTint) TintRgb(tintTargets, tint);
        if (!anchor.gameObject.activeSelf) anchor.gameObject.SetActive(true);
    }

    private void SetVisible(bool visible)
    {
        if (_line != null && _line.enabled != visible) _line.enabled = visible;
        if (startAnchor != null && startAnchor.gameObject.activeSelf != visible) startAnchor.gameObject.SetActive(visible);
        if (endAnchor != null && endAnchor.gameObject.activeSelf != visible) endAnchor.gameObject.SetActive(visible);
        if (_beads != null)
        {
            foreach (var bead in _beads)
                if (bead != null && bead.gameObject.activeSelf != visible) bead.gameObject.SetActive(visible);
        }
    }

    // Recolor sprites with the aim color's RGB but keep each sprite's own alpha, so a translucent aim tint
    // can't force a solid marker to disappear (it only shifts its hue toward valid/invalid).
    private static void TintRgb(SpriteRenderer[] targets, Color rgb)
    {
        if (targets == null) return;
        foreach (var sr in targets)
            if (sr != null) sr.color = new Color(rgb.r, rgb.g, rgb.b, sr.color.a);
    }

    private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u) * p0 + (2f * u * t) * p1 + (t * t) * p2;
    }

    // Derivative of the quadratic Bézier: the travel direction at t (not normalized).
    private static Vector3 QuadraticTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
    }
}
