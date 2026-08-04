using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// A single floating world-space label: pops in, rises, then fades out and returns itself to the pool.
/// Nothing here knows what the message means — <see cref="FloatingTextStyle"/> supplies the whole look and
/// timing, so the same component renders "Crippled", a heal number, or anything else.
/// Instances are created and recycled by <see cref="FloatingTextManager"/>; don't add this by hand.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class FloatingText : MonoBehaviour
{
    private TextMeshPro _label;
    private MeshRenderer _meshRenderer;

    // The anchor the popup rides (usually the minion) plus the offset it keeps from it. Tracking happens
    // in LateUpdate rather than as a DOMove so the popup can both follow a moving minion AND rise at once.
    private Transform _anchor;
    private Vector3 _anchorOffset;

    // Where the popup would sit with no rise applied. Re-cached from the anchor every frame while it is
    // alive, so when the anchor is destroyed mid-flight (the minion died) the popup finishes rising from
    // the last place it saw rather than snapping back to its spawn point.
    private Vector3 _basePosition;
    private Vector3 _riseOffset;
    private float _riseProgress;

    private Sequence _sequence;
    private Action<FloatingText> _onFinished;
    private bool _playing;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (_label == null) _label = GetComponent<TextMeshPro>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Shows <paramref name="text"/> at <paramref name="worldPos"/> using <paramref name="style"/>.
    /// Pass an <paramref name="anchor"/> to have the popup follow a moving transform (the style's
    /// followAnchor decides whether it actually tracks), along with the <paramref name="anchorOffset"/> it
    /// should hold from that anchor — normally exactly the offset already baked into
    /// <paramref name="worldPos"/>. Pass a null anchor to pin the popup where it spawned.
    /// <paramref name="onFinished"/> fires once the fade completes, and is how the manager reclaims this.
    /// </summary>
    public void Play(string text, FloatingTextStyle style, Vector3 worldPos, Transform anchor, Vector3 anchorOffset, Action<FloatingText> onFinished)
    {
        if (style == null) return;

        CacheComponents();

        _sequence?.Kill();
        _onFinished = onFinished;

        ApplyStyle(text, style);

        // Spread stacked popups sideways so two landing on the same minion in the same beat stay readable.
        float jitter = style.horizontalJitter > 0f
            ? UnityEngine.Random.Range(-style.horizontalJitter, style.horizontalJitter)
            : 0f;
        var jitterOffset = new Vector3(jitter, 0f, 0f);

        _anchor = style.followAnchor ? anchor : null;
        _anchorOffset = anchorOffset + jitterOffset;
        _basePosition = worldPos + jitterOffset;
        _riseOffset = style.riseOffset;
        _riseProgress = 0f;
        _playing = true;

        transform.position = _basePosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one * style.startScale;

        // Inserted rather than appended: the rise spans the entire lifetime while the pop-in and the fade
        // occupy its head and tail, so all three overlap on one timeline instead of running back to back.
        _sequence = DOTween.Sequence().SetTarget(this);
        _sequence.Insert(0f, transform.DOScale(style.endScale, style.popInDuration).SetEase(style.popInEase));
        _sequence.Insert(0f, DOTween.To(() => _riseProgress, v => _riseProgress = v, 1f, style.TotalDuration)
            .SetEase(style.riseEase));
        _sequence.Insert(style.popInDuration + style.holdDuration,
            DOTween.To(() => _label.alpha, v => _label.alpha = v, 0f, style.fadeDuration).SetEase(style.fadeEase));
        _sequence.OnComplete(Finish);
    }

    private void ApplyStyle(string text, FloatingTextStyle style)
    {
        _label.text = text;
        _label.color = style.color;
        _label.alpha = 1f;
        _label.fontSize = style.fontSize;
        _label.fontStyle = style.fontStyle;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableAutoSizing = false;
        _label.overflowMode = TextOverflowModes.Overflow;

        // Resolved every time rather than only when the style names a font: these labels are pooled, so a
        // popup that inherits its font from the config must not keep whatever font the previous user set.
        TMP_FontAsset font = style.font != null ? style.font : FloatingTextManager.Instance?.Config.defaultFont;
        if (font != null && _label.font != font)
            _label.font = font;

        // Reads the outline off the material instance TMP creates per label, so styles can differ per popup.
        _label.outlineColor = style.outlineColor;
        _label.outlineWidth = style.outlineWidth;

        // Generous box so a long message never wraps; Overflow above means nothing is clipped either.
        _label.rectTransform.sizeDelta = new Vector2(8f, 2f);

        int layerId = SortingLayer.NameToID(style.sortingLayer);
        if (!string.IsNullOrEmpty(style.sortingLayer) && SortingLayer.IsValid(layerId))
            _meshRenderer.sortingLayerID = layerId;
        _meshRenderer.sortingOrder = style.sortingOrder;
    }

    private void LateUpdate()
    {
        if (!_playing) return;

        // Unity's overloaded null check covers a destroyed anchor, which just freezes _basePosition.
        if (_anchor != null) _basePosition = _anchor.position + _anchorOffset;

        transform.position = _basePosition + _riseOffset * _riseProgress;
    }

    private void Finish()
    {
        _playing = false;
        _sequence = null;
        _anchor = null;

        Action<FloatingText> callback = _onFinished;
        _onFinished = null;
        callback?.Invoke(this);
    }

    /// <summary>Stops the popup immediately without running its finished callback (used when pooling it away).</summary>
    public void Cancel()
    {
        _sequence?.Kill();
        _sequence = null;
        _playing = false;
        _anchor = null;
        _onFinished = null;
    }

    private void OnDisable()
    {
        // The tween outlives the GameObject, so a popup disabled mid-flight (scene teardown, pool reclaim)
        // must not leave a sequence writing into a dead label.
        _sequence?.Kill();
        _sequence = null;
        _playing = false;
    }
}
