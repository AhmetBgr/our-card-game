using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A game mode: the rules it starts from and the modifiers its setup screen exposes. A new mode is a
/// new asset listing whichever modifiers it wants -- the popup, the deck panel and the match read the
/// list through <see cref="MatchModifiers"/> and need no change.
/// </summary>
[CreateAssetMenu(fileName = "GameMode", menuName = "Game Modes/Game Mode")]
public class GameModeSO : ScriptableObject
{
    [Tooltip("Stable key the saved modifier profile is stored under. Never rename once shipped.")]
    public string modeId;

    public string displayName;

    [Tooltip("Deck rules before any modifier touches them.")]
    public DeckRules baseDeckRules = DeckRules.Standard(10);

    [Tooltip("Shown in the modifiers popup in this order.")]
    public List<GameModifierSO> modifiers = new List<GameModifierSO>();
}
