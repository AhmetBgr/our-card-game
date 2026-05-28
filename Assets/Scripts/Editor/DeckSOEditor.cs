using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DeckSO))]
public class DeckSOEditor : Editor
{
    private int _deckSize = 10;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        _deckSize = EditorGUILayout.IntField("Deck Size", _deckSize);

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
            .ToList();

        if (pool.Count == 0)
        {
            Debug.LogWarning("DeckSOEditor: no CardSO assets found in Resources/Cards.");
            return;
        }

        // Group by cost, shuffle each bucket.
        Dictionary<int, List<CardSO>> buckets = pool
            .GroupBy(c => c.cost)
            .ToDictionary(g => g.Key, g => g.OrderBy(_ => Random.value).ToList());

        List<int> costKeys = buckets.Keys.OrderBy(k => k).ToList();

        deck.cards.Clear();

        // Round-robin across cost buckets, removing each card after use so there are no repeats.
        while (deck.cards.Count < _deckSize)
        {
            // Remove exhausted buckets each pass.
            costKeys.RemoveAll(k => buckets[k].Count == 0);
            if (costKeys.Count == 0)
                break;

            foreach (int key in costKeys.ToList())
            {
                if (deck.cards.Count >= _deckSize) break;
                List<CardSO> bucket = buckets[key];
                if (bucket.Count == 0) continue;

                deck.cards.Add(bucket[0]);
                bucket.RemoveAt(0);
            }
        }

        EditorUtility.SetDirty(deck);
        Debug.Log($"DeckSOEditor: filled '{deck.deckName}' with {deck.cards.Count} cards across {costKeys.Count} cost tiers.");
    }
}
