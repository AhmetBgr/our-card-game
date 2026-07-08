public static class ActionLogMessageFactory
{
    private static bool IsPlayer(Agent owner)
    {
        return owner == GameManager.Instance.player;
    }

    private static string OwnerLabel(Agent owner)
    {
        return IsPlayer(owner) ? "P:" : "E:";
    }

    public static ActionLogEntry MinionSummoned(MinionController minion)
    {
        return new ActionLogEntry
        {
            EventType = ActionLogEventType.MinionSummoned,
            Message = $"{OwnerLabel(minion.owner)} summoned <color=yellow>{minion.card.cardName}</color>",
            PreviewCard = minion.card,
            IsPlayerOwned = IsPlayer(minion.owner),
        };
    }

    public static ActionLogEntry MinionDied(MinionController minion)
    {
        return new ActionLogEntry
        {
            EventType = ActionLogEventType.MinionDied,
            Message = $"{OwnerLabel(minion.owner)}'s <color=yellow>{minion.card.cardName}</color> died",
            PreviewCard = minion.card,
            IsPlayerOwned = IsPlayer(minion.owner),
        };
    }

    public static ActionLogEntry CardPlayed(Agent owner, CardSO card)
    {
        return new ActionLogEntry
        {
            EventType = ActionLogEventType.CardPlayed,
            Message = $"{OwnerLabel(owner)} played <color=yellow>{card.cardName}</color>",
            PreviewCard = card,
            IsPlayerOwned = IsPlayer(owner),
        };
    }

    public static ActionLogEntry TurnEndSpacer()
    {
        return new ActionLogEntry
        {
            EventType = ActionLogEventType.TurnEnded,
            IsSpacer = true,
        };
    }
}
