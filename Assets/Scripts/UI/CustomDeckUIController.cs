using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CustomDeckUIController : MonoBehaviour
{
    /// <summary>One row of the deck view. A list rather than a name-keyed map, because a deck may hold the same card twice.</summary>
    private struct Entry
    {
        public string name;
        public CardButtonHandler button;
    }

    private readonly List<Entry> entries = new List<Entry>();

    [SerializeField] private Transform cardsContainer;
    [SerializeField] private GameObject randomText;

    private List<CardButtonHandler> cardButtonPool;
    private bool hideCardContents;

    // The view is laid out for the classic 10 rows; a bigger deck (a modifier allows up to 30) is split
    // into pages of this many, turned by the page buttons on the panel.
    [SerializeField] private int cardsPerPage = 10;
    private int currentPage;

    public int CurrentPage => currentPage;
    public int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)entries.Count / cardsPerPage));

    /// <summary>Raised whenever the page shown or the number of pages may have changed.</summary>
    public event Action PagesChanged;

    // The panel this deck view lives in — it owns the side whose deck we mutate.
    private DeckPanelController owner;

    void Awake()
    {
        owner = GetComponentInParent<DeckPanelController>(true);
    }

    public void Initialize(DeckData deck, bool isRandomDeck = false)
    {
        hideCardContents = isRandomDeck;

        if (randomText != null)
            randomText.SetActive(isRandomDeck);

        cardButtonPool ??= cardsContainer.GetComponentsInChildren<CardButtonHandler>(true).ToList();

        foreach (var cardButton in cardButtonPool)
            cardButton.gameObject.SetActive(false);

        entries.Clear();
        currentPage = 0;

        foreach (var name in deck.Deck)
        {
            AddCard(name, deck.isLocked);
        }

        UpdateOrder();
    }

    public void AddCard(string name, bool isLocked = false)
    {
        var card = name;

        var cardSO = DeckDatabase.Instance.GetCard(name);
        if (cardSO == null)
        {
            Debug.LogWarning($"Card with name {name} not found in database.");
            return;
        }

        // Rows on other pages are inactive too, so "free" means not holding a card, not inactive.
        var cardButton = cardButtonPool.FirstOrDefault(b => !entries.Any(e => e.button == b));
        if (cardButton == null)
        {
            // The authored pool covers the classic deck size; a modifier that allows more grows it.
            if (cardButtonPool.Count == 0)
            {
                Debug.LogWarning($"No card button to clone for card {name}.");
                return;
            }

            cardButton = Instantiate(cardButtonPool[0], cardsContainer);
            cardButton.name = cardButtonPool[0].name;
            cardButtonPool.Add(cardButton);
        }

        cardButton.gameObject.SetActive(true);
        cardButton.OnClicked = isLocked ? null : () => {
            owner.RemoveFromCurrentCustomDeck(card);
        };

        cardButton.Card = cardSO;

        if (hideCardContents)
        {
            cardButton.SetHidden();
        }
        else
        {
            cardButton.SetName(name);
            cardButton.SetCost(cardSO.cost);
        }

        entries.Add(new Entry { name = name, button = cardButton });
    }

    /// <summary>Removes ONE copy of the card: with duplicates allowed, the others stay.</summary>
    public void RemoveCard(string cardName)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].name != cardName) continue;

            var cardButton = entries[i].button;
            entries.RemoveAt(i);

            cardButton.OnClicked = null;
            cardButton.gameObject.SetActive(false);
            break;
        }

        ShowPage(currentPage);
    }

    public void UpdateOrder()
    {
        // Keep entries in display order so a page is a plain slice of them.
        var sorted = entries.OrderBy(e => e.button.Card.cost).ToList();
        entries.Clear();
        entries.AddRange(sorted);

        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].button.transform.SetSiblingIndex(i);
        }

        ShowPage(currentPage);
    }

    /// <summary>Turns to the page holding the last copy of the card, so a freshly added card is in view.</summary>
    public void ShowPageOf(string cardName)
    {
        int index = entries.FindLastIndex(e => e.name == cardName);
        if (index >= 0) ShowPage(index / cardsPerPage);
    }

    public void ShowPage(int page)
    {
        currentPage = Mathf.Clamp(page, 0, PageCount - 1);

        int start = currentPage * cardsPerPage;
        for (int i = 0; i < entries.Count; i++)
            entries[i].button.gameObject.SetActive(i >= start && i < start + cardsPerPage);

        PagesChanged?.Invoke();
    }
}
