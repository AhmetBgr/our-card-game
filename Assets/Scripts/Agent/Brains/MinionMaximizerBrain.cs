using UnityEngine;

[CreateAssetMenu(fileName = "MinionMaximizerBrain", menuName = "AI/Brains/Minion Maximizer")]
public class MinionMaximizerBrain : AgentBrain
{
    [Header("Card Play")]
    public float minionCardBonus = 50f;
    public float spellPenalty = -10f;
    public float costUseBonus = 2f;

    [Header("Attack")]
    public float killPenalty = -100f;
    public float chipDamageBonus = 5f;
    public float heroDamageBonus = 30f;

    [Header("Cell Selection")]
    public float adjacentMinionBonus = 10f;
    public float harmfulAreaEnemyBonus = 15f;
    public float harmfulAreaFriendlyPenalty = -30f;
    public float beneficialAreaFriendlyBonus = 20f;
    public float beneficialAreaEnemyPenalty = -10f;

    [Header("Minion Selection — Survival")]
    public float survivableTargetBonus = 20f;
    public int survivalHealthThreshold = 1;

    [Header("Minion Selection — Intent-Aware")]
    public float beneficialOnFriendlyBonus = 30f;
    public float beneficialOnEnemyPenalty = -40f;
    public float harmfulOnEnemyBonus = 25f;
    public float harmfulOnFriendlyPenalty = -50f;
    public float lethalHarmfulPenalty = -200f;
    public float removeTargetPenalty = -200f;

    public override float ScorePlayCard(CardController card, Agent self)
    {
        bool isMinion = card.modal.health > 0;
        float baseScore = isMinion ? minionCardBonus : spellPenalty;
        return baseScore + card.modal.cost * costUseBonus;
    }

    public override float ScoreAttack(MinionController attacker, MinionController target, Agent self)
    {
        bool isHero = target == GameManager.Instance.player.hero;
        int effectiveDamage = Mathf.Max(attacker.modal.attack - target.modal.armor, 0);
        bool willKill = target.modal.health - effectiveDamage <= 0;

        float score = willKill ? killPenalty : chipDamageBonus;
        if (isHero) score += heroDamageBonus;
        return score;
    }

    public override float ScoreCellSelection(Transform cell, CardSO contextCard, Agent self)
    {
        if (GridManager.Instance == null) return 0f;

        var centerCell = GridManager.Instance.GetCell(cell.position);
        Vector2Int center = centerCell.index;

        int neighborMinions = 0;
        int enemyNeighbors = 0;
        int friendlyNeighbors = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var ni = new Vector2Int(center.x + dx, center.y + dy);
                if (GridManager.Instance.IsOutSideOfGrid(ni)) continue;
                var c = GridManager.Instance.GetCell(ni);
                if (c.obj == null) continue;

                neighborMinions++;
                var m = c.obj.GetComponent<MinionController>();
                if (m == null) continue;
                if (m.owner == self) friendlyNeighbors++;
                else enemyNeighbors++;
            }
        }

        CardSO.CardIntent intent = contextCard != null ? contextCard.aiIntent : CardSO.CardIntent.Neutral;
        if (intent == CardSO.CardIntent.Harmful)
            return enemyNeighbors * harmfulAreaEnemyBonus + friendlyNeighbors * harmfulAreaFriendlyPenalty;
        if (intent == CardSO.CardIntent.Beneficial)
            return friendlyNeighbors * beneficialAreaFriendlyBonus + enemyNeighbors * beneficialAreaEnemyPenalty;

        return neighborMinions * adjacentMinionBonus;
    }

    public override float ScoreMinionSelection(MinionController candidate, CardSO contextCard, Agent self)
    {
        float score = candidate.modal.health > survivalHealthThreshold ? survivableTargetBonus : 0f;

        if (contextCard == null) return score;

        bool isFriendly = candidate.owner == self;

        if (contextCard.aiRemovesTarget)
        {
            score += removeTargetPenalty;
            return score;
        }

        if (contextCard.aiIntent == CardSO.CardIntent.Beneficial)
        {
            score += isFriendly ? beneficialOnFriendlyBonus : beneficialOnEnemyPenalty;
        }
        else if (contextCard.aiIntent == CardSO.CardIntent.Harmful)
        {
            int estDamage = Mathf.Max(contextCard.aiEffectMagnitude - candidate.modal.armor, 0);
            bool willKill = candidate.modal.health <= estDamage;
            score += isFriendly ? harmfulOnFriendlyPenalty : harmfulOnEnemyBonus;
            if (willKill) score += lethalHarmfulPenalty;
        }

        return score;
    }
}
