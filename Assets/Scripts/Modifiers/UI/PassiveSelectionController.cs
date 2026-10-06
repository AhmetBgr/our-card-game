using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The passive picker under one side's hero carousel, shown only while the multiple-passives modifier
/// is on. Lists the pickable passives (HeroDatabase.AllPassives); the selected hero's own passives
/// are left out - they are always on, so they are not a choice - and the rest toggle in and out of
/// that side's extra list in the save, which the match registers on top of the hero's own
/// (GameManager.ExtraPassivesFor), up to MatchModifiers.MaxExtraPassives.
///
/// The cap is enforced twice on purpose: here, so a click past it does nothing and the count label
/// says why, and again in HeroPassiveSystem.Register, which is what actually decides how many reach
/// the board.
///
/// Like the deck and hero panels it is sided by the DeckSelectionContext above it.
/// </summary>
public class PassiveSelectionController : MonoBehaviour
{
    [Tooltip("The part that is shown and hidden. Empty = this object.")]
    [SerializeField] private GameObject panel;

    [SerializeField] private Transform chipsContainer;
    [SerializeField] private PassiveChipButton chipPrefab;

    [Tooltip("Optional. The picker's heading: its authored text is kept and the pick count appended, " +
             "e.g. \"Extra Passives - (1/2)\".")]
    [SerializeField] private TMP_Text countLabel;

    [Tooltip("Optional fixed spot for a chip's tooltip - point it at the deck panel's card-preview " +
             "area so a hovered passive reads where a hovered card does. Empty = the tooltip sits " +
             "beside the chip.")]
    [SerializeField] private Transform tooltipPoint;

    private readonly List<PassiveChipButton> chips = new List<PassiveChipButton>();
    private bool built;

    // Extras picked for this side right now, recomputed by RefreshState. Kept as a field so the cap
    // check reads the same number the label shows, rather than re-deriving it from the saved names —
    // which would have to re-resolve every name just to tell a pick from the hero's own.
    private int picked;

    // The heading's authored text, read before the first count is appended to it. Captured rather than
    // written in code so the wording stays in the scene, where it is authored (and translated).
    private string countLabelPrefix;

    public SelectionSide Side { get; private set; }

    private GameObject Panel => panel != null ? panel : gameObject;

    void Awake()
    {
        Side = DeckSelectionContext.SideOf(this);

        // Before anything can overwrite it — RefreshState appends to this every time it runs, so
        // reading it later would compound "Extra Passives - (0/2) - (1/2)".
        if (countLabel != null) countLabelPrefix = countLabel.text;
    }

    void OnEnable()
    {
        MatchModifiers.Changed += RefreshVisibility;
        HeroSelectionController.HeroSelectionChanged += OnHeroSelectionChanged;
    }

    void OnDisable()
    {
        MatchModifiers.Changed -= RefreshVisibility;
        HeroSelectionController.HeroSelectionChanged -= OnHeroSelectionChanged;
    }

    void Start()
    {
        Build();
        RefreshVisibility();
    }

    private void Build()
    {
        if (built || chipPrefab == null || chipsContainer == null || HeroDatabase.Instance == null) return;
        built = true;

        foreach (HeroPassiveSO passive in HeroDatabase.Instance.AllPassives)
        {
            if (passive == null) continue;

            PassiveChipButton chip = Instantiate(chipPrefab, chipsContainer);
            chip.name = passive.name;
            chip.SetPassive(passive);
            chip.SetTooltipPoint(tooltipPoint);
            chip.OnClicked = OnChipClicked;
            chips.Add(chip);
        }

        RefreshState();
    }

    private void RefreshVisibility()
    {
        Panel.SetActive(MatchModifiers.ExtraPassivesEnabled);
        RefreshState();
    }

    private void OnHeroSelectionChanged(SelectionSide side, bool value)
    {
        if (side == Side) RefreshState();
    }

    /// <summary>The hero this side has picked, or null for the Random slot (nothing is locked then).</summary>
    private HeroSO SelectedHero()
    {
        if (SaveManager.Instance == null || HeroDatabase.Instance == null) return null;

        int index = SaveManager.Instance.GetSelectedHeroIndex(Side);
        return index == HeroDatabase.RandomHeroIndex ? null : HeroDatabase.Instance.GetHeroByIndex(index);
    }

    private void RefreshState()
    {
        if (!built || SaveManager.Instance == null) return;

        HeroSO hero = SelectedHero();
        List<string> extras = SaveManager.Instance.GetExtraPassives(Side);

        picked = 0;
        foreach (PassiveChipButton chip in chips)
        {
            bool locked = hero != null && hero.passives != null && hero.passives.Contains(chip.Passive);
            bool selected = extras != null && extras.Contains(chip.Passive.name);

            // The hero's own passives are always on, so they are not a pick: keep them out of the list.
            chip.gameObject.SetActive(!locked);
            if (locked) continue;

            chip.SetState(false, selected);
            if (selected) picked++;
        }

        if (countLabel != null)
        {
            string count = $"({picked}/{MatchModifiers.MaxExtraPassives})";
            countLabel.text = string.IsNullOrEmpty(countLabelPrefix) ? count : $"{countLabelPrefix} - {count}";
        }
    }

    private void OnChipClicked(PassiveChipButton chip)
    {
        if (chip.IsLocked || SaveManager.Instance == null) return;

        var extras = new List<string>(SaveManager.Instance.GetExtraPassives(Side));

        // Unpicking always goes through; picking is capped. A click on a third chip does nothing
        // rather than dropping an earlier pick — which of the two to lose is not ours to guess.
        if (!extras.Remove(chip.Passive.name))
        {
            if (picked >= MatchModifiers.MaxExtraPassives) return;
            extras.Add(chip.Passive.name);
        }

        SaveManager.Instance.SetExtraPassives(Side, extras);
        RefreshState();
    }
}
