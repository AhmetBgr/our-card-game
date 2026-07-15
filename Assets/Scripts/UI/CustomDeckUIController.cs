using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class CustomDeckUIController : MonoBehaviour
{
    private Dictionary<string, CardButtonHandler> cards = new Dictionary<string, CardButtonHandler>();

    [SerializeField] private Transform cardsContainer;
    [SerializeField] private GameObject randomText;

    private List<CardButtonHandler> cardButtonPool;
    private bool hideCardContents;

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

        cards.Clear();

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
            Debug.LogWarning($"No available card button slot for card {name}.");
            return;
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

        cards.Add(name, cardButton);
    }
    public void RemoveCard(string cardName)
    {
        if (cards.ContainsKey(cardName)) {

            var cardButton = cards[cardName];
            cards.Remove(cardName);

            cardButton.OnClicked = null;
            cardButton.gameObject.SetActive(false);
        }

    }
    public void UpdateOrder()
    {
        var cardbuttons = cards.Values.ToList();
        cardbuttons.Sort((a, b) => a.Card.cost.CompareTo(b.Card.cost));

        for (int i = 0; i < cardbuttons.Count; i++)
        {
            cardbuttons[i].transform.SetSiblingIndex(i);
        }
    }
}
