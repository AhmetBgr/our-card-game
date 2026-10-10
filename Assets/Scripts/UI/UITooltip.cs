using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single shared hover tooltip for a screen-space canvas. One instance lives under the canvas and
/// is moved next to whatever <see cref="UITooltipTrigger"/> is being hovered, so every tooltip in the
/// scene shares one panel instead of each element carrying its own.
///
/// Named UITooltip rather than Tooltip because a global <c>Tooltip</c> type would shadow
/// <see cref="TooltipAttribute"/> and break every <c>[Tooltip("...")]</c> in the project.
/// </summary>
public class UITooltip : MonoBehaviour
{
    private static UITooltip instance;

    /// <summary>
    /// The tooltip panel for the current scene. Resolved lazily and including inactive objects,
    /// because the panel spends nearly all of its life hidden — waiting for Awake would mean a
    /// tooltip that never shows.
    /// </summary>
    public static UITooltip Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<UITooltip>(FindObjectsInactive.Include);

            return instance;
        }
    }

    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("Gap in canvas units between the hovered element and the tooltip.")]
    [SerializeField] private float gap = 12f;

    [Tooltip("Panel art behind the text. Empty = the graphic on this object. Hidden while the tooltip " +
             "sits on an authored spot (ShowAt), where it would box in a surface that is already a panel.")]
    [SerializeField] private Graphic background;

    private RectTransform rect;
    private RectTransform canvasRect;
    private bool backgroundResolved;

    void Awake()
    {
        instance = this;

        // Authored visible so the panel can be laid out in the editor; hidden before the first frame.
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>Shows <paramref name="text"/> beside <paramref name="target"/>.</summary>
    public void Show(string text, RectTransform target)
    {
        if (string.IsNullOrEmpty(text) || target == null)
            return;

        EnsureInitialized();

        gameObject.SetActive(true);

        if (label != null)
            label.text = text;

        ShowBackground(true);

        // The panel is content-sized, so its width is only correct after the layout has been rebuilt
        // with the new text — and the width is what decides which side of the target it fits on.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        PlaceBeside(target);
    }

    /// <summary>
    /// Shows <paramref name="text"/> centered on <paramref name="point"/> — an authored spot in the
    /// scene — instead of hanging it off the hovered element. For surfaces that already have a place
    /// where hover detail belongs: the passive picker puts its text on the deck panel's card-preview
    /// area, so hovering a passive reads in the same spot as hovering a card.
    /// </summary>
    public void ShowAt(string text, Transform point)
    {
        if (string.IsNullOrEmpty(text) || point == null)
            return;

        EnsureInitialized();

        gameObject.SetActive(true);

        if (label != null)
            label.text = text;

        // Bare text on an authored spot: the spot is chosen because it is already a place the eye
        // goes (the card-preview area), so the panel art would only box it in.
        ShowBackground(false);

        // Content-sized like Show(): the panel's own size decides how far it has to clamp, so it is
        // only correct after a rebuild with the new text.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        PlaceAt(point);
    }

    /// <summary>Shows <paramref name="text"/> centred under <paramref name="target"/>, with the panel art.</summary>
    public void ShowBelow(string text, RectTransform target)
    {
        if (string.IsNullOrEmpty(text) || target == null)
            return;

        EnsureInitialized();

        gameObject.SetActive(true);

        if (label != null)
            label.text = text;

        ShowBackground(true);

        // Content-sized like Show(): the width decides how far it has to clamp.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        PlaceBelow(target);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // Show() can be the first thing that ever touches this component (the panel starts hidden, so
    // Awake may not have run yet), which is why the cached transforms are resolved here.
    void EnsureInitialized()
    {
        if (rect == null)
            rect = (RectTransform)transform;

        if (canvasRect == null)
        {
            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null)
                canvasRect = (RectTransform)canvas.rootCanvas.transform;
        }
    }

    // Resolved lazily (and only once) rather than in Awake, for the same reason the transforms are:
    // Show can be the first thing that ever touches this component. An authored reference wins; with
    // none, the graphic on this object is the panel art.
    void ShowBackground(bool visible)
    {
        if (!backgroundResolved)
        {
            backgroundResolved = true;
            if (background == null) background = GetComponent<Graphic>();
        }

        if (background != null) background.enabled = visible;
    }

    // Centers the panel on an authored point and clamps it inside the canvas, so a long description
    // at the edge of the preview area cannot run off screen.
    void PlaceAt(Transform point)
    {
        if (canvasRect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 p = canvasRect.InverseTransformPoint(point.position);

        float halfW = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;
        p.x = Mathf.Clamp(p.x, -halfW + rect.rect.width * 0.5f, halfW - rect.rect.width * 0.5f);
        p.y = Mathf.Clamp(p.y, -halfH + rect.rect.height * 0.5f, halfH - rect.rect.height * 0.5f);

        rect.anchoredPosition = p;
    }

    // Hangs the panel from the target's bottom edge, centred on it and kept inside the canvas sideways.
    void PlaceBelow(RectTransform target)
    {
        if (canvasRect == null)
            return;

        var corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector2 min = canvasRect.InverseTransformPoint(corners[0]);
        Vector2 max = canvasRect.InverseTransformPoint(corners[2]);

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);

        float halfW = canvasRect.rect.width * 0.5f;
        float x = Mathf.Clamp((min.x + max.x) * 0.5f, -halfW + rect.rect.width * 0.5f, halfW - rect.rect.width * 0.5f);
        rect.anchoredPosition = new Vector2(x, min.y - gap);
    }

    // Prefers the right of the target, flipping to the left when the panel would overhang the canvas.
    // The pivot does the work, so the anchored position is always the target's edge plus the gap.
    void PlaceBeside(RectTransform target)
    {
        if (canvasRect == null)
            return;

        var corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector2 min = canvasRect.InverseTransformPoint(corners[0]);
        Vector2 max = canvasRect.InverseTransformPoint(corners[2]);
        float centerY = (min.y + max.y) * 0.5f;

        bool fitsRight = max.x + gap + rect.rect.width <= canvasRect.rect.width * 0.5f;

        rect.pivot = new Vector2(fitsRight ? 0f : 1f, 0.5f);
        rect.anchoredPosition = new Vector2(fitsRight ? max.x + gap : min.x - gap, centerY);
    }
}
