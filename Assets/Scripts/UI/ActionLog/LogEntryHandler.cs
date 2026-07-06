using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LogEntryHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Image background;
    [SerializeField] private Color playerTint = new Color(0.12f, 0.28f, 0.5f, 0.4f);
    [SerializeField] private Color opponentTint = new Color(0.5f, 0.15f, 0.15f, 0.4f);

    private ActionLogEntry entry;

    public void Bind(ActionLogEntry entry)
    {
        this.entry = entry;
        messageText.text = entry.Message;
        if (background != null)
        {
            background.color = entry.IsPlayerOwned ? playerTint : opponentTint;
        }
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
