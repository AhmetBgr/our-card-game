using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// One line of hand-holding, shown only during the tutorial match: "Drag here to play", "Select a Tile To
/// Summon", and whatever comes next.
///
/// The tutorial-only rule lives HERE rather than at each call site, so a prompt added later cannot forget
/// it and start nagging experienced players. <see cref="Hide"/> is deliberately NOT gated the same way —
/// hiding has to work unconditionally, or a prompt could be stranded on screen.
///
/// The object is authored INACTIVE and switched on around the fade, so outside the tutorial a prompt costs
/// nothing at all: no layout, no draw call, no TMP mesh rebuild. That is also why nothing is done in Awake
/// — Awake never runs while the object is switched off, so <see cref="Show"/> establishes its own
/// preconditions instead.
/// </summary>
public class TutorialPrompt : MonoBehaviour
{
    [Tooltip("Faded 0 -> 1 to bring the prompt in. On this object, so the whole prompt (label, plus any " +
             "backing plate or arrow added later) is driven by one alpha. Keep blocksRaycasts OFF, or the " +
             "prompt swallows clicks meant for the thing it is pointing at.")]
    [SerializeField] private CanvasGroup group;

    [Tooltip("The wording. Optional: leave it empty for a prompt whose text is never set from code. " +
             "Callers may pass their own message to Show(), which overwrites this label; passing nothing " +
             "keeps whatever is authored here, which is the usual case.")]
    [SerializeField] private TMP_Text label;

    [Tooltip("Seconds for the fade, in each direction.")]
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Breathing")]

    [Tooltip("Alpha the prompt sinks to at the bottom of each breath, then rises from again. Set it to " +
             "breatheMaxAlpha (or the duration to 0) to switch breathing off and leave the prompt steady.")]
    [SerializeField, Range(0f, 1f)] private float breatheMinAlpha = 0.55f;

    [Tooltip("Alpha at the top of each breath — and what the prompt fades IN to, so the arrival lands " +
             "exactly where the breath starts rather than dipping to meet it. Drop it below 1 for a " +
             "prompt that never sits fully opaque over the board.")]
    [SerializeField, Range(0f, 1f)] private float breatheMaxAlpha = 1f;

    [Tooltip("Seconds for ONE HALF of the breath — down to breatheMinAlpha, or back up. A full cycle is " +
             "twice this.")]
    [SerializeField] private float breatheDuration = 0.9f;

    private Tween fade;
    private Tween breathe;

    // What the prompt is currently asked to be, as opposed to what the fade has got to so far. Both
    // entry points are idempotent against this: a caller may drive them from a per-frame predicate
    // (SwitchController does, mirroring CardController's per-frame playability check), and restarting
    // the fade on every one of those calls would pin it at its start value forever.
    private bool shown;
    private string currentMessage;

    /// <summary>
    /// Bring the prompt in. No-op outside the tutorial match, and safe to call repeatedly. Pass
    /// <paramref name="message"/> to override the authored wording, or nothing to keep it.
    /// </summary>
    public void Show(string message = null)
    {
        if (!GameManager.IsTutorialMatch) return;
        if (group == null) return;

        if (shown)
        {
            // Already up. Only the wording can still change — swap it in place rather than fading the
            // prompt out and back in for what is the same prompt saying something new.
            if (label != null && !string.IsNullOrEmpty(message) && message != currentMessage)
            {
                label.text = message;
                currentMessage = message;
            }

            return;
        }

        shown = true;
        currentMessage = message;

        if (label != null && !string.IsNullOrEmpty(message)) label.text = message;

        KillTweens();

        // Only wind the alpha back when the prompt is actually at rest. A Show that interrupts a fade out
        // must pick up from where that fade got to, rather than snapping to invisible and starting over.
        if (!gameObject.activeSelf) group.alpha = 0f;

        gameObject.SetActive(true);

        // The breath only starts once the prompt has finished arriving — see StartBreathe.
        fade = group.DOFade(breatheMaxAlpha, fadeDuration).SetUpdate(true).OnComplete(StartBreathe);
    }

    /// <summary>
    /// The idle pulse an active prompt keeps up, so a hint that has been on screen a while still reads as
    /// live rather than as part of the board.
    ///
    /// Strictly sequential with the fade rather than concurrent, because both drive the same alpha: this
    /// starts on the fade-in's completion, and <see cref="Hide"/> kills it before fading out. Same ease and
    /// yoyo as <see cref="ManaBarSlider"/>'s breathe, so the two read as one idiom rather than two.
    /// </summary>
    private void StartBreathe()
    {
        if (group == null) return;

        // Switched off: a floor at or above the ceiling has nowhere to breathe to, and a zero duration has
        // no time to do it in. Either way the prompt is left sitting at breatheMaxAlpha, where the fade in
        // already put it, rather than running a degenerate tween.
        if (breatheMinAlpha >= breatheMaxAlpha || breatheDuration <= 0f) return;

        if (breathe != null) breathe.Kill();

        breathe = group.DOFade(breatheMinAlpha, breatheDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    /// <summary>
    /// Fade the prompt out and switch it off once the fade lands, so it is never cut mid-fade. Safe to
    /// call on an already-resting prompt, which is every call of every non-tutorial match.
    /// </summary>
    public void Hide()
    {
        if (group == null || !shown) return;

        shown = false;

        // Kills the breath too, so the fade out starts from wherever the pulse had got to and carries it
        // smoothly down rather than fighting it for the same alpha.
        KillTweens();

        // Kill() does not fire OnComplete, so a Show that interrupts this cannot switch the prompt back
        // off behind the fade in that just replaced it.
        fade = group.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(HideImmediate);
    }

    /// <summary>Straight to rest, no fade. For teardown, where a fade would never get to finish.</summary>
    public void HideImmediate()
    {
        shown = false;

        KillTweens();

        if (group != null) group.alpha = 0f;
        gameObject.SetActive(false);
    }

    // Covers the scene going away mid-fade or mid-breath, and the SetActive(false) above.
    private void OnDisable() => KillTweens();

    private void KillTweens()
    {
        if (fade != null) fade.Kill();
        fade = null;

        if (breathe != null) breathe.Kill();
        breathe = null;
    }
}
