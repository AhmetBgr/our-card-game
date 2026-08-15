using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector button and Tools-menu entry for wiping the save.
///
/// Offered in both places on purpose: <see cref="SaveManager"/> is a <see cref="PermanentSingleton{T}"/>
/// that lives in no scene, so outside Play Mode the only object to select is the prefab under
/// Assets/Prefabs/Managers. The menu item reaches the same code with nothing selected, which is what you
/// want when the reason you are clearing is "let me see the tutorial again".
/// </summary>
[CustomEditor(typeof(SaveManager))]
public class SaveManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        SaveManager saveManager = (SaveManager)target;
        string path = saveManager.SaveFilePath;

        EditorGUILayout.HelpBox(
            (File.Exists(path) ? "Save file:\n" : "No save file yet. It would be written to:\n") + path,
            MessageType.None);

        if (GUILayout.Button("Clear Save Data"))
            Clear(saveManager);
    }

    [MenuItem("Tools/Save Data/Clear Save Data")]
    private static void ClearFromMenu()
    {
        SaveManager saveManager = Resolve();

        if (saveManager == null)
        {
            Debug.LogError("Clear Save Data: no SaveManager found. Expected one in the loaded scenes " +
                           "(Play Mode) or a prefab carrying the component.");
            return;
        }

        Clear(saveManager);
    }

    /// <summary>
    /// A SaveManager to read the save location off. The live one first, so a Play Mode clear uses whatever
    /// the running game is actually reading and writing; otherwise the prefab, so a renamed
    /// <see cref="SaveManager.saveFileName"/> is still honoured rather than the path being guessed at here.
    /// </summary>
    private static SaveManager Resolve()
    {
        SaveManager live = Object.FindObjectOfType<SaveManager>();
        if (live != null) return live;

        foreach (string guid in AssetDatabase.FindAssets("SaveManager t:Prefab"))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;

            SaveManager component = prefab.GetComponent<SaveManager>();
            if (component != null) return component;
        }

        return null;
    }

    private static void Clear(SaveManager saveManager)
    {
        string path = saveManager.SaveFilePath;
        bool hasFile = File.Exists(path);

        if (!EditorUtility.DisplayDialog(
                "Clear save data?",
                "This wipes both sides' decks, the high score, the selected hero and deck, and the action " +
                "log setting — and re-arms the tutorial, so the next launch plays it again.\n\n" +
                (hasFile ? path : "(no save file on disk; the legacy PlayerPrefs key is still cleared)") +
                "\n\nThis cannot be undone.",
                "Clear", "Cancel"))
            return;

        int deletedFiles = 0;

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                deletedFiles++;
            }

            // The sibling SaveData() writes before swapping in. A crash mid-write can leave one behind,
            // and it would be a stray copy of the very data we were asked to remove.
            string tempPath = path + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                deletedFiles++;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Clear Save Data: could not delete {path}: {e.Message}");
            return;
        }

        // Saves written before the JSON file existed still sit in PlayerPrefs, and LoadData() migrates
        // them straight back on the next launch — deleting the file alone would not actually clear anything
        // for anyone upgrading from such a build.
        if (!string.IsNullOrEmpty(saveManager.saveDataKey) && PlayerPrefs.HasKey(saveManager.saveDataKey))
        {
            PlayerPrefs.DeleteKey(saveManager.saveDataKey);
            PlayerPrefs.Save();
        }

        // In Play Mode the running SaveManager still holds the old save in memory and writes it back on
        // quit and on pause, which would undo all of the above the moment you leave Play Mode. Drop it and
        // reload: with no file and no PlayerPrefs left, LoadData() builds a fresh save and writes it out.
        // Resolved again rather than reusing the argument, so clicking the button on the PREFAB while
        // playing still resets the live instance instead of dirtying the asset.
        if (Application.isPlaying)
        {
            SaveManager live = Object.FindObjectOfType<SaveManager>();
            if (live != null)
            {
                live.saveData = null;
                live.LoadData();
            }
        }

        Debug.Log($"Save data cleared ({deletedFiles} file(s) deleted): {path}" +
                  (Application.isPlaying ? " — the running game was reset to a fresh save." : string.Empty));
    }
}
