using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Spawns floating world-space labels ("Crippled", and anything else shaped like it) and recycles them.
///
/// Deliberately zero-setup: nothing needs to exist in the scene. The first call creates the manager, which
/// builds its own label instances from code and reads its presets from Resources/FloatingTextConfig.asset.
/// Drop a FloatingTextManager into a scene only if you want to override the config or supply a fancier
/// label prefab (one with a background, an icon, etc.).
///
/// Callers pass a style id, never colours or timings — see <see cref="FloatingTextConfig"/>.
/// </summary>
public class FloatingTextManager : MonoBehaviour
{
    [Tooltip("Presets to use. Empty = loaded from Resources/FloatingTextConfig.asset.")]
    [SerializeField] private FloatingTextConfig config;

    [Tooltip("Optional custom label prefab (for a background sprite, an icon, ...). Empty = a plain TextMeshPro label is built in code.")]
    [SerializeField] private FloatingText labelPrefab;

    private static FloatingTextManager _instance;
    private static bool _quitting;

    private readonly Queue<FloatingText> _pool = new Queue<FloatingText>();

    // Statics survive entering play mode when domain reload is disabled ("Enter Play Mode Options"), which
    // would otherwise leave a dead instance — or a stuck _quitting — poisoning the next session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _quitting = false;
    }

    public static FloatingTextManager Instance
    {
        get
        {
            if (_quitting) return null;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<FloatingTextManager>();

                // Auto-bootstrap: a scene that never bothered to host one still gets popups.
                if (_instance == null)
                    _instance = new GameObject(nameof(FloatingTextManager)).AddComponent<FloatingTextManager>();
            }

            return _instance;
        }
    }

    public FloatingTextConfig Config
    {
        get
        {
            if (config == null) config = Resources.Load<FloatingTextConfig>(FloatingTextConfig.ResourcePath);

            // Last resort so a missing asset shows plain white text instead of throwing at a call site
            // that has no business knowing about configs.
            if (config == null) config = ScriptableObject.CreateInstance<FloatingTextConfig>();

            return config;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnApplicationQuit() => _quitting = true;

    // ---------------------------------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------------------------------

    /// <summary>Shows <paramref name="text"/> at a fixed world position using the named preset.</summary>
    public static void Show(Vector3 worldPos, string text, string styleId = null)
    {
        FloatingTextManager manager = Instance;
        if (manager == null) return;

        manager.Spawn(text, manager.Config.GetStyle(styleId), worldPos);
    }

    /// <summary>As <see cref="Show(Vector3,string,string)"/>, but with a style built or tweaked by the caller.</summary>
    public static void ShowWithStyle(Vector3 worldPos, string text, FloatingTextStyle style)
    {
        FloatingTextManager manager = Instance;
        if (manager == null || style == null) return;

        manager.Spawn(text, style, worldPos);
    }

    /// <summary>
    /// Shows <paramref name="text"/> over <paramref name="anchor"/> (a minion, a hero, a cell). The popup
    /// rides the anchor while it rises, so it stays put on a unit that moves mid-animation.
    /// <paramref name="extraOffset"/> is added on top of the style's own spawn offset, for anchors whose
    /// pivot isn't where the label should appear.
    /// </summary>
    public static void ShowOn(Transform anchor, string text, string styleId = null, Vector3 extraOffset = default)
    {
        FloatingTextManager manager = Instance;
        if (manager == null || anchor == null) return;

        manager.SpawnOn(text, manager.Config.GetStyle(styleId), anchor, extraOffset);
    }

    /// <summary>As <see cref="ShowOn(Transform,string,string,Vector3)"/>, but with a caller-supplied style.</summary>
    public static void ShowOnWithStyle(Transform anchor, string text, FloatingTextStyle style, Vector3 extraOffset = default)
    {
        FloatingTextManager manager = Instance;
        if (manager == null || anchor == null || style == null) return;

        manager.SpawnOn(text, style, anchor, extraOffset);
    }

    // ---------------------------------------------------------------------------------------------
    // Spawning / pooling
    // ---------------------------------------------------------------------------------------------

    private void SpawnOn(string text, FloatingTextStyle style, Transform anchor, Vector3 extraOffset)
    {
        // The offset the label keeps while following is the same one it spawned with, so a popup on a
        // moving minion holds its position over the unit rather than sliding down onto its pivot.
        Vector3 offset = style.spawnOffset + extraOffset;

        FloatingText label = Rent();
        label.Play(text, style, anchor.position + offset, anchor, offset, Release);
    }

    private void Spawn(string text, FloatingTextStyle style, Vector3 worldPos)
    {
        FloatingText label = Rent();
        label.Play(text, style, worldPos + style.spawnOffset, null, Vector3.zero, Release);
    }

    private FloatingText Rent()
    {
        while (_pool.Count > 0)
        {
            FloatingText pooled = _pool.Dequeue();
            if (pooled == null) continue; // destroyed out from under the pool (scene reload)

            pooled.gameObject.SetActive(true);
            return pooled;
        }

        return Create();
    }

    private void Release(FloatingText label)
    {
        if (label == null) return;

        label.gameObject.SetActive(false);
        label.transform.SetParent(transform, false);
        _pool.Enqueue(label);
    }

    private FloatingText Create()
    {
        if (labelPrefab != null)
            return Instantiate(labelPrefab, transform);

        // TextMeshPro (the 3D one) needs a RectTransform, so the GameObject is built with one up front —
        // a plain Transform would leave TMP dereferencing null on its first layout.
        var go = new GameObject("FloatingText", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var label = go.AddComponent<TextMeshPro>();
        if (Config.defaultFont != null) label.font = Config.defaultFont;

        return go.AddComponent<FloatingText>();
    }
}
