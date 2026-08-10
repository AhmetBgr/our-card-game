using UnityEngine;
public class HeroController : MinionController
{
    private bool _initialized;

    protected override void Start()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Seeds this hero's modal from its CardSO, at most once per hero.
    ///
    /// Both Start() and GameManager.SetupGame call it, because their order is NOT fixed: SetupGame is
    /// a coroutine started from GameManager.Start(), so its first segment runs inline during that
    /// Start(), and Unity's Start() order between GameManager and HeroController is undefined (no
    /// execution-order override exists). SetupGame stamps the standing self-modifiers of the hero's
    /// passives (the Summoner's -2 Attack) straight onto modal.attack, while Initialize() re-seeds
    /// modal from the CardSO — so if Initialize ran second it would silently wipe the stamp.
    /// Being idempotent makes whichever runs first win and turns the other into a no-op.
    /// </summary>
    public void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        Initialize(owner, owner == GameManager.Instance.player);
    }

    // No entrance animation for heroes — they're just there from the start. Overridden so the base
    // minion pop-in doesn't run on them.
    protected override void PlayAppearAnimation()
    {
        view.ShowHeroImmediately();
    }
    protected override void OnMouseEnter()
    {
        if (GameManager.Instance.currentState == GameState.EndGame)
            return;

        //GameManager.Instance.player.handManager.ShowInfoCard(card);

        if (SelectionManager.Instance.HasActiveMinionRequest)
        {
            // show weapon image

            // Light the death skull when hovering this hero as a lethal attack/spell target, exactly
            // like a minion. The base MinionController.OnMouseEnter does this inline; the hero overrides
            // OnMouseEnter, so mirror it here. Hiding is inherited via MinionController.OnMouseExit ->
            // HideDeathPreview.
            ShowDeathPreview();
        }
        else if (!DraggableItem.AnyCardDragging)
        {
            var index = modal.isPlayerMinion ? new Vector2Int(-1, -1) : new Vector2Int(-2, -2);
            MinionRangeHandler.Instance.ShowRange(index, modal.range, modal.isPlayerMinion);
            // Same as a minion's SeeRange hover: mark the enemy units standing inside this hero's range.
            // Hiding is inherited from MinionController.OnMouseExit.
            MinionRangeHandler.Instance.ShowTargetsInRange(this);
        }
    }

    // OnMouseDown and SetReadyToAttack are intentionally NOT overridden here:
    // the hero reuses MinionController's full attack flow (selection + canAttack
    // branch + StartAttack/Attack with counterattack and animations).
    /*public override bool CanAttack(Agent opponent)
    {
        return false;
    }

    public override IEnumerator Attack(Agent opponent, MinionController target = null)
    {
        yield break;
    }

    */
    // Hero is off-grid (lives on Agent.hero, not in a grid cell), so it must NOT run the
    // base minion death path, which removes the entity from a grid cell and records the dead
    // card into that cell's CellController (semantically wrong for a hero, and throws if the
    // cell has no cellObj). Game-over is detected separately by GameManager.CheckWinCondition()
    // in Update() via hero.modal.health <= 0.
    protected override void Die()
    {
        selectable.SetSelectable(false);
    }

    protected override void PlayDeathAnimation()
    {
    }
    public override void Move(Vector3Int pos)
    {

    }
    public override void FailedMove(Vector3 dir, MinionController collidedEntity = null)
    {

    }

    public override MoveInfo CanMove(Vector3Int pos)
    {
        return default;
    }
}
