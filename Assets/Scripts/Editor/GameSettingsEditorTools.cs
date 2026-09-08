using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu entries for the settings file, alongside the ones <see cref="SaveManagerEditor"/> offers for the
/// save. The two are separate stores on purpose (see <see cref="GameSettings"/>), so clearing one here
/// deliberately leaves the other alone -- which is also why "Clear Save Data" does not touch settings.
/// </summary>
public static class GameSettingsEditorTools
{
    [MenuItem("Tools/Settings/Reveal Settings File")]
    private static void Reveal()
    {
        string path = GameSettings.FilePath;

        if (!File.Exists(path))
        {
            Debug.Log($"No settings file yet. It would be written to:\n{path}");

            // The folder is still worth opening: it is where the save lives too.
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                EditorUtility.RevealInFinder(directory);

            return;
        }

        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Tools/Settings/Print Settings")]
    private static void Print()
    {
        Debug.Log($"Settings ({(GameSettings.HasStoredSettings() ? GameSettings.FilePath : "defaults, nothing stored yet")}):\n" +
                  JsonUtility.ToJson(GameSettings.Data, true));
    }

    [MenuItem("Tools/Settings/Clear Settings")]
    private static void Clear()
    {
        if (!EditorUtility.DisplayDialog(
                "Clear settings?",
                "This puts the volumes, the mute switch and the interface options back to their defaults, " +
                "and deletes:\n\n" + GameSettings.FilePath +
                "\n\nDecks, the high score and the tutorial flag live in the save and are not touched.",
                "Clear", "Cancel"))
            return;

        GameSettings.DeleteStoredSettings();
        Debug.Log("Settings cleared.");
    }
}
