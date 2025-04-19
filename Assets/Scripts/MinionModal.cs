using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class MinionModal 
{
    public Sprite art;
    public new string name;
    public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;

    public int defHealth;
    public bool isPlayerMinion = true;

    public MinionController owner;

    public UnityEvent OnPlay;
    public UnityEvent OnDeath;
    public UnityEvent OnTurnStart;
    public UnityEvent OnOwnerTurnEnd;
    public UnityEvent OnSpellPlayed;
    public UnityEvent OnMinionPlayed;
    public UnityEvent OnThisMoved;
    public UnityEvent OnAnyMoved;
    public UnityEvent OnOwnerDrawedCard;

    public MinionModal(CardSO card, MinionController owner)
    {
        UpdateModal(card, owner);

        //isPlayerMinion = GameManager.Instance.isPlayerTurn;
    }

    public void UpdateModal(CardSO card, MinionController owner)
    {
        name = card.name;
        desc = card.desc;
        attack = card.attack;
        health = card.health;
        defHealth = card.defHealth;
        range = card.range;
        art = card.art[1];
        cost = card.cost;
        this.owner = owner;


        OnPlay = card.OnPlay;
        OnDeath = card.OnDeath;
        OnTurnStart = card.OnTurnStart;
        OnOwnerTurnEnd = card.OnOwnerTurnEnd;
        OnSpellPlayed = card.OnSpellPlayed;
        OnMinionPlayed = card.OnMinionPlayed;
        OnThisMoved = card.OnThisMoved;
        OnAnyMoved = card.OnAnyMoved;
        OnOwnerDrawedCard = card.OnOwnerDrawedCard;
    }
}
