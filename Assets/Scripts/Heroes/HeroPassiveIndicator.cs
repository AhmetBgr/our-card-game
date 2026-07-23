using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// One passive's icon on the hero's indicator row. Dumb view: it renders whatever HeroPassiveDisplay
/// it is handed and knows nothing about specific passives, so a new passive needs no code here.
///
/// Lives on Assets/Prefabs/UI/PassiveIndicator.prefab, nested inside PassiveUICanvas.prefab — which
/// Agent.SpawnPassiveUI instantiates under the agent's passiveUIPos anchor and hands to
/// HeroPassiveIndicatorView, the only thing that drives it.
/// </summary>
public class HeroPassiveIndicator : MonoBehaviour
{
    [SerializeField] private Image iconImage;

    [Header("Tooltip")]
    [Tooltip("Panel shown while the icon is hovered. Starts inactive; toggled by pointer enter/exit.")]
    [SerializeField] private GameObject tooltipRoot;

    [Tooltip("Renders the passive description, highlighted the same way card descriptions are.")]
    [SerializeField] private TMP_Text tooltipText;

    [Tooltip("Ring drawn around the icon for PassiveIndicatorType.Aura.")]
    [SerializeField] private GameObject auraOutline;

    [Tooltip("Countdown ring shown for PassiveIndicatorType.Counter.")]
    [SerializeField] private GameObject counterRoot;

    [Tooltip("Radial-filled Image inside counterRoot. Drains 1 -> 0 as the counter reaches zero.")]
    [SerializeField] private Image counterFill;

    [SerializeField] private TMP_Text counterText;

    [Tooltip("Icon tint while the passive is doing something.")]
    [SerializeField] private Color activeTint = Color.white;

    [Tooltip("Icon tint while the passive is present but dormant.")]
    [SerializeField] private Color dormantTint = new Color(1f, 1f, 1f, 0.35f);

    [Header("Counter Animation")]
    [Tooltip("Seconds the ring takes to drain one counter unit.")]
    [SerializeField] private float counterSecondsPerUnit = 0.09f;
    [SerializeField] private float counterMinDuration = 0.12f;

    [Tooltip("Ceiling, so a huge overflowing hit cannot stall the board on a long drain.")]
    [SerializeField] private float counterMaxDuration = 0.9f;

    [Header("Proc Flash")]
    [SerializeField] private float procScale = 1.35f;
    [SerializeField] private float procPunchDuration = 0.12f;
    [SerializeField] private float procSettleDuration = 0.18f;

    /// <summary>The passive this slot renders. Set by Bind; used to route proc flashes.</summary>
    public HeroPassiveSO Passive { get; private set; }

    // Same lazy Resources load CardView uses, so passive tooltips and card descriptions are highlighted
    // from one config and can never drift apart. Null-safe: a missing asset renders plain text.
    private static CardTextHighlightConfig highlightConfig;
    private static CardTextHighlightConfig HighlightConfig =>
        highlightConfig != null ? highlightConfig : (highlightConfig = Resources.Load<CardTextHighlightConfig>("CardTextHighlightConfig"));

    private Tween _procTween;
    private Tween _counterTween;

    // Raw description last pushed, plus its formatted form. Cached because Format runs a regex pass and
    // the body only changes when the bound passive does — not on every counter tick.
    private string _tooltipBody;
    private string _tooltipFormatted;

    // The tooltip's authored offset, cached before anything moves it so every flip is derived from the
    // prefab value rather than from whatever the last flip left behind. See ApplyTooltipPlacement.
    private RectTransform _tooltipRect;
    private Vector2 _tooltipAnchoredPos;
    private Vector3 _tooltipLocalPos;

    // What the ring is showing right now, which during an animation is NOT display.counterValue.
    // Kept so the next animation starts from where the last one actually ended rather than snapping.
    private float _shownRemaining;
    private int _shownMax = 1;

    public void Bind(HeroPassiveSO passive, in HeroPassiveDisplay display)
    {
        Passive = passive;
        Apply(display);
    }

    /// <summary>Snaps to the display with no animation. Used on Bind and for every non-counter change.</summary>
    public void Apply(in HeroPassiveDisplay display)
    {
        KillCounterTween();
        ApplyChrome(display);

        if (display.visible && display.type == PassiveIndicatorType.Counter)
            ShowCounter(display.counterValue, display.counterMax);
    }

    /// <summary>
    /// Drains the ring to the new value, passing THROUGH zero once per completed stack: empty, punch,
    /// snap back to full, keep draining with what's left over. A 6-damage hit on a per-5 passive reads
    /// as 5→0 (punch) then 5→4, rather than silently wrapping to 4 and looking like a 1-point tick.
    ///
    /// cyclesCrossed is the number of stacks completed since the last display — see
    /// HeroPassiveDisplay.counterStacks. Zero means a plain drain with no proc.
    /// </summary>
    public void ApplyAnimated(in HeroPassiveDisplay display, int cyclesCrossed)
    {
        ApplyChrome(display);

        if (!display.visible || display.type != PassiveIndicatorType.Counter)
        {
            KillCounterTween();
            return;
        }

        int max = Mathf.Max(1, display.counterMax);
        int target = Mathf.Clamp(display.counterValue, 0, max);

        // Start from whatever is on screen, unless the scale changed underneath us (a rebind to a
        // different passive), in which case there is no meaningful position to travel from.
        float from = _shownMax == max ? Mathf.Clamp(_shownRemaining, 0f, max) : max;

        // Total ground to cover: down to zero once per crossed stack, then on to the remainder.
        float travel = from + (Mathf.Max(0, cyclesCrossed) * max) - target;

        // Counter went UP (a heal rewinding the count, or a fresh bind) — nothing to drain, just show it.
        if (!isActiveAndEnabled || travel <= 0.0001f)
        {
            KillCounterTween();
            ShowCounter(target, max);
            return;
        }

        KillCounterTween();

        float duration = Mathf.Clamp(travel * counterSecondsPerUnit, counterMinDuration, counterMaxDuration);
        int punched = 0;
        float t = 0f;

        _counterTween = DOTween.To(() => t, x =>
        {
            t = x;

            // How many full blocks we have drained through at this point in the travel. Clamped to the
            // known stack count so a RuntimeCounter (cyclesCrossed 0) can rest on a legitimate 0
            // instead of wrapping back up to full.
            int crossings = t >= from ? Mathf.FloorToInt((t - from) / max) + 1 : 0;
            crossings = Mathf.Min(crossings, Mathf.Max(0, cyclesCrossed));

            if (crossings > punched)
            {
                punched = crossings;
                PlayProcFlash();
            }

            ShowCounter(from - t + (crossings * max), max);
        }, travel, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() => ShowCounter(target, max));
    }

    /// <summary>
    /// Shows the description panel. The text is formatted here rather than on Apply so a passive whose
    /// icon is never hovered costs no regex work at all.
    ///
    /// Physics-based (OnMouseEnter + a Collider2D) rather than IPointerEnterHandler, matching
    /// MinionController and CellController. This is not a style choice: CardPlayArea is a screen-space
    /// OVERLAY raycast target covering the board, and overlay raycasters always outrank world-space
    /// ones — so an EventSystem pointer never reaches anything on the hero's world-space canvas. It
    /// cannot simply be switched off either; PlayArea implements IDropHandler and needs it for card
    /// drops. Legacy mouse events bypass the EventSystem entirely and are unaffected.
    /// </summary>
    private void OnMouseEnter()
    {
        if (tooltipRoot == null || string.IsNullOrEmpty(_tooltipBody)) return;

        ApplyTooltipPlacement();

        if (tooltipText != null)
        {
            if (_tooltipFormatted == null)
                _tooltipFormatted = CardTextFormatter.Format(_tooltipBody, HighlightConfig);

            tooltipText.text = _tooltipFormatted;
        }

        tooltipRoot.SetActive(true);
    }

    private void OnMouseExit() => HideTooltip();

    /// <summary>
    /// Puts the tooltip on the side of the icon that has room for it. The prefab authors the panel
    /// ABOVE the icon, which is right for a row on the near half of the board but runs off the top of
    /// the screen for one on the far half — so an indicator sitting above the world origin gets the
    /// vertically mirrored offset instead.
    ///
    /// Only Y is negated. The authored X offset keeps the panel inside the board horizontally and is
    /// correct on both halves; mirroring it too would swing the panel to the opposite side for no
    /// reason. Re-evaluated on every hover rather than once at startup, so it cannot be wrong if the
    /// row is ever re-anchored after Awake.
    /// </summary>
    private void ApplyTooltipPlacement()
    {
        if (tooltipRoot == null) return;

        bool flip = transform.position.y > 0f;

        if (_tooltipRect != null)
        {
            _tooltipRect.anchoredPosition = flip
                ? new Vector2(_tooltipAnchoredPos.x, -_tooltipAnchoredPos.y)
                : _tooltipAnchoredPos;
        }
        else
        {
            tooltipRoot.transform.localPosition = flip
                ? new Vector3(_tooltipLocalPos.x, -_tooltipLocalPos.y, _tooltipLocalPos.z)
                : _tooltipLocalPos;
        }
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }

    private void ApplyChrome(in HeroPassiveDisplay display)
    {
        gameObject.SetActive(display.visible);

        // Rebinding to a different passive invalidates the cached highlight pass. Compare the raw
        // string: Apply runs on every counter tick, and re-formatting there would be pure waste.
        if (_tooltipBody != display.body)
        {
            _tooltipBody = display.body;
            _tooltipFormatted = null;
            HideTooltip();
        }

        if (!display.visible)
        {
            HideTooltip();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = display.icon;
            iconImage.color = display.isActive ? activeTint : dormantTint;
        }

        // Exactly one shape is lit, chosen by display.type. Both are switched off first so a passive
        // that changes type (or a slot rebound to a different passive) can never leave the other one
        // stranded on — the authored prefab ships with both children active for editor layout.
        bool isAura = display.type == PassiveIndicatorType.Aura;
        bool isCounter = display.type == PassiveIndicatorType.Counter;

        if (auraOutline != null) auraOutline.SetActive(isAura);
        if (counterRoot != null) counterRoot.SetActive(isCounter);
    }

    /// <summary>Pushes a (possibly fractional, mid-animation) remaining count to the ring and its text.</summary>
    private void ShowCounter(float remaining, int max)
    {
        _shownRemaining = remaining;
        _shownMax = max;

        // Ceil, so the text only ticks down as the ring fully clears each unit, and so a resting
        // integer value renders exactly as itself.
        if (counterText != null) counterText.text = Mathf.CeilToInt(remaining).ToString();
        if (counterFill != null) counterFill.fillAmount = Mathf.Clamp01(remaining / max);
    }

    private void KillCounterTween()
    {
        _counterTween?.Kill();
        _counterTween = null;
    }

    /// <summary>
    /// Pop the icon when its passive fires. Pure view feedback — safe to call from inside GameManager's
    /// dispatch loop because it touches nothing but this transform.
    /// </summary>
    public void PlayProcFlash()
    {
        if (!isActiveAndEnabled) return;

        // Same hygiene as MinionView/MinionController: kill the previous tween before restarting, so
        // rapid re-procs can't leave the icon stuck mid-scale.
        _procTween?.Kill();
        transform.localScale = Vector3.one;

        _procTween = DOTween.Sequence()
            .Append(transform.DOScale(Vector3.one * procScale, procPunchDuration).SetEase(Ease.OutBack))
            .Append(transform.DOScale(Vector3.one, procSettleDuration).SetEase(Ease.InOutSine));
    }

    private void Awake()
    {
        // Cache the authored offset first: HideTooltip and every later flip must never be the thing
        // that defines "the prefab value".
        if (tooltipRoot != null)
        {
            _tooltipRect = tooltipRoot.transform as RectTransform;

            if (_tooltipRect != null) _tooltipAnchoredPos = _tooltipRect.anchoredPosition;
            else _tooltipLocalPos = tooltipRoot.transform.localPosition;
        }

        HideTooltip();
    }

    private void OnDisable()
    {
        _procTween?.Kill();
        _procTween = null;
        transform.localScale = Vector3.one;

        // A slot hidden mid-hover never gets its pointer-exit, so the panel would stay up and reappear
        // with the next passive bound to this slot.
        HideTooltip();

        // Land on the resting value rather than freezing mid-drain: a slot disabled halfway through an
        // animation would otherwise come back showing a stale fractional ring.
        KillCounterTween();
        if (_shownMax > 0) ShowCounter(Mathf.Round(_shownRemaining), _shownMax);
    }
}
