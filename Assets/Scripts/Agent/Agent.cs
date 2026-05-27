using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Agent : MonoBehaviour
{
    public List<IEnumerator> availableActions = new List<IEnumerator>();

    public List<CardSO> deck = new List<CardSO>();
    public List<CardController> hand = new List<CardController>();
    public List<MinionController> minions = new List<MinionController>();
    public MinionController hero;
    public HandManager handManager;
    public CardHandLayout cardHandLayout;
    public DeckViewHandler deckViewHandler;

    public CardController cardPrefab;
    public Transform cardPlayPos;

    public virtual bool IsPlayer() { return false; }

    protected int _availibleMana;

    public virtual int availibleMana
    {
        get { return _availibleMana; }
        set { _availibleMana = value; }
    }

    protected virtual void Awake()
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            var temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        deckViewHandler.UpdateView(deck.Count, deck[deck.Count - 1].isUpgraded);
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

    public void DrawCard()
    {
        UpdateHand();

        if (deck.Count == 0 || hand.Count >= 7) return;

        CardSO cardSO = deck[deck.Count - 1];
        deck.RemoveAt(deck.Count - 1);

        CardController cardObj = InstantiateCard(cardSO);
        hand.Add(cardObj);
        cardHandLayout.AddCard(cardObj.transform);

        StartCoroutine(GameManager.Instance.InvokeOnCardDrawActions());
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

        StartCoroutine(GameManager.Instance.InvokeOnCardDrawActions());
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
        cardObj.transform.SetSiblingIndex(0);

        cardObj.transform.position = cardPlayPos.position;
        cardObj.card = card;
        cardObj.modal.isPlayerMinion = isPlayerCard;
        cardObj.modal.UpdateModal(card);
        cardObj.view.UpdateView(cardObj.modal);
        cardObj.transform.localScale = Vector3.zero;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(cardObj.transform.DOScale(Vector3.one, 0.25f));
        sequence.Append(DOVirtual.DelayedCall(0.5f, () => { }));
        sequence.Append(cardObj.transform.DOJump(cardHandLayout.deckPosition.position + Vector3.up * 50f, 50f, 1, 0.5f));
        sequence.Join(cardObj.transform.DOScale(cardHandLayout.cardinitialScale, 0.5f));
        sequence.Join(cardObj.transform.DORotate(Vector3.up * 90, 0.15f).OnComplete(() =>
        {
            cardObj.modal.isPlayerMinion = false;
            cardObj.view.UpdateView(cardObj.modal);
            cardObj.transform.DORotate(Vector3.up * 0, 0.15f);
        }));
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
        cardObj.modal.isPlayerMinion = IsPlayer();
        cardObj.modal.UpdateModal(cardSO);
        cardObj.view.UpdateView(cardObj.modal);
        return cardObj;
    }
}
