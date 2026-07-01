using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        GameManager gameManager = (GameManager)target;

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Trigger Victory Panel"))
                gameManager.TriggerVictory();

            if (GUILayout.Button("Trigger Defeat Panel"))
                gameManager.TriggerDefeat();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to trigger the victory/defeat panels.", MessageType.Info);
    }
}
