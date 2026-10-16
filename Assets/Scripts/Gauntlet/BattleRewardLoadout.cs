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

    /// <summary>Shows the run as it stands, so a pick just taken is already in the deck.</summary>
    public void Show()
    {
        if (!GauntletRun.IsActive) return;

        gameObject.SetActive(true);
        Shift(true);
        if (!_placeholdersCleared)
        {
            GauntletDeckList.ClearPlaceholders(deckEntryTemplate);
            _placeholdersCleared = true;
        }

        hero?.ShowRunHero();
        GauntletDeckList.Refresh(deckHeaderText, deckEntryTemplate, _deckEntries, GauntletRun.Deck);
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
