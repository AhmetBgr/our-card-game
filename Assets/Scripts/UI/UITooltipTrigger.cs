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

    public string Message
    {
        get => message;
        set => message = value;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UITooltip.Instance != null)
            UITooltip.Instance.Show(message, (RectTransform)transform);
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
