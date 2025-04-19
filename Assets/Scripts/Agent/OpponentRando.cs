using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OpponentRando : Agent
{
    public override void UpdateAvailableActions()
    {
        availableActions.Clear();

        foreach (var item in minions)
        {
            if (item.CanAttack(GameManager.Instance.player))
            {
                List<MinionController> selectableTargets = new List<MinionController>();

                List<MinionController> targets = new List<MinionController>();
                targets.AddRange(GameManager.Instance.player.minions);
                targets.Add(GameManager.Instance.player.hero);
                foreach (var minion in targets)
                {
                    if ((minion.transform.position - item.transform.position).magnitude < item.modal.range + 1)
                    {
                        selectableTargets.Add(minion);
                    }
                }
                if(selectableTargets.Count > 0)
                {
                    availableActions.Add(item.Attack(GameManager.Instance.player, selectableTargets[UnityEngine.Random.Range(0, selectableTargets.Count)]));
                }
            }
        }

        foreach (var card in hand)
        {
            bool canPlay = false;
            Debug.Log("testing: ");
            StartCoroutine(card.CanPlay(this, result => 
            {
                canPlay = result;
                Debug.Log("canplay result: " + result);
            }));

            if (canPlay)
            {
                availableActions.Add(Play(card));
            }
        }
    }
    public IEnumerator Play(CardController card)
    {
        yield return StartCoroutine(GameManager.Instance.PlayCard(card, this));

    }

    public override IEnumerator PlayTurn()
    {
        UpdateHand();
        UpdateAvailableActions();

        while (availableActions.Count > 0)
        {
            UpdateHand();
            UpdateAvailableActions();

            if (availableActions.Count == 0)
                yield break;

            ActionHolder.OnWaitingCellSelect += SelectCell;
            ActionHolder.OnWaitingMinionSelect += SelectMinion;


            IEnumerator action = availableActions[UnityEngine.Random.Range(0, availableActions.Count)];

            yield return new WaitForSeconds(1f);

            Debug.LogWarning("Start action");

            yield return StartCoroutine(action);

            Debug.LogWarning("end of  action");

            ActionHolder.OnWaitingCellSelect -= SelectCell;
            ActionHolder.OnWaitingMinionSelect -= SelectMinion;

            yield return new WaitForSeconds(1);

        }



    }
    public void SelectMinion(List<MinionController> minions, CardTEst card)
    {

        //List<MinionController> friendlyMinions = new List<MinionController>();  

        //friendlyMinions = minions.Where(minion => !minion.modal.isPlayerMinion).ToList();
        if (minions.Count == 0)
        {
            StopAllCoroutines();
        }
        var filteredList  = new List<MinionController>();

        if (card.type == CardTEst.Type.Debuff)
        {
            filteredList = minions.Where(x => x.modal.isPlayerMinion).ToList();
        }
        else if (card.type == CardTEst.Type.Buff)
        {
            filteredList = minions.Where(x => !x.modal.isPlayerMinion).ToList();
        }
        else
        {
            filteredList = minions;
        }

        ActionHolder.selectedMinion = filteredList[UnityEngine.Random.Range(0, filteredList.Count)];
    }
    public void SelectCell(List<Transform> cells, CardTEst card)
    {
        if(cells.Count == 0)
        {
            StopAllCoroutines();

        }

        ActionHolder.selectedcell = cells[UnityEngine.Random.Range(0, cells.Count)];    
    }
}
