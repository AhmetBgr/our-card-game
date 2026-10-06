using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads every <see cref="HeroSO"/> under Assets/Resources/Heroes and exposes them as a
/// stable, index-addressable list so the menu's hero-selection panel and the in-game
/// <see cref="Player"/> agree on which hero a given SelectedHeroIndex refers to.
///
/// Mirrors <see cref="DeckDatabase"/>, but is a PermanentSingleton so it self-instantiates
/// (and survives scene loads) in both the Menu and Game scenes without needing a scene object.
/// </summary>
public class HeroDatabase : PermanentSingleton<HeroDatabase>
{
    /// <summary>Sentinel SelectedHeroIndex meaning "pick a random hero at game start" (mirrors the mystery deck).</summary>
    public const int RandomHeroIndex = -1;

    /// <summary>Where the tutorial-only heroes live, relative to Resources.</summary>
    public const string TutorialHeroesFolder = "Heroes/Other";

    /// <summary>
    /// The heroes a match can actually be played with: everything under Heroes/ except the
    /// tutorial-only ones. This is what SelectedHeroIndex indexes into, what the setup scene's
    /// carousel lists, and what a random roll picks from.
    /// </summary>
    public List<HeroSO> AllHeroes = new List<HeroSO>();

    /// <summary>
    /// Tutorial-only heroes, held apart from <see cref="AllHeroes"/> so neither Quick Play's random
    /// roll nor the custom-game carousel can offer them. Only ever addressed by name, by the tutorial
    /// itself (see <see cref="GetHeroByName"/>), so they need no stable index.
    /// </summary>
    private readonly List<HeroSO> tutorialHeroes = new List<HeroSO>();

    /// <summary>Where the pickable passives live, relative to Resources. The tutorial variants in Heroes/Other are not here.</summary>
    public const string PassivesFolder = "Heroes/Passives";

    /// <summary>
    /// Every passive a side can add in the setup screen (multiple-passives modifier), sorted by asset
    /// name. Addressed by asset name in the save, like the tutorial heroes, so the list can grow or
    /// reorder without a saved pick pointing at the wrong passive.
    /// </summary>
    public List<HeroPassiveSO> AllPassives = new List<HeroPassiveSO>();

    protected override void Awake()
    {
        base.Awake();
        LoadHeroes();
    }

    void LoadHeroes()
    {
        AllHeroes.Clear();
        tutorialHeroes.Clear();

        // Resources.LoadAll recurses into subfolders but only returns HeroSO assets, so the
        // Passives/ subfolder (HeroPassiveSO) is naturally excluded.
        HeroSO[] heroes = Resources.LoadAll<HeroSO>("Heroes");

        // Loading the subfolder on its own is what makes the split possible: a HeroSO carries no
        // record of where it came from, so at runtime there's nothing else to tell them apart by.
        HeroSO[] tutorialOnly = Resources.LoadAll<HeroSO>(TutorialHeroesFolder);
        tutorialHeroes.AddRange(tutorialOnly);

        foreach (var hero in heroes)
        {
            if (Array.IndexOf(tutorialOnly, hero) < 0)
                AllHeroes.Add(hero);
        }

        // Resources load order is not guaranteed; sort by asset name so SelectedHeroIndex maps
        // to the same hero across sessions and between the Menu and Game scenes.
        AllHeroes.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        AllPassives.Clear();
        AllPassives.AddRange(Resources.LoadAll<HeroPassiveSO>(PassivesFolder));
        AllPassives.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        Debug.Log($"Loaded {AllHeroes.Count} heroes into HeroDatabase ({tutorialHeroes.Count} tutorial-only heroes held back).");
    }

    public HeroPassiveSO GetPassiveByName(string assetName) =>
        AllPassives.Find(p => p != null && p.name == assetName);

    /// <summary>The passives named in <paramref name="assetNames"/>, in that order; unknown names are dropped with a warning.</summary>
    public List<HeroPassiveSO> ResolvePassives(IReadOnlyList<string> assetNames)
    {
        var result = new List<HeroPassiveSO>();
        if (assetNames == null) return result;

        for (int i = 0; i < assetNames.Count; i++)
        {
            HeroPassiveSO passive = GetPassiveByName(assetNames[i]);
            if (passive != null) result.Add(passive);
            else Debug.LogWarning($"Passive asset not found: {assetNames[i]}.");
        }

        return result;
    }

    public HeroSO GetHeroByIndex(int index)
    {
        if (AllHeroes.Count == 0)
            return null;

        index = Mathf.Clamp(index, 0, AllHeroes.Count - 1);
        return AllHeroes[index];
    }

    /// <summary>
    /// The hero whose asset is named <paramref name="assetName"/>, or null when there is no such hero.
    /// Lets callers that need one specific hero (the tutorial match-up) address it by name instead of
    /// by a position in the name-sorted list, which shifts whenever a hero asset is added or renamed.
    /// Null rather than a fallback hero, so a caller can tell "not found" from a real answer and go
    /// back to the saved selection instead of silently fielding the wrong hero.
    /// </summary>
    public HeroSO GetHeroByName(string assetName)
    {
        // Searches the tutorial-only heroes too — being unpickable is about the random roll and the
        // carousel, not about being unreachable to the code that deliberately asks for one.
        var hero = AllHeroes.Find(h => h != null && h.name == assetName)
                   ?? tutorialHeroes.Find(h => h != null && h.name == assetName);

        if (hero == null)
            Debug.LogWarning($"Hero asset not found: {assetName}.");

        return hero;
    }

    public HeroSO GetRandomHero()
    {
        if (AllHeroes.Count == 0)
            return null;

        return AllHeroes[UnityEngine.Random.Range(0, AllHeroes.Count)];
    }

    public HeroSO GetSelectedHero() => GetSelectedHero(SelectionSide.Player);

    public HeroSO GetSelectedHero(SelectionSide side)
    {
        int index = SaveManager.Instance.GetSelectedHeroIndex(side);

        // "Random Hero" was chosen in the menu: roll a real hero now (once, at game setup).
        // Each side rolls independently.
        if (index == RandomHeroIndex)
            return GetRandomHero();

        return GetHeroByIndex(index);
    }
}
