using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class Agent : MonoBehaviour
{
    public List<IEnumerator> availableActions = new List<IEnumerator>();

    public List<CardTEst> deck = new List<CardTEst>();
    public List<CardController> hand = new List<CardController>();
    public List<MinionController> minions = new List<MinionController>();
    public MinionController hero;
    public HandManager handManager;
    public CardHandLayout cardHandLayout;

    public CardController cardPrefab;

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
        /*for (int i = hand.Count -1; i >= 0; i--)
        {
            if(hand[i] == null)
            {
                //hand.RemoveAt(i);
                cardHandLayout.RemoveCard(hand[i].transform);
            }
        }*/
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

        CardTEst card = deck[Random.Range(0, deck.Count)];
        deck.Remove(card);
        CardController cardObj = Instantiate(cardPrefab);
        cardObj.card = card;
        cardObj.modal.isPlayerMinion = isPlayerCard;
        cardObj.modal.UpdateModal(card);
        cardObj.view.UpdateView(cardObj.modal);
        hand.Add(cardObj);
        //handManager.AddToHand(cardObj.transform, handManager.GetEmptyHandSlot());
        cardHandLayout.AddCard(cardObj.transform);
        StartCoroutine(GameManager.Instance.InvokeOnCardDrawActions());
    }
}
