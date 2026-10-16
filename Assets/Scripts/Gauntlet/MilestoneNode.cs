using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One milestone on the Draft scene's progression track: the reward's name, the XP it sits at, a glow
/// while it is reached but unclaimed, and a mark once claimed. <see cref="MilestoneTrack"/> clones it
/// from a template per milestone and positions it; this only shows the state it is given.
/// </summary>
public class MilestoneNode : MonoBehaviour
{
    [Tooltip("Hover / click target, tinted by state.")]
    [SerializeField] private Graphic background;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI xpText;
    [Tooltip("Shown while the milestone is reached but not yet claimed.")]
    [SerializeField] private GameObject claimableHighlight;
    [Tooltip("Shown once the milestone is claimed.")]
    [SerializeField] private GameObject claimedMark;

    [SerializeField] private Color unreachedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color reachedTint = Color.white;
    [SerializeField] private Color claimedTint = new Color(0.4f, 0.4f, 0.4f, 1f);

    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverDuration = 0.1f;
    [Tooltip("Scale punch when the milestone becomes reached while the XP bar fills.")]
    [SerializeField] private float reachPunch = 0.25f;

    private bool _reached;
    private bool _stateShown;

    public Graphic Background => background;

    public void SetLabels(string reward, string xp)
    {
        if (rewardText != null) rewardText.text = reward;
        if (xpText != null) xpText.text = xp;
    }

    /// <param name="punchIfNewlyReached">Punches the node when this call is the one that reaches it.</param>
    public void SetState(bool reached, bool claimed, bool claimable, bool punchIfNewlyReached = false)
    {
        bool newlyReached = _stateShown && reached && !_reached;
        _reached = reached;
        _stateShown = true;

        if (background != null) background.color = claimed ? claimedTint : reached ? reachedTint : unreachedTint;
        if (claimableHighlight != null) claimableHighlight.SetActive(claimable);
        if (claimedMark != null) claimedMark.SetActive(claimed);

        if (newlyReached && punchIfNewlyReached)
        {
            transform.DOKill(true);
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * reachPunch, 0.3f, 6, 0.5f);
        }
    }

    public void SetHovered(bool hovered)
    {
        transform.DOKill(true);
        transform.DOScale(hovered ? hoverScale : 1f, hoverDuration);
    }

    void OnDestroy() => transform.DOKill();
}
