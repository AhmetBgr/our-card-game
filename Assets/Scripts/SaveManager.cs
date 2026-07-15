using System.Collections.Generic;
using UnityEngine;

public class SaveManager : PermanentSingleton<SaveManager>
{
    public const int PlayerDeckIndex = 0;
    public const int MysteryDeckIndex = 1;

    public const int DeckSlotCount = 10;

    public string saveDataKey = "DeckData";

    public SaveData saveData;
    public DeckSO defaultDeck;
    [Tooltip("Seeds the opponent's default deck slot, so a fresh save starts the AI on its authored deck.")]
    public DeckSO defaultOpponentDeck;
    public int DeckSize = 10;

    // Every deck operation is addressed by side, so the player's and the opponent's ten slots stay
    // independent. The `side` parameter defaults to Player throughout, keeping single-side callers simple.
    public DeckData[] GetDecks(SelectionSide side) =>
        side == SelectionSide.Opponent ? saveData.OpponentDecks : saveData.Decks;

    public int GetSelectedDeckIndex(SelectionSide side) =>
        side == SelectionSide.Opponent ? saveData.SelectedOpponentDeckIndex : saveData.SelectedDeckIndex;

    public void SetSelectedDeckIndex(SelectionSide side, int value)
    {
        if (side == SelectionSide.Opponent)
            saveData.SelectedOpponentDeckIndex = value;
        else
            saveData.SelectedDeckIndex = value;
    }

    public int GetSelectedHeroIndex(SelectionSide side) =>
        side == SelectionSide.Opponent ? saveData.SelectedOpponentHeroIndex : saveData.SelectedHeroIndex;

    public void SetSelectedHeroIndex(SelectionSide side, int value)
    {
        if (side == SelectionSide.Opponent)
            saveData.SelectedOpponentHeroIndex = value;
        else
            saveData.SelectedHeroIndex = value;
    }

    protected override void Awake()
    {
        base.Awake();
        // load
        LoadData();
    }

    public void LoadData()
    {
        // Load data from PlayerPrefs or other storage
        //string deckData = PlayerPrefs.GetString(saveDataKey, string.Empty);
        string json = PlayerPrefs.GetString(saveDataKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            // Deserialize and load the deck data
            Debug.Log($"Loaded deck data: {json}");
            saveData = JsonUtility.FromJson<SaveData>(json);
            EnsureSaveDataIsValid();
        }

        if (saveData == null || saveData.Decks == null || saveData.Decks.Length == 0)
        {
            Debug.Log("No deck data found.");
            CreateNewSave();
            SaveData();
        }
    }

    public void SaveData()
    {
        var value = SerializeData();

        PlayerPrefs.SetString(saveDataKey, value);
        PlayerPrefs.Save();
    }

    public int HighScore => saveData != null ? saveData.HighScore : 0;

    // Records `score` as the new best if it beats the stored one, persisting immediately (a match ends
    // with Replay/Exit, both of which reload a scene, so we can't wait for OnApplicationQuit).
    // Returns true only when a new record was written.
    public bool TrySetHighScore(int score)
    {
        if (saveData == null || score <= saveData.HighScore) return false;

        saveData.HighScore = score;
        SaveData();
        return true;
    }
    public string GetSaveData()
    {
        // Retrieve deck data from PlayerPrefs or other storage
        string deckData = PlayerPrefs.GetString(saveDataKey, string.Empty);
        return deckData;
    }
    public void RemoveCard(string cardName, int deckIndex, SelectionSide side = SelectionSide.Player)
    {
        if (!IsValidDeckIndex(deckIndex, side))
            return;

        var decks = GetDecks(side);

        if (!decks[deckIndex].Deck.Contains(cardName)) return;

        decks[deckIndex].Deck.Remove(cardName);
        SaveData();
    }

    public bool AddCard(string cardName, int deckIndex, SelectionSide side = SelectionSide.Player)
    {
        if (!IsValidDeckIndex(deckIndex, side))
            return false;

        var decks = GetDecks(side);

        if (decks[deckIndex].Deck.Contains(cardName)) return false;

        if (decks[deckIndex].Deck.Count >= DeckSize) return false;

        decks[deckIndex].Deck.Add(cardName);

        SaveData();

        return true;
    }

    public void GenerateRandomDeck(int deckIndex, SelectionSide side = SelectionSide.Player)
    {
        if (!IsValidDeckIndex(deckIndex, side))
            return;

        var pool = new List<CardSO>(DeckDatabase.Instance.AllCards);
        pool.RemoveAll(c => c.isUpgraded);

        // Shuffle so picks within each mana-curve tier are random.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            var temp = pool[i];
            pool[i] = pool[randomIndex];
            pool[randomIndex] = temp;
        }

        var deck = GetDecks(side)[deckIndex].Deck;
        deck.Clear();

        foreach (var tier in DeckSO.ManaCurve)
        {
            int taken = 0;
            for (int i = pool.Count - 1; i >= 0 && taken < tier.count; i--)
            {
                if (System.Array.IndexOf(tier.costs, pool[i].cost) < 0)
                    continue;

                deck.Add(pool[i].cardName);
                pool.RemoveAt(i);
                taken++;
            }
        }

        // Pad with whatever is left if a tier couldn't be fully satisfied.
        while (deck.Count < DeckSize && pool.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, pool.Count);
            deck.Add(pool[randomIndex].cardName);
            pool.RemoveAt(randomIndex);
        }

        SaveData();
    }

    bool IsValidDeckIndex(int index, SelectionSide side = SelectionSide.Player)
    {
        if (saveData == null)
            return false;

        var decks = GetDecks(side);
        if (decks == null)
            return false;

        if (index < 0 || index >= decks.Length)
        {
            Debug.LogWarning($"Invalid {side} deck index: {index}");
            return false;
        }

        return true;
    }
    public string SerializeData()
    {
        // todo: serialize saveData to json
        string value = JsonUtility.ToJson(saveData);
        return value;
    }

    void EnsureSaveDataIsValid()
    {
        if (saveData == null)
            return;

        if (saveData.Decks == null)
            saveData.Decks = new DeckData[0];

        // Saves written before the opponent gained its own deck slots deserialize OpponentDecks as
        // null, so build it here rather than treating the save as corrupt and wiping the player's decks.
        if (saveData.OpponentDecks == null || saveData.OpponentDecks.Length == 0)
            saveData.OpponentDecks = BuildDeckSlots(defaultOpponentDeck);

        saveData.SelectedDeckIndex = Mathf.Clamp(saveData.SelectedDeckIndex, 0, Mathf.Max(0, saveData.Decks.Length - 1));
        saveData.SelectedOpponentDeckIndex = Mathf.Clamp(saveData.SelectedOpponentDeckIndex, 0, Mathf.Max(0, saveData.OpponentDecks.Length - 1));

        // Keep the RandomHero sentinel (-1); otherwise floor at 0. The upper bound is clamped at
        // read time by HeroDatabase.GetHeroByIndex, so this stays independent of load order.
        if (saveData.SelectedHeroIndex < HeroDatabase.RandomHeroIndex)
            saveData.SelectedHeroIndex = 0;

        if (saveData.SelectedOpponentHeroIndex < HeroDatabase.RandomHeroIndex)
            saveData.SelectedOpponentHeroIndex = 0;

        EnsureDecksAreValid(saveData.Decks);
        EnsureDecksAreValid(saveData.OpponentDecks);
    }

    static void EnsureDecksAreValid(DeckData[] decks)
    {
        foreach (var deck in decks)
        {
            if (deck == null)
                continue;

            if (deck.Deck == null)
                deck.Deck = new List<string>();
        }

        if (decks.Length > PlayerDeckIndex && decks[PlayerDeckIndex] != null)
            decks[PlayerDeckIndex].isLocked = true;

        if (decks.Length > MysteryDeckIndex && decks[MysteryDeckIndex] != null)
            decks[MysteryDeckIndex].isLocked = true;
    }

    void CreateNewSave()
    {
        saveData = new SaveData
        {
            HighScore = 0,
            SelectedDeckIndex = 0,
            SelectedHeroIndex = 0,
            SelectedOpponentDeckIndex = 0,
            // Index 1 is 2-Summoner (AllHeroes is sorted by asset name), the hero the AI shipped with.
            SelectedOpponentHeroIndex = 1,
            Decks = BuildDeckSlots(defaultDeck),
            OpponentDecks = BuildDeckSlots(defaultOpponentDeck)
        };
    }

    // The ten slots one side gets: slot 0 seeded from `seedDeck` and locked, slot 1 the locked
    // mystery deck (filled by GenerateRandomDeck), the rest empty and editable.
    DeckData[] BuildDeckSlots(DeckSO seedDeck)
    {
        var decks = new DeckData[DeckSlotCount];

        decks[PlayerDeckIndex] = new DeckData
        {
            Name = seedDeck != null ? seedDeck.deckName : "Default_Deck_0",
            isLocked = true,
            Deck = new List<string>()
        };

        if (seedDeck != null)
        {
            for (int i = 0; i < seedDeck.cards.Count && decks[PlayerDeckIndex].Deck.Count < DeckSize; i++)
            {
                if (seedDeck.cards[i] != null)
                    decks[PlayerDeckIndex].Deck.Add(seedDeck.cards[i].cardName);
            }
        }

        for (int i = 1; i < decks.Length; i++)
        {
            decks[i] = new DeckData
            {
                Name = "Default_Deck_" + i,
                isLocked = i == MysteryDeckIndex,
                Deck = new List<string>()
            };
        }

        return decks;
    }

    protected void OnApplicationQuit()
    {
        SaveData();    
    }

    protected void OnApplicationPause(bool pauseStatus)
    {
        SaveData();

    }
}
