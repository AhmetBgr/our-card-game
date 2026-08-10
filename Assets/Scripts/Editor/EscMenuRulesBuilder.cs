using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the Esc menu's RulesPanel and HowToPlayPanel from <c>Docs/rules-panel.md</c> and
/// <c>Docs/how-to-play-panel.md</c>, so the written copy and the in-game panels can never drift apart:
/// edit the markdown, run the matching Tools/Esc Menu/Sync ▸ menu item, done.
///
/// A doc with no <c>## Heading</c> lines is treated as one flat, unlabelled section — every <c>-
/// bullet</c> just becomes a line of the panel's body. A doc that DOES use <c>## Heading</c> lines
/// still gets one section per heading, each with its own bold label. Every section also gets a hidden
/// Screenshot slot (only reattached on resync for sections that have a heading to key off) — drop a
/// sprite in and tick the object active, and the layout group reflows around it.
/// </summary>
public static class EscMenuRulesBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/EscMenu.prefab";
    private const string RulesDocPath = "Docs/rules-panel.md";
    private const string HowToPlayDocPath = "Docs/how-to-play-panel.md";

    private const string TitleFontPath = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF Typface.asset";
    private const string BodyFontPath  = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF.asset";

    // Fallbacks only, for a RulesPanel that has lost its Frame. Everything is normally measured off the
    // Frame itself (see MeasureFrame), so resizing or moving the frame art in the editor and re-syncing
    // is all it takes to re-fit the rules to it.
    private const float FallbackCentreX = 31.4813f;
    private const float FallbackWidth = 786.8f;
    private const float FallbackHeight = 907f;

    // Breathing room between the frame art and the text inside it.
    private const float SidePadding = 44f;
    private const float TopPadding = 24f;
    private const float BottomPadding = 28f;
    private const float HeaderHeight = 70f;

    // The scrollbar's visible width, and how far left of that its CLICKABLE area reaches (see
    // BuildScrollbar) so a bar this thin is still easy to grab.
    private const float ScrollbarWidth = 18f;
    private const float ScrollbarGrabExtra = 12f;

    private static readonly Color TitleColor     = new Color(0.851f, 0.851f, 0.851f, 1f);
    private static readonly Color HeadColor      = new Color(1f, 0.886f, 0.639f, 1f);
    private static readonly Color BodyColor      = new Color(0.82f, 0.82f, 0.82f, 1f);
    private static readonly Color HighlightColor = new Color(1f, 0.788f, 0.353f, 1f);
    private static readonly string HighlightColorHex = "#" + ColorUtility.ToHtmlStringRGBA(HighlightColor);

    private class Section
    {
        public string Heading;
        public readonly List<string> Lines = new List<string>();
    }

    [MenuItem("Tools/Esc Menu/Sync Rules Panel")]
    public static void Sync() => SyncPanel("RulesPanel", RulesDocPath, "Rules", repairLegacyLayout: true);

    [MenuItem("Tools/Esc Menu/Sync How To Play Panel")]
    public static void SyncHowToPlay() => SyncPanel("HowToPlayPanel", HowToPlayDocPath, titleText: null, repairLegacyLayout: false);

    /// <summary>
    /// Rebuilds one panel's scrollable body from its markdown doc. Shared by RulesPanel and
    /// HowToPlayPanel — they differ only in which GameObject and doc they point at, and RulesPanel
    /// alone needs the one-time layout repair below. A null/empty <paramref name="titleText"/> skips the
    /// title and divider entirely and lets the scroll area use that extra vertical space instead.
    /// </summary>
    private static void SyncPanel(string panelName, string docPath, string titleText, bool repairLegacyLayout)
    {
        string docFullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", docPath);
        if (!File.Exists(docFullPath))
        {
            Debug.LogError($"[{panelName}] {docPath} not found — nothing to sync from.");
            return;
        }

        List<Section> sections = Parse(File.ReadAllLines(docFullPath));
        if (sections.Count == 0)
        {
            Debug.LogError($"[{panelName}] {docPath} has no bullets to build a panel from.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform panel = FindChild(root.transform, "Panel") ?? FindChild(root.transform, "TitlePanel");
            if (panel == null) { Debug.LogError($"[{panelName}] no Panel/TitlePanel under EscMenu"); return; }

            Transform target = FindChild(root.transform, panelName) ?? FindChild(panel, panelName);
            if (target == null) { Debug.LogError($"[{panelName}] no {panelName} under EscMenu"); return; }

            if (repairLegacyLayout)
            {
                // EscMenuController shows/hides `panel` and nothing else, so everything belonging to the
                // pause menu has to live under it. RulesPanel and ButtonsPanel were once siblings of it,
                // which left them on screen mid-match.
                Transform buttons = FindChild(root.transform, "ButtonsPanel") ?? FindChild(panel, "ButtonsPanel");
                panel.gameObject.name = "Panel";
                if (buttons != null && buttons.parent != panel) buttons.SetParent(panel, false);
                if (target.parent != panel) target.SetParent(panel, false);
                if (buttons != null) buttons.SetSiblingIndex(1);
                target.SetSiblingIndex(2);

                // Backdrop stays exactly as authored — it already covers the screen at 0.949 alpha.
                var panelRt = (RectTransform)panel;
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
            }

            // Measured BEFORE the old content is cleared, while the Frame is still the authored one.
            Rect area = MeasureFrame(target);
            Dictionary<string, Sprite> keptShots = CacheScreenshots(target);

            // Rebuildable: drop everything the last sync made, keep the authored Frame.
            for (int i = target.childCount - 1; i >= 0; i--)
            {
                Transform child = target.GetChild(i);
                if (child.name != "Frame") Object.DestroyImmediate(child.gameObject);
            }

            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            TMP_FontAsset bodyFont  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

            float centreX = area.center.x;
            float width = area.width - SidePadding * 2f;
            float top = area.yMax;
            float scrollBottom = area.yMin + BottomPadding;
            float scrollTop;

            if (!string.IsNullOrEmpty(titleText))
            {
                float titleY = top - TopPadding - HeaderHeight * 0.5f;
                float dividerY = top - TopPadding - HeaderHeight - 10f;
                scrollTop = dividerY - 14f;

                TextMeshProUGUI title = MakeText(target, "RulesTitle", titleFont, 46f, TitleColor,
                    TextAlignmentOptions.Center);
                title.text = titleText;
                title.fontStyle = FontStyles.Bold;
                SetRect(title.rectTransform, centreX, titleY, width, HeaderHeight);

                Image divider = MakeImage(target, "Divider", new Color(1f, 1f, 1f, 0.16f));
                SetRect(divider.rectTransform, centreX, dividerY, width, 4f);
            }
            else
            {
                scrollTop = top - TopPadding;
            }

            GameObject scrollGo = NewUI("RulesScroll", target);
            var scrollRt = (RectTransform)scrollGo.transform;
            SetRect(scrollRt, centreX, (scrollTop + scrollBottom) * 0.5f, width, scrollTop - scrollBottom);
            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();

            GameObject viewportGo = NewUI("Viewport", scrollRt);
            var viewportRt = (RectTransform)viewportGo.transform;
            Stretch(viewportRt);
            // The viewport stops exactly where the scrollbar begins. Any gap between the two would be a
            // dead strip belonging to neither, and a press landing in it hits nothing at all.
            viewportRt.offsetMax = new Vector2(-ScrollbarWidth, 0f);
            viewportGo.AddComponent<RectMask2D>();
            // Invisible but raycastTarget=true: without a Graphic here, dragging over the body text
            // (raycastTarget=false) hits nothing, so ScrollRect's own drag handlers never fire and only
            // the scrollbar handle is draggable.
            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = Color.clear;

            GameObject contentGo = NewUI("Content", viewportRt);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(14, 14, 6, 30);
            contentLayout.spacing = 26f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childAlignment = TextAnchor.UpperLeft;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < sections.Count; i++)
                BuildSection(contentRt, i, sections[i], titleFont, bodyFont, keptShots);

            Scrollbar bar = BuildScrollbar(scrollRt);

            scroll.content = contentRt;
            scroll.viewport = viewportRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[{panelName}] synced {sections.Count} section(s) from {docPath} into {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// '## Heading' opens a section, '- bullet' adds a body line to it, and an indented line continues
    /// the bullet above (markdown wraps mid-sentence; the panel must not). A bullet reached before any
    /// heading opens an implicit, unlabelled section instead of being dropped — that's what lets a doc
    /// skip headings entirely and just be a flat list. Everything else — the H1, the intro paragraph,
    /// the '---' rules — is ignored.
    /// </summary>
    private static List<Section> Parse(string[] lines)
    {
        var sections = new List<Section>();
        Section current = null;
        var bullet = new StringBuilder();

        void FlushBullet()
        {
            if (bullet.Length == 0) return;
            if (current == null)
            {
                current = new Section { Heading = string.Empty };
                sections.Add(current);
            }
            current.Lines.Add(Inline(bullet.ToString()));
            bullet.Clear();
        }

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();

            if (line.StartsWith("## "))
            {
                FlushBullet();
                current = new Section { Heading = line.Substring(3).Trim() };
                sections.Add(current);
                continue;
            }

            if (line.StartsWith("# ") || line.StartsWith("---") || line.Length == 0)
            {
                FlushBullet();
                continue;
            }

            if (line.StartsWith("- "))
            {
                FlushBullet();
                bullet.Append(line.Substring(2).Trim());
                continue;
            }

            // Continuation of the bullet above (markdown soft wrap), or stray prose before any heading.
            if (bullet.Length > 0) bullet.Append(' ').Append(line.Trim());
        }

        FlushBullet();
        sections.RemoveAll(s => s.Lines.Count == 0);
        return sections;
    }

    /// <summary>
    /// Markdown emphasis to TMP rich text. <c>==word==</c> is the odd one out — not real Markdown, but
    /// GitHub/Obsidian both treat it as "highlight", and it reads better in a bullet than bold does, so
    /// it's what marks the one or two important words in a sentence. Everything else is already plain.
    /// </summary>
    private static string Inline(string text)
    {
        text = Regex.Replace(text, @"==(.+?)==", $"<color={HighlightColorHex}>$1</color>");
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
        text = Regex.Replace(text, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<i>$1</i>");
        text = text.Replace("`", string.Empty);
        return text;
    }

    /// <summary>
    /// The area the rules have to live inside, in RulesPanel-local units: the Frame's own rect, scaled by
    /// the Frame's localScale (the art is authored small and scaled up 8x). Measuring rather than hard-coding
    /// is what lets the frame be resized in the editor without this tool putting the text back in the wrong place.
    /// </summary>
    private static Rect MeasureFrame(Transform rules)
    {
        Transform frame = rules.Find("Frame");
        if (frame == null)
        {
            Debug.LogWarning("[RulesPanel] no Frame under RulesPanel; falling back to the authored size.");
            return new Rect(FallbackCentreX - FallbackWidth * 0.5f, -FallbackHeight * 0.5f,
                FallbackWidth, FallbackHeight);
        }

        var rt = (RectTransform)frame;
        float w = rt.rect.width * rt.localScale.x;
        float h = rt.rect.height * rt.localScale.y;
        Vector2 centre = rt.anchoredPosition;
        return new Rect(centre.x - w * 0.5f, centre.y - h * 0.5f, w, h);
    }

    /// <summary>
    /// Sprites already dropped into the per-section screenshot slots, keyed by the heading they sit under,
    /// so a resync re-attaches them instead of throwing them away.
    /// </summary>
    private static Dictionary<string, Sprite> CacheScreenshots(Transform rules)
    {
        var cache = new Dictionary<string, Sprite>();
        Transform content = rules.Find("RulesScroll/Viewport/Content");
        if (content == null) return cache;

        for (int i = 0; i < content.childCount; i++)
        {
            Transform section = content.GetChild(i);
            Transform heading = section.Find("Heading");
            Transform shot = section.Find("Screenshot");
            if (heading == null || shot == null) continue;

            var label = heading.GetComponent<TextMeshProUGUI>();
            var image = shot.GetComponent<Image>();
            if (label == null || image == null || image.sprite == null) continue;

            cache[label.text] = image.sprite;
        }
        return cache;
    }

    private static void BuildSection(RectTransform parent, int index, Section section,
        TMP_FontAsset headFont, TMP_FontAsset bodyFont, Dictionary<string, Sprite> keptShots)
    {
        string safe = Regex.Replace(section.Heading, @"[^A-Za-z0-9]", string.Empty);
        if (safe.Length == 0) safe = "Body";
        GameObject sectionGo = NewUI($"Section_{index + 1:00}_{safe}", parent);

        var layout = sectionGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        // A blank heading means the doc never used '## Heading' at all (a flat bullet list) — the panel's
        // own title already labels the content, so skip the redundant bold line instead of rendering it empty.
        if (!string.IsNullOrEmpty(section.Heading))
        {
            TextMeshProUGUI head = MakeText(sectionGo.transform, "Heading", headFont, 27f, HeadColor,
                TextAlignmentOptions.TopLeft);
            head.text = section.Heading;
            head.fontStyle = FontStyles.Bold;
        }

        TextMeshProUGUI body = MakeText(sectionGo.transform, "Body", bodyFont, 21f, BodyColor,
            TextAlignmentOptions.TopLeft);
        body.text = string.Join("\n", section.Lines.Select(l => $"•  {l}"));
        body.lineSpacing = 8f;
        body.paragraphSpacing = 6f;
        body.margin = new Vector4(6f, 0f, 0f, 0f);

        GameObject shotGo = NewUI("Screenshot", sectionGo.transform);
        Image shot = shotGo.AddComponent<Image>();
        shot.preserveAspect = true;
        var element = shotGo.AddComponent<LayoutElement>();
        element.preferredHeight = 280f;
        element.flexibleWidth = 1f;

        // A screenshot survives a resync; a section that never had one stays hidden until you add it.
        if (keptShots.TryGetValue(section.Heading, out Sprite kept) && kept != null)
        {
            shot.sprite = kept;
            shotGo.SetActive(true);
        }
        else
        {
            shotGo.SetActive(false);
        }
    }

    private static Scrollbar BuildScrollbar(RectTransform parent)
    {
        GameObject barGo = NewUI("Scrollbar Vertical", parent);
        var barRt = (RectTransform)barGo.transform;
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(ScrollbarWidth, 0f);
        barRt.anchoredPosition = Vector2.zero;

        // NEGATIVE padding GROWS the clickable rect: Graphic.raycastPadding is applied as
        // `xMin += x; yMin += y; xMax -= z; yMax -= w`, so positive values SHRINK it. This reaches the
        // grab area left into the viewport without changing how the bar looks; the scrollbar is a later
        // sibling than the viewport, so it wins that overlap.
        var raycastPadding = new Vector4(-ScrollbarGrabExtra, 0f, 0f, 0f);

        Image track = barGo.AddComponent<Image>();
        track.color = new Color(0f, 0f, 0f, 0.35f);
        track.raycastPadding = raycastPadding;

        GameObject areaGo = NewUI("Sliding Area", barRt);
        var areaRt = (RectTransform)areaGo.transform;
        Stretch(areaRt);
        areaRt.offsetMin = new Vector2(2f, 2f);
        areaRt.offsetMax = new Vector2(-2f, -2f);

        GameObject handleGo = NewUI("Handle", areaRt);
        var handleRt = (RectTransform)handleGo.transform;
        handleRt.sizeDelta = Vector2.zero;
        Image handle = handleGo.AddComponent<Image>();
        handle.color = new Color(1f, 1f, 1f, 0.55f);
        handle.raycastPadding = raycastPadding;

        Scrollbar bar = barGo.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handleRt;
        bar.targetGraphic = handle;
        return bar;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font,
        float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        GameObject go = NewUI(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
