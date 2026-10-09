using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AllCardsUIController : MonoBehaviour
{
    private Dictionary<string, CardButtonHandler> allCards = new Dictionary<string, CardButtonHandler>();
    [SerializeField] protected CardButtonHandler cardButtonPrefab;
    [SerializeField] private List<Button> pageButtons;
    [SerializeField] private int cardsPerPage = 8;

    [Header("Upgraded filter")]
    [Tooltip("Shown only while the deck rules allow upgraded cards. Holds the label and the switch.")]
    [SerializeField] private GameObject upgradedFilterRoot;
    [Tooltip("While on, the grid lists upgraded cards only; while off, base cards only.")]
    [SerializeField] private ToggleButton upgradedOnlyToggle;

    // Every card the grid can show, in cost order. Which of them are on offer depends on the deck
    // rules in force (upgraded cards only while a modifier allows them and the filter is on) -- see ApplyRules.
    private readonly List<CardButtonHandler> everyCard = new List<CardButtonHandler>();
    private List<CardButtonHandler> orderedCards = new List<CardButtonHandler>();
    private int currentPage = 0;
    private bool includeUpgraded;
    private bool upgradedOnly;

    // The panel this grid belongs to; it decides which side's deck a click edits.
    private DeckPanelController owner;

    void Awake()
    {
        owner = GetComponentInParent<DeckPanelController>(true);
    }

    public void Initialize()
    {
        var allCardSOs = new List<CardSO>(DeckDatabase.Instance.AllCards);
        allCardSOs.Sort((a, b) => a.cost.CompareTo(b.cost));

        foreach (var item in allCardSOs)
        {
            var name = item.cardName;
            if (allCards.ContainsKey(name)) continue;

            var cardButton = Instantiate(cardButtonPrefab, transform);
            cardButton.Card = item;
            cardButton.OnClicked = () => {
                if (!owner.IsCurCustomDeckLocked())
                {
                    if (!cardButton.Button.interactable)
                    {
                        owner.RemoveFromCurrentCustomDeck(name);
                    }
                    else
                    {
                        if (owner.TryAddToCurrentCustomDeck(name))
                            UpdateSelectableCards();
                    }
                }
            };

            cardButton.SetName(name);
            cardButton.SetCost(item.cost);
            allCards.Add(name, cardButton);
            everyCard.Add(cardButton);
        }

        if (upgradedOnlyToggle != null)
        {
            upgradedOnlyToggle.onValueChanged.RemoveListener(OnUpgradedOnlyChanged);
            upgradedOnlyToggle.onValueChanged.AddListener(OnUpgradedOnlyChanged);
        }

        ApplyRules(force: true);
    }

    /// <summary>
    /// Re-reads the deck rules and shows the cards they allow. Cheap when nothing changed: the grid is
    /// only rebuilt when the upgraded-cards rule flips.
    /// </summary>
    public void ApplyRules(bool force = false)
    {
        bool upgraded = MatchModifiers.CurrentDeckRules.allowUpgraded;
        if (!force && upgraded == includeUpgraded) return;

        includeUpgraded = upgraded;

        // The filter only means something while upgraded cards are on offer; it is hidden and reset
        // otherwise, so the rule going off can never leave the grid stuck on an empty upgraded list.
        if (upgradedFilterRoot != null) upgradedFilterRoot.SetActive(includeUpgraded);
        if (!includeUpgraded)
        {
            upgradedOnly = false;
            if (upgradedOnlyToggle != null) upgradedOnlyToggle.SetIsOn(false, notify: false);
        }

        RebuildOffer();
    }

    private void OnUpgradedOnlyChanged(bool on)
    {
        upgradedOnly = on && includeUpgraded;
        RebuildOffer();
    }

    private void RebuildOffer()
    {
        // Upgraded cards stay out of the grid until the filter is switched on, even while the rule
        // allows them; the switch then swaps the grid over to the upgraded cards.
        orderedCards = everyCard.FindAll(c => upgradedOnly ? c.Card.isUpgraded : !c.Card.isUpgraded);

        SetupPageButtons();
        ShowPage(currentPage);
    }

    private void SetupPageButtons()
    {
        int totalPages = Mathf.CeilToInt((float)orderedCards.Count / cardsPerPage);

        for (int i = 0; i < pageButtons.Count; i++)
        {
            bool active = i < totalPages;
            pageButtons[i].gameObject.SetActive(active);

            if (!active) continue;

            int pageIndex = i;
            pageButtons[i].onClick.RemoveAllListeners();
            pageButtons[i].onClick.AddListener(() => ShowPage(pageIndex));
        }
    }

    private void ShowPage(int page)
    {
        int totalPages = Mathf.CeilToInt((float)orderedCards.Count / cardsPerPage);
        currentPage = Mathf.Clamp(page, 0, totalPages - 1);

        // Cards outside the offered set stay hidden whatever page is up.
        foreach (var card in everyCard)
            card.gameObject.SetActive(false);

        int start = currentPage * cardsPerPage;
        for (int i = 0; i < orderedCards.Count; i++)
            orderedCards[i].gameObject.SetActive(i >= start && i < start + cardsPerPage);

        for (int i = 0; i < pageButtons.Count; i++)
            pageButtons[i].interactable = i != currentPage;
    }

    public void UpdateSelectableCards()
    {
        foreach (var button in allCards.Values)
            button.Button.interactable = true;

        var side = owner.Side;

        // The mystery/randomized deck hides its contents (its cards show as "???"). Marking
        // that deck's cards as selected here would grey out exactly the cards it contains in
        // the all-cards grid, revealing the hidden deck — so leave every card unmarked while
        // it's the selected deck. It's locked, so nothing can be added/removed anyway.
        if (SaveManager.Instance.GetSelectedDeckIndex(side) == SaveManager.MysteryDeckIndex)
            return;

        // With duplicates allowed a card in the deck is still on offer (clicking adds another copy;
        // copies are removed from the deck view), so nothing is greyed out.
        if (MatchModifiers.CurrentDeckRules.allowDuplicates)
            return;

        List<string> cardsInCustomDeck = owner.curCustomDeck;
        foreach (var item in cardsInCustomDeck)
        {
            if (!allCards.ContainsKey(item)) continue;
            allCards[item].Button.interactable = false;
        }
    }
}
