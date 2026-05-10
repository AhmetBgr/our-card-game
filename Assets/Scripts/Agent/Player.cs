using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Agent
{
    public enum State
    {
        SelectingMinionForAttack, SelectingMinion, SelectingCell, Waiting, None
    }

    public State curState = State.None;
    public override int availibleMana
    {
        get { return _availibleMana; }
        set
        {
            int oldValue = _availibleMana;
            _availibleMana = value;

            OnPlayerManaChanged?.Invoke(value, oldValue);
        }
    }

    public static event Action<int, int> OnPlayerManaChanged;

    protected override void Awake()
    {
        // todo: get selected deck
        SaveManager saveManager = SaveManager.Instance;
        var selectedDeck = saveManager.saveData.Decks[saveManager.saveData.SelectedDeckIndex];

        deck.Clear();
        // Load Deck
        foreach (var cardName in selectedDeck.Deck)
        {
            CardSO cardSO = DeckDatabase.Instance.GetCard(cardName);
            if (cardSO != null)
            {
                deck.Add(cardSO);
            }
            else
            {
                Debug.LogWarning($"Card with name {cardName} not found in database.");
            }
        }

        // Shuffle Deck
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1); // UnityEngine.Random
            var temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        deckViewHandler.UpdateView(deck.Count, deck[deck.Count - 1].isUpgraded);
    }

    public override IEnumerator PlayTurn()
    {
        while (GameManager.Instance.isPlayerTurn)
        {
            yield return null;
        }
    }

    public override IEnumerator SkipTurn()
    {
        yield break;
    }

    public override bool IsPlayer()
    {
        return true;
    }
}
