using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckUIController : MonoBehaviour
{

    [SerializeField] protected CardButtonHandler cardButtonPrefab;
    void Start()
    {
        
    }

    public virtual void Initialize(DeckData deck)
    {
        var allCardSOs = DeckDatabase.Instance.AllCards;


        foreach (var name in deck.Deck)
        {
            var cardButton = Instantiate(cardButtonPrefab, transform);
            cardButton.SetName(name);
        }
    }
}
