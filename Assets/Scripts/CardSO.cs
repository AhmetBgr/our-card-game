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

    [Tooltip("Damage this minion deals to an enemy minion when it MOVES into it (collides). Only the " +
             "moving minion deals damage — being rammed deals nothing back. 0 = none. Default 0 keeps " +
             "existing minions unchanged; the Collision Damage Aura hero passive raises it to 1 on friendly minions.")]
    public int collisionDamage = 0;

    public bool canMove = true;
    [Tooltip("If false, this unit can never attack — not even via triggered/automatic card actions.")]
    public bool canAttack = true;
    [Tooltip("If false, this unit cannot be selected to attack manually (player click / AI choice), but can still attack via triggered/automatic card actions when canAttack is true.")]
    public bool canAttackManually = true;

    public Type type;

    [Tooltip("Hover intent shown while this card is picking a target minion. Set to ToPush on push cards so hovering a candidate previews the push arrow.")]
    public HoverIntent selectionIntent = HoverIntent.ToSelectGenerally;

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
