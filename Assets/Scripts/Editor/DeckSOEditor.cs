using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DeckSO))]
public class DeckSOEditor : Editor
{
    // Target mana curve for a 10-card deck: (allowed costs, how many cards to draw from them).
    private static readonly (int[] costs, int count)[] ManaCurve = new (int[] costs, int count)[]
    {
        (new[] { 0, 1 }, 2),
        (new[] { 2 }, 3),
        (new[] { 3 }, 2),
        (new[] { 4 }, 2),
        (new[] { 5 }, 1),
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Fill with Random Cards (Balanced Cost)"))
            FillBalanced();

        if (GUILayout.Button("Clear Cards"))
        {
            DeckSO deck = (DeckSO)target;
            Undo.RecordObject(deck, "Clear Deck");
            deck.cards.Clear();
            EditorUtility.SetDirty(deck);
        }
    }

    private void FillBalanced()
    {
        DeckSO deck = (DeckSO)target;
        Undo.RecordObject(deck, "Fill Deck Balanced");

        List<CardSO> pool = AssetDatabase.FindAssets("t:CardSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => { var l = p.ToLowerInvariant(); return l.Contains("/cards/") && !l.Contains("/test/"); })
            .Select(p => AssetDatabase.LoadAssetAtPath<CardSO>(p))
            .Where(c => c != null && !c.isUpgraded)
            .OrderBy(_ => Random.value)
            .ToList();

        if (pool.Count == 0)
        {
            Debug.LogWarning("DeckSOEditor: no CardSO assets found in Resources/Cards.");
            return;
        }

        deck.cards.Clear();

        foreach ((int[] costs, int count) tier in ManaCurve)
        {
            List<CardSO> matches = pool.Where(c => tier.costs.Contains(c.cost)).Take(tier.count).ToList();
            deck.cards.AddRange(matches);
            foreach (CardSO c in matches) pool.Remove(c);

            if (matches.Count < tier.count)
                Debug.LogWarning($"DeckSOEditor: only found {matches.Count}/{tier.count} cards for cost {string.Join("/", tier.costs)}.");
        }

        EditorUtility.SetDirty(deck);
        Debug.Log($"DeckSOEditor: filled '{deck.deckName}' with {deck.cards.Count} cards following the mana curve.");
    }
}
