using System.Collections.Generic;
using UnityEngine;

public class SaveManager : PermanentSingleton<SaveManager>
{
    public const int PlayerDeckIndex = 0;
    public const int MysteryDeckIndex = 1;

    public string saveDataKey = "DeckData";

    public SaveData saveData;
    public DeckSO defaultDeck;
    public int DeckSize = 10;

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
    public string GetSaveData()
    {
        // Retrieve deck data from PlayerPrefs or other storage
        string deckData = PlayerPrefs.GetString(saveDataKey, string.Empty);
        return deckData;
    }
    public void RemoveCard(string cardName, int deckIndex)
    {
        // todo: remove card from savedata
        if (!IsValidDeckIndex(deckIndex))
            return;

        if (!saveData.Decks[deckIndex].Deck.Contains(cardName)) return;

        saveData.Decks[deckIndex].Deck.Remove(cardName);
            SaveData();


        /*List<string> cards = new List<string>(saveData.Decks[deckIndex].Deck);

        if (cards.Remove(cardName))
        {
            saveData.Decks[deckIndex].Deck = cards.ToArray();
            SaveData();
        }*/
    }

    public bool AddCard(string cardName, int deckIndex)
    {
        // add card to savedata


        if (!IsValidDeckIndex(deckIndex))
            return false;

        if (saveData.Decks[deckIndex].Deck.Contains(cardName)) return false;

        if (saveData.Decks[deckIndex].Deck.Count >= DeckSize) return false;

        saveData.Decks[deckIndex].Deck.Add(cardName);


        /*List<string> cards = new List<string>(saveData.Decks[deckIndex].Deck);
        cards.Add(cardName);
        saveData.Decks[deckIndex].Deck = cards.ToArray();
        */
        SaveData();

        return true;
    }

    public void GenerateRandomDeck(int deckIndex)
    {
        if (!IsValidDeckIndex(deckIndex))
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

        var deck = saveData.Decks[deckIndex].Deck;
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

    bool IsValidDeckIndex(int index)
    {
        if (saveData == null || saveData.Decks == null)
            return false;

        if (index < 0 || index >= saveData.Decks.Length)
        {
            Debug.LogWarning($"Invalid deck index: {index}");
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

        saveData.SelectedDeckIndex = Mathf.Clamp(saveData.SelectedDeckIndex, 0, Mathf.Max(0, saveData.Decks.Length - 1));

        foreach (var deck in saveData.Decks)
        {
            if (deck == null)
                continue;

            if (deck.Deck == null)
                deck.Deck = new List<string>();
        }

        if (saveData.Decks.Length > PlayerDeckIndex && saveData.Decks[PlayerDeckIndex] != null)
            saveData.Decks[PlayerDeckIndex].isLocked = true;

        if (saveData.Decks.Length > MysteryDeckIndex && saveData.Decks[MysteryDeckIndex] != null)
            saveData.Decks[MysteryDeckIndex].isLocked = true;
    }

    void CreateNewSave()
    {
        saveData = new SaveData
        {
            HighScore = 0,
            SelectedDeckIndex = 0,
            Decks = new DeckData[10]


        };
        saveData.Decks[PlayerDeckIndex] = new DeckData
        {
            Name = defaultDeck != null ? defaultDeck.deckName : "Default_Deck_0",
            isLocked = true,
            Deck = new List<string>()
        };

        if (defaultDeck != null)
        {
            for (int i = 0; i < defaultDeck.cards.Count && saveData.Decks[PlayerDeckIndex].Deck.Count < DeckSize; i++)
            {
                if (defaultDeck.cards[i] != null)
                    saveData.Decks[PlayerDeckIndex].Deck.Add(defaultDeck.cards[i].cardName);
            }
        }


        for (int i = 1; i < saveData.Decks.Length; i++)
        {
            saveData.Decks[i] = new DeckData
            {
                Name = "Default_Deck_" + i,
                isLocked = i == MysteryDeckIndex,
                Deck = new List<string>()
            };
        }
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
