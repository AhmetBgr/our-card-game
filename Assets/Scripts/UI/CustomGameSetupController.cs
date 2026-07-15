using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the two-step match setup in the CreateCustomGame scene: first the player picks their own
/// deck and hero, then Proceed slides the panel container across to the opponent's panel and the
/// same choices are made for the AI. Both panels stay active the whole time (they need their Start()
/// to run and their ready-events to fire); only the container moves.
/// </summary>
public class CustomGameSetupController : MonoBehaviour
{
    [Header("Sliding panels")]
    [Tooltip("Container holding both ChooseDeckAndHero panels. This is what slides.")]
    [SerializeField] private RectTransform slidingContainer;
    [SerializeField] private RectTransform playerPanel;
    [SerializeField] private RectTransform opponentPanel;
    [SerializeField] private float slideDuration = 0.4f;

    [Header("Buttons")]
    [SerializeField] private Button proceedButton;
    [SerializeField] private Button backButton;
    //[SerializeField] private Button exitButton;
    [Tooltip("Label on the proceed button; switches from 'Proceed' to 'Play' on the opponent step.")]
    [SerializeField] private TextMeshProUGUI proceedLabel;
    [SerializeField] private string proceedText = "Proceed";
    [SerializeField] private string playText = "Play";
    [Tooltip("Shown (under the Proceed button) while the side being edited has an invalid/incomplete deck. Optional.")]
    [SerializeField] private GameObject invalidDeckText;

    [Header("Per-side editing indicators")]
    [Tooltip("Enabled while the player's side is being edited, disabled while the opponent's is. Optional.")]
    [SerializeField] private Transform playerEditingTransform;
    [Tooltip("Enabled while the opponent's side is being edited, disabled while the player's is. Optional.")]
    [SerializeField] private Transform opponentEditingTransform;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "Menu";

    // Which side the user is currently choosing for.
    private SelectionSide step = SelectionSide.Player;

    // Each side is ready once it has both a full deck (or the mystery deck) and a hero.
    private bool playerDeckReady, playerHeroReady;
    private bool opponentDeckReady, opponentHeroReady;

    void Start()
    {
        // Always open on the player's step, wherever the container happened to be left in the editor.
        Focus(playerPanel, animate: false);

        proceedButton.onClick.AddListener(OnProceed);
        backButton.onClick.AddListener(OnBack);

        //if (exitButton != null)
        //    exitButton.onClick.AddListener(() => Application.Quit());

        UpdateProceedButton();
        UpdateEditingIndicators();
    }

    private void OnEnable()
    {
        DeckPanelController.DeckChanged += OnDeckChanged;
        HeroSelectionController.HeroSelectionChanged += OnHeroSelectionChanged;
    }

    private void OnDisable()
    {
        DeckPanelController.DeckChanged -= OnDeckChanged;
        HeroSelectionController.HeroSelectionChanged -= OnHeroSelectionChanged;
    }

    private void OnProceed()
    {
        if (step == SelectionSide.Player)
        {
            step = SelectionSide.Opponent;
            Focus(opponentPanel, animate: true);
            UpdateProceedButton();
            UpdateEditingIndicators();
            return;
        }

        SceneTransitionManager.Instance.TransitionToScene(gameSceneName);
    }

    private void OnBack()
    {
        // From the opponent step, Back returns to the player's choice rather than leaving the scene.
        if (step == SelectionSide.Opponent)
        {
            step = SelectionSide.Player;
            Focus(playerPanel, animate: true);
            UpdateProceedButton();
            UpdateEditingIndicators();
            return;
        }

        SceneTransitionManager.Instance.TransitionToScene(menuSceneName);
    }

    // x the container needs so that `panel` lands dead centre. The container is centre-anchored, so
    // centring a child means cancelling out that child's own offset — no dependence on where the
    // container currently sits, which is what makes the opening step deterministic. The panels' own
    // authored offsets still drive the distance, so the layout can be retuned without touching this.
    private float FocusX(RectTransform panel) => -panel.anchoredPosition.x;

    private void Focus(RectTransform panel, bool animate)
    {
        slidingContainer.DOComplete();

        float targetX = FocusX(panel);

        if (animate)
        {
            slidingContainer.DOAnchorPosX(targetX, slideDuration).SetEase(Ease.InOutCubic);
        }
        else
        {
            var pos = slidingContainer.anchoredPosition;
            pos.x = targetX;
            slidingContainer.anchoredPosition = pos;
        }
    }

    void OnDeckChanged(SelectionSide side, bool value)
    {
        if (side == SelectionSide.Opponent)
            opponentDeckReady = value;
        else
            playerDeckReady = value;

        UpdateProceedButton();
    }

    void OnHeroSelectionChanged(SelectionSide side, bool value)
    {
        if (side == SelectionSide.Opponent)
            opponentHeroReady = value;
        else
            playerHeroReady = value;

        UpdateProceedButton();
    }

    void UpdateProceedButton()
    {
        bool deckReady = step == SelectionSide.Opponent ? opponentDeckReady : playerDeckReady;
        bool heroReady = step == SelectionSide.Opponent ? opponentHeroReady : playerHeroReady;

        proceedButton.interactable = deckReady && heroReady;

        if (proceedLabel != null)
            proceedLabel.text = step == SelectionSide.Opponent ? playText : proceedText;

        // Surface the "invalid deck" notice for the side being edited whenever its deck is incomplete.
        if (invalidDeckText != null)
            invalidDeckText.SetActive(!deckReady);
    }

    // Show the editing indicator for the side currently being chosen and hide the other's. Called
    // only where `step` changes (Start, Proceed, Back), so it stays in sync with the focused panel.
    void UpdateEditingIndicators()
    {
        if (playerEditingTransform != null)
            playerEditingTransform.gameObject.SetActive(step == SelectionSide.Player);

        if (opponentEditingTransform != null)
            opponentEditingTransform.gameObject.SetActive(step == SelectionSide.Opponent);
    }
}
