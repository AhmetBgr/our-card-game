using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor shortcuts for the Forged in Battle run save (<see cref="GauntletSaveStore"/>). Domain reload is
/// off in this project, so the run in memory resets between play sessions while the save on disk stays
/// -- which is exactly what resuming needs to be tested with, and why deleting it needs a button.
/// </summary>
public static class GauntletSaveEditorTools
{
    const string Menu = "Tools/Forged in Battle/";

    [MenuItem(Menu + "Log Run Save")]
    static void LogRunSave()
    {
        string json = GauntletSaveStore.Read();
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log($"Forged in Battle: no run saved ({GauntletSaveStore.Location}).");
            return;
        }

        var data = JsonUtility.FromJson<GauntletSaveData>(json);
        string pretty = data != null ? JsonUtility.ToJson(data, true) : json;
        Debug.Log($"Forged in Battle run save ({GauntletSaveStore.Location}, {json.Length} chars):\n{pretty}");
    }

    [MenuItem(Menu + "Delete Run Save")]
    static void DeleteRunSave()
    {
        if (!GauntletSaveStore.Has())
        {
            Debug.Log("Forged in Battle: no run saved.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Delete Forged in Battle run",
                "Delete the saved run? The Forged in Battle button will start a new one.", "Delete", "Cancel"))
            return;

        GauntletSaveStore.Delete();
        if (Application.isPlaying) GauntletRun.Unload();
        Debug.Log("Forged in Battle: run save deleted.");
    }

    [MenuItem(Menu + "Reveal Run Save")]
    static void RevealRunSave() => EditorUtility.RevealInFinder(GauntletSaveStore.Location);
}
