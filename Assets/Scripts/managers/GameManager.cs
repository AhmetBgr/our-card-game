using System; 
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DG.Tweening;
using TMPro;
using Unity.VisualScripting;
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

    private bool cancelPlayingCardRequested = false;
    private CardController playingCard = null;
    private Agent playingAgent = null;
    private int playingAgentManaBeforePlay = 0;
    private int playingCardHandLayoutIndex = -1;

    public TextMeshProUGUI[] playerCornerDamageTexts;
    public TextMeshProUGUI[] opponentCornerDamageTexts;

    public List<SelectableEntity> selectables = new List<SelectableEntity>();
    private Queue<IEnumerator> actionQueue = new Queue<IEnumerator>();
    private Queue<IEnumerator> onTurnEndActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onCardDrawActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onMinionDeathActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onMinionCollidedActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onMinionTookDamageActions = new Queue<IEnumerator>();

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
        MinionController.OnDied += OnMinionDied;
        MinionController.OnCollided += OnMinionCollided;
        MinionController.OnTookDamage += OnMinionTookDamage;
    }
    private void OnDestroy()
    {
        MinionController.OnDied -= OnMinionDied;
        MinionController.OnCollided -= OnMinionCollided;
        MinionController.OnTookDamage -= OnMinionTookDamage;

    }



    private void Update()
    {
        if (currentState == GameState.EndGame) return;

        if (isPlayingCard && isPlayerTurn && Input.GetMouseButtonDown(1))
        {
            CancelPlayingCard();
        }

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

        player.DrawCard();
        yield return new WaitForSeconds(0.5f);

        opponent.DrawCard();
        yield return new WaitForSeconds(0.5f);

        player.DrawCard();
        yield return new WaitForSeconds(0.5f);

        opponent.DrawCard();
        yield return new WaitForSeconds(0.5f);

        player.DrawCard();
        yield return new WaitForSeconds(0.5f);

        opponent.DrawCard();
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
        player.DrawCard();
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
                    ActionHolder.thisCardSO = minion.card;
                    ActionHolder.thisCard = null;
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
                    ActionHolder.thisCardSO = minion.card;
                    ActionHolder.thisCard = null;
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
                    if (minion.CanMove(pos).CanMove)
                    {
                        minion.Move(pos);
                        yield return new WaitForSeconds(0.26f);

                        
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

        opponent.DrawCard();

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
                    ActionHolder.thisCardSO = minion.card;
                    ActionHolder.thisCard = null;
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
                    if (minion.CanMove(pos).CanMove)
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
        cancelPlayingCardRequested = false;
        ActionHolder.cancelRequested = false;
        playingCard = card;
        playingAgent = agent;
        playingAgentManaBeforePlay = agent != null ? agent.availibleMana : 0;
        playingCardHandLayoutIndex = player != null && player.cardHandLayout != null
            ? player.cardHandLayout.cards.IndexOf(card.transform)
            : -1;
        //agent.availibleMana -= card.modal.cost;
        isPlayingCard = true;
        actionQueue.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        ActionHolder.selectedAgent = null;
        //ActionHolder.thisMinion = null;
        ActionHolder.thisCardSO = card.card;
        ActionHolder.thisCard = card;
        ActionHolder.selectedMinions.Clear();
        ActionHolder.selectedCells.Clear();
        ActionHolder.curActionsList = actionQueue;
        card.card.OnPlay.Invoke();

        Debug.LogWarning("playing card");

        yield return StartCoroutine(ExecuteActions(card));

        Debug.LogWarning("played card");
    }
    public void PayCardCost(Agent agent, int cost)
    {

        
        
    }
    public IEnumerator _PayCardCost(Agent agent, int cost)
    {
        agent.availibleMana -= cost;

        yield return null;
    }
    public void RefundCardCost(Agent agent, int cost)
    {
        agent.availibleMana += cost;
    }
    // checks if card can be played?
    public void TestCard(CardController card)
    {
        isTestingFailed = false;

        actionQueue.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        ActionHolder.thisCardSO = card.card;
        ActionHolder.thisCard = card;
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
            if (cancelPlayingCardRequested || ActionHolder.cancelRequested)
            {
                actionQueue.Clear();
                break;
            }
            Debug.Log("executeing action");
            IEnumerator action = actionQueue.Dequeue();
            yield return StartCoroutine(action);
        }
        Debug.Log("execution complete");
        isTesting = false;

        if (cancelPlayingCardRequested || ActionHolder.cancelRequested)
        {
            FinishCancelPlayingCard();
            yield break;
        }

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
        playingCard = null;
        playingAgent = null;
        cancelPlayingCardRequested = false;
        ActionHolder.cancelRequested = false;

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

    public void CancelPlayingCard()
    {
        if (!isPlayingCard) return;
        if (!isPlayerTurn) return;
        if (currentState != GameState.PlayerTurn) return;
        if (playingAgent != player) return;

        cancelPlayingCardRequested = true;
        ActionHolder.cancelRequested = true;

        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        ActionHolder.selectedAgent = null;
        ActionHolder.selectedMinions.Clear();
        ActionHolder.selectedCells.Clear();

        player.curState = Player.State.Waiting;
        ClearSelectables();
    }

    private void FinishCancelPlayingCard()
    {
        if (playingAgent != null)
        {
            playingAgent.availibleMana = playingAgentManaBeforePlay;
        }

        if (playingCard != null && isPlayerTurn && player != null && player.cardHandLayout != null)
        {
            var layout = player.cardHandLayout;

            layout.RemoveCard(playingCard.transform);
            int insertIndex = playingCardHandLayoutIndex >= 0 ? playingCardHandLayoutIndex : 0;
            layout.AddCard(playingCard.transform, insertIndex);

            playingCard.transform.localRotation = Quaternion.identity;
            playingCard.transform.localScale = Vector3.one;
            playingCard.draggableItem.ParentAfterDrag = layout.transform;
            playingCard.EnablePeek();
        }

        isPlayingCard = false;
        cancelPlayingCardRequested = false;
        ActionHolder.cancelRequested = false;

        ClearSelectables();

        if (isPlayerTurn)
        {
            player.UpdateHand();
            SetPlayerMinionsReadyToAttack();
        }
        else
        {
            opponent.UpdateHand();
        }

        playingCard = null;
        playingAgent = null;
        playingCardHandLayoutIndex = -1;
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
        ActionHolder.thisCardSO = minion.card;

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
    private IEnumerator InvokeOnMinionDeathActions(MinionController minion)
    {
        var tempselectedcell = ActionHolder.selectedcell;
        var tempselectedMinion = ActionHolder.selectedMinion ;
        var tempthisMinion = ActionHolder.thisMinion ;
        var tempthisCardSO = ActionHolder.thisCardSO ;
        var tempThisCard = ActionHolder.thisCard;
        var tempselectedMinions = new List<MinionController>();
        tempselectedMinions.AddRange(ActionHolder.selectedMinions);
        var tempselectedCells = new List<Transform>();
        tempselectedCells.AddRange(ActionHolder.selectedCells);
        var tempselectedAgent = ActionHolder.selectedAgent;
        Queue<IEnumerator> tempCurActionList = new Queue<IEnumerator>(ActionHolder.curActionsList);

        onMinionDeathActions.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        //ActionHolder.thisMinion = minion;
        ActionHolder.thisCardSO = minion.card;
        ActionHolder.selectedMinions.Clear();
        //ActionHolder.selectedMinions.Add(minion);
        ActionHolder.selectedCells.Clear();
        //ActionHolder.selectedAgent = player;
        ActionHolder.curActionsList = onMinionDeathActions;
        minion.card.OnDeath.Invoke();

        Debug.LogWarning("executing on death actions");

        yield return StartCoroutine(ExecuteActions(onMinionDeathActions));


        ActionHolder.selectedcell = tempselectedcell;
        ActionHolder.selectedMinion = tempselectedMinion;
        ActionHolder.thisMinion = tempthisMinion;
        ActionHolder.thisCardSO = tempthisCardSO;
        ActionHolder.selectedMinions = new List<MinionController>(tempselectedMinions);
        ActionHolder.selectedCells = new List<Transform>(tempselectedCells);
        ActionHolder.selectedAgent = tempselectedAgent;
        ActionHolder.curActionsList = new Queue<IEnumerator>(tempCurActionList);

        Debug.LogWarning("executed on death actions");

    }

    private void OnMinionDied(MinionController minion)
    {
        StartCoroutine(InvokeOnMinionDeathActions(minion));
    }

    private void OnMinionCollided(MinionController minion, MinionController collidedMinion)
    {
        StartCoroutine(_OnMinionCollided(minion, collidedMinion));
    }

    private void OnMinionTookDamage(MinionController minion, int damage)
    {
        StartCoroutine(InvokeOnMinionTookDamageActions(minion, damage));
    }

    private IEnumerator InvokeOnMinionTookDamageActions(MinionController minion, int damage)
    {
        var tempselectedcell = ActionHolder.selectedcell;
        var tempselectedMinion = ActionHolder.selectedMinion;
        var tempthisMinion = ActionHolder.thisMinion;
        var tempthisCardSO = ActionHolder.thisCardSO;
        var tempThisCard = ActionHolder.thisCard;
        var tempTargetMinion = ActionHolder.selectedTargetMinion;
        var tempselectedMinions = new List<MinionController>();
        tempselectedMinions.AddRange(ActionHolder.selectedMinions);
        var tempselectedCells = new List<Transform>();
        tempselectedCells.AddRange(ActionHolder.selectedCells);
        var tempselectedAgent = ActionHolder.selectedAgent;
        Queue<IEnumerator> tempCurActionList = new Queue<IEnumerator>(ActionHolder.curActionsList);

        onMinionTookDamageActions.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        ActionHolder.selectedTargetMinion = null;
        ActionHolder.thisMinion = minion;
        ActionHolder.thisCardSO = minion.card;
        ActionHolder.thisCard = null;
        ActionHolder.selectedMinions.Clear();
        ActionHolder.selectedMinions.Add(minion);
        ActionHolder.selectedCells.Clear();
        ActionHolder.selectedAgent = minion.owner;
        ActionHolder.curActionsList = onMinionTookDamageActions;

        minion.card.OnTookDamage.Invoke();

        Debug.LogWarning($"executing on took damage actions (damage: {damage})");

        yield return StartCoroutine(ExecuteActions(onMinionTookDamageActions));

        ActionHolder.selectedcell = tempselectedcell;
        ActionHolder.selectedMinion = tempselectedMinion;
        ActionHolder.selectedTargetMinion = tempTargetMinion;
        ActionHolder.thisMinion = tempthisMinion;
        ActionHolder.thisCardSO = tempthisCardSO;
        ActionHolder.thisCard = tempThisCard;
        ActionHolder.selectedMinions = new List<MinionController>(tempselectedMinions);
        ActionHolder.selectedCells = new List<Transform>(tempselectedCells);
        ActionHolder.selectedAgent = tempselectedAgent;
        ActionHolder.curActionsList = new Queue<IEnumerator>(tempCurActionList);

        Debug.LogWarning("executed on took damage actions");
    }

    private IEnumerator _OnMinionCollided(MinionController minion, MinionController collidedMinion)
    {
        var tempselectedcell = ActionHolder.selectedcell;
        var tempselectedMinion = ActionHolder.selectedMinion;
        var tempthisMinion = ActionHolder.thisMinion;
        var tempthisCardSO = ActionHolder.thisCardSO;
        var tempThisCard = ActionHolder.thisCard;
        var tempTargetMinion = ActionHolder.selectedTargetMinion;
        var tempselectedMinions = new List<MinionController>();
        tempselectedMinions.AddRange(ActionHolder.selectedMinions);
        var tempselectedCells = new List<Transform>();
        tempselectedCells.AddRange(ActionHolder.selectedCells);
        var tempselectedAgent = ActionHolder.selectedAgent;
        Queue<IEnumerator> tempCurActionList = new Queue<IEnumerator>(ActionHolder.curActionsList);

        onMinionCollidedActions.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;
        ActionHolder.selectedTargetMinion = collidedMinion;
        //ActionHolder.thisMinion = minion;
        ActionHolder.thisCardSO = minion.card;
        ActionHolder.selectedMinions.Clear();
        //ActionHolder.selectedMinions.Add(minion);
        ActionHolder.selectedCells.Clear();
        //ActionHolder.selectedAgent = player;
        ActionHolder.curActionsList = onMinionCollidedActions;
        minion.card.OnMinionCollided.Invoke();

        Debug.LogWarning("executing minion collision actions");

        yield return StartCoroutine(ExecuteActions(onMinionCollidedActions));


        ActionHolder.selectedcell = tempselectedcell;
        ActionHolder.selectedMinion = tempselectedMinion;
        ActionHolder.selectedTargetMinion = tempTargetMinion;
        ActionHolder.thisMinion = tempthisMinion;
        ActionHolder.thisCardSO = tempthisCardSO;
        ActionHolder.thisCard = tempThisCard;
        ActionHolder.selectedMinions = new List<MinionController>(tempselectedMinions);
        ActionHolder.selectedCells = new List<Transform>(tempselectedCells);
        ActionHolder.selectedAgent = tempselectedAgent;
        ActionHolder.curActionsList = new Queue<IEnumerator>(tempCurActionList);

        Debug.LogWarning("executed minion collision actions");

    }
}

