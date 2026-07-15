using UnityEngine;

/// <summary>
/// Marks a subtree of the selection UI as belonging to one <see cref="SelectionSide"/>.
/// Sits on the container that holds a DeckPanel + HeroSelectionPanel pair (ChooseDeckAndHero /
/// ChooseDeckAndHero-Opponent), letting both panels — and everything under them — discover which
/// side's save data they operate on without any singleton or per-instance wiring.
/// </summary>
public class DeckSelectionContext : MonoBehaviour
{
    [Tooltip("Which side the panels under this container select for.")]
    [SerializeField] private SelectionSide side = SelectionSide.Player;

    public SelectionSide Side => side;

    private DeckPanelController deckPanel;

    /// <summary>The deck panel in this container. Used by the sibling hero panel for card previews.</summary>
    public DeckPanelController DeckPanel
    {
        get
        {
            if (deckPanel == null)
                deckPanel = GetComponentInChildren<DeckPanelController>(true);

            return deckPanel;
        }
    }

    /// <summary>
    /// The side of the nearest context above <paramref name="component"/>, or Player when there is
    /// none. The fallback is what lets scenes with a single, un-contextualized panel (Menu) keep
    /// working unchanged.
    /// </summary>
    public static SelectionSide SideOf(Component component)
    {
        if (component == null)
            return SelectionSide.Player;

        var context = component.GetComponentInParent<DeckSelectionContext>(true);
        return context != null ? context.Side : SelectionSide.Player;
    }
}
