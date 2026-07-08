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

    [Header("Turn-end spacer")]
    [SerializeField] private Color spacerColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private float spacerHeight = 6f;

    private ActionLogEntry entry;
    private LayoutElement layoutElement;

    public bool IsSpacer => entry != null && entry.IsSpacer;

    public void Bind(ActionLogEntry entry)
    {
        this.entry = entry;

        if (entry.IsSpacer)
        {
            BindSpacer();
            return;
        }

        messageText.text = entry.Message;
        if (background != null)
        {
            background.color = entry.IsPlayerOwned ? playerTint : opponentTint;
        }
    }

    // A spacer is a thin, non-interactive divider row: no text, a faint bar, and a reduced height.
    private void BindSpacer()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
        if (background != null)
        {
            background.color = spacerColor;
            background.raycastTarget = false;
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }
        if (layoutElement != null)
        {
            layoutElement.minHeight = spacerHeight;
            layoutElement.preferredHeight = spacerHeight;
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
