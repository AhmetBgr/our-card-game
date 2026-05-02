using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Tilemaps;
using UnityEngine;

public class CustomDeckUIController : MonoBehaviour
{
    private Dictionary<string, CardButtonHandler> cards = new Dictionary<string, CardButtonHandler>();

    [SerializeField] protected CardButtonHandler cardButtonPrefab;

    public void Initialize(DeckData deck)
    {

        foreach (var name in deck.Deck)
        {
            AddCard(name, deck.isLocked);
        }

        UpdateOrder();
    }

    public void AddCard(string name, bool isLocked = false)
    {
        var card = name;

        var cardButton = Instantiate(cardButtonPrefab, transform);

        if (!isLocked)
        {

            cardButton.OnClicked = () => {
                DeckPanelController.Instance.RemoveFromCurrentCustomDeck(card);
            };
        }

        var cardSO = DeckDatabase.Instance.GetCard(name);
        cardButton.Card = cardSO;
        cardButton.SetName(name);
        cardButton.SetCost(cardSO.cost);
        cards.Add(name, cardButton);
    }
    public void RemoveCard(string cardName)
    {
        if (cards.ContainsKey(cardName)) {

            var cardButton = cards[cardName];
            cards.Remove(cardName);

            Destroy(cardButton.gameObject);
        }

    }
    public void UpdateOrder()
    {
        var cardbuttons = cards.Values.ToList();
        foreach (var item in cardbuttons)
        {
            item.transform.SetParent(transform.parent);
        }
        cardbuttons.Sort((a, b) => a.Card.cost.CompareTo(b.Card.cost));

        foreach (var item in cardbuttons)
        {
            item.transform.SetParent(transform);
        }
    }
}
