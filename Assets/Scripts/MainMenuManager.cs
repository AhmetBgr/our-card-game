using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the title screen. Play opens a popup with the game modes: Quick Play drops straight into a
/// match with a random hero and a freshly rolled deck on both sides, Custom Game opens the full setup
/// scene, and Forged in Battle is a placeholder for a mode that is not written yet. Settings and Credits
/// open their own overlays, Quit leaves the game.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("Opens the Play popup, where the game modes are chosen.")]
    [SerializeField] private Button playButton;

    [Tooltip("Sits in the column under Play, and opens the settings panel over the menu.")]
    [SerializeField] private Button settingsButton;

    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Play")]
    [Tooltip("Overlay shown by the Play button, holding one button per game mode. Its backdrop blocks the " +
             "menu underneath.")]
    [SerializeField] private GameObject playPanel;
    [SerializeField] private Button quickPlayButton;
    [SerializeField] private Button customGameButton;

    [Tooltip("Not a mode yet. Kept non-interactable from here, so nobody can switch it on in the scene " +
             "before there is anything behind it.")]
    [SerializeField] private Button forgedInBattleButton;

    [SerializeField] private Button closePlayButton;

    [Header("Credits")]
    [Tooltip("Overlay shown by the Credits button. Its backdrop blocks the menu underneath.")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button closeCreditsButton;

    [Header("Settings")]
    [Tooltip("The same panel the pause menu opens, over its own backdrop. Built by " +
             "Tools ▸ Settings ▸ Rebuild Settings Panels.")]
    [SerializeField] private SettingsPanelController settingsPanel;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string customGameSceneName = "CreateCustomGame";

    void Start()
    {
        // Checked before the buttons are wired: when it fires, this scene is already on its way out and
        // is never shown, so there is nothing here for the player to press.
        if (TryStartTutorial()) return;

        if (playButton != null)
            playButton.onClick.AddListener(() => ShowPlay(true));

        if (closePlayButton != null)
            closePlayButton.onClick.AddListener(() => ShowPlay(false));

        if (quickPlayButton != null)
            quickPlayButton.onClick.AddListener(OnQuickPlay);

        if (customGameButton != null)
            customGameButton.onClick.AddListener(() => GoToScene(customGameSceneName));

        // Nothing to open yet: the button is there so the player can see the mode is coming, and its
        // tooltip says as much. A dead button still gets the blocked-click sound from UISoundTrigger.
        if (forgedInBattleButton != null)
            forgedInBattleButton.interactable = false;

        if (creditsButton != null)
            creditsButton.onClick.AddListener(() => ShowCredits(true));

        if (closeCreditsButton != null)
            closeCreditsButton.onClick.AddListener(() => ShowCredits(false));

        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ToggleSettings);

        // Always open on the menu itself, however the panels were left in the editor.
        ShowPlay(false);
        ShowCredits(false);
        if (settingsPanel != null) settingsPanel.Close();
    }

    /// <summary>
    /// The tutorial hasn't been played yet, so skip the title screen entirely and drop straight into
    /// the Game scene. The match-up itself is applied there by <see cref="Agent.ApplySavedSelection"/>,
    /// not written into the save here, so it can't overwrite the player's own hero and deck choices.
    /// <see cref="GameManager.CheckWinCondition"/> flips the save flag when that match ends, so the
    /// menu behaves normally from the next visit onwards.
    /// </summary>
    /// <returns>True when the hand-off happened, so the caller can stop setting the menu up.</returns>
    bool TryStartTutorial()
    {
        if (SaveManager.Instance.IsTutorial) return false;

        // Skipped, not faded: the scene starts under a fully opaque overlay that is only fading in this
        // same frame, so a normal transition would fade the menu up and straight back down -- two seconds
        // of a title screen the player was never meant to reach. SkipToScene holds that black through the
        // load instead, and fades in on the Game scene.
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.SkipToScene(gameSceneName);
        else
            SceneManager.LoadScene(gameSceneName);

        return true;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // One layer at a time, and the settings first: they are the panel that can be opened from on top
        // of the others, so they are the one Escape has to reach first. Play and Credits never overlap --
        // each opens from the column the other's backdrop covers -- so their order is moot.
        if (settingsPanel != null && settingsPanel.IsOpen)
            settingsPanel.Close();
        else if (playPanel != null && playPanel.activeSelf)
            ShowPlay(false);
        else if (creditsPanel != null && creditsPanel.activeSelf)
            ShowCredits(false);
    }

    /// <summary>Open the settings over the menu, or put them away. What the Settings button does.</summary>
    public void ToggleSettings()
    {
        if (settingsPanel == null) return;

        // The play and credits popups are modals of their own; never leave two stacked.
        if (!settingsPanel.IsOpen)
        {
            ShowPlay(false);
            ShowCredits(false);
        }

        settingsPanel.Toggle();
    }

    void ShowPlay(bool show)
    {
        if (playPanel != null)
            playPanel.SetActive(show);
    }

    void ShowCredits(bool show)
    {
        if (creditsPanel == null) return;

        creditsPanel.SetActive(show);

        // The body scrolls; always open it at the top rather than wherever it was left.
        if (show)
        {
            var scroll = creditsPanel.GetComponentInChildren<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>
    /// Sets both sides to the mystery deck and the "Random Hero" sentinel, which is exactly the
    /// selection CreateCustomGame produces when the mystery slot and the random hero slot are picked
    /// for player and opponent alike. The mystery deck is re-rolled here because the deck panel that
    /// normally regenerates it never runs in Quick Play; the hero sentinel resolves per side in
    /// <see cref="HeroDatabase.GetSelectedHero"/> once the Game scene loads, so the two sides roll
    /// independently.
    /// </summary>
    void OnQuickPlay()
    {
        var saveManager = SaveManager.Instance;

        Randomize(saveManager, SelectionSide.Player);
        Randomize(saveManager, SelectionSide.Opponent);

        saveManager.SaveData();

        GoToScene(gameSceneName);
    }

    static void Randomize(SaveManager saveManager, SelectionSide side)
    {
        saveManager.GenerateRandomDeck(SaveManager.MysteryDeckIndex, side);
        saveManager.SetSelectedDeckIndex(side, SaveManager.MysteryDeckIndex);
        saveManager.SetSelectedHeroIndex(side, HeroDatabase.RandomHeroIndex);
    }

    // The fade transition is optional: a scene entered without a SceneTransitionManager (or one whose
    // transition object is disabled) still needs its buttons to work.
    void GoToScene(string sceneName)
    {
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.TransitionToScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
