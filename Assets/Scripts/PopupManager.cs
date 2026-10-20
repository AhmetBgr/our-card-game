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
    // straight away (Quick Play) or reconfigured (Custom Game). In a Forged in Battle run it goes back
    // to the Draft scene instead, which shows the next draft step or the run's result. A won battle that
    // owes rewards never gets here: BattleRewardsPanel opens beside the end-game panel and its own
    // Proceed replaces this one (see OpenGameOverPopup).
    public void ExitToMenu()
    {
        if (GauntletRun.IsActive)
        {
            GauntletRun.LeaveBattle();
            return;
        }

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

        // A run's battle can't be replayed: a loss ends the run, a win moves on to the next draft.
        if (replayButton != null) replayButton.gameObject.SetActive(!GauntletRun.IsActive);

        float resolvedDelay = delay ?? gameOverDelay;

        // Hold the outcome hook back by the same delay the panel waits, so the stinger hits on the first
        // frame of the scale-in rather than over the empty board. Scaled time, matching the tween below.
        if (resolvedDelay > 0f) DOVirtual.DelayedCall(resolvedDelay, () => GameOver?.Invoke(won), false);
        else GameOver?.Invoke(won);

        float openDuration = OpenPopup(gameOverPopup, won, resolvedDelay);

        // A won run battle opens its rewards beside the panel, scaling in with it, and milestone rows land
        // as the panel's XP track fills. Must follow OpenPopup: the track reads its base XP there, before
        // the rewards panel credits the battle's score. The rewards panel's Proceed is the way out, so the
        // panel's own is hidden while it is up; a loss or a finished run keeps it.
        bool rewardsShown = false;
        if (won && GauntletRun.IsActive)
        {
            var rewards = BattleRewardsPanel.Resolve();
            if (rewards != null)
            {
                var xpTrack = gameOverPopup.GetComponentInChildren<BattleXpTrack>(true);
                // Its rows wait out the panel's entrance: the column they sit in is still sliding in.
                rewardsShown = rewards.Show(resolvedDelay + openDuration, xpTrack,
                    gameOverPopup.GetComponent<CanvasGroup>());
            }
        }
        if (exitButton != null) exitButton.gameObject.SetActive(!rewardsShown);
    }

    /// <summary>
    /// Fired when the end-game panel opens, with the outcome. Purely additive: consumed by the audio
    /// system, no core logic depends on it. This is the single funnel every ending goes through -- the
    /// real win check and both editor debug triggers -- which is why the hook lives here rather than in
    /// GameManager.CheckWinCondition.
    ///
    /// Note this fires after the panel's open delay has elapsed, not when the match ends, so the stinger
    /// lands together with the scale-in animation.
    /// </summary>
    public static event System.Action<bool> GameOver;

    /// <summary>Opens <paramref name="popup"/>, and returns how long it takes to finish arriving.</summary>
    public float OpenPopup(Transform popup, bool won = false, float delay = 0f)
    {
        CloseCurPopup();
        curPopup = popup;

        curPopup.gameObject.SetActive(true);

        // Populate the end-game stats/score on this panel, if it has a view. Additive: does nothing on
        // panels without a MatchStatsView. Covers both real wins/losses and the editor debug triggers.
        const float scaleInDuration = 0.5f;

        // A panel with an entrance of its own -- the end-of-match panel, whose title and columns each
        // arrive separately -- opens at full size and poses itself; everything else scales in whole.
        var intro = popup.GetComponent<GameOverPanelIntro>();
        float openDuration = intro != null ? intro.Duration : scaleInDuration;
        curPopup.localScale = intro != null ? Vector3.one : Vector3.zero;

        var statsView = popup.GetComponentInChildren<MatchStatsView>(true);

        // In a run, the panel also carries the milestone track, which fills with the score being tallied.
        // Wired before the reveal starts so it is on screen from the first number, and left to itself on
        // panels without one.
        var xpTrack = popup.GetComponentInChildren<BattleXpTrack>(true);
        if (xpTrack != null) xpTrack.Show(won, statsView);

        // The stats view reveals its rows one by one; hold them until the panel has finished arriving.
        if (statsView != null) statsView.Show(won, delay + openDuration);

        // Posed for the entrance now, played after the delay, so the board is clear of the panel until then.
        if (intro != null) intro.Play(delay);
        else curPopup.DOScale(1f, scaleInDuration).SetDelay(delay);

        return openDuration;
    }

    public void CloseCurPopup()
    {
        if (curPopup == null) return;

        var intro = curPopup.GetComponent<GameOverPanelIntro>();
        if (intro != null) intro.Stop();

        curPopup.DOScale(0f, 0.5f).OnComplete(() =>
        {
            curPopup.gameObject.SetActive(false);
            curPopup = null;
        });
    }
}
