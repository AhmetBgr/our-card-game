using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AgentBrain : ScriptableObject
{
    /// <summary>
    /// The score an action has to reach before the agent is willing to take it. When nothing available
    /// clears the bar the agent passes instead of being forced into its least-bad move.
    ///
    /// Defaults to negative infinity — always act — so brains that deliberately score everything negative
    /// keep behaving exactly as they did (MinionMaximizerBrain penalises kills on purpose; a bar of 0
    /// would make it sit still all game). Override it in brains whose scores are calibrated around zero.
    /// </summary>
    public virtual float MinimumActionScore => float.NegativeInfinity;

    public abstract float ScorePlayCard(CardController card, Agent self);
    public abstract float ScoreAttack(MinionController attacker, MinionController target, Agent self);
    /// <summary>
    /// Scores <paramref name="cell"/> as the target of the cell selection now open.
    ///
    /// <paramref name="footprint"/> maps a chosen cell index to every index the card would actually
    /// affect (see CellFootprints), so an area spell can be judged on what it really covers instead of
    /// an assumed shape. It may be null when the caller doesn't know the shape — score the chosen cell
    /// alone in that case rather than guessing at a wider area.
    /// </summary>
    public abstract float ScoreCellSelection(Transform cell, CardSO contextCard, Agent self,
                                             Func<Vector2Int, IEnumerable<Vector2Int>> footprint);
    public abstract float ScoreMinionSelection(MinionController candidate, CardSO contextCard, Agent self);
}
