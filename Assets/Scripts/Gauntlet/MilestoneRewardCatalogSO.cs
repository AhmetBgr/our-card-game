using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How every Forged in Battle milestone reward reads and looks: its name on the track node and on the
/// rewards row, what it says it did once it has been claimed, the line it falls back to when it had
/// nothing left to give, and the icon its row carries. One entry per <see cref="MilestoneReward"/>.
///
/// This is presentation only -- what a reward actually does, and how much of it, stays in
/// <see cref="GauntletConfigSO"/>. The two meet through <c>{0}</c>: every line here is a format string,
/// and <c>{0}</c> is the reward's amount out of that config (the health it grants, the cards it offers,
/// ...) or, for the rewards that name something they touched, that name. Each line's tooltip says which.
///
/// The cards a won battle pays out are in here too (<see cref="BattleCards"/>), even though they are not
/// a milestone: they are the one other thing the rewards panel lists, and splitting their wording off
/// somewhere else would defeat the point of the asset.
///
/// Every line is optional. Left empty, a milestone's falls back to the built-in wording below and a
/// battle-cards line falls back to whatever <see cref="BattleRewardsPanel"/> has in its inspector --
/// which is what the game read before this asset existed. So a half-filled catalog is a perfectly good
/// catalog, and a missing one changes nothing. Loaded from <see cref="ResourcePath"/>, like the other
/// Resources-backed configs.
/// </summary>
[CreateAssetMenu(fileName = "MilestoneRewardCatalog", menuName = "Game Modes/Milestone Reward Catalog")]
public class MilestoneRewardCatalogSO : ScriptableObject
{
    public const string ResourcePath = "Gauntlet/MilestoneRewardCatalog";

    /// <summary>One reward's wording and look.</summary>
    [Serializable]
    public class Entry
    {
        public MilestoneReward reward;

        [Header("Wording")]
        [Tooltip("Short name, on the track node and at the head of the reward's row. {0} is the amount.")]
        public string displayName = "";

        [Tooltip("What the reward does, for its preview. {0} is the amount.")]
        [TextArea(2, 3)] public string description = "";

        [Tooltip("What the row reads once the reward has been taken. {0} is the amount, except on " +
                 "Forge Perk (the perk forged) and Duplicate (the card copied).")]
        public string acquired = "";

        [Tooltip("Instead of 'acquired' when the reward had nothing left to give -- no perk to forge, no " +
                 "card to copy. Only Forge Perk, New Perk and Duplicate can land here.")]
        public string unavailable = "";

        [Header("Singular wording")]
        [Tooltip("Used in place of the lines above when the amount is exactly 1. Leave empty to use them " +
                 "whatever the amount.")]
        public string displayNameOne = "";
        [TextArea(2, 3)] public string descriptionOne = "";
        public string acquiredOne = "";

        [Header("Look")]
        [Tooltip("Icon on this reward's row. Empty falls back to the shared milestone icon on " +
                 "BattleRewardsPanel.")]
        public Sprite icon;

        [Tooltip("Tint for that icon. Ignored while the icon above is empty: the shared one keeps the " +
                 "shared tint.")]
        public Color iconTint = Color.white;
    }

    /// <summary>
    /// The cards a won battle is worth -- the rewards panel's other row. Not a
    /// <see cref="MilestoneReward"/>: winning the battle is what pays for it, not the XP track.
    /// </summary>
    [Serializable]
    public class CardRewardEntry
    {
        [Header("Wording")]
        [Tooltip("The row while the picks are still owed. {0} is how many cards the win is worth.")]
        public string offered = "";

        [Tooltip("The row once they have been drafted. {0} is how many cards.")]
        public string acquired = "";

        [Tooltip("Prompt over the card options while one is being picked. {0} is the pick being made, " +
                 "{1} how many the reward is worth.")]
        public string pickPrompt = "";

        [Header("When the win is worth one card")]
        [Tooltip("Used in place of the lines above when there is a single card. Leave empty to use them " +
                 "whatever the count.")]
        public string offeredOne = "";
        public string acquiredOne = "";

        [Header("Look")]
        [Tooltip("Icon on the row. Empty falls back to the card icon on BattleRewardsPanel.")]
        public Sprite icon;

        [Tooltip("Tint for that icon. Ignored while the icon above is empty.")]
        public Color iconTint = Color.white;
    }

    [Tooltip("One per reward. A reward with no entry here reads with the built-in wording.")]
    [SerializeField] private List<Entry> rewards = new List<Entry>();

    [Header("Battle cards")]
    [Tooltip("The cards a won battle pays out. Every line left empty keeps whatever BattleRewardsPanel " +
             "has in its own inspector.")]
    [SerializeField] private CardRewardEntry battleCards = new CardRewardEntry();

    private static MilestoneRewardCatalogSO _cached;

    /// <summary>The catalog asset, or an empty one (all built-in wording) if the asset is missing.</summary>
    public static MilestoneRewardCatalogSO Load()
    {
        if (_cached != null) return _cached;

        _cached = Resources.Load<MilestoneRewardCatalogSO>(ResourcePath);
        if (_cached == null) _cached = CreateInstance<MilestoneRewardCatalogSO>();
        return _cached;
    }

    /// <summary>This reward's entry, or null when the catalog has none for it.</summary>
    public Entry Find(MilestoneReward reward)
    {
        if (rewards == null) return null;

        foreach (var entry in rewards)
            if (entry != null && entry.reward == reward) return entry;
        return null;
    }

    /// <param name="amount">What the reward is worth, from the config; see <see cref="GauntletConfigSO.RewardAmount"/>.</param>
    public string Name(MilestoneReward reward, int amount)
    {
        var entry = Find(reward);
        string line = Pick(entry?.displayName, entry?.displayNameOne, amount);
        return Fill(!string.IsNullOrEmpty(line) ? line : DefaultName(reward, amount), amount);
    }

    public string Description(MilestoneReward reward, int amount)
    {
        var entry = Find(reward);
        string line = Pick(entry?.description, entry?.descriptionOne, amount);
        return Fill(!string.IsNullOrEmpty(line) ? line : DefaultDescription(reward, amount), amount);
    }

    /// <param name="subject">
    /// What <c>{0}</c> stands for when the reward names something it touched -- the perk Forge Perk
    /// forged, the card Duplicate copied. Left null, <c>{0}</c> is <paramref name="amount"/>.
    /// </param>
    public string Acquired(MilestoneReward reward, int amount, string subject = null)
    {
        var entry = Find(reward);
        string line = Pick(entry?.acquired, entry?.acquiredOne, amount);
        if (string.IsNullOrEmpty(line)) line = DefaultAcquired(reward);
        return Fill(line, (object)subject ?? amount);
    }

    public string Unavailable(MilestoneReward reward)
    {
        var entry = Find(reward);
        return !string.IsNullOrEmpty(entry?.unavailable) ? entry.unavailable : DefaultUnavailable(reward);
    }

    /// <summary>This reward's own icon, or null to keep whichever icon the row would have used.</summary>
    public Sprite Icon(MilestoneReward reward) => Find(reward)?.icon;

    /// <summary>The tint that goes with <see cref="Icon"/>; <paramref name="fallback"/> when it has none.</summary>
    public Color IconTint(MilestoneReward reward, Color fallback)
    {
        var entry = Find(reward);
        return entry != null && entry.icon != null ? entry.iconTint : fallback;
    }

    // ---------------------------------------------------------------- Battle cards

    /// <summary>The won-battle card reward's wording and look; never null.</summary>
    public CardRewardEntry BattleCards => battleCards ?? (battleCards = new CardRewardEntry());

    /// <param name="cards">How many cards the win is worth.</param>
    /// <returns>The authored line, or empty to keep the rewards panel's own wording.</returns>
    public string BattleCardsOffered(int cards) =>
        Fill(Pick(BattleCards.offered, BattleCards.offeredOne, cards), cards);

    /// <returns>The authored line, or empty to keep the rewards panel's own wording.</returns>
    public string BattleCardsAcquired(int cards) =>
        Fill(Pick(BattleCards.acquired, BattleCards.acquiredOne, cards), cards);

    /// <param name="pick">The pick being made, counting from 1.</param>
    /// <param name="of">How many picks the reward is worth.</param>
    /// <returns>The authored line, or empty to keep the rewards panel's own wording.</returns>
    public string BattleCardsPrompt(int pick, int of) => Fill(BattleCards.pickPrompt, pick, of);

    // ---------------------------------------------------------------- Shared

    /// <summary>The singular wording when there is one of something and the entry spells one out.</summary>
    private static string Pick(string many, string one, int amount) =>
        amount == 1 && !string.IsNullOrEmpty(one) ? one : many;

    /// <summary>
    /// A line is authored, so its <c>{0}</c> may well be mistyped. A bad format string reads as itself
    /// rather than throwing out of whatever was drawing it.
    /// </summary>
    private static string Fill(string line, params object[] args)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;

        try
        {
            return string.Format(line, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[Gauntlet] Milestone reward line '{line}' is not a valid format string.");
            return line;
        }
    }

    // ---------------------------------------------------------------- Built-in wording

    private static string DefaultName(MilestoneReward reward, int amount)
    {
        switch (reward)
        {
            case MilestoneReward.UpgradedCards: return amount == 1 ? "Upgraded Card" : "{0} Upgraded Cards";
            case MilestoneReward.NewPerk: return "New Perk";
            case MilestoneReward.ForgePerk: return "Forge Perk";
            case MilestoneReward.BonusHealth: return "+{0} Health";
            case MilestoneReward.BonusAttack: return "+{0} Attack";
            case MilestoneReward.BiggerHand: return "Bigger Hand";
            case MilestoneReward.DuplicateCard: return "Duplicate";
            case MilestoneReward.None: return "Coming Soon";
            default: return reward.ToString();
        }
    }

    private static string DefaultDescription(MilestoneReward reward, int amount)
    {
        switch (reward)
        {
            case MilestoneReward.UpgradedCards:
                return amount == 1
                    ? "Draft an upgraded card for your deck"
                    : "Draft {0} upgraded cards for your deck";
            case MilestoneReward.NewPerk: return "Draft a new perk for your hero";
            case MilestoneReward.ForgePerk: return "Forge one of your hero's perks at random";
            case MilestoneReward.BonusHealth: return "Your hero gains +{0} health";
            case MilestoneReward.BonusAttack: return "Your hero gains +{0} attack";
            case MilestoneReward.BiggerHand:
                return amount == 1
                    ? "Start every battle with 1 more card in hand"
                    : "Start every battle with {0} more cards in hand";
            case MilestoneReward.DuplicateCard:
                return "Pick 1 of {0} cards from your deck and add a copy of it";
            case MilestoneReward.None: return "Nothing here yet";
            default: return string.Empty;
        }
    }

    private static string DefaultAcquired(MilestoneReward reward)
    {
        switch (reward)
        {
            case MilestoneReward.ForgePerk: return "{0} is forged";
            case MilestoneReward.BonusHealth: return "+{0} Hero Health";
            case MilestoneReward.BonusAttack: return "+{0} Hero Attack";
            case MilestoneReward.BiggerHand: return "+{0} Starting Hand Size";
            case MilestoneReward.DuplicateCard: return "{0} duplicated";
            default: return string.Empty;
        }
    }

    private static string DefaultUnavailable(MilestoneReward reward)
    {
        switch (reward)
        {
            case MilestoneReward.ForgePerk: return "No perk left to forge";
            case MilestoneReward.NewPerk: return "No new perks left to learn";
            case MilestoneReward.DuplicateCard: return "No cards to duplicate";
            default: return string.Empty;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Fills the list with one entry per reward, each pre-written with the built-in wording, so the asset
    /// opens as something to edit rather than something to assemble. Leaves entries already there alone.
    /// </summary>
    [ContextMenu("Fill With Built-In Wording")]
    private void FillWithDefaults()
    {
        if (rewards == null) rewards = new List<Entry>();

        foreach (MilestoneReward reward in Enum.GetValues(typeof(MilestoneReward)))
        {
            if (Find(reward) != null) continue;

            rewards.Add(new Entry
            {
                reward = reward,
                displayName = DefaultName(reward, 2),
                displayNameOne = DefaultName(reward, 1),
                description = DefaultDescription(reward, 2),
                descriptionOne = DefaultDescription(reward, 1),
                acquired = DefaultAcquired(reward),
                unavailable = DefaultUnavailable(reward),
            });
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
