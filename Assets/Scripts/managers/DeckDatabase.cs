using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckDatabase : Singleton<DeckDatabase>
{
    public Dictionary<string, CardSO> cardsByName = new Dictionary<string, CardSO>();

    public List<CardSO> AllCards = new List<CardSO>();

    protected override void Awake()
    {
        base.Awake();
        LoadCards();
    }

    void LoadCards()
    {
        cardsByName.Clear();

        CardSO[] cards = Resources.LoadAll<CardSO>("Cards");

        foreach (CardSO card in cards)
        {
            if (cardsByName.ContainsKey(card.name))
            {
                Debug.LogWarning($"Duplicate CardSO name found: {card.name}");
                continue;
            }

            cardsByName.Add(card.name, card);
            AllCards.Add(card);
        }

        Debug.Log($"Loaded {cardsByName.Count} cards into CardDatabase.");
    }

    public CardSO GetCard(string cardName)
    {
        if (cardsByName.TryGetValue(cardName, out CardSO card))
            return card;

        Debug.LogWarning($"Card not found: {cardName}");
        return null;
    }
}
