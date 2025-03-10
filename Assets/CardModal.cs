using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardModal : MonoBehaviour
{
    public Sprite art;
    public new string name;
    [TextAreaAttribute] public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;

    public int defHealth;

    public void UpdateModal(CardTEst card)
    {
        name = card.name;
        desc = card.desc;
        attack = card.attack;
        health = card.health;
        range = card.range;
        art = card.art;
    }
}
