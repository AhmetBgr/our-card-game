using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckView : MonoBehaviour
{

    [SerializeField] private CardButtonHandler cardButtonPrefab;
    void Start()
    {
        
    }

    public void Initialize(DeckData deck)
    {
        var allCardSOs = DeckDatabase.Instance.AllCards;

        foreach (var name in deck.Deck)
        {
            var cardButton = Instantiate(cardButtonPrefab, transform);
            cardButton.SetName(name);
        }
    }
}
