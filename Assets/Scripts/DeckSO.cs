using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDeck", menuName = "Cards/Deck")]
public class DeckSO : ScriptableObject
{
    public string deckName = "Default Deck";
    public List<CardSO> cards = new List<CardSO>();
}
