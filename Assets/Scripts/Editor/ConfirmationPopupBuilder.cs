using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the generic <see cref="ConfirmationPopup"/> prefab into Resources, styled like the rest of the
/// UI: the pause menu's frame sprite, the shared Button_Small prefab (nested, so restyling that prefab
/// restyles these buttons) and the SitkaSmall font. Rerun it to restyle; hand edits to the popup prefab
/// are overwritten.
///
/// The root carries its own Canvas, so it is built at the root of a preview scene, NOT under another
/// canvas: a nested Canvas is saved as World Space and loses its sorting order.
/// </summary>
public static class ConfirmationPopupBuilder
{
    const string PrefabPath = "Assets/Resources/" + ConfirmationPopup.ResourcePath + ".prefab";
    const string SpriteSheet = "Assets/Sprites/UIElements/panel_01.png";
    const string ButtonPrefabPath = "Assets/Prefabs/UI/Button_Small.prefab";

    [MenuItem("Tools/UI/Build Confirmation Popup")]
    public static void Build()
    {
        var frameSprite = FindSprite("panel_01_12");
        var buttonPrefab = AssetDatabase.LoadAssetAtPath<Button>(ButtonPrefabPath);
        var font = FindFont("SitkaSmall SDF Outline");
        if (frameSprite == null || buttonPrefab == null || font == null)
        {
            Debug.LogError("ConfirmationPopupBuilder: missing sprite, Button_Small prefab or font; nothing built.");
            return;
        }

        // In a preview scene, so building never dirties whichever scene is open.
        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("ConfirmationPopup", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
        try
        {

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(900f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var group = root.AddComponent<CanvasGroup>();
            var popup = root.AddComponent<ConfirmationPopup>();

            // Dim backdrop: swallows clicks to everything underneath, and cancels when clicked.
            var backdrop = NewUI("Backdrop", root.transform);
            Stretch(backdrop);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.7f);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };

            var frame = NewUI("Frame", root.transform);
            frame.sizeDelta = new Vector2(600f, 320f);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.pixelsPerUnitMultiplier = 1f;

            var title = NewText("Title", frame, font, 40f, Color.white);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -58f);
            title.rectTransform.sizeDelta = new Vector2(540f, 60f);
            title.text = "Are you sure?";

            var message = NewText("Message", frame, font, 24f, new Color(0.85f, 0.85f, 0.85f, 1f));
            message.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            message.rectTransform.sizeDelta = new Vector2(520f, 120f);
            message.textWrappingMode = TextWrappingModes.Normal;
            message.text = "Message";

            var confirm = NewButton("ConfirmButton", frame, buttonPrefab, preview, font, new Vector2(-130f, 62f), out var confirmLabel);
            var cancel = NewButton("CancelButton", frame, buttonPrefab, preview, font, new Vector2(130f, 62f), out var cancelLabel);
            confirmLabel.text = "Confirm";
            cancelLabel.text = "Cancel";

            var so = new SerializedObject(popup);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.FindProperty("cancelLabel").objectReferenceValue = cancelLabel;
            so.FindProperty("backdropButton").objectReferenceValue = backdropButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            Debug.Log(saved ? $"ConfirmationPopupBuilder: built {PrefabPath}" : $"ConfirmationPopupBuilder: failed to save {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        var rt = NewUI(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// A nested Button_Small instance turned into a text button: full scale, sized for a label, its icon
    /// hidden and its text shown in the popup's font. Its sprite swap and sprite stay the prefab's.
    /// </summary>
    static Button NewButton(string name, Transform parent, Button prefab, UnityEngine.SceneManagement.Scene scene,
        TMP_FontAsset font, Vector2 position, out TextMeshProUGUI label)
    {
        var button = (Button)PrefabUtility.InstantiatePrefab(prefab, scene);
        button.name = name;
        var rt = (RectTransform)button.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(220f, 56f);

        var icon = rt.Find("Image");
        if (icon != null) icon.gameObject.SetActive(false);

        label = rt.GetComponentInChildren<TextMeshProUGUI>(true);
        label.gameObject.SetActive(true);
        label.font = font;
        label.fontSize = 24f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(6f, 5f);
        label.rectTransform.offsetMax = new Vector2(-6f, 5f);
        return button;
    }

    static Sprite FindSprite(string name)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SpriteSheet))
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        return null;
    }

    static TMP_FontAsset FindFont(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset " + name))
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font != null && font.name == name) return font;
        }
        return null;
    }
}
