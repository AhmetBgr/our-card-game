public enum ActionLogEventType
{
    MinionPlayed,
    MinionDied,
    CardPlayed,
}

public class ActionLogEntry
{
    public ActionLogEventType EventType;
    public string Message;
    public CardSO PreviewCard;
}
