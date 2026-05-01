using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Tilemaps;
using UnityEngine;

public class CustomDeckUIController : MonoBehaviour
{
    private List<CardButtonHandler> cards = new List<CardButtonHandler>();

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
        cardButton.OnClicked = () => {
            DeckPanelController.Instance.RemoveFromCurrentCustomDeck(card);
            if(!isLocked)
                RemoveCard(cardButton);
        };
        var cardSO = DeckDatabase.Instance.GetCard(name);
        cardButton.Card = cardSO;
        cardButton.SetName(name);
        cardButton.SetCost(cardSO.cost);
        cards.Add(cardButton);
    }
    public void RemoveCard(CardButtonHandler cardButtonHandler)
    {
        if (cards.Contains(cardButtonHandler)) 
            cards.Remove(cardButtonHandler);

        Destroy(cardButtonHandler.gameObject);
    }
    public void UpdateOrder()
    {
        foreach (var item in cards)
        {
            item.transform.SetParent(transform.parent);
        }
        cards.Sort((a, b) => a.Card.cost.CompareTo(b.Card.cost));

        foreach (var item in cards)
        {
            item.transform.SetParent(transform);
        }
    }
}
