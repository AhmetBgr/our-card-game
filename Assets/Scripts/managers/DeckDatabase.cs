using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DeckDatabase : Singleton<DeckDatabase>
{
    public Dictionary<string, CardSO> cardsByName = new Dictionary<string, CardSO>();
    public Dictionary<string, CardSO> upgradedCardsByName = new Dictionary<string, CardSO>();

    public List<CardSO> AllCards = new List<CardSO>();

    protected override void Awake()
    {
        base.Awake();
        LoadCards();
    }

    void LoadCards()
    {
        cardsByName.Clear();

        CardSO[] cards;
#if UNITY_EDITOR
        cards = Array.ConvertAll(
            AssetDatabase.FindAssets("t:CardSO", new[] { "Assets/Resources/Cards" }),
            guid => AssetDatabase.LoadAssetAtPath<CardSO>(AssetDatabase.GUIDToAssetPath(guid))
        );
        cards = Array.FindAll(cards, c => c != null &&
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(c)).Replace('\\', '/') == "Assets/Resources/Cards");
#else
        cards = Resources.LoadAll<CardSO>("Cards");
#endif

        foreach (CardSO card in cards)
        {
            if(String.IsNullOrEmpty(card.cardName)) continue;
            if (card.isUpgraded) {
                if (upgradedCardsByName.ContainsKey(card.cardName))
                {
                    Debug.LogWarning($"Duplicate CardSO name found: {card.cardName}");
                    continue;
                }

                upgradedCardsByName.Add(card.cardName, card);
                AllCards.Add(card);
                continue;
            }
            if (cardsByName.ContainsKey(card.cardName))
            {
                Debug.LogWarning($"Duplicate CardSO name found: {card.cardName}");
                continue;
            }
            cardsByName.Add(card.cardName, card);
            AllCards.Add(card);
        }
        Debug.Log($"Loaded {cardsByName.Count} cards into CardDatabase.");
    }

    public CardSO GetCard(string cardName)
    {
        if (cardsByName.TryGetValue(cardName, out CardSO card))
            return card;

        if (upgradedCardsByName.TryGetValue(cardName, out CardSO card2))
            return card2;
        
        Debug.LogWarning($"Card not found: {cardName}");
        return null;
    }
}
