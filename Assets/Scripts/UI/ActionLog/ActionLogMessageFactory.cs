public static class ActionLogMessageFactory
{
    private static string OwnerLabel(Agent owner)
    {
        return owner == GameManager.Instance.player ? "Player (You)" : "Opponent";
    }

    public static ActionLogEntry MinionPlayed(MinionController minion)
    {
        return new ActionLogEntry
        {
            EventType = ActionLogEventType.MinionPlayed,
            Message = $"{OwnerLabel(minion.owner)} played <color=yellow>{minion.card.cardName}</color>",
            PreviewCard = minion.card,
        };
    }

    public static ActionLogEntry MinionDied(MinionController minion)
    {
        return new ActionLogEntry
        {
            EventType = ActionLogEventType.MinionDied,
            Message = $"{OwnerLabel(minion.owner)}'s <color=yellow>{minion.card.cardName}</color> died",
            PreviewCard = minion.card,
        };
    }

    public static ActionLogEntry CardPlayed(Agent owner, CardSO card)
    {
        return new ActionLogEntry
        {
            EventType = ActionLogEventType.CardPlayed,
            Message = $"{OwnerLabel(owner)} played <color=yellow>{card.cardName}</color>",
            PreviewCard = card,
        };
    }
}
