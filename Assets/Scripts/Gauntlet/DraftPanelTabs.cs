using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The review tabs across the top of the end-of-match panel during a Forged in Battle run, so the
/// rewards screen can be read against the state of the run without leaving for the Draft scene.
///
/// Two tabs:
///   * Loadout -- the run's hero and deck. This is the Game scene's own <see cref="BattleRewardLoadout"/>,
///     the one <see cref="BattleRewardsPanel"/> shows while a reward card is picked, resolved at runtime
///     because a prefab cannot reference a scene object. It is shown without its panel shift: the tab
///     shows it on its own, with no card options to make room for. A reward pick taken while that tab is
///     up hides the loadout from under it, so the tab needs a second click to bring it back;
///   * Next Match -- the battle track and the upcoming battle's briefing together, the Draft scene's own
///     layouts (Assets/prefabs/UI/Draft) nested under the end-game panel and authored DISABLED. They are
///     inert copies -- no skull is clickable here -- so this only fills them in, with the same helpers
///     the Draft scene fills its own with (<see cref="GauntletBattleTrack"/>, <see cref="GauntletNextMatch"/>).
///
/// The run's milestones are not a tab: the end-game panel already carries its own
/// <see cref="BattleXpTrack"/> strip, which fills as the battle's score is tallied.
///
/// Outside a run the bar hides itself: a Quick Play or Custom Game ending gets the plain end-of-match
/// panel it has always had. The whole bar is also behind <c>tabsShown</c>, off for now, so it stays out of
/// the way until the tabs are finished.
/// </summary>
public class DraftPanelTabs : MonoBehaviour
{
    /// <summary>Which panel a tab opens. Drives how the panel is filled when it opens.</summary>
    public enum PanelKind
    {
        Loadout,
        NextMatch,
    }

    /// <summary>One tab: its button, and the panel that button opens.</summary>
    [System.Serializable]
    private class Tab
    {
        public PanelKind kind;
        public ToggleButton button;

        [Tooltip("The panel this tab opens. Left empty for Loadout: the scene's own BattleRewardLoadout " +
                 "is used instead, because a prefab cannot reference a scene object.")]
        public GameObject panel;
    }

    /// <summary>One skull on the read-only copy of the battle track.</summary>
    [System.Serializable]
    private class BattleNode
    {
        public Graphic skull;
        [Tooltip("Shown on the upcoming battle.")]
        public GameObject highlight;
        [Tooltip("Shown on battles already won.")]
        public GameObject defeatedMark;
    }

    [Header("Bar")]
    [Tooltip("Off while the tabs are still being built out: the bar never shows, and a run's end-of-match " +
             "panel reads exactly like a Quick Play one.")]
    [SerializeField] private bool tabsShown;

    [Tooltip("The row of tab buttons. Hidden outside a run.")]
    [SerializeField] private GameObject bar;

    [Tooltip("Dimmer behind an open panel, so the panel reads over the match stats. Optional.")]
    [SerializeField] private GameObject backdrop;

    [SerializeField] private Tab[] tabs;

    [Header("Next match: battle track")]
    [SerializeField] private BattleNode[] battleNodes;

    [Tooltip("Skull tint once its battle is won; the Draft scene's track uses the same one.")]
    [SerializeField] private Color beatenSkullTint = new Color(0.396f, 0.396f, 0.396f, 1f);

    [Header("Next match: briefing")]
    [SerializeField] private GauntletHeroPanelView enemyHero;
    [SerializeField] private TextMeshProUGUI nextMatchHeaderText;
    [SerializeField] private TextMeshProUGUI nextMatchRulesText;

    private BattleRewardLoadout _loadout;
    private float _rulesFontSize;

    /// <summary>
    /// The scene's loadout panel, found the way <see cref="BattleRewardsPanel.Resolve"/> finds itself:
    /// it is authored disabled, so it cannot be dragged into a prefab's inspector.
    /// </summary>
    private BattleRewardLoadout Loadout
    {
        get
        {
            if (_loadout == null)
            {
                var found = FindObjectsOfType<BattleRewardLoadout>(true);
                _loadout = found.Length > 0 ? found[0] : null;
            }
            return _loadout;
        }
    }

    // The end-game panel is authored disabled, so this runs the first time it opens, before OnEnable.
    private void Awake()
    {
        if (nextMatchRulesText != null) _rulesFontSize = nextMatchRulesText.fontSize;

        if (tabs == null) return;
        foreach (var tab in tabs)
        {
            if (tab == null || tab.button == null) continue;

            var captured = tab;
            tab.button.onValueChanged.AddListener(on => OnTabToggled(captured, on));
        }
    }

    private void OnEnable()
    {
        // The tabs only mean anything in a run: there is no deck, hero or track to review otherwise.
        if (bar != null) bar.SetActive(tabsShown && GauntletRun.IsActive);
        CloseAll();
    }

    /// <summary>Drops every panel and un-latches every button, leaving the plain end-of-match panel.</summary>
    public void CloseAll()
    {
        if (tabs != null)
            foreach (var tab in tabs)
            {
                if (tab == null) continue;

                if (tab.button != null) tab.button.SetIsOn(false, notify: false);
                SetPanelShown(tab, false);
            }

        if (backdrop != null) backdrop.SetActive(false);
    }

    /// <summary>The tabs are exclusive: opening one drops whichever was up.</summary>
    private void OnTabToggled(Tab tab, bool on)
    {
        if (on && tabs != null)
            foreach (var other in tabs)
            {
                if (other == null || other == tab) continue;

                if (other.button != null) other.button.SetIsOn(false, notify: false);
                SetPanelShown(other, false);
            }

        SetPanelShown(tab, on);
        if (backdrop != null) backdrop.SetActive(on);
    }

    private void SetPanelShown(Tab tab, bool shown)
    {
        if (tab.kind == PanelKind.Loadout)
        {
            var loadout = Loadout;
            if (loadout == null) return;

            if (shown) loadout.Show(shiftPanels: false);
            else loadout.Hide();
            return;
        }

        if (tab.panel == null) return;

        // Filled on the way up, so a reward just taken is already in what the panel shows.
        if (shown) FillNextMatch();
        tab.panel.SetActive(shown);
    }

    /// <summary>The track the run has fought through, and the battle it is about to fight.</summary>
    private void FillNextMatch()
    {
        if (battleNodes != null)
            for (int i = 0; i < battleNodes.Length; i++)
            {
                var node = battleNodes[i];
                if (node == null) continue;

                GauntletBattleTrack.RefreshNode(node.skull, node.highlight, node.defeatedMark, i, beatenSkullTint);
            }

        GauntletNextMatch.Refresh(enemyHero, nextMatchHeaderText, nextMatchRulesText, _rulesFontSize);
    }
}
