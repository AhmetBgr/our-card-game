using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hero is a card with passives. Subclassing CardSO keeps HeroController.card, CardModal.UpdateModal,
/// MinionView and RangeUtility working untouched.
///
/// Hero assets live in Assets/Resources/Heroes, which DeckDatabase.LoadCards never scans (it is
/// hard-scoped to Assets/Resources/Cards), so a hero can never be picked by SummonRandomMinion.
/// </summary>
[CreateAssetMenu(fileName = "Hero", menuName = "Cards/Hero")]
public class HeroSO : CardSO
{
    [Header("Hero")]
    public string heroName;
    public HeroArchetype archetype = HeroArchetype.None;

    [Tooltip("Registered by HeroPassiveSystem at game setup. Order is the order they resolve in.")]
    public List<HeroPassiveSO> passives = new List<HeroPassiveSO>();
}
