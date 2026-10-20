using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The rewards panel of a won Forged in Battle battle: the list of what the battle paid out, opened beside
/// the end-of-match panel (<see cref="PopupManager.OpenGameOverPopup"/>), the way Slay the Spire hands out
/// a fight's rewards before the map comes back. It sits under that panel in the hierarchy, so it scales
/// in and out with it. Its Proceed replaces the end-of-match one.
///
/// It lists two kinds of reward, one row each:
///   * the new cards a win is worth (<see cref="GauntletRun.PendingBattleReward"/>): the row runs the
///     picks it owes one at a time through the match's own <see cref="CardSelectionPanel"/>, by way of
///     <see cref="CardChoice"/>, so a reward pick looks exactly like an in-match Discover. The panel
///     hides itself while the options are up and comes back between picks;
///   * every milestone the battle's XP just reached: the row is added the moment the track under the
///     end-of-match panel fills up to it (<see cref="BattleXpTrack.XpShown"/>), and claims it. Instant
///     rewards apply on the spot and the row says what they did, an
///     Upgraded Cards milestone runs its picks like the card row, and a New Perk milestone swaps the list
///     for its options until one is taken.
///
/// The battle's score is credited here rather than on the way out (<see cref="GauntletRun.CollectScore"/>),
/// so the milestones the win reaches are already reached by the time the rows are built.
///
/// Rewards are not skippable: Proceed stays locked until nothing is owed, then leaves for the Draft scene
/// (<see cref="GauntletRun.LeaveBattle"/>), which is what the end-of-match Proceed did before this panel
/// existed. The panel GameObject is authored DISABLED, like CardSelectionPanel, so it is resolved lazily
/// through <see cref="Resolve"/> rather than cached from Awake.
/// </summary>
public class BattleRewardsPanel : MonoBehaviour
{
    /// <summary>One row of the list, with what it is for.</summary>
    private class Row
    {
        public Button button;
        public TextMeshProUGUI label;
        public Image icon;

        /// <summary>Index into <see cref="GauntletRun.Milestones"/>, or -1 for the card reward.</summary>
        public int milestone = -1;

        /// <summary>Taken: the row is inert and reads as what it gave.</summary>
        public bool taken;
    }

    [Header("Panel")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private string titleLabel = "Rewards!";
    [Tooltip("Title while the list shows a milestone's perk options instead of the rewards.")]
    [SerializeField] private string perkTitleLabel = "Choose a Perk";

    [Tooltip("Parent the reward rows are spawned under; drives their layout.")]
    [SerializeField] private Transform rowsContainer;

    [Tooltip("Cloned once per reward; kept hidden. Its label is the first TextMeshProUGUI under it, its " +
             "icon the Image on a child called Icon.")]
    [SerializeField] private Button rowTemplate;

    [Tooltip("Leaves for the Draft scene. Locked until every reward has been taken.")]
    [SerializeField] private Button proceedButton;

    [Tooltip("How far the Proceed button fades while it is locked. Its sprite-swap transition has no " +
             "disabled look of its own, so the fade is what says it cannot be clicked yet.")]
    [Range(0f, 1f)] [SerializeField] private float lockedProceedAlpha = 0.45f;

    [Header("Card reward")]
    [SerializeField] private Sprite cardIcon;
    [SerializeField] private string cardRowLabel = "Add {0} cards to your deck";
    [SerializeField] private string cardRowTakenLabel = "{0} cards added to your deck";
    [Tooltip("Shown once an Upgraded Cards milestone's picks are taken; {0} is how many.")]
    [SerializeField] private string upgradedCardsTakenLabel = "{0} upgraded cards added to your deck";
    [Tooltip("Taken label of an Upgraded Cards milestone worth a single card.")]
    [SerializeField] private string upgradedCardTakenLabel = "Upgraded card added to your deck";
    [Tooltip("Prompt over the card options; {0} is the pick being made and {1} the picks owed.")]
    [SerializeField] private string cardPickPrompt = "Draft a New Card For Your Deck {0}/{1}";
    [Tooltip("Prompt of a Duplicate milestone's pick.")]
    [SerializeField] private string duplicatePickPrompt = "Choose a Card to Duplicate";
    [Tooltip("The run's hero and deck, shown beside the options while a card is picked.")]
    [SerializeField] private BattleRewardLoadout loadout;

    [Header("Milestone rewards")]
    [Tooltip("Icon on a milestone row. A perk option shows the passive's own icon instead.")]
    [SerializeField] private Sprite milestoneIcon;
    [Tooltip("Tint for that icon: the track's own 'reached' gold, so a milestone row reads as one.")]
    [SerializeField] private Color milestoneIconTint = new Color(1f, 0.83f, 0.24f, 1f);
    [Tooltip("{0} is the milestone's XP, {1} the reward's name.")]
    [SerializeField] private string milestoneRowLabel = "{0} XP milestone: {1}";
    [Tooltip("Shown on a milestone row once claimed; {0} is what the reward did.")]
    [SerializeField] private string milestoneTakenLabel = "{0}";
    [Tooltip("Shown on the perk milestone's row once a perk is taken; {0} is the perk's name.")]
    [SerializeField] private string perkTakenLabel = "{0} learned";

    [Header("Timing")]
    [SerializeField] private float scaleInDuration = 0.4f;
    [Tooltip("How long each row takes to pop in, and the stagger between them.")]
    [SerializeField] private float rowRevealDuration = 0.25f;
    [SerializeField] private float rowRevealStagger = 0.08f;

    /// <summary>The reward rows, in the order they are listed.</summary>
    private readonly List<Row> _rows = new List<Row>();

    /// <summary>The perk options currently standing in for the list, if any.</summary>
    private readonly List<Row> _perkOptions = new List<Row>();

    /// <summary>The milestone row whose perk is being picked.</summary>
    private Row _perkRow;

    /// <summary>The row whose card picks are running: the card reward, or an Upgraded Cards milestone.</summary>
    private Row _pickingRow;

    /// <summary>What that row will read once its picks are taken.</summary>
    private string _pickingTakenLabel;

    /// <summary>The options could not be put on screen; the Draft scene asks for the picks instead.</summary>
    private bool _pickBlocked;

    private bool _leaving;

    /// <summary>Added on demand: what fades the Proceed button while it is locked.</summary>
    private CanvasGroup _proceedGroup;

    /// <summary>The end-of-match panel's XP track, followed so milestone rows land as the bar reaches them.</summary>
    private BattleXpTrack _xpSource;

    /// <summary>XP the track has shown so far: milestones up to here are listed.</summary>
    private int _shownXp;

    /// <summary>The end-of-match panel beside this one; hidden along with it while a card pick is up.</summary>
    private CanvasGroup _companion;

    /// <summary>When the next row may pop in: after the scale-in, and a stagger after the row before.</summary>
    private float _nextRowRevealTime;

    private static BattleRewardsPanel _instance;

    /// <summary>
    /// The panel in the current scene, disabled or not. Re-found whenever the cached one has gone away, so
    /// reloading the match scene picks up the new scene's panel instead of a destroyed one -- the same
    /// lazy resolve <see cref="CardSelectionPanel"/> uses.
    /// </summary>
    public static BattleRewardsPanel Resolve()
    {
        if (_instance == null)
        {
            var found = FindObjectsOfType<BattleRewardsPanel>(true);
            _instance = found.Length > 0 ? found[0] : null;
        }
        return _instance;
    }

    /// <summary>
    /// True while the run has rewards for the battle just won that are still to be taken. A run that is
    /// over has none: a loss ends it, and the last win goes straight to the result screen -- nothing on a
    /// finished run can be claimed (<see cref="GauntletRun.CanClaim"/>), so a panel listing rewards there
    /// could never be got past.
    /// </summary>
    public static bool HasRewards =>
        GauntletRun.IsActive && GauntletRun.Outcome == GauntletRun.RunOutcome.InProgress
        && (GauntletRun.PendingBattleReward || GauntletRun.HasUnclaimedMilestone);

    void Start()
    {
        if (proceedButton != null) proceedButton.onClick.AddListener(OnProceed);
    }

    /// <summary>
    /// Opens the panel on the rewards the won battle owes, scaling in after <paramref name="delay"/> so it
    /// lands together with the end-of-match panel. Milestone rows follow <paramref name="xpSource"/> as it
    /// fills; without one (or with it hidden) every reached milestone is listed straight away.
    /// <paramref name="companion"/> is hidden while a card pick owns the screen. Returns false, leaving the
    /// panel closed, when nothing is owed.
    /// </summary>
    public bool Show(float delay, BattleXpTrack xpSource, CanvasGroup companion)
    {
        // Must run after xpSource.Show, which reads the run's XP before this battle as its base.
        // The score the end-of-match panel is tallying becomes XP here, so a milestone the win reaches is
        // already reached below. The Draft scene's track then has nothing left to fill in.
        GauntletRun.CollectScore();
        if (!HasRewards) return false;
        GauntletRun.XpShown = GauntletRun.Xp;

        _leaving = false;
        _pickBlocked = false;

        UnsubscribeXp();
        _companion = companion;
        _xpSource = xpSource != null && xpSource.IsShowing ? xpSource : null;
        _shownXp = _xpSource != null ? _xpSource.ShownXp : GauntletRun.Xp;
        _nextRowRevealTime = Time.time + delay + scaleInDuration;

        if (titleText != null) titleText.text = titleLabel;

        gameObject.SetActive(true);
        BuildRows();
        RefreshRows();

        if (_xpSource != null) _xpSource.XpShown += OnXpShown;

        // No scale-in of its own: the panel lives under the end-of-match panel, which PopupManager
        // already scales in, and tweening here too would compound the two curves.
        transform.DOKill();
        transform.localScale = Vector3.one;
        return true;
    }

    private void OnXpShown(int xp)
    {
        if (xp <= _shownXp) return;
        _shownXp = xp;
        ListReachedMilestones();
        RefreshRows();
    }

    private void UnsubscribeXp()
    {
        if (_xpSource != null) _xpSource.XpShown -= OnXpShown;
        _xpSource = null;
    }

    // ---------------------------------------------------------------- Rows

    private void BuildRows()
    {
        ClearPerkOptions();
        ClearRows();

        if (rowsContainer == null || rowTemplate == null)
        {
            Debug.LogWarning("BattleRewardsPanel: not wired up (rows container / row template missing)");
            return;
        }

        rowTemplate.gameObject.SetActive(false);

        if (GauntletRun.PendingBattleReward)
        {
            var row = SpawnRow(cardIcon, string.Format(cardRowLabel, GauntletRun.PicksThisStep));
            row.button.onClick.AddListener(() => OnCardRowClicked(row));
            _rows.Add(row);
            RevealNextRow(row);
        }

        ListReachedMilestones();
    }

    /// <summary>
    /// Adds a row for every claimable milestone the track has filled up to and that is not listed yet. The
    /// bar only climbs, so rows arrive in milestone order.
    /// </summary>
    private void ListReachedMilestones()
    {
        if (rowsContainer == null || rowTemplate == null) return;

        var milestones = GauntletRun.Milestones;
        for (int i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            if (milestone.Xp > _shownXp || !IsOwed(milestone) || IsListed(i)) continue;

            string name = GauntletRun.MilestoneRewardName(milestone.Reward);
            var row = SpawnRow(milestoneIcon, string.Format(milestoneRowLabel, milestone.Xp, name), milestoneIconTint);
            row.milestone = i;
            int index = i;
            row.button.onClick.AddListener(() => OnMilestoneRowClicked(row, index));
            _rows.Add(row);

            // A perk's options have the list to themselves; the row joins the others when they come back.
            if (_perkOptions.Count > 0) row.button.gameObject.SetActive(false);
            RevealNextRow(row);
        }
    }

    private static bool IsOwed(GauntletRun.MilestoneState milestone) =>
        milestone.Reached && !milestone.Claimed && milestone.Reward != MilestoneReward.None;

    private bool IsListed(int milestone) => _rows.Exists(r => r.milestone == milestone);

    /// <summary>Pops the row in at its turn: not before the panel has scaled in, one stagger after the last.</summary>
    private void RevealNextRow(Row row)
    {
        float at = Mathf.Max(Time.time, _nextRowRevealTime);
        _nextRowRevealTime = at + rowRevealStagger;
        RevealRow(row, at - Time.time);
    }

    /// <summary>Rows only invite a click while what they give can be taken right now.</summary>
    private void RefreshRows()
    {
        foreach (var row in _rows)
        {
            if (row.taken) continue;
            row.button.interactable = row.milestone < 0
                ? GauntletRun.PendingBattleReward && GauntletRun.PendingPicks > 0
                : GauntletRun.CanClaim(row.milestone);
        }
        RefreshProceed();
    }

    /// <param name="wrap">Lets the label run onto a second line: a perk option carries its description.</param>
    private Row SpawnRow(Sprite icon, string label, Color? iconTint = null, bool wrap = false)
    {
        var button = Instantiate(rowTemplate, rowsContainer);
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();

        var iconTransform = button.transform.Find("Icon");
        var row = new Row
        {
            button = button,
            label = button.GetComponentInChildren<TextMeshProUGUI>(true),
            icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null,
        };

        if (row.label != null)
        {
            row.label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            row.label.text = label;
        }
        if (row.icon != null)
        {
            row.icon.sprite = icon;
            row.icon.color = iconTint ?? Color.white;
            row.icon.enabled = icon != null;
        }
        return row;
    }

    private void RevealRow(Row row, float delay)
    {
        row.button.transform.localScale = Vector3.zero;
        row.button.transform.DOScale(1f, rowRevealDuration).SetDelay(delay);
    }

    /// <summary>The row keeps its place in the list, reading as what it gave, and stops taking clicks.</summary>
    private void MarkTaken(Row row, string label)
    {
        if (row == null) return;

        row.taken = true;
        row.button.interactable = false;
        if (row.label != null && !string.IsNullOrEmpty(label)) row.label.text = label;
    }

    private void ClearRows()
    {
        foreach (var row in _rows) DestroyRow(row);
        _rows.Clear();
    }

    private void ClearPerkOptions()
    {
        foreach (var row in _perkOptions) DestroyRow(row);
        _perkOptions.Clear();
    }

    private static void DestroyRow(Row row)
    {
        if (row == null || row.button == null) return;
        row.button.transform.DOKill();
        Destroy(row.button.gameObject);
    }

    // ---------------------------------------------------------------- Card picks

    private void OnCardRowClicked(Row row)
    {
        if (_leaving || !GauntletRun.PendingBattleReward || GauntletRun.PendingPicks <= 0) return;

        _pickingRow = row;
        _pickingTakenLabel = string.Format(cardRowTakenLabel, GauntletRun.PicksThisStep);
        OpenNextCardPick();
    }

    /// <summary>
    /// Puts the next owed pick on screen, or brings the panel back once they are all taken. The panel is
    /// hidden while the options are up: the selection panel owns the screen (and the clicks) for as long
    /// as a choice is open.
    /// </summary>
    private void OpenNextCardPick()
    {
        if (GauntletRun.PendingPicks <= 0)
        {
            FinishCardPicks();
            return;
        }

        int pickNumber = GauntletRun.PicksThisStep - GauntletRun.PendingPicks + 1;
        string prompt = GauntletRun.PickingDuplicate
            ? duplicatePickPrompt
            : string.Format(cardPickPrompt, pickNumber, GauntletRun.PicksThisStep);

        gameObject.SetActive(false);
        SetCompanionVisible(false);
        CardChoice.Instance.Begin(GauntletRun.CurrentCardOptions(), prompt, OnCardPicked, requirePlayerTurn: false);

        // Nothing to pick from (no options rolled, or the match is in a state that refuses a choice): the
        // picks stay owed and the Draft scene asks for them, so Proceed must not stay locked here.
        if (CardChoice.Instance.HasActiveRequest)
        {
            if (loadout != null) loadout.Show();
        }
        else
        {
            Debug.LogWarning("BattleRewardsPanel: no card choice could be opened; the picks are left to the Draft scene.");
            _pickBlocked = true;
            ReturnToPanel();
        }
    }

    private void OnCardPicked(CardSO card)
    {
        GauntletRun.AddDraftedCard(card);
        OpenNextCardPick();
    }

    private void FinishCardPicks()
    {
        if (_pickingRow != null)
        {
            // A Duplicate pick names the card it copied, only known once it is picked.
            MarkTaken(_pickingRow, _pickingTakenLabel ?? string.Format(milestoneTakenLabel, GauntletRun.LastRewardMessage));
            _pickingRow = null;
            _pickingTakenLabel = null;
        }
        ReturnToPanel();
    }

    /// <summary>Back from a pick: the panel comes straight up, already scaled in.</summary>
    private void ReturnToPanel()
    {
        if (loadout != null) loadout.Hide();
        SetCompanionVisible(true);
        gameObject.SetActive(true);
        transform.DOKill();
        transform.localScale = Vector3.one;
        RefreshRows();
    }

    /// <summary>
    /// Hides the end-of-match panel with this one while a pick is up, by fading rather than disabling it so
    /// its score tally (and the track feeding milestone rows here) keeps running underneath.
    /// </summary>
    private void SetCompanionVisible(bool visible)
    {
        if (_companion == null) return;
        _companion.alpha = visible ? 1f : 0f;
        _companion.blocksRaycasts = visible;
    }

    // ---------------------------------------------------------------- Milestones

    private void OnMilestoneRowClicked(Row row, int index)
    {
        if (_leaving || !GauntletRun.CanClaim(index)) return;

        var reward = GauntletRun.Milestones[index].Reward;
        GauntletRun.ClaimMilestone(index);

        // Upgraded Cards owes picks, New Perk owes a perk; the rest have already applied themselves.
        if (GauntletRun.PendingPicks > 0)
        {
            _pickingRow = row;
            _pickingTakenLabel = GauntletRun.PickingDuplicate ? null : string.Format(milestoneTakenLabel,
                string.Format(!GauntletRun.PickingUpgraded ? cardRowTakenLabel
                        : GauntletRun.PicksThisStep == 1 ? upgradedCardTakenLabel : upgradedCardsTakenLabel,
                    GauntletRun.PicksThisStep));
            OpenNextCardPick();
            return;
        }

        if (GauntletRun.PendingPerkPick)
        {
            ShowPerkOptions(row);
            return;
        }

        string did = GauntletRun.LastRewardMessage;
        if (string.IsNullOrEmpty(did)) did = GauntletRun.MilestoneRewardName(reward);
        MarkTaken(row, string.Format(milestoneTakenLabel, did));
        RefreshRows();
    }

    /// <summary>
    /// The perk milestone's options take the list over: the reward rows step aside until one is taken,
    /// which keeps the pick on this panel rather than handing it to the Draft scene.
    /// </summary>
    private void ShowPerkOptions(Row row)
    {
        _perkRow = row;
        foreach (var other in _rows) other.button.gameObject.SetActive(false);
        if (titleText != null) titleText.text = perkTitleLabel;

        var options = GauntletRun.CurrentPerkOptions;
        for (int i = 0; i < options.Count; i++)
        {
            var perk = options[i];
            if (perk == null) continue;

            string label = string.IsNullOrEmpty(perk.description)
                ? perk.passiveName
                : $"{perk.passiveName}\n<size=72%>{perk.description}</size>";
            var option = SpawnRow(perk.icon != null ? perk.icon : milestoneIcon, label, null, wrap: true);
            option.button.onClick.AddListener(() => TakePerk(perk));
            _perkOptions.Add(option);
            RevealRow(option, i * rowRevealStagger);
        }

        RefreshProceed();
    }

    private void TakePerk(HeroPassiveSO perk)
    {
        if (_leaving || perk == null) return;

        GauntletRun.AddPerk(perk);

        ClearPerkOptions();
        foreach (var other in _rows) other.button.gameObject.SetActive(true);
        if (titleText != null) titleText.text = titleLabel;

        MarkTaken(_perkRow, string.Format(perkTakenLabel, perk.passiveName));
        _perkRow = null;
        RefreshRows();
    }

    // ---------------------------------------------------------------- Leaving

    private void RefreshProceed()
    {
        if (proceedButton == null) return;

        bool can = CanProceed;
        proceedButton.interactable = can;

        if (_proceedGroup == null)
        {
            _proceedGroup = proceedButton.GetComponent<CanvasGroup>();
            if (_proceedGroup == null) _proceedGroup = proceedButton.gameObject.AddComponent<CanvasGroup>();
        }
        _proceedGroup.alpha = can ? 1f : lockedProceedAlpha;
    }

    /// <summary>
    /// Rewards are not skippable: every row the panel lists has to be taken before the run moves on. Read
    /// off the rows rather than the run, so what unlocks Proceed is exactly what the player can see -- and
    /// stays locked while the track is still filling toward a milestone that has no row yet.
    /// </summary>
    private bool CanProceed
    {
        get
        {
            if (_leaving || _perkOptions.Count > 0) return false;

            var milestones = GauntletRun.Milestones;
            for (int i = 0; i < milestones.Count; i++)
                if (IsOwed(milestones[i]) && !IsListed(i)) return false;

            if (_pickBlocked) return true;

            foreach (var row in _rows)
                if (!row.taken) return false;
            return true;
        }
    }

    private void OnProceed()
    {
        if (!CanProceed) return;

        _leaving = true;
        UnsubscribeXp();
        if (proceedButton != null) proceedButton.interactable = false;
        GauntletRun.LeaveBattle();
    }

    void OnDestroy()
    {
        UnsubscribeXp();
        transform.DOKill();
        foreach (var row in _rows)
            if (row.button != null) row.button.transform.DOKill();
        foreach (var row in _perkOptions)
            if (row.button != null) row.button.transform.DOKill();
    }
}
