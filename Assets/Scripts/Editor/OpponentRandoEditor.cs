using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OpponentRando))]
public class OpponentRandoEditor : Editor
{
    private int deckSize = 10;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        deckSize = EditorGUILayout.IntField("Deck Size", deckSize);

        if (GUILayout.Button("Fill With Random"))
            FillWithRandom((OpponentRando)target);
    }

    private void FillWithRandom(OpponentRando opponent)
    {
        var allCards = Resources.LoadAll<CardSO>("Cards")
            .Where(c => !c.isUpgraded && !string.IsNullOrEmpty(c.cardName))
            .ToList();

        for (int i = allCards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (allCards[i], allCards[j]) = (allCards[j], allCards[i]);
        }

        Undo.RecordObject(opponent, "Fill With Random Deck");
        opponent.deck.Clear();
        for (int i = 0; i < Mathf.Min(deckSize, allCards.Count); i++)
            opponent.deck.Add(allCards[i]);

        EditorUtility.SetDirty(opponent);
        Debug.Log($"OpponentRando deck filled with {opponent.deck.Count} random cards.");
    }
}
