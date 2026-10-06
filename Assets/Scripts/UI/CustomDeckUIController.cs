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

    // The rows overlap once the deck outgrows the room the view was laid out for (the classic 10),
    // so an oversized deck still fits the carousel entry. Captured from the authored layout on first use.
    private VerticalLayoutGroup layout;
    private float authoredSpacing;
    private bool layoutCaptured;

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

        var cardButton = cardButtonPool.FirstOrDefault(b => !b.gameObject.activeSelf);
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

        UpdateOverlap();
    }

    public void UpdateOrder()
    {
        var sorted = entries.OrderBy(e => e.button.Card.cost).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].button.transform.SetSiblingIndex(i);
        }

        UpdateOverlap();
    }

    /// <summary>
    /// Tightens the row spacing (into overlap if it must) so the column never grows past the height
    /// the classic deck size fills. Restores the authored spacing while the deck fits.
    /// </summary>
    private void UpdateOverlap()
    {
        if (!layoutCaptured)
        {
            layout = cardsContainer != null ? cardsContainer.GetComponent<VerticalLayoutGroup>() : null;
            authoredSpacing = layout != null ? layout.spacing : 0f;
            layoutCaptured = true;
        }

        if (layout == null || entries.Count == 0) return;

        int capacity = SaveManager.Instance != null ? SaveManager.Instance.DeckSize : 10;
        int count = entries.Count;

        if (count <= capacity)
        {
            layout.spacing = authoredSpacing;
            return;
        }

        float rowHeight = ((RectTransform)entries[0].button.transform).rect.height;
        float roomHeight = capacity * rowHeight + (capacity - 1) * authoredSpacing;
        layout.spacing = (roomHeight - count * rowHeight) / (count - 1);
    }
}
