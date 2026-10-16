using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// One hero panel of a Forged in Battle layout: name plate, portrait with stats, passive chips. Shared by
/// the Draft scene (<see cref="DraftController"/>) and the card picks of the end-of-match rewards
/// (<see cref="BattleRewardLoadout"/>), so both show the run's hero the same way.
/// </summary>
[System.Serializable]
public class GauntletHeroPanelView
{
    public GameObject root;
    public TextMeshProUGUI nameText;
    public HeroButtonHandler portrait;
    [Tooltip("Overwritten after the portrait fills in, with the health the hero will actually start at.")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI attackText;
    [Tooltip("Filled in order with the hero's passives; chips past the last passive keep their empty look.")]
    public PassiveChipButton[] passiveChips;

    /// <summary>Resolves the parts of a spawned HeroPanel prefab instance. Stats keep the printed values.</summary>
    public static GauntletHeroPanelView FromInstance(GameObject go)
    {
        var nameBg = go.transform.Find("NameBG");
        return new GauntletHeroPanelView
        {
            root = go,
            nameText = nameBg != null ? nameBg.GetComponentInChildren<TextMeshProUGUI>(true) : null,
            portrait = go.GetComponentInChildren<HeroButtonHandler>(true),
            passiveChips = go.GetComponentsInChildren<PassiveChipButton>(true),
        };
    }

    /// <summary>
    /// Shows <paramref name="hero"/> ("???" and a random portrait when null) with the given stats.
    /// <paramref name="passives"/> are shown in place of the hero's own when given (a blessed enemy's).
    /// </summary>
    public void Show(HeroSO hero, int health, int attack, IReadOnlyList<HeroPassiveSO> passives = null)
    {
        if (nameText != null) nameText.text = hero != null ? hero.cardName : "???";

        if (portrait != null)
        {
            if (hero != null) portrait.SetHero(hero);
            else portrait.SetRandom(null);
        }

        if (hero != null)
        {
            if (healthText != null) healthText.text = health.ToString();
            if (attackText != null) attackText.text = attack.ToString();
        }

        if (passiveChips == null) return;
        if (passives == null && hero != null) passives = hero.passives;
        for (int i = 0; i < passiveChips.Length; i++)
        {
            var chip = passiveChips[i];
            if (chip == null) continue;
            chip.ShowPassive(passives != null && i < passives.Count ? passives[i] : null);
        }
    }

    /// <summary>The run's hero as it will start the next battle: carried stats, own passives and drafted perks.</summary>
    public void ShowRunHero()
    {
        var hero = GauntletRun.Hero;
        int health = 0, attack = 0;
        if (hero != null && !GauntletRun.TryGetCarriedStats(out health, out _, out attack))
        {
            health = hero.health;
            attack = hero.attack;
        }
        Show(hero, health, attack, hero != null ? GauntletRun.PlayerPassives : null);
    }
}

/// <summary>
/// The run's deck as a list of entries (cost and name), cloned from an authored template entry. Shared by
/// the Draft scene and the card picks of the end-of-match rewards.
/// </summary>
public static class GauntletDeckList
{
    /// <summary>One entry per card, by mana cost then name; duplicates get their own row.</summary>
    public static void Refresh(TextMeshProUGUI header, CardButtonHandler template, List<CardButtonHandler> entries,
        IReadOnlyList<CardSO> deck)
    {
        if (header != null) header.text = deck.Count > 0 ? $"DECK ({deck.Count})" : "DECK";
        if (template == null) return;

        var cards = new List<CardSO>(deck.Count);
        foreach (var card in deck)
            if (card != null) cards.Add(card);
        cards.Sort((a, b) =>
        {
            int byCost = a.cost.CompareTo(b.cost);
            return byCost != 0 ? byCost : string.CompareOrdinal(a.cardName, b.cardName);
        });

        while (entries.Count < cards.Count)
        {
            var entry = Object.Instantiate(template, template.transform.parent);
            entries.Add(entry);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            bool used = i < cards.Count;
            entry.gameObject.SetActive(used);
            if (!used) continue;

            var card = cards[i];
            entry.Card = card;
            entry.SetName(card.cardName);
            entry.SetCost(card.cost);
        }
    }

    /// <summary>Removes the example entries the layout was authored with; the template is kept, hidden, to clone from.</summary>
    public static void ClearPlaceholders(CardButtonHandler template)
    {
        if (template == null) return;

        var parent = template.transform.parent;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (child != template.gameObject) Object.Destroy(child);
        }
        template.gameObject.SetActive(false);
    }
}
