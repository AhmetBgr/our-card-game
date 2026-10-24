using System.IO;
using UnityEngine;

/// <summary>
/// Where the Forged in Battle run is kept between sessions: its own file beside the main save, so a run
/// that cannot be read any more is thrown away without touching the player's decks or settings.
///
/// Same storage rules as <see cref="SaveManager"/>: a file swapped in through a temp copy on desktop, and
/// PlayerPrefs on WebGL, where a System.IO write into persistentDataPath is lost on the next reload (see
/// SaveManager.WebStorageKey). Static, so it works in any scene and before any singleton exists.
/// </summary>
public static class GauntletSaveStore
{
    const string FileName = "gauntlet_run.json";
    const string WebStorageKey = "GauntletRunJson";

    static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool Has()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PlayerPrefs.HasKey(WebStorageKey);
#else
        return File.Exists(FilePath);
#endif
    }

    /// <summary>The stored run, or null when there is none or it cannot be parsed.</summary>
    public static GauntletSaveData Load()
    {
        string json = Read();
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return JsonUtility.FromJson<GauntletSaveData>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GauntletSaveStore: the saved run could not be parsed and is discarded. {e.Message}");
            Delete();
            return null;
        }
    }

    public static void Write(GauntletSaveData data)
    {
        if (data == null) return;
        string json = JsonUtility.ToJson(data);

#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.SetString(WebStorageKey, json);
        PlayerPrefs.Save();
#else
        string path = FilePath;
        string tempPath = path + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(tempPath, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GauntletSaveStore: failed to write {path}: {e.Message}");
        }
#endif
    }

    public static void Delete()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.DeleteKey(WebStorageKey);
        PlayerPrefs.Save();
#else
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GauntletSaveStore: failed to delete {FilePath}: {e.Message}");
        }
#endif
    }

    /// <summary>The raw stored JSON, for the editor tools.</summary>
    public static string Read()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PlayerPrefs.GetString(WebStorageKey, string.Empty);
#else
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GauntletSaveStore: failed to read {FilePath}: {e.Message}");
            return null;
        }
#endif
    }

    public static string Location => FilePath;
}
