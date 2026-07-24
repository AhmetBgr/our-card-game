using UnityEngine;
using UnityEngine.EventSystems;

// Sits on the end-game panel root, which is a full-screen raycast target, so a click anywhere on the
// panel fast-forwards the stats reveal to its finished state. Clicks land here by bubbling up from
// whichever graphic was hit; Buttons handle their own clicks and stop the bubble, so Replay and Exit
// keep working normally instead of being swallowed by the skip.
public class MatchStatsSkipOnClick : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Optional. Left empty, the view is found in this object's children when the panel opens.")]
    [SerializeField] private MatchStatsView statsView;

    private void Awake()
    {
        if (statsView == null) statsView = GetComponentInChildren<MatchStatsView>(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (statsView != null) statsView.SkipReveal();
    }
}
