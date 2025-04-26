using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardModal : MonoBehaviour
{
    public Sprite[] art;
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
    public void UpdateModal(CardSO card)
    {
        art = new Sprite[2];
        frame = card.frame;
        name = card.name;
        desc = card.desc;
        attack = card.attack;
        health = card.health;
        defHealth = card.defHealth; 
        range = card.range;
        art = card.art;
        cost = card.cost;
        upgradedVerdion = card.upgradedVersion;
        //isPlayerMinion = GameManager.Instance.isPlayerTurn;
    }
}
