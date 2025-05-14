using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardModal : MonoBehaviour
{
    public Sprite minionArt;
    public Sprite cardArt;
    public Sprite frame;

    public new string name;
    [TextAreaAttribute] public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;

    public int defHealth;
    public bool isPlayerMinion = true;
    public CardSO upgradedVerdion;
    public bool isUpgraded = false;
    public bool canMove = true;

    public void UpdateModal(CardSO card)
    {
        minionArt = card.minionArt;
        cardArt = card.cardArt;

        frame = card.frame;
        name = card.name;
        desc = card.desc;
        attack = card.attack;
        health = card.health;
        defHealth = card.defHealth; 
        range = card.range;
        cost = card.cost;
        upgradedVerdion = card.upgradedVersion;
        isUpgraded = card.isUpgraded;
        //isPlayerMinion = GameManager.Instance.isPlayerTurn;
    }
}
