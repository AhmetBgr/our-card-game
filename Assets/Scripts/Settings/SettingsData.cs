using System;

/// <summary>
/// Everything the player can set about how the game presents itself, and nothing about what they have
/// played. This is the whole payload of <c>settings.json</c>.
///
/// Kept apart from <see cref="SaveData"/> on purpose. Settings belong to the DEVICE, not the profile:
/// they have to be readable before a save exists (the first UI hover in the menu can beat
/// <see cref="SaveManager"/> to it), clearing a save must never cost the player their volume, and a
/// corrupt save must not take the settings down with it.
///
/// Every field carries its default as an initializer, which is also what a save written before that
/// field existed deserializes to -- so adding a setting never needs a version bump or a migration.
/// </summary>
[Serializable]
public class SettingsData
{
    /// <summary>
    /// Format version of the file, for the day a field's MEANING changes rather than merely being added.
    /// Nothing reads it yet; it costs one int and cannot be added retroactively.
    /// </summary>
    public int version = CurrentVersion;

    public const int CurrentVersion = 1;

    // -------------------------------------------------------------------------------------------------
    // Audio. Every volume is a 0..1 slider position, NOT a gain -- AudioManager applies the curve to
    // perceived loudness when it levels a voice. Storing the slider position is what lets that curve be
    // changed later without silently re-levelling everyone's saved settings.
    // -------------------------------------------------------------------------------------------------

    public float masterVolume = 1f;

    public float musicVolume = 0.6f;

    public float sfxVolume = 1f;

    /// <summary>
    /// Room tone and other beds. Nothing plays on this bus yet -- it exists so the mixer the player sets
    /// up today still means the same thing on the day ambience ships.
    /// </summary>
    public float ambientVolume = 0.8f;

    /// <summary>Silences every bus at once, without disturbing the levels underneath.</summary>
    public bool muted;

    // -------------------------------------------------------------------------------------------------
    // Interface
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the in-match action log is expanded. Off by default: the log is a reference for a player
    /// who wants it, not something a first match should open with.
    /// </summary>
    public bool showActionLog;

    /// <summary>
    /// Whether hovered hand cards tilt toward the pointer. On by default, since it is the existing look;
    /// players who dislike the motion can turn it off.
    /// </summary>
    public bool hoverTiltEnabled = true;

    // -------------------------------------------------------------------------------------------------
    // Display. Only meaningful on desktop builds; the browser and phones size the game themselves and
    // the settings panel hides the row there (see GameSettings.SupportsResolution).
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The resolution the player picked, in pixels. Zero means they never picked one, and the game runs
    /// at whatever it launched at -- which is what keeps a fresh install from fighting the player
    /// settings' default over who decides the window size.
    /// </summary>
    public int resolutionWidth;

    public int resolutionHeight;

    /// <summary>
    /// Whether the game runs fullscreen. Three-valued rather than a bool because a bool cannot say
    /// "the player never chose", and a fresh install must not fight the player settings over how the
    /// game comes up. See the Fullscreen constants below.
    /// </summary>
    public int fullscreen = FullscreenUnset;

    /// <summary>Leave the game however it launched.</summary>
    public const int FullscreenUnset = 0;

    public const int FullscreenWindowed = 1;

    public const int FullscreenOn = 2;
}
