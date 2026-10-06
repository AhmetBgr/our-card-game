using System;
using System.Collections.Generic;

/// <summary>
/// What a deck may contain. The mode supplies its base rules and modifiers relax them
/// (<see cref="GameModifierSO.ModifyDeckRules"/>); the deck panel and SaveManager.AddCard ask the
/// result rather than hard-coding the classic 10-unique-normal-cards rule.
/// </summary>
[Serializable]
public struct DeckRules
{
    public int minSize;
    public int maxSize;
    public bool allowDuplicates;
    public bool allowUpgraded;

    /// <summary>The rules the game shipped with: exactly <paramref name="size"/> unique, non-upgraded cards.</summary>
    public static DeckRules Standard(int size) => new DeckRules
    {
        minSize = size,
        maxSize = size,
        allowDuplicates = false,
        allowUpgraded = false
    };

    /// <summary>Whether one more copy of <paramref name="card"/> may go into <paramref name="deck"/>.</summary>
    public bool CanAdd(IReadOnlyList<string> deck, CardSO card, out string reason)
    {
        reason = null;
        if (card == null) { reason = "Unknown card"; return false; }

        if (deck.Count >= maxSize) { reason = $"Deck is full ({maxSize})"; return false; }
        if (!allowUpgraded && card.isUpgraded) { reason = "Upgraded cards are not allowed"; return false; }

        if (!allowDuplicates)
        {
            for (int i = 0; i < deck.Count; i++)
                if (deck[i] == card.cardName) { reason = "Duplicate cards are not allowed"; return false; }
        }

        return true;
    }

    /// <summary>
    /// Whether an existing deck satisfies these rules. Checked against the saved list rather than at
    /// add time only, so a deck built while a rule was relaxed reads as invalid once it is tightened
    /// again -- nothing is ever removed from the player's deck behind their back.
    /// </summary>
    public bool IsValid(IReadOnlyList<string> deck, out string reason)
    {
        reason = null;

        if (deck.Count < minSize)
        {
            reason = minSize == maxSize ? $"Deck needs {minSize} cards" : $"Deck needs at least {minSize} cards";
            return false;
        }

        if (deck.Count > maxSize)
        {
            reason = minSize == maxSize ? $"Deck needs {maxSize} cards" : $"Deck can hold at most {maxSize} cards";
            return false;
        }

        if (!allowDuplicates)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < deck.Count; i++)
                if (!seen.Add(deck[i])) { reason = "Deck contains duplicate cards"; return false; }
        }

        if (!allowUpgraded && DeckDatabase.Instance != null)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                CardSO card = DeckDatabase.Instance.GetCard(deck[i]);
                if (card != null && card.isUpgraded) { reason = "Deck contains upgraded cards"; return false; }
            }
        }

        return true;
    }
}
