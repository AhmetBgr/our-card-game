using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinionController : MonoBehaviour
{
    public SelectableEntity selectable;
    public CardModal modal;
    //public MinionModal modal;
    //public ShowInfo showInfo;

    public MinionView view;
    public CardSO card;
    public GridEntity gridEntity;
    public Animator animator;
    public MinionAnimationController animationController;
    public Agent owner;

    public SelectionType selectionType;

    public Vector3Int plannedMoveDir = Vector3Int.zero;
    public bool isMovementValidated = false;
    public bool isAttackedThisTurn = false;
    public bool canAttack = false;
    public int age = 0;

    public MinionController attackingMinion;

    public MinionController selectedMinion;

    public MinionController LastTarget;

    public SelectionType SelectionType { get => SelectionType.Minion; }
    public static event Action<List<MinionController>> OnSelectingMinionForAttack;
    public static event Action<MinionController> OnDied;
    public static event Action<MinionController, MinionController> OnCollided;
    public static event Action<MinionController, int> OnTookDamage;


    private void OnEnable()
    {
        GameManager.OnTurnEnd += OnTurnSwitch;
    }

    private void OnDisable()
    {
        GameManager.OnTurnEnd -= OnTurnSwitch;
    }

    protected virtual void Start()
    {
        /*if(showInfo != null) 
            showInfo.card = card;*/
    }
    public void Initialize(Agent owner)
    {
        modal.UpdateModal(card, owner);
        view.UpdateView(modal);
        PlayAppearAnimation();
    }

    protected virtual void PlayAppearAnimation()
    {
        view.PlayAppearAnimation();
    }
    private void OnTurnSwitch(GameState curState)
    {
        age++;
        isAttackedThisTurn = false;
        isMovementValidated = false;

    }

    protected virtual void OnMouseDown()
    {
        if (GameManager.Instance.currentState == GameState.EndGame)
            return;

        if (GameManager.Instance.player.curState == Player.State.SelectingMinion)
        {
            ActionHolder.selectedMinion = this;
        }
        else if (GameManager.Instance.player.curState == Player.State.SelectingMinionForAttack)
        {
            attackingMinion.selectedMinion = this; 
        }
        else if (canAttack)
        {
            foreach (var item in GameManager.Instance.player.minions)
            {
                item.selectable.SetSelectable(false);
            }
            GameManager.Instance.player.curState = Player.State.SelectingMinionForAttack;
            StartAttack(GameManager.Instance.opponent);
        }

    }
    protected void OnMouseEnter()
    {
        if (GameManager.Instance.currentState == GameState.EndGame)
            return;

        GameManager.Instance.player.handManager.ShowInfoCard(card);
        Debug.Log("shouldshow range");

        if (GameManager.Instance.player.curState == Player.State.SelectingMinionForAttack)
        {
            // show weapon image 
        }
        else
        {
            MinionRangeHandler.Instance.ShowRange(gridEntity.GetGridIndex(), modal.range);

        }
    }

    protected void OnMouseExit()
    {
        GameManager.Instance.player.handManager.HideInfoCard();
        MinionRangeHandler.Instance.HideRange();

    }

    public virtual void SetReadyToAttack()
    {
        if (!modal.canAttack || !modal.canAttackManually || isAttackedThisTurn || age < 1)
        {
            canAttack = false;
        }
        else{
            List<MinionController> targets = new List<MinionController>();
            targets.AddRange(GameManager.Instance.opponent.minions);
            targets.Add(GameManager.Instance.opponent.hero);
            bool targetExists = false;

            foreach (var minion in targets)
            {
                if ((minion.transform.position - transform.position).magnitude < modal.range + 1)
                {
                    targetExists = true;
                    break;
                }
            }

            canAttack = targetExists;

        }
        //showInfo.gameObject.SetActive(!canAttack);
        selectable.SetSelectable(canAttack);

    }
    public virtual bool CanAttack(Agent opponent)
    {
        if (!modal.canAttack || !modal.canAttackManually || isAttackedThisTurn || age == 0) return false;

        //var grid = GridManager.Instance.GetGrid();
        List<MinionController> targets = new List<MinionController>();
        targets.AddRange(opponent.minions);
        targets.Add(opponent.hero);
        foreach (var minion in targets)
        {
            if((minion.transform.position - transform.position).magnitude < modal.range + 1)
            {
                return true;
            }
        }
        return false;
    }

    public void StartAttack(Agent opponent, MinionController target = null)
    {
        StartCoroutine(Attack(opponent, target));
    }

    public virtual IEnumerator Attack(Agent opponent, MinionController target = null)
    {
        Debug.Log("starting Attack");

        // Cards flagged canAttack = false can never attack (manual selection or triggered/auto attacks).
        if (!modal.canAttack) yield break;

        List<MinionController> selectableminions = new List<MinionController>();
        List<MinionController> targets = new List<MinionController>();
        targets.AddRange(opponent.minions);
        targets.Add(opponent.hero);
        foreach (var minion in targets)
        {
            Debug.Log("checking if minion selectable");

            if ((minion.transform.position - transform.position).magnitude < modal.range + 1)
            {
                Debug.Log(" minion is selectable selectable");
                minion.attackingMinion = this;
                minion.selectable.SetSelectable(true);
                selectableminions.Add(minion);
            }
        }

        Debug.Log("selectiable minion count: " + selectableminions.Count);

        OnSelectingMinionForAttack?.Invoke(selectableminions);
        Debug.Log("target: " + target);
        if(target != null)
        {
            selectedMinion = target;
        }
        while (selectedMinion == null)
        {
            //Debug.Log("selecting minion for attack");

            yield return null;
        }
        Debug.Log("damaging minion");

        Vector3 dir = (selectedMinion.transform.position - transform.position).normalized;
        transform.DOPunchPosition(dir*0.2f, 0.5f, vibrato: 1).SetEase(Ease.InOutBack).SetDelay(0.5f);
        selectedMinion.selectable.SetSelectable(false);
        selectedMinion.TakeDamage(modal.attack);
        selectedMinion.transform.DOPunchPosition(dir * 0.03f, 0.15f, vibrato: 5).SetDelay(0.75f);
        float distance = (selectedMinion.transform.position - transform.position).magnitude;    
        if(distance < 2) //selectedMinion.modal.health > 0 && 
        {
            TakeDamage(selectedMinion.modal.attack);
            if (selectedMinion.modal.range < 2)
            {
                StartCoroutine(selectedMinion.animationController.PlaySlashAnimation(-dir, 0.5f));
            }
            else
            {
                StartCoroutine(selectedMinion.animationController.PlayArrowAnimation(-dir, transform.position, 0.65f, animationController.PlayArrowHitAnimation));
            }
        }

        if(modal.range < 2)
        {
            StartCoroutine(animationController.PlaySlashAnimation(dir, 0.5f));
        }
        else 
        {
            StartCoroutine(animationController.PlayArrowAnimation(dir, selectedMinion.transform.position, 0.65f, selectedMinion.animationController.PlayArrowHitAnimation));
        }
        LastTarget = selectedMinion;
        isAttackedThisTurn = true;
        selectedMinion = null;
        canAttack = false;

        // Clear the attack-target selection: every minion we lit up as a potential target
        // (not just the one we hit) must be de-highlighted and have its attackingMinion link
        // cleared once the attack resolves, otherwise they stay selectable after the attack.
        foreach (var minion in selectableminions)
        {
            if (minion == null) continue;
            minion.attackingMinion = null;
            minion.selectable.SetSelectable(false);
        }

        if(GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.curState = Player.State.Waiting;

        }

        GameManager.Instance.SetPlayerMinionsReadyToAttack();

        yield break;
    }
    public bool TakeDamage(int damage)
    {
        int effectiveDamage = Mathf.Max(damage - modal.armor, 0);
        modal.health -= effectiveDamage;

        DOVirtual.DelayedCall(0.75f, () => view.UpdateView(modal));
        Debug.Log("minion take damage: " + modal.name);

        if (effectiveDamage > 0)
        {
            Debug.Log("on took damage invoke");
            OnTookDamage?.Invoke(this, effectiveDamage);
        }

        if (modal.health <= 0)
        {
            Debug.Log("minion died: " + modal.name);
            Die();
            return true;
        }
        else
        {
            return false;
        }

    }

    protected virtual void Die()
    {
        OnDied?.Invoke(this);
        selectable.SetSelectable(false);
        gridEntity.RemoveFromGridCell();
        if (GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.minions.Remove(this);
        }
        else
        {
            GameManager.Instance.opponent.minions.Remove(this);
        }
        Invoke("PlayDeathAnimation", 1f);

        GridManager.Instance.GetCell(transform.position).cellObj.GetComponent<CellController>().AddToMinionsDiedHere(card, modal.isPlayerMinion);
    }

    protected virtual void PlayDeathAnimation()
    {
        //view.FadeOutArtImage(1.35f);
        animator.enabled = true;
        animator.speed = 3.34f;
        if((modal.range == 1  || modal.range == 1) && modal.defHealth < 5)
        {
            animator.Play("MeleeBronzeDeath");
        }
        else if ((modal.range == 1 || modal.range == 1) && modal.defHealth < 10)
        {
            animator.Play("MeleeSilverDeath");
        }
        else if ((modal.range == 1 || modal.range == 1) && modal.defHealth < 15)
        {
            animator.Play("MeleeGoldDeath");
        }
        else if (modal.range == 2 && modal.defHealth < 5)
        {
            animator.Play("RangedBronzeDeath");
        }
        else if (modal.range == 2 && modal.defHealth < 10)
        {
            animator.Play("RangedSilverDeath");
        }
        else if (modal.range == 2 && modal.defHealth < 15)
        {
            animator.Play("RangedGoldDeath");
        }
    }
    public void DestroySelf()
    {
        Destroy(gameObject);
    }
    public virtual void Move(Vector3Int pos)
    {
        gridEntity.WorldPos = pos;
        transform.DOMove(pos, 0.25f);
        plannedMoveDir = Vector3Int.zero;
        isMovementValidated = false;

        GridManager.Instance.InvokeGridChanged();
    }

    // True if the cell directly in front of this minion (toward the enemy side) is in-grid and empty.
    // Used by spawn-on-occupied-cell logic: a start cell is only valid if its occupant can be pushed.
    public bool CanBePushedForward()
    {
        Vector3Int dir = modal.isPlayerMinion ? Vector3Int.up : Vector3Int.down;
        Vector3Int target = Vector3Int.RoundToInt(transform.position) + dir;
        Vector2Int idx = GridManager.Instance.PosToGridIndex(target);
        if (GridManager.Instance.IsOutSideOfGrid(idx)) return false;
        return GridManager.Instance.GetCell(idx).obj == null;
    }

    // Move this minion one cell forward, bypassing age/canMove gating (Move() itself does no checks).
    public void PushForward()
    {
        Vector3Int dir = modal.isPlayerMinion ? Vector3Int.up : Vector3Int.down;
        Vector3Int target = Vector3Int.RoundToInt(transform.position) + dir;
        Move(target);
    }

    public virtual void FailedMove(Vector3 dir, MinionController  collidedEntity = null)
    {
        Debug.Log("failed move");
        transform.DOPunchPosition(dir/20, 0.25f, vibrato: 0).SetEase(Ease.OutCubic);
        plannedMoveDir = Vector3Int.zero;
        isMovementValidated = false;

        if (collidedEntity != null) {
            OnCollided?.Invoke(this, collidedEntity);
        }
    }
    public virtual MoveInfo CanMove(Vector3Int pos)
    {
        var moveInfo = new MoveInfo();

        moveInfo.CanMove = false;
        moveInfo.CollidedEntity = null;

        if(!modal.canMove) return moveInfo;

        if(age < 1)
        {
            Debug.Log("cant move because age: " + age);
            return moveInfo;
        }

        if (GridManager.Instance.IsOutSideOfGrid(GridManager.Instance.PosToGridIndex(pos)))
        {
            Debug.Log("-2: " + GridManager.Instance.PosToGridIndex(pos));

            plannedMoveDir = Vector3Int.zero;
            return moveInfo;
        }

        GameObject objectAtDest = GridManager.Instance.GetCell(GridManager.Instance.PosToGridIndex(pos)).obj;

        if (objectAtDest != null) {
            moveInfo.CollidedEntity = objectAtDest.GetComponent<MinionController>();
            return moveInfo;
        }

        moveInfo.CanMove= true;
        return moveInfo;
    }

    public struct MoveInfo
    {
        public bool CanMove;
        public MinionController CollidedEntity;
    }
}
