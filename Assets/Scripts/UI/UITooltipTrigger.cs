using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shows the shared <see cref="UITooltip"/> with <see cref="message"/> while the pointer is over this
/// element. Sits on the element itself (a button, an icon, ...) so the text lives next to the thing
/// it describes rather than in a lookup table somewhere else.
/// </summary>
public class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(2, 4)]
    [SerializeField] private string message;

    [Tooltip("Optional fixed spot: the tooltip is centered there instead of being placed beside this " +
             "element. For an element spawned at runtime the spot is handed over in code (see " +
             "PassiveSelectionController), since a prefab cannot reference a scene object.")]
    [SerializeField] private Transform point;

    public string Message
    {
        get => message;
        set => message = value;
    }

    /// <summary>Where the tooltip shows. Null = beside this element.</summary>
    public Transform Point
    {
        get => point;
        set => point = value;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UITooltip.Instance == null) return;

        if (point != null) UITooltip.Instance.ShowAt(message, point);
        else UITooltip.Instance.Show(message, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UITooltip.Instance != null)
            UITooltip.Instance.Hide();
    }

    // A disabled object never receives OnPointerExit, so the tooltip would otherwise stay on screen
    // after the element it describes goes away.
    void OnDisable()
    {
        if (UITooltip.Instance != null)
            UITooltip.Instance.Hide();
    }
}
