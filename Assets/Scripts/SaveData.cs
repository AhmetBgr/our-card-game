using System;
using System.Collections.Generic;

[Serializable]
public class SaveData 
{
    public DeckData[] Decks;
    public int HighScore;
    public int SelectedDeckIndex;
    public int SelectedHeroIndex;
}

[Serializable]
public class DeckData
{
    public string Name;

    public List<string> Deck = new();
    public bool isLocked;
}

