using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class LogEntryHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI messageText;

    private ActionLogEntry entry;

    public void Bind(ActionLogEntry entry)
    {
        this.entry = entry;
        messageText.text = entry.Message;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (entry?.PreviewCard == null) return;
        GameManager.Instance.player.handManager.ShowInfoCard(entry.PreviewCard);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GameManager.Instance.player.handManager.HideInfoCard();
    }
}
