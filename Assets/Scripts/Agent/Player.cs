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
}
