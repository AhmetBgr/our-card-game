using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scaffolds the game-modifier feature for the Custom Game screen: the modifier and mode assets, the
/// popup prefab and its two row prefabs, the passive-picker prefab and chip, and their placement in
/// <c>CreateCustomGame.unity</c> (a Modifiers button beside Back/Proceed, the popup under the Canvas,
/// one passive picker under each side's hero carousel), wired into CustomGameSetupController.
///
/// Same arrangement as <see cref="SettingsPanelBuilder"/>: <b>Tools ▸ Modifiers ▸ Install Custom Game
/// Modifiers</b> is safe to re-run -- it only creates what is missing and leaves everything already
/// there (hand-edits included) alone. Once built, the prefabs and assets are the source of truth; edit
/// them in the editor like anything else.
/// </summary>
public static class ModifiersPanelBuilder
{
    private const string AssetFolder = "Assets/Resources/Modifiers";
    private const string ModeAssetPath = AssetFolder + "/CustomGameMode.asset";

    private const string PopupPrefabPath = "Assets/Prefabs/UI/ModifiersPanel.prefab";
    private const string ToggleRowPrefabPath = "Assets/Prefabs/UI/ModifierToggleRow.prefab";
    private const string SliderRowPrefabPath = "Assets/Prefabs/UI/ModifierSliderRow.prefab";
    private const string PassivePanelPrefabPath = "Assets/Prefabs/UI/PassiveSelectionPanel.prefab";
    private const string PassiveChipPrefabPath = "Assets/Prefabs/UI/PassiveChip.prefab";

    private const string ScenePath = "Assets/Scenes/CreateCustomGame.unity";

    private const string ButtonWidePath = "Assets/Prefabs/UI/Button_Wide.prefab";
    private const string ButtonTogglePath = "Assets/Prefabs/UI/Button_SettingsToggle.prefab";
    private const string SliderPath = "Assets/Prefabs/UI/Slider_Settings.prefab";

    private const string PanelSpriteSheetPath = "Assets/Sprites/UIElements/panel_01.png";
    private const string FrameSpriteName = "panel_01_12";

    private const string TitleFontPath = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF Typface.asset";
    private const string BodyFontPath = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF.asset";

    private const string PopupName = "ModifiersPanel";
    private const string ModifiersButtonName = "ModifiersButton";
    private const string PassivePanelName = "PassiveSelectionPanel";

    // Laid out for the setup scene's 900 x 900 canvas, which with Match 0.5 is 900 * sqrt(aspect) wide
    // and 900 / sqrt(aspect) tall -- so a landscape screen only ever gives LESS height than 900 (675 at
    // 16:9) and more width. Hence two short columns rather than one tall one.
    private const float WindowWidth = 820f;
    private const float ContentWidth = 740f;

    /// <summary>Rows per column assumed when the mode asset cannot be read, so the window still has a size.</summary>
    private const int FallbackRowsPerColumn = 4;

    /// <summary>How many columns rows are dealt into, side by side under the title.</summary>
    private const int ColumnCount = 2;

    private const float ColumnGap = 24f;
    private const float ColumnWidth = (ContentWidth - ColumnGap * (ColumnCount - 1)) / ColumnCount;

    private const float RowHeight = 48f;
    private const float RowSpacing = 6f;

    /// <summary>Where the columns start and stop, measured in from the window's top and bottom edges.</summary>
    private const float RowsTop = 116f;
    private const float RowsBottom = 120f;

    // Where each part of a row sits, measured from the middle of the row -- so within half a column's
    // width, not half the window's. A row is inset by RowPadding at both ends.
    private const float RowPadding = 10f;
    private const float RowLeft = -ColumnWidth * 0.5f + RowPadding;   // -169
    private const float RowRight = ColumnWidth * 0.5f - RowPadding;   //  169

    // A slider row: label, bar, value, left to right. The label auto-sizes down rather than spilling
    // over the bar, since a column is too narrow for the longest modifier names at full size.
    private const float LabelWidth = 160f;
    private const float LabelCentre = RowLeft + LabelWidth * 0.5f;
    private const float SliderWidth = 112f;
    private const float SliderCentre = RowLeft + LabelWidth + 6f + SliderWidth * 0.5f;
    private const float ValueWidth = 50f;
    private const float ValueCentre = RowRight - ValueWidth * 0.5f;

    // A toggle row has no value caption of its own (the switch carries one), so its label takes the
    // slider's space as well.
    private const float ToggleWidth = 60f;                            // 100 at the switch's 0.6 scale
    private const float ToggleCentre = RowRight - ToggleWidth * 0.5f;
    private const float ToggleLabelWidth = ToggleCentre - ToggleWidth * 0.5f - 8f - RowLeft;
    private const float ToggleLabelCentre = RowLeft + ToggleLabelWidth * 0.5f;

    private const float ChipSize = 40f;

    /// <summary>Space between the Back button and the Modifiers button, in canvas units.</summary>
    private const float ModifiersButtonGap = 6f;

    private static readonly Color TitleColor = new Color(0.851f, 0.851f, 0.851f, 1f);
    private static readonly Color HeadColor = new Color(1f, 0.886f, 0.639f, 1f);
    private static readonly Color BodyColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    private static readonly Color HighlightColor = new Color(1f, 0.788f, 0.353f, 1f);
    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Color ChipBackColor = new Color(0f, 0f, 0f, 0.55f);

    [MenuItem("Tools/Modifiers/Install Custom Game Modifiers")]
    public static void InstallAll()
    {
        GameModeSO mode = LoadOrCreateAssets();
        if (mode == null) return;

        GameObject popup = LoadOrBuildPopupPrefab();
        GameObject passivePanel = LoadOrBuildPassivePanelPrefab();
        if (popup == null || passivePanel == null) return;

        AddUpgradedFilterToDeckPanel();
        InstallIntoScene(mode, popup, passivePanel);
    }

    // ---------------------------------------------------------------------------------------------
    // Upgraded-only filter on the deck panel's card grid
    // ---------------------------------------------------------------------------------------------

    private const string DeckPanelPrefabPath = "Assets/Prefabs/UI/DeckPanel.prefab";
    private const string UpgradedFilterName = "UpgradedFilter";

    // Top-right corner of the all-cards frame, in the deck panel's units.
    private static readonly Vector2 UpgradedFilterPosition = new Vector2(372f, 206f);

    /// <summary>
    /// Adds the "Upgraded only" switch to DeckPanel.prefab (both sides are instances of it) and wires it
    /// into AllCardsUIController, which shows it only while upgraded cards are allowed. Skipped when
    /// the prefab already has one.
    /// </summary>
    [MenuItem("Tools/Modifiers/Add Upgraded Filter To Deck Panel")]
    public static void AddUpgradedFilterToDeckPanel()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DeckPanelPrefabPath) == null)
        {
            Debug.LogError($"[Modifiers] {DeckPanelPrefabPath} does not exist.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(DeckPanelPrefabPath);
        try
        {
            var grid = root.GetComponentInChildren<AllCardsUIController>(true);
            if (grid == null)
            {
                Debug.LogError($"[Modifiers] {DeckPanelPrefabPath} has no AllCardsUIController.");
                return;
            }

            if (root.transform.Find(UpgradedFilterName) != null)
            {
                Debug.Log($"[Modifiers] {DeckPanelPrefabPath} already has the upgraded filter; left untouched.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonTogglePath);
            if (prefab == null)
            {
                Debug.LogError($"[Modifiers] {ButtonTogglePath} is missing; no upgraded filter switch.");
                return;
            }

            GameObject filterGo = NewUI(UpgradedFilterName, root.transform);
            var filter = (RectTransform)filterGo.transform;
            SetRect(filter, UpgradedFilterPosition.x, UpgradedFilterPosition.y, 160f, 36f);

            TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            TextMeshProUGUI label = MakeText(filter, "Label", bodyFont, 15f, BodyColor, TextAlignmentOptions.Right);
            label.text = "Upgraded only";
            SetRect(label.rectTransform, -30f, 0f, 100f, 36f);

            var clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, filter);
            clone.name = "Toggle";
            clone.layer = filterGo.layer;

            var rect = (RectTransform)clone.transform;
            rect.anchoredPosition = new Vector2(48f, 0f);
            // The settings switch is authored at 0.6; the grid's chrome is smaller than the settings rows.
            rect.localScale = Vector3.one * 0.4f;

            var toggle = clone.GetComponent<ToggleButton>();
            if (toggle != null) toggle.SetIsOn(false, notify: false);

            var hit = filterGo.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            filterGo.AddComponent<UITooltipTrigger>().Message = "Show only upgraded cards in the list.";

            // Off until the controller finds upgraded cards allowed.
            filterGo.SetActive(false);

            var serialized = new SerializedObject(grid);
            serialized.FindProperty("upgradedFilterRoot").objectReferenceValue = filterGo;
            serialized.FindProperty("upgradedOnlyToggle").objectReferenceValue = toggle;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DeckPanelPrefabPath);
            Debug.Log($"[Modifiers] added the upgraded filter to {DeckPanelPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Assets
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Custom Game mode and its modifiers. Existing assets are kept as they are.</summary>
    [MenuItem("Tools/Modifiers/Create Missing Modifier Assets")]
    public static GameModeSO LoadOrCreateAssets()
    {
        EnsureFolder(AssetFolder);

        var playerHealth = LoadOrCreate<HeroHealthMultiplierModifierSO>("PlayerHeroHealth", m =>
        {
            m.id = "player_hero_health";
            m.displayName = "Player Hero Health";
            m.description = "Multiplies your hero's starting and maximum health.";
            m.side = SelectionSide.Player;
            m.min = 0.1f; m.max = 3f; m.step = 0.1f; m.defaultValue = 1f;
            m.format = "x{0:0.0}";
        });

        var opponentHealth = LoadOrCreate<HeroHealthMultiplierModifierSO>("OpponentHeroHealth", m =>
        {
            m.id = "opponent_hero_health";
            m.displayName = "Opponent Hero Health";
            m.description = "Multiplies the opponent hero's starting and maximum health.";
            m.side = SelectionSide.Opponent;
            m.min = 0.1f; m.max = 3f; m.step = 0.1f; m.defaultValue = 1f;
            m.format = "x{0:0.0}";
        });

        var duplicates = LoadOrCreate<DeckRuleModifierSO>("AllowDuplicateCards", m =>
        {
            m.id = "deck_allow_duplicates";
            m.displayName = "Allow Duplicate Cards";
            m.description = "A deck may hold more than one copy of the same card.";
            m.rule = DeckRuleModifierSO.Rule.AllowDuplicates;
        });

        var upgraded = LoadOrCreate<DeckRuleModifierSO>("AllowUpgradedCards", m =>
        {
            m.id = "deck_allow_upgraded";
            m.displayName = "Allow Upgraded Cards";
            m.description = "Upgraded cards can be put straight into a deck.";
            m.rule = DeckRuleModifierSO.Rule.AllowUpgraded;
        });

        var oversize = LoadOrCreate<DeckRuleModifierSO>("AllowLargerDecks", m =>
        {
            m.id = "deck_allow_oversize";
            m.displayName = "Allow More Than 10 Cards";
            m.description = "Decks may hold up to 30 cards.";
            m.rule = DeckRuleModifierSO.Rule.AllowOversize;
            m.sizeBound = 30;
        });

        var undersize = LoadOrCreate<DeckRuleModifierSO>("AllowSmallerDecks", m =>
        {
            m.id = "deck_allow_undersize";
            m.displayName = "Allow Fewer Than 10 Cards";
            m.description = "Decks may hold as few as 1 card.";
            m.rule = DeckRuleModifierSO.Rule.AllowUndersize;
            m.sizeBound = 1;
        });

        var passives = LoadOrCreate<MultiplePassivesModifierSO>("MultiplePassives", m =>
        {
            m.id = "multiple_passives";
            m.displayName = "Allow Multiple Passives";
            m.description = "Pick extra hero passives under the hero selection. Each hero keeps its own passive.";
        });

        var attack = LoadOrCreate<HeroAttackMultiplierModifierSO>("HeroAttackMultiplier", m =>
        {
            m.id = "hero_attack_multiplier";
            m.displayName = "Hero Attack Multiplier";
            m.description = "Multiplies both heroes' attack damage.";
            m.min = 1; m.max = 5; m.defaultValue = 1;
            m.format = "x{0}";
        });

        var mode = AssetDatabase.LoadAssetAtPath<GameModeSO>(ModeAssetPath);
        if (mode == null)
        {
            mode = ScriptableObject.CreateInstance<GameModeSO>();
            mode.modeId = "custom";
            mode.displayName = "Custom Game";
            mode.baseDeckRules = DeckRules.Standard(10);
            mode.modifiers = new List<GameModifierSO>
            {
                playerHealth, opponentHealth, attack, duplicates, upgraded, oversize, undersize, passives
            };
            AssetDatabase.CreateAsset(mode, ModeAssetPath);
            Debug.Log($"[Modifiers] created {ModeAssetPath}");
        }

        AssetDatabase.SaveAssets();
        return mode;
    }

    private static T LoadOrCreate<T>(string name, System.Action<T> configure) where T : GameModifierSO
    {
        string path = $"{AssetFolder}/{name}.asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        T created = ScriptableObject.CreateInstance<T>();
        configure(created);
        AssetDatabase.CreateAsset(created, path);
        Debug.Log($"[Modifiers] created {path}");
        return created;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ---------------------------------------------------------------------------------------------
    // Popup prefab
    // ---------------------------------------------------------------------------------------------

    private static GameObject LoadOrBuildPopupPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPrefabPath);
        return prefab != null ? prefab : BuildPopupPrefab();
    }

    /// <summary>Rebuilds the popup and its row prefabs from scratch, THROWING AWAY hand-edits to them.</summary>
    [MenuItem("Tools/Modifiers/Rebuild Modifiers Popup Prefabs (discards hand-edits)")]
    public static GameObject BuildPopupPrefab()
    {
        GameObject toggleRow = BuildToggleRowPrefab();
        GameObject sliderRow = BuildSliderRowPrefab();

        // Built under a temporary canvas: a RectTransform root saved from outside one loses its UI
        // components on import (Image turns into SpriteRenderer, TMP children drop).
        GameObject canvasGo = TempCanvas();
        try
        {
            GameObject rootGo = NewUI(PopupName, canvasGo.transform);
            Stretch((RectTransform)rootGo.transform);
            var controller = rootGo.AddComponent<ModifiersPopupController>();

            Image backdrop = MakeImage(rootGo.transform, "Backdrop", BackdropColor);
            backdrop.raycastTarget = true;
            Stretch(backdrop.rectTransform);

            // Only as tall as the columns need: the mode's modifiers split between them, rounded up. A
            // mode with more modifiers gets a taller window the next time this runs.
            int rowsPerColumn = RowsPerColumn();
            float rowsHeight = rowsPerColumn * RowHeight + (rowsPerColumn - 1) * RowSpacing;
            float windowHeight = RowsTop + rowsHeight + RowsBottom;

            GameObject windowGo = NewUI("Window", rootGo.transform);
            var window = (RectTransform)windowGo.transform;
            SetRect(window, 0f, 0f, WindowWidth, windowHeight);

            BuildFrame(window, WindowWidth, windowHeight);

            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

            float top = windowHeight * 0.5f;

            TextMeshProUGUI title = MakeText(window, "Title", titleFont, 36f, TitleColor, TextAlignmentOptions.Center);
            title.text = "Modifiers";
            title.fontStyle = FontStyles.Bold;
            SetRect(title.rectTransform, 0f, top - 62f, ContentWidth, 56f);

            Image divider = MakeImage(window, "Divider", new Color(1f, 1f, 1f, 0.16f));
            SetRect(divider.rectTransform, 0f, top - 98f, ContentWidth, 3f);

            // Rows are instantiated at runtime from the mode's list; this only gives them their columns.
            // The band holds the columns side by side, and each column stacks its own rows.
            GameObject rowsGo = NewUI("Rows", window);
            var rows = (RectTransform)rowsGo.transform;
            rows.anchorMin = rows.anchorMax = new Vector2(0.5f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.anchoredPosition = new Vector2(0f, -RowsTop);
            rows.sizeDelta = new Vector2(ContentWidth, rowsHeight);

            var band = rowsGo.AddComponent<HorizontalLayoutGroup>();
            band.spacing = ColumnGap;
            band.childAlignment = TextAnchor.UpperCenter;
            band.childControlWidth = false;
            band.childControlHeight = false;
            band.childForceExpandWidth = false;
            band.childForceExpandHeight = false;

            var columns = new RectTransform[ColumnCount];
            for (int i = 0; i < ColumnCount; i++)
                columns[i] = BuildColumn(rows, $"Column{i + 1}", rowsHeight);

            float buttonY = -windowHeight * 0.5f + 64f;
            Button resetButton = BuildPanelButton(window, "ResetButton", "Defaults", new Vector2(-120f, buttonY));
            Button closeButton = BuildPanelButton(window, "CloseButton", "Close", new Vector2(120f, buttonY));

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("panel").objectReferenceValue = rootGo;
            SerializedProperty columnsProperty = serialized.FindProperty("columns");
            columnsProperty.arraySize = columns.Length;
            for (int i = 0; i < columns.Length; i++)
                columnsProperty.GetArrayElementAtIndex(i).objectReferenceValue = columns[i];

            // Left wired as well, so a panel whose columns are cleared by hand still shows its rows.
            serialized.FindProperty("rowsContainer").objectReferenceValue = rows;
            serialized.FindProperty("toggleRowPrefab").objectReferenceValue = toggleRow.GetComponent<ModifierToggleRowView>();
            serialized.FindProperty("sliderRowPrefab").objectReferenceValue = sliderRow.GetComponent<ModifierSliderRowView>();
            serialized.FindProperty("titleLabel").objectReferenceValue = title;
            serialized.FindProperty("resetButton").objectReferenceValue = resetButton;
            serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PopupPrefabPath);
            Debug.Log($"[Modifiers] built {PopupPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(canvasGo);
        }
    }

    /// <summary>
    /// How many rows the tallest column will hold, read from the mode asset the popup is built for.
    /// </summary>
    private static int RowsPerColumn()
    {
        var mode = AssetDatabase.LoadAssetAtPath<GameModeSO>(ModeAssetPath);
        if (mode == null || mode.modifiers == null) return FallbackRowsPerColumn;

        int count = 0;
        foreach (GameModifierSO modifier in mode.modifiers)
            if (modifier != null) count++;

        return Mathf.Max(1, Mathf.CeilToInt(count / (float)ColumnCount));
    }

    /// <summary>One column of the popup: a fixed-width stack the rows are dealt into at runtime.</summary>
    private static RectTransform BuildColumn(RectTransform band, string name, float height)
    {
        GameObject go = NewUI(name, band);
        var column = (RectTransform)go.transform;
        column.anchorMin = column.anchorMax = new Vector2(0.5f, 1f);
        column.pivot = new Vector2(0.5f, 1f);
        column.sizeDelta = new Vector2(ColumnWidth, height);

        // The band reads these rather than the rect: with childControl* off it asks the children how
        // much space they want, and a bare RectTransform answers zero and lands on its neighbour.
        var element = go.AddComponent<LayoutElement>();
        element.preferredWidth = ColumnWidth;
        element.preferredHeight = height;

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RowSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return column;
    }

    private static GameObject BuildToggleRowPrefab()
    {
        GameObject canvasGo = TempCanvas();
        try
        {
            RectTransform row = BuildRowShell("ModifierToggleRow", canvasGo.transform, ToggleLabelCentre, ToggleLabelWidth,
                out TextMeshProUGUI label, out UITooltipTrigger tooltip);
            var view = row.gameObject.AddComponent<ModifierToggleRowView>();

            GameObject togglePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonTogglePath);
            if (togglePrefab == null)
            {
                Debug.LogError($"[Modifiers] {ButtonTogglePath} is missing; the toggle row has no switch.");
                return null;
            }

            // An instance, as in the settings panel, so restyling the switch restyles every row.
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(togglePrefab, row);
            clone.name = "Toggle";
            clone.layer = row.gameObject.layer;
            ((RectTransform)clone.transform).anchoredPosition = new Vector2(ToggleCentre, 0f);

            var toggle = clone.GetComponent<ToggleButton>();
            if (toggle != null) toggle.SetIsOn(false, notify: false);

            // The switch's own caption says On/Off, as it does in the settings panel.
            TMP_Text caption = toggle != null ? toggle.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            var serialized = new SerializedObject(view);
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.FindProperty("valueLabel").objectReferenceValue = caption;
            serialized.FindProperty("tooltip").objectReferenceValue = tooltip;
            serialized.FindProperty("toggle").objectReferenceValue = toggle;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, ToggleRowPrefabPath);
            Debug.Log($"[Modifiers] built {ToggleRowPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(canvasGo);
        }
    }

    private static GameObject BuildSliderRowPrefab()
    {
        GameObject canvasGo = TempCanvas();
        try
        {
            RectTransform row = BuildRowShell("ModifierSliderRow", canvasGo.transform, LabelCentre, LabelWidth,
                out TextMeshProUGUI label, out UITooltipTrigger tooltip);
            var view = row.gameObject.AddComponent<ModifierSliderRowView>();

            TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            TextMeshProUGUI value = MakeText(row, "Value", bodyFont, 20f, HighlightColor, TextAlignmentOptions.Right);
            value.text = "x1.0";
            SetRect(value.rectTransform, ValueCentre, 0f, ValueWidth, RowHeight);

            GameObject sliderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SliderPath);
            if (sliderPrefab == null)
            {
                Debug.LogError($"[Modifiers] {SliderPath} is missing; the slider row has no bar.");
                return null;
            }

            // A COPY rather than an instance: the settings slider carries a VolumeSlider that would bind
            // this bar to an audio bus, and a component cannot be removed from a prefab instance.
            GameObject sliderGo = Object.Instantiate(sliderPrefab, row);
            sliderGo.name = "Slider";
            sliderGo.layer = row.gameObject.layer;
            var volume = sliderGo.GetComponent<VolumeSlider>();
            if (volume != null) Object.DestroyImmediate(volume);

            var sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.anchorMin = sliderRect.anchorMax = sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(SliderCentre, 0f);
            sliderRect.sizeDelta = new Vector2(SliderWidth, sliderRect.sizeDelta.y);

            var serialized = new SerializedObject(view);
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.FindProperty("valueLabel").objectReferenceValue = value;
            serialized.FindProperty("tooltip").objectReferenceValue = tooltip;
            serialized.FindProperty("slider").objectReferenceValue = sliderGo.GetComponent<Slider>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, SliderRowPrefabPath);
            Debug.Log($"[Modifiers] built {SliderRowPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(canvasGo);
        }
    }

    /// <summary>
    /// The part of a row both kinds share: its rect, the label and the hover tooltip. A row is one
    /// column wide, and <paramref name="labelWidth"/> is whatever that kind leaves over for the name.
    /// </summary>
    private static RectTransform BuildRowShell(string name, Transform parent, float labelCentre, float labelWidth,
        out TextMeshProUGUI label, out UITooltipTrigger tooltip)
    {
        GameObject rowGo = NewUI(name, parent);
        var row = (RectTransform)rowGo.transform;
        SetRect(row, 0f, 0f, ColumnWidth, RowHeight);

        var element = rowGo.AddComponent<LayoutElement>();
        element.preferredWidth = ColumnWidth;
        element.preferredHeight = RowHeight;

        // An invisible raycast target, so hovering anywhere on the row raises the description.
        var hit = rowGo.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;

        tooltip = rowGo.AddComponent<UITooltipTrigger>();

        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        label = MakeText(row, "Label", bodyFont, 21f, BodyColor, TextAlignmentOptions.Left);
        label.text = "Modifier";

        // A column is narrower than the longest modifier names set at 21, so they shrink to fit rather
        // than running under the control beside them. Wrapping is off, so this only ever gives up size.
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 21f;

        SetRect(label.rectTransform, labelCentre, 0f, labelWidth, RowHeight);

        return row;
    }

    // ---------------------------------------------------------------------------------------------
    // Passive picker prefab
    // ---------------------------------------------------------------------------------------------

    private static GameObject LoadOrBuildPassivePanelPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PassivePanelPrefabPath);
        return prefab != null ? prefab : BuildPassivePanelPrefab();
    }

    /// <summary>Rebuilds the passive picker and its chip from scratch, THROWING AWAY hand-edits to them.</summary>
    [MenuItem("Tools/Modifiers/Rebuild Passive Picker Prefabs (discards hand-edits)")]
    public static GameObject BuildPassivePanelPrefab()
    {
        GameObject chip = BuildPassiveChipPrefab();
        if (chip == null) return null;

        GameObject canvasGo = TempCanvas();
        try
        {
            GameObject rootGo = NewUI(PassivePanelName, canvasGo.transform);
            SetRect((RectTransform)rootGo.transform, 0f, 0f, 300f, 70f);
            var controller = rootGo.AddComponent<PassiveSelectionController>();

            // The visuals are a CHILD of the controller's object: the picker hides itself while the
            // modifier is off, and a controller on the object it hides would stop listening for the
            // modifier coming back on.
            GameObject contentGo = NewUI("Content", rootGo.transform);
            var root = (RectTransform)contentGo.transform;
            Stretch(root);

            TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

            TextMeshProUGUI heading = MakeText(root, "Heading", bodyFont, 16f, HeadColor, TextAlignmentOptions.Center);
            heading.text = "Extra Passives";
            heading.fontStyle = FontStyles.Bold;
            SetRect(heading.rectTransform, 0f, 24f, 300f, 20f);

            TextMeshProUGUI count = MakeText(root, "Count", bodyFont, 14f, HighlightColor, TextAlignmentOptions.Right);
            count.text = "";
            SetRect(count.rectTransform, 120f, 24f, 60f, 20f);

            GameObject chipsGo = NewUI("Chips", root);
            var chips = (RectTransform)chipsGo.transform;
            SetRect(chips, 0f, -10f, 300f, ChipSize + 4f);

            var layout = chipsGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("panel").objectReferenceValue = contentGo;
            serialized.FindProperty("chipsContainer").objectReferenceValue = chips;
            serialized.FindProperty("chipPrefab").objectReferenceValue = chip.GetComponent<PassiveChipButton>();
            serialized.FindProperty("countLabel").objectReferenceValue = count;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PassivePanelPrefabPath);
            Debug.Log($"[Modifiers] built {PassivePanelPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(canvasGo);
        }
    }

    private static GameObject BuildPassiveChipPrefab()
    {
        GameObject canvasGo = TempCanvas();
        try
        {
            GameObject rootGo = NewUI("PassiveChip", canvasGo.transform);
            var root = (RectTransform)rootGo.transform;
            SetRect(root, 0f, 0f, ChipSize, ChipSize);

            var element = rootGo.AddComponent<LayoutElement>();
            element.preferredWidth = ChipSize;
            element.preferredHeight = ChipSize;

            var back = rootGo.AddComponent<Image>();
            back.color = ChipBackColor;
            back.raycastTarget = true;

            var tooltip = rootGo.AddComponent<UITooltipTrigger>();
            var chip = rootGo.AddComponent<PassiveChipButton>();

            Image frame = MakeImage(root, "SelectedFrame", HighlightColor);
            frame.sprite = LoadFrameSprite();
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 2f;
            SetRect(frame.rectTransform, 0f, 0f, ChipSize / 4f + 1f, ChipSize / 4f + 1f);
            frame.rectTransform.localScale = Vector3.one * 4f;

            Image icon = MakeImage(root, "Icon", Color.white);
            icon.preserveAspect = true;
            SetRect(icon.rectTransform, 0f, 0f, ChipSize - 8f, ChipSize - 8f);

            TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            TextMeshProUGUI locked = MakeText(root, "LockedOverlay", bodyFont, 9f, HighlightColor, TextAlignmentOptions.Center);
            locked.text = "HERO";
            locked.fontStyle = FontStyles.Bold;
            SetRect(locked.rectTransform, 0f, -ChipSize * 0.5f + 5f, ChipSize, 12f);

            var serialized = new SerializedObject(chip);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("selectedFrame").objectReferenceValue = frame.gameObject;
            serialized.FindProperty("lockedOverlay").objectReferenceValue = locked.gameObject;
            serialized.FindProperty("tooltip").objectReferenceValue = tooltip;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PassiveChipPrefabPath);
            Debug.Log($"[Modifiers] built {PassiveChipPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(canvasGo);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Scene
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Drops the popup, the Modifiers button and the two passive pickers into CreateCustomGame and
    /// wires the setup controller. Each part is skipped when something of its name is already there.
    /// </summary>
    private static void InstallIntoScene(GameModeSO mode, GameObject popupPrefab, GameObject passivePanelPrefab)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var controller = FindComponent<CustomGameSetupController>(scene);
        Transform canvas = FindRoot(scene, "Canvas");
        if (controller == null || canvas == null)
        {
            Debug.LogError("[Modifiers] CreateCustomGame has no CustomGameSetupController or Canvas root.");
            return;
        }

        bool changed = false;
        var serialized = new SerializedObject(controller);

        // The popup, over everything else on the canvas.
        Transform popup = canvas.Find(PopupName);
        if (popup == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(popupPrefab, canvas);
            instance.name = PopupName;
            Stretch((RectTransform)instance.transform);
            instance.transform.SetAsLastSibling();
            popup = instance.transform;
            changed = true;
        }
        serialized.FindProperty("modifiersPopup").objectReferenceValue = popup.GetComponent<ModifiersPopupController>();

        // The button, just right of Back in the bottom row (the middle of the row is the match summary).
        Button proceed = serialized.FindProperty("proceedButton").objectReferenceValue as Button;
        Button back = serialized.FindProperty("backButton").objectReferenceValue as Button;
        Transform buttonRow = proceed != null ? proceed.transform.parent : null;
        Transform modifiersButton = buttonRow != null ? buttonRow.Find(ModifiersButtonName) : null;
        if (modifiersButton == null && buttonRow != null && back != null)
        {
            var backRect = (RectTransform)back.transform;
            var rowRect = (RectTransform)buttonRow;

            Button button = BuildPanelButton(rowRect, ModifiersButtonName, "Modifiers", Vector2.zero);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = backRect.anchorMin;
            rect.pivot = backRect.pivot;
            rect.localScale = backRect.localScale;

            float backWidth = backRect.rect.width * backRect.localScale.x;
            rect.anchoredPosition = backRect.anchoredPosition + new Vector2(backWidth + ModifiersButtonGap, 0f);

            modifiersButton = button.transform;
            changed = true;
        }
        if (modifiersButton != null)
            serialized.FindProperty("modifiersButton").objectReferenceValue = modifiersButton.GetComponent<Button>();

        serialized.FindProperty("gameMode").objectReferenceValue = mode;
        if (serialized.ApplyModifiedPropertiesWithoutUndo()) changed = true;

        // The rows and chips describe themselves through UITooltip, which this scene never had.
        if (EnsureTooltipHost(scene, canvas)) changed = true;

        // One passive picker per side, under that side's hero carousel.
        foreach (var context in Object.FindObjectsByType<DeckSelectionContext>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (context.gameObject.scene != scene) continue;
            if (context.transform.Find(PassivePanelName) != null) continue;

            var hero = context.GetComponentInChildren<HeroSelectionController>(true);
            var heroRect = hero != null ? (RectTransform)hero.transform : null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(passivePanelPrefab, context.transform);
            instance.name = PassivePanelName;

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            // The carousel sits at x -293 inside its panel, with its name label 68 below the panel's
            // centre; the picker hangs under that label.
            Vector2 panelPos = heroRect != null ? heroRect.anchoredPosition : Vector2.zero;
            rect.anchoredPosition = new Vector2(panelPos.x - 293f, panelPos.y - 68f - 25f - 45f);
            rect.localScale = Vector3.one;
            changed = true;
        }

        if (!changed)
        {
            Debug.Log("[Modifiers] CreateCustomGame already has everything; left untouched.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Modifiers] installed the modifiers popup, button and passive pickers into CreateCustomGame.");
    }

    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string TooltipName = "Tooltip";

    /// <summary>
    /// Gives the scene a UITooltip panel if it has none, copied from the main menu's so the two read
    /// the same. Returns true when one was added.
    /// </summary>
    private static bool EnsureTooltipHost(Scene scene, Transform canvas)
    {
        if (FindComponent<UITooltip>(scene) != null) return false;

        Scene menu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
        try
        {
            UITooltip source = FindComponent<UITooltip>(menu);
            if (source == null)
            {
                Debug.LogWarning($"[Modifiers] {MainMenuScenePath} has no UITooltip to copy; hover descriptions will not show.");
                return false;
            }

            GameObject copy = Object.Instantiate(source.gameObject, canvas);
            copy.name = TooltipName;
            copy.transform.SetAsLastSibling();

            // Authored hidden, like the original: UITooltip.Instance finds inactive objects.
            copy.SetActive(false);
            return true;
        }
        finally
        {
            EditorSceneManager.CloseScene(menu, true);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Small helpers, matching SettingsPanelBuilder's so the two tools read the same way
    // ---------------------------------------------------------------------------------------------

    private static GameObject TempCanvas()
    {
        var go = new GameObject("__ModifiersBuildCanvas", typeof(RectTransform), typeof(Canvas));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private static void BuildFrame(RectTransform window, float width, float height)
    {
        GameObject go = NewUI("Frame", window);
        var image = go.AddComponent<Image>();
        image.sprite = LoadFrameSprite();
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

    private static Sprite LoadFrameSprite()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PanelSpriteSheetPath))
        {
            if (asset is Sprite sprite && sprite.name == FrameSpriteName) return sprite;
        }

        Debug.LogWarning($"[Modifiers] {FrameSpriteName} not found in {PanelSpriteSheetPath}; no frame art.");
        return null;
    }

    private static Button BuildPanelButton(RectTransform parent, string name, string label, Vector2 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonWidePath);
        if (prefab == null)
        {
            Debug.LogError($"[Modifiers] {ButtonWidePath} is missing; no {label} button.");
            return null;
        }

        var clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        clone.name = name;
        clone.layer = parent.gameObject.layer;

        var labels = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            // The wide button carries a second, smaller caption for the menu's "playing with" line.
            if (i == 0) labels[i].text = label;
            else labels[i].gameObject.SetActive(false);
        }

        var rect = (RectTransform)clone.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one * 0.5f;

        return clone.GetComponent<Button>();
    }

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
