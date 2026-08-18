using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feeds this card's current local rotation into its frame's CardMetallic material every frame,
/// so the sheen sweep and edge flash react live to the fan tilt (Z) and flip animations (Y) that
/// CardHandLayout/CardView/DraggableItem drive via DOTween. The shader has no other way to see
/// rotation — cards render as UI on a Screen-Space-Overlay canvas, so there is no camera-relative
/// view vector to derive it from in-shader.
/// </summary>
[RequireComponent(typeof(CardView))]
public class CardMetallicRotationDriver : MonoBehaviour
{
    private static readonly int RotationZId = Shader.PropertyToID("_RotationZ");
    private static readonly int RotationYId = Shader.PropertyToID("_RotationY");

    private Image _frame;
    private Material _material;

    private void Awake()
    {
        _frame = GetComponent<CardView>().FrameImage;
        if (_frame == null)
        {
            enabled = false;
            return;
        }

        // Instance the material so each card's sheen is independent and the shared asset is untouched.
        _material = new Material(_frame.material);
        _frame.material = _material;
    }

    private void LateUpdate()
    {
        if (_material == null) return;

        Vector3 euler = transform.localEulerAngles;
        _material.SetFloat(RotationZId, NormalizeAngle(euler.z));
        _material.SetFloat(RotationYId, NormalizeAngle(euler.y));
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f) degrees -= 360f;
        return degrees;
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
