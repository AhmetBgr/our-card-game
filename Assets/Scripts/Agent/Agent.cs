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

    public void DrawCard()
    {
        UpdateHand();

        if (deck.Count == 0 || hand.Count >= 7) return;

        CardSO cardSO = deck[deck.Count - 1];
        deck.RemoveAt(deck.Count - 1);

        CardController cardObj = InstantiateCard(cardSO);
        hand.Add(cardObj);
        cardHandLayout.AddCard(cardObj.transform);

        GameManager.Instance.TriggerCardDrawActions(this);
        deckViewHandler.UpdateView(deck.Count, deck.Count == 0 ? false : deck[deck.Count - 1].isUpgraded);
    }

    public void AddCard(CardSO cardSO, Transform startPos = null)
    {
        UpdateHand();

        if (hand.Count >= 7) return;

        if (cardSO == null)
        {
            Debug.LogWarning("Agent.AddCard called with null CardSO");
            return;
        }

        CardController cardObj = InstantiateCard(cardSO);
        hand.Add(cardObj);
        cardHandLayout.AddCard(cardObj.transform, startPos);

        GameManager.Instance.TriggerCardDrawActions(this);
        deckViewHandler.UpdateView(deck.Count, deck.Count == 0 ? false : deck[deck.Count - 1].isUpgraded);
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
