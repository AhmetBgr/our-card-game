using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Keeps the game view locked to a square (1:1) aspect ratio and fills the
// remainder of the screen with black bars: pillarboxed left/right on wide
// (Windows/Web fullscreen) screens, letterboxed top/bottom on tall
// (mobile/portrait) screens. Self-installs at startup, no scene setup needed.
public class AspectRatioLetterbox : PermanentSingleton<AspectRatioLetterbox>
{
    [SerializeField] private float targetAspect = 1f;
    [SerializeField] private Color barColor = Color.black;
    [SerializeField] private int sortingOrder = 32760;

    private Camera targetCamera;
    private RectTransform barTop;
    private RectTransform barBottom;
    private RectTransform barLeft;
    private RectTransform barRight;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        BuildBarCanvas();
        AcquireCamera();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AcquireCamera();
        lastScreenWidth = -1;
    }

    private void AcquireCamera()
    {
        targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        Apply();
    }

    private void Apply()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        float barWidthPx = 0f;
        float barHeightPx = 0f;

        if (scaleHeight < 1f)
        {
            // Screen is taller/narrower than the target square -> bars top & bottom.
            barHeightPx = (1f - scaleHeight) * 0.5f * Screen.height;

            if (targetCamera != null)
            {
                Rect rect = targetCamera.rect;
                rect.width = 1f;
                rect.height = scaleHeight;
                rect.x = 0f;
                rect.y = (1f - scaleHeight) * 0.5f;
                targetCamera.rect = rect;
            }
        }
        else
        {
            // Screen is wider than the target square -> bars left & right.
            float scaleWidth = 1f / scaleHeight;
            barWidthPx = (1f - scaleWidth) * 0.5f * Screen.width;

            if (targetCamera != null)
            {
                Rect rect = targetCamera.rect;
                rect.width = scaleWidth;
                rect.height = 1f;
                rect.x = (1f - scaleWidth) * 0.5f;
                rect.y = 0f;
                targetCamera.rect = rect;
            }
        }

        ApplyBar(barTop, barHeightPx, false);
        ApplyBar(barBottom, barHeightPx, false);
        ApplyBar(barLeft, barWidthPx, true);
        ApplyBar(barRight, barWidthPx, true);
    }

    private static void ApplyBar(RectTransform bar, float size, bool horizontal)
    {
        bool visible = size > 0.5f;
        bar.gameObject.SetActive(visible);
        if (!visible) return;

        Vector2 sizeDelta = bar.sizeDelta;
        if (horizontal) sizeDelta.x = size;
        else sizeDelta.y = size;
        bar.sizeDelta = sizeDelta;
    }

    private void BuildBarCanvas()
    {
        var canvasGO = new GameObject("AspectRatioLetterboxCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        canvasGO.AddComponent<GraphicRaycaster>();

        barTop = CreateBar(canvasGO.transform, "BarTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        barBottom = CreateBar(canvasGO.transform, "BarBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        barLeft = CreateBar(canvasGO.transform, "BarLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        barRight = CreateBar(canvasGO.transform, "BarRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
    }

    private RectTransform CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = barColor;
        image.raycastTarget = true;

        go.SetActive(false);
        return rt;
    }
}
