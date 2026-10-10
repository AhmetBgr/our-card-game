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
///   2. card picks owed: pick 1 of N base cards in the draft panel, until the step's picks are done;
///   3. nothing owed: the draft panel hides and the upcoming battle's skull on the track becomes the way
///      in: clicking it keeps the Next Match panel up and shows the Proceed button under the skull.
///      Hovering that skull previews the enemy at any point once a hero is picked, mid-draft included;
///   4. run over: the result in the draft panel's header, with New Run and Main Menu.
/// Entering the scene with no run active (from the menu, or opened directly in the editor) starts one.
///
/// Card options are drawn with the same display-only card prefab the in-match card choice uses
/// (CardPreview Variant); hero options use the HeroPanel prefab, like the player's own hero panel.
/// The scene's layout is authored by hand; this only fills it in and toggles its panels.
/// </summary>
public class DraftController : MonoBehaviour
{
    /// <summary>One hero panel of the layout: name plate, portrait with stats, passive chips.</summary>
    [System.Serializable]
    private class HeroPanelView
    {
        public GameObject root;
        public TextMeshProUGUI nameText;
        public HeroButtonHandler portrait;
        [Tooltip("Overwritten after the portrait fills in, with the health the hero will actually start at.")]
        public TextMeshProUGUI healthText;
        public TextMeshProUGUI attackText;
        [Tooltip("Filled in order with the hero's passives; chips past the last passive keep their empty look.")]
        public PassiveChipButton[] passiveChips;

        /// <summary>Resolves the parts of a spawned HeroPanel prefab instance. Stats keep the printed values.</summary>
        public static HeroPanelView FromInstance(GameObject go)
        {
            var nameBg = go.transform.Find("NameBG");
            return new HeroPanelView
            {
                root = go,
                nameText = nameBg != null ? nameBg.GetComponentInChildren<TextMeshProUGUI>(true) : null,
                portrait = go.GetComponentInChildren<HeroButtonHandler>(true),
                passiveChips = go.GetComponentsInChildren<PassiveChipButton>(true),
            };
        }
    }

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
    [SerializeField] private HeroPanelView playerHero;
    [SerializeField] private HeroPanelView enemyHero;

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
        Refresh();
    }

    // ---------------------------------------------------------------- Steps

    private void Refresh()
    {
        ClearOptions();
        _nextMatchPinned = false;

        bool inProgress = GauntletRun.Outcome == GauntletRun.RunOutcome.InProgress;

        // The enemy is rolled as soon as the hero is known (so a mirror can be avoided), and kept until
        // the battle is fought, so the preview and the fight agree.
        if (inProgress && GauntletRun.Hero != null && GauntletRun.EnemyHero == null)
            GauntletRun.PrepareNextBattle();

        RefreshDeck();
        RefreshHero(playerHero, GauntletRun.Hero, carried: true);
        RefreshNextMatch();
        RefreshTrack();
        SetNextMatchVisible(false);

        if (menuButtonLabel != null) menuButtonLabel.text = mainMenuLabel;
        if (menuButton != null) menuButton.gameObject.SetActive(!inProgress);

        if (!inProgress) ShowResult();
        else if (GauntletRun.Hero == null) ShowHeroPick();
        else if (GauntletRun.PendingPicks > 0) ShowCardPick();
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
        });
    }

    private void ShowCardPick()
    {
        int pickNumber = GauntletRun.PicksThisStep - GauntletRun.PendingPicks + 1;
        SetDraftPanel($"Draft a New Card For Your Deck {pickNumber}/{GauntletRun.PicksThisStep}");
        SetPrimary(null);

        SpawnOptions(GauntletRun.RollCardOptions(), picked =>
        {
            GauntletRun.AddDraftedCard(picked);
            Refresh();
        });
    }

    /// <summary>Picks done: the track's upcoming skull takes over (see <see cref="OnNodeClicked"/>).</summary>
    private void ShowBriefing()
    {
        SetDraftPanel(null);
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
        && GauntletRun.PendingPicks == 0;

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

    /// <param name="passives">Shown in place of the hero's own passives when given (a blessed enemy's).</param>
    private void RefreshHero(HeroPanelView panel, HeroSO hero, bool carried, IReadOnlyList<HeroPassiveSO> passives = null)
    {
        if (panel == null) return;

        if (panel.nameText != null) panel.nameText.text = hero != null ? hero.cardName : "???";

        if (panel.portrait != null)
        {
            if (hero != null) panel.portrait.SetHero(hero);
            else panel.portrait.SetRandom(null);
        }

        if (hero != null)
        {
            int health, attack;
            if (carried && GauntletRun.TryGetCarriedStats(out int hp, out _, out int atk))
            {
                health = hp;
                attack = atk;
            }
            else
            {
                health = carried ? hero.health : EnemyStartingHealth(hero);
                attack = hero.attack;
            }

            if (panel.healthText != null) panel.healthText.text = health.ToString();
            if (panel.attackText != null) panel.attackText.text = attack.ToString();
        }

        if (panel.passiveChips == null) return;
        if (passives == null && hero != null) passives = hero.passives;
        for (int i = 0; i < panel.passiveChips.Length; i++)
        {
            var chip = panel.passiveChips[i];
            if (chip == null) continue;
            chip.ShowPassive(passives != null && i < passives.Count ? passives[i] : null);
        }
    }

    /// <summary>Same scaling (and Bonus Health) <see cref="GauntletRun.ApplyEnemySetup"/> applies in the match.</summary>
    private static int EnemyStartingHealth(HeroSO hero) =>
        GauntletRun.EnemyStartingHealth(hero.defHealth > 0 ? hero.defHealth : hero.health);

    private void RefreshNextMatch()
    {
        var encounter = GauntletRun.CurrentEncounter;
        var enemy = GauntletRun.EnemyHero;

        RefreshHero(enemyHero, enemy, carried: false, enemy != null ? GauntletRun.EnemyPassives : null);

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

    private void RefreshDeck()
    {
        var deck = GauntletRun.Deck;

        if (deckHeaderText != null) deckHeaderText.text = deck.Count > 0 ? $"DECK ({deck.Count})" : "DECK";
        if (deckEntryTemplate == null) return;

        // One entry per card, by mana cost then name; duplicates get their own row.
        var cards = new List<CardSO>(deck.Count);
        foreach (var card in deck)
            if (card != null) cards.Add(card);
        cards.Sort((a, b) =>
        {
            int byCost = a.cost.CompareTo(b.cost);
            return byCost != 0 ? byCost : string.CompareOrdinal(a.cardName, b.cardName);
        });

        while (_deckEntries.Count < cards.Count)
        {
            var entry = Instantiate(deckEntryTemplate, deckEntryTemplate.transform.parent);
            _deckEntries.Add(entry);
        }

        for (int i = 0; i < _deckEntries.Count; i++)
        {
            var entry = _deckEntries[i];
            bool used = i < cards.Count;
            entry.gameObject.SetActive(used);
            if (!used) continue;

            var card = cards[i];
            entry.Card = card;
            entry.SetName(card.cardName);
            entry.SetCost(card.cost);
        }
    }

    /// <summary>
    /// Removes the stand-ins the layout was authored with: the example option cards and the example
    /// deck entries (the template is kept, hidden, to clone from).
    /// </summary>
    private void ClearAuthoredPlaceholders()
    {
        if (optionsContainer != null)
            for (int i = optionsContainer.childCount - 1; i >= 0; i--)
                Destroy(optionsContainer.GetChild(i).gameObject);

        if (deckEntryTemplate != null)
        {
            var parent = deckEntryTemplate.transform.parent;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (child != deckEntryTemplate.gameObject) Destroy(child);
            }
            deckEntryTemplate.gameObject.SetActive(false);
        }
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
                    onPicked(option);
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

            var view = HeroPanelView.FromInstance(go);
            RefreshHero(view, hero, carried: false);

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
