using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Agent : MonoBehaviour
{
    public List<IEnumerator> availableActions = new List<IEnumerator>();

    public DeckSO deckSO;
    public List<CardSO> deck = new List<CardSO>();
    public List<CardController> hand = new List<CardController>();
    public List<MinionController> minions = new List<MinionController>();
    public MinionController hero;
    public HandManager handManager;
    public CardHandLayout cardHandLayout;
    public DeckViewHandler deckViewHandler;

    public CardController cardPrefab;
    public Transform cardPlayPos;

    [Header("Passive UI")]
    [Tooltip("Canvas holding this agent's hero-passive indicator (Assets/Prefabs/UI/PassiveUICanvas). Spawned under passiveUIPos at startup rather than authored on the hero, so the row sits at a fixed board position instead of riding the hero's transform.")]
    public GameObject passiveUICanvasPrefab;

    [Tooltip("Where this agent's passive UI is spawned. A child of the agent, placed where the indicator row should sit.")]
    public Transform passiveUIPos;

    [Header("Empty Deck Draw")]
    [Tooltip("How many upgraded cards are offered when this agent draws from an empty deck.")]
    [SerializeField] private int emptyDeckChoiceCount = 3;

    [Tooltip("How long the AI 'thinks' with its face-down options on screen before committing to a pick. " +
             "Without this the choice resolves the instant the panel opens, which reads as no choice at all.")]
    [SerializeField] private float emptyDeckAiThinkSeconds = 1f;

    [Tooltip("How long the face-down panel lingers AFTER the AI has picked, before it closes itself.")]
    [SerializeField] private float emptyDeckSpectatorSeconds = 1.25f;

    [Tooltip("Panel message shown to the player for their own empty-deck draw. {0} is the damage it costs.")]
    [SerializeField] private string emptyDeckPlayerPrompt = "Your deck is empty — take a card and {0} damage";

    [Tooltip("Panel message shown to the player while the opponent takes an empty-deck draw. {0} is the damage it costs them.")]
    [SerializeField] private string emptyDeckOpponentPrompt = "Opponent's deck is empty — they take a card and {0} damage";

    /// <summary>
    /// How many times this agent has drawn from an empty deck. The next such draw costs this + 1 hero
    /// health, so the cost escalates 1, 2, 3... and never resets. Tracked per agent: each side pays for
    /// its own deck running out, not for the other's.
    /// </summary>
    public int emptyDeckDrawCount { get; private set; }

    // True while ANY agent's empty-deck draw is waiting on its card choice. Static because CardChoice and
    // its panel are a single shared resource — two concurrent draws would fight over one panel.
    private static bool _emptyDeckDrawInProgress;

    // Cleared at the start of every play session. Without this, stopping play while a draw is mid-pick
    // would leave the flag set, and with Enter Play Mode Options skipping the domain reload the statics
    // survive — every empty-deck draw next session would then wait forever on a gate nobody holds.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEmptyDeckDrawGate() => _emptyDeckDrawInProgress = false;

    public virtual bool IsPlayer() { return false; }

    protected int _availibleMana;

    public virtual int availibleMana
    {
        get { return _availibleMana; }
        set { _availibleMana = value; }
    }

    // The base Awake is the opponents' (Player overrides it): load whatever was chosen for the AI in
    // the setup scene, falling back to the authored deckSO so Game.unity still runs when opened directly.
    protected virtual void Awake()
    {
        ApplySavedSelection(SelectionSide.Opponent);

        if (deck.Count == 0 && deckSO != null)
            deck.AddRange(deckSO.cards);

        ShuffleDeck();
        RefreshDeckView();
        SpawnPassiveUI();
    }

    /// <summary>
    /// Spawns this agent's passive UI under <see cref="passiveUIPos"/> and hands the indicator inside it
    /// to the hero's view, which owns everything the indicator renders.
    ///
    /// Called from Awake, and that is what makes the ordering safe rather than lucky: the bind that
    /// fills the indicator (HeroPassiveSystem.Register) runs from GameManager.SetupGame, a coroutine off
    /// GameLoop, so every Awake in the scene has already finished by then. AttachIndicator rebinds
    /// anyway if it arrives late, so a future reorder degrades to a rebuild instead of an empty row.
    /// </summary>
    protected void SpawnPassiveUI()
    {
        if (passiveUICanvasPrefab == null || passiveUIPos == null)
        {
            Debug.LogWarning(
                $"[Agent] '{name}' is missing its passive UI wiring " +
                $"({(passiveUICanvasPrefab == null ? "passiveUICanvasPrefab" : "passiveUIPos")} is unassigned), " +
                $"so this agent's hero passives will not be shown.", this);
            return;
        }

        // instantiateInWorldSpace: false — keep the prefab's authored local offset and scale (the canvas
        // is world-space at 0.1 scale) and let the anchor place it, rather than dragging the prefab's
        // authored world position along and landing wherever that happens to be.
        GameObject canvas = Instantiate(passiveUICanvasPrefab, passiveUIPos, false);
        canvas.name = passiveUICanvasPrefab.name; // drop Unity's "(Clone)", so the hierarchy stays readable

        // Search inactive children too: the indicator ships hidden, and Hide() may already have run.
        HeroPassiveIndicator indicator = canvas.GetComponentInChildren<HeroPassiveIndicator>(true);
        if (indicator == null)
        {
            Debug.LogWarning($"[Agent] '{passiveUICanvasPrefab.name}' has no HeroPassiveIndicator in it.", this);
            return;
        }

        HeroPassiveIndicatorView view = HeroPassiveIndicatorView.For(hero);
        if (view == null)
        {
            Debug.LogWarning($"[Agent] '{name}' has no hero with a HeroPassiveIndicatorView to attach the passive UI to.", this);
            return;
        }

        view.AttachIndicator(indicator);
    }

    /// <summary>
    /// Applies the deck and hero chosen for <paramref name="side"/> in the setup scene. Runs in Awake
    /// so HeroController.Start()/Initialize() and GameManager.SetupGame() (passive registration) see
    /// the selected HeroSO.
    /// </summary>
    protected void ApplySavedSelection(SelectionSide side)
    {
        var saveManager = SaveManager.Instance;

        if (hero != null)
        {
            var selectedHero = HeroDatabase.Instance.GetSelectedHero(side);
            if (selectedHero != null)
                hero.card = selectedHero;
        }

        var decks = saveManager.GetDecks(side);
        var selectedDeck = decks[saveManager.GetSelectedDeckIndex(side)];

        deck.Clear();
        foreach (var cardName in selectedDeck.Deck)
        {
            CardSO cardSO = DeckDatabase.Instance.GetCard(cardName);
            if (cardSO != null)
                deck.Add(cardSO);
            else
                Debug.LogWarning($"Card with name {cardName} not found in database.");
        }
    }

    protected void ShuffleDeck()
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            var temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

    protected void RefreshDeckView()
    {
        if (deckViewHandler == null) return;

        deckViewHandler.UpdateView(deck.Count, deck.Count > 0 && deck[deck.Count - 1].isUpgraded);
    }

    private void Start()
    {
        MinionController.OnDied += UpdateMinions;
    }

    private void OnDestroy()
    {
        MinionController.OnDied -= UpdateMinions;
    }

    public virtual IEnumerator UpdateAvailableActions()
    {
        availableActions.Clear();
        yield break;
    }

    public virtual IEnumerator PlayTurn()
    {
        while (GameManager.Instance.isPlayerTurn)
            yield return null;
    }

    public virtual IEnumerator SkipTurn()
    {
        yield break;
    }

    public void UpdateHand()
    {
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (hand[i] == null)
            {
                hand.RemoveAt(i);
            }
        }
        // Layout's own null-sweep runs in UpdateCardPositions each frame.
    }

    public void UpdateMinions(MinionController minion)
    {
        if (!minions.Contains(minion)) return;
        minions.Remove(minion);
    }

    // Extra cards this agent is owed at the start of its NEXT turn (e.g. Do Nothing's delayed draw).
    // Banked here rather than on a trigger because the effect comes from a spell, which leaves no
    // minion behind to carry an OnTurnStart. Paid and cleared by GameManager.DrawTurnStartCards.
    public int pendingExtraDraws = 0;

    // Hands back the owed draws and clears the debt, so it can only ever be paid once.
    public int ConsumePendingExtraDraws()
    {
        int owed = pendingExtraDraws;
        pendingExtraDraws = 0;
        return owed;
    }

    /// <summary>
    /// Fire-and-forget draw, kept for the opening hand in GameManager.SetupGame, where the deck is
    /// guaranteed to still have cards in it.
    ///
    /// Every other draw must use <see cref="DrawCardRoutine"/> and yield on it: an empty deck now opens a
    /// selection panel and waits for a pick, which a void method cannot express. If the deck IS empty
    /// here the routine is started unsequenced rather than the draw being silently dropped, so the
    /// empty-deck rule still fires — it just isn't ordered against whatever the caller does next.
    /// </summary>
    public void DrawCard()
    {
        UpdateHand();

        if (deck.Count == 0)
        {
            GameManager.Instance.StartCoroutine(DrawCardRoutine());
            return;
        }

        if (hand.Count >= 7) return;

        DrawTopCard();
    }

    /// <summary>
    /// The real draw. Takes the top card when there is one, and otherwise runs the empty-deck draw, which
    /// blocks on a card choice — so callers must <c>yield return</c> on this rather than fire it off.
    /// </summary>
    public IEnumerator DrawCardRoutine()
    {
        UpdateHand();

        if (deck.Count > 0)
        {
            // Overdrawing at the hand cap burns the card, as it always has.
            if (hand.Count < 7) DrawTopCard();
            yield break;
        }

        yield return StartCoroutine(DrawFromEmptyDeck());
    }

    private void DrawTopCard()
    {
        CardSO cardSO = deck[deck.Count - 1];
        deck.RemoveAt(deck.Count - 1);

        CardController cardObj = InstantiateCard(cardSO);
        hand.Add(cardObj);
        cardHandLayout.AddCard(cardObj.transform);

        GameManager.Instance.TriggerCardDrawActions(this);
        deckViewHandler.UpdateView(deck.Count, deck.Count == 0 ? false : deck[deck.Count - 1].isUpgraded);
    }

    /// <summary>
    /// The empty-deck draw: with nothing left to draw, the agent is offered a handful of upgraded cards
    /// instead, and pays for the privilege in hero health — 1 the first time, 2 the second, and so on,
    /// separately for each side and never reset. That escalation is what stops a match stalling once both
    /// decks are dry: the two heroes are now on a clock that only gets faster.
    ///
    /// The player picks from a face-up panel. When the OPPONENT draws this way the same panel opens for
    /// the player showing card backs, so they can see the opponent is burning down but not what was on
    /// offer; it closes itself after <see cref="emptyDeckSpectatorSeconds"/> without revealing the pick.
    /// </summary>
    private IEnumerator DrawFromEmptyDeck()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.isTesting) yield break;

        // Queue behind any empty-deck draw already resolving — including one on the OTHER agent, since
        // there is a single selection panel between them. A "draw 2" on a dry deck, an on-draw trigger
        // that draws again, or the fire-and-forget DrawCard() fallback can all land a second draw while
        // the first is still waiting on a pick; without this gate the second call's Begin() preempts the
        // first panel, which makes the first draw resolve with no card while its damage still lands.
        while (_emptyDeckDrawInProgress)
            yield return null;

        _emptyDeckDrawInProgress = true;
        try
        {
            yield return StartCoroutine(ResolveEmptyDeckDraw(gm));
        }
        finally
        {
            _emptyDeckDrawInProgress = false;
        }
    }

    private IEnumerator ResolveEmptyDeckDraw(GameManager gm)
    {
        // Every check below is re-evaluated here rather than before the gate: a draw that queued behind
        // another one may find the hand now full, or the game already over, because of what resolved
        // while it waited.
        UpdateHand();

        if (gm.currentState == GameState.EndGame) yield break;

        int damage = ++emptyDeckDrawCount;

        // Hand full: no panel, because there is no card the agent could accept and a choice that resolves
        // to nothing is worse than no choice at all. The clock still ticks — the damage is the cost of
        // having run out of deck, not of taking the card.
        if (hand.Count >= 7)
        {
            Debug.Log($"[EmptyDeck] {name} drew from an empty deck with a full hand: {damage} damage, no card offered");
            ApplyEmptyDeckDamage(damage);
            yield break;
        }

        var pool = DeckDatabase.Instance != null ? DeckDatabase.Instance.AllUpgradedCards : null;
        List<CardSO> options = CardPoolSO.PickDistinct(pool, emptyDeckChoiceCount);

        if (options.Count == 0)
        {
            Debug.LogWarning("[EmptyDeck] no upgraded cards in the database to offer; taking the damage only");
            ApplyEmptyDeckDamage(damage);
            yield break;
        }

        CardSO chosen = null;

        if (IsPlayer())
        {
            // requirePlayerTurn: false — a triggered draw can empty the player's deck during the
            // OPPONENT's turn, and the choice still belongs to the player.
            CardChoice.Instance.Begin(
                options,
                string.Format(emptyDeckPlayerPrompt, damage),
                picked => chosen = picked,
                requirePlayerTurn: false);

            // Waiting on HasActiveRequest rather than on `chosen` alone: if the request is torn down
            // without a pick (game over, or another effect preempting it with its own choice) this exits
            // instead of hanging the turn forever. No pick simply means no card — the damage still lands.
            while (chosen == null && CardChoice.Instance.HasActiveRequest && gm.currentState != GameState.EndGame)
                yield return null;

            CardChoice.Instance.Cancel();
        }
        else
        {
            CardChoice.Instance.BeginFaceDown(options, string.Format(emptyDeckOpponentPrompt, damage));

            // Let the options sit on screen before the AI commits, so the player reads it as a decision
            // being made rather than a panel that blinks past.
            yield return new WaitForSeconds(emptyDeckAiThinkSeconds);

            // Picked at random rather than by the brain: the panel is face down, so a "smart" pick is
            // invisible to the player and would only make free upgraded cards swingier. Change here if
            // you want the AI to value the roll (OpponentBrained.ChooseCard takes the highest cost).
            chosen = options[Random.Range(0, options.Count)];

            yield return new WaitForSeconds(emptyDeckSpectatorSeconds);
            CardChoice.Instance.Cancel();
        }

        if (chosen != null)
        {
            Debug.Log($"[EmptyDeck] {name} took '{chosen.cardName}' and paid {damage} damage");
            // Goes through AddCard, so this counts as a draw for "whenever you draw a card" triggers, and
            // the card arrives at its normal printed cost.
            AddCard(chosen);
        }

        ApplyEmptyDeckDamage(damage);
    }

    /// <summary>
    /// Charges the empty-deck draw to this agent's hero. Routed through TakeDamage so the damage number,
    /// OnTookDamage triggers and hero-damage passives all fire exactly as they would for a hit from the
    /// board — heroes carry no armor today, so nothing absorbs it.
    /// </summary>
    private void ApplyEmptyDeckDamage(int damage)
    {
        if (hero == null)
        {
            Debug.LogWarning($"[EmptyDeck] {name} has no hero to charge {damage} damage to");
            return;
        }

        hero.TakeDamage(damage);

        // Checked immediately as well as from GameManager.Update, so a hero that dies to the clock ends
        // the match before the rest of the turn plays out on top of it.
        GameManager.Instance.CheckWinCondition();
    }

    /// <summary>
    /// Put a card straight into this agent's hand. Returns the created card, or null when it couldn't be
    /// added (full hand, null SO) — callers that need to keep tweaking it, e.g. the Discover verb zeroing
    /// the cost of what the player picked, read it off the return value.
    /// </summary>
    public CardController AddCard(CardSO cardSO, Transform startPos = null)
    {
        UpdateHand();

        if (hand.Count >= 7) return null;

        if (cardSO == null)
        {
            Debug.LogWarning("Agent.AddCard called with null CardSO");
            return null;
        }

        CardController cardObj = InstantiateCard(cardSO);
        hand.Add(cardObj);
        cardHandLayout.AddCard(cardObj.transform, startPos);

        GameManager.Instance.TriggerCardDrawActions(this);
        deckViewHandler.UpdateView(deck.Count, deck.Count == 0 ? false : deck[deck.Count - 1].isUpgraded);

        return cardObj;
    }

    public void RemoveCardFromHand(CardController card)
    {
        if (card == null) return;
        cardHandLayout.RemoveCard(card.transform);
        hand.Remove(card);
    }

    public void SpawnCardToDeck(CardSO card, bool isPlayerCard)
    {
        deck.Insert(Random.Range(0, Mathf.Max(0, deck.Count - 1)), card);

        CardController cardObj = Instantiate(cardPrefab);
        cardObj.transform.SetParent(cardHandLayout.transform.parent);
        cardObj.transform.SetSiblingIndex(cardHandLayout.transform.parent.childCount-1);

        cardObj.transform.position = cardPlayPos.position;
        cardObj.card = card;
        cardObj.modal.UpdateModal(card, this, isPlayerCard);
        cardObj.view.UpdateView(cardObj.modal);
        cardObj.transform.localScale = Vector3.zero;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(cardObj.transform.DOScale(Vector3.one*1.2f, 0.25f));
        sequence.Append(DOVirtual.DelayedCall(0.5f, () => { }));

        sequence.Append(cardObj.transform.DOJump(cardHandLayout.deckPosition.position + Vector3.up * 50f, 50f, 1, 0.5f));
        sequence.Join(cardObj.transform.DOScale(cardHandLayout.cardinitialScale, 0.5f));
        sequence.Join(cardObj.transform.DORotate(Vector3.up * 90, 0.15f).OnComplete(() =>
        {
            cardObj.modal.isPlayerMinion = false;
            cardObj.view.UpdateView(cardObj.modal);
            cardObj.transform.DORotate(Vector3.up * 0, 0.15f);
        }));
        sequence.AppendCallback(() => cardObj.transform.SetSiblingIndex(deck.Count > 1 ? 0 : cardHandLayout.transform.parent.childCount - 1));
        sequence.Append(cardObj.transform.DOMove(cardHandLayout.deckPosition.GetChild(cardHandLayout.deckPosition.childCount - 1).position, 0.5f));
        sequence.OnComplete(() => Destroy(cardObj.gameObject));

        deckViewHandler.UpdateView(deck.Count, deck[deck.Count - 1].isUpgraded);
    }

    public void AddCardToDeck(CardController card)
    {
        if (card == null || card.card == null)
        {
            Debug.LogWarning("Agent.AddCardToDeck called with null card");
            return;
        }

        int insertIndex = Random.Range(0, Mathf.Max(0, deck.Count - 1));
        deck.Insert(insertIndex, card.card);

        if (deckViewHandler != null)
            deckViewHandler.UpdateView(deck.Count, deck[deck.Count - 1].isUpgraded);

        RemoveCardFromHand(card);

        Transform deckTarget = cardHandLayout != null && cardHandLayout.deckPosition != null && cardHandLayout.deckPosition.childCount > 0
            ? cardHandLayout.deckPosition.GetChild(cardHandLayout.deckPosition.childCount - 1)
            : (cardHandLayout != null ? cardHandLayout.deckPosition : null);

        if (deckTarget == null)
        {
            Destroy(card.gameObject);
            return;
        }

        card.transform.SetParent(cardHandLayout.transform.parent);
        card.transform.SetSiblingIndex(0);

        Sequence sequence = DOTween.Sequence();
        sequence.Append(card.transform.DOJump(cardHandLayout.deckPosition.position + Vector3.up * 50f, 50f, 1, 0.5f));
        sequence.Join(card.transform.DOScale(cardHandLayout.cardinitialScale, 0.5f));
        sequence.Join(card.transform.DORotate(Vector3.up * 90, 0.15f).OnComplete(() =>
        {
            if (card != null && card.modal != null && card.view != null)
            {
                card.modal.isPlayerMinion = false;
                card.view.UpdateView(card.modal);
                card.transform.DORotate(Vector3.zero, 0.15f);
            }
        }));
        sequence.Append(card.transform.DOMove(deckTarget.position, 0.5f));
        sequence.OnComplete(() =>
        {
            if (card != null) Destroy(card.gameObject);
        });
    }

    public CardSO RemoveRandomCardFromDeck()
    {
        if (deck.Count == 0) return null;

        int randomIndex = Random.Range(0, deck.Count);
        CardSO card = deck[randomIndex];
        deck.RemoveAt(randomIndex);
        deckViewHandler.UpdateView(deck.Count, deck.Count == 0 ? false : card.isUpgraded);
        return card;
    }

    public CardController InstantiateCard(CardSO cardSO)
    {
        CardController cardObj = Instantiate(cardPrefab);
        cardObj.gameObject.name = cardSO.cardName;
        cardObj.card = cardSO;
        cardObj.modal.UpdateModal(cardSO, this, IsPlayer());
        cardObj.view.UpdateView(cardObj.modal);

        // Only the player can drag their own cards — the opponent's hand is never draggable.
        if (cardObj.draggableItem != null)
            cardObj.draggableItem.Interactable = IsPlayer();

        return cardObj;
    }
}
