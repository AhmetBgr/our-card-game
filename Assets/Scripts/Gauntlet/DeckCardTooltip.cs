using UnityEngine;

/// <summary>
/// Shows the card of a hovered deck list entry as a tooltip beside the list: the Draft scene's deck and
/// the copy shown next to the end-of-match reward picks. Sits on the deck panel; its
/// <see cref="CardButtonHandler"/> entries find it above them when there is no
/// <see cref="DeckPanelController"/> to preview through.
///
/// The preview is one display-only card (CardPreview Variant), built on first hover under this panel so
/// it shares the panel's scale, with a canvas of its own sorted above the root canvas so it draws over
/// everything. That canvas has no raycaster, so the preview never takes the hover from the entry.
/// </summary>
public class DeckCardTooltip : MonoBehaviour
{
    [Tooltip("Display-only card prefab drawn as the tooltip (CardPreview Variant).")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float cardScale = 0.8f;
    [Tooltip("Space between the list entry and the card, in this panel's units.")]
    [SerializeField] private float gap = 12f;

    private RectTransform _preview;
    private CardModal _modal;
    private CardView _view;
    private Transform _keywordPoint;

    public void Show(CardSO card, RectTransform entry)
    {
        if (card == null || entry == null || !EnsurePreview()) return;

        _modal.UpdateModal(card, null, true);
        _view.UpdateView(_modal);
        _preview.gameObject.SetActive(true);
        Place(entry);

        // Hung under the card: beside it, the keywords would land on the list the pointer is reading.
        _keywordPoint.position = KeywordPointFor(_preview);
        KeywordTooltip.Show(card.desc, _preview, this, _keywordPoint);
    }

    /// <summary>The bottom-left corner of the card's visible frame, a little below it.</summary>
    private Vector3 KeywordPointFor(RectTransform preview)
    {
        var visual = _view.VisualRect != null ? _view.VisualRect : preview;
        var corners = new Vector3[4];
        visual.GetWorldCorners(corners);
        return corners[0] + (corners[0] - corners[1]).normalized * (gap * transform.lossyScale.y);
    }

    public void Hide()
    {
        if (_preview != null) _preview.gameObject.SetActive(false);
        KeywordTooltip.Hide(this);
    }

    private void OnDisable() => Hide();

    /// <summary>
    /// Beside the entry, on the side facing the middle of the screen, level with it but kept inside the
    /// root canvas.
    /// </summary>
    private void Place(RectTransform entry)
    {
        var self = (RectTransform)transform;
        var corners = new Vector3[4];

        entry.GetWorldCorners(corners);
        Vector3 entryMin = self.InverseTransformPoint(corners[0]);
        Vector3 entryMax = self.InverseTransformPoint(corners[2]);

        var root = GetComponentInParent<Canvas>().rootCanvas;
        ((RectTransform)root.transform).GetWorldCorners(corners);
        Vector3 canvasMin = self.InverseTransformPoint(corners[0]);
        Vector3 canvasMax = self.InverseTransformPoint(corners[2]);

        Vector2 size = Vector2.Scale(_preview.rect.size, _preview.localScale);
        bool toLeft = (entryMin.x + entryMax.x) * 0.5f > (canvasMin.x + canvasMax.x) * 0.5f;

        float x = toLeft ? entryMin.x - gap - size.x * 0.5f : entryMax.x + gap + size.x * 0.5f;
        float y = (entryMin.y + entryMax.y) * 0.5f;
        y = Mathf.Clamp(y, canvasMin.y + size.y * 0.5f, Mathf.Max(canvasMin.y + size.y * 0.5f, canvasMax.y - size.y * 0.5f));

        _preview.localPosition = new Vector3(x, y, 0f);
    }

    private bool EnsurePreview()
    {
        if (_preview != null) return true;
        if (cardPrefab == null) return false;

        var go = Instantiate(cardPrefab, transform, false);
        go.name = "CardTooltip";
        _preview = (RectTransform)go.transform;
        _preview.anchorMin = _preview.anchorMax = _preview.pivot = new Vector2(0.5f, 0.5f);
        _preview.localScale = Vector3.one * cardScale;
        _modal = go.GetComponent<CardModal>();
        _view = go.GetComponent<CardView>();
        if (_modal == null || _view == null)
        {
            Debug.LogWarning("DeckCardTooltip: the card prefab needs a CardModal and a CardView.");
            Destroy(go);
            _preview = null;
            return false;
        }

        var root = GetComponentInParent<Canvas>().rootCanvas;
        var canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerID = root.sortingLayerID;
        canvas.sortingOrder = root.sortingOrder + 100;

        var group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        _keywordPoint = new GameObject("KeywordTooltipPoint", typeof(RectTransform)).transform;
        _keywordPoint.SetParent(transform, false);
        return true;
    }
}
