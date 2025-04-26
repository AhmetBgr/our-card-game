using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using DG.Tweening;
using Unity.Collections.LowLevel.Unsafe;
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

    public CardController cardPrefab;
    public Transform cardPlayPos;


    protected int _availibleMana;

    public virtual int availibleMana 
    { 
        get { return _availibleMana; }
        set { _availibleMana = value; }
    }

    private void Start()
    {
        MinionController.OnDied += UpdateMinions;
    }

    private void OnDestroy()
    {
        MinionController.OnDied += UpdateMinions;

    }

    public virtual void UpdateAvailableActions()
    {
        availableActions.Clear();

        /*foreach (var item in minions)
        {
            if (item.CanAttack())
            {
                availableActions.Add(item.Attack());
            }
        }

        foreach (var card in hand)
        {
            if (card.CanPlay())
            {
                availableActions.Add(card.Play());
            }
        }*/
    }
    public virtual IEnumerator PlayTurn()
    {
        while (GameManager.Instance.isPlayerTurn)
        {
            yield return null;
        }
    }
    public virtual IEnumerator SkipTurn()
    {
        yield break;
    }

    public void UpdateHand()
    {
        for (int i = hand.Count -1; i >= 0; i--)
        {
            if(hand[i] == null)
            {
                hand.RemoveAt(i);
                //cardHandLayout.RemoveCard(hand[i].transform);
            }
        }
    }
    public void UpdateMinions(MinionController minion)
    {
        if (!minions.Contains(minion)) return;

        minions.Remove(minion);
    }
    public void DrawCard(bool isPlayerCard)
    {
        UpdateHand();
        //handManager.UpdateSlots();

        if(deck.Count == 0 | hand.Count >= 7) return;

        CardSO card = deck[Random.Range(0, deck.Count)];
        deck.Remove(card);
        CardController cardObj = Instantiate(cardPrefab);
        cardObj.gameObject.name = card.name;
        cardObj.card = card;
        cardObj.modal.isPlayerMinion = isPlayerCard;
        cardObj.modal.UpdateModal(card);
        cardObj.view.UpdateView(cardObj.modal);
        hand.Add(cardObj);
        //handManager.AddToHand(cardObj.transform, handManager.GetEmptyHandSlot());
        cardHandLayout.AddCard(cardObj.transform);
        StartCoroutine(GameManager.Instance.InvokeOnCardDrawActions());
    }

    public void SpawnCardToDeck(CardSO card, bool isPlayerCard)
    {
        deck.Insert(Random.Range(0, Mathf.Max(0, deck.Count - 2)), card);

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
            cardObj.transform.DORotate(Vector3.up * 0, 0.15f).OnComplete(() =>
            {

            });
        }));
        sequence.Append(cardObj.transform.DOMove(cardHandLayout.deckPosition.GetChild(cardHandLayout.deckPosition.childCount -1).position, 0.5f));
        sequence.OnComplete(() =>
        {
            Destroy(cardObj.gameObject);
        });
    }
}
