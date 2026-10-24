using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// One milestone on the Draft scene's progression track: the reward's name and icon, the XP it sits at,
/// a mark while the bar has not reached it, and another once it is claimed. <see cref="MilestoneTrack"/>
/// clones it from a template per milestone and positions it; this only shows the state it is given.
/// </summary>
public class MilestoneNode : MonoBehaviour
{
    [Tooltip("Hover / click target, tinted by state.")]
    [SerializeField] private Graphic background;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI xpText;
    [Tooltip("Shown while the bar has not reached the milestone yet; hidden once it has.")]
    [FormerlySerializedAs("claimableHighlight")]
    [SerializeField] private GameObject unreachedMark;
    [Tooltip("Shown once the milestone is claimed.")]
    [SerializeField] private GameObject claimedMark;
    [Tooltip("The reward's icon, out of the milestone reward catalog. The sprite and colour it is " +
             "authored with here are what a reward the catalog has no icon for falls back to.")]
    [SerializeField] private Image rewardIcon;

    [SerializeField] private Color unreachedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color reachedTint = Color.white;
    [SerializeField] private Color claimedTint = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Tooltip("Dim the reward icon with the tints above, so an unreached node reads as one. Off keeps " +
             "the icon at its own colour whatever the state.")]
    [SerializeField] private bool tintIconByState = true;

    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverDuration = 0.1f;
    [Tooltip("Scale punch when the milestone becomes reached while the XP bar fills.")]
    [SerializeField] private float reachPunch = 0.25f;

    private bool _reached;
    private bool _claimed;
    private bool _stateShown;

    /// <summary>The icon's authored sprite and colour, kept as the fallback for an iconless reward.</summary>
    private Sprite _authoredIcon;
    private Color _authoredIconTint = Color.white;
    private bool _authoredIconKnown;
    private Color _iconTint = Color.white;

    public Graphic Background => background;

    public void SetLabels(string reward, string xp)
    {
        if (rewardText != null) rewardText.text = reward;
        if (xpText != null) xpText.text = xp;
    }

    /// <summary>
    /// The reward's icon and the tint that goes with it, from <see cref="MilestoneRewardCatalogSO"/>. A
    /// null <paramref name="icon"/> is a reward the catalog has no icon for: the node keeps the sprite and
    /// colour it was authored with, so the node still reads as a reward rather than going blank.
    /// </summary>
    public void SetRewardIcon(Sprite icon, Color tint)
    {
        if (rewardIcon == null) return;

        if (!_authoredIconKnown)
        {
            _authoredIcon = rewardIcon.sprite;
            _authoredIconTint = rewardIcon.color;
            _authoredIconKnown = true;
        }

        bool own = icon != null;
        rewardIcon.sprite = own ? icon : _authoredIcon;
        _iconTint = own ? tint : _authoredIconTint;
        ApplyIconTint();
    }

    /// <param name="punchIfNewlyReached">Punches the node when this call is the one that reaches it.</param>
    public void SetState(bool reached, bool claimed, bool punchIfNewlyReached = false)
    {
        bool newlyReached = _stateShown && reached && !_reached;
        _reached = reached;
        _claimed = claimed;
        _stateShown = true;

        if (background != null) background.color = StateTint();
        if (unreachedMark != null) unreachedMark.SetActive(!reached);
        if (claimedMark != null) claimedMark.SetActive(claimed);
        ApplyIconTint();

        if (newlyReached && punchIfNewlyReached)
        {
            transform.DOKill(true);
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * reachPunch, 0.3f, 6, 0.5f);
        }
    }

    private Color StateTint() => _claimed ? claimedTint : _reached ? reachedTint : unreachedTint;

    /// <summary>The icon carries its catalog tint, dimmed by the state tint (whose alpha is 1, so it only darkens).</summary>
    private void ApplyIconTint()
    {
        if (rewardIcon == null) return;
        rewardIcon.color = tintIconByState ? _iconTint * StateTint() : _iconTint;
    }

    public void SetHovered(bool hovered)
    {
        transform.DOKill(true);
        transform.DOScale(hovered ? hoverScale : 1f, hoverDuration);
    }

    void OnDestroy() => transform.DOKill();
}
