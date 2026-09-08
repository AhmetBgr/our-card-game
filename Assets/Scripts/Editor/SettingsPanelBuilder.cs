using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds <c>Assets/Prefabs/UI/SettingsPanel.prefab</c> and drops an instance of it into the two places
/// the settings are reachable from -- the pause menu prefab and the title screen -- wiring both owners up
/// to it.
///
/// ONE prefab, instanced twice, rather than two built copies. It is the same panel in both places, so it
/// should be the same asset: a row added to it appears in the pause menu and on the title screen at once,
/// and an artist retouching the frame has no second copy to be told about. What genuinely differs per host
/// is left as an instance override -- where the window sits, and whether the backdrop is on.
///
/// Same arrangement as <see cref="EscMenuRulesBuilder"/>, and for the same reason: a panel of a dozen rows
/// laid out by hand is a panel that drifts. <b>Tools ▸ Settings ▸ Install Settings Panels</b> scaffolds it
/// all, and is safe to re-run -- it only fills in what is missing.
///
/// Once the prefab exists, THE PREFAB IS THE PANEL: edit it in the editor like any other asset. This tool
/// does not touch it again unless you ask for <b>Rebuild Settings Panel Prefab (discards hand-edits)</b>,
/// which builds it from scratch and is named for what that costs. The same goes for what it places in the
/// hosts: an instance or a Settings button that is already there is left alone, however it has been moved
/// or restyled since.
/// </summary>
public static class SettingsPanelBuilder
{
    /// <summary>The panel itself. Built by this tool, instanced by both hosts.</summary>
    private const string PrefabPath = "Assets/Prefabs/UI/SettingsPanel.prefab";

    private const string EscMenuPrefabPath = "Assets/Prefabs/UI/EscMenu.prefab";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    private const string ButtonWidePath = "Assets/Prefabs/UI/Button_Wide.prefab";

    /// <summary>
    /// The switch every toggle row uses, instanced rather than copied so that restyling it restyles every
    /// row at once. It is its own asset rather than the generic Button_Toggle because this panel's
    /// switches carry their state in the ART -- the caption is turned off -- and that is a different
    /// control, not a differently-skinned one.
    /// </summary>
    private const string ButtonTogglePath = "Assets/Prefabs/UI/Button_SettingsToggle.prefab";

    /// <summary>
    /// The bar every volume row uses, on the same terms as the toggle: one asset, four instances, so the
    /// track, fill and handle are styled once. All a row overrides is which bus it moves and which label
    /// it writes the percentage into.
    /// </summary>
    private const string SliderPath = "Assets/Prefabs/UI/Slider_Settings.prefab";

    private const string PanelSpriteSheetPath = "Assets/Sprites/UIElements/panel_01.png";
    private const string FrameSpriteName = "panel_01_12";

    private const string TitleFontPath = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF Typface.asset";
    private const string BodyFontPath = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF.asset";

    /// <summary>Name of the object the tool owns in each host. Anything else called this is replaced.</summary>
    private const string PanelName = "SettingsPanel";

    /// <summary>
    /// Pixels per unit the panel's art is read at, matching the 32 the sprite sheet is imported with.
    ///
    /// This is the number that decides how thick a SLICED sprite's borders come out: a border is
    /// spriteBorderPixels * canvasReferencePixelsPerUnit / (spritePixelsPerUnit * multiplier). The two
    /// canvases the panel is instanced under disagree about it -- the match's is 32, the title
    /// screen's 100 -- which made every frame edge three times thicker on one of them, and pushed the
    /// switch sprites past the point where their borders fit inside the button at all, so they drew as
    /// hollow rings. Pinning it on the panel's own canvas settles the argument in the panel's favour.
    ///
    /// The tool only puts the Canvas there; the value is written at runtime by
    /// <see cref="SettingsPanelController"/>, because Canvas does not serialise it.
    /// </summary>
    private const float PanelReferencePixelsPerUnit = 32f;

    private const string SettingsButtonName = "SettingsButton";

    /// <summary>The Display heading and its rows, as one object so the web build can hide them together.</summary>
    private const string DisplaySectionName = "DisplaySection";

    private const string ResolutionRowName = "ResolutionRow";

    private const string FullscreenRowName = "FullscreenRow";

    private const string SaveButtonName = "Button_Save";

    /// <summary>The "you have unsaved changes" notice beside the Save button. Off until there are.</summary>
    private const string UnsavedLabelName = "UnsavedLabel";

    /// <summary>Sprites the resolution dropdown is dressed in, from the same sheet as the frame.</summary>
    private const string DropdownBarSpriteName = "panel_01_14";
    private const string DropdownArrowSpriteName = "panel_01_16";
    private const string DropdownTickSpriteName = "panel_01_35";

    private const float DropdownHeight = 44f;
    private const float DropdownItemHeight = 44f;
    private const int DropdownVisibleItems = 5;

    // The panel's own geometry, in its Window's local units. The window is a fixed size rather than a
    // fitted one: every row is the same shape, so there is nothing for a layout group to work out, and a
    // fixed frame is the one that can be positioned to sit inside the art in each host.
    private const float WindowWidth = 880f;
    private const float WindowHeight = 1056f;

    private const float ContentWidth = 780f;
    private const float RowHeight = 60f;

    // Where each column of a row sits, measured from the middle of the row.
    private const float LabelCentre = -264f;
    private const float LabelWidth = 232f;
    private const float SliderCentre = 60f;
    private const float SliderWidth = 400f;
    private const float ValueCentre = 322f;
    private const float ValueWidth = 108f;
    private const float ToggleCentre = 250f;

    private static readonly Color TitleColor = new Color(0.851f, 0.851f, 0.851f, 1f);
    private static readonly Color HeadColor = new Color(1f, 0.886f, 0.639f, 1f);
    private static readonly Color BodyColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    private static readonly Color HighlightColor = new Color(1f, 0.788f, 0.353f, 1f);

    private static readonly Color SliderTrackColor = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color SliderHandleColor = new Color(0.93f, 0.91f, 0.86f, 1f);
    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Color DropdownListColor = new Color(0.09f, 0.09f, 0.09f, 0.98f);

    /// <summary>One volume row: the label the player reads and the bus it moves.</summary>
    private struct VolumeRow
    {
        public string Name;
        public string Label;
        public VolumeSlider.Bus Bus;
    }

    // Master first because it is the one most players touch, then loudest to quietest of the rest.
    // Ambient is here before anything plays on it on purpose: a bus that appears later reads as a
    // setting that was taken away and given back, and this way the mix a player sets today still means
    // the same thing when ambience ships.
    private static readonly VolumeRow[] VolumeRows =
    {
        new VolumeRow { Name = "Master", Label = "Master", Bus = VolumeSlider.Bus.Master },
        new VolumeRow { Name = "Music", Label = "Music", Bus = VolumeSlider.Bus.Music },
        new VolumeRow { Name = "Sfx", Label = "Sound Effects", Bus = VolumeSlider.Bus.Sfx },
        new VolumeRow { Name = "Ambient", Label = "Ambience", Bus = VolumeSlider.Bus.Ambient }
    };

    /// <summary>
    /// Puts the panel where it belongs, and is SAFE TO RE-RUN: it creates the prefab only if it is
    /// missing, instances it only where there is no instance yet, and leaves everything that is already
    /// in place exactly as it is -- hand-edits included.
    ///
    /// The destructive rebuild is a separate menu entry, and says so in its name.
    /// </summary>
    [MenuItem("Tools/Settings/Install Settings Panels")]
    public static void RebuildAll()
    {
        // The prefab first: both hosts carry nothing but an instance of it, so this is where the panel
        // actually comes from and the two installs below are placement.
        if (LoadOrBuildPrefab() == null) return;

        // A prefab built before the panel grew its Display section and Save button gets them added
        // in place, around whatever has been tuned by hand since -- the install path's promise is to
        // fill in what is missing, not to start over.
        UpgradePrefab();

        BuildIntoEscMenu();
        BuildIntoMainMenu();
    }

    // ---------------------------------------------------------------------------------------------
    // Upgrading a prefab that predates a row
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds the Display section (with the resolution dropdown) and the Save button to an existing
    /// panel prefab that lacks them, and wires them to the controller. Everything already there is
    /// kept -- including hand-tuned positions -- and only nudged as far as the new rows need: the
    /// sections above move up a little, the frame grows a little, and the buttons hanging off its
    /// bottom edge move down with it.
    ///
    /// Safe to re-run: a prefab that already has both is left untouched.
    /// </summary>
    [MenuItem("Tools/Settings/Add Missing Rows To Settings Panel Prefab")]
    public static void UpgradePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"[Settings] {PrefabPath} does not exist; build it first.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var controller = root.GetComponent<SettingsPanelController>();
            var window = root.transform.Find("Window") as RectTransform;
            if (controller == null || window == null)
            {
                Debug.LogError($"[Settings] {PrefabPath} has no controller or Window; cannot upgrade.");
                return;
            }

            bool changed = false;

            var serialized = new SerializedObject(controller);

            if (window.Find(DisplaySectionName) == null)
            {
                AddDisplaySectionInPlace(window, serialized);
                changed = true;
            }

            // A panel built before it started sizing itself to its host canvas gets the two parts that
            // scale named. Everything else on the panel is a child of one of them.
            SerializedProperty scaled = serialized.FindProperty("scaledParts");
            if (scaled.arraySize == 0 && WireScaledParts(root.transform, scaled)) changed = true;

            // A panel built before it carried its own canvas gets one, so its sprites stop being read
            // at whatever pixels-per-unit the host happens to use.
            if (EnsurePanelCanvas(root)) changed = true;

            Transform display = window.Find(DisplaySectionName);
            if (display != null && display.Find(FullscreenRowName) == null)
            {
                AddFullscreenRowInPlace(window, display, serialized);
                changed = true;
            }

            if (window.Find(SaveButtonName) == null)
            {
                AddSaveButtonInPlace(window, serialized);
                changed = true;
            }

            if (window.Find(UnsavedLabelName) == null)
            {
                AddUnsavedLabelInPlace(window, serialized);
                changed = true;
            }

            // A dropdown built before its list stopped scrolling by drag gets the drag-proof rect
            // swapped in; the rest of the list -- how it has been styled since -- stays as it is.
            var dropdown = root.GetComponentInChildren<TMP_Dropdown>(true);
            var listScroll = dropdown != null && dropdown.template != null
                ? dropdown.template.GetComponent<ScrollRect>()
                : null;
            if (listScroll != null)
            {
                bool dragProof = listScroll is ScrollbarOnlyScrollRect;

                var barRect = listScroll.verticalScrollbar != null
                    ? (RectTransform)listScroll.verticalScrollbar.transform
                    : null;
                bool barSized = barRect != null && Mathf.Approximately(barRect.sizeDelta.x, DropdownScrollbarWidth);

                if (!dragProof || !barSized)
                {
                    StyleDropdownScrollbar(MakeScrollbarOnly(listScroll));
                    changed = true;
                }
            }

            if (root.transform.Find(ConfirmDialogName) == null)
            {
                TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
                TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

                ResolutionConfirmDialog dialog = BuildResolutionConfirm(root.transform, titleFont, bodyFont);
                serialized.FindProperty("resolutionConfirm").objectReferenceValue = dialog;
                changed = true;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo()) changed = true;

            if (!changed)
            {
                Debug.Log($"[Settings] {PrefabPath} already has every row; left untouched.");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[Settings] added the Display section and Save button to {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// How far the existing sections move up to make room, how much taller the frame gets, and how far
    /// the buttons on its bottom edge follow it. The buttons move LESS than the frame grows: they hang
    /// close enough to the bottom of a square screen that the difference is taken out of their overlap
    /// with the frame's border rather than out of the screen.
    /// </summary>
    private const float UpgradeShiftUp = 60f;
    private const float UpgradeFrameGrow = 40f;
    private const float UpgradeButtonShift = 28f;

    /// <summary>
    /// The vertical room a Display section takes: its heading, its one row, and the gap the panel
    /// leaves before the next heading.
    /// </summary>
    private const float DisplaySectionHeight = 56f + DisplayRowStep + 68f;

    /// <summary>Distance between the section's two rows, and what adding one to it costs the panel.</summary>
    private const float DisplayRowStep = 56f;

    /// <summary>
    /// Fits a Display section at the top of a hand-tuned panel, where the Audio heading was, and moves
    /// the existing sections down under it. Measured off what is there rather than off this file's
    /// layout constants, because the two parted company the moment someone opened the prefab in the
    /// editor.
    /// </summary>
    private static void AddDisplaySectionInPlace(RectTransform window, SerializedObject controller)
    {
        var audioLabel = window.Find("AudioLabel") as RectTransform;

        // The existing sections move up to take the slack under the divider, then down by the height
        // of the section going in above them.
        float sectionShift = UpgradeShiftUp - DisplaySectionHeight;

        foreach (string name in new[] { "AudioLabel", "GameObject", "InterfaceLabel", "ActionLogRow", "MuteRow" })
        {
            foreach (RectTransform rect in DirectChildren(window, name))
            {
                if (rect.parent != window) continue;
                rect.anchoredPosition += new Vector2(0f, sectionShift);
            }
        }

        // Where the first heading now sits, less the shift it just took: the first heading's old spot,
        // plus the slack.
        float y = audioLabel != null
            ? audioLabel.anchoredPosition.y + DisplaySectionHeight
            : WindowHeight * 0.5f - 176f;

        // The frame grows DOWN: its top edge stays where the title expects it.
        var frame = window.Find("Frame") as RectTransform;
        if (frame != null)
        {
            float scale = Mathf.Approximately(frame.localScale.y, 0f) ? 1f : frame.localScale.y;
            float top = frame.anchoredPosition.y + frame.sizeDelta.y * scale * 0.5f;
            float newHeight = frame.sizeDelta.y + UpgradeFrameGrow / scale;

            frame.sizeDelta = new Vector2(frame.sizeDelta.x, newHeight);
            frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, top - newHeight * scale * 0.5f);
        }

        // The buttons hang off the frame's bottom edge, so they follow it.
        foreach (string name in new[] { "Button_Reset", "Button_Close", SaveButtonName, "ResetButton", "CloseButton" })
        {
            foreach (RectTransform rect in DirectChildren(window, name))
                rect.anchoredPosition -= new Vector2(0f, UpgradeButtonShift);
        }

        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        BuildDisplaySection(window, bodyFont, y, out GameObject section, out ToggleButton fullscreen,
            out TMP_Dropdown dropdown);

        // Kept in reading order in the hierarchy too, so the prefab reads the way the panel does.
        if (audioLabel != null) section.transform.SetSiblingIndex(audioLabel.GetSiblingIndex());

        controller.FindProperty("displaySection").objectReferenceValue = section;
        controller.FindProperty("resolutionDropdown").objectReferenceValue = dropdown;
        controller.FindProperty("fullscreenRow.toggle").objectReferenceValue = fullscreen;
        controller.FindProperty("fullscreenRow.stateLabel").objectReferenceValue = StateLabelOf(fullscreen);
    }

    /// <summary>
    /// Fits a fullscreen switch above the resolution row of a hand-tuned panel, and makes room for it
    /// by growing the frame SYMMETRICALLY -- half up, half down -- rather than downward. The buttons
    /// hang off the frame's bottom edge and are already close to the bottom of a square screen; a
    /// panel that grows only downward pushes them off it.
    ///
    /// So the top half of the panel moves up by half a row, the bottom half moves down by half a row,
    /// and the space that opens between them is exactly the row going in.
    /// </summary>
    private static void AddFullscreenRowInPlace(RectTransform window, Transform section, SerializedObject controller)
    {
        float half = DisplayRowStep * 0.5f;

        // Grown about its own centre: the frame keeps its middle, so the window stays where it is in
        // whatever host it has been placed in.
        var frame = window.Find("Frame") as RectTransform;
        if (frame != null)
        {
            float scale = Mathf.Approximately(frame.localScale.y, 0f) ? 1f : frame.localScale.y;
            frame.sizeDelta = new Vector2(frame.sizeDelta.x, frame.sizeDelta.y + DisplayRowStep / scale);
        }

        // Everything above the new row rises with the frame's top edge...
        foreach (string name in new[] { "Title", "Divider", DisplaySectionName })
        {
            foreach (RectTransform rect in DirectChildren(window, name))
                rect.anchoredPosition += new Vector2(0f, half);
        }

        // ...and everything below it, the bottom-edge buttons included, sinks with the bottom edge.
        // Both coordinate spaces in play here read downward as negative, so both subtract.
        foreach (string name in new[]
                 {
                     "AudioLabel", "GameObject", "InterfaceLabel", "ActionLogRow", "MuteRow",
                     UnsavedLabelName, "Button_Reset", "Button_Close", SaveButtonName,
                     "ResetButton", "CloseButton"
                 })
        {
            foreach (RectTransform rect in DirectChildren(window, name))
                rect.anchoredPosition -= new Vector2(0f, half);
        }

        // Inside the section the resolution row drops a row's height, and the switch takes its place.
        var resolution = section.Find(ResolutionRowName) as RectTransform;
        float y = resolution != null ? resolution.anchoredPosition.y : -56f;
        if (resolution != null) resolution.anchoredPosition -= new Vector2(0f, DisplayRowStep);

        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        ToggleButton toggle = BuildToggleRow((RectTransform)section, FullscreenRowName, "Fullscreen", bodyFont, y);

        if (resolution != null) toggle.transform.parent.SetSiblingIndex(resolution.GetSiblingIndex());

        // Lined up with the switches already on the panel rather than with this file's constant: the
        // existing rows may well have been nudged since they were built, and a new switch that sits a
        // few pixels off the column is more obviously wrong than one in a slightly odd column.
        RectTransform neighbour = FirstChild(window.Find("ActionLogRow"), "Toggle")
                                  ?? FirstChild(window.Find("MuteRow"), "Toggle");
        if (neighbour != null)
        {
            var rect = (RectTransform)toggle.transform;
            rect.anchoredPosition = new Vector2(neighbour.anchoredPosition.x, rect.anchoredPosition.y);
        }

        controller.FindProperty("fullscreenRow.toggle").objectReferenceValue = toggle;
        controller.FindProperty("fullscreenRow.stateLabel").objectReferenceValue = StateLabelOf(toggle);
    }

    /// <summary>
    /// A Save button in the same style and at the same height as the panel's existing bottom-edge
    /// buttons, mirrored to the right-hand corner. Cloned from whichever of them is there, so it
    /// carries their look -- hand-styled or not -- rather than this file's idea of it.
    /// </summary>
    private static void AddSaveButtonInPlace(RectTransform window, SerializedObject controller)
    {
        RectTransform model = FirstChild(window, "Button_Reset") ?? FirstChild(window, "ResetButton");

        Button save;

        if (model != null)
        {
            GameObject clone = Object.Instantiate(model.gameObject, window);
            clone.name = SaveButtonName;
            clone.SetActive(true);

            var rect = (RectTransform)clone.transform;
            rect.SetSiblingIndex(model.GetSiblingIndex() + 1);

            // Mirrored across the window's centre line, whatever the model is anchored to.
            float anchorX = Mathf.Lerp(-WindowWidth * 0.5f, WindowWidth * 0.5f, model.anchorMin.x);
            float centreX = anchorX + model.anchoredPosition.x;
            rect.anchoredPosition = new Vector2(-centreX - anchorX, model.anchoredPosition.y);

            var labels = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (labels.Length > 0) labels[0].text = "Save";

            save = clone.GetComponent<Button>();
        }
        else
        {
            save = BuildPanelButton(window, SaveButtonName, "Save", new Vector2(0f, -WindowHeight * 0.5f + 92f));
        }

        controller.FindProperty("saveButton").objectReferenceValue = save;
    }

    /// <summary>
    /// The unsaved-changes notice, level with the bottom-edge buttons and in the space between them.
    /// Measured off the Save button so it lands wherever that has been put.
    /// </summary>
    private static void AddUnsavedLabelInPlace(RectTransform window, SerializedObject controller)
    {
        RectTransform save = FirstChild(window, SaveButtonName);

        float y;
        if (save != null)
        {
            // The button's centre in the window's own centred coordinates, whatever it is anchored to.
            float anchorY = Mathf.Lerp(-window.sizeDelta.y * 0.5f, window.sizeDelta.y * 0.5f, save.anchorMin.y);
            y = anchorY + save.anchoredPosition.y;
        }
        else
        {
            y = -window.sizeDelta.y * 0.5f + 92f;
        }

        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        TextMeshProUGUI label = BuildUnsavedLabel(window, bodyFont, new Vector2(0f, y));

        controller.FindProperty("unsavedLabel").objectReferenceValue = label.gameObject;
    }

    private static TextMeshProUGUI BuildUnsavedLabel(RectTransform window, TMP_FontAsset font, Vector2 position)
    {
        TextMeshProUGUI label = MakeText(window, UnsavedLabelName, font, 22f, HighlightColor, TextAlignmentOptions.Center);
        label.text = "You have unsaved changes";
        label.fontStyle = FontStyles.Italic;
        SetRect(label.rectTransform, position.x, position.y, 360f, 40f);

        // Off until the controller has something to say; authored visible would mean a fresh panel
        // opens claiming changes that were never made.
        label.gameObject.SetActive(false);

        return label;
    }

    /// <summary>
    /// Names the parts that scale with the host canvas: the framed window, and the confirm dialog's
    /// box. Deliberately NOT the backdrop or the dialog's shade -- those have to cover the screen at
    /// any size. Returns whether anything was written.
    /// </summary>
    /// <summary>
    /// Gives the panel a canvas of its own, so its sprites are read at the size they were drawn at
    /// whatever the host canvas says. See <see cref="PanelReferencePixelsPerUnit"/>.
    ///
    /// The raycaster comes with it: graphics register for input with their nearest canvas, so a nested
    /// canvas without one is a panel nothing can be clicked on. Sorting is deliberately NOT overridden
    /// -- the panel keeps drawing where its place in the hierarchy puts it.
    ///
    /// Returns whether anything changed.
    /// </summary>
    private static bool EnsurePanelCanvas(GameObject panelRoot)
    {
        bool changed = false;

        var canvas = panelRoot.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = panelRoot.AddComponent<Canvas>();
            changed = true;
        }

        // The pixels-per-unit itself is NOT set here: Canvas does not serialise it, so a value written
        // into the prefab would not survive the save. SettingsPanelController writes it on Awake.
        if (canvas.overrideSorting)
        {
            canvas.overrideSorting = false;
            changed = true;
        }

        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
        {
            panelRoot.AddComponent<GraphicRaycaster>();
            changed = true;
        }

        return changed;
    }

    private static bool WireScaledParts(Transform panelRoot, SerializedProperty scaledParts)
    {
        var parts = new List<RectTransform>();

        var window = panelRoot.Find("Window") as RectTransform;
        if (window != null) parts.Add(window);

        var box = panelRoot.Find(ConfirmDialogName + "/Box") as RectTransform;
        if (box != null) parts.Add(box);

        if (parts.Count == 0) return false;

        scaledParts.arraySize = parts.Count;
        for (int i = 0; i < parts.Count; i++)
            scaledParts.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];

        return true;
    }

    private static IEnumerable<RectTransform> DirectChildren(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name && child is RectTransform rect) yield return rect;
        }
    }

    private static RectTransform FirstChild(Transform parent, string name)
    {
        if (parent == null) return null;

        foreach (RectTransform rect in DirectChildren(parent, name)) return rect;
        return null;
    }

    // ---------------------------------------------------------------------------------------------
    // The prefab
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds <see cref="PrefabPath"/> from scratch, THROWING AWAY whatever the prefab currently holds
    /// -- which includes anything tuned in the editor since it was last built. That is why it is its own
    /// menu entry, named for what it costs, rather than something the install path does on the way past.
    ///
    /// Existing instances keep their link and pick the new panel up on their own.
    /// </summary>
    [MenuItem("Tools/Settings/Rebuild Settings Panel Prefab (discards hand-edits)")]
    public static GameObject BuildPrefab()
    {
        // The controls the panel is assembled from are resolved FIRST: building the slider prefab opens a
        // workspace scene of its own, and opening one inside another is asking for the wrong scene to be
        // written into.
        if (LoadOrBuildSliderPrefab() == null) return null;

        // Built inside a throwaway scene of its own, under a real Canvas. A UI hierarchy assembled with no
        // Canvas above it does not survive being saved -- Images come back as SpriteRenderers and the TMP
        // children are dropped -- and building it in whatever scene happens to be open would dirty the
        // work someone has in progress there.
        Scene workspace = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        GameObject prefab = null;

        try
        {
            var canvasGo = new GameObject("Workspace Canvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            EditorSceneManager.MoveGameObjectToScene(canvasGo, workspace);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            SettingsPanelController controller = BuildPanel(canvasGo.transform);

            prefab = PrefabUtility.SaveAsPrefabAsset(controller.gameObject, PrefabPath);

            if (!VerifySavedPrefab()) return null;

            Debug.Log($"[Settings] rebuilt {PrefabPath}");
        }
        finally
        {
            EditorSceneManager.CloseScene(workspace, true);
        }

        return prefab;
    }

    /// <summary>
    /// Reads the saved prefab back off disk and checks the pieces that go missing when a UI prefab is
    /// saved wrong. Cheap, and it turns a silently broken panel into a message.
    /// </summary>
    private static bool VerifySavedPrefab()
    {
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved == null)
        {
            Debug.LogError($"[Settings] {PrefabPath} did not save.");
            return false;
        }

        int images = saved.GetComponentsInChildren<Image>(true).Length;
        int labels = saved.GetComponentsInChildren<TextMeshProUGUI>(true).Length;
        int sliders = saved.GetComponentsInChildren<VolumeSlider>(true).Length;
        int dropdowns = saved.GetComponentsInChildren<TMP_Dropdown>(true).Length;

        if (images > 0 && labels > 0 && sliders == VolumeRows.Length && dropdowns == 1) return true;

        Debug.LogError($"[Settings] {PrefabPath} saved wrong: {images} image(s), {labels} label(s), " +
                       $"{sliders} of {VolumeRows.Length} volume slider(s), {dropdowns} dropdown(s).");
        return false;
    }

    /// <summary>The prefab, built first if it is not there yet.</summary>
    private static GameObject LoadOrBuildPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return prefab != null ? prefab : BuildPrefab();
    }

    /// <summary>
    /// Puts an instance of the prefab under <paramref name="parent"/>, replacing whatever sat there under
    /// the same name -- including a panel built by the earlier version of this tool, which was a plain
    /// copy rather than an instance.
    ///
    /// The two things a host gets to decide are applied here, as instance overrides: where the window
    /// sits, and whether the panel dims what is behind it.
    /// </summary>
    private static SettingsPanelController InstallInstance(Transform parent, bool withBackdrop,
        Vector2 windowPosition, out bool created)
    {
        created = false;

        GameObject prefab = LoadOrBuildPrefab();
        if (prefab == null) return null;

        Transform existing = parent.Find(PanelName);

        if (existing != null)
        {
            // Already an instance of our prefab: LEAVE IT ALONE. Whoever put it here may have nudged the
            // window, switched the backdrop, or otherwise tuned it since, and none of that is the tool's
            // to overwrite -- it would be thrown away to rebuild something that is already correct. The
            // panel's CONTENT lives in the prefab, so an instance left untouched still picks up every
            // change made there.
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing.gameObject) == PrefabPath)
                return existing.GetComponent<SettingsPanelController>();

            // Anything else under this name is a leftover from before the panel was a prefab, and is
            // replaced -- that is the one case where there is nothing worth keeping.
            Object.DestroyImmediate(existing.gameObject);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        created = true;
        instance.name = PanelName;

        Stretch((RectTransform)instance.transform);

        var window = instance.transform.Find("Window") as RectTransform;
        if (window != null) window.anchoredPosition = windowPosition;

        Transform backdrop = instance.transform.Find("Backdrop");
        if (backdrop != null) backdrop.gameObject.SetActive(withBackdrop);

        // Authored off. Both owners also put it away on startup, but a panel that is only ever hidden by
        // somebody else is one bad merge away from opening on top of a match.
        instance.SetActive(false);

        return instance.GetComponent<SettingsPanelController>();
    }

    // ---------------------------------------------------------------------------------------------
    // The pause menu
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Tools/Settings/Rebuild Settings Panel (Pause Menu)")]
    public static void BuildIntoEscMenu()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EscMenuPrefabPath);
        try
        {
            Transform panel = root.transform.Find("Panel");
            if (panel == null)
            {
                Debug.LogError($"[Settings] no Panel under {EscMenuPrefabPath}");
                return;
            }

            // The rules and how-to-play panels live in the left column; the settings take the same space
            // and are never up at the same time, so the window sits in the middle of it.
            //
            // No backdrop: the pause menu already has one, and a second full-screen block on top of it
            // would cover the button column -- including the Settings button that has to stay clickable
            // to put the panel away again.
            SettingsPanelController controller = InstallInstance(panel, withBackdrop: false,
                windowPosition: new Vector2(-131.5f, 0f), created: out bool panelCreated);

            if (controller == null) return;

            Button settingsButton = EnsureEscMenuButton(panel, out bool buttonCreated);

            bool wired = false;

            var escMenu = root.GetComponent<EscMenuController>();
            if (escMenu != null)
            {
                var serialized = new SerializedObject(escMenu);
                serialized.FindProperty("settingsPanel").objectReferenceValue = controller;
                serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;

                // Reports whether anything actually moved, which is what tells a re-run that the menu was
                // already wired to these two objects.
                wired = serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Settings] EscMenu has no EscMenuController to wire the panel to.");
            }

            // Nothing was missing, so nothing is written. A tool that rewrites an asset it did not change
            // shows up as a diff to review and a file to merge, and -- worse here -- reformats a prefab
            // somebody may be part-way through editing.
            if (!panelCreated && !buttonCreated && !wired)
            {
                Debug.Log($"[Settings] {EscMenuPrefabPath} already has the panel; left untouched.");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, EscMenuPrefabPath);
            Debug.Log($"[Settings] installed the settings panel in {EscMenuPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// The Settings entry in the pause menu's button column, cloned from How To Play so it carries that
    /// button's whole look and its ToggleButton latch without any of it being restated here.
    ///
    /// Adding a sixth button also re-spaces the column: five buttons at the authored 130 apart already
    /// reach the frame, and a sixth at that spacing would hang out the bottom of the art.
    /// </summary>
    private static Button EnsureEscMenuButton(Transform panel, out bool created)
    {
        created = false;

        Transform buttons = panel.Find("ButtonsPanel/Buttons");
        if (buttons == null)
        {
            Debug.LogError("[Settings] no ButtonsPanel/Buttons under the pause menu Panel.");
            return null;
        }

        Transform howToPlay = buttons.Find("HowToPlayButton");
        if (howToPlay == null)
        {
            Debug.LogError("[Settings] no HowToPlayButton to model the Settings button on.");
            return null;
        }

        // Already there: leave it exactly as it is. It may well have been moved, relabelled or restyled
        // by hand since, and re-cloning it would throw that away to produce something that already
        // exists. Same reasoning as the panel instance in InstallInstance.
        Transform existing = buttons.Find(SettingsButtonName);
        if (existing != null) return existing.GetComponent<Button>();

        var clone = Object.Instantiate(howToPlay.gameObject, buttons);
        created = true;
        clone.name = SettingsButtonName;

        var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = "Settings";

        // The clone comes over latched if How To Play happened to be saved that way.
        var toggle = clone.GetComponent<ToggleButton>();
        if (toggle != null) toggle.SetIsOn(false, notify: false);

        Respace(buttons);

        return clone.GetComponent<Button>();
    }

    /// <summary>
    /// Spreads the column's buttons evenly over the frame they sit in, so the tool never has to know how
    /// many there are -- adding another one here is a matter of the button existing.
    /// </summary>
    private static void Respace(Transform buttons)
    {
        var rects = new List<RectTransform>();
        for (int i = 0; i < buttons.childCount; i++)
        {
            if (buttons.GetChild(i).gameObject.activeSelf) rects.Add((RectTransform)buttons.GetChild(i));
        }

        if (rects.Count < 2) return;

        // Measured off the frame art rather than assumed, exactly as the rules panel measures its own,
        // so resizing the frame in the editor and re-running this keeps the buttons inside it.
        var frame = buttons.parent.Find("Frame") as RectTransform;
        float frameHeight = frame != null ? frame.rect.height * frame.localScale.y : 700f;
        float frameCentre = frame != null ? frame.anchoredPosition.y : 36f;

        // In the Buttons container's own coordinates.
        frameCentre -= ((RectTransform)buttons).anchoredPosition.y;

        RectTransform first = rects[0];
        float buttonHeight = first.rect.height * first.localScale.y;

        float span = Mathf.Max(0f, frameHeight - buttonHeight - 24f);
        float step = span / (rects.Count - 1);
        float top = frameCentre + span * 0.5f;

        for (int i = 0; i < rects.Count; i++)
            rects[i].anchoredPosition = new Vector2(rects[i].anchoredPosition.x, top - step * i);
    }

    // ---------------------------------------------------------------------------------------------
    // The title screen
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Tools/Settings/Rebuild Settings Panel (Main Menu)")]
    public static void BuildIntoMainMenu()
    {
        Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool alreadyOpen = scene.IsValid() && scene.isLoaded;

        if (!alreadyOpen) scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

        try
        {
            Transform canvas = FindRoot(scene, "Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[Settings] no Canvas in {MainMenuScenePath}");
                return;
            }

            // Its own backdrop here: the title screen has no dimmed layer of its own, and the settings
            // must block the menu underneath the way the credits do.
            SettingsPanelController controller = InstallInstance(canvas, withBackdrop: true,
                windowPosition: Vector2.zero, created: out bool panelCreated);

            if (controller == null) return;

            // Above the credits and everything else on the canvas, whatever order they were authored in.
            // Only on the way in: sibling order is draw order, and if it has been moved since, that was
            // somebody deciding what draws over what.
            if (panelCreated) controller.transform.SetAsLastSibling();

            Button settingsButton = EnsureMainMenuButton(canvas, out bool buttonCreated);

            bool wired = false;

            var menu = FindComponent<MainMenuManager>(scene);
            if (menu != null)
            {
                var serialized = new SerializedObject(menu);
                serialized.FindProperty("settingsPanel").objectReferenceValue = controller;
                serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;
                wired = serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Settings] the main menu has no MainMenuManager to wire the panel to.");
            }

            if (!panelCreated && !buttonCreated && !wired)
            {
                Debug.Log($"[Settings] {MainMenuScenePath} already has the panel; left untouched.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Settings] installed the settings panel in {MainMenuScenePath}");
        }
        finally
        {
            if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// The title screen's way in: a button of its own in the corner rather than a fifth entry in the
    /// column, because the frame around that column has no room left in it. Cloned from the Credits
    /// button so it is the same button in a smaller size.
    /// </summary>
    private static Button EnsureMainMenuButton(Transform canvas, out bool created)
    {
        created = false;

        // Kept as found, for the same reason the pause menu's is: it may well have been moved or
        // relabelled since, and none of that is the tool's to undo.
        Transform existing = canvas.Find(SettingsButtonName);
        if (existing != null) return existing.GetComponent<Button>();

        Transform model = canvas.Find("Image/Buttons/CreditsButton (1)");
        if (model == null)
        {
            Debug.LogError("[Settings] no Credits button to model the Settings button on.");
            return null;
        }

        var clone = Object.Instantiate(model.gameObject, canvas);
        created = true;
        clone.name = SettingsButtonName;

        // Anything the model carried that only made sense in the column: its tooltip, and the second,
        // inactive caption the menu buttons keep for their "playing with" line.
        var tooltip = clone.GetComponent<UITooltipTrigger>();
        if (tooltip != null) Object.DestroyImmediate(tooltip);

        var labels = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (i == 0) labels[i].text = "Settings";
            else labels[i].gameObject.SetActive(false);
        }

        var rect = (RectTransform)clone.transform;
        rect.localScale = model.localScale * 0.62f;

        // The game is locked to a square view (see AspectRatioLetterbox), so the canvas rect IS what the
        // player sees and a corner of it is a corner of the screen.
        var canvasRect = (RectTransform)canvas;
        float halfWidth = canvasRect.rect.width * 0.5f;
        float halfHeight = canvasRect.rect.height * 0.5f;
        float buttonWidth = rect.rect.width * rect.localScale.x;
        float buttonHeight = rect.rect.height * rect.localScale.y;

        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(
            halfWidth - buttonWidth * 0.5f - 28f,
            -halfHeight + buttonHeight * 0.5f + 28f);

        return clone.GetComponent<Button>();
    }

    // ---------------------------------------------------------------------------------------------
    // The panel itself
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The panel, built once into what becomes the prefab. Everything a host is allowed to differ on is
    /// deliberately NOT a parameter here -- it is an override applied to the instance afterwards, so the
    /// asset stays the single description of what the settings panel is.
    /// </summary>
    private static SettingsPanelController BuildPanel(Transform parent)
    {
        GameObject rootGo = NewUI(PanelName, parent);
        Stretch((RectTransform)rootGo.transform);
        EnsurePanelCanvas(rootGo);
        var controller = rootGo.AddComponent<SettingsPanelController>();

        // Authored ON, and switched off on the instance that does not want it: a backdrop is the norm for
        // this panel, and the pause menu -- which already dims the screen itself -- is the exception.
        // Raycast target, because it is the thing that stops a click reaching the menu behind the panel.
        Image backdrop = MakeImage(rootGo.transform, "Backdrop", BackdropColor);
        backdrop.raycastTarget = true;
        Stretch(backdrop.rectTransform);

        GameObject windowGo = NewUI("Window", rootGo.transform);
        var window = (RectTransform)windowGo.transform;
        SetRect(window, 0f, 0f, WindowWidth, WindowHeight);

        BuildFrame(window, WindowWidth, WindowHeight);

        TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        float top = WindowHeight * 0.5f;

        TextMeshProUGUI title = MakeText(window, "Title", titleFont, 46f, TitleColor, TextAlignmentOptions.Center);
        title.text = "Settings";
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform, 0f, top - 74f, ContentWidth, 70f);

        Image divider = MakeImage(window, "Divider", new Color(1f, 1f, 1f, 0.16f));
        SetRect(divider.rectTransform, 0f, top - 118f, ContentWidth, 4f);

        float y = top - 176f;

        // First, so the one section that is not there on every platform leaves its gap at the top of
        // the panel, right under the divider, where a gap reads as breathing room rather than as a
        // row that went missing.
        BuildDisplaySection(window, bodyFont, y, out GameObject displaySection,
            out ToggleButton fullscreenToggle, out TMP_Dropdown resolutionDropdown);
        y -= DisplaySectionHeight;

        MakeSectionLabel(window, "AudioLabel", "Audio", bodyFont, y);
        y -= 56f;

        foreach (VolumeRow row in VolumeRows)
        {
            BuildVolumeRow(window, row, bodyFont, y);
            y -= RowHeight + 8f;
        }

        // The loop has already stepped past the last row; the rest is the gap between sections.
        y -= 22f;

        MakeSectionLabel(window, "InterfaceLabel", "Interface", bodyFont, y);
        y -= 56f;

        ToggleButton actionLogToggle = BuildToggleRow(window, "ActionLogRow", "Show Action Log", bodyFont, y);

        // The buttons sit on the bottom edge of the frame rather than under the last row, so the
        // panel does not visibly change shape as rows are added to it.
        float buttonY = -WindowHeight * 0.5f + 92f;
        Button resetButton = BuildPanelButton(window, "ResetButton", "Defaults", new Vector2(-230f, buttonY));
        Button saveButton = BuildPanelButton(window, SaveButtonName, "Save", new Vector2(0f, buttonY));
        Button closeButton = BuildPanelButton(window, "CloseButton", "Close", new Vector2(230f, buttonY));

        // Just above the button row, since with three buttons there is no room between them.
        TextMeshProUGUI unsavedLabel = BuildUnsavedLabel(window, bodyFont, new Vector2(0f, buttonY + 78f));

        // Over the window, not in it: it has to cover the panel's own buttons while it is asking.
        ResolutionConfirmDialog resolutionConfirm = BuildResolutionConfirm(rootGo.transform, titleFont, bodyFont);

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("actionLogRow.toggle").objectReferenceValue = actionLogToggle;
        serialized.FindProperty("actionLogRow.stateLabel").objectReferenceValue = StateLabelOf(actionLogToggle);
        serialized.FindProperty("displaySection").objectReferenceValue = displaySection;
        serialized.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDropdown;
        serialized.FindProperty("fullscreenRow.toggle").objectReferenceValue = fullscreenToggle;
        serialized.FindProperty("fullscreenRow.stateLabel").objectReferenceValue = StateLabelOf(fullscreenToggle);
        serialized.FindProperty("resolutionConfirm").objectReferenceValue = resolutionConfirm;
        serialized.FindProperty("unsavedLabel").objectReferenceValue = unsavedLabel.gameObject;
        serialized.FindProperty("saveButton").objectReferenceValue = saveButton;
        serialized.FindProperty("resetButton").objectReferenceValue = resetButton;
        serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
        WireScaledParts(rootGo.transform, serialized.FindProperty("scaledParts"));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return controller;
    }

    /// <summary>The toggle's own caption is what says On or Off; there is no second label to keep in step.</summary>
    private static TMP_Text StateLabelOf(ToggleButton toggle) =>
        toggle != null ? toggle.GetComponentInChildren<TextMeshProUGUI>(true) : null;

    private static void BuildFrame(RectTransform window, float width, float height)
    {
        Sprite frameSprite = LoadFrameSprite();

        GameObject go = NewUI("Frame", window);
        var image = go.AddComponent<Image>();
        image.sprite = frameSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.raycastTarget = true;

        // Scaled 8x like every other frame in this UI: the art is authored small, and its slice borders
        // scale with it, so a frame built at 1x would have hairline edges.
        var rect = (RectTransform)go.transform;
        SetRect(rect, 0f, 0f, width / 8f, height / 8f);
        rect.localScale = new Vector3(8f, 8f, 8f);
        rect.SetAsFirstSibling();
    }

    /// <summary>Name of the keep-or-revert question, a sibling of the Window at the panel's root.</summary>
    private const string ConfirmDialogName = "ResolutionConfirm";

    // Wide enough for the longest line it has to carry, which is a size and a screen mode together:
    // "Reverting to 2560 x 1440, fullscreen in 15 s".
    private const float ConfirmWidth = 660f;
    private const float ConfirmHeight = 340f;

    /// <summary>
    /// The question that follows a resolution change: a shade over the whole panel so nothing under it
    /// can be pressed, a small framed box, the countdown, and Keep / Revert. Built inactive; the
    /// controller shows it.
    /// </summary>
    private static ResolutionConfirmDialog BuildResolutionConfirm(Transform panelRoot, TMP_FontAsset titleFont,
        TMP_FontAsset bodyFont)
    {
        GameObject go = NewUI(ConfirmDialogName, panelRoot);
        Stretch((RectTransform)go.transform);
        var dialog = go.AddComponent<ResolutionConfirmDialog>();

        Image shade = MakeImage(go.transform, "Shade", new Color(0f, 0f, 0f, 0.6f));
        shade.raycastTarget = true;
        Stretch(shade.rectTransform);

        GameObject boxGo = NewUI("Box", go.transform);
        var box = (RectTransform)boxGo.transform;
        SetRect(box, 0f, 0f, ConfirmWidth, ConfirmHeight);

        BuildFrame(box, ConfirmWidth, ConfirmHeight);

        TextMeshProUGUI title = MakeText(box, "Title", titleFont, 30f, HeadColor, TextAlignmentOptions.Center);
        // Not "this resolution": the same question covers the screen mode, and a save can move both.
        title.text = "Keep these display settings?";
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform, 0f, 98f, ConfirmWidth - 60f, 44f);

        TextMeshProUGUI body = MakeText(box, "Body", bodyFont, 24f, BodyColor, TextAlignmentOptions.Center);
        body.text = "Now 1280 × 720\nReverting to 1920 × 1080 in 15 s";
        SetRect(body.rectTransform, 0f, 24f, ConfirmWidth - 60f, 80f);

        Button keep = BuildPanelButton(box, "KeepButton", "Keep", new Vector2(-110f, -92f));
        Button revert = BuildPanelButton(box, "RevertButton", "Revert", new Vector2(110f, -92f));

        var serialized = new SerializedObject(dialog);
        serialized.FindProperty("bodyLabel").objectReferenceValue = body;
        serialized.FindProperty("keepButton").objectReferenceValue = keep;
        serialized.FindProperty("revertButton").objectReferenceValue = revert;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Off until asked. The component hides itself on Awake too, but Awake does not run on an
        // object that starts inactive, and a prefab that opens with the question up is a bug.
        go.SetActive(false);

        return dialog;
    }

    private static Sprite LoadFrameSprite()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PanelSpriteSheetPath))
        {
            if (asset is Sprite sprite && sprite.name == FrameSpriteName) return sprite;
        }

        Debug.LogWarning($"[Settings] {FrameSpriteName} not found in {PanelSpriteSheetPath}; the panel " +
                         "will have no frame art.");
        return null;
    }

    private static void MakeSectionLabel(RectTransform window, string name, string text, TMP_FontAsset font, float y)
    {
        TextMeshProUGUI label = MakeText(window, name, font, 27f, HeadColor, TextAlignmentOptions.Left);
        label.text = text;
        label.fontStyle = FontStyles.Bold;

        // Full content width, centred on the window: a left-aligned label starts at the LEFT EDGE of its
        // rect, so a wide rect centred anywhere else starts outside the frame.
        SetRect(label.rectTransform, 0f, y, ContentWidth, 40f);
    }

    private static void BuildVolumeRow(RectTransform window, VolumeRow row, TMP_FontAsset font, float y)
    {
        GameObject rowGo = NewUI($"Row_{row.Name}", window);
        var rowRect = (RectTransform)rowGo.transform;
        SetRect(rowRect, 0f, y, ContentWidth, RowHeight);

        TextMeshProUGUI label = MakeText(rowRect, "Label", font, 24f, BodyColor, TextAlignmentOptions.Left);
        label.text = row.Label;
        SetRect(label.rectTransform, LabelCentre, 0f, LabelWidth, RowHeight);

        TextMeshProUGUI value = MakeText(rowRect, "Value", font, 24f, HighlightColor, TextAlignmentOptions.Right);
        value.text = "100%";
        SetRect(value.rectTransform, ValueCentre, 0f, ValueWidth, RowHeight);

        GameObject prefab = LoadOrBuildSliderPrefab();
        if (prefab == null)
        {
            Debug.LogError($"[Settings] no slider prefab; the {row.Label} row has no bar.");
            return;
        }

        // An INSTANCE, so the four bars stay one control: restyle the asset and every row follows, and a
        // rebuilt panel keeps whatever it looks like today.
        var sliderGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rowRect);
        sliderGo.name = "Slider";
        sliderGo.layer = rowGo.layer;

        var sliderRect = (RectTransform)sliderGo.transform;
        sliderRect.anchoredPosition = new Vector2(SliderCentre, 0f);
        sliderRect.sizeDelta = new Vector2(SliderWidth, sliderRect.sizeDelta.y);

        // The only two things that are genuinely this row's: which bus it moves, and where it writes the
        // percentage. Everything else -- the colours, the handle, the height -- is the prefab's.
        var volume = sliderGo.GetComponent<VolumeSlider>();
        volume.bus = row.Bus;
        volume.valueLabel = value;
    }

    /// <summary>The slider prefab, built from the description below if it is not there yet.</summary>
    private static GameObject LoadOrBuildSliderPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SliderPath);
        return prefab != null ? prefab : BuildSliderPrefab();
    }

    /// <summary>
    /// Rebuilds <see cref="SliderPath"/> from scratch, THROWING AWAY however it has been styled since.
    /// Its own entry, named for what it costs, for the same reason the panel's rebuild is.
    /// </summary>
    [MenuItem("Tools/Settings/Rebuild Settings Slider Prefab (discards hand-edits)")]
    public static GameObject BuildSliderPrefab()
    {
        Scene workspace = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        try
        {
            var canvasGo = new GameObject("Workspace Canvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            EditorSceneManager.MoveGameObjectToScene(canvasGo, workspace);

            // Built by Unity's own factory rather than by hand: a Slider is four nested rects whose
            // anchors the component writes to as it runs, and getting one of them subtly wrong gives a
            // handle that travels the wrong distance.
            GameObject sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGo.name = "Slider_Settings";
            sliderGo.transform.SetParent(canvasGo.transform, false);

            var rect = (RectTransform)sliderGo.transform;
            SetRect(rect, 0f, 0f, SliderWidth, 26f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            StyleSlider(sliderGo);

            // Left unbound on purpose: the bus and the percentage label belong to whichever row the
            // instance lands in, and the label is not even part of this object.
            sliderGo.AddComponent<VolumeSlider>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sliderGo, SliderPath);

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(SliderPath);
            if (saved == null || saved.GetComponent<Slider>() == null ||
                saved.GetComponentsInChildren<Image>(true).Length != 3)
            {
                Debug.LogError($"[Settings] {SliderPath} saved wrong.");
                return null;
            }

            Debug.Log($"[Settings] rebuilt {SliderPath}");
            return prefab;
        }
        finally
        {
            EditorSceneManager.CloseScene(workspace, true);
        }
    }

    /// <summary>
    /// How far past the painted bar a press still counts, in each direction. The vertical figure takes
    /// the grabbable area out to the full height of a row: the bar is drawn 13px tall inside a 26px
    /// control, and a target that thin in a 60px row is one the pointer misses more often than it hits.
    /// </summary>
    private const float SliderGrabExtraY = 17f;
    private const float SliderGrabExtraX = 6f;

    /// <summary>
    /// Repaints the factory slider in the panel's colours. The default sprites are Unity's own UI skin,
    /// which is the one thing in this menu that would look like Unity rather than like the game.
    /// </summary>
    private static void StyleSlider(GameObject sliderGo)
    {
        AddSliderHitArea(sliderGo);

        Transform background = sliderGo.transform.Find("Background");
        if (background != null)
        {
            var image = background.GetComponent<Image>();
            image.sprite = null;
            image.color = SliderTrackColor;
        }

        Transform fill = sliderGo.transform.Find("Fill Area/Fill");
        if (fill != null)
        {
            var image = fill.GetComponent<Image>();
            image.sprite = null;
            image.color = HighlightColor;
        }

        Transform handle = sliderGo.transform.Find("Handle Slide Area/Handle");
        if (handle != null)
        {
            var image = handle.GetComponent<Image>();
            image.sprite = null;
            image.color = SliderHandleColor;

            // Grabbing the handle should not demand pixel accuracy either, so its own hit rect reaches
            // past the grip it draws. Same trick as the hit area, see AddSliderHitArea.
            image.raycastPadding = new Vector4(-8f, -SliderGrabExtraY, -8f, -SliderGrabExtraY);

            var rect = (RectTransform)handle;
            rect.sizeDelta = new Vector2(22f, 12f);
        }
    }

    /// <summary>
    /// The invisible graphic that makes the whole control clickable.
    ///
    /// Unity's slider factory puts no graphic on the slider's own object, so the only things a pointer
    /// can hit are the painted track and the handle -- which is why a press that visibly lands ON the
    /// slider so often does nothing. This covers the control edge to edge, and events bubble from it up
    /// to the Slider, which is what makes a press anywhere jump the value and start a drag.
    /// </summary>
    private static void AddSliderHitArea(GameObject sliderGo)
    {
        Transform existing = sliderGo.transform.Find("Hit Area");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject hitGo = NewUI("Hit Area", sliderGo.transform);
        hitGo.transform.SetSiblingIndex(0);

        var rect = (RectTransform)hitGo.transform;
        Stretch(rect);

        var image = hitGo.AddComponent<Image>();

        // Fully transparent, but still a raycast target: alpha plays no part in hit testing unless an
        // alpha threshold is set, which is what makes this the standard way to give a control a hit area
        // bigger than the thing it draws.
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        // NEGATIVE padding GROWS the clickable rect -- raycastPadding is applied as xMin += x, yMin += y,
        // xMax -= z, yMax -= w, so positive values SHRINK it. The same inversion the rules panel's
        // scrollbar relies on.
        image.raycastPadding = new Vector4(-SliderGrabExtraX, -SliderGrabExtraY,
                                           -SliderGrabExtraX, -SliderGrabExtraY);
    }

    private static ToggleButton BuildToggleRow(RectTransform window, string name, string label,
        TMP_FontAsset font, float y)
    {
        GameObject rowGo = NewUI(name, window);
        var rowRect = (RectTransform)rowGo.transform;
        SetRect(rowRect, 0f, y, ContentWidth, RowHeight);

        TextMeshProUGUI text = MakeText(rowRect, "Label", font, 24f, BodyColor, TextAlignmentOptions.Left);
        text.text = label;

        // Twice a volume row's label width, since these read as sentences rather than bus names -- and
        // re-centred so the extra width grows to the RIGHT, into the space the slider would occupy.
        SetRect(text.rectTransform, LabelCentre + LabelWidth * 0.5f, 0f, LabelWidth * 2f, RowHeight);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonTogglePath);
        if (prefab == null)
        {
            Debug.LogError($"[Settings] {ButtonTogglePath} is missing; the {label} toggle has no button.");
            return null;
        }

        // An INSTANCE, not a copy: the switch is one asset, so restyling it restyles every row, and a
        // rebuilt panel picks up whatever it looks like today rather than reverting it to whatever this
        // code once said it looked like.
        var clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rowRect);
        clone.name = "Toggle";
        clone.layer = rowGo.layer;

        // Only placement is set here. Size, scale, sprites and whether the caption shows all belong to
        // the prefab -- overriding them per instance is how the two rows would drift apart again.
        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition = new Vector2(ToggleCentre, 0f);

        var toggle = clone.GetComponent<ToggleButton>();
        if (toggle != null) toggle.SetIsOn(false, notify: false);

        return toggle;
    }

    /// <summary>
    /// The Display heading, the fullscreen switch and the resolution row, under one parent so the
    /// controller can hide the section as a whole on the platforms where none of it means anything.
    /// <paramref name="y"/> is where the heading goes; the rows hang below it at the panel's spacing.
    ///
    /// Fullscreen first: it is the coarser choice, and the size means a different thing depending on
    /// which way it is set.
    /// </summary>
    private static void BuildDisplaySection(RectTransform window, TMP_FontAsset font, float y,
        out GameObject section, out ToggleButton fullscreenToggle, out TMP_Dropdown dropdown)
    {
        section = NewUI(DisplaySectionName, window);
        var sectionRect = (RectTransform)section.transform;

        // Sized to nothing and centred on the heading: the children are placed relative to it exactly
        // as they would be relative to the window, so the section is a grouping and not a layout.
        SetRect(sectionRect, 0f, y, 0f, 0f);

        MakeSectionLabel(sectionRect, "DisplayLabel", "Display", font, 0f);

        fullscreenToggle = BuildToggleRow(sectionRect, FullscreenRowName, "Fullscreen", font, -56f);
        dropdown = BuildResolutionRow(sectionRect, font, -112f);
    }

    private static TMP_Dropdown BuildResolutionRow(RectTransform parent, TMP_FontAsset font, float y)
    {
        GameObject rowGo = NewUI(ResolutionRowName, parent);
        var rowRect = (RectTransform)rowGo.transform;
        SetRect(rowRect, 0f, y, ContentWidth, RowHeight);

        TextMeshProUGUI label = MakeText(rowRect, "Label", font, 24f, BodyColor, TextAlignmentOptions.Left);
        label.text = "Resolution";
        SetRect(label.rectTransform, LabelCentre, 0f, LabelWidth, RowHeight);

        TMP_Dropdown dropdown = BuildDropdown(rowRect, font);
        SetRect((RectTransform)dropdown.transform, SliderCentre, 0f, SliderWidth, DropdownHeight);

        return dropdown;
    }

    /// <summary>
    /// A TextMesh Pro dropdown, dressed in the panel's sprites and colours. Made by TMP's own factory
    /// and restyled, for the same reason the slider is: the template is a scroll view, a viewport, a
    /// content rect and a toggle prototype, all of which the component writes to as it opens, and a
    /// hand-built one that is subtly wrong is a list that opens empty.
    ///
    /// Left EMPTY of options: the controller fills it with the monitor's resolutions at runtime.
    /// </summary>
    private static TMP_Dropdown BuildDropdown(RectTransform parent, TMP_FontAsset font)
    {
        Sprite bar = LoadSheetSprite(DropdownBarSpriteName);
        Sprite arrow = LoadSheetSprite(DropdownArrowSpriteName);
        Sprite tick = LoadSheetSprite(DropdownTickSpriteName);

        GameObject go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = "Dropdown";
        go.transform.SetParent(parent, false);
        SetLayerRecursively(go, parent.gameObject.layer);

        var dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();

        // The closed control: the bar sprite, the current value in the panel's highlight colour, and a
        // chevron turned to point down.
        var background = go.GetComponent<Image>();
        background.sprite = bar;
        background.type = Image.Type.Sliced;
        background.color = Color.white;

        StyleDropdownText(dropdown.captionText, font, HighlightColor);
        var caption = dropdown.captionText.rectTransform;
        caption.offsetMin = new Vector2(18f, 4f);
        caption.offsetMax = new Vector2(-44f, -4f);

        Transform arrowTransform = go.transform.Find("Arrow");
        if (arrowTransform != null)
        {
            var arrowImage = arrowTransform.GetComponent<Image>();
            arrowImage.sprite = arrow;
            arrowImage.color = BodyColor;

            var arrowRect = (RectTransform)arrowTransform;
            arrowRect.sizeDelta = new Vector2(22f, 22f);
            arrowRect.anchoredPosition = new Vector2(-24f, 0f);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        // The open list: a dark sheet, rows the height of the panel's own rows, five of them before it
        // scrolls, and a highlight bar that only shows under the pointer.
        RectTransform template = dropdown.template;
        if (template != null)
        {
            template.sizeDelta = new Vector2(template.sizeDelta.x, DropdownItemHeight * DropdownVisibleItems);

            var templateImage = template.GetComponent<Image>();
            if (templateImage != null)
            {
                templateImage.sprite = null;
                templateImage.color = DropdownListColor;
            }

            // The list scrolls by its bar and the wheel only. See MakeScrollbarOnly.
            var scroll = template.GetComponent<ScrollRect>();
            if (scroll != null) StyleDropdownScrollbar(MakeScrollbarOnly(scroll));

            Transform item = template.Find("Viewport/Content/Item");
            if (item != null)
            {
                var itemRect = (RectTransform)item;
                itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, DropdownItemHeight);

                var content = (RectTransform)item.parent;
                content.sizeDelta = new Vector2(content.sizeDelta.x, DropdownItemHeight);

                var toggle = item.GetComponent<Toggle>();
                if (toggle != null)
                {
                    ColorBlock colors = toggle.colors;
                    colors.normalColor = new Color(1f, 1f, 1f, 0f);
                    colors.highlightedColor = Color.white;
                    colors.selectedColor = new Color(1f, 1f, 1f, 0.6f);
                    colors.pressedColor = Color.white;
                    colors.disabledColor = new Color(1f, 1f, 1f, 0f);
                    toggle.colors = colors;
                }
            }

            var itemBackground = template.Find("Viewport/Content/Item/Item Background")?.GetComponent<Image>();
            if (itemBackground != null)
            {
                itemBackground.sprite = null;
                itemBackground.color = new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, 0.22f);
            }

            var itemCheck = template.Find("Viewport/Content/Item/Item Checkmark")?.GetComponent<Image>();
            if (itemCheck != null)
            {
                itemCheck.sprite = tick;
                itemCheck.color = HighlightColor;

                var checkRect = itemCheck.rectTransform;
                checkRect.sizeDelta = new Vector2(14f, 16f);
                checkRect.anchoredPosition = new Vector2(18f, 0f);
            }

            if (dropdown.itemText != null)
            {
                StyleDropdownText(dropdown.itemText, font, BodyColor);
                var itemLabel = dropdown.itemText.rectTransform;
                itemLabel.offsetMin = new Vector2(38f, 2f);
                itemLabel.offsetMax = new Vector2(-14f, -2f);
            }
        }

        return dropdown;
    }

    /// <summary>
    /// Wide enough to be the only way to scroll: with dragging the list turned off, the bar is what
    /// the player has to hit. The drawn bar is this wide; the grab zone reaches
    /// <see cref="DropdownScrollbarGrabPadding"/> further out on each side.
    /// </summary>
    private const float DropdownScrollbarWidth = 30f;

    private const float DropdownScrollbarGrabPadding = 14f;

    /// <summary>
    /// Dresses the list's scrollbar in the panel's colours and makes it easy to hit: the drawn bar
    /// is wide, both the track and the handle answer to a press well outside their edges, and the
    /// handle lights up under the pointer so a near miss is visible before it is a miss. Idempotent,
    /// so it can be re-run over a bar styled by an earlier version of this tool.
    /// </summary>
    private static void StyleDropdownScrollbar(ScrollRect scroll)
    {
        Scrollbar bar = scroll != null ? scroll.verticalScrollbar : null;
        if (bar == null) return;

        var barRect = (RectTransform)bar.transform;
        barRect.sizeDelta = new Vector2(DropdownScrollbarWidth, barRect.sizeDelta.y);

        // NEGATIVE padding GROWS the clickable rect; same inversion the slider's hit area relies on.
        var grab = new Vector4(-DropdownScrollbarGrabPadding, -6f, -DropdownScrollbarGrabPadding, -6f);

        var track = bar.GetComponent<Image>();
        if (track != null)
        {
            track.sprite = null;
            track.color = SliderTrackColor;
            track.raycastTarget = true;
            track.raycastPadding = grab;
        }

        if (bar.targetGraphic is Image handle)
        {
            handle.sprite = null;
            handle.color = SliderHandleColor;
            handle.raycastTarget = true;
            handle.raycastPadding = grab;
        }

        bar.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = bar.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.93f, 0.78f, 1f);
        colors.pressedColor = HighlightColor;
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        bar.colors = colors;

        // Room between the rows and the wider bar, so the last few pixels of a row are not under it.
        scroll.verticalScrollbarSpacing = 6f;
    }

    /// <summary>
    /// Swaps a ScrollRect for a <see cref="ScrollbarOnlyScrollRect"/> carrying the same setup, so a
    /// press that slides on the list is not a scroll. Every reference the old one held -- content,
    /// viewport, the bar -- is carried across; nothing else on the object is touched. Returns the
    /// replacement, or the original if it already was one.
    /// </summary>
    private static ScrollRect MakeScrollbarOnly(ScrollRect original)
    {
        if (original == null || original is ScrollbarOnlyScrollRect) return original;

        GameObject go = original.gameObject;

        RectTransform content = original.content;
        RectTransform viewport = original.viewport;
        bool horizontal = original.horizontal;
        bool vertical = original.vertical;
        ScrollRect.MovementType movement = original.movementType;
        float elasticity = original.elasticity;
        bool inertia = original.inertia;
        float deceleration = original.decelerationRate;
        float sensitivity = original.scrollSensitivity;
        Scrollbar horizontalBar = original.horizontalScrollbar;
        Scrollbar verticalBar = original.verticalScrollbar;
        ScrollRect.ScrollbarVisibility horizontalVisibility = original.horizontalScrollbarVisibility;
        ScrollRect.ScrollbarVisibility verticalVisibility = original.verticalScrollbarVisibility;
        float horizontalSpacing = original.horizontalScrollbarSpacing;
        float verticalSpacing = original.verticalScrollbarSpacing;

        Object.DestroyImmediate(original);

        var replacement = go.AddComponent<ScrollbarOnlyScrollRect>();
        replacement.content = content;
        replacement.viewport = viewport;
        replacement.horizontal = horizontal;
        replacement.vertical = vertical;
        replacement.movementType = movement;
        replacement.elasticity = elasticity;
        replacement.inertia = inertia;
        replacement.decelerationRate = deceleration;
        replacement.scrollSensitivity = sensitivity;
        replacement.horizontalScrollbar = horizontalBar;
        replacement.verticalScrollbar = verticalBar;
        replacement.horizontalScrollbarVisibility = horizontalVisibility;
        replacement.verticalScrollbarVisibility = verticalVisibility;
        replacement.horizontalScrollbarSpacing = horizontalSpacing;
        replacement.verticalScrollbarSpacing = verticalSpacing;

        return replacement;
    }

    private static void StyleDropdownText(TMP_Text text, TMP_FontAsset font, Color color)
    {
        if (text == null) return;

        if (font != null) text.font = font;
        text.fontSize = 22f;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static Sprite LoadSheetSprite(string spriteName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PanelSpriteSheetPath))
        {
            if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
        }

        Debug.LogWarning($"[Settings] {spriteName} not found in {PanelSpriteSheetPath}.");
        return null;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private static Button BuildPanelButton(RectTransform window, string name, string label, Vector2 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonWidePath);
        if (prefab == null)
        {
            Debug.LogError($"[Settings] {ButtonWidePath} is missing; the panel has no {label} button.");
            return null;
        }

        var clone = Object.Instantiate(prefab, window);
        clone.name = name;
        clone.layer = window.gameObject.layer;

        var labels = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            // The wide button carries a second, smaller caption for the menu's "playing with" line, which
            // has nothing to say here.
            if (i == 0) labels[i].text = label;
            else labels[i].gameObject.SetActive(false);
        }

        var rect = (RectTransform)clone.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one * 0.8f;

        return clone.GetComponent<Button>();
    }

    // ---------------------------------------------------------------------------------------------
    // Small helpers, matching EscMenuRulesBuilder's so the two tools read the same way
    // ---------------------------------------------------------------------------------------------

    private static Transform FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go.transform;

        return null;
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            T found = go.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

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
        text.textWrappingMode = TextWrappingModes.NoWrap;
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
