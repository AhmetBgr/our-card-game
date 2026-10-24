using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The run's hero and deck, shown beside the card options while a reward card is picked over the
/// end-of-match screen, laid out like the Draft scene's panels so the player knows what the deck needs.
/// <see cref="BattleRewardsPanel"/> shows it when a pick opens and hides it when the panel comes back;
/// it is authored disabled.
/// </summary>
public class BattleRewardLoadout : MonoBehaviour
{
    [SerializeField] private GauntletHeroPanelView hero;

    [Header("Deck list")]
    [SerializeField] private TextMeshProUGUI deckHeaderText;
    [Tooltip("Entries are cloned from this one; the authored entries next to it are placeholders and get removed.")]
    [SerializeField] private CardButtonHandler deckEntryTemplate;

    [Header("Room for the panels")]
    [Tooltip("Moved sideways while shown (the selection panel's options and prompt), so they clear the " +
             "panels; put back on hide. The selection panel also serves in-match choices, which keep its layout.")]
    [SerializeField] private RectTransform[] shiftedWhileShown;
    [SerializeField] private float shiftX = -150f;

    private readonly List<CardButtonHandler> _deckEntries = new List<CardButtonHandler>();
    private bool _placeholdersCleared;
    private bool _shifted;

    /// <summary>
    /// Shows the run as it stands, so a pick just taken is already in the deck.
    /// Pass <paramref name="shiftPanels"/> false when nothing is in the way: the end-of-match review tabs
    /// (<see cref="DraftPanelTabs"/>) show the loadout on its own, with no card options to make room for.
    /// </summary>
    public void Show(bool shiftPanels = true)
    {
        if (!GauntletRun.IsActive) return;

        gameObject.SetActive(true);
        if (shiftPanels) Shift(true);
        if (!_placeholdersCleared)
        {
            GauntletDeckList.ClearPlaceholders(deckEntryTemplate);
            _placeholdersCleared = true;
        }

        hero?.ShowRunHero();
        GauntletDeckList.Refresh(deckHeaderText, deckEntryTemplate, _deckEntries, GauntletRun.Deck);
    }

    /// <summary>
    /// Where a card taken on the rewards screen should fly: the deck list it is about to join. Handed to
    /// <see cref="CardSelectionPanel.SetDeckTarget"/> when a pick opens, because the panel is a prefab
    /// and this is a scene object.
    /// </summary>
    public RectTransform DeckAnchor =>
        deckHeaderText != null ? (RectTransform)deckHeaderText.transform : (RectTransform)transform;

    /// <summary>
    /// The chip a perk taken on the rewards screen is about to fill: the first free slot on the hero's
    /// passive row. This is what the perk flies to AND what unlocks when it gets there, so the player
    /// sees the perk appear on the hero. Falls back to the last chip when the row is already full, and
    /// to null when there are no chips at all.
    /// </summary>
    public PassiveChipButton PassiveSlot
    {
        get
        {
            var chips = hero?.passiveChips;
            if (chips == null) return null;

            PassiveChipButton last = null;
            foreach (var chip in chips)
            {
                if (chip == null) continue;
                if (chip.Passive == null) return chip;
                last = chip;
            }
            return last;
        }
    }

    public void Hide()
    {
        Shift(false);
        gameObject.SetActive(false);
    }

    private void OnDestroy() => Shift(false);

    private void Shift(bool on)
    {
        if (_shifted == on || shiftedWhileShown == null) return;
        _shifted = on;

        float dx = on ? shiftX : -shiftX;
        foreach (var rt in shiftedWhileShown)
            if (rt != null) rt.anchoredPosition += new Vector2(dx, 0f);
    }
}
