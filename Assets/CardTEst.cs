using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.Events;


[CreateAssetMenu(fileName = "TestCArd", menuName = "New Test Card")]
public class CardTEst : ScriptableObject
{
    public Sprite art;
    public new string name;
    [TextAreaAttribute] public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;

    public int defHealth;


    public List<int> effectValues;

    public UnityEvent OnPlay;
    public UnityEvent OnDeath;
    public UnityEvent OnTurnStart;
    public UnityEvent OnTurnEnd;
    public UnityEvent OnSpellPlayed;
    public UnityEvent OnMinionPlayed;
    public UnityEvent OnThisMoved;
    public UnityEvent OnAnyMoved;

    // Start is called before the first frame update
    void OnEnable()
    {
        defHealth = health;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
