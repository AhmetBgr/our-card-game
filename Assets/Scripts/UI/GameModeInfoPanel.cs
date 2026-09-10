using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The description box above the game modes in the main menu's Play popup. It is always on screen --
/// unlike a hover <see cref="UITooltip"/>, which the player has to go looking for -- and its text follows
/// whichever mode the pointer is over, falling back to <see cref="idleMessage"/> when it is over none.
///
/// The box is deliberately a fixed part of the popup rather than a floating panel: three modes in a row
/// with nothing said about them is a row of three guesses, and a tooltip that covers the buttons it
/// describes (which is what the floating one did here) is worse than no tooltip.
///
/// Hover is picked up by an <see cref="EventTrigger"/> this component adds to each button at runtime, so
/// nothing has to be authored onto the buttons themselves and a mode is added here by adding a row to
/// <see cref="entries"/>. EventTrigger rather than <see cref="Selectable"/> state because a
/// non-interactable button -- the mode that isn't written yet -- still has a description to show, and
/// still reports pointer enter and exit.
/// </summary>
public class GameModeInfoPanel : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("The mode button the pointer has to be over for this description to show.")]
        public Button button;

        [TextArea(2, 4)]
        public string description;
    }

    [Tooltip("Where the description is written. Its own text is replaced, so whatever is authored on it " +
             "is only what the box looks like in the editor.")]
    [SerializeField] private TMP_Text label;

    [Tooltip("Shown whenever the pointer is over none of the modes. The box never empties -- an empty " +
             "box in a fixed layout reads as something failing to load.")]
    [TextArea(2, 4)]
    [SerializeField] private string idleMessage = "Hover a mode to see what it does.";

    [SerializeField] private List<Entry> entries = new List<Entry>();

    /// <summary>
    /// How many modes are being hovered, which is only ever 0 or 1 -- but pointer exit on the mode being
    /// left can arrive after pointer enter on the mode being moved to, and a plain bool would leave the
    /// box on the idle message with the pointer sitting on a button.
    /// </summary>
    private int hovering;

    void Awake()
    {
        foreach (Entry entry in entries)
        {
            if (entry == null || entry.button == null) continue;

            // Captured per entry: the closure is what carries the description, so nothing has to be
            // looked up when the pointer arrives.
            Entry captured = entry;

            AddHandler(captured.button.gameObject, EventTriggerType.PointerEnter, () =>
            {
                hovering++;
                Write(captured.description);
            });

            AddHandler(captured.button.gameObject, EventTriggerType.PointerExit, () =>
            {
                hovering = Mathf.Max(0, hovering - 1);
                if (hovering == 0) Write(idleMessage);
            });
        }

        Write(idleMessage);
    }

    /// <summary>
    /// Back to the idle message every time the popup opens. A description left over from the last time
    /// it was open would otherwise be describing a button the pointer is nowhere near.
    /// </summary>
    void OnEnable()
    {
        hovering = 0;
        Write(idleMessage);
    }

    private void Write(string text)
    {
        if (label != null)
            label.text = text;
    }

    private static void AddHandler(GameObject target, EventTriggerType type, Action handler)
    {
        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => handler());
        trigger.triggers.Add(entry);
    }
}
