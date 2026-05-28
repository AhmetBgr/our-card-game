using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "Card", menuName = "New Card")]
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

    [Header("AI Hints")]
    [Tooltip("Beneficial = buff/heal a minion; Harmful = damage/destroy; Neutral = summon, draw, self-only.")]
    public CardIntent aiIntent = CardIntent.Neutral;
    [Tooltip("Absolute magnitude of the dominant ChangeMinionHealth/Attack/DefHealth call (e.g., 3 for ChangeMinionHealth(-3)). 0 if not applicable.")]
    public int aiEffectMagnitude = 0;
    [Tooltip("True for SwitchSide, destroy-target, banish — anything that removes the targeted minion from its owner's board.")]
    public bool aiRemovesTarget = false;

    public CardSO upgradedVersion;
    public CardSO normalVersion;

    public bool isUpgraded = false;

    [HideInInspector] public int defHealth;


    public UnityEvent OnPlay; //
    public UnityEvent OnDeath; // 
    public UnityEvent OnTurnStart; 
    public UnityEvent OnOwnerTurnEnd; //
    public UnityEvent OnAnyMinionSummoned;
    //public UnityEvent OnAnyMinionDied;
    public UnityEvent OnOwnerDrewCard; //
    public UnityEvent OnMinionCollided; //
    public UnityEvent OnTookDamage; //

    public UnityEvent BonusEvents; // Holds events to be added to another event

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

    public enum CardIntent
    {
        Neutral, Beneficial, Harmful
    }
}
