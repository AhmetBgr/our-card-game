using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The milestone panel: an XP bar with one <see cref="MilestoneNode"/> per milestone of the run, placed
/// along it by XP, under a lock overlay that slides off to the left once the track unlocks.
/// Purely a view: <see cref="DraftController"/> feeds it the run's state and handles hover and click
/// through <see cref="NodeHovered"/> and <see cref="NodeClicked"/>.
///
/// It is used twice: the Draft scene drives it with <see cref="Show"/>, which tweens the bar up to the
/// run's XP; the copy under the end-of-match panel is driven frame by frame with
/// <see cref="ShowDriven"/> / <see cref="SetXp"/>, so its bar fills together with the score being tallied.
/// </summary>
public class MilestoneTrack : MonoBehaviour
{
    [Header("Lock")]
    [Tooltip("Covers the track until the first battle is won, then slides off to the left.")]
    [SerializeField] private RectTransform lockOverlay;
    [SerializeField] private float lockSlideDuration = 0.6f;
    [SerializeField] private Ease lockSlideEase = Ease.InBack;
    [Tooltip("Wait before the lock slides, so the scene transition has finished.")]
    [SerializeField] private float lockSlideDelay = 0.4f;

    [Header("XP bar")]
    [Tooltip("The bar's full extent: XP 0 at its left edge, the last milestone at its right edge.")]
    [SerializeField] private RectTransform barArea;
    [Tooltip("Filled image (Horizontal) showing the run's XP.")]
    [SerializeField] private Image barFill;
    [SerializeField] private TextMeshProUGUI xpText;
    [SerializeField] private string xpFormat = "{0} XP";
    [Tooltip("Seconds the bar takes to fill up to newly gained XP.")]
    [SerializeField] private float xpFillDuration = 1f;

    [Header("Nodes")]
    [Tooltip("Cloned once per milestone; kept hidden.")]
    [SerializeField] private MilestoneNode nodeTemplate;

    public event System.Action<int, bool> NodeHovered;
    public event System.Action<int> NodeClicked;

    private readonly List<MilestoneNode> _nodes = new List<MilestoneNode>();
    private IReadOnlyList<GauntletRun.MilestoneState> _milestones;
    private Vector2 _lockHome;
    private bool _lockHomeKnown;
    private Sequence _sequence;
    private int _maxXp = 1;

    /// <summary>The nodes were built while the track had no size yet; lay them out once it has.</summary>
    private bool _nodesNeedLayout;

    /// <summary>
    /// Shows the run's milestones and XP. <paramref name="fromXp"/> below <paramref name="toXp"/> fills the
    /// bar up from there; <paramref name="revealLock"/> slides the lock away first.
    /// </summary>
    public void Show(IReadOnlyList<GauntletRun.MilestoneState> milestones, int fromXp, int toXp, bool unlocked, bool revealLock)
    {
        _milestones = milestones;
        _sequence?.Kill();
        BuildNodes();

        if (lockOverlay != null && !_lockHomeKnown)
        {
            _lockHome = lockOverlay.anchoredPosition;
            _lockHomeKnown = true;
        }

        bool animateXp = unlocked && toXp > fromXp;
        ApplyXp(animateXp ? fromXp : toXp, punch: false);

        if (!unlocked)
        {
            SetLock(true);
            return;
        }

        _sequence = DOTween.Sequence();
        if (revealLock && lockOverlay != null)
        {
            SetLock(true);
            float width = lockOverlay.rect.width * lockOverlay.localScale.x;
            _sequence.AppendInterval(lockSlideDelay);
            _sequence.Append(lockOverlay.DOAnchorPosX(_lockHome.x - width, lockSlideDuration).SetEase(lockSlideEase));
            _sequence.AppendCallback(() => lockOverlay.gameObject.SetActive(false));
        }
        else
        {
            SetLock(false);
        }

        if (animateXp)
        {
            if (!revealLock) _sequence.AppendInterval(lockSlideDelay);
            _sequence.Append(DOVirtual.Float(fromXp, toXp, xpFillDuration, v => ApplyXp(Mathf.RoundToInt(v), punch: true))
                .SetEase(Ease.OutCubic));
        }
    }

    /// <summary>
    /// Shows the track with its XP driven from outside instead of by a tween of its own: the end-of-match
    /// panel feeds it the score as it counts up (see BattleXpTrack) through <see cref="SetXp"/>. The lock
    /// still slides off by itself when <paramref name="revealLock"/>.
    /// </summary>
    public void ShowDriven(IReadOnlyList<GauntletRun.MilestoneState> milestones, int xp, bool revealLock)
    {
        _milestones = milestones;
        _sequence?.Kill();
        BuildNodes();

        if (lockOverlay != null && !_lockHomeKnown)
        {
            _lockHome = lockOverlay.anchoredPosition;
            _lockHomeKnown = true;
        }

        ApplyXp(xp, punch: false);

        if (!revealLock || lockOverlay == null)
        {
            SetLock(false);
            return;
        }

        SetLock(true);
        float width = lockOverlay.rect.width * lockOverlay.localScale.x;
        _sequence = DOTween.Sequence();
        _sequence.AppendInterval(lockSlideDelay);
        _sequence.Append(lockOverlay.DOAnchorPosX(_lockHome.x - width, lockSlideDuration).SetEase(lockSlideEase));
        _sequence.AppendCallback(() => lockOverlay.gameObject.SetActive(false));
    }

    /// <summary>The XP the bar shows, for a track driven from outside. Nodes punch as they are reached.</summary>
    public void SetXp(int xp) => ApplyXp(xp, punch: true);

    /// <summary>Re-reads the milestones' claimed state without touching the XP shown.</summary>
    public void RefreshStates()
    {
        if (_milestones == null) return;
        for (int i = 0; i < _nodes.Count && i < _milestones.Count; i++)
            ApplyNodeState(i, _milestones[i].Reached, punch: false);
    }

    private void SetLock(bool locked)
    {
        if (lockOverlay == null) return;
        lockOverlay.DOKill();
        lockOverlay.anchoredPosition = _lockHome;
        lockOverlay.gameObject.SetActive(locked);
    }

    private void ApplyXp(int xp, bool punch)
    {
        if (barFill != null) barFill.fillAmount = Mathf.Clamp01(xp / (float)_maxXp);
        if (xpText != null) xpText.text = string.Format(xpFormat, xp);

        if (_milestones == null) return;
        for (int i = 0; i < _nodes.Count && i < _milestones.Count; i++)
            ApplyNodeState(i, xp >= _milestones[i].Xp, punch);
    }

    /// <param name="reached">Reached as far as the bar has filled so far, which may trail the run's XP.</param>
    private void ApplyNodeState(int index, bool reached, bool punch)
    {
        var milestone = _milestones[index];
        bool empty = milestone.Reward == MilestoneReward.None;
        bool claimable = reached && !milestone.Claimed && !empty;
        _nodes[index].SetState(reached, milestone.Claimed, claimable, punch);
    }

    private void BuildNodes()
    {
        if (nodeTemplate == null || barArea == null || _milestones == null) return;
        nodeTemplate.gameObject.SetActive(false);

        _maxXp = 1;
        foreach (var m in _milestones) _maxXp = Mathf.Max(_maxXp, m.Xp);

        while (_nodes.Count < _milestones.Count)
        {
            var node = Instantiate(nodeTemplate, nodeTemplate.transform.parent);
            int index = _nodes.Count;
            Hook(node, index);
            _nodes.Add(node);
        }

        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            bool used = i < _milestones.Count;
            node.gameObject.SetActive(used);
            if (!used) continue;

            var milestone = _milestones[i];
            node.SetLabels(GauntletRun.MilestoneRewardName(milestone.Reward), milestone.Xp.ToString());
        }

        LayoutNodes();
    }

    /// <summary>
    /// Spreads the nodes along the bar by XP. The bar's extent is read in world space, so it is only
    /// meaningful once the track is actually drawn at size: under the end-of-match panel it is built
    /// while the panel is still scaled to zero, which collapses every corner onto one point and would
    /// stack the whole track in one spot. In that case the layout waits for the scale-in (see
    /// <see cref="LateUpdate"/>).
    /// </summary>
    private void LayoutNodes()
    {
        if (nodeTemplate == null || barArea == null || _milestones == null) return;

        if (transform.lossyScale.x <= 0.0001f)
        {
            _nodesNeedLayout = true;
            return;
        }
        _nodesNeedLayout = false;

        // The bar's local x-range in the nodes' parent, so a node sits right above its XP on the bar.
        var parent = (RectTransform)nodeTemplate.transform.parent;
        var corners = new Vector3[4];
        barArea.GetWorldCorners(corners);
        float left = parent.InverseTransformPoint(corners[0]).x;
        float right = parent.InverseTransformPoint(corners[3]).x;

        var template = (RectTransform)nodeTemplate.transform;
        for (int i = 0; i < _nodes.Count && i < _milestones.Count; i++)
        {
            var rt = (RectTransform)_nodes[i].transform;
            Vector3 local = template.localPosition;
            local.x = Mathf.Lerp(left, right, _milestones[i].Xp / (float)_maxXp);
            rt.localPosition = local;
        }
    }

    void LateUpdate()
    {
        if (_nodesNeedLayout) LayoutNodes();
    }

    private void Hook(MilestoneNode node, int index)
    {
        var target = node.Background != null ? node.Background.gameObject : node.gameObject;
        if (node.Background != null) node.Background.raycastTarget = true;

        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            node.SetHovered(true);
            NodeHovered?.Invoke(index, true);
        });
        AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            node.SetHovered(false);
            NodeHovered?.Invoke(index, false);
        });
        AddTrigger(trigger, EventTriggerType.PointerClick, () => NodeClicked?.Invoke(index));
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    void OnDestroy()
    {
        _sequence?.Kill();
        if (lockOverlay != null) lockOverlay.DOKill();
    }
}
