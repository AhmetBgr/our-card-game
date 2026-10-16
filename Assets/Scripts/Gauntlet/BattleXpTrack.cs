using DG.Tweening;
using UnityEngine;

/// <summary>
/// The run's milestone track under the end-of-match panel: it fills as the panel tallies the score,
/// because that score is exactly the XP the won battle is about to add (<see cref="GauntletRun.Xp"/> is
/// only credited on the way out, in <see cref="GauntletRun.CollectScore"/>).
///
/// It rides on <see cref="MatchStatsView.ShownScoreChanged"/> rather than running a tween of its own, so
/// the bar, the nodes it passes and the number on the panel can never drift apart -- a skipped reveal
/// jumps all three at once. <see cref="PopupManager.OpenPopup"/> hands it the panel's stats view.
///
/// Only a won gauntlet battle has anything to show: outside a run, and on a defeat, the track stays
/// hidden and the panel is the plain end-of-match panel it has always been.
/// </summary>
public class BattleXpTrack : MonoBehaviour
{
    [Tooltip("The track to fill. Lives under this object, which is the panel strip that holds it.")]
    [SerializeField] private MilestoneTrack track;

    [Tooltip("Moved out of the track's way while it is up (the end-game buttons), and put back after. " +
             "Optional: leave empty to keep the panel's own layout.")]
    [SerializeField] private RectTransform buttonsArea;

    [Tooltip("Where the buttons sit while the track is up.")]
    [SerializeField] private Vector2 buttonsPositionWithTrack = new Vector2(295f, -456f);

    private Vector2 _buttonsHome;
    private bool _buttonsHomeKnown;

    /// <summary>The run's XP before this battle: the score counts up on top of it.</summary>
    private int _baseXp;

    private MatchStatsView _stats;

    /// <summary>
    /// Shows the track for a won battle of a run, following <paramref name="stats"/> as it counts up.
    /// Anything else (no run, a defeat) leaves it hidden. Call before the stats view starts revealing,
    /// so the bar is on screen at zero score rather than catching up halfway.
    /// </summary>
    public void Show(bool won, MatchStatsView stats)
    {
        Unsubscribe();

        if (!won || !GauntletRun.IsActive || !GauntletRun.MilestonesUnlocked)
        {
            Hide();
            return;
        }

        _baseXp = GauntletRun.Xp;
        _stats = stats;

        gameObject.SetActive(true);
        MoveButtonsAside(true);

        if (track != null)
        {
            // The first win is also when the track unlocks, so this panel is where the lock slides off;
            // the Draft scene then shows it already open.
            bool reveal = !GauntletRun.MilestonesRevealed;
            track.ShowDriven(GauntletRun.Milestones, _baseXp, reveal);
            GauntletRun.MilestonesRevealed = true;
        }

        if (_stats != null) _stats.ShownScoreChanged += OnScoreShown;
        else OnScoreShown(0);
    }

    public void Hide()
    {
        Unsubscribe();
        MoveButtonsAside(false);
        gameObject.SetActive(false);
    }

    /// <param name="score">The score on the panel right now; it dips below zero while penalties land.</param>
    private void OnScoreShown(int score)
    {
        if (track != null) track.SetXp(_baseXp + Mathf.Max(0, score));
    }

    private void Unsubscribe()
    {
        if (_stats != null) _stats.ShownScoreChanged -= OnScoreShown;
        _stats = null;
    }

    private void MoveButtonsAside(bool aside)
    {
        if (buttonsArea == null) return;

        if (!_buttonsHomeKnown)
        {
            _buttonsHome = buttonsArea.anchoredPosition;
            _buttonsHomeKnown = true;
        }
        buttonsArea.anchoredPosition = aside ? buttonsPositionWithTrack : _buttonsHome;
    }

    void OnDisable() => Unsubscribe();

    void OnDestroy()
    {
        Unsubscribe();
        transform.DOKill();
    }
}
