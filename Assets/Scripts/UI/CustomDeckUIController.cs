using System.Collections;
using System.Collections.Generic;
using UnityEditor.Tilemaps;
using UnityEngine;

public class CustomDeckUIController : MonoBehaviour
{
    [SerializeField] protected CardButtonHandler cardButtonPrefab;

    public void Initialize(DeckData deck)
    {
        var allCardSOs = DeckDatabase.Instance.AllCards;

        foreach (var name in deck.Deck)
        {
            AddCard(name);
        }
    }

    public void AddCard(string name)
    {
        var card = name;

        var cardButton = Instantiate(cardButtonPrefab, transform);
        cardButton.OnClicked = () => {
            DeckPanelController.Instance.RemoveFromCurrentCustomDeck(card);
            
            Destroy(cardButton.gameObject);
        };

        cardButton.SetName(name);
    }
}
