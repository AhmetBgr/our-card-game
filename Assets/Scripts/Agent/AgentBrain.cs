using System.Collections.Generic;
using UnityEngine;

public abstract class AgentBrain : ScriptableObject
{
    public abstract float ScorePlayCard(CardController card, Agent self);
    public abstract float ScoreAttack(MinionController attacker, MinionController target, Agent self);
    public abstract float ScoreCellSelection(Transform cell, CardSO contextCard, Agent self);
    public abstract float ScoreMinionSelection(MinionController candidate, CardSO contextCard, Agent self);
}
