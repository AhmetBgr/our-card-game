using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Draft scene of Forged in Battle. Every visit to the scene shows whichever step the
/// <see cref="GauntletRun"/> is on:
///   1. no hero yet: pick 1 of N heroes;
///   2. card picks owed: pick 1 of N base cards, until the step's picks are done;
///   3. nothing owed: the next battle's briefing and a Fight button;
///   4. run over: victory or defeat, with New Run and Main Menu.
/// Entering the scene with no run active (from the menu, or opened directly in the editor) starts one.
///
/// Options are drawn with the same display-only card prefab the in-match card choice uses
/// (CardPreview Variant); a HeroSO is a CardSO, so heroes render through it too, passive text and all.
/// </summary>
public class DraftController : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [Tooltip("Run progress and the hero's carried statline.")]
    [SerializeField] private TextMeshProUGUI statusText;
    [Tooltip("The deck drafted so far.")]
    [SerializeField] private TextMeshProUGUI deckText;

    [Header("Options")]
    [Tooltip("Parent the option cards are spawned under; drives their layout.")]
    [SerializeField] private Transform optionsContainer;
    [Tooltip("Display-only card prefab used for each option (CardPreview Variant).")]
    [SerializeField] private GameObject optionPrefab;
    [SerializeField] private float baseCardScale = 1f;
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float optionRevealDuration = 0.25f;
    [SerializeField] private float optionRevealStagger = 0.08f;

    [Header("Buttons")]
    [Tooltip("Fight on the briefing step; New Run on the result step.")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private TextMeshProUGUI primaryButtonLabel;
    [Tooltip("Abandons the run and goes back to the main menu.")]
    [SerializeField] private Button menuButton;
    [SerializeField] private TextMeshProUGUI menuButtonLabel;
    [SerializeField] private string leaveRunLabel = "Leave Run";
    [SerializeField] private string mainMenuLabel = "Main Menu";

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _leaving;

    void Start()
    {
        // The draft runs on the plain rules: no Custom Game modifier may leak into the run's matches.
        MatchModifiers.Clear();

        if (!GauntletRun.IsActive) GauntletRun.StartNew();

        if (menuButton != null) menuButton.onClick.AddListener(ExitToMenu);
        if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimary);

        Refresh();
    }

    // ---------------------------------------------------------------- Steps

    private void Refresh()
    {
        ClearOptions();
        RefreshStatus();
        RefreshDeck();

        if (menuButtonLabel != null)
            menuButtonLabel.text = GauntletRun.Outcome == GauntletRun.RunOutcome.InProgress ? leaveRunLabel : mainMenuLabel;

        if (GauntletRun.Outcome != GauntletRun.RunOutcome.InProgress) ShowResult();
        else if (GauntletRun.Hero == null) ShowHeroPick();
        else if (GauntletRun.PendingPicks > 0) ShowCardPick();
        else ShowBriefing();
    }

    private void ShowHeroPick()
    {
        SetTitle("Choose your hero", "Your hero stays with you for the whole run.");
        SetPrimary(null);

        SpawnOptions(GauntletRun.RollHeroOptions(), picked =>
        {
            GauntletRun.SetHero((HeroSO)picked);
            Refresh();
        });
    }

    private void ShowCardPick()
    {
        int pickNumber = GauntletRun.PicksThisStep - GauntletRun.PendingPicks + 1;
        string subtitle = GauntletRun.EncounterIndex == 0
            ? "Build your starting deck."
            : "Victory! Add new cards to your deck.";
        SetTitle($"Choose a card ({pickNumber}/{GauntletRun.PicksThisStep})", subtitle);
        SetPrimary(null);

        SpawnOptions(GauntletRun.RollCardOptions(), picked =>
        {
            GauntletRun.AddDraftedCard(picked);
            Refresh();
        });
    }

    private void ShowBriefing()
    {
        var encounter = GauntletRun.CurrentEncounter;
        string label = encounter != null && !string.IsNullOrEmpty(encounter.label)
            ? encounter.label
            : $"Battle {GauntletRun.EncounterIndex + 1}";
        string health = encounter != null
            ? $"Enemy hero health: {Mathf.RoundToInt(encounter.enemyHealthMultiplier * 100f)}%"
            : string.Empty;

        SetTitle(label, health);
        SetPrimary("Fight!");
    }

    private void ShowResult()
    {
        bool won = GauntletRun.Outcome == GauntletRun.RunOutcome.Won;
        SetTitle(won ? "Gauntlet conquered!" : "Defeated",
            won
                ? $"You beat all {GauntletRun.EncounterCount} enemies."
                : $"Your run ended in battle {GauntletRun.EncounterIndex + 1} of {GauntletRun.EncounterCount}.");
        SetPrimary("New Run");
    }

    private void OnPrimary()
    {
        if (_leaving) return;

        if (GauntletRun.Outcome != GauntletRun.RunOutcome.InProgress)
        {
            GauntletRun.StartNew();
            Refresh();
            return;
        }

        if (GauntletRun.Hero == null || GauntletRun.PendingPicks > 0) return;

        _leaving = true;
        SetButtonsInteractable(false);
        GauntletRun.PrepareNextBattle();
        GauntletRun.LoadScene(GauntletRun.Config.gameSceneName);
    }

    private void ExitToMenu()
    {
        if (_leaving) return;

        _leaving = true;
        SetButtonsInteractable(false);
        string menu = GauntletRun.Config != null ? GauntletRun.Config.menuSceneName : "MainMenu";
        GauntletRun.End();
        GauntletRun.LoadScene(menu);
    }

    // ---------------------------------------------------------------- View

    private void SetTitle(string title, string subtitle)
    {
        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;
    }

    /// <summary>Shows the primary button with this label, or hides it for null.</summary>
    private void SetPrimary(string label)
    {
        if (primaryButton == null) return;

        primaryButton.gameObject.SetActive(label != null);
        if (label != null && primaryButtonLabel != null) primaryButtonLabel.text = label;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (primaryButton != null) primaryButton.interactable = interactable;
        if (menuButton != null) menuButton.interactable = interactable;
        foreach (var go in _spawned)
            if (go != null && go.TryGetComponent(out Button b)) b.interactable = interactable;
    }

    private void RefreshStatus()
    {
        if (statusText == null) return;

        var sb = new StringBuilder();
        int battle = Mathf.Min(GauntletRun.EncounterIndex + 1, GauntletRun.EncounterCount);
        sb.Append($"Battle {battle} / {GauntletRun.EncounterCount}");

        if (GauntletRun.Hero != null && GauntletRun.TryGetCarriedStats(out int hp, out int maxHp, out int atk))
            sb.Append($"\n{GauntletRun.Hero.cardName}\n<color=#FF6B6B>{hp}/{maxHp} HP</color>   <color=#FFD36B>{atk} ATK</color>");

        statusText.text = sb.ToString();
    }

    private void RefreshDeck()
    {
        if (deckText == null) return;

        var deck = GauntletRun.Deck;
        if (deck.Count == 0)
        {
            deckText.text = "Deck: empty";
            return;
        }

        // Group duplicates in pick order: "Fireball x2".
        var counts = new Dictionary<CardSO, int>();
        var order = new List<CardSO>();
        foreach (var card in deck)
        {
            if (card == null) continue;
            if (counts.TryGetValue(card, out int c)) counts[card] = c + 1;
            else { counts[card] = 1; order.Add(card); }
        }

        var sb = new StringBuilder($"Deck ({deck.Count})");
        foreach (var card in order)
        {
            sb.Append($"\n[{card.cost}] {card.cardName}");
            if (counts[card] > 1) sb.Append($" x{counts[card]}");
        }
        deckText.text = sb.ToString();
    }

    private void SpawnOptions<T>(List<T> options, System.Action<CardSO> onPicked) where T : CardSO
    {
        if (optionsContainer == null || optionPrefab == null)
        {
            Debug.LogWarning("[Gauntlet] DraftController is not wired up (options container / prefab missing).");
            return;
        }

        var baseScale = Vector3.one * baseCardScale;

        for (int i = 0; i < options.Count; i++)
        {
            CardSO option = options[i];
            if (option == null) continue;

            GameObject go = Instantiate(optionPrefab, optionsContainer);
            go.SetActive(true);
            _spawned.Add(go);

            var modal = go.GetComponent<CardModal>();
            var view = go.GetComponent<CardView>();
            if (modal != null)
            {
                modal.UpdateModal(option, null, true);
                if (view != null)
                {
                    view.UpdateView(modal);
                    view.SetPlayableOutline(true);
                    view.SetHoveredOutline(false);

                    var hover = go.GetComponent<CardHoverOutline>();
                    if (hover == null) hover = go.AddComponent<CardHoverOutline>();
                    hover.ConfigureHoverScale(hoverScale, baseScale);
                }
            }

            if (go.TryGetComponent(out Button button))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (_leaving) return;
                    onPicked(option);
                });
            }

            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(baseCardScale, optionRevealDuration).SetDelay(i * optionRevealStagger);
        }
    }

    private void ClearOptions()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            go.transform.DOKill();
            Destroy(go);
        }
        _spawned.Clear();
    }
}
