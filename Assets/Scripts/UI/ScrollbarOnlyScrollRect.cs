using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A ScrollRect that cannot be dragged. The scrollbar and the wheel still move it; a press-and-slide
/// on the content does nothing.
///
/// For lists where a drag means something else -- the resolution dropdown, where a press on a row is
/// a pick, and a press that wanders a few pixels before release should still be that pick rather than
/// a scroll that lands on a different row.
/// </summary>
public class ScrollbarOnlyScrollRect : ScrollRect
{
    public override void OnInitializePotentialDrag(PointerEventData eventData) { }

    public override void OnBeginDrag(PointerEventData eventData) { }

    public override void OnDrag(PointerEventData eventData) { }

    public override void OnEndDrag(PointerEventData eventData) { }
}
