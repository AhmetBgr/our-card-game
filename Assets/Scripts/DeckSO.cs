using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDeck", menuName = "Cards/Deck")]
public class DeckSO : ScriptableObject
{
    public string deckName = "Default Deck";
    public List<CardSO> cards = new List<CardSO>();

    // Target mana curve for a 10-card deck: (allowed costs, how many cards to draw from them).
    public static readonly (int[] costs, int count)[] ManaCurve = new (int[] costs, int count)[]
    {
        (new[] { 0, 1 }, 2),
        (new[] { 2 }, 3),
        (new[] { 3 }, 2),
        (new[] { 4 }, 2),
        (new[] { 5 }, 1),
    };
}
