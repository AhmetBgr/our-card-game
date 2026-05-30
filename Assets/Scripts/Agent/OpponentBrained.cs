using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OpponentBrained : Agent
{
    [SerializeField] private AgentBrain brain;

    private readonly List<float> actionScores = new List<float>();

    public override IEnumerator UpdateAvailableActions()
    {
        availableActions.Clear();
        actionScores.Clear();

        foreach (var item in minions)
        {
            if (!item.CanAttack(GameManager.Instance.player)) continue;

            List<MinionController> targets = new List<MinionController>();
            targets.AddRange(GameManager.Instance.player.minions);
            targets.Add(GameManager.Instance.player.hero);

            foreach (var target in targets)
            {
                if (RangeUtility.IsInRange(item, target))
                {
                    availableActions.Add(item.Attack(GameManager.Instance.player, target));
                    actionScores.Add(brain != null ? brain.ScoreAttack(item, target, this) : 0f);
                }
            }
        }

        yield return null;

        foreach (var card in hand)
        {
            bool canPlay = false;
            yield return StartCoroutine(card.CanPlay(this, result => { canPlay = result; }));

            if (canPlay)
            {
                availableActions.Add(Play(card));
                actionScores.Add(brain != null ? brain.ScorePlayCard(card, this) : 0f);
            }
        }
    }

    public IEnumerator Play(CardController card)
    {
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

            ActionHolder.OnWaitingCellSelect += SelectCell;
            ActionHolder.OnWaitingMinionSelect += SelectMinion;

            IEnumerator action = PickBestAction();

            yield return new WaitForSeconds(1f);

            Debug.LogWarning("Start action");
            yield return StartCoroutine(action);
            Debug.LogWarning("end of  action");

            ActionHolder.OnWaitingCellSelect -= SelectCell;
            ActionHolder.OnWaitingMinionSelect -= SelectMinion;

            if (GameManager.Instance.currentState == GameState.EndGame)
                break;

            yield return new WaitForSeconds(1);
        }
    }

    private IEnumerator PickBestAction()
    {
        int bestIndex = 0;
        float bestScore = actionScores.Count > 0 ? actionScores[0] : 0f;
        for (int i = 1; i < availableActions.Count; i++)
        {
            float s = i < actionScores.Count ? actionScores[i] : 0f;
            if (s > bestScore)
            {
                bestScore = s;
                bestIndex = i;
            }
        }
        return availableActions[bestIndex];
    }

    public void SelectMinion(List<MinionController> minions, CardSO card)
    {
        if (minions.Count == 0)
        {
            StopAllCoroutines();
            return;
        }

        List<MinionController> filtered;
        if (card.type == CardSO.Type.Debuff)
            filtered = minions.Where(x => x.modal.isPlayerMinion).ToList();
        else if (card.type == CardSO.Type.Buff)
            filtered = minions.Where(x => !x.modal.isPlayerMinion).ToList();
        else
            filtered = minions;

        if (filtered.Count == 0)
        {
            ActionHolder.selectedMinion = minions[0];
            return;
        }

        MinionController best = filtered[0];
        float bestScore = brain != null ? brain.ScoreMinionSelection(filtered[0], card, this) : 0f;
        for (int i = 1; i < filtered.Count; i++)
        {
            float s = brain != null ? brain.ScoreMinionSelection(filtered[i], card, this) : 0f;
            if (s > bestScore)
            {
                bestScore = s;
                best = filtered[i];
            }
        }

        ActionHolder.selectedMinion = best;
    }

    public void SelectCell(List<Transform> cells, CardSO card)
    {
        if (cells.Count == 0)
        {
            StopAllCoroutines();
            return;
        }

        Transform best = cells[0];
        float bestScore = brain != null ? brain.ScoreCellSelection(cells[0], card, this) : 0f;
        for (int i = 1; i < cells.Count; i++)
        {
            float s = brain != null ? brain.ScoreCellSelection(cells[i], card, this) : 0f;
            if (s > bestScore)
            {
                bestScore = s;
                best = cells[i];
            }
        }

        ActionHolder.selectedcell = best;
    }

    public override bool IsPlayer() => false;
}
