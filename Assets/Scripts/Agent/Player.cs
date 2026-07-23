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

    // Deliberately does not call base.Awake(): that one applies the OPPONENT's saved selection and
    // seeds the deck from deckSO. Everything the player does share with it is mirrored here by hand.
    protected override void Awake()
    {
        ApplySavedSelection(SelectionSide.Player);
        ShuffleDeck();
        RefreshDeckView();
        SpawnPassiveUI();
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
