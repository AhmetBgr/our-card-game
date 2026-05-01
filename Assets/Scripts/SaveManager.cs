using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : PermanentSingleton<SaveManager>
{
    public string saveDataKey = "DeckData";

    public SaveData saveData;

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
    }

    void CreateNewSave()
    {
        saveData = new SaveData
        {
            HighScore = 0,
            SelectedDeckIndex = 0,
            Decks = new DeckData[10]


        };
        saveData.Decks[0] = new DeckData
        {
            Name = "Default_Deck_0",
            isLocked = true,
            Deck = new List<string>{
                "Moth",
                "Nailpuncher",
                "Priest"
            }
        };

        for (int i = 1; i < saveData.Decks.Length; i++)
        {
            saveData.Decks[i] = new DeckData
            {
                Name = "Default_Deck_" + i,
                isLocked = false,
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
