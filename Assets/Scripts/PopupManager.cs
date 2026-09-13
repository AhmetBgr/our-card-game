using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : Singleton<PopupManager>
{
    [Header("End-game popup (the same panel is used for victory and defeat)")]
    public Transform gameOverPopup;

    [Header("Titles - only the one matching the outcome is enabled")]
    public Transform victoryTitle;
    public Transform defeatTitle;

    [Header("Buttons")]
    public Button replayButton;
    [Tooltip("The panel's 'Proceed' button: leaves the match and returns to the main menu.")]
    public Button exitButton;

    [Header("Scenes")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("Timing")]
    [Tooltip("Seconds to wait before the end-game panel scales in, so the killing blow and the hero's " +
             "death get to play out on a clear board first.")]
    [SerializeField] private float gameOverDelay = 2f;

    private Transform curPopup = null;

    /// <summary>
    /// True while a popup is on screen, so <see cref="BoardInteractionGate"/> can suppress board
    /// interaction underneath it. Note this stays true until <see cref="CloseCurPopup"/>'s scale-out
    /// tween finishes and clears the field — the board should stay inert while the panel is still
    /// visibly shrinking, so that lag is the wanted behaviour rather than an oversight.
    /// </summary>
    public bool HasOpenPopup => curPopup != null;

    void Start()
    {
        if (replayButton != null) replayButton.onClick.AddListener(Replay);
        if (exitButton != null) exitButton.onClick.AddListener(ExitToMenu);
    }

    // Replay reruns the match with the decks and heroes already chosen for both sides.
    public void Replay()
    {
        SceneTransitionManager.Instance.TransitionToScene("Game");
    }

    // Proceed leaves the match for the main menu, from which the next match can either be started
    // straight away (Quick Play) or reconfigured (Custom Game).
    public void ExitToMenu()
    {
        SceneTransitionManager.Instance.TransitionToScene(menuSceneName);
    }

    // Win and loss show the identical panel (background, stats, buttons); the only difference is
    // which title transform is enabled.
    // Leave delay null to use the inspector's gameOverDelay; pass an explicit value to override it
    // (the editor debug triggers pass 0 so the preview is instant).
    public void OpenGameOverPopup(bool won, float? delay = null)
    {
        if (gameOverPopup == null) return;

        if (victoryTitle != null) victoryTitle.gameObject.SetActive(won);
        if (defeatTitle != null) defeatTitle.gameObject.SetActive(!won);

        GameOver?.Invoke(won);

        OpenPopup(gameOverPopup, won, delay ?? gameOverDelay);
    }

    /// <summary>
    /// Fired when the end-game panel opens, with the outcome. Purely additive: consumed by the audio
    /// system, no core logic depends on it. This is the single funnel every ending goes through -- the
    /// real win check and both editor debug triggers -- which is why the hook lives here rather than in
    /// GameManager.CheckWinCondition.
    ///
    /// Note the panel is opened with a delay, so the stinger deliberately leads the visual.
    /// </summary>
    public static event System.Action<bool> GameOver;

    public void OpenPopup(Transform popup, bool won = false, float delay = 0f)
    {
        CloseCurPopup();
        curPopup = popup;

        curPopup.gameObject.SetActive(true);
        curPopup.localScale = Vector3.zero;

        // Populate the end-game stats/score on this panel, if it has a view. Additive: does nothing on
        // panels without a MatchStatsView. Covers both real wins/losses and the editor debug triggers.
        const float scaleInDuration = 0.5f;

        var statsView = popup.GetComponentInChildren<MatchStatsView>(true);
        // The stats view reveals its rows one by one; hold them until the panel has finished scaling in.
        if (statsView != null) statsView.Show(won, delay + scaleInDuration);

        curPopup.DOScale(1f, scaleInDuration).SetDelay(delay);
    }

    public void CloseCurPopup()
    {
        if (curPopup == null) return;

        curPopup.DOScale(0f, 0.5f).OnComplete(() =>
        {
            curPopup.gameObject.SetActive(false);
            curPopup = null;
        });
    }
}
