using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An authored list of cards that an effect can roll from (e.g. "Something Happens" playing a random
/// spell). Deliberately a hand-authored list rather than a filter over DeckDatabase: what a card can
/// roll is a design decision, so adding a new spell to the game shouldn't silently change the odds —
/// or hand a roll a card that was never meant to be castable this way.
/// </summary>
[CreateAssetMenu(fileName = "CardPool", menuName = "New Card Pool")]
public class CardPoolSO : ScriptableObject
{
    [Tooltip("Cards this pool can roll. A card is only rollable if it is listed here.")]
    public List<CardSO> cards = new List<CardSO>();

    /// <summary>Random entry, skipping null slots. Returns null when the pool has no usable card.</summary>
    public CardSO GetRandom()
    {
        List<CardSO> valid = cards.FindAll(c => c != null);

        if (valid.Count == 0) return null;

        return valid[Random.Range(0, valid.Count)];
    }
}
