using System; 
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


public enum GameState { Setup, StartGame, PlayerTurn, OpponentTurn, EndGame }

public class GameManager : Singleton<GameManager>
{
    public GameObject minionprefab;
    public bool isPlayingCard = false;
    public IEnumerator curaction;
    private Queue<IEnumerator> actionQueue = new Queue<IEnumerator>();

    public List<MinionController> playerMinions = new List<MinionController>();
    public List<MinionController> opponentMinions = new List<MinionController>();


    public GameState currentState;
    public int maxMana = 0;
    private int _curPlayerMana;
    public int curPlayerMana
    {
        get { return _curPlayerMana; }
        set
        {
            int oldValue = _curPlayerMana;
            _curPlayerMana = value;

            OnPlayerManaChanged?.Invoke(value, oldValue);
        }
    }
    public bool isPlayerTurn;
    public int playerHealth = 30;
    public int opponentHealth = 30;

    public static event Action<GameState> OnTurnSwitch;
    public static event Action<int, int> OnPlayerManaChanged;


    void Start()
    {
        StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        yield return StartCoroutine(SetupGame());

        while (currentState != GameState.EndGame)
        {
            if (isPlayerTurn)
            {
                maxMana++;
                yield return StartCoroutine(PlayerTurn());
            }
            else
            {
                for (int x = 0; x < GridManager.Instance.GridWidth; x++)
                {
                    for (int y = 0; y < GridManager.Instance.GridHeight; y++)
                    {
                        Cell cell = GridManager.Instance.GetCell(new Vector2Int(x,y));

                        MinionController minion;

                        if(cell.obj != null && cell.obj.TryGetComponent(out minion))
                        {
                            Vector3Int pos = Vector3Int.RoundToInt(minion.transform.position) + Vector3Int.up;
                            if (minion.CanMove(pos))
                            {
                                minion.Move(pos);
                                yield return new WaitForSeconds(0.26f);

                                GridManager.Instance.InvokeGridChanged();
                            }
                            else
                            {
                                minion.FailedMove(Vector3.up);
                            }
                        }
                    }
                }

                /*foreach (var minion in playerMinions)
                {
                    minion.PlanMove(Vector3Int.up);
                }
                */
                /*foreach (var minion in playerMinions)
                {
                    Vector3Int pos = Vector3Int.RoundToInt(minion.transform.position) + Vector3Int.up;
                   
                    if (minion.CanMove(pos))
                    {

                        Debug.Log("can move");
                        minion.Move(pos);
                        yield return new WaitForSeconds(0.26f);

                        GridManager.Instance.InvokeGridChanged();

                    }
                    else
                    {
                        Debug.Log("cant move");

                        minion.FailedMove(Vector3Int.up);
                    }
                }*/
                /*foreach (var minion in playerMinions)
                {
                    Vector3Int pos = Vector3Int.RoundToInt(minion.transform.position) + Vector3Int.up;
                    if (minion.isMovementValidated && minion.plannedMoveDir != Vector3Int.zero)
                    {

                        Debug.Log("can move");
                        minion.Move(pos);

                    }
                    else
                    {
                        Debug.Log("cant move");

                        //minion.FailedMove(Vector3Int.up);
                    }
                }*/

                yield return new WaitForSeconds(0.5f);

                GridManager.Instance.InvokeGridChanged();

                yield return StartCoroutine(OpponentTurn());
            }
        }
    }

    IEnumerator SetupGame()
    {
        currentState = GameState.Setup;
        Debug.Log("Setting up game...");

        yield return new WaitForSeconds(2f);

        isPlayerTurn = true;
        currentState = GameState.StartGame;
        Debug.Log("Game Started!");

        yield return null;
    }

    IEnumerator PlayerTurn()
    {
        currentState = GameState.PlayerTurn;
        curPlayerMana = maxMana;
        Debug.Log("Player's Turn");
        OnTurnSwitch?.Invoke(currentState);

        while (isPlayerTurn)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);
    }

    public void EndPlayerTurn()
    {
        if (currentState != GameState.PlayerTurn) return;

        isPlayerTurn = false;
        Debug.Log("Player Ends Turn");

        StartCoroutine(OpponentTurn());


    }

    IEnumerator OpponentTurn()
    {
        currentState = GameState.OpponentTurn;
        Debug.Log("Opponent's Turn");
        OnTurnSwitch?.Invoke(currentState);

        yield return new WaitForSeconds(2f);

        Debug.Log("Opponent has played.");
        isPlayerTurn = true;

        yield return null;
    }

    public void CheckWinCondition()
    {
        if (playerHealth <= 0)
        {
            Debug.Log("Player Loses!");
            currentState = GameState.EndGame;
        }
        else if (opponentHealth <= 0)
        {
            Debug.Log("Player Wins!");
            currentState = GameState.EndGame;
        }
    }
    public void Addtoactions(IEnumerator action)
    {
        actionQueue.Enqueue(action);
    }
    public void RemoveFromActions(IEnumerator action)
    {
        actionQueue.Enqueue(action);
    }

    public void PlayCard(CardController card)
    {
        if (currentState != GameState.PlayerTurn || isPlayingCard || card.modal.cost > curPlayerMana) return;

        curPlayerMana -= card.modal.cost;
        isPlayingCard = true;
        actionQueue.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;

        card.card.OnPlay.Invoke();
        StartCoroutine(ExecuteActions(card));
    }
    IEnumerator ExecuteActions(CardController card)
    {
        while (actionQueue.Count > 0)
        {
            IEnumerator action = actionQueue.Dequeue();
            yield return StartCoroutine(action);
        }

        Destroy(card.gameObject);
        isPlayingCard = false;
        Debug.Log("All actions completed.");
    }
    public void SummonMinion(CardTEst card, Vector3 pos)
    {
        MinionController minion = Instantiate(minionprefab, pos, Quaternion.identity).GetComponent<MinionController>();
        minion.card = card;

        if (isPlayerTurn)
        {
            playerMinions.Add(minion);
            minion.isPlayerMinion = true;   

        }
        else
        {
            opponentMinions.Add(minion);
        }
    }
}
