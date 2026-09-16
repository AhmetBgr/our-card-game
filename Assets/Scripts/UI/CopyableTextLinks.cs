using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes the <c>&lt;link="..."&gt;</c> tags in a TextMeshPro text copyable: clicking one puts its link
/// id on the clipboard and briefly swaps the link's text for a confirmation. Used by the credits, where
/// the ids are the full source URLs and the visible text is the short form.
///
/// Only a click copies. A drag is left to whatever ScrollRect the text sits in, which the event system
/// already guarantees: a press that turns into a drag never becomes a click.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class CopyableTextLinks : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Shown in place of a link's text for a moment after it is copied.")]
    [SerializeField] private string copiedMessage = "Copied!";

    [SerializeField] private float copiedMessageDuration = 1.2f;

    private TextMeshProUGUI text;
    private string originalText;
    private Coroutine restore;

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();

        // The text has to catch the click itself; the links are only found inside it.
        text.raycastTarget = true;
    }

    void OnDisable()
    {
        RestoreText();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        Camera eventCamera = text.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventData.pressEventCamera;
        int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventCamera);
        if (index < 0) return;

        TMP_LinkInfo link = text.textInfo.linkInfo[index];
        string id = link.GetLinkID();
        string shown = link.GetLinkText();

        Clipboard.Copy(id);
        ShowCopied(id, shown);
    }

    void ShowCopied(string id, string shown)
    {
        RestoreText();

        originalText = text.text;

        // Swap only the visible part of this one link. Its markup is found by id, so the same short text
        // appearing elsewhere is left alone.
        string open = $"<link=\"{id}\">";
        int start = originalText.IndexOf(open, System.StringComparison.Ordinal);
        if (start < 0) { originalText = null; return; }

        int end = originalText.IndexOf("</link>", start, System.StringComparison.Ordinal);
        if (end < 0) { originalText = null; return; }

        int inner = start + open.Length;
        string markup = originalText.Substring(inner, end - inner);
        string replaced = markup.Replace(shown, copiedMessage);

        text.text = originalText.Substring(0, inner) + replaced + originalText.Substring(end);
        restore = StartCoroutine(RestoreAfterDelay());
    }

    IEnumerator RestoreAfterDelay()
    {
        yield return new WaitForSecondsRealtime(copiedMessageDuration);
        restore = null;
        RestoreText();
    }

    void RestoreText()
    {
        if (restore != null)
        {
            StopCoroutine(restore);
            restore = null;
        }

        if (originalText != null)
        {
            text.text = originalText;
            originalText = null;
        }
    }
}
