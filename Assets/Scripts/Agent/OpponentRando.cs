using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DG.Tweening;
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
        /*cardHandLayout.RemoveCard(card.transform);
        //Destroy(card.gameObject);

        card.transform.SetParent(cardHandLayout.transform.parent);
        card.transform.SetSiblingIndex(cardHandLayout.transform.parent.childCount - 1);
        card.transform.localRotation = Quaternion.identity;

        card.transform.DOScale(Vector3.one * 1.5f, 0.5f);
        card.transform.DORotate(Vector3.up * 90, 0.15f).OnComplete(() =>
        {
            card.modal.isPlayerMinion = true;
            card.view.UpdateView(card.modal);
            card.transform.DORotate(Vector3.up * 0, 0.15f);
        });
        card.transform.DOMove(PlayArea.Instance.opponentCardPos.position, 0.5f).OnComplete(() =>
        {
            card.transform.DOScale(0f, 0.25f).SetDelay(1f).OnComplete(() =>
            {
                if (card.modal.upgradedVerdion != null)
                {
                    SpawnCardToDeck(card.modal.upgradedVerdion, true);
                }
                Destroy(card.gameObject);
            });
        });
        yield return new WaitForSeconds(5);

        card.modal.isPlayerMinion = false;*/

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
    public void SelectMinion(List<MinionController> minions, CardSO card)
    {

        //List<MinionController> friendlyMinions = new List<MinionController>();  

        //friendlyMinions = minions.Where(minion => !minion.modal.isPlayerMinion).ToList();
        if (minions.Count == 0)
        {
            StopAllCoroutines();
        }
        var filteredList  = new List<MinionController>();

        if (card.type == CardSO.Type.Debuff)
        {
            filteredList = minions.Where(x => x.modal.isPlayerMinion).ToList();
        }
        else if (card.type == CardSO.Type.Buff)
        {
            filteredList = minions.Where(x => !x.modal.isPlayerMinion).ToList();
        }
        else
        {
            filteredList = minions;
        }

        ActionHolder.selectedMinion = filteredList[UnityEngine.Random.Range(0, filteredList.Count)];
    }
    public void SelectCell(List<Transform> cells, CardSO card)
    {
        if(cells.Count == 0)
        {
            StopAllCoroutines();

        }

        ActionHolder.selectedcell = cells[UnityEngine.Random.Range(0, cells.Count)];    
    }
}
