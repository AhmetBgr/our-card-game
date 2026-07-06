using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionLogPanel : Singleton<ActionLogPanel>
{
    [SerializeField] private LogEntryHandler entryPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private int maxEntries = 75;

    private readonly List<LogEntryHandler> activeEntries = new List<LogEntryHandler>();

    public void AddEntry(ActionLogEntry entry)
    {
        var instance = Instantiate(entryPrefab, content);
        instance.Bind(entry);
        activeEntries.Add(instance);

        if (activeEntries.Count > maxEntries)
        {
            var oldest = activeEntries[0];
            activeEntries.RemoveAt(0);
            Destroy(oldest.gameObject);
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
