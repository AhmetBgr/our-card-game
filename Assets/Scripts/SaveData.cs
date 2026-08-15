using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public DeckData[] Decks;
    public DeckData[] OpponentDecks;
    public int HighScore;
    public int SelectedDeckIndex;
    public int SelectedOpponentDeckIndex;
    public int SelectedHeroIndex;
    public int SelectedOpponentHeroIndex;

    // The action log starts collapsed: `false` is both the fresh-save default and what saves written
    // before this field existed deserialize to, so old players also get it off until they open it.
    public bool ShowActionLog;

    // False until the tutorial match has been played through. `false` is both the fresh-save default
    // and what older saves deserialize to, so existing players get the tutorial once as well.
    public bool IsTutorial;
}

[Serializable]
public class DeckData
{
    public string Name;

    public List<string> Deck = new();
    public bool isLocked;
}

