using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : PermanentSingleton<SaveManager>
{
    public const int PlayerDeckIndex = 0;
    public const int MysteryDeckIndex = 1;

    public const int DeckSlotCount = 10;

    [Tooltip("Legacy PlayerPrefs key. Only read once, to migrate saves written before the JSON file existed.")]
    public string saveDataKey = "DeckData";

    [Tooltip("File the save is written to, under Application.persistentDataPath.")]
    public string saveFileName = "savedata.json";

    /// <summary>
    /// Absolute path of the save file. persistentDataPath is the only directory guaranteed writable on
    /// every platform we ship to, so the save never lives next to the build.
    ///
    /// Not used by the WebGL player -- see <see cref="WebStorageKey"/>.
    /// </summary>
    public string SaveFilePath =>
        Path.Combine(Application.persistentDataPath,
            string.IsNullOrWhiteSpace(saveFileName) ? DefaultSaveFileName : saveFileName);

    const string DefaultSaveFileName = "savedata.json";

    /// <summary>
    /// PlayerPrefs key the browser build stores the save under, instead of a file.
    ///
    /// On WebGL, Application.persistentDataPath points into an in-memory emscripten filesystem that is
    /// only flushed to the browser's IndexedDB when the engine asks it to -- and the engine only asks
    /// from the PlayerPrefs subsystem, or when the page opts in with `autoSyncPersistentDataPath`, which
    /// the default web template leaves off. A System.IO write there therefore looks like it succeeded
    /// and is thrown away the moment the tab reloads. PlayerPrefs.Save() is the one write the browser
    /// build actually persists, so on that platform the whole save rides on it.
    ///
    /// Deliberately distinct from <see cref="saveDataKey"/>: that key is the pre-file legacy payload,
    /// which is still migrated (and then deleted) here exactly as it is on every other platform.
    /// </summary>
    const string WebStorageKey = "SaveDataJson";

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
        string json = ReadSaveFile();

        // Nothing on disk yet: a player upgrading from a build that stored the save in PlayerPrefs still
        // has their decks there, so adopt that payload once and let the SaveData() below write the file.
        bool migrated = false;
        if (string.IsNullOrEmpty(json))
        {
            json = PlayerPrefs.GetString(saveDataKey, string.Empty);
            migrated = !string.IsNullOrEmpty(json);
        }

        if (!string.IsNullOrEmpty(json))
        {
            // Deserialize and load the deck data
            Debug.Log($"Loaded deck data: {json}");
            try
            {
                saveData = JsonUtility.FromJson<SaveData>(json);
            }
            catch (System.Exception e)
            {
                // A truncated or hand-edited file must not brick the game; fall through to a fresh save.
                Debug.LogError($"Saved data could not be parsed, starting a new save. {e.Message}");
                saveData = null;
            }

            EnsureSaveDataIsValid();
        }

        bool createdNewSave = false;
        if (saveData == null || saveData.Decks == null || saveData.Decks.Length == 0)
        {
            Debug.Log("No deck data found.");
            CreateNewSave();
            createdNewSave = true;
        }

        // Write immediately when the file doesn't reflect what we just loaded: a fresh or migrated save
        // has no file yet, and a corrupt one must be replaced rather than re-read on the next boot.
        if (migrated || createdNewSave || !HasStoredSave())
            SaveData();

        if (migrated)
        {
            PlayerPrefs.DeleteKey(saveDataKey);
            PlayerPrefs.Save();
        }
    }

    public void SaveData() => WriteSaveFile(SerializeData());

    void WriteSaveFile(string value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // PlayerPrefs.Save() is what pushes the browser's in-memory filesystem out to IndexedDB, so it
        // has to run on every save rather than being left to quit time -- a closing tab may never get
        // that far. See WebStorageKey.
        PlayerPrefs.SetString(WebStorageKey, value);
        PlayerPrefs.Save();
#else
        // Write to a sibling temp file first, then swap it in: a crash mid-write leaves the previous
        // save intact instead of a half-flushed one.
        string path = SaveFilePath;
        string tempPath = path + ".tmp";

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(tempPath, value);

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tempPath, path);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to write save file at {path}: {e.Message}");
        }
#endif
    }

    string ReadSaveFile()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PlayerPrefs.GetString(WebStorageKey, string.Empty);
#else
        string path = SaveFilePath;

        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to read save file at {path}: {e.Message}");
            return string.Empty;
        }
#endif
    }

    /// <summary>
    /// Whether the store already holds a save, so <see cref="LoadData"/> can tell "nothing written yet"
    /// from "read it back fine" without assuming the store is a file.
    /// </summary>
    bool HasStoredSave()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PlayerPrefs.HasKey(WebStorageKey);
#else
        return File.Exists(SaveFilePath);
#endif
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
    // Whether the in-match action log is expanded. Off by default; the panel writes back on every
    // toggle, so the choice carries across matches and sessions.
    public bool ShowActionLog => saveData != null && saveData.ShowActionLog;

    public void SetShowActionLog(bool value)
    {
        if (saveData == null || saveData.ShowActionLog == value) return;

        saveData.ShowActionLog = value;
        SaveData();
    }

    // Whether hovered hand cards tilt toward the pointer. On by default; the toggle writes back
    // immediately so the choice carries across matches and sessions.
    public bool HoverTiltEnabled => saveData == null || saveData.HoverTiltEnabled;

    public void SetHoverTiltEnabled(bool value)
    {
        if (saveData == null || saveData.HoverTiltEnabled == value) return;

        saveData.HoverTiltEnabled = value;
        SaveData();
    }

    // Asset names of the two heroes the tutorial match forces on each side.
    public const string TutorialPlayerHeroName = "3-Hunter_Tutorial";
    public const string TutorialOpponentHeroName = "1-Berserker_Tutorial";

    public const string TutorialDeckResourcePath = "Decks/Tutorial Deck";

    private static DeckSO tutorialDeck;

    /// <summary>
    /// The authored deck the player is dealt in the tutorial match. Loaded from Resources rather than
    /// wired into a scene, so it resolves the same whether the game boots through the menu or the Game
    /// scene is played directly. Null-safe: a missing asset just leaves the player on their saved deck.
    /// </summary>
    public static DeckSO TutorialDeck =>
        tutorialDeck != null ? tutorialDeck : (tutorialDeck = Resources.Load<DeckSO>(TutorialDeckResourcePath));

    /// <summary>
    /// False until the tutorial match has been finished. While it is false the title screen skips
    /// itself and drops straight into the Game scene, where <see cref="Agent.ApplySavedSelection"/>
    /// fields the tutorial heroes and deck. See <see cref="SetTutorial"/>.
    /// </summary>
    public bool IsTutorial => saveData != null && saveData.IsTutorial;

    public void SetTutorial(bool value)
    {
        if (saveData == null || saveData.IsTutorial == value) return;

        saveData.IsTutorial = value;
        SaveData();
    }

    public string GetSaveData() => ReadSaveFile();
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
    // Pretty-printed: the save is a file on disk now, so it's worth being readable when debugging.
    public string SerializeData() => JsonUtility.ToJson(saveData, true);

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

    void CreateNewSave() => saveData = BuildNewSaveData();

    /// <summary>
    /// A fresh save, exactly as a first launch would build it. Handed back rather than assigned so the
    /// editor's "Clear Save Data (Tutorial Completed)" can write one straight to disk: the SaveManager it asks
    /// is usually the prefab, and assigning <see cref="saveData"/> on that would dirty the asset.
    /// </summary>
    public SaveData BuildNewSaveData()
    {
        return new SaveData
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
