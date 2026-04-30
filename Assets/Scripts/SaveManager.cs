using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : PermanentSingleton<SaveManager>
{
    public string saveDataKey = "DeckData";

    public SaveData saveData;

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
        }
        else
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

        List<string> cards = new List<string>(saveData.Decks[deckIndex].Deck);

        if (cards.Remove(cardName))
        {
            saveData.Decks[deckIndex].Deck = cards.ToArray();
            SaveData();
        }
    }

    public void AddCard(string cardName, int deckIndex)
    {
        // add card to savedata

        if (!IsValidDeckIndex(deckIndex))
            return;

        List<string> cards = new List<string>(saveData.Decks[deckIndex].Deck);
        cards.Add(cardName);
        saveData.Decks[deckIndex].Deck = cards.ToArray();

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
    void CreateNewSave()
    {
        saveData = new SaveData
        {
            HighScore = 0,
            Decks = new DeckData[]
            {
                new DeckData
                {
                    Name = "Default Deck",
                    Deck = new string[0]
                }
            }
        };
        saveData.Decks[0].isLocked = true;
        saveData.Decks[0].Deck = new string[]
        {
            "Moth",
            "Nailpuncher",
            "Priest"
        };
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
