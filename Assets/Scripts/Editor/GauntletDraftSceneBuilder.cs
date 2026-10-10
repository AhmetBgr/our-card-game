using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the Forged in Battle Draft scene from scratch and makes sure the mode's config asset exists
/// and the scene is in the build. Re-run it after changing the layout here; the scene is regenerated,
/// so hand edits to it are lost.
/// </summary>
public static class GauntletDraftSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Draft.unity";
    const string ConfigPath = "Assets/Resources/Gauntlet/GauntletConfig.asset";

    const string CardPreviewPrefab = "Assets/Prefabs/UI/CardPreview Variant.prefab";
    const string ButtonPrefab = "Assets/Prefabs/UI/Button_Wide.prefab";
    const string SaveManagerPrefab = "Assets/Prefabs/Managers/SaveManager.prefab";
    const string TransitionPrefab = "Assets/Prefabs/UI/SceneTransitionCanvas.prefab";
    const string AudioManagerPrefab = "Assets/Prefabs/Managers/AudioManager.prefab";
    const string TitleFont = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF Outline.asset";
    const string BodyFont = "Assets/TextMesh Pro/Fonts/SitkaSmall SDF.asset";

    // Width of the deck strip on the right, and the margin around it. The title, the options row and
    // the Fight button centre in the space left of it, whatever the screen's aspect.
    const float DeckPanelWidth = 230f;
    const float Margin = 20f;
    const float ContentRightInset = DeckPanelWidth + Margin * 2f;

    [MenuItem("Tools/Gauntlet/Create Config Asset")]
    public static GauntletConfigSO EnsureConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<GauntletConfigSO>(ConfigPath);
        if (config != null) return config;

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Gauntlet"))
            AssetDatabase.CreateFolder("Assets/Resources", "Gauntlet");

        config = ScriptableObject.CreateInstance<GauntletConfigSO>();
        AssetDatabase.CreateAsset(config, ConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    [MenuItem("Tools/Gauntlet/Build Draft Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureConfig();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFont);
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);

        // Camera, matching the other menu scenes (orthographic, URP).
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Managers present in every scene, so the Draft scene also runs when opened directly.
        InstantiatePrefab(SaveManagerPrefab);
        InstantiatePrefab(AudioManagerPrefab);
        var transition = InstantiatePrefab(TransitionPrefab);
        if (transition != null && transition.TryGetComponent(out Canvas transitionCanvas))
            transitionCanvas.worldCamera = cam;

        // Canvas, same settings as CreateCustomGame.
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(900f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        var bg = NewRect("BG", root);
        Stretch(bg);
        var bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(0.13f, 0.12f, 0.15f, 1f);

        var title = NewText("TitleText", root, titleFont, 44f, TextAlignmentOptions.Center);
        ContentRow(title.rectTransform, 1f, -50f, 60f);

        var subtitle = NewText("SubtitleText", root, bodyFont, 22f, TextAlignmentOptions.Center);
        subtitle.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        ContentRow(subtitle.rectTransform, 1f, -100f, 34f);

        // Deck list, on a translucent strip down the right edge.
        var deckPanel = NewRect("DeckPanel", root);
        deckPanel.anchorMin = new Vector2(1f, 0f);
        deckPanel.anchorMax = new Vector2(1f, 1f);
        deckPanel.pivot = new Vector2(1f, 0.5f);
        deckPanel.sizeDelta = new Vector2(DeckPanelWidth, -Margin * 2f);
        deckPanel.anchoredPosition = new Vector2(-Margin, 0f);
        var deckPanelImage = deckPanel.gameObject.AddComponent<Image>();
        deckPanelImage.color = new Color(0f, 0f, 0f, 0.35f);
        // Run progress and the hero's statline at the top of the strip, the deck list under it.
        var status = NewText("StatusText", deckPanel, bodyFont, 20f, TextAlignmentOptions.TopLeft);
        status.rectTransform.anchorMin = new Vector2(0f, 1f);
        status.rectTransform.anchorMax = new Vector2(1f, 1f);
        status.rectTransform.pivot = new Vector2(0.5f, 1f);
        status.rectTransform.offsetMin = new Vector2(14f, -104f);
        status.rectTransform.offsetMax = new Vector2(-14f, -14f);
        var deckText = NewText("DeckText", deckPanel, bodyFont, 18f, TextAlignmentOptions.TopLeft);
        Stretch(deckText.rectTransform, 14f);
        deckText.rectTransform.offsetMax = new Vector2(-14f, -116f);

        // Options row, centred in the space left of the deck panel.
        var options = NewRect("Options", root);
        ContentRow(options, 0.5f, -10f, 380f);
        var layout = options.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 30f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var primary = InstantiateButton(root, "PrimaryButton", "Fight!", new Vector2(0.5f, 0f), new Vector2((Margin - ContentRightInset) * 0.5f, 70f));
        var menu = InstantiateButton(root, "MenuButton", "Leave Run", new Vector2(0f, 0f), new Vector2(130f, 70f));

        var controllerGo = new GameObject("DraftController", typeof(DraftController));
        var so = new SerializedObject(controllerGo.GetComponent<DraftController>());
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = subtitle;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("deckText").objectReferenceValue = deckText;
        so.FindProperty("optionsContainer").objectReferenceValue = options;
        so.FindProperty("optionPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CardPreviewPrefab);
        so.FindProperty("baseCardScale").floatValue = 0.85f;
        so.FindProperty("primaryButton").objectReferenceValue = primary;
        so.FindProperty("primaryButtonLabel").objectReferenceValue = primary != null ? primary.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        so.FindProperty("menuButton").objectReferenceValue = menu;
        so.FindProperty("menuButtonLabel").objectReferenceValue = menu != null ? menu.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log($"[Gauntlet] Built {ScenePath}");
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static GameObject InstantiatePrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[Gauntlet] Missing prefab {path}");
            return null;
        }
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    static Button InstantiateButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefab);
        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = position;

        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
        return go.GetComponent<Button>();
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>A full-width row of the content area (screen minus the deck strip), at a vertical anchor.</summary>
    static void ContentRow(RectTransform rt, float anchorY, float y, float height)
    {
        rt.anchorMin = new Vector2(0f, anchorY);
        rt.anchorMax = new Vector2(1f, anchorY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(Margin, 0f);
        rt.offsetMax = new Vector2(-ContentRightInset, 0f);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }
}
