using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the title screen: Quick Play drops straight into a match with a random hero and a freshly
/// rolled deck on both sides, Custom Game opens the full setup scene, Quit leaves the game.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button quickPlayButton;
    [SerializeField] private Button customGameButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Credits")]
    [Tooltip("Overlay shown by the Credits button. Its backdrop blocks the menu underneath.")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button closeCreditsButton;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string customGameSceneName = "CreateCustomGame";

    void Start()
    {
        // Checked before the buttons are wired: when it fires, this scene is already on its way out and
        // is never shown, so there is nothing here for the player to press.
        if (TryStartTutorial()) return;

        if (quickPlayButton != null)
            quickPlayButton.onClick.AddListener(OnQuickPlay);

        if (customGameButton != null)
            customGameButton.onClick.AddListener(() => GoToScene(customGameSceneName));

        if (creditsButton != null)
            creditsButton.onClick.AddListener(() => ShowCredits(true));

        if (closeCreditsButton != null)
            closeCreditsButton.onClick.AddListener(() => ShowCredits(false));

        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);

        // Always open on the menu itself, however the panel was left in the editor.
        ShowCredits(false);
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
        if (creditsPanel != null && creditsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            ShowCredits(false);
    }

    void ShowCredits(bool show)
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(show);
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
