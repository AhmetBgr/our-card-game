using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CardModal : MonoBehaviour
{
    public Sprite minionArt;
    public Sprite cardArt;

    public new string name;
    [TextAreaAttribute] public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;
    public int armor;

    public int defHealth;
    public Agent owner;
    public bool isPlayerMinion = true;
    public CardSO upgradedVerdion;
    public bool isUpgraded = false;
    public bool canMove = true;
    public bool canAttack = true;
    public bool canAttackManually = true;

    public UnityEvent OnPlay;
    public UnityEvent OnDeath;
    public UnityEvent OnTurnStart;
    public UnityEvent OnOwnerTurnEnd;
    public UnityEvent OnAnyMinionSummoned;
    //public UnityEvent OnAnyMinionDied;
    public UnityEvent OnOwnerDrawedCard;
    public UnityEvent OnMinionCollided;
    public UnityEvent OnTookDamage;
    public UnityEvent BonusEvents; // Holds events to be added to another event

    public void UpdateModal(CardSO card, Agent owner)
    {
        minionArt = card.minionArt;
        cardArt = card.cardArt;

        name = card.cardName;
        desc = card.desc;
        attack = card.attack;
        health = card.health;
        defHealth = card.defHealth; 
        range = card.range;
        cost = card.cost;
        armor = card.armor;
        canMove = card.canMove;
        canAttack = card.canAttack;
        canAttackManually = card.canAttackManually;
        upgradedVerdion = card.upgradedVersion;
        isUpgraded = card.isUpgraded;

        // Copy event templates from CardSO into this runtime modal.
        // Most are reference-copied (gameplay invokes via the modal).
        OnPlay = card.OnPlay;
        OnDeath = card.OnDeath;
        OnTurnStart = card.OnTurnStart;
        OnOwnerTurnEnd = card.OnOwnerTurnEnd;
        OnAnyMinionSummoned = card.OnAnyMinionSummoned;
        OnOwnerDrawedCard = card.OnOwnerDrewCard;
        OnMinionCollided = card.OnMinionCollided;
        BonusEvents = card.BonusEvents;

        // OnTookDamage receives runtime AddListener calls (see ActionHolder.AddBonusEventsToMinionsOnTookDamage).
        // Use a per-instance UnityEvent that relays into the SO's event, so runtime listeners
        // don't accumulate on the shared CardSO asset across plays / matches.
        OnTookDamage = new UnityEvent();
        if (card.OnTookDamage != null)
        {
            var soOnTookDamage = card.OnTookDamage;
            OnTookDamage.AddListener(soOnTookDamage.Invoke);
        }
        this.owner = owner;
        //isPlayerMinion = GameManager.Instance.isPlayerTurn;
    }
}
