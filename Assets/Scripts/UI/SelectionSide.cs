/// <summary>
/// Which side of the match a deck/hero selection belongs to. Threads through the selection UI
/// (DeckSelectionContext), the save data (SaveManager) and the Game scene (Agent.ApplySavedSelection)
/// so the player's and the opponent's choices stay independent.
/// </summary>
public enum SelectionSide
{
    Player,
    Opponent
}
