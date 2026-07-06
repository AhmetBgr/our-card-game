public enum ActionLogEventType
{
    MinionSummoned,
    MinionDied,
    CardPlayed,
}

public class ActionLogEntry
{
    public ActionLogEventType EventType;
    public string Message;
    public CardSO PreviewCard;
    public bool IsPlayerOwned;
}
