using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The player's settings, and the one place they are read, written and persisted.
///
/// Static rather than a MonoBehaviour singleton because settings have to answer before any scene object
/// exists: <see cref="AudioManager"/> levels its very first voice off them, and that voice can be the
/// hover sound of the first button in the main menu, played before any Awake this code controls has run.
/// A component would have to be found, bootstrapped, or remembered in every scene; a static class is
/// simply available.
///
/// Storage mirrors <see cref="SaveManager"/>, for the same reason it does there: a JSON file under
/// <see cref="Application.persistentDataPath"/> everywhere, and PlayerPrefs in the browser, where a
/// System.IO write into the emscripten filesystem looks like it succeeded and is thrown away on the next
/// page load. A separate file from the save, so clearing one never touches the other.
///
/// Writes are COALESCED. A slider drag raises a change every frame, and a file write (or, on the web, a
/// PlayerPrefs flush to IndexedDB) per frame would be felt. Changes mark the settings dirty and a hidden
/// driver flushes them a moment after the last one, plus on pause, focus loss and quit -- so the worst
/// case for a tab closed mid-drag is losing the last fraction of a second of slider movement.
///
/// The settings panel works differently: it opens an EDIT SESSION (<see cref="BeginEditing"/>). Inside
/// one, changes still apply live -- the player hears the volume they are dragging toward -- but nothing
/// is written until <see cref="SaveEdits"/>, and <see cref="DiscardEdits"/> puts everything back the way
/// it was. That is what lets the panel have a Save button that means something, and a Close that
/// means "never mind".
/// </summary>
public static class GameSettings
{
    /// <summary>File the settings are written to, under <see cref="Application.persistentDataPath"/>.</summary>
    public const string FileName = "settings.json";

    /// <summary>PlayerPrefs key the browser build stores the settings under, instead of a file.</summary>
    private const string WebStorageKey = "SettingsJson";

    // Keys the first audio system wrote its volumes to, before there was a settings file. Read once, on
    // a device with no settings yet, and then deleted -- a player who set their volume in an earlier
    // build keeps it instead of being reset to full.
    private const string LegacyMasterKey = "audio.master";
    private const string LegacySfxKey = "audio.sfx";
    private const string LegacyMusicKey = "audio.music";
    private const string LegacyMutedKey = "audio.muted";

    /// <summary>
    /// Seconds of quiet after the last change before the settings are written. Long enough that a slider
    /// drag is one write, short enough that a player who quits straight after a click keeps it.
    /// </summary>
    private const float WriteDelay = 0.35f;

    /// <summary>
    /// Raised after any setting changes, however it changed -- a slider, a toggle, or a reset to
    /// defaults. Live UI subscribes to this so two panels showing the same setting can never disagree.
    /// </summary>
    public static event Action Changed;

    private static SettingsData _data;

    /// <summary>The settings as they were when the current edit session began; what Discard goes back to.</summary>
    private static SettingsData _snapshot;

    /// <summary>
    /// The screen size the game came up at, before any stored resolution was applied. What a stored
    /// resolution of zero means, and so what "revert" goes back to when nothing was ever chosen.
    /// </summary>
    private static Vector2Int _launchScreen;

    /// <summary>Whether the game came up fullscreen. What an unset fullscreen choice means.</summary>
    private static bool _launchFullscreen;

    /// <summary>Whether the store already held settings when they were loaded. Gates the migrations.</summary>
    private static bool _hadStoredSettings;

    private static bool _legacySaveAdopted;

    private static bool _dirty;
    private static float _dirtyAt;

    private static SettingsFlusher _flusher;

    // Statics survive entering play mode when domain reload is disabled ("Enter Play Mode Options"),
    // which would otherwise leave the previous session's subscribers -- all destroyed objects -- on
    // Changed, and a dead flusher believed to exist. Same reasoning as AudioManager.ResetStatics.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        _data = null;
        _snapshot = null;
        _launchScreen = Vector2Int.zero;
        _launchFullscreen = false;
        IsEditing = false;
        _hadStoredSettings = false;
        _legacySaveAdopted = false;
        _dirty = false;
        _flusher = null;
    }

    /// <summary>
    /// The live settings. Loaded on first touch. Mutating the returned object directly does NOT persist
    /// or notify -- go through the properties below, which is why they exist.
    /// </summary>
    public static SettingsData Data
    {
        get
        {
            if (_data == null) Load();
            return _data;
        }
    }

    // -------------------------------------------------------------------------------------------------
    // The settings themselves
    // -------------------------------------------------------------------------------------------------

    public static float MasterVolume
    {
        get => Data.masterVolume;
        set => SetFloat(ref Data.masterVolume, value);
    }

    public static float MusicVolume
    {
        get => Data.musicVolume;
        set => SetFloat(ref Data.musicVolume, value);
    }

    public static float SfxVolume
    {
        get => Data.sfxVolume;
        set => SetFloat(ref Data.sfxVolume, value);
    }

    public static float AmbientVolume
    {
        get => Data.ambientVolume;
        set => SetFloat(ref Data.ambientVolume, value);
    }

    public static bool Muted
    {
        get => Data.muted;
        set => SetBool(ref Data.muted, value);
    }

    public static bool ShowActionLog
    {
        get => Data.showActionLog;
        set => SetBool(ref Data.showActionLog, value);
    }

    public static bool HoverTiltEnabled
    {
        get => Data.hoverTiltEnabled;
        set => SetBool(ref Data.hoverTiltEnabled, value);
    }

    /// <summary>
    /// Use the plain forged-card animation instead of the morph. Read on demand, once per forge, the
    /// way <see cref="HoverTiltEnabled"/> is -- flipping it mid-match changes the next forge, and never
    /// disturbs one already playing.
    /// </summary>
    public static bool ReduceCardAnimations
    {
        get => Data.reduceCardAnimations;
        set => SetBool(ref Data.reduceCardAnimations, value);
    }

    /// <summary>Chosen window resolution, in pixels. Zero when the player never chose one.</summary>
    public static int ResolutionWidth => Data.resolutionWidth;

    public static int ResolutionHeight => Data.resolutionHeight;

    /// <summary>
    /// Records the resolution the player wants. Both axes at once, because a width without its height
    /// is a moment where the settings describe a resolution that does not exist. Zero for both means
    /// the size the game launched at.
    ///
    /// RECORDED, NOT APPLIED. Nothing reaches the screen until <see cref="SaveEdits"/>, which is also
    /// when the settings panel puts its keep-or-revert question up -- so the one moment the screen
    /// changes is the moment the player asked for it, and browsing the list costs nothing.
    /// </summary>
    public static void SetResolution(int width, int height)
    {
        width = Mathf.Max(0, width);
        height = Mathf.Max(0, height);

        if (Data.resolutionWidth == width && Data.resolutionHeight == height) return;

        Data.resolutionWidth = width;
        Data.resolutionHeight = height;
        MarkDirty();
        Raise();
    }

    /// <summary>
    /// Whether the game is fullscreen. Reading it answers for the screen as it IS -- when the player
    /// has never chosen, that is however the game launched.
    /// </summary>
    public static bool Fullscreen
    {
        get
        {
            switch (Data.fullscreen)
            {
                case SettingsData.FullscreenOn: return true;
                case SettingsData.FullscreenWindowed: return false;
                default: return _launchFullscreen;
            }
        }
        set => SetFullscreenChoice(value ? SettingsData.FullscreenOn : SettingsData.FullscreenWindowed);
    }

    /// <summary>
    /// The stored choice as it sits in the file, "never chosen" included. What an undo has to put
    /// back, since choosing what was already true is still a change to the file.
    /// </summary>
    public static int FullscreenChoice => Data.fullscreen;

    /// <summary>
    /// Records a fullscreen choice. Recorded and not applied, on the same terms as the resolution
    /// beside it: the screen changes when the player saves.
    /// </summary>
    public static void SetFullscreenChoice(int choice)
    {
        choice = Mathf.Clamp(choice, SettingsData.FullscreenUnset, SettingsData.FullscreenOn);

        if (Data.fullscreen == choice) return;

        Data.fullscreen = choice;
        MarkDirty();
        Raise();
    }

    /// <summary>Puts every setting back to the value a fresh install has, and persists that.</summary>
    public static void ResetToDefaults()
    {
        _data = new SettingsData();
        MarkDirty();
        Raise();
    }

    private static void SetFloat(ref float field, float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(field, value)) return;

        field = value;
        MarkDirty();
        Raise();
    }

    private static void SetBool(ref bool field, bool value)
    {
        if (field == value) return;

        field = value;
        MarkDirty();
        Raise();
    }

    // -------------------------------------------------------------------------------------------------
    // Edit sessions
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether an edit session is open. While it is, changes apply live but are NOT written; they wait
    /// for <see cref="SaveEdits"/> or are undone by <see cref="DiscardEdits"/>.
    /// </summary>
    public static bool IsEditing { get; private set; }

    /// <summary>Whether the live settings differ from what the open edit session started with.</summary>
    public static bool HasUnsavedChanges => IsEditing && !SameAs(Data, _snapshot);

    /// <summary>
    /// Starts an edit session: remembers the settings as they are now, and stops writing until the
    /// session is saved. Re-entrant -- a second call inside an open session is a no-op, so the snapshot
    /// is the state before ANY of the edits, not before the latest one.
    /// </summary>
    public static void BeginEditing()
    {
        if (IsEditing) return;

        // Anything still pending from outside a session -- the action log button on the board, say --
        // reaches disk first, so what Discard goes back to is exactly what is stored.
        Flush();

        _snapshot = Clone(Data);
        IsEditing = true;
    }

    /// <summary>
    /// Writes the session's edits out and makes them the new baseline. The session STAYS OPEN: the panel
    /// is still on screen, and the next change should again be one the player has to save.
    /// </summary>
    public static void SaveEdits()
    {
        if (!IsEditing) return;

        _snapshot = Clone(_data);
        _dirty = true;
        Flush();

        // Saving is where the display settings finally reach the screen. Read
        // SavedDisplayChoice before calling this to know what to put back if the player cannot see
        // the result -- that is the whole point of the panel's confirmation.
        ApplyDisplay();
    }

    /// <summary>
    /// Ends the session and puts every setting back to what it was when the session began. Raises
    /// <see cref="Changed"/> if that moved anything, so the audio, the action log and any panel still
    /// listening follow it back.
    /// </summary>
    public static void DiscardEdits()
    {
        if (!IsEditing) return;

        IsEditing = false;

        SettingsData snapshot = _snapshot;
        _snapshot = null;

        if (snapshot == null) return;

        bool moved = !SameAs(_data, snapshot);
        _data = snapshot;
        _dirty = false;

        if (!moved) return;

        Raise();

        // A size or screen mode tried and not kept goes back with everything else.
        ApplyDisplay();
    }

    private static SettingsData Clone(SettingsData data) =>
        JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(data));

    /// <summary>
    /// Field-by-field equality, by way of the same serializer that writes the file: a setting added
    /// tomorrow is compared without anyone remembering to add it here.
    /// </summary>
    private static bool SameAs(SettingsData a, SettingsData b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        return JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
    }

    // -------------------------------------------------------------------------------------------------
    // Display
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether picking a resolution means anything on this platform. The browser sizes the canvas to
    /// the page and a phone has one resolution, so the setting is hidden there rather than shown and
    /// ignored. Evaluated per build, so an editor targeting the web hides it too, which is the only way
    /// to see the web layout without building.
    /// </summary>
    public static bool SupportsResolution
    {
        get
        {
#if UNITY_WEBGL
            return false;
#else
            return !Application.isMobilePlatform;
#endif
        }
    }

    /// <summary>
    /// The display settings as one value: a size, and the tri-state fullscreen choice. What a save has
    /// to remember so the player can undo it, since by then the new one is already on disk.
    /// </summary>
    public struct DisplayChoice
    {
        public int width;
        public int height;
        public int fullscreen;

        public bool Matches(DisplayChoice other) =>
            width == other.width && height == other.height && fullscreen == other.fullscreen;
    }

    /// <summary>
    /// The display settings as last written to disk -- or, inside an edit session that has not saved
    /// yet, as the session began. The baseline a save moves away from.
    /// </summary>
    public static DisplayChoice SavedDisplayChoice => ChoiceOf(_snapshot ?? Data);

    /// <summary>The display settings as they stand now, saved or not.</summary>
    public static DisplayChoice CurrentDisplayChoice => ChoiceOf(Data);

    private static DisplayChoice ChoiceOf(SettingsData data) => new DisplayChoice
    {
        width = data.resolutionWidth,
        height = data.resolutionHeight,
        fullscreen = data.fullscreen
    };

    /// <summary>
    /// What a stored choice actually means on screen, with "never chosen" resolved to whatever the
    /// game launched with. For describing a choice to the player, who has no use for a zero.
    /// </summary>
    public static void Resolve(DisplayChoice choice, out Vector2Int size, out bool fullscreen)
    {
        size = new Vector2Int(choice.width, choice.height);

        if (size.x <= 0 || size.y <= 0) size = _launchScreen;
        if (size.x <= 0 || size.y <= 0) size = new Vector2Int(Screen.width, Screen.height);

        switch (choice.fullscreen)
        {
            case SettingsData.FullscreenOn:
                fullscreen = true;
                break;
            case SettingsData.FullscreenWindowed:
                fullscreen = false;
                break;
            default:
                fullscreen = _launchFullscreen;
                break;
        }
    }

    /// <summary>
    /// Puts a display choice back, on screen AND on disk, leaving every other setting where it is.
    ///
    /// For undoing a save the player could not confirm. It writes rather than waiting for the next
    /// save, because the thing being undone already reached the file -- an undo that is not written
    /// is one that comes back on the next launch, which is exactly the screen they could not see.
    /// </summary>
    public static void RestoreDisplayChoice(DisplayChoice choice)
    {
        if (Data == null) return;

        Assign(_data, choice);

        // The saved baseline moves with it, so the panel does not then report an unsaved change that
        // the player never made.
        if (_snapshot != null) Assign(_snapshot, choice);

        _dirty = true;
        Flush();

        Raise();
        ApplyDisplay();
    }

    private static void Assign(SettingsData data, DisplayChoice choice)
    {
        data.resolutionWidth = choice.width;
        data.resolutionHeight = choice.height;
        data.fullscreen = choice.fullscreen;
    }

    /// <summary>
    /// Puts the stored size and screen mode on screen, if they are not there already -- or, for
    /// whichever of them was never chosen, whatever the game launched with.
    ///
    /// Both go out in ONE call. Size and mode are set by the same function, so applying them
    /// separately means a first call that reads back the mode it is about to change, and a window
    /// that flickers through a state nobody asked for.
    ///
    /// Fullscreen means BORDERLESS fullscreen. Exclusive fullscreen hands the display over to the
    /// game, which is a far worse place to be if the mode turns out to be one the monitor cannot
    /// show; borderless fails visibly instead, where the confirm dialog can still be answered.
    /// </summary>
    public static void ApplyDisplay()
    {
        if (!SupportsResolution || !Application.isPlaying) return;

        int width = Data.resolutionWidth;
        int height = Data.resolutionHeight;

        if (width <= 0 || height <= 0)
        {
            width = _launchScreen.x;
            height = _launchScreen.y;
        }

        if (width <= 0 || height <= 0) return;

        FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (Screen.width == width && Screen.height == height && Screen.fullScreenMode == mode) return;

        Screen.SetResolution(width, height, mode);
    }

    // Before the first scene so the title screen never draws one frame at the wrong size or in the
    // wrong mode. After ResetStatics (SubsystemRegistration), which is what lets this be the first
    // touch that loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyStoredDisplay()
    {
        _launchScreen = new Vector2Int(Screen.width, Screen.height);
        _launchFullscreen = Screen.fullScreen;

        ApplyDisplay();

        // Not in the editor: there the "window" is the Game view, and docking or resizing it is layout
        // work, not a player choosing a size.
        if (SupportsResolution && !Application.isEditor)
        {
            var host = new GameObject(nameof(WindowSizeWatcher)) { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<WindowSizeWatcher>();
        }
    }

    /// <summary>
    /// Keeps a window the player resized by hand -- dragging an edge, maximizing -- as their chosen
    /// resolution, so the next launch comes up at the same size instead of snapping back.
    ///
    /// Waits while the settings panel has an edit session open, so a drag never slips a size past its
    /// Save button or its keep-or-revert question. Whatever the window settles at once the panel closes
    /// is what gets kept. A size the game set itself (a save, a revert) already matches the stored one,
    /// so recording it changes nothing.
    /// </summary>
    private static void RecordWindowSize(int width, int height)
    {
        if (IsEditing || width <= 0 || height <= 0) return;

        // A window being resized is a windowed game. Stored as fullscreen, the next launch would come up
        // fullscreen at the dragged size, which is neither thing the player had.
        bool leaveFullscreen = Fullscreen;

        if (!leaveFullscreen && Data.resolutionWidth == width && Data.resolutionHeight == height) return;

        Data.resolutionWidth = width;
        Data.resolutionHeight = height;
        if (leaveFullscreen) Data.fullscreen = SettingsData.FullscreenWindowed;

        MarkDirty();
        Raise();
    }

    private static void Raise()
    {
        // A listener that throws must not stop the rest of them hearing about a setting they are showing.
        try
        {
            Changed?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // -------------------------------------------------------------------------------------------------
    // Persistence
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Absolute path of the settings file. Not used by the WebGL player -- see <see cref="WebStorageKey"/>.
    /// </summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    private static void Load()
    {
        string json = Read();
        _hadStoredSettings = !string.IsNullOrEmpty(json);

        if (_hadStoredSettings)
        {
            try
            {
                _data = JsonUtility.FromJson<SettingsData>(json);
            }
            catch (Exception e)
            {
                // A truncated or hand-edited file must not brick the game, and must not cost the player
                // anything they cannot set again in ten seconds.
                Debug.LogError($"Settings could not be parsed, falling back to defaults. {e.Message}");
                _data = null;
                _hadStoredSettings = false;
            }
        }

        if (_data == null) _data = new SettingsData();

        Sanitize(_data);

        if (!_hadStoredSettings && AdoptLegacyAudioPrefs())
        {
            // Written straight out rather than left dirty: the flusher only exists in play mode, and the
            // legacy keys are deleted as they are read, so a migration that never reached disk is lost.
            Write();
            _hadStoredSettings = true;
        }
    }

    /// <summary>
    /// Volumes set in a build that kept them in PlayerPrefs, folded into a fresh settings file.
    /// Returns whether anything was actually carried over.
    /// </summary>
    private static bool AdoptLegacyAudioPrefs()
    {
        bool found = false;

        if (PlayerPrefs.HasKey(LegacyMasterKey))
        {
            _data.masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(LegacyMasterKey, _data.masterVolume));
            found = true;
        }

        if (PlayerPrefs.HasKey(LegacySfxKey))
        {
            _data.sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(LegacySfxKey, _data.sfxVolume));
            found = true;
        }

        if (PlayerPrefs.HasKey(LegacyMusicKey))
        {
            _data.musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(LegacyMusicKey, _data.musicVolume));
            found = true;
        }

        if (PlayerPrefs.HasKey(LegacyMutedKey))
        {
            _data.muted = PlayerPrefs.GetInt(LegacyMutedKey, 0) == 1;
            found = true;
        }

        if (!found) return false;

        PlayerPrefs.DeleteKey(LegacyMasterKey);
        PlayerPrefs.DeleteKey(LegacySfxKey);
        PlayerPrefs.DeleteKey(LegacyMusicKey);
        PlayerPrefs.DeleteKey(LegacyMutedKey);
        PlayerPrefs.Save();

        return true;
    }

    /// <summary>
    /// Adopts the two interface preferences that used to ride along in the JSON save, for a device that
    /// has a save but no settings file yet. Called by <see cref="SaveManager"/> once it has loaded, which
    /// is the only thing that can read them; a no-op on every later launch.
    ///
    /// The fields are deliberately left in <see cref="SaveData"/> rather than deleted -- they are what
    /// this reads from, and a save is not rewritten just because it still carries them.
    /// </summary>
    public static void AdoptLegacySavePreferences(bool showActionLog, bool hoverTiltEnabled)
    {
        if (_data == null) Load();
        if (_hadStoredSettings || _legacySaveAdopted) return;

        _legacySaveAdopted = true;

        _data.showActionLog = showActionLog;
        _data.hoverTiltEnabled = hoverTiltEnabled;

        Write();
        _hadStoredSettings = true;

        Raise();
    }

    /// <summary>Marks the settings as needing a write, and makes sure something will do it.</summary>
    private static void MarkDirty()
    {
        // Inside an edit session nothing is written until the player saves. Not even marked: a flush on
        // focus loss must not sneak the unsaved values onto disk behind the Save button's back.
        if (IsEditing) return;

        _dirty = true;
        _dirtyAt = Time.realtimeSinceStartup;

        // Nothing drives a coalesced write outside play mode -- an editor tool changing a setting has no
        // frames to wait for -- so write there and then.
        if (!Application.isPlaying)
        {
            Flush();
            return;
        }

        EnsureFlusher();
    }

    /// <summary>Writes the settings out now, if anything is pending. Safe to call at any time.</summary>
    public static void Flush()
    {
        if (!_dirty || _data == null) return;

        _dirty = false;
        Write();
    }

    /// <summary>Called by the flusher each frame; writes once the changes have settled.</summary>
    internal static void FlushIfDue()
    {
        if (!_dirty) return;
        if (Time.realtimeSinceStartup - _dirtyAt < WriteDelay) return;

        Flush();
    }

    private static void EnsureFlusher()
    {
        if (_flusher != null) return;

        // Hidden, but NOT HideAndDontSave: an object flagged DontSave survives leaving play mode in the
        // editor and turns up as a stray in whatever scene is open. Living in the DontDestroyOnLoad
        // scene is what keeps it alive across scene loads, and lets it die with the play session.
        var host = new GameObject(nameof(SettingsFlusher)) { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(host);
        _flusher = host.AddComponent<SettingsFlusher>();
    }

    private static void Write()
    {
        string json = JsonUtility.ToJson(_data, true);

#if UNITY_WEBGL && !UNITY_EDITOR
        // PlayerPrefs.Save() is what pushes the browser's in-memory filesystem out to IndexedDB, so it
        // has to run on every write rather than being left to quit time -- a closing tab may never get
        // that far. Same arrangement as SaveManager.
        PlayerPrefs.SetString(WebStorageKey, json);
        PlayerPrefs.Save();
#else
        string path = FilePath;
        string tempPath = path + ".tmp";

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Written to a sibling temp file and swapped in, so a crash mid-write leaves the previous
            // settings intact instead of a half-flushed file that will not parse.
            File.WriteAllText(tempPath, json);

            if (File.Exists(path)) File.Delete(path);

            File.Move(tempPath, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write settings at {path}: {e.Message}");
        }
#endif
    }

    private static string Read()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PlayerPrefs.GetString(WebStorageKey, string.Empty);
#else
        string path = FilePath;

        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read settings at {path}: {e.Message}");
            return string.Empty;
        }
#endif
    }

    /// <summary>Whether the store already holds settings, without caring whether it is a file or not.</summary>
    public static bool HasStoredSettings()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PlayerPrefs.HasKey(WebStorageKey);
#else
        return File.Exists(FilePath);
#endif
    }

    /// <summary>
    /// Deletes the stored settings and returns to defaults. Used by the editor tools; the game itself
    /// only ever resets in place, which leaves the file there.
    /// </summary>
    public static void DeleteStoredSettings()
    {
        _dirty = false;
        _snapshot = null;
        IsEditing = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.DeleteKey(WebStorageKey);
        PlayerPrefs.Save();
#else
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);

            string tempPath = FilePath + ".tmp";
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to delete settings at {FilePath}: {e.Message}");
        }
#endif

        _data = new SettingsData();
        _hadStoredSettings = false;
        _legacySaveAdopted = false;

        Raise();
    }

    /// <summary>
    /// Values a hand-edited or older file could hold that the rest of the game assumes away: volumes are
    /// slider positions, and nothing outside 0..1 has a meaning.
    /// </summary>
    private static void Sanitize(SettingsData data)
    {
        data.masterVolume = Mathf.Clamp01(data.masterVolume);
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        data.ambientVolume = Mathf.Clamp01(data.ambientVolume);

        // A half-set resolution is no resolution at all.
        if (data.resolutionWidth <= 0 || data.resolutionHeight <= 0)
        {
            data.resolutionWidth = 0;
            data.resolutionHeight = 0;
        }

        data.fullscreen = Mathf.Clamp(data.fullscreen, SettingsData.FullscreenUnset, SettingsData.FullscreenOn);

        if (data.version <= 0) data.version = SettingsData.CurrentVersion;
    }

    /// <summary>
    /// The frames a static class does not have. Hidden and undestroyable-by-scene-load, created on the
    /// first change rather than at boot so a session that never touches a setting never allocates it.
    /// </summary>
    private class SettingsFlusher : MonoBehaviour
    {
        private void LateUpdate() => FlushIfDue();

        // Both of these are the last callback a mobile or browser session is guaranteed to get, so the
        // pending write happens here rather than being left to OnApplicationQuit, which may never run.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Flush();
        }

        private void OnApplicationQuit() => Flush();
    }

    /// <summary>
    /// Notices the window changing size in windowed mode and hands the size to
    /// <see cref="RecordWindowSize"/> once it stops changing. A drag passes through dozens of
    /// in-between sizes, and only the one the player lets go at is worth keeping.
    /// </summary>
    private class WindowSizeWatcher : MonoBehaviour
    {
        /// <summary>Seconds the size has to hold still before it counts as chosen.</summary>
        private const float SettleDelay = 0.5f;

        private Vector2Int _lastSize;
        private float _changedAt;
        private bool _pending;

        private void Awake() => _lastSize = new Vector2Int(Screen.width, Screen.height);

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);

            if (size != _lastSize)
            {
                _lastSize = size;
                _changedAt = Time.realtimeSinceStartup;
                _pending = true;
                return;
            }

            if (!_pending || IsEditing) return;
            if (Time.realtimeSinceStartup - _changedAt < SettleDelay) return;

            _pending = false;

            // A fullscreen game's size is the display's, not a window's. Leaving fullscreen does land
            // here windowed, and that window is kept like any other.
            if (Screen.fullScreenMode != FullScreenMode.Windowed) return;

            RecordWindowSize(size.x, size.y);
        }

        // Written on the way out too, so resizing and closing straight away still keeps the size.
        private void OnApplicationQuit()
        {
            if (_pending && !IsEditing && Screen.fullScreenMode == FullScreenMode.Windowed)
                RecordWindowSize(Screen.width, Screen.height);

            Flush();
        }
    }
}
