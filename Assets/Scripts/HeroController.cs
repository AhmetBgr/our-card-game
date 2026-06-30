using UnityEngine;
public class HeroController : MinionController
{
    protected override void Start()
    {
        Initialize(owner, owner == GameManager.Instance.player);
    }

    protected override void PlayAppearAnimation()
    {
        view.PlayHeroAppearAnimation();
    }
    protected override void OnMouseEnter()
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
            var index = modal.isPlayerMinion ? new Vector2Int(-1, -1) : new Vector2Int(-2, -2);
            MinionRangeHandler.Instance.ShowRange(index, modal.range);
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
