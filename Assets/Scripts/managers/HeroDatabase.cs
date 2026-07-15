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

    public List<HeroSO> AllHeroes = new List<HeroSO>();

    protected override void Awake()
    {
        base.Awake();
        LoadHeroes();
    }

    void LoadHeroes()
    {
        AllHeroes.Clear();

        // Resources.LoadAll recurses into subfolders but only returns HeroSO assets, so the
        // Passives/ subfolder (HeroPassiveSO) is naturally excluded.
        HeroSO[] heroes = Resources.LoadAll<HeroSO>("Heroes");

        AllHeroes.AddRange(heroes);

        // Resources load order is not guaranteed; sort by asset name so SelectedHeroIndex maps
        // to the same hero across sessions and between the Menu and Game scenes.
        AllHeroes.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        Debug.Log($"Loaded {AllHeroes.Count} heroes into HeroDatabase.");
    }

    public HeroSO GetHeroByIndex(int index)
    {
        if (AllHeroes.Count == 0)
            return null;

        index = Mathf.Clamp(index, 0, AllHeroes.Count - 1);
        return AllHeroes[index];
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
