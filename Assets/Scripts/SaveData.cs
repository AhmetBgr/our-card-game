using System;

[Serializable]
public class SaveData 
{
    public DeckData[] Decks;
    public int HighScore;
    public int SelectedDeckIndex;
}

[Serializable]
public class DeckData
{
    public string Name;

    public string[] Deck;
    public bool isLocked;
}

