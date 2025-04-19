using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardModal : MonoBehaviour
{
    public Sprite[] art;
    public new string name;
    [TextAreaAttribute] public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;

    public int defHealth;
    public bool isPlayerMinion = true;

    public void UpdateModal(CardTEst card)
    {
        art = new Sprite[2];
        name = card.name;
        desc = card.desc;
        attack = card.attack;
        health = card.health;
        defHealth = card.defHealth; 
        range = card.range;
        art = card.art;
        cost = card.cost;
        //isPlayerMinion = GameManager.Instance.isPlayerTurn;
    }
}
