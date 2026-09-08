using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The in-match pause menu. Escape or the on-screen toggle opens it, either one (or Resume) closes it
/// again, and the buttons cover the only things there are to do from a paused match: carry on, start it
/// over, leave for the title screen, quit the game, or read up on how to play.
///
/// The rules and how-to-play panels are NOT part of the menu's resting state: they are authored on but
/// switched off at Awake and only come back for as long as the How To Play button is latched on, so a
/// pause is a short list of buttons rather than a wall of text. They sit in their own column left of the
/// buttons, which is why the buttons stay up alongside them — the toggle that opened them has to remain
/// clickable to shut them again, and it holds itself down while they are up.
///
/// This component lives on an ALWAYS-ACTIVE root whose <see cref="panel"/> child is the thing shown and
/// hidden. The panel has to be authored inactive — nothing should be on screen at kickoff — and Update
/// never runs on a disabled object, so the Escape key has to be read from somewhere that stays enabled.
/// Same problem <see cref="CardSelectionPanel"/> solves with a static bootstrap; an always-on parent is
/// the cheaper answer here because this menu opens on a key rather than on an event.
///
/// Pausing is <see cref="Time.timeScale"/> = 0, which is enough to freeze this particular game: every
/// animation is either a DOTween tween (scaled time unless it opts out) or a coroutine yielding on
/// WaitForSeconds, and the opponent's whole turn is one of those coroutines.
///
/// What timeScale does NOT stop is input, so the three input paths are shut off separately:
///   - uGUI clicks (dragging a card out of hand, end turn) are eaten by the panel's full-screen backdrop,
///     the same trick CardSelectionPanel's backdrop plays.
///   - Legacy OnMouse* board hover is killed by <see cref="BoardInteractionGate"/>, which reads
///     <see cref="IsOpen"/>.
///   - Right-click cancel is polled directly in GameManager.Update, which also checks
///     <see cref="IsOpen"/>.
/// </summary>
public class EscMenuController : MonoBehaviour
{
    [Tooltip("The menu itself: backdrop, frame, title and buttons. Authored inactive — this component " +
             "sits on the always-active parent so it can keep reading the Escape key.")]
    [SerializeField] private GameObject panel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Tooltip("Sits under the other buttons and shows the rules alongside them. Not a scene change and " +
             "not a modal — it only switches the two reading panels on and off. Carries a " +
             "ToggleButton so it stays visibly held down for as long as they are up.")]
    [SerializeField] private Button howToPlayButton;

    [Tooltip("Opens the settings in the same column the rules use, and on the same terms: a latched " +
             "toggle, not a scene change and not a modal.")]
    [SerializeField] private Button settingsButton;

    [Header("Reading panels")]
    [Tooltip("Built from Docs/rules-panel.md by Tools ▸ Esc Menu ▸ Sync Rules Panel.")]
    [SerializeField] private GameObject rulesPanel;

    [Tooltip("Built from Docs/how-to-play-panel.md by Tools ▸ Esc Menu ▸ Sync How To Play Panel.")]
    [SerializeField] private GameObject howToPlayPanel;

    [Header("Settings")]
    [Tooltip("Shares the left column with the reading panels — only ever one of the two is up. Built by " +
             "Tools ▸ Settings ▸ Rebuild Settings Panel.")]
    [SerializeField] private SettingsPanelController settingsPanel;


    [Header("On-screen toggle")]
    [Tooltip("Corner button that opens and closes the menu, for players who don't reach for Escape. It is " +
             "a SIBLING of the panel rather than a child of it, ordered after it — that is what keeps it " +
             "drawn above the backdrop and clickable while the menu is open.")]
    [SerializeField] private Button toggleButton;

    [SerializeField] private string openLabel = "Menu";
    [SerializeField] private string closeLabel = "Close";

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "MainMenu";

    /// <summary>
    /// True while the pause menu is on screen. Static so the input sources timeScale cannot reach can ask
    /// without holding a reference to a menu that only exists in the match scene.
    /// </summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// What the game was running at before we paused. Read rather than assumed to be 1 so a slow-motion or
    /// fast-forward debug setting survives a trip through the menu.
    /// </summary>
    private float _resumeTimeScale = 1f;

    /// <summary>
    /// The toggle's caption. Resolved rather than serialised, the way CardSelectionPanel picks up its See
    /// Board label, so restyling the button in the prefab needs no extra inspector wiring.
    /// </summary>
    private TMPro.TextMeshProUGUI _toggleLabel;

    /// <summary>
    /// The latch on the How To Play button, when it is the authored toggle rather than a plain Button.
    /// Same arrangement as <see cref="CardSelectionPanel"/>'s See Board button: the toggle owns the click
    /// and reports the state it moved to, so nothing here has to invert anything.
    /// </summary>
    private ToggleButton _howToPlayToggle;

    /// <summary>The latch on the Settings button, on the same terms as <see cref="_howToPlayToggle"/>.</summary>
    private ToggleButton _settingsToggle;

    /// <summary>Whether the rules and how-to-play panels are currently showing.</summary>
    private bool _infoOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ResetState()
    {
        // Statics survive between play sessions when Enter Play Mode Options skips the domain reload, so a
        // session that ended paused would otherwise start the next one with the board inert and the
        // right-click cancel dead. Same reasoning as BoardInteractionGate.Bootstrap.
        IsOpen = false;
    }

    private void Awake()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ExitToMenu);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);

        if (howToPlayButton != null)
        {
            _howToPlayToggle = howToPlayButton.GetComponent<ToggleButton>();

            // The toggle has already flipped itself by the time it reports in, so take the state it hands
            // over — routing that through ToggleHowToPlay would flip a second time and cancel it out.
            if (_howToPlayToggle != null) _howToPlayToggle.onValueChanged.AddListener(ShowInfo);
            else howToPlayButton.onClick.AddListener(ToggleHowToPlay);
        }

        if (settingsButton != null)
        {
            _settingsToggle = settingsButton.GetComponent<ToggleButton>();

            if (_settingsToggle != null) _settingsToggle.onValueChanged.AddListener(ShowSettings);
            else settingsButton.onClick.AddListener(ToggleSettings);
        }

        // The panel can also close itself — its own Close button — and the latch has to come back up
        // when it does.
        if (settingsPanel != null) settingsPanel.Closed += OnSettingsClosed;

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(Toggle);
            _toggleLabel = toggleButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        }

        // Always start the match unpaused, however the panel was left in the editor. The reading panels
        // are authored ON so they can be laid out in the prefab; this is where they get put away.
        if (panel != null) panel.SetActive(false);
        IsOpen = false;
        ShowInfo(false);
        ShowSettings(false);

        RefreshToggle();
    }

    private void OnDisable()
    {
        // Never leave the game frozen behind us. This runs on scene unload and on leaving play mode, both
        // of which can happen with the menu still open.
        if (IsOpen) Resume();
    }

    private void OnDestroy()
    {
        if (settingsPanel != null) settingsPanel.Closed -= OnSettingsClosed;
    }

    private void Update()
    {
        // The toggle is drawn above every other panel on this canvas, so it has to take ITSELF off screen
        // once the match is decided — otherwise it floats over the end-game panel doing nothing when
        // clicked, because Open refuses at that point. Re-checked per frame rather than hooked to an
        // end-of-match event for the same reason BoardInteractionGate recomputes its gate: a predicate
        // cannot get out of sync, a subscription can.
        if (toggleButton != null)
        {
            bool show = IsOpen || !MatchIsOver();
            if (toggleButton.gameObject.activeSelf != show) toggleButton.gameObject.SetActive(show);
        }

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // Escape backs out one layer at a time: it closes whatever is up in the left column first and the
        // menu second, so reading the rules — or setting the volume — is never a one-way trip into
        // leaving the match by accident.
        if (IsOpen && _infoOpen) ShowInfo(false);
        else if (IsOpen && settingsPanel != null && settingsPanel.IsOpen) ShowSettings(false);
        else Toggle();
    }

    /// <summary>Open the menu if it is shut, shut it if it is open. What both Escape and the corner button do.</summary>
    public void Toggle()
    {
        if (IsOpen) Resume();
        else Open();
    }

    public void Open()
    {
        if (IsOpen || panel == null) return;
        if (MatchIsOver()) return;

        _resumeTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        panel.SetActive(true);

        // Every pause starts on the buttons, whatever was left open last time.
        ShowInfo(false);
        ShowSettings(false);

        // Above everything else on the canvas, including a card selection panel left open mid-card. Done
        // on open rather than authored, because sibling order is a property of whatever else the scene has
        // added to the canvas since.
        transform.SetAsLastSibling();

        IsOpen = true;
        RefreshToggle();
    }

    public void Resume()
    {
        if (panel != null) panel.SetActive(false);
        ShowInfo(false);
        ShowSettings(false);

        // Guarded, so a Resume that closes nothing — OnDisable firing on an already-closed menu — cannot
        // overwrite a timeScale somebody else owns.
        if (IsOpen) Time.timeScale = _resumeTimeScale;

        IsOpen = false;
        RefreshToggle();
    }

    private void RefreshToggle()
    {
        if (_toggleLabel != null) _toggleLabel.text = IsOpen ? closeLabel : openLabel;
    }

    /// <summary>Show the rules next to the buttons, or put them away again. What the How To Play button does.</summary>
    public void ToggleHowToPlay() => ShowInfo(!_infoOpen);

    private void ShowInfo(bool show)
    {
        _infoOpen = show;
        if (rulesPanel != null) rulesPanel.SetActive(show);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(show);

        // Silent, because every other way of closing the panels — Escape, Resume, a fresh pause — has to
        // put the latch back up without being reported straight back in here.
        if (_howToPlayToggle != null) _howToPlayToggle.SetIsOn(show, notify: false);

        // The rules and the settings share the left column, so opening one puts the other away rather
        // than drawing both into the same space.
        if (show) ShowSettings(false);
    }

    /// <summary>Show the settings next to the buttons, or put them away again. What the Settings button does.</summary>
    public void ToggleSettings() => ShowSettings(settingsPanel != null && !settingsPanel.IsOpen);

    private void ShowSettings(bool show)
    {
        if (settingsPanel != null) settingsPanel.SetOpen(show);

        if (_settingsToggle != null) _settingsToggle.SetIsOn(show, notify: false);

        if (show) ShowInfo(false);
    }

    /// <summary>The panel closed itself (its own Close button). Nothing to shut, just the latch to raise.</summary>
    private void OnSettingsClosed()
    {
        if (_settingsToggle != null) _settingsToggle.SetIsOn(false, notify: false);
    }

    /// <summary>Rerun the match with the decks and heroes already chosen, exactly as Play Again does.</summary>
    public void Restart() => LeaveTo(gameSceneName);

    public void ExitToMenu() => LeaveTo(menuSceneName);

    public void Quit()
    {
        // Application.Quit still runs a frame or two on the way out; don't hand it a frozen clock.
        Resume();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Unpausing FIRST is not tidiness: SceneTransitionManager's fade is a coroutine driven by
    /// Time.deltaTime, so kicking off a transition at timeScale 0 leaves the screen stuck part-faded with
    /// the next scene never loading.
    /// </summary>
    private void LeaveTo(string sceneName)
    {
        Resume();

        // Same optional-transition handling as MainMenuManager: a scene entered without a
        // SceneTransitionManager still has to be able to leave.
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.TransitionToScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Whether the match has already been decided. The end-game panel is its own modal and already offers
    /// Play Again / Proceed, so stacking a pause menu over it would only be a second way to leave a match
    /// that is over. Checked on the state as well as the panel because the panel opens on a delay.
    /// </summary>
    private static bool MatchIsOver()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.currentState == GameState.EndGame) return true;

        var popup = PopupManager.Instance;
        return popup != null && popup.HasOpenPopup;
    }
}
