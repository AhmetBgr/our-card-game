using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drives the Draft scene of Forged in Battle. Every visit to the scene shows whichever step the
/// <see cref="GauntletRun"/> is on:
///   1. no hero yet: pick 1 of N heroes in the draft panel;
///   2. card picks owed: pick 1 of N base cards in the draft panel, until the step's picks are done.
///      A claimed card milestone owes picks the same way (upgraded cards too); a perk milestone owes a
///      pick of 1 of N passives;
///   3. a reached milestone unclaimed: the draft panel asks for it to be claimed. Hovering a milestone
///      on the track previews it in the draft panel, clicking a reached one claims it;
///   4. nothing owed: the draft panel hides and the upcoming battle's skull on the track becomes the way
///      in: clicking it keeps the Next Match panel up and shows the Proceed button under the skull.
///      Hovering that skull previews the enemy at any point once a hero is picked, mid-draft included;
///   5. run over: the result in the draft panel's header, with New Run and Main Menu.
/// Entering the scene with no run active (from the menu, or opened directly in the editor) starts one.
///
/// Card options are drawn with the same display-only card prefab the in-match card choice uses
/// (CardPreview Variant); hero options use the HeroPanel prefab, like the player's own hero panel.
/// The scene's layout is authored by hand; this only fills it in and toggles its panels.
/// </summary>
public class DraftController : MonoBehaviour
{
    /// <summary>One skull on the battle track.</summary>
    [System.Serializable]
    private class BattleNode
    {
        [Tooltip("Hover / click target. Gets an EventTrigger at runtime.")]
        public Graphic skull;
        [Tooltip("Shown on the upcoming battle.")]
        public GameObject highlight;
        [Tooltip("Shown on battles already won.")]
        public GameObject defeatedMark;
    }

    [Header("Draft panel")]
    [SerializeField] private GameObject draftPanel;
    [SerializeField] private TextMeshProUGUI draftHeaderText;
    [Tooltip("Parent the option cards are spawned under; drives their layout.")]
    [SerializeField] private Transform optionsContainer;
    [Tooltip("Display-only card prefab used for each option (CardPreview Variant).")]
    [SerializeField] private GameObject optionPrefab;
    [SerializeField] private float baseCardScale = 0.8f;
    [Tooltip("Hero panel prefab used for each hero option (HeroPanel).")]
    [SerializeField] private GameObject heroOptionPrefab;
    [SerializeField] private float heroOptionScale = 0.9f;
    [Tooltip("Width each hero option takes in the options row's layout. The prefab's own rect is wider than " +
             "its art, and three of them would overflow the row.")]
    [SerializeField] private float heroOptionSlotWidth = 190f;
    [Tooltip("Scale multiplier while a hero option is hovered.")]
    [SerializeField] private float heroHoverScale = 1.05f;
    [SerializeField] private float heroHoverDuration = 0.1f;
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float optionRevealDuration = 0.25f;
    [SerializeField] private float optionRevealStagger = 0.08f;

    [Header("Deck list")]
    [SerializeField] private TextMeshProUGUI deckHeaderText;
    [Tooltip("Entries are cloned from this one; the authored entries next to it are placeholders and get removed.")]
    [SerializeField] private CardButtonHandler deckEntryTemplate;

    [Header("Heroes")]
    [SerializeField] private GauntletHeroPanelView playerHero;
    [SerializeField] private GauntletHeroPanelView enemyHero;

    [Header("Next match")]
    [SerializeField] private GameObject nextMatchPanel;
    [SerializeField] private TextMeshProUGUI nextMatchHeaderText;
    [SerializeField] private TextMeshProUGUI nextMatchRulesText;

    [Header("Battle track")]
    [SerializeField] private BattleNode[] battleNodes;
    [Tooltip("Gap in canvas units between the bottom of the selected skull and the top of the Proceed button.")]
    [SerializeField] private float proceedGap = 4f;
    [Tooltip("Skull tint once its battle is won; the others are shown untinted.")]
    [SerializeField] private Color beatenSkullTint = new Color(0.396f, 0.396f, 0.396f, 1f);

    [Header("Milestones")]
    [SerializeField] private MilestoneTrack milestoneTrack;
    [Tooltip("Passive chip prefab (PassiveChip) used for each perk option.")]
    [SerializeField] private PassiveChipButton perkOptionPrefab;
    [Tooltip("Size each perk option takes in the options row.")]
    [SerializeField] private Vector2 perkOptionSize = new Vector2(170f, 210f);
    [SerializeField] private float perkIconScale = 1.8f;
    [SerializeField] private float perkTextSize = 14f;

    [Header("Buttons")]
    [Tooltip("Proceed on the briefing step (sits under the selected skull); New Run on the result step.")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private TextMeshProUGUI primaryButtonLabel;
    [Tooltip("Back to the main menu; only shown once the run is over.")]
    [SerializeField] private Button menuButton;
    [SerializeField] private TextMeshProUGUI menuButtonLabel;
    [Tooltip("On the result step, New Run and Main Menu are centred side by side in this rect.")]
    [SerializeField] private RectTransform resultButtonArea;
    [SerializeField] private float resultButtonSpacing = 130f;
    [SerializeField] private string proceedLabel = "Proceed";
    [SerializeField] private string newRunLabel = "New Run";
    [SerializeField] private string mainMenuLabel = "Main Menu";

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<CardButtonHandler> _deckEntries = new List<CardButtonHandler>();
    private bool _nextMatchPinned;
    private bool _draftPanelShown;
    private bool _leaving;
    private float _rulesFontSize;

    void Start()
    {
        // The draft runs on the plain rules: no Custom Game modifier may leak into the run's matches.
        MatchModifiers.Clear();

        if (!GauntletRun.IsActive) GauntletRun.StartNew();

        if (menuButton != null) menuButton.onClick.AddListener(ExitToMenu);
        if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimary);

        if (nextMatchRulesText != null) _rulesFontSize = nextMatchRulesText.fontSize;
        ClearAuthoredPlaceholders();
        HookBattleNodes();
        if (milestoneTrack != null)
        {
            milestoneTrack.NodeHovered += OnMilestoneHover;
            milestoneTrack.NodeClicked += OnMilestoneClicked;
        }
        Refresh();
        RefreshMilestones(animate: true);
    }

    // ---------------------------------------------------------------- Steps

    private void Refresh()
    {
        _nextMatchPinned = false;

        bool inProgress = GauntletRun.Outcome == GauntletRun.RunOutcome.InProgress;

        // The enemy is rolled as soon as the hero is known (so a mirror can be avoided), and kept until
        // the battle is fought, so the preview and the fight agree.
        if (inProgress && GauntletRun.Hero != null && GauntletRun.EnemyHero == null)
            GauntletRun.PrepareNextBattle();

        RefreshDeck();
        playerHero?.ShowRunHero();
        RefreshNextMatch();
        RefreshTrack();
        if (milestoneTrack != null) milestoneTrack.RefreshStates();
        SetNextMatchVisible(false);

        if (menuButtonLabel != null) menuButtonLabel.text = mainMenuLabel;
        if (menuButton != null) menuButton.gameObject.SetActive(!inProgress);

        ShowStep();
    }

    /// <summary>Fills the draft panel for the step the run is on. Also restores it after a milestone preview.</summary>
    private void ShowStep()
    {
        ClearOptions();

        if (GauntletRun.Outcome != GauntletRun.RunOutcome.InProgress) ShowResult();
        else if (GauntletRun.Hero == null) ShowHeroPick();
        else if (GauntletRun.PendingPerkPick) ShowPerkPick();
        else if (GauntletRun.PendingPicks > 0) ShowCardPick();
        else if (GauntletRun.HasUnclaimedMilestone) ShowMilestonePrompt();
        else ShowBriefing();
    }

    private void ShowHeroPick()
    {
        SetDraftPanel("Choose Your Hero");
        SetPrimary(null);

        SpawnHeroOptions(GauntletRun.RollHeroOptions(), picked =>
        {
            GauntletRun.SetHero(picked);
            Refresh();
            RefreshMilestones(animate: false);
        });
    }

    private void ShowCardPick()
    {
        int pickNumber = GauntletRun.PicksThisStep - GauntletRun.PendingPicks + 1;
        if (GauntletRun.PickingDuplicate)
        {
            SetDraftPanel("Choose a Card to Duplicate");
        }
        else
        {
            string what = GauntletRun.PickingUpgraded ? "an Upgraded Card" : "a New Card";
            SetDraftPanel($"Draft {what} For Your Deck {pickNumber}/{GauntletRun.PicksThisStep}");
        }
        SetPrimary(null);

        SpawnOptions(GauntletRun.CurrentCardOptions(), picked =>
        {
            GauntletRun.AddDraftedCard(picked);
            Refresh();
        });
    }

    private void ShowPerkPick()
    {
        SetDraftPanel("Draft a New Perk For Your Hero");
        SetPrimary(null);

        SpawnPerkOptions(GauntletRun.CurrentPerkOptions, picked =>
        {
            GauntletRun.AddPerk(picked);
            Refresh();
        });
    }

    /// <summary>A reached milestone waits to be claimed: the next battle stays shut until it is.</summary>
    private void ShowMilestonePrompt()
    {
        SetDraftPanel(WithSubtitle("Milestone Reached!", "Click a glowing milestone to claim its reward"));
        SetPrimary(null);
    }

    /// <summary>Picks done: the track's upcoming skull takes over (see <see cref="OnNodeClicked"/>).</summary>
    private void ShowBriefing()
    {
        // What an instant milestone reward just did stays up until the player moves on.
        SetDraftPanel(GauntletRun.LastRewardMessage);
        SetPrimary(null);
    }

    private void ShowResult()
    {
        bool won = GauntletRun.Outcome == GauntletRun.RunOutcome.Won;
        SetDraftPanel(won ? "Gauntlet Conquered!" : "Defeated");
        SetPrimary(newRunLabel);

        // The draft panel is empty on this step: its two buttons sit side by side in the middle of it.
        if (resultButtonArea != null)
        {
            Vector3 centre = resultButtonArea.TransformPoint(resultButtonArea.rect.center);
            Vector3 step = resultButtonArea.TransformVector(new Vector3(resultButtonSpacing * 0.5f, 0f, 0f));
            if (primaryButton != null) PlaceCentred((RectTransform)primaryButton.transform, centre + step);
            if (menuButton != null) PlaceCentred((RectTransform)menuButton.transform, centre - step);
        }
    }

    private void OnPrimary()
    {
        if (_leaving) return;

        if (GauntletRun.Outcome != GauntletRun.RunOutcome.InProgress)
        {
            GauntletRun.StartNew();
            Refresh();
            RefreshMilestones(animate: false);
            return;
        }

        if (!CanFight) return;

        _leaving = true;
        SetButtonsInteractable(false);
        if (GauntletRun.EnemyHero == null) GauntletRun.PrepareNextBattle();
        GauntletRun.LoadScene(GauntletRun.Config.gameSceneName);
    }

    private void ExitToMenu()
    {
        if (_leaving) return;

        _leaving = true;
        SetButtonsInteractable(false);
        string menu = GauntletRun.Config != null ? GauntletRun.Config.menuSceneName : "MainMenu";
        GauntletRun.End();
        GauntletRun.LoadScene(menu);
    }

    private static bool CanFight =>
        GauntletRun.Outcome == GauntletRun.RunOutcome.InProgress
        && GauntletRun.Hero != null
        && GauntletRun.PendingPicks == 0
        && !GauntletRun.PendingPerkPick
        && !GauntletRun.HasUnclaimedMilestone;

    // ---------------------------------------------------------------- Milestones

    /// <summary>
    /// Shows the run's track. On the first visit after it unlocks the lock slides away, and any XP
    /// gained since the last visit fills in.
    /// </summary>
    private void RefreshMilestones(bool animate)
    {
        if (milestoneTrack == null) return;

        bool unlocked = GauntletRun.MilestonesUnlocked;
        bool reveal = animate && unlocked && !GauntletRun.MilestonesRevealed;
        int from = animate ? GauntletRun.XpShown : GauntletRun.Xp;

        milestoneTrack.Show(GauntletRun.Milestones, from, GauntletRun.Xp, unlocked, reveal);

        if (unlocked)
        {
            GauntletRun.MilestonesRevealed = true;
            GauntletRun.XpShown = GauntletRun.Xp;
        }
    }

    /// <summary>
    /// Previews are only for the time between picks: while a pick is owed, or the Proceed button is up,
    /// the draft panel stays on that.
    /// </summary>
    private bool CanPreviewMilestones =>
        !_leaving && !_nextMatchPinned
        && GauntletRun.Outcome == GauntletRun.RunOutcome.InProgress
        && GauntletRun.MilestonesUnlocked
        && GauntletRun.PendingPicks == 0 && !GauntletRun.PendingPerkPick;

    private void OnMilestoneHover(int index, bool entered)
    {
        if (!CanPreviewMilestones) return;

        if (entered) ShowMilestonePreview(index);
        else ShowStep();
    }

    private void OnMilestoneClicked(int index)
    {
        if (_leaving || !GauntletRun.CanClaim(index)) return;

        GauntletRun.ClaimMilestone(index);
        Refresh();
    }

    /// <summary>
    /// The hovered milestone in the draft panel: what it gives, and for a reached card or perk reward, the
    /// options it offers (display only; clicking the milestone claims it and makes them pickable).
    /// </summary>
    private void ShowMilestonePreview(int index)
    {
        var milestones = GauntletRun.Milestones;
        if (index < 0 || index >= milestones.Count) return;

        var milestone = milestones[index];
        string status;
        if (milestone.Reward == MilestoneReward.None) status = GauntletRun.MilestoneRewardDescription(milestone.Reward);
        else if (milestone.Claimed) status = "Claimed";
        else if (!milestone.Reached) status = $"{GauntletRun.MilestoneRewardDescription(milestone.Reward)}. Reach {milestone.Xp} XP to unlock";
        else status = $"{GauntletRun.MilestoneRewardDescription(milestone.Reward)}. Click to claim";

        ClearOptions();
        SetDraftPanel(WithSubtitle($"{milestone.Xp} XP: {GauntletRun.MilestoneRewardName(milestone.Reward)}", status));
        SetPrimary(null);

        if (!milestone.Reached || milestone.Claimed) return;

        var cards = GauntletRun.MilestoneCardOptions(index);
        if (cards != null) SpawnOptions(cards, null);

        var perks = GauntletRun.MilestonePerkOptions(index);
        if (perks != null) SpawnPerkOptions(perks, null);
    }

    private static string WithSubtitle(string title, string subtitle) => $"{title}\n<size=60%>{subtitle}</size>";

    // ---------------------------------------------------------------- Battle track

    private void HookBattleNodes()
    {
        if (battleNodes == null) return;

        for (int i = 0; i < battleNodes.Length; i++)
        {
            var skull = battleNodes[i]?.skull;
            if (skull == null) continue;

            skull.raycastTarget = true;
            var trigger = skull.GetComponent<EventTrigger>();
            if (trigger == null) trigger = skull.gameObject.AddComponent<EventTrigger>();

            int index = i;
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => OnNodeHover(index, true));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => OnNodeHover(index, false));
            AddTrigger(trigger, EventTriggerType.PointerClick, () => OnNodeClicked(index));
        }
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private bool IsUpcoming(int nodeIndex) =>
        GauntletRun.Outcome == GauntletRun.RunOutcome.InProgress
        && GauntletRun.Hero != null
        && nodeIndex == GauntletRun.EncounterIndex;

    private void OnNodeHover(int index, bool entered)
    {
        if (_leaving || _nextMatchPinned || !IsUpcoming(index)) return;
        SetNextMatchVisible(entered);
    }

    private void OnNodeClicked(int index)
    {
        // Mid-draft the skull is hover-only: clicking does nothing until the deck is drafted.
        if (_leaving || !IsUpcoming(index) || !CanFight) return;

        _nextMatchPinned = true;
        SetNextMatchVisible(true);
        SetPrimary(proceedLabel);
        PlacePrimary(battleNodes[index].skull.rectTransform);
    }

    private void RefreshTrack()
    {
        if (battleNodes == null) return;

        bool won = GauntletRun.Outcome == GauntletRun.RunOutcome.Won;
        for (int i = 0; i < battleNodes.Length; i++)
        {
            var node = battleNodes[i];
            if (node == null) continue;

            bool defeated = won || i < GauntletRun.EncounterIndex;
            if (node.defeatedMark != null) node.defeatedMark.SetActive(defeated);
            if (node.skull != null) node.skull.color = defeated ? beatenSkullTint : Color.white;
            if (node.highlight != null) node.highlight.SetActive(IsUpcoming(i));
        }
    }

    /// <summary>Centres the primary button under <paramref name="target"/>, its top edge just below it.</summary>
    private void PlacePrimary(RectTransform target)
    {
        if (primaryButton == null || target == null) return;

        var rt = (RectTransform)primaryButton.transform;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 bottomCentre = rt.parent.InverseTransformPoint((corners[0] + corners[3]) * 0.5f);

        float height = rt.rect.height * rt.localScale.y;
        PlaceCentred(rt, rt.parent.TransformPoint(bottomCentre - new Vector3(0f, proceedGap + height * 0.5f, 0f)));
    }

    /// <summary>Moves <paramref name="rt"/> so its centre lands on a world point, whatever its pivot.</summary>
    private static void PlaceCentred(RectTransform rt, Vector3 worldCentre)
    {
        Vector3 local = rt.parent.InverseTransformPoint(worldCentre);
        Vector2 size = Vector2.Scale(rt.rect.size, rt.localScale);
        rt.localPosition = new Vector3(
            local.x + (rt.pivot.x - 0.5f) * size.x,
            local.y + (rt.pivot.y - 0.5f) * size.y,
            rt.localPosition.z);
    }

    // ---------------------------------------------------------------- View

    private void SetDraftPanel(string header)
    {
        _draftPanelShown = header != null;
        if (draftPanel != null) draftPanel.SetActive(_draftPanelShown && !IsNextMatchVisible);
        if (header != null && draftHeaderText != null) draftHeaderText.text = header;
    }

    private void SetNextMatchVisible(bool visible)
    {
        if (nextMatchPanel == null) return;

        nextMatchPanel.SetActive(visible);

        // The two panels overlap: while the enemy is previewed mid-draft, the options make way for it.
        if (draftPanel != null) draftPanel.SetActive(_draftPanelShown && !visible);
    }

    private bool IsNextMatchVisible => nextMatchPanel != null && nextMatchPanel.activeSelf;

    /// <summary>Shows the primary button with this label, or hides it for null.</summary>
    private void SetPrimary(string label)
    {
        if (primaryButton == null) return;

        primaryButton.gameObject.SetActive(label != null);
        if (label != null && primaryButtonLabel != null) primaryButtonLabel.text = label;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (primaryButton != null) primaryButton.interactable = interactable;
        if (menuButton != null) menuButton.interactable = interactable;
        foreach (var go in _spawned)
            if (go != null)
                foreach (var b in go.GetComponentsInChildren<Button>(true)) b.interactable = interactable;
    }

    /// <summary>A hero as an enemy would field it this battle (the encounter's health scaling).</summary>
    /// <param name="passives">Shown in place of the hero's own passives when given (a blessed enemy's).</param>
    private static void RefreshHero(GauntletHeroPanelView panel, HeroSO hero, IReadOnlyList<HeroPassiveSO> passives = null)
    {
        if (panel == null) return;
        panel.Show(hero, hero != null ? EnemyStartingHealth(hero) : 0, hero != null ? hero.attack : 0, passives);
    }

    /// <summary>Same scaling (and Bonus Health) <see cref="GauntletRun.ApplyEnemySetup"/> applies in the match.</summary>
    private static int EnemyStartingHealth(HeroSO hero) =>
        GauntletRun.EnemyStartingHealth(hero.defHealth > 0 ? hero.defHealth : hero.health);

    private void RefreshNextMatch()
    {
        var encounter = GauntletRun.CurrentEncounter;
        var enemy = GauntletRun.EnemyHero;

        RefreshHero(enemyHero, enemy, enemy != null ? GauntletRun.EnemyPassives : null);

        if (nextMatchHeaderText != null)
            nextMatchHeaderText.text = encounter != null && !string.IsNullOrEmpty(encounter.label)
                ? encounter.label
                : $"Battle {GauntletRun.EncounterIndex + 1}";

        if (nextMatchRulesText == null) return;
        if (encounter == null)
        {
            nextMatchRulesText.text = string.Empty;
            return;
        }

        var rules = new System.Text.StringBuilder();

        // The enemy's blessings under one header, one line each; stacked ones (Extra Passive) once, with a count.
        var blessings = GauntletRun.EnemyBlessings;
        if (blessings.Count > 0) rules.Append("> Enemy blessings:");
        var listed = new List<GauntletBlessing>();
        foreach (var blessing in blessings)
        {
            if (listed.Contains(blessing)) continue;
            listed.Add(blessing);

            int stacks = 0;
            foreach (var b in blessings) if (b == blessing) stacks++;

            rules.Append($"\n<color=#FFD36B>{GauntletRun.BlessingName(blessing)}{(stacks > 1 ? $" x{stacks}" : "")}</color>: " +
                         GauntletRun.BlessingDescription(blessing));
        }

        // Up to three blessings can outgrow the panel at the authored size; shrink to fit instead.
        nextMatchRulesText.enableAutoSizing = true;
        nextMatchRulesText.fontSizeMax = _rulesFontSize;
        nextMatchRulesText.fontSizeMin = _rulesFontSize * 0.6f;
        nextMatchRulesText.text = rules.ToString();
    }

    private void RefreshDeck() =>
        GauntletDeckList.Refresh(deckHeaderText, deckEntryTemplate, _deckEntries, GauntletRun.Deck);

    /// <summary>
    /// Removes the stand-ins the layout was authored with: the example option cards and the example
    /// deck entries (the template is kept, hidden, to clone from).
    /// </summary>
    private void ClearAuthoredPlaceholders()
    {
        if (optionsContainer != null)
            for (int i = optionsContainer.childCount - 1; i >= 0; i--)
                Destroy(optionsContainer.GetChild(i).gameObject);

        GauntletDeckList.ClearPlaceholders(deckEntryTemplate);
    }

    private void SpawnOptions<T>(List<T> options, System.Action<CardSO> onPicked) where T : CardSO
    {
        if (optionsContainer == null || optionPrefab == null)
        {
            Debug.LogWarning("[Gauntlet] DraftController is not wired up (options container / prefab missing).");
            return;
        }

        var baseScale = Vector3.one * baseCardScale;

        for (int i = 0; i < options.Count; i++)
        {
            CardSO option = options[i];
            if (option == null) continue;

            GameObject go = Instantiate(optionPrefab, optionsContainer);
            go.SetActive(true);
            _spawned.Add(go);

            var modal = go.GetComponent<CardModal>();
            var view = go.GetComponent<CardView>();
            if (modal != null)
            {
                modal.UpdateModal(option, null, true);
                if (view != null)
                {
                    view.UpdateView(modal);
                    view.SetPlayableOutline(true);
                    view.SetHoveredOutline(false);

                    var hover = go.GetComponent<CardHoverOutline>();
                    if (hover == null) hover = go.AddComponent<CardHoverOutline>();
                    hover.ConfigureHoverScale(hoverScale, baseScale);
                }
            }

            if (go.TryGetComponent(out Button button))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (_leaving) return;
                    onPicked?.Invoke(option);
                });
            }

            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(baseCardScale, optionRevealDuration).SetDelay(i * optionRevealStagger);
        }
    }

    private void SpawnHeroOptions(List<HeroSO> heroes, System.Action<HeroSO> onPicked)
    {
        if (optionsContainer == null || heroOptionPrefab == null)
        {
            Debug.LogWarning("[Gauntlet] DraftController is not wired up (options container / hero prefab missing).");
            return;
        }

        for (int i = 0; i < heroes.Count; i++)
        {
            HeroSO hero = heroes[i];
            if (hero == null) continue;

            GameObject go = Instantiate(heroOptionPrefab, optionsContainer);
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
            rt.sizeDelta = new Vector2(heroOptionSlotWidth, rt.sizeDelta.y);
            _spawned.Add(go);

            var view = GauntletHeroPanelView.FromInstance(go);
            RefreshHero(view, hero);

            System.Action pick = () =>
            {
                if (_leaving) return;
                onPicked(hero);
            };

            // The portrait is a button of its own; the rest of the panel picks through the trigger below.
            var button = view.portrait != null ? view.portrait.GetComponent<Button>() : null;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => pick());
            }

            // Hovering anywhere on the panel lights its Highlighted image and scales it up a touch.
            var highlighted = go.transform.Find("Highlighted");
            if (highlighted != null) highlighted.gameObject.SetActive(false);

            // The passive text shows for the whole panel, under the first chip, so the chips' own
            // beside-the-chip tooltips are switched off here.
            RectTransform tooltipAnchor = null;
            var passiveText = new System.Text.StringBuilder();
            if (view.passiveChips != null)
            {
                foreach (var chip in view.passiveChips)
                {
                    if (chip == null || !chip.TryGetComponent(out UITooltipTrigger chipTooltip)) continue;
                    if (chip.Passive != null && !string.IsNullOrEmpty(chipTooltip.Message))
                    {
                        if (tooltipAnchor == null) tooltipAnchor = (RectTransform)chip.transform;
                        if (passiveText.Length > 0) passiveText.Append("\n\n");
                        passiveText.Append(chipTooltip.Message);
                    }
                    chipTooltip.enabled = false;
                }
            }
            string tooltip = passiveText.ToString();

            var trigger = go.GetComponent<EventTrigger>();
            if (trigger == null) trigger = go.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => SetHeroOptionHovered(go, highlighted, true, tooltip, tooltipAnchor));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetHeroOptionHovered(go, highlighted, false, tooltip, tooltipAnchor));
            AddTrigger(trigger, EventTriggerType.PointerClick, pick);

            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(heroOptionScale, optionRevealDuration).SetDelay(i * optionRevealStagger);
        }
    }

    /// <summary>
    /// One option per passive: its chip, scaled up, over its name and description. Null
    /// <paramref name="onPicked"/> shows them for display only (a milestone preview).
    /// </summary>
    private void SpawnPerkOptions(IReadOnlyList<HeroPassiveSO> perks, System.Action<HeroPassiveSO> onPicked)
    {
        if (optionsContainer == null || perkOptionPrefab == null)
        {
            Debug.LogWarning("[Gauntlet] DraftController is not wired up (options container / perk prefab missing).");
            return;
        }

        for (int i = 0; i < perks.Count; i++)
        {
            HeroPassiveSO perk = perks[i];
            if (perk == null) continue;

            var go = new GameObject(perk.passiveName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(optionsContainer, false);
            rt.sizeDelta = perkOptionSize;
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = perkOptionSize.x;
            layout.preferredHeight = perkOptionSize.y;
            _spawned.Add(go);

            var chip = Instantiate(perkOptionPrefab, rt);
            chip.ShowPassive(perk);
            var chipRt = (RectTransform)chip.transform;
            chipRt.anchorMin = chipRt.anchorMax = new Vector2(0.5f, 1f);
            chipRt.anchoredPosition = new Vector2(0f, -chipRt.rect.height * perkIconScale * 0.5f);
            chipRt.localScale = Vector3.one * perkIconScale;

            // The option spells the passive out itself, so the chip's own tooltip would only repeat it.
            string description = perk.passiveName;
            if (chip.TryGetComponent(out UITooltipTrigger chipTooltip))
            {
                if (!string.IsNullOrEmpty(chipTooltip.Message)) description = chipTooltip.Message;
                chipTooltip.enabled = false;
            }

            var textGo = new GameObject("Description", typeof(RectTransform));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = new Vector2(1f, 0f);
            textRt.pivot = new Vector2(0.5f, 0f);
            textRt.sizeDelta = new Vector2(0f, perkOptionSize.y - chipRt.rect.height * perkIconScale - 8f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (draftHeaderText != null)
            {
                text.font = draftHeaderText.font;
                text.fontSharedMaterial = draftHeaderText.fontSharedMaterial;
            }
            text.fontSize = perkTextSize;
            text.alignment = TextAlignmentOptions.Top;
            text.enableWordWrapping = true;
            text.text = description;

            if (onPicked != null)
            {
                System.Action pick = () =>
                {
                    if (_leaving) return;
                    onPicked(perk);
                };
                chip.OnClicked = _ => pick();

                var trigger = go.AddComponent<EventTrigger>();
                AddTrigger(trigger, EventTriggerType.PointerEnter, () => SetPerkOptionHovered(go, true));
                AddTrigger(trigger, EventTriggerType.PointerExit, () => SetPerkOptionHovered(go, false));
                AddTrigger(trigger, EventTriggerType.PointerClick, pick);
            }

            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(1f, optionRevealDuration).SetDelay(i * optionRevealStagger);
        }
    }

    private void SetPerkOptionHovered(GameObject option, bool hovered)
    {
        if (_leaving && hovered) return;

        option.transform.DOKill();
        option.transform.DOScale(hovered ? heroHoverScale : 1f, heroHoverDuration);
    }

    private void SetHeroOptionHovered(GameObject option, Transform highlighted, bool hovered, string tooltip, RectTransform tooltipAnchor)
    {
        if (_leaving && hovered) return;

        if (highlighted != null) highlighted.gameObject.SetActive(hovered);

        if (UITooltip.Instance != null)
        {
            if (hovered && tooltipAnchor != null) UITooltip.Instance.ShowBelow(tooltip, tooltipAnchor);
            else UITooltip.Instance.Hide();
        }

        option.transform.DOKill();
        float scale = heroOptionScale * (hovered ? heroHoverScale : 1f);
        option.transform.DOScale(scale, heroHoverDuration);
    }

    private void ClearOptions()
    {
        // A hovered hero option's tooltip would otherwise outlive the option.
        if (_spawned.Count > 0 && UITooltip.Instance != null) UITooltip.Instance.Hide();

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            go.transform.DOKill();
            Destroy(go);
        }
        _spawned.Clear();
    }
}
