using UnityEngine;
using UnityEditor;
using UnityEditor.Events;

/// <summary>
/// One-click wiring for the shipped hero passives, mirroring CardSOEditor's "Initialize Default
/// Minion Card". A UnityEvent persistent call has to name a target object and a method on it, which
/// is painful to author by hand in the .asset YAML — so we build it with UnityEventTools instead.
/// </summary>
[CustomEditor(typeof(TriggeredHeroPassiveSO))]
public class HeroPassiveSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var passive = (TriggeredHeroPassiveSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Wire Actions (clears existing)", EditorStyles.boldLabel);

        if (GUILayout.Button("Berserker — +1 Attack per 5 Health lost"))
            WireBerserker(passive);

        if (GUILayout.Button("Hunter — 1 dmg to random enemy on enemy spawn tile"))
            WireHunter(passive);
    }

    private static bool EnsureActionHolder(TriggeredHeroPassiveSO passive)
    {
        if (passive.actionHolder == null)
            passive.actionHolder = Resources.Load<ActionHolder>("ActionHolder");

        if (passive.actionHolder == null)
        {
            Debug.LogError("ActionHolder not found at Resources/ActionHolder");
            return false;
        }

        if (passive.actions == null)
            passive.actions = new UnityEngine.Events.UnityEvent();

        for (int i = passive.actions.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(passive.actions, i);

        return true;
    }

    private static void Save(TriggeredHeroPassiveSO passive, string what)
    {
        EditorUtility.SetDirty(passive);
        AssetDatabase.SaveAssets();
        Debug.Log($"Wired {what} onto {passive.name}");
    }

    private static void WireBerserker(TriggeredHeroPassiveSO passive)
    {
        if (!EnsureActionHolder(passive)) return;

        UnityEventTools.AddIntPersistentListener(
            passive.actions,
            passive.actionHolder.ScaleAttackWithHealthLost,
            5
        );

        Save(passive, "Berserker");
    }

    private static void WireHunter(TriggeredHeroPassiveSO passive)
    {
        if (!EnsureActionHolder(passive)) return;

        UnityEventTools.AddPersistentListener(
            passive.actions,
            passive.actionHolder.SelectEnemySpawnCells
        );

        UnityEventTools.AddPersistentListener(
            passive.actions,
            passive.actionHolder.SelectRandomEnemyMinionInSelectedCells
        );

        UnityEventTools.AddIntPersistentListener(
            passive.actions,
            passive.actionHolder.ChangeMinionHealth,
            -1
        );

        Save(passive, "Hunter");
    }
}
