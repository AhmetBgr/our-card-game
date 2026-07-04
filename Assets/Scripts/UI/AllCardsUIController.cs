using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AllCardsUIController : MonoBehaviour
{
    private Dictionary<string, CardButtonHandler> allCards = new Dictionary<string, CardButtonHandler>();
    [SerializeField] protected CardButtonHandler cardButtonPrefab;
    [SerializeField] private List<Button> pageButtons;
    [SerializeField] private int cardsPerPage = 8;

    private List<CardButtonHandler> orderedCards = new List<CardButtonHandler>();
    private int currentPage = 0;

    public void Initialize()
    {
        var allCardSOs = new List<CardSO>(DeckDatabase.Instance.AllCards);
        allCardSOs.RemoveAll(c => c.isUpgraded);
        allCardSOs.Sort((a, b) => a.cost.CompareTo(b.cost));

        foreach (var item in allCardSOs)
        {
            var name = item.cardName;
            var cardButton = Instantiate(cardButtonPrefab, transform);
            cardButton.OnClicked = () => {
                if (!DeckPanelController.Instance.IsCurCustomDeckLocked())
                {
                    if (!cardButton.Button.interactable)
                    {
                        DeckPanelController.Instance.RemoveFromCurrentCustomDeck(name);
                    }
                    else
                    {
                        if (DeckPanelController.Instance.TryAddToCurrentCustomDeck(name))
                            UpdateSelectableCards();
                    }
                }
            };

            cardButton.SetName(name);
            cardButton.SetCost(item.cost);
            allCards.Add(name, cardButton);
            orderedCards.Add(cardButton);
        }

        SetupPageButtons();
        ShowPage(0);
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

        // The mystery/randomized deck hides its contents (its cards show as "???"). Marking
        // that deck's cards as selected here would grey out exactly the cards it contains in
        // the all-cards grid, revealing the hidden deck — so leave every card unmarked while
        // it's the selected deck. It's locked, so nothing can be added/removed anyway.
        if (SaveManager.Instance.saveData.SelectedDeckIndex == SaveManager.MysteryDeckIndex)
            return;

        List<string> cardsInCustomDeck = SaveManager.Instance.saveData.Decks[SaveManager.Instance.saveData.SelectedDeckIndex].Deck;
        foreach (var item in cardsInCustomDeck)
        {
            if (!allCards.ContainsKey(item)) continue;
            allCards[item].Button.interactable = false;
        }
    }
}
