using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The stack of keyword-explanation boxes shown beside a hovered card — one entry per keyword
/// mechanic found in the card's description (see <see cref="KeywordDatabase"/>).
///
/// The prefab (Resources/UI/KeywordTooltipPanel) carries one inactive entry template child that is
/// cloned per keyword, ActionLogPanel-style, so the whole tooltip is a single prefab with no
/// cross-prefab references. Placement adapts <see cref="UITooltip"/>'s PlaceBeside: prefer the
/// anchor's right, flip left when the panel would overhang the canvas, plus a vertical clamp
/// because a multi-keyword stack can be taller than the anchor sits from the screen edge.
///
/// Callers never touch this component directly — everything goes through the static
/// <see cref="KeywordTooltip"/> facade, which lazily instantiates the prefab under its own
/// dedicated overlay canvas (see TryCreatePanel for why a dedicated canvas is load-bearing). That
/// is what lets the tooltip work and render identically in any scene (Game, MainMenu, the deck
/// builder) with zero scene wiring — unlike UITooltip, which must be authored into each scene.
/// </summary>
public class KeywordTooltipPanel : MonoBehaviour
{
    [Tooltip("Inactive child cloned once per keyword. Needs TMP children named NameText and ExplanationText.")]
    [SerializeField] private GameObject entryTemplate;

    [Tooltip("Gap in canvas units between the hovered element and the tooltip stack.")]
    [SerializeField] private float gap = 12f;

    [Tooltip("Minimum distance kept between the stack and the canvas edges when clamping.")]
    [SerializeField] private float edgeMargin = 8f;

    [Tooltip("Scale factor of the tooltip's own dedicated canvas. 1 = design size; raise/lower to make every tooltip bigger/smaller globally, in every scene.")]
    [SerializeField] private float referenceScaleFactor = 1f;

    /// <summary>Read by the facade when it builds the dedicated tooltip canvas.</summary>
    public float ReferenceScaleFactor => referenceScaleFactor;

    private readonly List<GameObject> entries = new List<GameObject>();
    private RectTransform rect;

    // Same asset CardView/HeroPassiveIndicator load, so entry text styling matches card text.
    private static CardTextHighlightConfig highlightConfig;
    private static bool highlightConfigSearched;

    private static CardTextHighlightConfig HighlightConfig
    {
        get
        {
            if (!highlightConfigSearched)
            {
                highlightConfig = Resources.Load<CardTextHighlightConfig>("CardTextHighlightConfig");
                highlightConfigSearched = true;
            }
            return highlightConfig;
        }
    }

    public void Show(IReadOnlyList<CardTextHighlightConfig.KeywordDefinition> keywords, RectTransform anchor)
    {
        if (anchor == null || !BuildEntries(keywords))
            return;

        PlaceBeside(anchor);
    }

    /// <summary>
    /// Shows the stack pinned to a fixed point (its top-left corner lands on the point) instead of
    /// being placed beside an anchor. For surfaces with an authored tooltip spot — the game scene's
    /// card preview, the custom-game deck builder.
    /// </summary>
    public void ShowAt(IReadOnlyList<CardTextHighlightConfig.KeywordDefinition> keywords, Transform point)
    {
        if (point == null || !BuildEntries(keywords))
            return;

        PlaceAt(point);
    }

    private bool BuildEntries(IReadOnlyList<CardTextHighlightConfig.KeywordDefinition> keywords)
    {
        if (keywords == null || keywords.Count == 0 || entryTemplate == null)
            return false;

        if (rect == null)
            rect = (RectTransform)transform;

        gameObject.SetActive(true);

        // Clear every clone by walking the hierarchy rather than a tracked list: a mid-play script
        // reload wipes private lists but not the clone GameObjects, and a tracked list would leak
        // them forever after.
        Transform parent = entryTemplate.transform.parent;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (child != entryTemplate)
                Destroy(child);
        }
        entries.Clear();

        foreach (var def in keywords)
        {
            GameObject entry = Instantiate(entryTemplate, parent);
            Bind(entry, def);
            entry.SetActive(true);
            entries.Add(entry);
        }

        // The stack is content-sized, so its width/height are only correct after a rebuild with the
        // new entries — and both decide which side it fits on and how far it must clamp.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        return true;
    }

    public void HideNow()
    {
        gameObject.SetActive(false);
    }

    private static void Bind(GameObject entry, CardTextHighlightConfig.KeywordDefinition def)
    {
        var nameLabel = FindLabel(entry.transform, "NameText");
        if (nameLabel != null)
            nameLabel.text = def.displayName;

        var bodyLabel = FindLabel(entry.transform, "ExplanationText");
        if (bodyLabel != null)
            bodyLabel.text = CardTextFormatter.Format(def.explanation, HighlightConfig);
    }

    private static TextMeshProUGUI FindLabel(Transform entry, string childName)
    {
        Transform child = entry.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    // Prefers the right of the anchor, flipping left when the stack would overhang the canvas; the
    // pivot does the horizontal work (UITooltip.PlaceBeside). Vertically the stack is centered on
    // the anchor, then clamped inside the canvas because a tall stack beside a card at the bottom
    // of the hand would otherwise run off screen.
    private void PlaceBeside(RectTransform anchor)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        var canvasRect = (RectTransform)canvas.rootCanvas.transform;

        // The panel lives on its own dedicated overlay canvas (see the facade), so no scale or
        // pixels-per-unit compensation is needed here: rendering is identical in every scene by
        // construction. The anchor sits on a DIFFERENT canvas, but overlay canvases share
        // screen-pixel world space, so InverseTransformPoint converts its corners correctly.
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);

        Vector2 min = canvasRect.InverseTransformPoint(corners[0]);
        Vector2 max = canvasRect.InverseTransformPoint(corners[2]);

        bool fitsRight = max.x + gap + rect.rect.width <= canvasRect.rect.width * 0.5f;

        // Top-aligned with the anchor (Hearthstone-style: the stack hangs down from the card's top
        // corner), clamped so a tall stack can't run off the bottom or top of the canvas.
        float halfCanvasH = canvasRect.rect.height * 0.5f;
        float clampedY = Mathf.Clamp(max.y,
            -halfCanvasH + rect.rect.height + edgeMargin,
            halfCanvasH - edgeMargin);

        rect.pivot = new Vector2(fitsRight ? 0f : 1f, 1f);
        rect.anchoredPosition = new Vector2(fitsRight ? max.x + gap : min.x - gap, clampedY);
    }

    // Pins the stack's top-left corner to a world-space point (an authored empty RectTransform in
    // the scene), clamped inside the canvas. Overlay canvases share screen-pixel world space, so
    // the point can live on any of them.
    private void PlaceAt(Transform point)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        var canvasRect = (RectTransform)canvas.rootCanvas.transform;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 1f);

        Vector2 p = canvasRect.InverseTransformPoint(point.position);

        float halfW = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;
        p.x = Mathf.Clamp(p.x, -halfW + edgeMargin, halfW - rect.rect.width - edgeMargin);
        p.y = Mathf.Clamp(p.y, -halfH + rect.rect.height + edgeMargin, halfH - edgeMargin);

        rect.anchoredPosition = p;
    }
}

/// <summary>
/// Static entry point for keyword tooltips: <c>KeywordTooltip.Show(desc, anchor, owner)</c> on
/// hover-enter, <c>KeywordTooltip.Hide(owner)</c> on hover-exit. Each hover surface is a two-line
/// hookup and none of them know about the panel, the prefab, or each other.
///
/// The owner token exists because surfaces overlap: hovering from a board minion straight onto a
/// hand card can deliver the minion's exit AFTER the card's enter, and an unconditional hide would
/// kill the tooltip the card just opened. Hide only acts when the caller is the current owner.
/// </summary>
public static class KeywordTooltip
{
    private const string PrefabPath = "UI/KeywordTooltipPanel";

    private static KeywordTooltipPanel panel;
    private static object currentOwner;
    private static bool prefabWarned;

    /// <param name="positionPoint">Optional authored spot for the stack (its top-left corner lands
    /// there). Null = the default placement beside the anchor card.</param>
    public static void Show(string desc, RectTransform anchor, object owner, Transform positionPoint = null)
    {
        if (anchor == null)
            return;

        var keywords = KeywordDatabase.GetKeywords(desc);
        if (keywords.Count == 0)
        {
            // A card with no keywords must still replace the previous card's tooltip when the
            // pointer slides directly between them (exit events can arrive after this enter).
            Hide(currentOwner);
            return;
        }

        if (panel == null && !TryCreatePanel())
            return;

        currentOwner = owner;

        if (positionPoint != null)
        {
            panel.ShowAt(keywords, positionPoint);
            return;
        }

        // Callers pass the card's root, but the root rect is a layout slot bigger than the drawn
        // card — align to the visible frame instead so the stack sits flush with what the player
        // sees. Surfaces without a CardView (synthetic anchors) use the rect they passed.
        var cardView = anchor.GetComponent<CardView>();
        if (cardView != null)
            anchor = cardView.VisualRect;

        panel.Show(keywords, anchor);
    }

    public static void Hide(object owner)
    {
        if (owner == null || !ReferenceEquals(owner, currentOwner))
            return;

        HideAll();
    }

    public static void HideAll()
    {
        currentOwner = null;
        if (panel != null)
            panel.HideNow();
    }

    // The panel gets its own overlay canvas with fixed settings (scale factor from the prefab,
    // default reference pixels-per-unit) instead of being reparented into whichever canvas the
    // hovered card lives in. Scene canvases disagree on scaleFactor (UICanvas 0.75 vs MainMenu 1)
    // AND referencePixelsPerUnit (32 vs 100) — the latter changes 9-sliced border geometry, which
    // no uniform scale can compensate. A dedicated canvas makes rendering identical everywhere by
    // construction. Destroyed with the scene; recreated on the next Show.
    private static bool TryCreatePanel()
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            if (!prefabWarned)
            {
                Debug.LogWarning($"KeywordTooltip: no prefab at Resources/{PrefabPath}; keyword tooltips disabled.");
                prefabWarned = true;
            }
            return false;
        }

        var canvasGO = new GameObject("KeywordTooltipCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the scene's overlay canvases (all at 0); the tooltip must draw over the cards it
        // annotates. No GraphicRaycaster, so it can never intercept pointer events.
        canvas.sortingOrder = 1;

        panel = Object.Instantiate(prefab, canvasGO.transform).GetComponent<KeywordTooltipPanel>();
        if (panel == null)
        {
            if (!prefabWarned)
            {
                Debug.LogWarning("KeywordTooltip: prefab has no KeywordTooltipPanel component; keyword tooltips disabled.");
                prefabWarned = true;
            }
            Object.Destroy(canvasGO);
            return false;
        }

        var scaler = canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = panel.ReferenceScaleFactor;

        panel.gameObject.SetActive(false);
        return true;
    }

    // Self-installing, so no scene needs to know tooltips exist (CardSelectionPanel precedent).
    // Remove-then-add keeps it idempotent across disabled domain reloads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        DraggableItem.DragStarted -= OnDragStarted;
        DraggableItem.DragStarted += OnDragStarted;
    }

    // A dragged card must not keep its tooltip pinned to the hand position it left.
    private static void OnDragStarted(Transform dragged) => HideAll();
}
