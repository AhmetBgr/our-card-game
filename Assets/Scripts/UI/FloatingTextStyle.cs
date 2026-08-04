using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// One look-and-motion preset for a floating world-space label — the "Crippled" popup that rises off a
/// minion and fades, and anything else shaped like it. Everything a popup needs is here, so a new kind of
/// message (a different word, colour, size, speed) is a new entry in <see cref="FloatingTextConfig"/>
/// rather than new code: callers only ever pass a text and a style id.
/// </summary>
[System.Serializable]
public class FloatingTextStyle
{
    [Tooltip("Key callers pass to FloatingTextManager.Show(...). Matched case-insensitively.")]
    public string id = "default";

    [Header("Look")]
    public Color color = Color.white;
    [Tooltip("In world units, not pixels: this is 3D text, so ~4 reads as a normal label over a 1-unit cell.")]
    public float fontSize = 4f;
    public FontStyles fontStyle = FontStyles.Bold;
    [Tooltip("Leave empty to use the config's default font.")]
    public TMP_FontAsset font;
    public Color outlineColor = Color.black;
    [Range(0f, 1f)] public float outlineWidth = 0.2f;

    [Header("Sorting")]
    [Tooltip("Sorting layer the label renders on. Must be above whatever it floats over — the board and the minion frames sit on lower layers. Ignored if the name doesn't exist.")]
    public string sortingLayer = "Layer 3";
    public int sortingOrder = 200;

    [Header("Motion")]
    [Tooltip("Where the label starts, relative to the anchor it was spawned on (usually the minion's centre).")]
    public Vector3 spawnOffset = new Vector3(0f, 0.3f, 0f);
    [Tooltip("How far the label travels over its whole lifetime. Y is 'up the screen' on this board; use Z instead if a popup should drift towards the camera.")]
    public Vector3 riseOffset = new Vector3(0f, 0.8f, 0f);
    public Ease riseEase = Ease.OutCubic;

    [Tooltip("Scale-up as the label appears. The rise and the fade are timed off this too.")]
    public float popInDuration = 0.15f;
    public Ease popInEase = Ease.OutBack;
    public float startScale = 0.4f;
    public float endScale = 1f;

    [Tooltip("Fully visible for this long after the pop-in, before the fade starts.")]
    public float holdDuration = 0.45f;
    public float fadeDuration = 0.55f;
    public Ease fadeEase = Ease.InQuad;

    [Tooltip("Random left/right spread applied at spawn, so two popups landing on the same minion in the same beat don't sit exactly on top of each other. 0 = always centred.")]
    public float horizontalJitter = 0.1f;

    [Tooltip("On = the label tracks the anchor it was spawned on, so it stays with a minion that moves (or gets swapped) mid-flight. Off = it stays where it spawned. Either way it survives the anchor being destroyed, finishing at the last known spot.")]
    public bool followAnchor = true;

    /// <summary>Whole lifetime of the popup: pop-in, hold, then fade. The rise spans all of it.</summary>
    public float TotalDuration => popInDuration + holdDuration + fadeDuration;

    /// <summary>
    /// Shallow copy, for callers that want to tweak a preset for one popup (a different colour, a longer
    /// hold) without editing the shared config asset.
    /// </summary>
    public FloatingTextStyle Clone()
    {
        return (FloatingTextStyle)MemberwiseClone();
    }
}
