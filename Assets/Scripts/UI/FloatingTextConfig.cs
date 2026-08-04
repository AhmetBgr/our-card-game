using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The named <see cref="FloatingTextStyle"/> presets the game can show. Lives at
/// Resources/FloatingTextConfig.asset so <see cref="FloatingTextManager"/> can find it with no scene
/// wiring; adding a new kind of popup means adding a style here, not touching code.
/// </summary>
[CreateAssetMenu(fileName = "FloatingTextConfig", menuName = "UI/Floating Text Config")]
public class FloatingTextConfig : ScriptableObject
{
    /// <summary>Resources path the manager loads this from when nothing is assigned in the scene.</summary>
    public const string ResourcePath = "FloatingTextConfig";

    [Tooltip("Used by any style that leaves its own font empty. Empty here too falls back to the TMP project default.")]
    public TMP_FontAsset defaultFont;

    [Tooltip("Used when the caller passes no style id, or an id that isn't in the list below (so a typo shows a plain popup instead of nothing at all).")]
    public FloatingTextStyle defaultStyle = new FloatingTextStyle { id = "default" };

    public List<FloatingTextStyle> styles = new List<FloatingTextStyle>();

    // Built lazily from `styles` and dropped whenever the asset is edited, so tweaking ids in the
    // inspector during play doesn't leave the lookup stale.
    private Dictionary<string, FloatingTextStyle> _byId;

    public FloatingTextStyle GetStyle(string id)
    {
        if (string.IsNullOrEmpty(id)) return defaultStyle;

        if (_byId == null) BuildIndex();

        return _byId.TryGetValue(id, out FloatingTextStyle style) ? style : defaultStyle;
    }

    private void BuildIndex()
    {
        _byId = new Dictionary<string, FloatingTextStyle>(System.StringComparer.OrdinalIgnoreCase);

        if (styles == null) return;

        foreach (FloatingTextStyle style in styles)
        {
            if (style == null || string.IsNullOrEmpty(style.id)) continue;
            _byId[style.id] = style;
        }
    }

    private void OnEnable() => _byId = null;
    private void OnValidate() => _byId = null;
}
