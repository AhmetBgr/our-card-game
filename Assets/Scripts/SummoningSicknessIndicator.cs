using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// The looping "zzz" that marks a minion as summoning-sick: it landed this turn, so it can neither move
/// nor attack yet. Several "z" glyphs drift diagonally off the minion's head, growing and fading as they
/// go, staggered so there is always one in flight.
///
/// The condition is exactly <c>MinionController.age &lt; 1</c> — the same test that suppresses the move
/// arrow (<see cref="MinionController.ShowMoveArrow"/>) and gates attacking (<c>SetReadyToAttack</c>).
/// That single test already covers "and not overdrived": Overdrive is implemented as
/// <c>ActionHolder.RemoveSummoningSickness</c>, which stamps <c>age = 1</c> the moment the minion lands,
/// so an Overdrive minion is never age 0 to begin with and never snores. <see cref="appearDelay"/> also
/// outlasts the summon pop-in, so the queued Overdrive action resolves long before the first z would show.
///
/// Deliberately zero-setup, in the spirit of <see cref="FloatingTextManager"/>: the labels are built in
/// code and <see cref="MinionController.Start"/> attaches the component itself, so every minion prefab
/// (Minion, Minion_Ranged, and any variant) gets this without prefab wiring. Drop the component onto a
/// prefab by hand only to override the tuning below — <c>EnsureSummoningSicknessIndicator</c> reuses an
/// existing instance rather than adding a second one.
/// </summary>
public class SummoningSicknessIndicator : MonoBehaviour
{
    [Header("Content")]
    [Tooltip("The glyph each label shows. One label = one 'z'.")]
    [SerializeField] private string glyph = "z";
    [Tooltip("How many z's are in the loop. They are evenly staggered across one cycle, so more z's = a denser trail, not a faster one.")]
    [SerializeField, Min(1)] private int zCount = 3;

    [Header("Placement (local to the minion)")]
    [Tooltip("Where a z is born, relative to the minion's centre. The minion occupies one grid cell, so ~0.45 up sits just above its head.")]
    [SerializeField] private Vector3 startOffset = new Vector3(0.1f, 0.45f, 0f);
    [Tooltip("The diagonal a z travels over one full cycle. Positive x/y drifts up and to the right.")]
    [SerializeField] private Vector3 travel = new Vector3(0.4f, 0.5f, 0f);
    [Tooltip("Sideways wobble layered on top of the diagonal, so the trail reads as drifting smoke rather than a straight line. 0 = a clean diagonal.")]
    [SerializeField] private float sway = 0.05f;
    [Tooltip("How many full left-right wobbles a z makes over its cycle.")]
    [SerializeField] private float swayCycles = 1f;

    [Header("Timing")]
    [Tooltip("Seconds for one z to travel the whole diagonal and fade out. The loop repeats forever while the minion stays asleep.")]
    [SerializeField] private float cycleDuration = 1.8f;
    [Tooltip("Fraction of the cycle spent fading IN; the remainder fades out. The fade-out is the long tail the request asked for, so keep this small.")]
    [SerializeField, Range(0.01f, 0.9f)] private float fadeInFraction = 0.18f;
    [Tooltip("Held before the first z appears, so the z's don't start mid summon pop-in (MinionView.PlayAppearAnimation runs 0.25s delay + 0.5s scale-up). Re-applied every time the indicator is shown.")]
    [SerializeField] private float appearDelay = 0.75f;

    [Header("Look")]
    [Tooltip("World font size, in the same units the FloatingTextConfig styles use (their labels sit at 2-4 on this board).")]
    [SerializeField] private float fontSize = 2.5f;
    [Tooltip("Scale multiplier at birth and at the end of the drift: a z grows as it floats away.")]
    [SerializeField] private float startScale = 0.7f;
    [SerializeField] private float endScale = 1.15f;
    [SerializeField] private Color color = new Color(0.85f, 0.92f, 1f, 1f);
    [SerializeField] private Color outlineColor = new Color(0.05f, 0.08f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;
    [Tooltip("Sorting layer the labels draw on. 'Layer 3' is the one the floating popups use, i.e. above every board sprite.")]
    [SerializeField] private string sortingLayer = "Layer 3";
    [Tooltip("Order within that layer. Kept below the floating popups' 200 so a damage number always wins over the z's.")]
    [SerializeField] private int sortingOrder = 100;
    [Tooltip("Empty = the font from Resources/FloatingTextConfig.asset, so the z's match the rest of the board text.")]
    [SerializeField] private TMP_FontAsset font;

    // The minion this reads. Resolved in Awake, or handed over by MinionController before Awake runs.
    private MinionController _minion;

    // Container for the labels, parented under the minion so they ride it: a z stays over the minion's
    // head through a Move()/push tween, and scales with the summon pop-in instead of popping in at full
    // size over a minion that is still growing.
    private Transform _container;
    private readonly List<TextMeshPro> _labels = new List<TextMeshPro>();
    private readonly List<Tween> _tweens = new List<Tween>();

    private bool _shown;
    private bool _built;

    /// <summary>Wires the minion up front, for callers that add this component at runtime.</summary>
    public void Bind(MinionController minion) => _minion = minion;

    private void Awake()
    {
        if (_minion == null) _minion = GetComponent<MinionController>();

        // Heroes are never summoned — they start the match in place — but they DO start at age 0 and
        // share MinionController, so without this they would snore through the first turn. Today
        // HeroController.Start never calls base.Start(), so one is never attached in the first place;
        // this keeps that true even if that override changes.
        if (_minion is HeroController) enabled = false;
    }

    // Polled rather than event-driven because `age` is a plain public int written from several places
    // (the turn-end tick, RemoveSummoningSickness, PushSelectedMinionForward's save/restore) with no
    // change notification. The check is two field reads; the tweens only exist while the z's are up.
    private void Update()
    {
        bool shouldShow = ShouldShow();
        if (shouldShow == _shown) return;

        _shown = shouldShow;
        if (shouldShow) Show();
        else Hide();
    }

    private bool ShouldShow()
    {
        if (_minion == null || _minion.modal == null) return false;

        // A minion whose health has hit 0 is mid-death (the corpse lingers ~1s for its death clip before
        // DestroySelf) — it shouldn't keep snoring on the way out.
        if (_minion.modal.health <= 0) return false;

        return _minion.age < 1;
    }

    private void Show()
    {
        Build();
        if (_container == null) return;

        _container.gameObject.SetActive(true);
        StartTweens();
    }

    private void Hide()
    {
        KillTweens();
        if (_container != null) _container.gameObject.SetActive(false);
    }

    private void StartTweens()
    {
        KillTweens();

        float stagger = cycleDuration / zCount;

        for (int i = 0; i < _labels.Count; i++)
        {
            int index = i; // captured per label; the loop variable itself is shared across iterations

            // Seeded at t = 0 so a label sits invisible at its start position through its stagger delay,
            // during which the setter below is not called.
            ApplyProgress(index, 0f);

            // One 0 -> 1 progress tween per label drives position, scale and alpha together. Looping a
            // single normalized value (rather than a sequence of separate position/fade tweens) means
            // every restart recomputes the whole pose from scratch, so a z can never inherit a stale
            // mid-fade alpha or a drifted position from the previous lap.
            Tween tween = DOTween.To(() => 0f, v => ApplyProgress(index, v), 1f, cycleDuration)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetDelay(appearDelay + index * stagger)
                .SetTarget(this);

            _tweens.Add(tween);
        }
    }

    private void KillTweens()
    {
        foreach (Tween tween in _tweens)
            tween?.Kill();

        _tweens.Clear();
    }

    // t runs 0 -> 1 over one cycle: the z slides along the diagonal, grows, and its alpha ramps up over
    // the first fadeInFraction of the trip and then falls to nothing by the end.
    private void ApplyProgress(int index, float t)
    {
        if (index >= _labels.Count) return;

        TextMeshPro label = _labels[index];
        if (label == null) return;

        Vector3 pos = startOffset + travel * t;
        if (sway != 0f)
            pos.x += Mathf.Sin(t * Mathf.PI * 2f * swayCycles) * sway;
        label.transform.localPosition = pos;

        float scale = Mathf.Lerp(startScale, endScale, t);
        label.transform.localScale = new Vector3(scale, scale, 1f);

        label.alpha = t < fadeInFraction
            ? t / fadeInFraction
            : 1f - (t - fadeInFraction) / (1f - fadeInFraction);
    }

    private void Build()
    {
        if (_built) return;
        _built = true;

        var containerObj = new GameObject("SummoningSickness");
        _container = containerObj.transform;
        _container.SetParent(transform, false);

        TMP_FontAsset resolvedFont = ResolveFont();
        int layerId = SortingLayer.NameToID(sortingLayer);
        bool layerIsValid = !string.IsNullOrEmpty(sortingLayer) && SortingLayer.IsValid(layerId);

        for (int i = 0; i < zCount; i++)
            _labels.Add(CreateLabel(i, resolvedFont, layerIsValid ? layerId : (int?)null));
    }

    private TextMeshPro CreateLabel(int index, TMP_FontAsset resolvedFont, int? layerId)
    {
        // TextMeshPro (the 3D one) lays out against a RectTransform, so the GameObject is built with one
        // up front — the same reason FloatingTextManager.Create does it this way.
        var go = new GameObject("z" + index, typeof(RectTransform));
        go.transform.SetParent(_container, false);

        var label = go.AddComponent<TextMeshPro>();
        if (resolvedFont != null) label.font = resolvedFont;

        label.text = glyph;
        label.color = color;
        label.alpha = 0f;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;

        // Reads the per-label material instance TMP creates, so the outline is local to these glyphs.
        label.outlineColor = outlineColor;
        label.outlineWidth = outlineWidth;

        // Roomy box so a single glyph is never clipped or nudged by layout.
        label.rectTransform.sizeDelta = new Vector2(2f, 2f);

        var meshRenderer = go.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            if (layerId.HasValue) meshRenderer.sortingLayerID = layerId.Value;
            meshRenderer.sortingOrder = sortingOrder;
        }

        return label;
    }

    // The board's shared text font, so the z's sit alongside the floating popups instead of falling back
    // to the TMP project default. Loaded straight from Resources rather than through
    // FloatingTextManager.Instance, which would bootstrap that manager as a side effect of a minion spawning.
    private TMP_FontAsset ResolveFont()
    {
        if (font != null) return font;

        var config = Resources.Load<FloatingTextConfig>(FloatingTextConfig.ResourcePath);
        return config != null ? config.defaultFont : null;
    }

    // DOTween outlives the GameObject, so a minion destroyed (or disabled) mid-loop must not leave a
    // tween writing into dead labels.
    private void OnDisable() => KillTweens();

    private void OnDestroy() => KillTweens();
}
