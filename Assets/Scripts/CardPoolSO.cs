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

    [Tooltip("How many options a Discover from this pool puts in front of the player.")]
    public int choiceCount = 3;

    [Tooltip("Mana knocked off a card discovered into hand from this pool. Never takes a cost below 0.")]
    public int costReduction = 3;

    /// <summary>Every non-null entry, as a fresh list the caller may filter in place.</summary>
    public List<CardSO> UsableCards()
    {
        return cards.FindAll(c => c != null);
    }

    /// <summary>Random entry, skipping null slots. Returns null when the pool has no usable card.</summary>
    public CardSO GetRandom()
    {
        List<CardSO> valid = UsableCards();

        if (valid.Count == 0) return null;

        return valid[Random.Range(0, valid.Count)];
    }

    /// <summary>
    /// Up to <paramref name="count"/> DISTINCT random entries from <paramref name="source"/>. Takes the
    /// source as a parameter rather than reading `cards` so a caller can offer a pre-filtered set — the
    /// Discover verb narrows the pool to cards that can actually resolve on the current board first.
    /// Returns fewer than `count` only when the source is smaller.
    /// </summary>
    public static List<CardSO> PickDistinct(List<CardSO> source, int count)
    {
        var picked = new List<CardSO>();
        if (source == null || count <= 0) return picked;

        var remaining = new List<CardSO>(source);
        while (picked.Count < count && remaining.Count > 0)
        {
            int index = Random.Range(0, remaining.Count);
            picked.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        return picked;
    }
}
