using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The card selection panel: draws whatever choice <see cref="CardChoice"/> currently has open and routes
/// a click on an option back to it. Holds no game rules of its own — it is a view over the request, so a
/// different presentation can be swapped in without touching the core.
///
/// Options are spawned from a display-only card prefab (CardPreview Variant: Button + CardModal +
/// CardView). Deliberately NOT the full Card prefab, which carries CardController + DraggableItem and
/// would let the player drag a mid-selection option onto the board.
///
/// The panel GameObject is authored DISABLED and is only enabled while a selection is open. That rules
/// out subscribing from Awake/OnEnable — neither runs on a disabled object — so the wiring lives in a
/// static bootstrap instead: <see cref="Bootstrap"/> subscribes once per play session and resolves the
/// (possibly disabled) panel lazily when a request actually opens.
/// </summary>
public class CardSelectionPanel : MonoBehaviour
{
    [Tooltip("Parent the option cards are spawned under; drives their layout.")]
    [SerializeField] private Transform optionsContainer;

    [SerializeField] private TextMeshProUGUI promptText;

    [Tooltip("Display-only card prefab used for each option (CardPreview Variant).")]
    [SerializeField] private GameObject optionPrefab;

    [Tooltip("How long each option takes to pop in, and the stagger between them.")]
    [SerializeField] private float optionRevealDuration = 0.25f;
    [SerializeField] private float optionRevealStagger = 0.08f;

    [Tooltip("How much a hovered option swells. The layout group doesn't control child size, so growing a card shifts nothing around it.")]
    [SerializeField] private float hoverScale = 1.2f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private static CardSelectionPanel _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Remove-then-add is idempotent (removing an unsubscribed handler is a no-op), so this stays
        // correct if the bootstrap runs again without a domain reload — e.g. with Enter Play Mode Options
        // set to skip it, where the statics survive between sessions.
        CardChoice.Opened -= HandleOpened;
        CardChoice.Closed -= HandleClosed;
        CardChoice.Opened += HandleOpened;
        CardChoice.Closed += HandleClosed;
    }

    private static void HandleOpened(IReadOnlyList<CardSO> options, string prompt)
    {
        var panel = Resolve();
        if (panel != null) panel.Open(options, prompt);
    }

    private static void HandleClosed()
    {
        var panel = Resolve();
        if (panel != null) panel.Close();
    }

    /// <summary>
    /// The panel in the current scene, disabled or not. Re-found whenever the cached one has gone away,
    /// so reloading the match scene (Replay) picks up the new scene's panel instead of a destroyed one —
    /// Unity's overloaded == reports a destroyed object as null, which is what drives the re-find.
    /// </summary>
    private static CardSelectionPanel Resolve()
    {
        if (_instance == null)
        {
            var found = FindObjectsOfType<CardSelectionPanel>(true);
            _instance = found.Length > 0 ? found[0] : null;
        }
        return _instance;
    }

    /// <summary>Show the panel with these options. Public so the panel can be driven directly, the way
    /// PopupManager exposes OpenPopup/CloseCurPopup.</summary>
    public void Open(IReadOnlyList<CardSO> options, string prompt)
    {
        Clear();

        if (optionsContainer == null || optionPrefab == null)
        {
            Debug.LogWarning("CardSelectionPanel: not wired up (options container / prefab missing)");
            return;
        }

        if (promptText != null)
            promptText.text = string.IsNullOrEmpty(prompt) ? "Choose a card" : prompt;

        // Shown before spawning so the layout group is active and positions the options as they arrive.
        gameObject.SetActive(true);

        var owner = GameManager.Instance != null ? GameManager.Instance.player : null;

        for (int i = 0; i < options.Count; i++)
        {
            CardSO option = options[i]; // captured per iteration for the click closure
            if (option == null) continue;

            GameObject go = Instantiate(optionPrefab, optionsContainer);
            go.SetActive(true);
            _spawned.Add(go);

            var modal = go.GetComponent<CardModal>();
            var view = go.GetComponent<CardView>();
            if (modal != null)
            {
                // Shown as the player's own card so the view renders the full face (cost, stats, art)
                // rather than the opponent's card back.
                modal.UpdateModal(option, owner, true);
                if (view != null)
                {
                    view.UpdateView(modal);

                    // Same outlines as a card in hand. Playable is set once rather than per-frame: the
                    // panel blocks play while it is open, so available mana can't move underneath it.
                    view.SetPlayableOutline(CardView.IsPlayableNow(modal));
                    view.SetHoveredOutline(false);

                    // CardPreview Variant has no EventTrigger/CardController, so hover needs its own
                    // source here (see CardHoverOutline).
                    var hover = go.GetComponent<CardHoverOutline>();
                    if (hover == null) hover = go.AddComponent<CardHoverOutline>();
                    // Rest scale passed explicitly: the reveal tween below starts the card at zero, so the
                    // component must not infer its resting size from the transform.
                    hover.ConfigureHoverScale(hoverScale, Vector3.one);
                }
            }

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => CardChoice.Instance.TryResolveClick(option));
            }

            // Stagger the reveal so the options read as being dealt out rather than appearing at once.
            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(1f, optionRevealDuration).SetDelay(i * optionRevealStagger);
        }
    }

    /// <summary>Clear the options and hide the panel again.</summary>
    public void Close()
    {
        Clear();
        gameObject.SetActive(false);
    }

    private void Clear()
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
