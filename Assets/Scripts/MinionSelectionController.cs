using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinionSelectionController : MonoBehaviour
{
    public MinionController minionController;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    /*protected virtual void OnMouseDown()
    {


        if (GameManager.Instance.player.curState == Player.State.SelectingMinion)
        {
            ActionHolder.selectedMinion = minionController;
        }
        else if (GameManager.Instance.player.curState == Player.State.SelectingMinionForAttack)
        {
            minionController.attackingMinion.selectedMinion = minionController;
        }
        else if (minionController.canAttack)
        {
            foreach (var item in GameManager.Instance.player.minions)
            {
                item.selectable.SetSelectable(false);
            }
            GameManager.Instance.player.curState = Player.State.SelectingMinionForAttack;
            minionController.StartAttack(GameManager.Instance.opponent);
        }
    }*/
}
