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

    [Tooltip("Resting size of every option card. The reveal tween lands here and hover swells out from here.")]
    [SerializeField] private float baseCardScale = 1f;

    [Tooltip("How much a hovered option swells, on top of the base scale. The layout group doesn't control child size, so growing a card shifts nothing around it.")]
    [SerializeField] private float hoverScale = 1.2f;

    [Header("See Board")]
    [Tooltip("Optional. Toggles the options out of the way so the board underneath can be read. Left " +
             "unassigned, one is built at runtime — same self-installing approach as BoardInteractionGate, " +
             "so the feature needs no scene wiring. Assign your own to restyle it.")]
    [SerializeField] private Button seeBoardButton;

    [SerializeField] private string seeBoardLabel = "See Board";
    [SerializeField] private string showCardsLabel = "Show Cards";

    private Vector3 BaseScale => Vector3.one * baseCardScale;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    // True while the options are tucked away and the player is reading the board.
    private bool _peeking;

    private TextMeshProUGUI _seeBoardButtonLabel;

    /// <summary>
    /// The panel's own full-screen Image: the dimming backdrop, and — because it is a raycast target
    /// covering the whole canvas — the thing that stops uGUI clicks (dragging a card out of hand, the
    /// end-turn button) reaching anything underneath. Peeking drops its alpha to 0 but deliberately
    /// leaves it enabled and raycasting, which is what keeps the player locked out while they look.
    /// </summary>
    private Image _backdrop;
    private Color _backdropColor;
    private bool _backdropCaptured;

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

    private static void HandleOpened(IReadOnlyList<CardSO> options, string prompt, bool faceDown)
    {
        var panel = Resolve();
        if (panel != null) panel.Open(options, prompt, faceDown);
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
    /// PopupManager exposes OpenPopup/CloseCurPopup.
    ///
    /// <paramref name="faceDown"/> draws the options as card backs and makes them inert: that is the
    /// spectator view of the OPPONENT choosing, where the player is being told a choice is happening but
    /// must not see what is on offer or be able to pick from it.</summary>
    public void Open(IReadOnlyList<CardSO> options, string prompt, bool faceDown = false)
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

        CaptureBackdrop();

        // Every request starts with the cards up, however the last one ended.
        _peeking = false;

        // Only an interactive request gets the toggle: a face-down spectator panel closes itself after a
        // beat and asks nothing of the player, so a button to tuck it away would be dead weight.
        SetUpSeeBoardButton(interactive: !faceDown);
        ApplyPeekState();

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
                // isPlayerMinion drives face vs. back in CardView.UpdateView: true renders the full face
                // (cost, stats, art), false renders the card back — which is exactly what the face-down
                // spectator view wants, upgraded back sprite and all.
                modal.UpdateModal(option, owner, !faceDown);
                if (view != null)
                {
                    view.UpdateView(modal);

                    // Same outlines as a card in hand. Playable is set once rather than per-frame: the
                    // panel blocks play while it is open, so available mana can't move underneath it.
                    // A face-down card is not the player's to play, so it gets no playable outline.
                    view.SetPlayableOutline(!faceDown && CardView.IsPlayableNow(modal));
                    view.SetHoveredOutline(false);

                    // CardPreview Variant has no EventTrigger/CardController, so hover needs its own
                    // source here (see CardHoverOutline). Skipped while face down: swelling on hover
                    // would advertise these as pickable when they are not.
                    if (!faceDown)
                    {
                        var hover = go.GetComponent<CardHoverOutline>();
                        if (hover == null) hover = go.AddComponent<CardHoverOutline>();
                        // Rest scale passed explicitly: the reveal tween below starts the card at zero, so
                        // the component must not infer its resting size from the transform.
                        hover.ConfigureHoverScale(hoverScale, BaseScale);
                    }
                }
            }

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                // Belt and braces: CardChoice.TryResolveClick already refuses face-down requests, but an
                // inert button also drops the hover/press tinting that would make these look clickable.
                button.interactable = !faceDown;
                if (!faceDown)
                    button.onClick.AddListener(() => CardChoice.Instance.TryResolveClick(option));
            }

            // Stagger the reveal so the options read as being dealt out rather than appearing at once.
            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(baseCardScale, optionRevealDuration).SetDelay(i * optionRevealStagger);
        }
    }

    /// <summary>Clear the options and hide the panel again.</summary>
    public void Close()
    {
        Clear();

        // Restore the backdrop before going away: Open() resets _peeking, but a panel left transparent
        // would flash the board through on the next request's first frame.
        _peeking = false;
        ApplyPeekState();

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Tuck the options away to read the board, or bring them back. The REQUEST stays open throughout —
    /// this only moves pixels. That is what keeps the player locked out while peeking: BoardInteractionGate
    /// blocks the board on CardChoice.HasActiveRequest (minions, hero, hover feedback), the backdrop keeps
    /// eating uGUI clicks (card drags, end turn), and GameManager.CancelPlayingCard is already inert while
    /// a request is open. Nothing but this button responds.
    /// </summary>
    public void ToggleSeeBoard()
    {
        _peeking = !_peeking;
        ApplyPeekState();
    }

    private void ApplyPeekState()
    {
        if (promptText != null) promptText.gameObject.SetActive(!_peeking);
        if (optionsContainer != null) optionsContainer.gameObject.SetActive(!_peeking);

        if (_backdrop != null && _backdropCaptured)
        {
            // Alpha only — enabled and raycastTarget stay untouched so the invisible backdrop still
            // swallows every click that is not the button itself.
            Color c = _backdropColor;
            _backdrop.color = _peeking ? new Color(c.r, c.g, c.b, 0f) : c;
        }

        if (_seeBoardButtonLabel != null)
            _seeBoardButtonLabel.text = _peeking ? showCardsLabel : seeBoardLabel;
    }

    private void CaptureBackdrop()
    {
        if (_backdrop == null) _backdrop = GetComponent<Image>();

        // Captured once, and only from a non-peeking state (Open resets _peeking before this runs), so a
        // re-open mid-session can never bake the transparent colour in as the "original".
        if (_backdrop != null && !_backdropCaptured)
        {
            _backdropColor = _backdrop.color;
            _backdropCaptured = true;
        }
    }

    private void SetUpSeeBoardButton(bool interactive)
    {
        if (!interactive)
        {
            if (seeBoardButton != null) seeBoardButton.gameObject.SetActive(false);
            return;
        }

        if (seeBoardButton == null) seeBoardButton = BuildSeeBoardButton();
        if (seeBoardButton == null) return;

        _seeBoardButtonLabel = seeBoardButton.GetComponentInChildren<TextMeshProUGUI>(true);

        seeBoardButton.gameObject.SetActive(true);
        // Last sibling so it draws above the backdrop and stays clickable while everything else is hidden.
        seeBoardButton.transform.SetAsLastSibling();

        seeBoardButton.onClick.RemoveAllListeners();
        seeBoardButton.onClick.AddListener(ToggleSeeBoard);
    }

    /// <summary>
    /// Builds the fallback toggle. Deliberately plain — it borrows the prompt's font so it matches, and
    /// exists so the feature works without anyone opening the scene. Assign <see cref="seeBoardButton"/>
    /// to replace it with an authored one.
    /// </summary>
    private Button BuildSeeBoardButton()
    {
        var go = new GameObject("SeeBoardButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);

        var rt = (RectTransform)go.transform;
        // Top-right: the one corner of the board that stays empty. Anchored bottom-centre it sat on top
        // of the player's hand, which is exactly what they peek at the board to look at.
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-32f, -32f);
        rt.sizeDelta = new Vector2(240f, 68f);

        var background = go.GetComponent<Image>();
        background.color = new Color(0.10f, 0.10f, 0.12f, 0.95f);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);

        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = seeBoardLabel;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 34f;
        label.color = Color.white;
        label.raycastTarget = false; // the Button owns the click; the label must not intercept it
        if (promptText != null && promptText.font != null) label.font = promptText.font;

        return go.GetComponent<Button>();
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
