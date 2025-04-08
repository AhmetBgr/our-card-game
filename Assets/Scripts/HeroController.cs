using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using static UnityEditor.PlayerSettings;
using static UnityEditor.Progress;
public class HeroController : MinionController
{
    protected override void OnMouseDown()
    {
        Debug.Log("here12");
        if (GameManager.Instance.player.curState == Player.State.SelectingMinion)
        {
            Debug.Log("here2");

            ActionHolder.selectedMinion = this;
        }
        else if (GameManager.Instance.player.curState == Player.State.SelectingMinionForAttack)
        {
            Debug.Log("here3");

            attackingMinion.selectedMinion = this;
        }
    }

    public override void SetReadyToAttack()
    {

    }
    public override bool CanAttack(Agent opponent)
    {
        return false;
    }

    public override IEnumerator Attack(Agent opponent, MinionController target = null)
    {
        yield break;
    }


    protected override void PlayDeathAnimation()
    {
    }
    public override void Move(Vector3Int pos)
    {

    }
    public override void FailedMove(Vector3 dir)
    {

    }

    public override bool CanMove(Vector3Int pos)
    {
        return false;
    }
}
