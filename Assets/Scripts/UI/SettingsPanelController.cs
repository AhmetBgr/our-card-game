using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The settings panel: the volume buses, the mute switch, the interface options and -- on desktop -- the
/// resolution, over one shared <see cref="GameSettings"/>.
///
/// One prefab serves both places settings can be reached from -- the title screen and the in-match pause
/// menu -- because the alternative is two panels that drift apart, and a player who set something in one
/// and cannot find it in the other. Neither owner has to know what is ON the panel: they call
/// <see cref="Open"/> and <see cref="Close"/>, and listen to <see cref="Closed"/> for the panel closing
/// itself.
///
/// NOTHING IS SAVED UNTIL THE PLAYER PRESSES SAVE. Opening the panel opens an edit session on the
/// settings (<see cref="GameSettings.BeginEditing"/>): every control still writes straight through, so
/// the volume being dragged is the volume being heard, but the file is not touched. Save writes it and
/// the button goes quiet again; Close -- by the button, by Escape, by the pause menu putting the panel
/// away, or by the scene going -- discards whatever was not saved and puts the live settings back.
///
/// The controls do NOT hold the settings. Every one of them is seeded from <see cref="GameSettings"/> and
/// writes straight back to it, and the panel re-seeds itself whenever the settings change from anywhere
/// -- which is what makes Reset to Defaults, and a discard on close, show up here without a line of
/// code between them.
///
/// The volume sliders are not wired here at all: each carries a <see cref="VolumeSlider"/> that binds
/// itself to its bus. This component owns only the things that have no component of their own.
/// </summary>
public class SettingsPanelController : MonoBehaviour
{
    /// <summary>
    /// A latching button with an optional On/Off caption beside it. The caption is what makes a
    /// sprite-swap toggle readable -- the two sprites differ, but not in a way that says which one means
    /// yes.
    /// </summary>
    [Serializable]
    public class ToggleRow
    {
        public ToggleButton toggle;

        [Tooltip("Optional. Set to On/Off as the toggle flips.")]
        public TMP_Text stateLabel;

        public void Bind(UnityEngine.Events.UnityAction<bool> handler)
        {
            if (toggle != null) toggle.onValueChanged.AddListener(handler);
        }

        public void Unbind(UnityEngine.Events.UnityAction<bool> handler)
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(handler);
        }

        /// <summary>Shows <paramref name="value"/> without reporting it back, and updates the caption.</summary>
        public void Show(bool value)
        {
            if (toggle != null) toggle.SetIsOn(value, notify: false);
            if (stateLabel != null) stateLabel.text = value ? "On" : "Off";
        }
    }

    [Tooltip("The part that is shown and hidden. Empty = this object. Give it a child when the controller " +
             "has to keep running while the panel is away (reading a key, say).")]
    [SerializeField] private GameObject panel;

    [Header("Interface")]
    [Tooltip("Whether the in-match action log is expanded. The same setting the log's own Log button flips.")]
    [SerializeField] private ToggleRow actionLogRow = new ToggleRow();

    [Header("Display")]
    [Tooltip("The whole Display section -- heading and rows. Hidden on platforms where a resolution " +
             "means nothing (the browser, phones); see GameSettings.SupportsResolution.")]
    [SerializeField] private GameObject displaySection;

    [Tooltip("Borderless fullscreen, or a window. Flipped at once and has to be confirmed, like the " +
             "resolution beside it.")]
    [SerializeField] private ToggleRow fullscreenRow = new ToggleRow();

    [Tooltip("Lists the monitor's resolutions. A pick is applied at once and has to be confirmed.")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Tooltip("Asks whether the new resolution is any good, and reverts it if nobody says so in time.")]
    [SerializeField] private ResolutionConfirmDialog resolutionConfirm;

    [Header("Buttons")]
    [Tooltip("Writes the changes made since the panel opened (or since the last save). Only interactable " +
             "while there is something to write.")]
    [SerializeField] private Button saveButton;

    [Tooltip("Optional. Shown while there is something to save, so a lit Save button is not the only " +
             "sign of it.")]
    [SerializeField] private GameObject unsavedLabel;

    [SerializeField] private Button resetButton;

    [Tooltip("Closes the panel, discarding unsaved changes. The pause menu also closes it with its own " +
             "Settings button and with Escape.")]
    [SerializeField] private Button closeButton;

    /// <summary>Raised when the panel closes, however it was closed. The owner un-latches its button on this.</summary>
    public event Action Closed;

    /// <summary>Whether the panel is currently on screen.</summary>
    public bool IsOpen => Body != null && Body.activeSelf;

    private GameObject Body => panel != null ? panel : gameObject;

    /// <summary>What each dropdown entry stands for, in the dropdown's own order.</summary>
    private readonly List<Vector2Int> _resolutionOptions = new List<Vector2Int>();

    // Set while the dropdown is being filled or seeded, so the onValueChanged that fires is not mistaken
    // for the player picking something.
    private bool _seedingResolution;

    private void Awake()
    {
        actionLogRow.Bind(OnActionLogChanged);
        fullscreenRow.Bind(OnFullscreenChanged);

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionPicked);

        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (resetButton != null) resetButton.onClick.AddListener(ResetToDefaults);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (displaySection != null) displaySection.SetActive(GameSettings.SupportsResolution);

        // Authored on so the panel can be laid out in the editor; never on screen at kickoff.
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        actionLogRow.Unbind(OnActionLogChanged);
        fullscreenRow.Unbind(OnFullscreenChanged);

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionPicked);
    }

    private void OnEnable()
    {
        Refresh();
        GameSettings.Changed += Refresh;
    }

    private void OnDisable()
    {
        GameSettings.Changed -= Refresh;

        // The panel going away with its session still open -- a scene unloading under the pause menu,
        // or the object being switched off by something that is not Close -- is a close, and an
        // unsaved close discards. Idempotent, so a Close that already discarded costs nothing here.
        GameSettings.DiscardEdits();
    }

    /// <summary>Pulls every control back into line with the settings. Cheap, and never writes anything.</summary>
    public void Refresh()
    {
        actionLogRow.Show(GameSettings.ShowActionLog);
        fullscreenRow.Show(GameSettings.Fullscreen);

        SeedResolution();
        RefreshSaveButton();
    }

    public void Open()
    {
        if (Body == null) return;

        // The session opens BEFORE the controls are seeded, so the snapshot it keeps is what is on
        // screen, and the first change the player makes is the first thing the Save button lights for.
        GameSettings.BeginEditing();

        Body.SetActive(true);

        // Refreshed on open rather than only on enable: when the panel IS this object's child, this
        // component never disabled, so OnEnable does not run again.
        Refresh();
    }

    /// <summary>Puts the panel away. Anything not saved is discarded and the live settings go back.</summary>
    public void Close()
    {
        if (Body == null || !Body.activeSelf) return;

        // The question is moot once the answer is "never mind": the discard below puts the screen
        // back whether it was answered or not.
        if (resolutionConfirm != null) resolutionConfirm.Hide();

        GameSettings.DiscardEdits();

        Body.SetActive(false);
        Closed?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>Show the panel, or put it away, to match <paramref name="show"/>.</summary>
    public void SetOpen(bool show)
    {
        if (show) Open();
        else Close();
    }

    /// <summary>
    /// Writes everything changed since the panel opened, or since the last save. The panel stays open:
    /// the player may well want to keep going, and a panel that vanishes on Save is one that has to be
    /// reopened to check what it did.
    ///
    /// Saving is also when the display settings reach the screen, so it is where the keep-or-revert
    /// question goes up. The baseline is read BEFORE the save, because that is the only moment the
    /// settings still remember what to go back to.
    /// </summary>
    public void Save()
    {
        if (!GameSettings.HasUnsavedChanges) return;

        GameSettings.DisplayChoice before = GameSettings.SavedDisplayChoice;

        GameSettings.SaveEdits();

        // Nothing raised Changed -- the values did not move, only their saved-ness did -- so the button
        // is told by hand.
        RefreshSaveButton();

        ConfirmDisplay(before);
    }

    /// <summary>
    /// Asks whether the screen the save just produced is one the player can see, and puts it back if
    /// they cannot say so. Nothing is asked when the save left the display alone.
    /// </summary>
    private void ConfirmDisplay(GameSettings.DisplayChoice before)
    {
        if (resolutionConfirm == null) return;

        GameSettings.DisplayChoice after = GameSettings.CurrentDisplayChoice;
        if (after.Matches(before)) return;

        // The two can differ on paper and not on screen -- picking the size the game is already at,
        // say -- and a question about a change nobody can see is just a countdown in the way.
        GameSettings.Resolve(before, out Vector2Int oldSize, out bool oldFullscreen);
        GameSettings.Resolve(after, out Vector2Int newSize, out bool newFullscreen);

        if (oldSize == newSize && oldFullscreen == newFullscreen) return;

        resolutionConfirm.Show(
            $"Now {Describe(newSize, newFullscreen)}",
            Describe(oldSize, oldFullscreen),
            onKeep: null,
            onRevert: () => GameSettings.RestoreDisplayChoice(before));
    }

    private static string Describe(Vector2Int size, bool fullscreen) =>
        $"{size.x} × {size.y}, {(fullscreen ? "fullscreen" : "windowed")}";

    private void OnActionLogChanged(bool value) => GameSettings.ShowActionLog = value;

    /// <summary>
    /// Records the screen mode. Nothing happens to the screen until the player saves -- see
    /// <see cref="ConfirmDisplay"/>.
    /// </summary>
    private void OnFullscreenChanged(bool value) => GameSettings.Fullscreen = value;

    /// <summary>
    /// Back to a fresh install's settings -- on screen, not on disk. Like every other change on the
    /// panel it waits for Save, which is also what makes it safe not to confirm: a reset the player did
    /// not mean is one Close away from never having happened.
    /// </summary>
    public void ResetToDefaults()
    {
        GameSettings.ResetToDefaults();

        // Refresh is subscribed to Changed and has already run by now; this is for the case where it is
        // not, because the panel object itself is inactive and something reset the settings anyway.
        Refresh();
    }

    // -------------------------------------------------------------------------------------------------
    // Save button
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The button is lit or not by whether it can be pressed, and by the notice beside it. Nothing
    /// touches its opacity: the button is drawn at the same strength as the two beside it, whatever
    /// state it is in.
    /// </summary>
    private void RefreshSaveButton()
    {
        bool canSave = GameSettings.HasUnsavedChanges;

        if (saveButton != null) saveButton.interactable = canSave;
        if (unsavedLabel != null) unsavedLabel.SetActive(canSave);
    }

    // -------------------------------------------------------------------------------------------------
    // Resolution
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The sizes worth offering, if the monitor can show them. The driver's own list runs to twenty-odd
    /// entries, most of them scaled oddities like 1760 × 990 that nobody has heard of; these are the
    /// ones a player expects to find. Smallest first, so the list reads the way the driver's does.
    /// </summary>
    private static readonly Vector2Int[] CommonResolutions =
    {
        new Vector2Int(1024, 768),
        new Vector2Int(1280, 720),
        new Vector2Int(1280, 800),
        new Vector2Int(1280, 1024),
        new Vector2Int(1366, 768),
        new Vector2Int(1440, 900),
        new Vector2Int(1600, 900),
        new Vector2Int(1680, 1050),
        new Vector2Int(1920, 1080),
        new Vector2Int(1920, 1200),
        new Vector2Int(2560, 1440),
        new Vector2Int(2560, 1600),
        new Vector2Int(3440, 1440),
        new Vector2Int(3840, 2160)
    };

    /// <summary>
    /// Fills the dropdown with the common sizes the monitor supports and selects the one the settings
    /// hold -- or, when they hold none, the size the game is running at, which is the honest answer to
    /// "what is it set to". Either of those can be a size that is not on the list (a windowed game
    /// dragged to an odd size, the editor's Game view), and is added rather than shown as nothing.
    /// </summary>
    private void SeedResolution()
    {
        if (resolutionDropdown == null || !GameSettings.SupportsResolution) return;

        var screen = new Vector2Int(Screen.width, Screen.height);
        var stored = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);

        bool hasStored = stored.x > 0 && stored.y > 0;
        Vector2Int current = hasStored ? stored : screen;

        _seedingResolution = true;
        try
        {
            // Both are always on the list, not just the one being shown: the size the game is running
            // at has to stay pickable after the player browses away from it, or there is no way back
            // to it short of closing the panel.
            RebuildResolutionOptions(screen, current);

            int index = _resolutionOptions.IndexOf(current);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
            resolutionDropdown.RefreshShownValue();
        }
        finally
        {
            _seedingResolution = false;
        }
    }

    private void RebuildResolutionOptions(params Vector2Int[] mustInclude)
    {
        _resolutionOptions.Clear();

        // What the monitor can actually show. Screen.resolutions lists one entry per refresh rate; the
        // player is choosing a size, so those collapse to one.
        var supported = new HashSet<Vector2Int>();
        foreach (Resolution mode in Screen.resolutions)
        {
            if (mode.width > 0 && mode.height > 0) supported.Add(new Vector2Int(mode.width, mode.height));
        }

        // A platform that reports no modes at all gets the whole common list rather than an empty one;
        // the native size below caps what is sensible on it anyway.
        bool filter = supported.Count > 0;

        foreach (Vector2Int size in CommonResolutions)
        {
            if (!filter || supported.Contains(size)) _resolutionOptions.Add(size);
        }

        // The display's own size is always offered, common or not -- it is the one the player is most
        // likely to want back.
        DisplayInfo display = Screen.mainWindowDisplayInfo;
        InsertResolution(new Vector2Int(display.width, display.height));

        foreach (Vector2Int extra in mustInclude) InsertResolution(extra);

        var labels = new List<string>(_resolutionOptions.Count);
        foreach (Vector2Int size in _resolutionOptions) labels.Add($"{size.x} × {size.y}");

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
    }

    /// <summary>Adds a size to the options where it belongs by area, unless it is already there.</summary>
    private void InsertResolution(Vector2Int size)
    {
        if (size.x <= 0 || size.y <= 0 || _resolutionOptions.Contains(size)) return;

        int at = _resolutionOptions.FindIndex(other => other.x * other.y > size.x * size.y);
        _resolutionOptions.Insert(at < 0 ? _resolutionOptions.Count : at, size);
    }

    // Whether the open list has been scrolled to the selected entry yet. Once per opening.
    private bool _resolutionListScrolled;

    private void Update()
    {
        if (resolutionDropdown == null) return;

        // TMP's dropdown opens its list scrolled to the top, and a monitor lists twenty-odd modes with
        // the current one somewhere in the middle. It offers no hook for the moment the list appears,
        // so this watches for it and scrolls the selected entry into view once.
        if (!resolutionDropdown.IsExpanded)
        {
            _resolutionListScrolled = false;
            return;
        }

        if (_resolutionListScrolled) return;

        Transform list = resolutionDropdown.transform.Find("Dropdown List");
        if (list == null) return;

        var scroll = list.GetComponent<ScrollRect>();
        if (scroll != null && resolutionDropdown.options.Count > 1)
        {
            float t = resolutionDropdown.value / (float)(resolutionDropdown.options.Count - 1);
            scroll.verticalNormalizedPosition = 1f - t;
        }

        _resolutionListScrolled = true;
    }

    /// <summary>
    /// Records the pick. Like every other setting on the panel it is only a pending value until the
    /// player saves, which is where the screen actually changes -- see <see cref="ConfirmDisplay"/>.
    /// </summary>
    private void OnResolutionPicked(int index)
    {
        if (_seedingResolution) return;
        if (index < 0 || index >= _resolutionOptions.Count) return;

        Vector2Int size = _resolutionOptions[index];
        GameSettings.SetResolution(size.x, size.y);
    }
}
