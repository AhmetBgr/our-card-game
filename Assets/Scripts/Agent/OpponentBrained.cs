using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        //yield return new WaitForSeconds(0.5f);
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

            // Decided before subscribing, and deliberately so: PickBestAction has no side effects and
            // needs no selection handlers, and when it declines we can leave without ever having attached
            // them. Passing ends the turn — there is nothing left to reconsider, since the board only
            // changes from here if WE change it.
            IEnumerator action = PickBestAction();
            if (action == null)
                yield break;

            ActionHolder.OnWaitingCellSelect += SelectCell;
            ActionHolder.OnWaitingMinionSelect += SelectMinion;
            ActionHolder.OnWaitingCardChoice += ChooseCard;

            // The unsubscribe MUST run no matter how the action ends — a leaked handler would auto-resolve
            // the PLAYER's cell/minion picks on their turn (summoning with no prompt). A finally guarantees
            // it even if the action throws; the empty-target case cancels the current card gracefully
            // (see SelectCell/SelectMinion) instead of stopping this coroutine, which would skip the finally.
            try
            {
                yield return new WaitForSeconds(1f);

                Debug.LogWarning("Start action");
                yield return StartCoroutine(action);
                Debug.LogWarning("end of  action");

                // An action (especially an attack) can spawn triggered actions — e.g. a minion that kills
                // itself on the counter-attack fires its OnDeath a frame or two later. Those run as separate
                // coroutines over the shared ActionHolder selection globals, so we must let them fully drain
                // before issuing the next action; otherwise the next play/turn-end clobbers their state and
                // the trigger's effect (the seed's +1/+1 buff) silently does nothing.
                while (GameManager.Instance.HasInFlightTriggeredActions)
                    yield return null;
            }
            finally
            {
                ActionHolder.OnWaitingCellSelect -= SelectCell;
                ActionHolder.OnWaitingMinionSelect -= SelectMinion;
                ActionHolder.OnWaitingCardChoice -= ChooseCard;
            }

            if (GameManager.Instance.currentState == GameState.EndGame)
                break;

            yield return new WaitForSeconds(1);
        }
    }

    /// <summary>
    /// The best available action, or null when none of them is worth taking.
    ///
    /// Returning null is the point. This used to seed the argmax with index 0 and only ever swap for a
    /// strictly better score, so *something* was always executed however badly it scored: the AI emptied
    /// its hand every turn, cast buffs into an empty board, and threw minions into lethal counter-attacks
    /// purely because those were the only entries in the list. Doing nothing was never on the menu.
    ///
    /// Now the winner still has to clear the brain's <see cref="AgentBrain.MinimumActionScore"/>. Brains
    /// that don't set one keep the old always-act behaviour, since the default bar is negative infinity.
    /// </summary>
    private IEnumerator PickBestAction()
    {
        if (availableActions.Count == 0) return null;

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

        float bar = brain != null ? brain.MinimumActionScore : float.NegativeInfinity;
        if (bestScore < bar)
        {
            Debug.Log($"[AI] Passing: best of {availableActions.Count} available actions scores " +
                      $"{bestScore:0.#}, under the {bar:0.#} bar — nothing here is worth doing.");
            return null;
        }

        return availableActions[bestIndex];
    }

    // Answers a Discover prompt. The options are whole cards rather than board entities, so the brain's
    // target scorers don't apply; cost is the one comparable the pool guarantees, and every option has
    // already been filtered to "can resolve on this board" by the verb. Ties break at random so repeated
    // Discovers off the same pool don't always land on the same card.
    public void ChooseCard(List<CardSO> options, CardSO card)
    {
        if (options == null || options.Count == 0)
        {
            // Nothing to choose: cancel THIS card's resolution gracefully, as SelectCell/SelectMinion do.
            ActionHolder.cancelRequested = true;
            return;
        }

        int bestCost = options.Max(o => o != null ? o.cost : int.MinValue);
        var tied = options.Where(o => o != null && o.cost == bestCost).ToList();
        if (tied.Count == 0) tied = options;

        ActionHolder.chosenCard = tied[UnityEngine.Random.Range(0, tied.Count)];
    }

    public void SelectMinion(List<MinionController> minions, CardSO card)
    {
        if (minions.Count == 0)
        {
            // No valid target: cancel THIS card's resolution gracefully. Never StopAllCoroutines here —
            // that kills PlayTurn mid-turn and leaks our event subscription onto the player's selections.
            ActionHolder.cancelRequested = true;
            return;
        }

        List<MinionController> filtered;
        if (card.type == CardSO.Type.Debuff)
            filtered = minions.Where(x => x.modal.isPlayerMinion).ToList();
        else if (card.type == CardSO.Type.Buff)
            filtered = minions.Where(x => !x.modal.isPlayerMinion).ToList();
        else if (card.aiIntent == CardSO.CardIntent.Beneficial)
            filtered = minions.Where(x => !x.modal.isPlayerMinion).ToList();
        else if (card.aiIntent == CardSO.CardIntent.Harmful)
            filtered = minions.Where(x => x.modal.isPlayerMinion).ToList();
        else
            filtered = minions;

        if (filtered.Count == 0)
        {
            // No target on the right side exists — e.g. a damage spell whose only reachable minions are
            // ours. We are already committed to the play, so fall through to the scored pick and take the
            // least-bad victim rather than whichever minion happened to be first in the list. (The brain
            // is expected to refuse these plays outright via ScorePlayCard; this is the leftover case
            // where enemy minions exist but none of them is selectable.)
            filtered = minions;
        }

        // Exclude minions already committed as targets (e.g. first pick in a two-pick swap spell).
        var candidates = filtered.Where(m => !ActionHolder.selectedTargetMinions.Contains(m)).ToList();
        if (candidates.Count == 0) candidates = filtered;

        MinionController best = candidates[0];
        float bestScore = brain != null ? brain.ScoreMinionSelection(candidates[0], card, this) : 0f;
        for (int i = 1; i < candidates.Count; i++)
        {
            float s = brain != null ? brain.ScoreMinionSelection(candidates[i], card, this) : 0f;
            if (s > bestScore)
            {
                bestScore = s;
                best = candidates[i];
            }
        }

        ActionHolder.selectedMinion = best;
    }

    public void SelectCell(List<Transform> cells, CardSO card)
    {
        if (cells.Count == 0)
        {
            // No valid cell: cancel THIS card's resolution gracefully. Never StopAllCoroutines here —
            // that kills PlayTurn mid-turn and leaks our event subscription onto the player's selections.
            ActionHolder.cancelRequested = true;
            return;
        }

        // What this card actually covers if aimed at a given cell — an area spell is only as good as the
        // minions inside its shape, and the shape is not derivable from the card alone. Null if the verb
        // driving this selection didn't publish one; the brain then falls back to the cell itself.
        var footprint = ActionHolder.currentCellFootprint;

        // Single pass: score once per cell, keeping every cell tied at the best score so far. Scoring
        // used to run three times over the list, which an area footprint makes meaningfully expensive.
        List<Transform> tied = new List<Transform>();
        float bestScore = float.NegativeInfinity;

        foreach (var c in cells)
        {
            float s = brain != null ? brain.ScoreCellSelection(c, card, this, footprint) : 0f;

            if (s > bestScore)
            {
                bestScore = s;
                tied.Clear();
                tied.Add(c);
            }
            else if (s == bestScore)
            {
                tied.Add(c);
            }
        }

        // Ties break at random so repeated casts don't always land on the same cell.
        ActionHolder.selectedcell = tied[UnityEngine.Random.Range(0, tied.Count)];
    }

    public override bool IsPlayer() => false;
}
