using System.Collections;
using UnityEngine;

public enum TurnState { Start, PlayerTurn, OpponentTurn, End }

public class TurnManager : Singleton<TurnManager>
{

    private TurnState currentState;
    private bool isPlayerTurn;


    void Start()
    {
        StartGame();
    }

    void StartGame()
    {
        isPlayerTurn = true;
        StartCoroutine(StartTurn());
    }

    IEnumerator StartTurn()
    {
        currentState = TurnState.Start;
        Debug.Log("Starting Turn: " + (isPlayerTurn ? "Player" : "Opponent"));

        yield return new WaitForSeconds(1f);

        if (isPlayerTurn)
            StartPlayerTurn();
        else
            StartOpponentTurn();
    }

    void StartPlayerTurn()
    {
        currentState = TurnState.PlayerTurn;
        Debug.Log("Player's Turn");
    }

    public void EndPlayerTurn()
    {
        if (currentState != TurnState.PlayerTurn) return;

        Debug.Log("Ending Player's Turn");
        StartCoroutine(SwitchTurn());
    }

    void StartOpponentTurn()
    {
        currentState = TurnState.OpponentTurn;
        Debug.Log("Opponent's Turn");
        StartCoroutine(OpponentAI());
    }

    IEnumerator OpponentAI()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log("Opponent has played their turn");
        StartCoroutine(SwitchTurn());
    }

    IEnumerator SwitchTurn()
    {
        currentState = TurnState.End;
        yield return new WaitForSeconds(1f);

        isPlayerTurn = !isPlayerTurn;
        StartCoroutine(StartTurn());
    }

    public bool IsPlayerTurn()
    {
        return isPlayerTurn && currentState == TurnState.PlayerTurn;
    }
}
