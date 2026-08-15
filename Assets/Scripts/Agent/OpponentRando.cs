using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class OpponentRando : Agent
{
    public override IEnumerator UpdateAvailableActions()
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
                    if (RangeUtility.IsInRange(item, minion))
                    {
                        selectableTargets.Add(minion);
                    }
                }
                if (selectableTargets.Count > 0)
                {
                    availableActions.Add(item.Attack(GameManager.Instance.player, selectableTargets[UnityEngine.Random.Range(0, selectableTargets.Count)]));
                }
            }
        }
        yield return null;
        foreach (var card in hand)
        {
            bool canPlay = false;
            yield return StartCoroutine(card.CanPlay(this, result => {

                canPlay = result;


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
        Debug.Log("opponent should play card");
        yield return StartCoroutine(GameManager.Instance.PlayCard(card, this));

    }

    public override IEnumerator PlayTurn()
    {
        UpdateHand();
        yield return StartCoroutine(UpdateAvailableActions());

        while (availableActions.Count > 0)
        {
            UpdateHand();
            yield return StartCoroutine(UpdateAvailableActions());

            if (availableActions.Count == 0)
                yield break;

            SubscribeSelectionHandlers();

            // The unsubscribe MUST run no matter how the action ends — a leaked handler would auto-resolve
            // the PLAYER's cell/minion picks on their turn (summoning with no prompt). A finally guarantees
            // it even if the action throws; the empty-target case cancels the current card gracefully
            // (see SelectCell/SelectMinion) instead of stopping this coroutine, which would skip the finally.
            //
            // It does NOT cover the scene going away underneath us — see OpponentBrained for the full
            // reasoning; OnDestroy below is what covers a match restarted mid-AI-turn.
            try
            {
                IEnumerator action = availableActions[UnityEngine.Random.Range(0, availableActions.Count)];

                yield return new WaitForSeconds(1f);

                Debug.LogWarning("Start action");

                yield return StartCoroutine(action);

                Debug.LogWarning("end of  action");
            }
            finally
            {
                UnsubscribeSelectionHandlers();
            }

            if (GameManager.Instance.currentState == GameState.EndGame)
                break;

            yield return new WaitForSeconds(1);

        }
    }

    private void SubscribeSelectionHandlers()
    {
        // Unsubscribe first so a re-entry can't stack a second copy of the same handler.
        UnsubscribeSelectionHandlers();

        ActionHolder.OnWaitingCellSelect += SelectCell;
        ActionHolder.OnWaitingMinionSelect += SelectMinion;
        ActionHolder.OnWaitingCardChoice += ChooseCard;
    }

    private void UnsubscribeSelectionHandlers()
    {
        // Idempotent: -= on a delegate that isn't subscribed is a no-op, so this is safe to call from
        // both the finally above and OnDestroy, whichever gets there first (or only).
        ActionHolder.OnWaitingCellSelect -= SelectCell;
        ActionHolder.OnWaitingMinionSelect -= SelectMinion;
        ActionHolder.OnWaitingCardChoice -= ChooseCard;
    }

    /// <summary>
    /// The teardown path the coroutine's finally cannot reach. Runs on scene unload, so a match restarted
    /// mid-AI-turn cannot carry these handlers into the next one and auto-resolve the player's picks.
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        UnsubscribeSelectionHandlers();
    }
    public void SelectMinion(List<MinionController> minions, CardSO card)
    {

        //List<MinionController> friendlyMinions = new List<MinionController>();  

        //friendlyMinions = minions.Where(minion => !minion.modal.isPlayerMinion).ToList();
        if (minions.Count == 0)
        {
            // No valid target: cancel THIS card's resolution gracefully. Never StopAllCoroutines here —
            // that kills PlayTurn mid-turn and leaks our event subscription onto the player's selections.
            ActionHolder.cancelRequested = true;
            return;
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

        ActionHolder.selectedMinion = filteredList.Count == 0 ? null : filteredList[UnityEngine.Random.Range(0, filteredList.Count)];
    }
    // Answers a Discover prompt. This brain plays at random by design, so it takes a random option —
    // the CardChoice panel never opens on the AI's turn, so this write IS the pick.
    public void ChooseCard(List<CardSO> options, CardSO card)
    {
        if (options == null || options.Count == 0)
        {
            // Nothing to choose: cancel THIS card's resolution gracefully, as SelectCell/SelectMinion do.
            ActionHolder.cancelRequested = true;
            return;
        }

        ActionHolder.chosenCard = options[UnityEngine.Random.Range(0, options.Count)];
    }
    public void SelectCell(List<Transform> cells, CardSO card)
    {
        if(cells.Count == 0)
        {
            // No valid cell: cancel THIS card's resolution gracefully. Never StopAllCoroutines here —
            // that kills PlayTurn mid-turn and leaks our event subscription onto the player's selections.
            ActionHolder.cancelRequested = true;
            return;
        }

        ActionHolder.selectedcell = cells[UnityEngine.Random.Range(0, cells.Count)];
    }

    public override bool IsPlayer()
    {
        return false;
    }
}
