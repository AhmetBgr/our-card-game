using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows the current match setup as three separate labels in the CreateCustomGame scene:
/// the player's "Berserker with Deck 1", a middle "VS", and the opponent's "Summoner with Deck 2".
/// Splitting the line into three TextMeshPro targets lets each part be placed and sized on its own,
/// while every meaningful word keeps its own inspector-tunable <see cref="Segment"/> style
/// (colour, bold, italic, size), applied via TextMeshPro rich-text tags.
///
/// It rebuilds whenever either side changes a deck or hero, subscribing to the same
/// <see cref="DeckPanelController.DeckChanged"/> / <see cref="HeroSelectionController.HeroSelectionChanged"/>
/// events the setup flow uses, and reads the live selection straight from <see cref="SaveManager"/> /
/// <see cref="HeroDatabase"/> so it can never drift from what the panels display.
/// </summary>
public class MatchSummaryController : MonoBehaviour
{
    /// <summary>
    /// Per-word styling. Each toggle wraps the text in a TMP rich-text tag; a segment with nothing
    /// enabled inherits the label's own colour/size and stays plain.
    /// </summary>
    [Serializable]
    public class Segment
    {
        [Tooltip("Tint this word. Leave off to inherit the label's colour.")]
        public bool overrideColor = false;
        public Color color = Color.white;

        public bool bold = false;
        public bool italic = false;

        [Tooltip("Override the font size for this word. Leave off to inherit the label's size.")]
        public bool overrideSize = false;
        [Min(1f)] public float size = 36f;

        /// <summary>Wraps <paramref name="text"/> in the enabled rich-text tags.</summary>
        public string Apply(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (overrideColor)
                text = $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
            if (bold)
                text = $"<b>{text}</b>";
            if (italic)
                text = $"<i>{text}</i>";
            if (overrideSize)
                text = $"<size={size}>{text}</size>";

            return text;
        }
    }

    [Header("Output labels")]
    [Tooltip("Left label: the player's hero and deck. Enable Rich Text on it for the styling to show.")]
    [SerializeField] private TextMeshProUGUI playerText;
    [Tooltip("Middle label: the 'VS' separator.")]
    [SerializeField] private TextMeshProUGUI vsText;
    [Tooltip("Right label: the opponent's hero and deck.")]
    [SerializeField] private TextMeshProUGUI opponentText;

    [Header("Wording")]
    [Tooltip("Joins a hero to its deck: 'Berserker WITH Deck 1'.")]
    [SerializeField] private string withWord = "with";
    [Tooltip("The middle separator label.")]
    [SerializeField] private string vsWord = "VS";
    [Tooltip("How a deck is named. {0} is the deck number (1-based).")]
    [SerializeField] private string deckNameFormat = "Deck {0}";
    [Tooltip("Shown for a hero slot left on 'Random Hero'.")]
    [SerializeField] private string randomHeroLabel = "Random Hero";

    [Header("Segment styles")]
    [SerializeField] private Segment playerHeroStyle = new Segment { overrideColor = true, color = new Color(0.35f, 0.7f, 1f), bold = true };
    [SerializeField] private Segment playerDeckStyle = new Segment();
    [SerializeField] private Segment withStyle = new Segment();
    [SerializeField] private Segment vsStyle = new Segment { bold = true };
    [SerializeField] private Segment opponentHeroStyle = new Segment { overrideColor = true, color = new Color(1f, 0.45f, 0.4f), bold = true };
    [SerializeField] private Segment opponentDeckStyle = new Segment();

    private void OnEnable()
    {
        DeckPanelController.DeckChanged += OnSelectionChanged;
        HeroSelectionController.HeroSelectionChanged += OnSelectionChanged;
        Refresh();
    }

    private void OnDisable()
    {
        DeckPanelController.DeckChanged -= OnSelectionChanged;
        HeroSelectionController.HeroSelectionChanged -= OnSelectionChanged;
    }

    // Both events carry (side, isReady); the summary reflects the raw selection regardless of
    // readiness, so the payload is ignored and we simply rebuild from current state.
    private void OnSelectionChanged(SelectionSide side, bool value) => Refresh();

    /// <summary>Rebuilds the three labels from the live save selection. Safe to call any time.</summary>
    public void Refresh()
    {
        if (SaveManager.Instance == null || HeroDatabase.Instance == null)
            return;

        if (playerText != null)
            playerText.text = Side(SelectionSide.Player, playerHeroStyle, playerDeckStyle);

        if (vsText != null)
            vsText.text = vsStyle.Apply(vsWord);

        if (opponentText != null)
            opponentText.text = Side(SelectionSide.Opponent, opponentHeroStyle, opponentDeckStyle);
    }

    // "<hero> with <deck>" for one side, each piece styled by its own segment.
    private string Side(SelectionSide side, Segment heroStyle, Segment deckStyle) =>
        $"{heroStyle.Apply(HeroName(side))} {withStyle.Apply(withWord)} {deckStyle.Apply(DeckName(side))}";

    private string HeroName(SelectionSide side)
    {
        int index = SaveManager.Instance.GetSelectedHeroIndex(side);
        if (index == HeroDatabase.RandomHeroIndex)
            return randomHeroLabel;

        var hero = HeroDatabase.Instance.GetHeroByIndex(index);
        if (hero == null)
            return string.Empty;

        // Prefer the authored card name, mirroring the hero-selection label; fall back to the asset name.
        return !string.IsNullOrEmpty(hero.cardName) ? hero.cardName : hero.name;
    }

    private string DeckName(SelectionSide side)
    {
        int index = SaveManager.Instance.GetSelectedDeckIndex(side);
        return string.Format(deckNameFormat, index + 1);
    }
}
