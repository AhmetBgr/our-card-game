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
    public Button exitButton;

    [Header("Scenes")]
    [Tooltip("Scene the Exit button returns to: the match-setup scene, which reopens on the player's step.")]
    [SerializeField] private string setupSceneName = "CreateCustomGame";

    private Transform curPopup = null;

    void Start()
    {
        if (replayButton != null) replayButton.onClick.AddListener(Replay);
        if (exitButton != null) exitButton.onClick.AddListener(ExitToSetup);
    }

    // Replay reruns the match with the decks and heroes already chosen for both sides.
    public void Replay()
    {
        SceneTransitionManager.Instance.TransitionToScene("Game");
    }

    // Exit goes back to match setup so both sides can be reconfigured, rather than to the legacy Menu.
    public void ExitToSetup()
    {
        SceneTransitionManager.Instance.TransitionToScene(setupSceneName);
    }

    // Win and loss show the identical panel (background, stats, buttons); the only difference is
    // which title transform is enabled.
    public void OpenGameOverPopup(bool won, float delay = 0f)
    {
        if (gameOverPopup == null) return;

        if (victoryTitle != null) victoryTitle.gameObject.SetActive(won);
        if (defeatTitle != null) defeatTitle.gameObject.SetActive(!won);

        OpenPopup(gameOverPopup, won, delay);
    }

    public void OpenPopup(Transform popup, bool won = false, float delay = 0f)
    {
        CloseCurPopup();
        curPopup = popup;

        curPopup.gameObject.SetActive(true);
        curPopup.localScale = Vector3.zero;

        // Populate the end-game stats/score on this panel, if it has a view. Additive: does nothing on
        // panels without a MatchStatsView. Covers both real wins/losses and the editor debug triggers.
        var statsView = popup.GetComponentInChildren<MatchStatsView>(true);
        if (statsView != null) statsView.Show(won);

        curPopup.DOScale(1f, 0.5f).SetDelay(delay);
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
