using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "TestCArd", menuName = "New Test Card")]
public class CardSO : ScriptableObject
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
    public Type type;

    public CardSO upgradedVersion;

    public bool isUpgraded = false;

    [HideInInspector] public int defHealth;

    public UnityEvent OnPlay; //
    public UnityEvent OnDeath; // 
    public UnityEvent OnTurnStart; 
    public UnityEvent OnOwnerTurnEnd; //
    public UnityEvent OnAnyMinionSummoned;
    public UnityEvent OnOwnerDrawedCard; //
    

    void OnEnable()
    {
        defHealth = health;
    }

    public enum Type
    {
        None, Buff, Debuff
    }
}
