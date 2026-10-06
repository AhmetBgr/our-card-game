using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The passive picker under one side's hero carousel, shown only while the multiple-passives modifier
/// is on. Lists every pickable passive (HeroDatabase.AllPassives); the selected hero's own passives
/// show locked-on, the rest toggle in and out of that side's extra list in the save, which the match
/// registers on top of the hero's own (GameManager.ExtraPassivesFor).
///
/// Like the deck and hero panels it is sided by the DeckSelectionContext above it.
/// </summary>
public class PassiveSelectionController : MonoBehaviour
{
    [Tooltip("The part that is shown and hidden. Empty = this object.")]
    [SerializeField] private GameObject panel;

    [SerializeField] private Transform chipsContainer;
    [SerializeField] private PassiveChipButton chipPrefab;

    [Tooltip("Optional. Shows how many extras are picked.")]
    [SerializeField] private TMP_Text countLabel;

    private readonly List<PassiveChipButton> chips = new List<PassiveChipButton>();
    private bool built;

    public SelectionSide Side { get; private set; }

    private GameObject Panel => panel != null ? panel : gameObject;

    void Awake()
    {
        Side = DeckSelectionContext.SideOf(this);
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

        int picked = 0;
        foreach (PassiveChipButton chip in chips)
        {
            bool locked = hero != null && hero.passives != null && hero.passives.Contains(chip.Passive);
            bool selected = extras != null && extras.Contains(chip.Passive.name);
            chip.SetState(locked, selected);
            if (selected && !locked) picked++;
        }

        if (countLabel != null) countLabel.text = picked > 0 ? $"+{picked}" : "";
    }

    private void OnChipClicked(PassiveChipButton chip)
    {
        if (chip.IsLocked || SaveManager.Instance == null) return;

        var extras = new List<string>(SaveManager.Instance.GetExtraPassives(Side));
        if (!extras.Remove(chip.Passive.name)) extras.Add(chip.Passive.name);

        SaveManager.Instance.SetExtraPassives(Side, extras);
        RefreshState();
    }
}
