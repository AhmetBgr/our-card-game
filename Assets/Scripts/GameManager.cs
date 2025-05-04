using System; 
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;

public enum GameState { Setup, StartGame, PlayerTurn, OpponentTurn, EndGame }

public class GameManager : Singleton<GameManager>
{
    public Player player;
    public Agent opponent;
    public SwitchController switchController;   
    public GameObject minionprefab;
    public Transform discardPile;
    public bool isPlayingCard = false;
    public IEnumerator curaction;

    public TextMeshProUGUI[] playerCornerDamageTexts;
    public TextMeshProUGUI[] opponentCornerDamageTexts;

    public List<SelectableEntity> selectables = new List<SelectableEntity>();
    private Queue<IEnumerator> actionQueue = new Queue<IEnumerator>();
    private Queue<IEnumerator> onTurnEndActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onCardDrawActions = new Queue<IEnumerator>();

    [SerializeField] private Queue<IEnumerator> defaultActionQueue;

    public GameState currentState;
    public int maxMana = 0;
    public bool isPlayerTurn;
    public bool isTesting = false;
    public bool isTestingFailed = false;
    public static event Action<GameState> OnTurnEnd;
    public static event Action<GameState> OnTurnStarted;

    void Start()
    {
        StartCoroutine(GameLoop());
    }
    private void Update()
    {
        if (currentState == GameState.EndGame) return;

        CheckWinCondition();
    }
    IEnumerator GameLoop()
    {
        yield return StartCoroutine(SetupGame());

        while (currentState != GameState.EndGame)
        {
            if (isPlayerTurn)
            {
                maxMana = Mathf.Clamp(maxMana +1, 0, 10);
                /*for (int i = player.minions.Count - 1; i >= 0; i--)
                {
                    if (player.minions[i] == null)
                    {
                        player.minions.RemoveAt(i);
                    }
                }
                for (int i = opponent.minions.Count - 1; i >= 0; i--)
                {
                    if (opponent.minions[i] == null)
                    {
                        opponent.minions.RemoveAt(i);
                    }
                }*/
                yield return StartCoroutine(PlayerTurn());


            }
            else
            {
                /*for (int i = opponent.minions.Count - 1; i >= 0; i--)
                {
                    if (opponent.minions[i] == null)
                    {
                        opponent.minions.RemoveAt(i);
                    }
                }
                for (int i = player.minions.Count - 1; i >= 0; i--)
                {
                    if (player.minions[i] == null)
                    {
                        player.minions.RemoveAt(i);
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
        switchController.PlaySwitchAnim(true);
        currentState = GameState.Setup;
        //Debug.Log("Setting up game...");
        yield return new WaitForSeconds(0.5f);

        player.DrawCard(true);
        yield return new WaitForSeconds(0.5f);

        opponent.DrawCard(false);
        yield return new WaitForSeconds(0.5f);

        player.DrawCard(true);
        yield return new WaitForSeconds(0.5f);

        opponent.DrawCard(false);
        yield return new WaitForSeconds(0.5f);

        player.DrawCard(true);
        yield return new WaitForSeconds(0.5f);

        opponent.DrawCard(false);
        yield return new WaitForSeconds(0.5f);

        /*player.DrawCard(true);
        yield return new WaitForSeconds(0.25f);

        opponent.DrawCard(false);
        yield return new WaitForSeconds(0.25f);

        */
        isPlayerTurn = true;

        currentState = GameState.StartGame;
        //Debug.Log("Game Started!");
        //OnTurnSwitch?.Invoke(currentState);

        yield return null;
    }

    IEnumerator PlayerTurn()
    {
        currentState = GameState.PlayerTurn;
        player.availibleMana = maxMana;
        player.curState = Player.State.Waiting;
        //Debug.Log("Player's Turn");
        player.DrawCard(true);
        StartCoroutine(InvokeOnTurnStarted());
        yield return StartCoroutine(player.PlayTurn()); 

        yield return new WaitForSeconds(0.5f);
    }
    public IEnumerator InvokeOnTurnEnd()
    {
        OnTurnEnd?.Invoke(currentState);

        yield break;
    }
    public IEnumerator InvokeOnTurnStarted()
    {
        OnTurnStarted?.Invoke(currentState);
        int opponentCornerDamage = (opponent.hero.modal.defHealth - opponent.hero.modal.health) / 10 + 1;
        int playerCornerCornerDamage = (player.hero.modal.defHealth - player.hero.modal.health) / 10 + 1;

        if (currentState == GameState.PlayerTurn)
        {
            List<MinionController> minionsToDamage = new List<MinionController>();

            foreach (var minion in opponent.minions)
            {
                Vector2Int index = minion.gridEntity.GetGridIndex();
                if (index.x % 2 == 0 && index.y % 2 == 0 && index.y == 2)
                {
                    Debug.Log("minion shoudl take damage");
                    minionsToDamage.Add(minion);
                    //minion.TakeDamage((opponent.hero.modal.health - opponent.hero.modal.health) / 10 + 1);
                }
            }
            foreach (var minion in minionsToDamage)
            {
                minion.TakeDamage(playerCornerCornerDamage);
            }
        }
        else
        {
            List<MinionController> minionsToDamage = new List<MinionController>();
            foreach (var minion in player.minions)
            {
                Vector2Int index = minion.gridEntity.GetGridIndex();
                if (index.x % 2 == 0 && index.y % 2 == 0 && index.y == 0)
                {
                    minionsToDamage.Add(minion);

                }
            }

            foreach (var minion in minionsToDamage)
            {
                minion.TakeDamage(opponentCornerDamage);
            }
        }
        foreach (var item in playerCornerDamageTexts)
        {
            item.text = "-"+playerCornerCornerDamage.ToString();
        }
        foreach (var item in opponentCornerDamageTexts)
        {
            item.text = "-"+opponentCornerDamage.ToString();
        }
        yield break;
    }

    public IEnumerator InvokeOnCardDrawActions()
    {
        for (int x = 0; x < GridManager.Instance.GridWidth; x++)
        {
            for (int y = 0; y < GridManager.Instance.GridHeight; y++)
            {
                Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                MinionController minion;

                if (cell.obj != null && cell.obj.TryGetComponent(out minion) && ((minion.owner == player && isPlayerTurn) || (minion.owner == opponent && !isPlayerTurn)))
                {
                    onCardDrawActions.Clear();
                    ActionHolder.selectedcell = null;
                    ActionHolder.selectedMinion = null;
                    ActionHolder.thisMinion = minion;
                    ActionHolder.thisCard = minion.card;
                    ActionHolder.selectedMinions.Clear();
                    ActionHolder.selectedMinions.Add(minion);
                    ActionHolder.selectedCells.Clear();
                    ActionHolder.selectedAgent = player;
                    ActionHolder.curActionsList = onCardDrawActions;

                    minion.card.OnOwnerDrawedCard.Invoke();

                    yield return StartCoroutine(ExecuteActions(onCardDrawActions));
                }
            }
        }

        SetPlayerMinionsReadyToAttack();
    }

    public IEnumerator EndPlayerTurn()
    {
        if (currentState != GameState.PlayerTurn) yield break;

        for (int x = 0; x < GridManager.Instance.GridWidth; x++)
        {
            for (int y = 0; y < GridManager.Instance.GridHeight; y++)
            {
                Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                MinionController minion;

                if (cell.obj != null && cell.obj.TryGetComponent(out minion) && minion.owner == player)
                {
                    onTurnEndActions.Clear();
                    ActionHolder.selectedcell = null;
                    ActionHolder.selectedMinion = null;
                    ActionHolder.thisMinion = minion;
                    ActionHolder.thisCard = minion.card;

                    ActionHolder.selectedMinions.Clear();
                    ActionHolder.selectedMinions.Add(minion);
                    ActionHolder.selectedCells.Clear();
                    ActionHolder.selectedAgent = player;
                    ActionHolder.curActionsList = onTurnEndActions;

                    minion.card.OnOwnerTurnEnd.Invoke();

                    yield return StartCoroutine(ExecuteActions(onTurnEndActions));
                }
            }
        }

        isPlayerTurn = false;

        switchController.PlaySwitchAnim(false);

        yield return new WaitForSeconds(0.5f);

        // movement
        for (int x = 0; x < GridManager.Instance.GridWidth; x++)
        {
            for (int y = 0; y < GridManager.Instance.GridHeight; y++)
            {
                Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                MinionController minion;


                if (cell.obj != null && cell.obj.TryGetComponent(out minion))
                {
                    if (!minion.modal.isPlayerMinion) continue;


                    Vector3Int pos = Vector3Int.RoundToInt(minion.transform.position) + Vector3Int.up;
                    if (minion.CanMove(pos))
                    {
                        minion.Move(pos);
                        yield return new WaitForSeconds(0.26f);

                        GridManager.Instance.InvokeGridChanged();
                    }
                    else
                    {
                        //minion.FailedMove(Vector3.up);
                    }
                }
            }
        }

        StartCoroutine(InvokeOnTurnEnd());

        //Debug.Log("Player Ends Turn");

        //StartCoroutine(OpponentTurn());
    }

    IEnumerator OpponentTurn()
    {

        currentState = GameState.OpponentTurn;
        //Debug.Log("Opponent's Turn");
        opponent.availibleMana = maxMana;

        opponent.DrawCard(false);

        StartCoroutine(InvokeOnTurnStarted());

        yield return StartCoroutine(opponent.PlayTurn());

        for (int x = GridManager.Instance.GridWidth-1; x >= 0; x--)
        {
            for (int y = GridManager.Instance.GridHeight-1; y >= 0 ; y--)
            {
                Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                MinionController minion;

                if (cell.obj != null && cell.obj.TryGetComponent(out minion) && minion.owner == opponent)
                {
                    onTurnEndActions.Clear();
                    ActionHolder.selectedcell = null;
                    ActionHolder.selectedMinion = null;
                    ActionHolder.thisMinion = minion;
                    ActionHolder.thisCard = minion.card;

                    ActionHolder.selectedMinions.Clear();
                    ActionHolder.selectedMinions.Add(minion);
                    ActionHolder.selectedCells.Clear();
                    ActionHolder.selectedAgent = opponent;
                    ActionHolder.curActionsList = onTurnEndActions;

                    minion.card.OnOwnerTurnEnd.Invoke();

                    yield return StartCoroutine(ExecuteActions(onTurnEndActions));
                }
            }
        }


        isPlayerTurn = true;

        switchController.PlaySwitchAnim(true);

        yield return new WaitForSeconds(0.5f);

        // movement
        for (int x = GridManager.Instance.GridWidth - 1; x >= 0; x--)
        {
            for (int y = GridManager.Instance.GridHeight - 1; y >= 0; y--)
            {
                Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                MinionController minion;

                if (cell.obj != null && cell.obj.TryGetComponent(out minion))
                {
                    if (minion.modal.isPlayerMinion) continue;

                    Vector3Int pos = Vector3Int.RoundToInt(minion.transform.position) + Vector3Int.down;
                    if (minion.CanMove(pos))
                    {
                        minion.Move(pos);
                        yield return new WaitForSeconds(0.26f);

                        GridManager.Instance.InvokeGridChanged();
                    }
                    else
                    {
                        //minion.FailedMove(Vector3.down);
                    }
                }
            }
        }

        yield return new WaitForSeconds(0.5f);

        //Debug.Log("Opponent has played.");
        StartCoroutine(InvokeOnTurnEnd());

        SetPlayerMinionsReadyToAttack();

        yield return null;
    }

    public void SetPlayerMinionsReadyToAttack()
    {

        if (!isPlayerTurn) return;

        Debug.Log("should set palyer minions ready to attack");

        player.curState = Player.State.Waiting;
        // Set player minions Ready to attack
        foreach (var item in player.minions)
        {
            item.SetReadyToAttack();
            item.selectable.SetSelectable(item.canAttack);
        }
    }

    public void CheckWinCondition()
    {
        if (player.hero.modal.health <= 0)
        {
            Debug.Log("Player Loses!");
            currentState = GameState.EndGame;
            PopupManager.Instance.OpenPopup(PopupManager.Instance.defeatPopup, 1f);
        }
        else if (opponent.hero.modal.health <= 0)
        {
            Debug.Log("Player Wins!");
            currentState = GameState.EndGame;
            PopupManager.Instance.OpenPopup(PopupManager.Instance.victoryPopup, 1f);

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

    public IEnumerator PlayCard(CardController card, Agent agent)
    {
        //Debug.Log("card.modal.cost: " + card.modal.cost);
        //Debug.Log("agent.availibleMana: " + agent.availibleMana);

        if ((currentState != GameState.PlayerTurn || isPlayingCard || card.modal.cost > agent.availibleMana) && !isTesting) yield break;

        ClearSelectables();

        isTesting = false;
        isTestingFailed = false;
        agent.availibleMana -= card.modal.cost;
        isPlayingCard = true;
        actionQueue.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        ActionHolder.selectedAgent = null;
        //ActionHolder.thisMinion = null;
        ActionHolder.thisCard = card.card;

        ActionHolder.selectedMinions.Clear();
        ActionHolder.selectedCells.Clear();
        ActionHolder.curActionsList = actionQueue;
        card.card.OnPlay.Invoke();

        Debug.LogWarning("playing card");

        yield return StartCoroutine(ExecuteActions(card));


        Debug.LogWarning("played card");



    }

    public void TestCard(CardController card)
    {
        isTestingFailed = false;

        actionQueue.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        ActionHolder.thisCard = card.card;

        ActionHolder.selectedMinions.Clear();
        ActionHolder.selectedCells.Clear();
        //ActionHolder.thisMinion = null;
        ActionHolder.selectedAgent = null;
        ActionHolder.curActionsList = actionQueue;

        isTesting = true;
        card.card.OnPlay.Invoke();
        
        //yield return StartCoroutine(ExecuteActions(card));

        //Debug.Log("executeing card actions");
        while (actionQueue.Count > 0)
        {
            //Debug.Log("executeing action");
            IEnumerator action = actionQueue.Dequeue();
            StartCoroutine(action);
        }

    }

    public IEnumerator ExecuteActions(CardController card)
    {
        isTesting = false;

        Debug.Log("executeing card actions");
        while (actionQueue.Count > 0)
        {
            Debug.Log("executeing action");
            IEnumerator action = actionQueue.Dequeue();
            yield return StartCoroutine(action);
        }
        Debug.Log("execution complete");
        isTesting = false;

        if (isTesting) 
        {
            Debug.Log("is testing true");

            yield break;
        } 
        //Destroy(card.gameObject);
        //Debug.Log("execution complete2");

        if (isPlayerTurn)
        {
            //Debug.Log("execution complete3");

            //player.handManager.RemoveFromHand(card);
            if (card != null) { 
                player.cardHandLayout.RemoveCard(card.transform);
                card.transform.DOScale(0f, 0.25f).OnComplete(() => 
                {
                    if(card.modal.upgradedVerdion!= null)
                    {
                        player.SpawnCardToDeck(card.modal.upgradedVerdion, true);
                    }
                    player.hand.Remove(card);

                    card.transform.SetParent(discardPile);
                    card.gameObject.SetActive(false);
                    //Destroy(card.gameObject);
                });
            }
        }
        else
        {
            //Debug.Log("execution complete4");

            //opponent.handManager.RemoveFromHand(card);
            if (card != null) {
                opponent.cardHandLayout.RemoveCard(card.transform);
                //Destroy(card.gameObject);

                card.transform.SetParent(opponent.cardHandLayout.transform.parent);
                card.transform.SetSiblingIndex(opponent.cardHandLayout.transform.parent.childCount - 1);
                card.transform.localRotation = Quaternion.identity;

                card.transform.DOScale(Vector3.one * 1.5f, 0.5f);
                card.transform.DORotate(Vector3.up * 90, 0.15f).OnComplete(() =>
                {
                    card.modal.isPlayerMinion = true;
                    card.view.UpdateView(card.modal);
                    card.transform.DORotate(Vector3.up * 0, 0.15f);
                });
                card.transform.DOMove(PlayArea.Instance.opponentCardPos.position, 0.5f).OnComplete(() =>
                {
                    card.transform.DOScale(0f, 0.25f).SetDelay(1f).OnComplete(() =>
                    {
                        if (card.modal.upgradedVerdion != null)
                        {
                            opponent.SpawnCardToDeck(card.modal.upgradedVerdion, true);
                        }
                        //Destroy(card.gameObject);
                        opponent.hand.Remove(card);
                        card.transform.SetParent(discardPile);
                        card.gameObject.SetActive(false);
                    });
                });

                yield return new WaitForSeconds(3f);
            }

            //opponent.UpdateHand();
        }

        isPlayingCard = false;
        ClearSelectables();

        if (isPlayerTurn)
        {
            //Debug.LogWarning("setting player minions ready to attack");
            player.UpdateHand();
            SetPlayerMinionsReadyToAttack();

        }
        else
        {
            opponent.UpdateHand();
        }
        Debug.Log("All actions completed.");
    }

    public IEnumerator ExecuteActions(Queue<IEnumerator> actionsList)
    {
        //Debug.Log("executeing card actions");
        while (actionsList.Count > 0)
        {
            //Debug.Log("executeing action");
            IEnumerator action = actionsList.Dequeue();
            yield return StartCoroutine(action);
        }
        //Debug.Log("All actions completed.");
    }

    public void SummonMinion(CardSO card, Vector3 pos)
    {
        MinionController minion = Instantiate(minionprefab, pos, Quaternion.identity).GetComponent<MinionController>();
        minion.card = card;
        //minion.modal = new MinionModal(card, minion);  
        minion.modal.UpdateModal(minion.card);
        minion.view.UpdateView(minion.modal);
        ActionHolder.thisMinion = minion;
        ActionHolder.thisCard = minion.card;

        if (isPlayerTurn)
        {
            player.minions.Add(minion);
            minion.modal.isPlayerMinion = true;
            minion.owner = player;

        }
        else
        {
            opponent.minions.Add(minion);
            minion.modal.isPlayerMinion = false;
            minion.owner = opponent;

        }

    }
    private void ClearSelectables()
    {
        foreach (var selectable in selectables)
        {
            selectable.SetSelectable(false);
        }
    }

}

