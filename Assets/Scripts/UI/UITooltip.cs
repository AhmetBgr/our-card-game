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

    private RectTransform rect;
    private RectTransform canvasRect;

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

        // The panel is content-sized, so its width is only correct after the layout has been rebuilt
        // with the new text — and the width is what decides which side of the target it fits on.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        PlaceBeside(target);
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
