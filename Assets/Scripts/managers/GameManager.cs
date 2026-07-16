using System; 
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public enum GameState { Setup, StartGame, PlayerTurn, OpponentTurn, EndGame }

public class GameManager : Singleton<GameManager>
{
    public Player player;
    public Agent opponent;
    public SwitchController switchController;   
    public GameObject minionprefab;
    public GameObject rangedMinionprefab;
    public Transform discardPile;
    public bool isPlayingCard = false;
    public IEnumerator curaction;

    private bool cancelPlayingCardRequested = false;
    private CardController playingCard = null;

    /// <summary>
    /// The card currently being played, or null when no card play is in progress. Read-only; the general
    /// targeting arrow reads this so it can originate from the card while the player picks a cell/minion.
    /// </summary>
    public CardController PlayingCard => playingCard;
    private Agent playingAgent = null;
    private int playingAgentManaBeforePlay = 0;
    private int playingCardHandLayoutIndex = -1;

    public TextMeshProUGUI[] playerCornerDamageTexts;
    public TextMeshProUGUI[] opponentCornerDamageTexts;

    public List<SelectableEntity> selectables = new List<SelectableEntity>();
    private Queue<IEnumerator> actionQueue = new Queue<IEnumerator>();
    private Queue<IEnumerator> onTurnStartActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onTurnEndActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onCardDrawActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onMinionDeathActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onMinionCollidedActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onMinionTookDamageActions = new Queue<IEnumerator>();
    private Queue<IEnumerator> onHeroAttackedActions = new Queue<IEnumerator>();
    private readonly List<HeroPassiveSO> _heroPassiveMatchBuffer = new List<HeroPassiveSO>();

    // Triggered-action execution uses shared ActionHolder globals. To prevent different triggers
    // (death, took damage, collision, etc.) from stomping each other's state mid-execution,
    // we serialize all triggered-action coroutines through this scheduler.
    private bool _executingTriggeredActions = false;
    private readonly Queue<Action> _pendingTriggeredCallbacks = new Queue<Action>();

    // True while any triggered action is running or still queued to run. Callers that mutate the shared
    // ActionHolder selection globals (the AI turn loop, card plays, turn-end processing) must wait for
    // this to clear before proceeding, or they will clobber an in-flight trigger's state mid-execution
    // (e.g. a minion that kills itself attacking, whose OnDeath resolves a frame later).
    public bool HasInFlightTriggeredActions => _executingTriggeredActions || _pendingTriggeredCallbacks.Count > 0;

    [SerializeField] private Queue<IEnumerator> defaultActionQueue;

    public GameState currentState;
    public int maxMana = 0;
    public bool isPlayerTurn;
    public bool isTesting = false;
    public bool isTestingFailed = false;
    public static event Action<GameState> OnTurnEnd;
    public static event Action<GameState> OnTurnStarted;
    public static event Action<MinionController> OnMinionSummoned;
    // Fired once when a card play successfully commits (past the cancel checks). Purely additive:
    // consumed by the stats system; no core logic depends on it.
    public static event Action<Agent, CardSO> OnCardPlayed;

    private readonly HeroPassiveSystem heroPassives = new HeroPassiveSystem();

    void Start()
    {
        StartCoroutine(GameLoop());
        MinionController.OnDied += OnMinionDied;
        MinionController.OnCollided += OnMinionCollided;
        MinionController.OnTookDamage += OnMinionTookDamage;
        OnMinionSummoned += OnMinionSummonedForLog;
    }
    private void OnDestroy()
    {
        MinionController.OnDied -= OnMinionDied;
        MinionController.OnCollided -= OnMinionCollided;
        MinionController.OnTookDamage -= OnMinionTookDamage;
        OnMinionSummoned -= OnMinionSummonedForLog;
        heroPassives.Clear();

    }



    private void Update()
    {
        if (currentState == GameState.EndGame) return;

        if (isPlayerTurn && Input.GetMouseButtonDown(1))
        {
            // Right-click backs out: cancel the card being played, otherwise cancel an active
            // attack/minion selection.
            if (isPlayingCard) CancelPlayingCard();
            else SelectionManager.Instance.Cancel();
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
        // Registered here rather than in Start(): SetupGame is a coroutine driven from GameLoop, so
        // every HeroController.Start() has already run and hero.card/owner are populated. Start()
        // ordering between GameManager and HeroController is undefined.
        heroPassives.Register(player != null ? player.hero : null);
        heroPassives.Register(opponent != null ? opponent.hero : null);

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
        yield return StartCoroutine(DrawTurnStartCards(player));
        // Queue turn-start through the same serializer as the draw above so it runs AFTER the on-draw
        // pass completes, instead of racing it and clobbering shared ActionHolder state.
        EnqueueTriggeredAction(() => StartCoroutine(InvokeOnTurnStarted()));
        // Let the on-draw + turn-start passes fully resolve before play begins, so card plays don't
        // run concurrently with (and clobber) an in-flight trigger's shared ActionHolder selection state.
        yield return new WaitUntil(() => !HasInFlightTriggeredActions);
        yield return StartCoroutine(player.PlayTurn());

        yield return new WaitForSeconds(0.5f);
    }
    public IEnumerator InvokeOnTurnEnd()
    {
        // A turn boundary aborts any in-progress selection so it can't leak into the next turn.
        SelectionManager.Instance.Cancel();
        ActionLogPanel.Instance?.AddTurnSpacer();
        OnTurnEnd?.Invoke(currentState);

        yield break;
    }
    /// <summary>
    /// The agent's turn-start draw, plus any extra cards a delayed effect owes it (Do Nothing). The
    /// debt is consumed up front so it is paid exactly once even if a draw fizzles on the hand cap.
    /// Runs before InvokeOnTurnStarted is queued, so the extra card is in hand before any OnTurnStart
    /// trigger reads it, and each draw's on-draw pass (turrets) still serializes behind the last.
    /// </summary>
    private IEnumerator DrawTurnStartCards(Agent agent)
    {
        agent.DrawCard();

        int extra = agent.ConsumePendingExtraDraws();
        for (int i = 0; i < extra; i++)
        {
            yield return new WaitForSeconds(0.35f);
            agent.DrawCard();
        }
    }

    public IEnumerator InvokeOnTurnStarted()
    {
        OnTurnStarted?.Invoke(currentState);

        Agent currentAgent = currentState == GameState.PlayerTurn ? (Agent)player : opponent;

        bool iterateForward = currentState == GameState.PlayerTurn;

        int xStart = iterateForward ? 0 : GridManager.Instance.GridWidth - 1;
        int xEnd = iterateForward ? GridManager.Instance.GridWidth : -1;
        int xStep = iterateForward ? 1 : -1;

        int yStart = iterateForward ? 0 : GridManager.Instance.GridHeight - 1;
        int yEnd = iterateForward ? GridManager.Instance.GridHeight : -1;
        int yStep = iterateForward ? 1 : -1;

        // Hold the triggered-action lock for the WHOLE pass (not per grid cell). Triggered passes share
        // ActionHolder's static selection state (thisMinion / selectedTargetMinions / ...); releasing the
        // lock between minions would let a queued sibling pass start and clobber an in-flight one mid-way.
        _executingTriggeredActions = true;
        try
        {
            for (int x = xStart; x != xEnd; x += xStep)
            {
                for (int y = yStart; y != yEnd; y += yStep)
                {
                    Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                    MinionController minion;

                    // Skip the hero here: it is handled by the dedicated hero block below. The hero's
                    // GridEntity is type Obj so it occupies a grid cell, and without this guard its
                    // OnTurnStart would fire twice (once here, once in the dedicated block).
                    if (cell.obj != null && cell.obj.TryGetComponent(out minion) && minion.owner == currentAgent && minion != currentAgent.hero)
                    {
                        using (ActionHolder.PushScope())
                        {
                            onTurnStartActions.Clear();
                            ActionHolder.ResetSelections();
                            ActionHolder.thisMinion = minion;
                            ActionHolder.thisCardSO = minion.card;
                            ActionHolder.thisCard = null;
                            ActionHolder.selectedMinions.Add(minion);
                            ActionHolder.selectedAgent = currentAgent;
                            ActionHolder.curActionsList = onTurnStartActions;

                            this.isTesting = false;
                            minion.modal.OnTurnStart.Invoke();

                            yield return StartCoroutine(ExecuteActions(onTurnStartActions));
                        }
                    }
                }
            }

            // Hero turn start
            MinionController hero = currentAgent.hero;
            if (hero != null)
            {
                using (ActionHolder.PushScope())
                {
                    onTurnStartActions.Clear();
                    ActionHolder.ResetSelections();
                    ActionHolder.thisMinion = hero;
                    ActionHolder.thisCardSO = hero.card;
                    ActionHolder.thisCard = null;
                    ActionHolder.selectedMinions.Add(hero);
                    ActionHolder.selectedAgent = currentAgent;
                    ActionHolder.curActionsList = onTurnStartActions;

                    this.isTesting = false;
                    hero.modal.OnTurnStart.Invoke();

                    yield return StartCoroutine(ExecuteActions(onTurnStartActions));
                }
            }
        }
        finally
        {
            FinishTriggeredAction();
        }
    }

    public void TriggerCardDrawActions(Agent drawingAgent)
    {
        EnqueueTriggeredAction(() => StartCoroutine(InvokeOnCardDrawActions(drawingAgent)));
    }

    private IEnumerator InvokeOnCardDrawActions(Agent drawingAgent)
    {
        // Lock acquired up front (before any yield) so a turn-start/turn-end pass enqueued right after
        // the draw waits for this whole pass instead of clobbering a turret's in-flight select->attack.
        _executingTriggeredActions = true;
        try
        {
            for (int x = 0; x < GridManager.Instance.GridWidth; x++)
            {
                for (int y = 0; y < GridManager.Instance.GridHeight; y++)
                {
                    Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                    MinionController minion;

                    // "Whenever you draw a card": a turret triggers when its own owner drew, regardless of
                    // whose turn it is (covers off-turn / card-effect draws, not just the start-of-turn draw).
                    if (cell.obj != null && cell.obj.TryGetComponent(out minion) && minion.owner == drawingAgent)
                    {
                        using (ActionHolder.PushScope())
                        {
                            onCardDrawActions.Clear();
                            ActionHolder.ResetSelections();
                            ActionHolder.thisMinion = minion;
                            ActionHolder.thisCardSO = minion.card;
                            ActionHolder.thisCard = null;
                            ActionHolder.selectedMinions.Add(minion);
                            ActionHolder.selectedAgent = minion.owner;
                            ActionHolder.curActionsList = onCardDrawActions;

                            this.isTesting = false;
                            minion.modal.OnOwnerDrawedCard.Invoke();

                            yield return StartCoroutine(ExecuteActions(onCardDrawActions));
                        }
                    }
                }
            }
        }
        finally
        {
            FinishTriggeredAction();
        }

        SetPlayerMinionsReadyToAttack();
    }

    public IEnumerator EndPlayerTurn()
    {
        if (currentState != GameState.PlayerTurn) yield break;

        switchController.PlaySwitchAnim(false);

        // Lock held across the whole OnOwnerTurnEnd loop so the per-minion passes can't interleave with
        // each other or a queued sibling pass (see InvokeOnCardDrawActions for the rationale).
        _executingTriggeredActions = true;
        try
        {
            for (int x = 0; x < GridManager.Instance.GridWidth; x++)
            {
                for (int y = 0; y < GridManager.Instance.GridHeight; y++)
                {
                    Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                    MinionController minion;

                    if (cell.obj != null && cell.obj.TryGetComponent(out minion) && minion.owner == player)
                    {
                        using (ActionHolder.PushScope())
                        {
                            onTurnEndActions.Clear();
                            ActionHolder.ResetSelections();
                            ActionHolder.thisMinion = minion;
                            ActionHolder.thisCardSO = minion.card;
                            ActionHolder.thisCard = null;
                            ActionHolder.selectedMinions.Add(minion);
                            ActionHolder.selectedAgent = player;
                            ActionHolder.curActionsList = onTurnEndActions;

                            this.isTesting = false;
                            minion.modal.OnOwnerTurnEnd.Invoke();

                            yield return StartCoroutine(ExecuteActions(onTurnEndActions));
                        }
                    }
                }
            }
        }
        finally
        {
            FinishTriggeredAction();
        }

        isPlayerTurn = false;


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
                    var moveInfo = minion.CanMove(pos);
                    if (moveInfo.CanMove)
                    {
                        minion.Move(pos);
                        yield return new WaitForSeconds(0.26f);


                    }
                    else if (moveInfo.Blocked)
                    {
                        // Wanted to advance but the cell ahead stayed occupied (minions are resolved
                        // front-first, so anything still in the way here is a genuine block) — bump.
                        minion.FailedMove(Vector3.up, moveInfo.CollidedEntity);
                        yield return new WaitForSeconds(0.26f);
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

        yield return StartCoroutine(DrawTurnStartCards(opponent));

        // Queue turn-start behind the on-draw pass (same serializer) instead of racing it.
        EnqueueTriggeredAction(() => StartCoroutine(InvokeOnTurnStarted()));

        // Critical: the AI's PlayTurn evaluates moves by running ExecuteActions/ResetSelections on the
        // shared ActionHolder globals. Wait for the on-draw + turn-start passes to drain first, or the
        // AI eval clobbers an opponent turret's in-flight select->attack (its target list gets reset).
        yield return new WaitUntil(() => !HasInFlightTriggeredActions);

        yield return StartCoroutine(opponent.PlayTurn());


        // Lock held across the whole OnOwnerTurnEnd loop (see InvokeOnCardDrawActions for rationale).
        _executingTriggeredActions = true;
        try
        {
            for (int x = GridManager.Instance.GridWidth-1; x >= 0; x--)
            {
                for (int y = GridManager.Instance.GridHeight-1; y >= 0 ; y--)
                {
                    Cell cell = GridManager.Instance.GetCell(new Vector2Int(x, y));

                    MinionController minion;

                    if (cell.obj != null && cell.obj.TryGetComponent(out minion) && minion.owner == opponent)
                    {
                        using (ActionHolder.PushScope())
                        {
                            onTurnEndActions.Clear();
                            ActionHolder.ResetSelections();
                            ActionHolder.thisMinion = minion;
                            ActionHolder.thisCardSO = minion.card;
                            ActionHolder.thisCard = null;
                            ActionHolder.selectedMinions.Add(minion);
                            ActionHolder.selectedAgent = opponent;
                            ActionHolder.curActionsList = onTurnEndActions;

                            this.isTesting = false;
                            minion.modal.OnOwnerTurnEnd.Invoke();

                            yield return StartCoroutine(ExecuteActions(onTurnEndActions));
                        }
                    }
                }
            }
        }
        finally
        {
            FinishTriggeredAction();
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
                    var moveInfo = minion.CanMove(pos);
                    if (moveInfo.CanMove)
                    {
                        minion.Move(pos);
                        yield return new WaitForSeconds(0.26f);

                        GridManager.Instance.InvokeGridChanged();
                    }
                    else if (moveInfo.Blocked)
                    {
                        // Wanted to advance but the cell ahead stayed occupied (minions are resolved
                        // front-first, so anything still in the way here is a genuine block) — bump.
                        minion.FailedMove(Vector3.down, moveInfo.CollidedEntity);
                        yield return new WaitForSeconds(0.26f);
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

    // Turn-switch hover preview: light up each player minion's move arrow to show what will happen when
    // the turn ends (white = will advance, yellow = wants to advance but will collide).
    public void ShowMovePreview()
    {
        foreach (var minion in player.minions)
            if (minion != null) minion.ShowMoveArrow();
    }

    public void HideMovePreview()
    {
        foreach (var minion in player.minions)
            if (minion != null) minion.HideMoveArrow();
    }

    public void SetPlayerMinionsReadyToAttack()
    {

        if (!isPlayerTurn) return;

        Debug.Log("should set palyer minions ready to attack");

        player.curState = Player.State.Waiting;
        // Set player minions Ready to attack. SetReadyToAttack owns the visual + clickable state
        // (attack highlight for attack-ready minions, no normal highlight), so we don't re-toggle
        // SetSelectable here — doing so would light the normal highlight on attack-ready minions.
        foreach (var item in player.minions)
        {
            item.SetReadyToAttack();
        }

        if (player.hero != null)
        {
            player.hero.SetReadyToAttack();
        }
    }

    public void CheckWinCondition()
    {
        if (player.hero.modal.health <= 0)
        {
            Debug.Log("Player Loses!");
            currentState = GameState.EndGame;
            PopupManager.Instance.OpenGameOverPopup(false, 1f);
        }
        else if (opponent.hero.modal.health <= 0)
        {
            Debug.Log("Player Wins!");
            currentState = GameState.EndGame;
            PopupManager.Instance.OpenGameOverPopup(true, 1f);

        }
    }

    // Editor-only debug triggers (see GameManagerEditor) to preview the end-game panels without
    // having to actually win/lose a match.
    public void TriggerVictory()
    {
        currentState = GameState.EndGame;
        PopupManager.Instance.OpenGameOverPopup(true);
    }

    public void TriggerDefeat()
    {
        currentState = GameState.EndGame;
        PopupManager.Instance.OpenGameOverPopup(false);
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

        bool isPlayTurn = currentState == GameState.PlayerTurn || currentState == GameState.OpponentTurn;
        if ((!isPlayTurn || isPlayingCard || card.modal.cost > agent.availibleMana) && !isTesting) yield break;

        // Playing a card preempts any in-progress attack/minion selection (e.g. mid-attack), tearing it
        // down so the played card's own selection steps start from a clean slate.
        SelectionManager.Instance.Cancel();

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
        ActionHolder.ResetSelections();
        //ActionHolder.thisMinion = null;
        ActionHolder.thisCardSO = card.card;
        ActionHolder.thisCard = card;
        ActionHolder.curActionsList = actionQueue;
        card.modal.OnPlay.Invoke();

        Debug.LogWarning("playing card: " + card.card.cardName);

        // Spell (non-minion) cards log their "played" entry here, before their effects resolve, so it
        // sits above the deaths/summons the spell triggers. It stays pending until the play commits so
        // a cancelled play leaves no orphaned entry. Minion cards instead log when they're summoned.
        if (card.modal.health <= 0)
        {
            ActionLogPanel.Instance?.SetPending(ActionLogMessageFactory.CardPlayed(playingAgent, card.card));
        }

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
    // checks if card can be played? Runs the card's OnPlay queue with isTesting=true and reports
    // failures via isTestingFailed. Must be a coroutine so actions run sequentially — otherwise
    // selection-step coroutines yield-return null and the caller sees an empty queue before they
    // ever set isTestingFailed.
    public IEnumerator TestCard(CardController card)
    {
        isTestingFailed = false;

        actionQueue.Clear();
        ActionHolder.ResetSelections();
        ActionHolder.thisCardSO = card.card;
        ActionHolder.thisCard = card;
        //ActionHolder.thisMinion = null;
        ActionHolder.curActionsList = actionQueue;

        bool prevIsTesting = isTesting;
        isTesting = true;
        try
        {
            card.modal.OnPlay.Invoke();

            while (actionQueue.Count > 0)
            {
                IEnumerator action = actionQueue.Dequeue();
                yield return StartCoroutine(action);
            }
        }
        finally
        {
            isTesting = prevIsTesting;
        }
    }

    public IEnumerator ExecuteActions(CardController card)
    {
        isTesting = false;
        _executingTriggeredActions = true;

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

        // Show the spell's pending "played" entry if none of its effects already did (e.g. a spell with
        // no minion deaths/summons). No-op for minion cards, which never set a pending entry.
        ActionLogPanel.Instance?.FlushPending();

        // Confirmed-commit point: this is past the cancel checks above, so cancelled/failed plays never
        // reach here. Broadcast for the stats system (cards played / mana spent / upgraded count).
        OnCardPlayed?.Invoke(playingAgent, card != null ? card.card : null);

        if (isPlayerTurn)
        {
            //Debug.Log("execution complete3");

            //player.handManager.RemoveFromHand(card);
            if (card != null) {
                player.RemoveCardFromHand(card);
                card.transform.DOScale(0f, 0.25f).OnComplete(() =>
                {
                    if (card.modal.upgradedVerdion != null)
                    {
                        player.SpawnCardToDeck(card.modal.upgradedVerdion, true);
                    }
                    card.transform.SetParent(discardPile);
                    card.gameObject.SetActive(false);
                });
            }
        }
        else
        {
            //Debug.Log("execution complete4");

            //opponent.handManager.RemoveFromHand(card);
            if (card != null) {
                opponent.RemoveCardFromHand(card);

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
        FinishTriggeredAction();
        
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

        SelectionManager.Instance.Cancel();
        player.curState = Player.State.Waiting;
        ClearSelectables();
    }

    private void FinishCancelPlayingCard()
    {
        // The cancelled card never resolved, so drop its unshown "played" entry.
        ActionLogPanel.Instance?.DiscardPending();

        if (playingAgent != null)
        {
            playingAgent.availibleMana = playingAgentManaBeforePlay;
        }

        if (playingCard != null && isPlayerTurn && player != null && player.cardHandLayout != null)
        {
            var layout = player.cardHandLayout;

            layout.RemoveCard(playingCard.transform);
            int insertIndex = playingCardHandLayoutIndex >= 0 ? playingCardHandLayoutIndex : 0;
            layout.InsertCardAt(playingCard.transform, insertIndex);

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
        // Turn-relative summon: the new minion belongs to whoever's turn it is.
        SummonMinion(card, pos, isPlayerTurn ? player : opponent);
    }

    /// <summary>
    /// Owner-explicit summon. Unlike the turn-relative overload above, the summoned minion belongs to
    /// `owner` regardless of whose turn it is — needed by hero passives that summon on the *defender's*
    /// side during the attacker's turn (summon-on-attacked). Push direction and side assignment are all
    /// resolved from `owner`, not from isPlayerTurn.
    /// </summary>
    public void SummonMinion(CardSO card, Vector3 pos, Agent owner)
    {
        bool ownerIsPlayer = owner == player;

        // If the target cell is already occupied, push the occupant forward one cell first so the new
        // minion spawns in the vacated cell. Callers only pass cells whose occupant is pushable.
        Vector2Int destIndex = GridManager.Instance.PosToGridIndex(pos);
        GameObject occupantObj = GridManager.Instance.GetCell(destIndex).obj;
        if (occupantObj != null && occupantObj.TryGetComponent(out MinionController occupant))
        {
            Vector3Int pushDir = ownerIsPlayer ? Vector3Int.up : Vector3Int.down;
            if (occupant.CanBePushedForward(pushDir))
            {
                occupant.PushForward(pushDir);
            }
            else
            {
                return;
            }
        }

        GameObject prefabToSpawn = (card.range >= 2 && rangedMinionprefab != null) ? rangedMinionprefab : minionprefab;
        MinionController minion = Instantiate(prefabToSpawn, pos, Quaternion.identity).GetComponent<MinionController>();
        minion.card = card;
        //minion.modal = new MinionModal(card, minion);
        minion.modal.UpdateModal(minion.card, owner, ownerIsPlayer);

        // Everything but events comes from the played hand card's modal (which may include in-hand
        // buffs), not the CardSO. The card's modal was itself populated from the CardSO when the card
        // was drawn. Falls back to the CardSO data (already set by UpdateModal) for summons with no
        // hand card (e.g. tokens / random / passive summons, where thisCard is null).
        CardController sourceCard = ActionHolder.thisCard;
        if (sourceCard != null && sourceCard.modal != null && sourceCard.card == card)
            minion.modal.CopyFrom(sourceCard.modal);

        minion.view.UpdateView(minion.modal);
        minion.view.PlayAppearAnimation();
        ActionHolder.thisMinion = minion;
        ActionHolder.thisCardSO = minion.card;

        owner.minions.Add(minion);
        minion.modal.isPlayerMinion = ownerIsPlayer;
        minion.owner = owner;

        // Let hero aura passives stamp per-minion stats (e.g. collision damage on friendly minions).
        // Pure side effect — safe here mid-summon; must not enqueue triggered actions.
        heroPassives.ApplyAurasOnSummon(minion);

        OnMinionSummoned?.Invoke(minion);
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
        // PushScope() snapshots selection state + isTesting; Dispose restores both.
        // FinishTriggeredAction runs after Dispose (finally OUTSIDE the using, not inside it) so the outer
        // trigger queue resumes against restored state. Order matters: FinishTriggeredAction drains the
        // next pending trigger and starts it SYNCHRONOUSLY up to its first yield. If it ran before this
        // scope's Restore(), that Restore would fire while the drained trigger is suspended mid-selection
        // and wipe the selection state it just set up — e.g. a deathrattle that damages a hero re-entrantly
        // drains that hero's took-damage passive here, and this Restore would clobber the passive's
        // selectedCells, making it silently select nothing.
        try
        {
            using (ActionHolder.PushScope())
            {
                _executingTriggeredActions = true;

                onMinionDeathActions.Clear();
                ActionHolder.ResetSelections();
                ActionHolder.thisMinion = minion;
                ActionHolder.thisCardSO = minion.card;
                ActionHolder.thisCard = null;
                ActionHolder.selectedAgent = minion.owner;
                ActionHolder.curActionsList = onMinionDeathActions;

                this.isTesting = false;
                minion.modal.OnDeath.Invoke();

                yield return StartCoroutine(ExecuteActions(onMinionDeathActions));
            }
        }
        finally
        {
            FinishTriggeredAction();
        }
    }

    private void OnMinionDied(MinionController minion)
    {
        ActionLogPanel.Instance?.AddEntry(ActionLogMessageFactory.MinionDied(minion));
        EnqueueTriggeredAction(() => StartCoroutine(InvokeOnMinionDeathActions(minion)));
    }

    private void OnMinionSummonedForLog(MinionController minion)
    {
        ActionLogPanel.Instance?.AddEntry(ActionLogMessageFactory.MinionSummoned(minion));
    }

    private void OnMinionCollided(MinionController minion, MinionController collidedMinion)
    {
        EnqueueTriggeredAction(() => StartCoroutine(InvokeOnMinionCollidedActions(minion, collidedMinion)));
    }

    private void OnMinionTookDamage(MinionController minion, int damage)
    {
        bool isHero = minion != null && minion.owner != null && minion == minion.owner.hero;
        Debug.Log($"[HUNTER] OnMinionTookDamage: minion='{minion?.card?.cardName}' dmg={damage} isHero={isHero} _executingTriggeredActions={_executingTriggeredActions} pending={_pendingTriggeredCallbacks.Count}");

        EnqueueTriggeredAction(() => StartCoroutine(InvokeOnMinionTookDamageActions(minion, damage)));
    }

    private void EnqueueTriggeredAction(Action startCoroutine)
    {
        if (_executingTriggeredActions)
        {
            Debug.Log("added top pending actions");
            _pendingTriggeredCallbacks.Enqueue(startCoroutine);
            return;
        }

        startCoroutine.Invoke();
    }

    private void FinishTriggeredAction()
    {
        _executingTriggeredActions = false;
        if (_pendingTriggeredCallbacks.Count > 0)
        {
            _pendingTriggeredCallbacks.Dequeue().Invoke();
        }
    }
    private IEnumerator InvokeOnMinionTookDamageActions(MinionController minion, int damage)
    {
        // FinishTriggeredAction is OUTSIDE the using (see InvokeOnMinionDeathActions) so this scope's
        // Restore() can't clobber the selection state of whatever pending trigger it drains next.
        try
        {
            using (ActionHolder.PushScope())
            {
                _executingTriggeredActions = true;

                onMinionTookDamageActions.Clear();
                ActionHolder.ResetSelections();
                ActionHolder.thisMinion = minion;
                ActionHolder.thisCardSO = minion.card;
                ActionHolder.thisCard = null;
                ActionHolder.selectedAgent = minion.owner;
                ActionHolder.curActionsList = onMinionTookDamageActions;

                this.isTesting = false;
                minion.modal.OnTookDamage.Invoke();

                // Hero "took damage" passives ride the same queue and scope as the self-trigger above,
                // so they can't race it. Registers are already set to the damaged minion (the hero).
                DispatchHeroTookDamagePassives(minion, damage);

                yield return StartCoroutine(ExecuteActions(onMinionTookDamageActions));
            }
        }
        finally
        {
            FinishTriggeredAction();
        }
    }

    /// <summary>
    /// Enqueues the verbs of every hero passive that fires for a "hero took damage" self-trigger onto
    /// the queue the caller is already draining.
    ///
    /// This is called INLINE from InvokeOnMinionTookDamageActions, inside the triggered-action scope
    /// GameManager already opened, with thisMinion/selectedAgent already set to the damaged hero. It
    /// deliberately does NOT open its own scope or start its own triggered action: doing that races the
    /// enclosing scope and gets its registers clobbered by the outer Restore (see HeroPassiveSystem for
    /// the full explanation). The passive verbs simply ride the caller's single ExecuteActions pass.
    /// </summary>
    private void DispatchHeroTookDamagePassives(MinionController hero, int damage)
    {
        HeroRuntime runtime = heroPassives.GetRuntime(hero);
        if (runtime == null)
        {
            Debug.Log($"[HUNTER] DispatchHeroTookDamage: hero='{hero?.card?.cardName}' dmg={damage} -> NO RUNTIME (not a registered passive hero), passive will NOT fire");
            return;
        }

        var ctx = new HeroPassiveContext(hero, subject: hero, amount: damage,
            ownerTurnNumber: runtime.ownerTurnNumber);

        _heroPassiveMatchBuffer.Clear();
        heroPassives.CollectMatching(runtime, HeroPassiveTrigger.HeroTookDamage, ctx, _heroPassiveMatchBuffer);
        Debug.Log($"[HUNTER] DispatchHeroTookDamage: hero='{hero?.card?.cardName}' dmg={damage} matched {_heroPassiveMatchBuffer.Count} HeroTookDamage passive(s), curActionsList==onTookDamage:{ReferenceEquals(ActionHolder.curActionsList, onMinionTookDamageActions)}");

        // Registers are already set by the caller (thisMinion = hero, selectedAgent = hero.owner), which
        // is exactly what a self-trigger needs; the owner-relative selectors then resolve the correct
        // board half even though the hero was damaged on the opponent's turn.
        foreach (var passive in _heroPassiveMatchBuffer)
            passive.Run(ctx);
    }

    /// <summary>
    /// A minion has struck the given hero. This is an "attack" — distinct from took-damage, which also
    /// fires for spells — so it drives the HeroAttacked passive trigger only. Routed through the same
    /// triggered-action scheduler as took-damage/collision so its ActionHolder scope can't race the
    /// strike's own took-damage trigger. Heroes with no passives are a no-op (behave exactly as before).
    /// </summary>
    public void OnHeroAttacked(MinionController hero, MinionController attacker)
    {
        if (hero == null) return;
        if (heroPassives.GetRuntime(hero) == null) return; // not a passive hero — nothing to dispatch

        EnqueueTriggeredAction(() => StartCoroutine(InvokeOnHeroAttackedActions(hero, attacker)));
    }

    private IEnumerator InvokeOnHeroAttackedActions(MinionController hero, MinionController attacker)
    {
        // FinishTriggeredAction is OUTSIDE the using (see InvokeOnMinionTookDamageActions) so this scope's
        // Restore() can't clobber the selection state of whatever pending trigger it drains next.
        try
        {
            using (ActionHolder.PushScope())
            {
                _executingTriggeredActions = true;

                onHeroAttackedActions.Clear();
                ActionHolder.ResetSelections();
                ActionHolder.thisMinion = hero;
                ActionHolder.thisCardSO = hero.card;
                ActionHolder.thisCard = null;
                ActionHolder.selectedAgent = hero.owner;
                ActionHolder.curActionsList = onHeroAttackedActions;

                this.isTesting = false;

                // Registers are set to the hero (thisMinion = hero, selectedAgent = hero.owner) so the
                // owner-relative summon verb resolves the defender's board half even though the attack
                // happened on the attacker's turn.
                DispatchHeroAttackedPassives(hero, attacker);

                yield return StartCoroutine(ExecuteActions(onHeroAttackedActions));
            }
        }
        finally
        {
            FinishTriggeredAction();
        }
    }

    /// <summary>Enqueues the verbs of every HeroAttacked passive on this hero onto the drain the caller owns.</summary>
    private void DispatchHeroAttackedPassives(MinionController hero, MinionController attacker)
    {
        HeroRuntime runtime = heroPassives.GetRuntime(hero);
        if (runtime == null) return;

        var ctx = new HeroPassiveContext(hero, subject: attacker,
            ownerTurnNumber: runtime.ownerTurnNumber);

        _heroPassiveMatchBuffer.Clear();
        heroPassives.CollectMatching(runtime, HeroPassiveTrigger.HeroAttacked, ctx, _heroPassiveMatchBuffer);

        foreach (var passive in _heroPassiveMatchBuffer)
            passive.Run(ctx);
    }

    /// <summary>
    /// True if `hero` is a registered passive hero carrying a passive that cancels the counter-attack an
    /// attacker would otherwise take. Non-heroes and heroes without such a passive return false, so the
    /// normal retaliation path in MinionController.Attack is untouched for everything else.
    /// </summary>
    public bool HeroSuppressesCounterAttack(MinionController hero)
    {
        HeroRuntime runtime = heroPassives.GetRuntime(hero);
        if (runtime == null || runtime.heroSO == null) return false;

        var passives = runtime.heroSO.passives;
        for (int i = 0; i < passives.Count; i++)
            if (passives[i] != null && passives[i].SuppressesCounterAttack) return true;

        return false;
    }

    /// <summary>
    /// Two minions collided (mover ran into target). Only the minion that MOVED deals damage: if the two
    /// are on opposite sides, the mover deals its collisionDamage to the target. Being rammed deals
    /// nothing back — a minion only hurts an enemy when it is the one that collides into it, never when an
    /// enemy collides into it. A plain minion (collisionDamage 0) deals nothing. Same-side or
    /// missing-owner collisions are ignored.
    /// </summary>
    private void ApplyCollisionDamage(MinionController mover, MinionController target)
    {
        if (mover == null || target == null) return;
        if (mover.modal == null) return;
        if (mover.owner == null || target.owner == null) return;
        if (mover.owner == target.owner) return; // friendly bump — no damage

        int moverDamage = mover.modal.collisionDamage;
        if (moverDamage > 0) target.TakeDamage(moverDamage);
    }

    private IEnumerator InvokeOnMinionCollidedActions(MinionController minion, MinionController collidedMinion)
    {
        // FinishTriggeredAction is OUTSIDE the using (see InvokeOnMinionDeathActions) so this scope's
        // Restore() can't clobber the selection state of whatever pending trigger it drains next.
        try
        {
            using (ActionHolder.PushScope())
            {
                _executingTriggeredActions = true;

                onMinionCollidedActions.Clear();
                ActionHolder.ResetSelections();
                ActionHolder.selectedTargetMinions.Clear();
                ActionHolder.selectedTargetMinions.Add(collidedMinion);
                ActionHolder.thisMinion = minion;
                ActionHolder.thisCardSO = minion.card;
                ActionHolder.thisCard = null;
                ActionHolder.selectedAgent = minion.owner;
                ActionHolder.curActionsList = onMinionCollidedActions;

                this.isTesting = false;

                // Intrinsic collision damage: the minion that moved into another deals its collisionDamage
                // to that enemy (the rammed minion deals nothing back). Default collisionDamage is 0, so
                // this is inert unless a passive (the Collision Damage Aura) raised it. Applied before the
                // scripted OnMinionCollided so card effects observe any deaths it caused, and inside this
                // scope so the TakeDamage-driven took-damage/death triggers serialize on the same scheduler.
                ApplyCollisionDamage(minion, collidedMinion);

                minion.modal.OnMinionCollided.Invoke();

                yield return StartCoroutine(ExecuteActions(onMinionCollidedActions));
            }
        }
        finally
        {
            FinishTriggeredAction();
        }
    }
}

