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
    public void Initialize(Agent owner, bool isPlayerMinion)
    {
        modal.UpdateModal(card, owner, isPlayerMinion);
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

        // Clicking the attacker again backs out of its own in-progress attack selection.
        if (SelectionManager.Instance.ActiveAttacker == this)
        {
            SelectionManager.Instance.Cancel();
            return;
        }

        // An active selection (spell-target or attack-target) consumes the click; only when nothing
        // is being selected does a click on an attack-ready minion begin a fresh attack.
        if (SelectionManager.Instance.TryResolveClick(this))
            return;

        // Don't let a click start an attack while a card is resolving.
        if (canAttack && !GameManager.Instance.isPlayingCard)
        {
            StartAttack(GameManager.Instance.opponent);
        }
    }
    protected virtual void OnMouseEnter()
    {
        if (GameManager.Instance.currentState == GameState.EndGame)
            return;

        GameManager.Instance.player.handManager.ShowInfoCard(card);
        Debug.Log("shouldshow range");

        if (SelectionManager.Instance.HasActiveMinionRequest)
        {
            // show weapon image
        }
        else if (!DraggableItem.AnyCardDragging)
        {
            MinionRangeHandler.Instance.ShowRange(gridEntity.GetGridIndex(), modal.range);
        }
    }

    protected void OnMouseExit()
    {
        GameManager.Instance.player.handManager.HideInfoCard();
        MinionRangeHandler.Instance.HideRange();

    }

    private void OnDrawGizmosSelected()
    {
        int range = modal != null ? modal.range : (card != null ? card.range : 0);
        if (range <= 0) return;

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        Vector3 size = new Vector3(0.9f, 0.9f, 0.1f);
        foreach (var off in RangeUtility.RangeOffsets(range))
        {
            // grid offset (dx,dy) -> world offset (dx,-dy); cellSize is 1
            Vector3 cell = transform.position + new Vector3(off.x, -off.y, 0f);
            DrawThickWireCube(cell, size, 4f);
        }
#endif
    }

#if UNITY_EDITOR
    private static void DrawThickWireCube(Vector3 center, Vector3 size, float thickness)
    {
        Vector3 e = size * 0.5f;
        Vector3 a = center + new Vector3(-e.x, -e.y, -e.z);
        Vector3 b = center + new Vector3( e.x, -e.y, -e.z);
        Vector3 c = center + new Vector3( e.x,  e.y, -e.z);
        Vector3 d = center + new Vector3(-e.x,  e.y, -e.z);
        Vector3 a2 = center + new Vector3(-e.x, -e.y,  e.z);
        Vector3 b2 = center + new Vector3( e.x, -e.y,  e.z);
        Vector3 c2 = center + new Vector3( e.x,  e.y,  e.z);
        Vector3 d2 = center + new Vector3(-e.x,  e.y,  e.z);

        UnityEditor.Handles.DrawLine(a, b, thickness);
        UnityEditor.Handles.DrawLine(b, c, thickness);
        UnityEditor.Handles.DrawLine(c, d, thickness);
        UnityEditor.Handles.DrawLine(d, a, thickness);

        UnityEditor.Handles.DrawLine(a2, b2, thickness);
        UnityEditor.Handles.DrawLine(b2, c2, thickness);
        UnityEditor.Handles.DrawLine(c2, d2, thickness);
        UnityEditor.Handles.DrawLine(d2, a2, thickness);

        UnityEditor.Handles.DrawLine(a, a2, thickness);
        UnityEditor.Handles.DrawLine(b, b2, thickness);
        UnityEditor.Handles.DrawLine(c, c2, thickness);
        UnityEditor.Handles.DrawLine(d, d2, thickness);
    }
#endif

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
                if (RangeUtility.IsInRange(this, minion))
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
            if(RangeUtility.IsInRange(this, minion))
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
            if (RangeUtility.IsInRange(this, minion))
            {
                selectableminions.Add(minion);
            }
        }

        Debug.Log("selectiable minion count: " + selectableminions.Count);

        MinionController chosen = null;

        if (target != null)
        {
            // Triggered / AI attack: the target is already decided, so bypass the interactive
            // selection request entirely (no highlights, no wait).
            chosen = target;
        }
        else
        {
            if (selectableminions.Count == 0) yield break; // nothing in range — don't open a dangling selection

            // Interactive attack: the SelectionManager lights the valid targets and routes the click.
            SelectionManager.Instance.BeginAttackRequest(this, selectableminions, picked => chosen = picked);
            OnSelectingMinionForAttack?.Invoke(selectableminions);

            while (chosen == null && SelectionManager.Instance.HasActiveMinionRequest && !GameManager.Instance.isTesting)
            {
                yield return null;
            }

            if (chosen == null)
            {
                // Cancelled (right-click / attacker re-click / spell preempt / turn end).
                SelectionManager.Instance.Cancel();
                yield break;
            }
        }

        Debug.Log("damaging minion");

        Vector3 dir = (chosen.transform.position - transform.position).normalized;
        // Lunge plays 20% faster (duration / 1.2).
        transform.DOPunchPosition(dir*0.2f, 0.5f / 1.2f, vibrato: 1).SetEase(Ease.InOutBack).SetDelay(0.5f);
        chosen.TakeDamage(modal.attack);
        chosen.transform.DOPunchPosition(dir * 0.03f, 0.15f, vibrato: 5).SetDelay(0.75f);
        if (RangeUtility.IsInRange(chosen, this)) // target retaliates if attacker is in ITS range
        {
            TakeDamage(chosen.modal.attack);
            // Counter-attack is delayed an extra 0.25s so it reads as a response to the strike
            // rather than overlapping it.
            if (chosen.modal.range < 2)
            {
                StartCoroutine(chosen.animationController.PlaySlashAnimation(-dir, 0.75f));
            }
            else
            {
                StartCoroutine(chosen.animationController.PlayArrowAnimation(-dir, transform.position, 0.9f, animationController.PlayArrowHitAnimation));
            }
        }

        if(modal.range < 2)
        {
            StartCoroutine(animationController.PlaySlashAnimation(dir, 0.5f));
        }
        else
        {
            StartCoroutine(animationController.PlayArrowAnimation(dir, chosen.transform.position, 0.65f, chosen.animationController.PlayArrowHitAnimation));
        }
        LastTarget = chosen;
        isAttackedThisTurn = true;
        canAttack = false;

        // Tear down the selection (de-highlights every lit target) now that the attack resolved, then
        // restore resting attack-readiness for the remaining minions. State is set above first so this
        // minion is correctly recomputed as no-longer-attackable.
        SelectionManager.Instance.Complete();
        GameManager.Instance.SetPlayerMinionsReadyToAttack();

        yield break;
    }
    public bool TakeDamage(int damage)
    {
        int effectiveDamage = Mathf.Max(damage - modal.armor, 0);
        modal.health -= effectiveDamage;

        DOVirtual.DelayedCall(0.75f, () => view.UpdateView(modal));
        Debug.Log("minion take damage: " + modal.name);

        if (modal.health <= 0)
        {
            // Lethal hit: go straight to death and deliberately do NOT fire OnTookDamage. The minion is
            // dying, so its OnDeath trigger supersedes. Firing OnTookDamage first would wrap a took-damage
            // triggered-action scope around the death: the death gets deferred behind it, then flushed from
            // inside the took-damage scope's finally, and when that outer scope disposes its Restore()
            // clobbers the death trigger's freshly-set ActionHolder selection state with stale leftovers —
            // which made OnDeath effects (e.g. the seed's +1/+1) intermittently no-op.
            Debug.Log("minion died: " + modal.name);
            Die();
            return true;
        }

        if (effectiveDamage > 0)
        {
            Debug.Log("on took damage invoke");
            OnTookDamage?.Invoke(this, effectiveDamage);
        }

        return false;
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
        // Recompute attack-readiness once the move finishes: a changed position changes which targets
        // are in range (both for this minion and for any minion that could reach it). We hook OnComplete
        // because RangeUtility reads transform.position, which only reaches `pos` when the tween ends.
        transform.DOMove(pos, 0.25f).OnComplete(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.SetPlayerMinionsReadyToAttack();
        });
        plannedMoveDir = Vector3Int.zero;
        isMovementValidated = false;

        GridManager.Instance.InvokeGridChanged();
    }

    // True if the cell one step along `pushDir` is in-grid and empty. `pushDir` is the summoning
    // agent's forward direction (up for the player, down for the opponent), so any occupant — friendly
    // or enemy — can be pushed out of the way as long as the cell ahead of it is free.
    // Used by spawn-on-occupied-cell logic: a start cell is only valid if its occupant can be pushed.
    public bool CanBePushedForward(Vector3Int pushDir)
    {
        Vector3Int target = Vector3Int.RoundToInt(transform.position) + pushDir;
        Vector2Int idx = GridManager.Instance.PosToGridIndex(target);
        if (GridManager.Instance.IsOutSideOfGrid(idx)) return false;
        return GridManager.Instance.GetCell(idx).obj == null;
    }

    // Move this minion one cell along `pushDir`, bypassing age/canMove gating (Move() does no checks).
    public void PushForward(Vector3Int pushDir)
    {
        Vector3Int target = Vector3Int.RoundToInt(transform.position) + pushDir;
        Move(target);
    }

    // Swap world positions with another minion in one grid refresh so neither slot is briefly vacant.
    public void SwapPositionsWith(MinionController other)
    {
        Vector3 posA = gridEntity.WorldPos;
        Vector3 posB = other.gridEntity.WorldPos;

        gridEntity.WorldPos = posB;
        other.gridEntity.WorldPos = posA;

        transform.DOMove(posB, 0.25f).OnComplete(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.SetPlayerMinionsReadyToAttack();
        });
        other.transform.DOMove(posA, 0.25f);

        plannedMoveDir = Vector3Int.zero;
        other.plannedMoveDir = Vector3Int.zero;

        GridManager.Instance.InvokeGridChanged();
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
