using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Drives the new mana bar visual: slides a "bar parent" transform to a marker
/// position matching the current mana, and updates the mana text.
/// Marker positions are indexed by mana amount (index 0 = 0 mana, index 3 = 3 mana, ...).
/// </summary>
public class ManaBarSlider : MonoBehaviour
{
    // Single scene instance, so card hover code can drive the play-preview.
    public static ManaBarSlider Instance { get; private set; }

    [Tooltip("The object that slides between mana positions.")]
    public Transform barParent;
    [Tooltip("Second bar that mirrors barParent on real mana changes; on hover it breathes alpha instead of moving.")]
    public Transform barParent2;


    [Tooltip("Target positions indexed by mana amount. Element 0 = 0 mana, element 1 = 1 mana, etc.")]
    public Transform[] manaPositions;

    [Tooltip("Text that shows current/max mana.")]
    public TextMeshProUGUI manaText;

    [Header("Movement")]
    public float moveDuration = 0.3f;
    public Ease moveEase = Ease.OutCubic;

    [Header("Hover breathe (barParent2)")]
    public float breatheDuration = 0.6f;
    [Range(0f, 1f)] public float breatheMinAlpha = 0.3f;

    [Header("Mana text punch")]
    public float punchScale = 0.3f;
    public float punchDuration = 0.3f;

    private Tween moveTween;
    private Tween moveTween2;
    private Tween breatheTween;
    private Tween punchTween;
    private CanvasGroup barParent2Group;

    // The player's actual mana, so a play-preview can be reverted back to it.
    private int currentMana;

    private void Awake()
    {
        Instance = this;

        if (barParent2 != null)
        {
            barParent2Group = barParent2.GetComponent<CanvasGroup>();
            if (barParent2Group == null) barParent2Group = barParent2.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        Player.OnPlayerManaChanged += HandleManaChanged;

        currentMana = GameManager.Instance.player.availibleMana;
        MoveBothToMana(currentMana, instant: true);
        UpdateText(currentMana);
    }

    private void OnDestroy()
    {
        Player.OnPlayerManaChanged -= HandleManaChanged;
        moveTween?.Kill();
        moveTween2?.Kill();
        breatheTween?.Kill();
        punchTween?.Kill();
        if (Instance == this) Instance = null;
    }

    private void HandleManaChanged(int newValue, int oldValue)
    {
        currentMana = newValue;
        MoveBothToMana(newValue, instant: false);
        UpdateText(newValue);
        PunchManaText();
    }

    // Scale-punch the mana text on a mana change. Skips if a punch is already running,
    // so rapid changes don't restart or stack the same animation.
    private void PunchManaText()
    {
        if (manaText == null) return;
        if (punchTween != null && punchTween.IsActive() && punchTween.IsPlaying()) return;

        punchTween = manaText.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, elasticity: 0, vibrato: 0);
    }

    /// <summary>
    /// Preview where the bar would land if a card of the given cost were played.
    /// barParent snaps instantly to the post-play position (no animation); barParent2
    /// stays put and breathes its alpha. Call <see cref="ClearPreview"/> to revert.
    /// </summary>
    public void PreviewPlay(int cost)
    {
        SnapBarToMana(barParent, currentMana - cost);
        StartBreathe();
    }

    /// <summary>Snap barParent back to the real mana and stop barParent2 breathing.</summary>
    public void ClearPreview()
    {
        SnapBarToMana(barParent, currentMana);
        StopBreathe();
    }

    // Moves both bars to the mana marker (used for real mana changes only).
    private void MoveBothToMana(int mana, bool instant)
    {
        if (!TryGetManaY(mana, out float targetY)) return;

        moveTween?.Kill();
        moveTween2?.Kill();

        if (instant)
        {
            SetBarY(barParent, targetY);
            SetBarY(barParent2, targetY);
        }
        else
        {
            if (barParent != null) moveTween = barParent.DOMoveY(targetY, moveDuration).SetEase(moveEase);
            if (barParent2 != null) moveTween2 = barParent2.DOMoveY(targetY, moveDuration).SetEase(moveEase);
        }
    }

    // Instantly places a single bar at the mana marker (used by the hover preview).
    private void SnapBarToMana(Transform bar, int mana)
    {
        if (bar == null || !TryGetManaY(mana, out float targetY)) return;

        if (bar == barParent) moveTween?.Kill();
        SetBarY(bar, targetY);
    }

    private void StartBreathe()
    {
        if (barParent2Group == null) return;
        breatheTween?.Kill();
        barParent2Group.alpha = 1f;
        breatheTween = barParent2Group.DOFade(breatheMinAlpha, breatheDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopBreathe()
    {
        if (barParent2Group == null) return;
        breatheTween?.Kill();
        barParent2Group.alpha = 1f;
    }

    private bool TryGetManaY(int mana, out float y)
    {
        y = 0f;
        if (manaPositions == null || manaPositions.Length == 0) return false;

        int index = Mathf.Clamp(mana, 0, manaPositions.Length - 1);
        Transform target = manaPositions[index];
        if (target == null) return false;

        // Vertical-only: take the marker's Y, keep each bar's own X and Z.
        y = target.position.y;
        return true;
    }

    private static void SetBarY(Transform bar, float y)
    {
        if (bar == null) return;
        Vector3 p = bar.position;
        p.y = y;
        bar.position = p;
    }

    private void UpdateText(int currentMana)
    {
        if (manaText == null) return;

        manaText.text = currentMana + "/" + GameManager.Instance.maxMana;
    }
}
