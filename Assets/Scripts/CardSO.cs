using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "TestCArd", menuName = "New Test Card")]
public class CardSO : ScriptableObject
{
    public ActionHolder actionHolder;

    public Sprite minionArt;
    public Sprite cardArt;

    public string cardName;
    [TextAreaAttribute] public string desc;
    public int attack;
    public int health;
    public int cost;
    public int range;

    public int armor;
    public bool canMove = true;

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
    public UnityEvent OnMinionCollided; //

    private void Awake()
    {
        //actionHolder = Resources.Load<ActionHolder>("");
        actionHolder = Resources.Load<ActionHolder>("ActionHolder");
    }

    void OnEnable()
    {
        defHealth = health;
    }

    public enum Type
    {
        None, Buff, Debuff
    }


}
