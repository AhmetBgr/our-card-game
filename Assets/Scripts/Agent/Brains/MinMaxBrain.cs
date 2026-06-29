using UnityEngine;

[CreateAssetMenu(fileName = "MinMaxBrain", menuName = "AI/Brains/Min-Max Brain")]
public class MinMaxBrain : AgentBrain
{
    [Header("Card Play — Mana Efficiency")]
    public float manaEfficiencyWeight = 10f;

    [Header("Card Play — Board State")]
    public float minionTempoBonus    = 20f;
    public float perEnemyMinionBonus = 8f;
    public float removalSpellBonus   = 30f;
    public float buffSpellBonus      = 15f;
    public float perFriendlyForBuff  = 5f;

    [Header("Attack — Trade Evaluation")]
    public float favorableTradeBonus = 80f;
    public float cleanTradeBonus     = 40f;
    public float suicidePenalty      = -60f;
    public float tempoValueWeight    = 5f;
    public float targetHealthDanger  = 3f;

    [Header("Attack — Hero Targeting")]
    public float heroAttackBonus   = 50f;
    public float heroBiasWhenClear = 70f;

    [Header("Minion Selection — Harmful")]
    public float harmfulHighAttackWeight  = 4f;
    public float harmfulLethalBonus       = 35f;
    public float harmfulOnFriendlyPenalty = -100f;

    [Header("Minion Selection — Beneficial")]
    public float beneficialHighAttackWeight = 3f;
    public float beneficialCanAttackBonus   = 20f;
    public float beneficialOnEnemyPenalty   = -100f;

    [Header("Cell Selection")]
    public float aoeEnemyWeight          = 20f;
    public float aoeFriendlyPenalty      = -25f;
    public float beneficialFriendlyWeight = 15f;
    public float beneficialAttackWeight   = 2f;

    // Brain is always on the opponent side; player is always the enemy.
    private Agent Enemy => GameManager.Instance.player;

    public override float ScorePlayCard(CardController card, Agent self)
    {
        Agent enemy = Enemy;
        float score = 0f;

        float manaRatio = self.availibleMana > 0 ? (float)card.modal.cost / self.availibleMana : 0f;
        score += manaRatio * manaEfficiencyWeight * card.modal.cost;

        bool isMinion = card.modal.health > 0;

        if (isMinion)
        {
            score += minionTempoBonus;
            score += enemy.minions.Count * perEnemyMinionBonus;
        }
        else
        {
            CardSO.CardIntent intent = card.card != null ? card.card.aiIntent : CardSO.CardIntent.Neutral;
            bool isRemoval = card.card != null && card.card.aiRemovesTarget;

            if (intent == CardSO.CardIntent.Harmful || isRemoval)
            {
                float totalThreat = 0f;
                foreach (var m in enemy.minions) totalThreat += m.modal.attack;
                score += removalSpellBonus + totalThreat * 1.5f;
            }
            else if (intent == CardSO.CardIntent.Beneficial)
            {
                score += self.minions.Count == 0
                    ? -50f
                    : buffSpellBonus + self.minions.Count * perFriendlyForBuff;
            }
        }

        // Soft penalty for mana left unspent after this card.
        score -= (self.availibleMana - card.modal.cost) * 0.5f;

        return score;
    }

    public override float ScoreAttack(MinionController attacker, MinionController target, Agent self)
    {
        Agent enemy = Enemy;
        bool isHero = target == enemy.hero;

        int effectiveDmgDealt = Mathf.Max(attacker.modal.attack - target.modal.armor, 0);
        bool targetWillDie = (target.modal.health - effectiveDmgDealt) <= 0;

        float dist = (target.transform.position - attacker.transform.position).magnitude;
        bool meleeExposure = attacker.modal.range < 2 && dist < 2f;
        int effectiveCounter = meleeExposure ? Mathf.Max(target.modal.attack - attacker.modal.armor, 0) : 0;
        bool attackerWillDie = (attacker.modal.health - effectiveCounter) <= 0;

        float score;

        if (targetWillDie && !attackerWillDie)
        {
            // Best case: we kill, we survive.
            score = favorableTradeBonus + target.modal.attack * tempoValueWeight;
        }
        else if (targetWillDie && attackerWillDie)
        {
            // Mutual trade — good if their attack value exceeds ours.
            float tradeValue = target.modal.attack - attacker.modal.attack;
            score = cleanTradeBonus + tradeValue * tempoValueWeight;
        }
        else if (!targetWillDie && !attackerWillDie)
        {
            // Chip damage — prefer chipping high-attack threats.
            score = target.modal.attack * targetHealthDanger;
        }
        else
        {
            // Suicide — we die, they live.
            score = suicidePenalty;
        }

        if (isHero)
            score += enemy.minions.Count == 0 ? heroBiasWhenClear : heroAttackBonus;

        return score;
    }

    public override float ScoreMinionSelection(MinionController candidate, CardSO contextCard, Agent self)
    {
        if (contextCard == null) return 0f;

        bool isFriendly = candidate.owner == self;
        CardSO.CardIntent intent = contextCard.aiIntent;

        if (intent == CardSO.CardIntent.Harmful && isFriendly)
            return harmfulOnFriendlyPenalty;

        if (intent == CardSO.CardIntent.Beneficial && !isFriendly)
            return beneficialOnEnemyPenalty;

        float score = 0f;

        if (intent == CardSO.CardIntent.Harmful)
        {
            score += candidate.modal.attack * harmfulHighAttackWeight;

            if (contextCard.aiRemovesTarget)
            {
                score += harmfulLethalBonus;
            }
            else if (contextCard.aiEffectMagnitude > 0)
            {
                int effectiveDmg = Mathf.Max(contextCard.aiEffectMagnitude - candidate.modal.armor, 0);
                if (candidate.modal.health <= effectiveDmg)
                    score += harmfulLethalBonus;
            }
        }
        else if (intent == CardSO.CardIntent.Beneficial)
        {
            score += candidate.modal.attack * beneficialHighAttackWeight;

            bool canStillAttack = !candidate.isAttackedThisTurn && candidate.age > 0;
            if (canStillAttack)
                score += beneficialCanAttackBonus;
        }

        return score;
    }

    public override float ScoreCellSelection(Transform cell, CardSO contextCard, Agent self)
    {
        if (GridManager.Instance == null) return 0f;

        Cell centerCell = GridManager.Instance.GetCell(cell.position);
        Vector2Int center = centerCell.index;

        CardSO.CardIntent intent = contextCard != null ? contextCard.aiIntent : CardSO.CardIntent.Neutral;

        float score = 0f;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                Vector2Int ni = new Vector2Int(center.x + dx, center.y + dy);
                if (GridManager.Instance.IsOutSideOfGrid(ni)) continue;

                Cell neighbor = GridManager.Instance.GetCell(ni);
                if (neighbor.obj == null) continue;

                MinionController m = neighbor.obj.GetComponent<MinionController>();
                if (m == null) continue;

                bool isFriendly = m.owner == self;

                if (intent == CardSO.CardIntent.Harmful)
                {
                    score += isFriendly ? aoeFriendlyPenalty : aoeEnemyWeight;
                }
                else if (intent == CardSO.CardIntent.Beneficial)
                {
                    if (isFriendly)
                        score += beneficialFriendlyWeight + m.modal.attack * beneficialAttackWeight;
                    else
                        score -= beneficialFriendlyWeight;
                }
                else
                {
                    score += 5f;
                }
            }
        }

        return score;
    }
}
