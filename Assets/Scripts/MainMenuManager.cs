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
