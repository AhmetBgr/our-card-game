using DG.Tweening;
using UnityEngine;

// Drives the "New Record!" badge. Everything hangs off OnEnable, so whoever switches the object on gets
// the animation for free: it scales up from nothing, then settles into a loop that pulses its size and
// rocks it from side to side for as long as the badge is showing. Both loops are relative to whatever the
// object was authored at, so moving, resizing or re-angling it in the inspector needs no code change.
public class NewRecordFlourish : MonoBehaviour
{
    [Header("Appear")]
    [Tooltip("How long the badge takes to scale up from nothing.")]
    [SerializeField] private float popDuration = 0.45f;
    [Tooltip("Overshooting eases (OutBack) give the badge a bit of punch as it lands.")]
    [SerializeField] private Ease popEase = Ease.OutBack;

    [Header("Looped pulse")]
    [Tooltip("Peak size of the pulse, as a multiple of the authored scale. 1 = no pulse.")]
    [SerializeField] private float pulseScale = 1.08f;
    [Tooltip("Seconds for one half of the pulse (out, then back).")]
    [SerializeField] private float pulseDuration = 0.7f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;

    [Header("Looped rocking")]
    [Tooltip("Z swing either side of the authored angle, in degrees: the badge rocks from -this to +this " +
             "and back, forever. 0 = no rocking.")]
    [SerializeField] private float rockDegrees = 6f;
    [Tooltip("Seconds for a full sweep from one extreme to the other. Leave it out of step with the pulse " +
             "duration so the two loops do not lock into the same rhythm.")]
    [SerializeField] private float rockDuration = 1.1f;
    [SerializeField] private Ease rockEase = Ease.InOutSine;

    private Vector3 baseScale = Vector3.one;
    private Vector3 baseEuler;
    private bool baseStateCaptured;
    private Tween popTween;
    private Tween pulseTween;
    private Tween rockTween;

    private void Awake()
    {
        CaptureBaseState();
    }

    // OnEnable can run before Awake when the object is enabled in the same frame it is created, so the
    // authored scale and rotation are captured on whichever happens first.
    private void CaptureBaseState()
    {
        if (baseStateCaptured) return;

        baseScale = transform.localScale;
        baseEuler = transform.localEulerAngles;
        baseStateCaptured = true;
    }

    private void OnEnable()
    {
        CaptureBaseState();
        KillTweens();

        transform.localScale = Vector3.zero;
        transform.localEulerAngles = baseEuler;

        // Unscaled so the badge keeps moving if the game is paused behind the panel.
        popTween = transform.DOScale(baseScale, popDuration)
            .SetEase(popEase)
            .SetUpdate(true)
            .OnComplete(StartLoops);
    }

    private void OnDisable()
    {
        KillTweens();
        if (!baseStateCaptured) return;

        transform.localScale = baseScale;
        transform.localEulerAngles = baseEuler;
    }

    // Started from the pop's OnComplete rather than appended to a sequence: DOTween rejects infinitely
    // looping tweens inside a Sequence.
    private void StartLoops()
    {
        popTween = null;
        StartPulse();
        StartRocking();
    }

    private void StartPulse()
    {
        if (pulseScale <= 1f || pulseDuration <= 0f) return;

        pulseTween = transform.DOScale(baseScale * pulseScale, pulseDuration)
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    // The rock has to swing both ways around the authored angle, but a yoyo loop only bounces between its
    // start and end. So it leans one way over half a sweep first, and the endless yoyo then runs between
    // the two extremes - which keeps the authored angle as the centre of the swing with no jump to get there.
    private void StartRocking()
    {
        if (rockDegrees <= 0f || rockDuration <= 0f) return;

        Vector3 lean = new Vector3(0f, 0f, rockDegrees);

        rockTween = transform.DOLocalRotate(baseEuler + lean, rockDuration * 0.5f)
            .SetEase(rockEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                rockTween = transform.DOLocalRotate(baseEuler - lean, rockDuration)
                    .SetEase(rockEase)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            });
    }

    private void KillTweens()
    {
        popTween?.Kill();
        popTween = null;
        pulseTween?.Kill();
        pulseTween = null;
        rockTween?.Kill();
        rockTween = null;
    }
}
