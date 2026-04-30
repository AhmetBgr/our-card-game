using DG.Tweening.Core.Easing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Progress;

public class AllCardsUIController : MonoBehaviour
{
    private Dictionary<string, CardButtonHandler> allCards = new Dictionary<string, CardButtonHandler>();
    [SerializeField] protected CardButtonHandler cardButtonPrefab;

    public void Initialize()
    {
        var allCardSOs = DeckDatabase.Instance.AllCards;

        foreach (var item in allCardSOs)
        {
            var card = item.name;

            var cardButton = Instantiate(cardButtonPrefab, transform);
            cardButton.OnClicked = () => {
                if(DeckPanelController.Instance.TryAddToCurrentCustomDeck(card))
                    UpdateSelectableCards();
            };

            cardButton.SetName(card);

            allCards.Add(card, cardButton);
        }
    }

    public void UpdateSelectableCards()
    {

        List<string> cardsInCustomDeck = SaveManager.Instance.saveData.Decks[SaveManager.Instance.saveData.SelectedDeckIndex].Deck;
        foreach (var button in allCards.Values)
        {
            button.Button.interactable = true;

        }

        foreach (var item in cardsInCustomDeck)
        {
            if (!allCards.ContainsKey(item)) continue;

            allCards[item].Button.interactable = false;
        }


    }
}
