public enum ActionLogEventType
{
    MinionSummoned,
    MinionDied,
    CardPlayed,
    TurnEnded,
}

public class ActionLogEntry
{
    public ActionLogEventType EventType;
    public string Message;
    public CardSO PreviewCard;
    public bool IsPlayerOwned;

    // A spacer is a non-interactive divider row used to visually separate one turn's events from
    // the next. It carries no message or preview card.
    public bool IsSpacer;
}
