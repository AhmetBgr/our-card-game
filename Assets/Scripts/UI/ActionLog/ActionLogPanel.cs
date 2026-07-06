using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionLogPanel : Singleton<ActionLogPanel>
{
    [SerializeField] private LogEntryHandler entryPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private int maxEntries = 75;

    [Header("Show / Hide toggle")]
    [Tooltip("The part of the panel that gets hidden when collapsed (e.g. the scroll view). The toggle button must NOT be under this object, or it would hide itself.")]
    [SerializeField] private GameObject collapsibleBody;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleLabel;
    [SerializeField] private string shownLabel = "Log ▼";
    [SerializeField] private string hiddenLabel = "Log ▲";
    [SerializeField] private bool startShown = true;

    private bool isShown = true;

    private readonly List<LogEntryHandler> activeEntries = new List<LogEntryHandler>();

    protected override void Awake()
    {
        base.Awake();

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleVisibility);
        }
        SetShown(startShown);
    }

    public void ToggleVisibility()
    {
        SetShown(!isShown);
    }

    public void SetShown(bool shown)
    {
        isShown = shown;

        if (collapsibleBody != null)
        {
            collapsibleBody.SetActive(shown);
        }
        if (toggleLabel != null)
        {
            toggleLabel.text = shown ? shownLabel : hiddenLabel;
        }
    }

    // A card's "played" entry is held here from the start of its play until the play actually commits,
    // so it lands just above the deaths/summons the card triggers instead of after them. It is shown
    // as soon as any sub-event is logged (or on successful completion), and dropped if the play is
    // cancelled so a backed-out card leaves no orphaned entry.
    private ActionLogEntry pendingEntry;

    public void SetPending(ActionLogEntry entry)
    {
        pendingEntry = entry;
    }

    public void FlushPending()
    {
        if (pendingEntry == null) return;
        var entry = pendingEntry;
        pendingEntry = null;
        AddEntryInternal(entry);
    }

    public void DiscardPending()
    {
        pendingEntry = null;
    }

    public void AddEntry(ActionLogEntry entry)
    {
        // Any pending "played" header must appear above this sub-event, so emit it first.
        if (pendingEntry != null)
        {
            var header = pendingEntry;
            pendingEntry = null;
            AddEntryInternal(header);
        }
        AddEntryInternal(entry);
    }

    private void AddEntryInternal(ActionLogEntry entry)
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
