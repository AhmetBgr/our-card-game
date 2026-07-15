using UnityEngine;
using DG.Tweening;

/// <summary>
/// Continuously rotates the attached object using DOTween.
/// All animation values are tweakable in the Inspector.
/// </summary>
public class RotationLoop : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Degrees to rotate over one cycle (per axis).")]
    public Vector3 rotateBy = new Vector3(0f, 0f, 360f);

    [Tooltip("Rotate in local space or world space.")]
    public Space space = Space.Self;

    [Tooltip("How the target rotation is interpreted.\nFast = shortest path, LocalAxisAdd = keeps spinning past 360 (recommended for full loops).")]
    public RotateMode rotateMode = RotateMode.FastBeyond360;

    [Header("Timing")]
    [Tooltip("Seconds to complete one rotation cycle.")]
    [Min(0.01f)]
    public float duration = 2f;

    [Tooltip("Delay before the rotation starts (seconds).")]
    public float startDelay = 0f;

    [Tooltip("Easing curve applied to the rotation.")]
    public Ease ease = Ease.Linear;

    [Header("Loop")]
    [Tooltip("Number of loops. -1 = infinite.")]
    public int loops = -1;

    [Tooltip("How each loop behaves.\nRestart = snap back and repeat, Incremental = keep adding rotation, Yoyo = rotate back and forth.")]
    public LoopType loopType = LoopType.Incremental;

    [Header("Playback")]
    [Tooltip("Start rotating automatically on enable.")]
    public bool playOnEnable = true;

    [Tooltip("Ignore Time.timeScale (keeps spinning while the game is paused).")]
    public bool ignoreTimeScale = false;

    private Tween _tween;

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        Kill();
    }

    /// <summary>Starts (or restarts) the rotation loop.</summary>
    public void Play()
    {
        Kill();

        // Target = current rotation + rotateBy, evaluated in the chosen space.
        Tweener rotation = space == Space.World
            ? transform.DORotate(transform.eulerAngles + rotateBy, duration, rotateMode)
            : transform.DOLocalRotate(transform.localEulerAngles + rotateBy, duration, rotateMode);

        _tween = rotation
            .SetEase(ease)
            .SetLoops(loops, loopType)
            .SetDelay(startDelay)
            .SetUpdate(ignoreTimeScale);
    }

    /// <summary>Pauses the rotation.</summary>
    public void Pause() => _tween?.Pause();

    /// <summary>Resumes a paused rotation.</summary>
    public void Resume() => _tween?.Play();

    /// <summary>Stops and clears the rotation tween.</summary>
    public void Kill()
    {
        if (_tween != null && _tween.IsActive())
            _tween.Kill();
        _tween = null;
    }
}
