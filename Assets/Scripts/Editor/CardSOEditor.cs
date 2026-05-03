using UnityEngine;
using UnityEditor;
using UnityEditor.Events;

[CustomEditor(typeof(CardSO))]
public class CardSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CardSO card = (CardSO)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Initialize Default Minion Card"))
        {
            InitializeDefaultMinionCard(card);
        }
    }

    private void InitializeDefaultMinionCard(CardSO card)
    {
        if (card.actionHolder == null)
        {
            Debug.LogError("ActionHolder is not assigned!");
            return;
        }

        UnityEventTools.AddIntPersistentListener(
            card.OnPlay,
            card.actionHolder.SelectCell,
            2
        );

        UnityEventTools.AddPersistentListener(
            card.OnPlay,
            card.actionHolder.SelectThisAgent
        );

        UnityEventTools.AddPersistentListener(
            card.OnPlay,
            card.actionHolder.PayCardCost
        );
        UnityEventTools.AddObjectPersistentListener<CardSO>(
            card.OnPlay,
            card.actionHolder.SummonMinion,
            card
        );

        // Mark dirty so Unity saves changes
        EditorUtility.SetDirty(card);
        AssetDatabase.SaveAssets();

        Debug.Log("OnPlayInitialize configured for " + card.name);
    }
}