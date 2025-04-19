using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "TestCArd", menuName = "New Test Card")]
public class CardTEst : ScriptableObject
{
    public Sprite[] art;

    public new string name;
    [TextAreaAttribute] public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;
    public Type type;
    [HideInInspector] public int defHealth;

    //[HideInInspector] public MinionController ownerMinion;


    public List<int> effectValues;

    public UnityEvent OnPlay;
    public UnityEvent OnDeath;
    public UnityEvent OnTurnStart;
    public UnityEvent OnOwnerTurnEnd;
    public UnityEvent OnSpellPlayed;
    public UnityEvent OnMinionPlayed;
    public UnityEvent OnThisMoved;
    public UnityEvent OnAnyMoved;
    public UnityEvent OnOwnerDrawedCard;
    

    void OnEnable()
    {
        defHealth = health;
    }

    public enum Type
    {
        None, Buff, Debuff
    }
}
