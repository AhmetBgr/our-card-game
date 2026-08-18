using UnityEngine;

/// <summary>
/// Tilts the card toward the pointer's offset from its own center while it's peeking (CardController's
/// hover-zoom state) — a light parallax so the card feels handled, not flat.
///
/// The pointer offset is measured against a screen-space reference rect CACHED once when peeking
/// starts (when the card is guaranteed unrotated — CardController.OnPointerEnter resets rotation to
/// identity right before peeking begins), not re-derived from the live transform every frame. Doing
/// the latter is a feedback loop: RectTransformUtility.ScreenPointToLocalPointInRectangle measures
/// against the rect's CURRENT world matrix, which is the very thing this script is rotating — once
/// tilted, the local axes used to interpret the *next* mouse position are themselves skewed by the
/// rotation already applied, corrupting the reading it's derived from (verified directly: the same
/// fixed screen point maps to a different local point once the rect is rotated). That was the actual
/// bug behind "tilts correctly on one side, wrong on the other" — not an axis-composition issue.
/// Measuring against a fixed snapshot instead removes the self-reference.
///
/// X (pitch, from vertical offset) and Y (yaw, from horizontal offset) — the original two-axis design.
///
/// Only active during isPeeking: that's the one window nothing else drives this transform's rotation
/// (peek removes the card from CardHandLayout's list), so there's nothing to fight. The moment peeking
/// ends, this stops touching rotation entirely — no eased hand-off — letting CardHandLayout's own lerp
/// (re-fan on peek end) take over cleanly, and leaving draw/turn-into DOTween animations (which drive Y
/// on non-peeking cards) completely alone.
///
/// Gated on SaveManager.Instance.HoverTiltEnabled, the persisted player preference (same pattern as
/// ActionLogPanel/ShowActionLog) — on by default, off if the player disables it.
/// </summary>
[RequireComponent(typeof(CardController))]
public class CardHoverTilt : MonoBehaviour
{
    [Header("Tilt")]
    [Tooltip("Max degrees the card pitches (rotates on X) when the pointer is at the top/bottom edge.")]
    public float maxTiltX = 14f;
    [Tooltip("Max degrees the card yaws (rotates on Y) when the pointer is at the left/right edge.")]
    public float maxTiltY = 14f;
    [Tooltip("How quickly the tilt eases toward the pointer and back to flat.")]
    public float tiltSpeed = 12f;
    [Tooltip("How far past the card's edge (as a fraction of its half-size) the pointer can sit and still count as \"over\" it.")]
    public float edgeSoftness = 0.15f;

    private CardController _controller;
    private RectTransform _rect;
    private float _currentX;
    private float _currentY;

    private bool _hasReference;
    private Vector2 _refCenter;
    private Vector2 _refHalfExtent;

    private void Awake()
    {
        _controller = GetComponent<CardController>();
        _rect = transform as RectTransform;
    }

    private void LateUpdate()
    {
        bool active = _controller.isPeeking && SaveManager.Instance != null && SaveManager.Instance.HoverTiltEnabled;

        if (!active)
        {
            _hasReference = false;

            bool atRest = Mathf.Abs(_currentX) < 0.05f && Mathf.Abs(_currentY) < 0.05f;
            if (atRest)
            {
                _currentX = 0f;
                _currentY = 0f;
                return;
            }

            // Ease back to flat rather than snapping — covers disabling the toggle mid-tilt.
            float tOut = Time.deltaTime * tiltSpeed;
            _currentX = Mathf.Lerp(_currentX, 0f, tOut);
            _currentY = Mathf.Lerp(_currentY, 0f, tOut);
            transform.localRotation = Quaternion.Euler(_currentX, _currentY, 0f);
            return;
        }

        if (!_hasReference)
        {
            CacheReferenceFrame();
        }

        float targetX = 0f;
        float targetY = 0f;

        if (_hasReference && TryGetNormalizedPointer(out Vector2 normalized))
        {
            targetX = -normalized.y * maxTiltX;
            targetY = normalized.x * maxTiltY;
        }

        float t = Time.deltaTime * tiltSpeed;
        _currentX = Mathf.Lerp(_currentX, targetX, t);
        _currentY = Mathf.Lerp(_currentY, targetY, t);

        transform.localRotation = Quaternion.Euler(_currentX, _currentY, 0f);
    }

    // Captures the card's current screen-space bounds as a fixed reference for this peek session.
    // Called on the first LateUpdate of a peek, before any tilt has been applied, so it reflects the
    // card's true (unrotated) footprint rather than a self-corrupted one.
    private void CacheReferenceFrame()
    {
        if (_rect == null) return;

        Vector3[] corners = new Vector3[4];
        _rect.GetWorldCorners(corners); // 0=BL, 1=TL, 2=TR, 3=BR

        // World position ≈ screen position for a Screen-Space-Overlay canvas, which is what this
        // project's cards use — no camera/projection conversion needed.
        _refCenter = (Vector2)(corners[0] + corners[2]) * 0.5f;
        _refHalfExtent = new Vector2(
            Vector2.Distance(corners[0], corners[3]) * 0.5f,
            Vector2.Distance(corners[0], corners[1]) * 0.5f);

        _hasReference = _refHalfExtent.x > 0f && _refHalfExtent.y > 0f;
    }

    private bool TryGetNormalizedPointer(out Vector2 normalized)
    {
        Vector2 mouse = Input.mousePosition;
        Vector2 offset = mouse - _refCenter;
        Vector2 n = new Vector2(offset.x / _refHalfExtent.x, offset.y / _refHalfExtent.y);

        float limit = 1f + edgeSoftness;
        if (Mathf.Abs(n.x) > limit || Mathf.Abs(n.y) > limit)
        {
            normalized = Vector2.zero;
            return false;
        }

        normalized = new Vector2(Mathf.Clamp(n.x, -1f, 1f), Mathf.Clamp(n.y, -1f, 1f));
        return true;
    }
}
