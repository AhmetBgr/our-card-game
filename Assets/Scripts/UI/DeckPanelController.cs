using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one deck-selection panel. There is one instance per <see cref="SelectionSide"/> (the
/// player's and the opponent's panels are the same prefab), so this deliberately holds no static
/// state: the side comes from the <see cref="DeckSelectionContext"/> above it, and every read and
/// write is addressed through it.
/// </summary>
public class DeckPanelController : MonoBehaviour
{
    [SerializeField] private CardButtonHandler cardButtonPrefab;
    [SerializeField] private Transform allCardsPanel;

    [SerializeField] private Transform selectableDecksPanel;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;

    [SerializeField] private CustomDeckUIController deckPrefab;
    [SerializeField] private TextMeshProUGUI deckName;
    [SerializeField] private TextMeshProUGUI cardAmount;

    [SerializeField] private GameObject mouseHoverCard;

    [Tooltip("Optional fixed spot for the keyword tooltip stack (its top-left corner lands here) while a card preview is shown. Empty = stack sits beside the preview card.")]
    [SerializeField] private Transform keywordTooltipPoint;
    [SerializeField] private GameObject upgradedMouseHoverCard;

    [Tooltip("Shown when the selected deck is locked (the default/mystery decks aren't editable).")]
    [SerializeField] private GameObject lockImage;
    [Tooltip("Shown when the selected deck is unlocked/editable.")]
    [SerializeField] private GameObject transferImage;

    private List<CustomDeckUIController> customDeckUIControllers = new();
    public AllCardsUIController AllCardsUIController;

    /// <summary>Whose selection this panel edits. Player when there is no context above it.</summary>
    public SelectionSide Side { get; private set; }

    private DeckData[] Decks => SaveManager.Instance.GetDecks(Side);
    private int SelectedDeckIndex => SaveManager.Instance.GetSelectedDeckIndex(Side);
    public List<string> curCustomDeck => Decks[SelectedDeckIndex].Deck;

    private float distanceBetweenToDecks;
    private Vector3 initialSelectablePanelPos;

    public static event Action<SelectionSide, bool> DeckChanged;

    /// <summary>
    /// Raised with <see cref="DeckChanged"/>, carrying why the deck is invalid (null when it is valid),
    /// so the setup screen can say which rule the deck breaks.
    /// </summary>
    public static event Action<SelectionSide, string> DeckInvalidReasonChanged;

    private bool initialized;

    void Awake()
    {
        Side = DeckSelectionContext.SideOf(this);
    }

    void OnEnable()
    {
        MatchModifiers.Changed += OnModifiersChanged;
    }

    void OnDisable()
    {
        MatchModifiers.Changed -= OnModifiersChanged;
    }

    // The deck rules just changed under the panel (a modifier toggled): re-offer the cards, re-grey
    // the grid and re-judge the deck. Nothing in the saved deck is touched.
    void OnModifiersChanged()
    {
        if (!initialized) return;

        AllCardsUIController.ApplyRules();
        AllCardsUIController.UpdateSelectableCards();
        UpdateCardAmount();
        TriggerDeckChanged();
    }

    void Start()
    {
        // Regenerate this side's mystery deck before building any views, so the panel
        // reads the freshly-generated cards instead of a just-cleared list.
        // Runs in Start (not Awake) so DeckDatabase/SaveManager Awakes are done.
        SaveManager.Instance.GenerateRandomDeck(SaveManager.MysteryDeckIndex, Side);

        PopulateAllCards();

        initialSelectablePanelPos = selectableDecksPanel.localPosition;

        var decks = Decks;
        for (int i = 0; i < decks.Length; i++)
        {
            var deckView = Instantiate(deckPrefab, selectableDecksPanel);
            deckView.Initialize(decks[i], isRandomDeck: i == SaveManager.MysteryDeckIndex);

            customDeckUIControllers.Add(deckView);
        }

        var horizantolLayout = selectableDecksPanel.GetComponent<HorizontalLayoutGroup>();
        distanceBetweenToDecks = horizantolLayout.spacing + selectableDecksPanel.GetChild(0).GetComponent<RectTransform>().rect.width;

        nextButton.onClick.AddListener(() => ChangeSelectedDeck(1));
        previousButton.onClick.AddListener(() => ChangeSelectedDeck(-1));

        var selectedDeckIndex = SelectedDeckIndex;

        SetSelectableDecksPanelToIndex(selectedDeckIndex, instant: true);

        UpdateDeckName(selectedDeckIndex);
        UpdateCardAmount();
        UpdateLockState();

        AllCardsUIController.UpdateSelectableCards();

        initialized = true;
        TriggerDeckChanged();

    }
    public void ShowCard(string cardName, Vector3 screenPos)
    {
        var card = DeckDatabase.Instance.GetCard(cardName);
        ShowCard(card);
    }

    // Heroes (HeroSO : CardSO) aren't in DeckDatabase, so they drive the shared preview directly.
    public void ShowCard(CardSO card)
    {
        if (card == null) return;

        if (hideCardCor != null)
            StopCoroutine(hideCardCor);

        //mouseHoverCard.card = card;
        var modal = mouseHoverCard.GetComponent<CardModal>();
        modal.UpdateModal(card, null, true);
        mouseHoverCard.GetComponent<CardView>().UpdateView(modal);
        mouseHoverCard.gameObject.SetActive(true);

        // The preview shows base and upgraded side by side, so the tooltip covers both descs.
        string tooltipDesc = card.upgradedVersion == null
            ? card.desc
            : card.desc + "\n" + card.upgradedVersion.desc;
        KeywordTooltip.Show(tooltipDesc, (RectTransform)mouseHoverCard.transform, this, keywordTooltipPoint);

        if (card.upgradedVersion == null) return;

        var modal2 = upgradedMouseHoverCard.GetComponent<CardModal>();
        modal2.UpdateModal(card.upgradedVersion, null, true);
        upgradedMouseHoverCard.GetComponent<CardView>().UpdateView(modal2);
        upgradedMouseHoverCard.gameObject.SetActive(true);

    }
    public void SetPosition(Vector3 screenPos)
    {
        mouseHoverCard.transform.position = screenPos;

    }
    private IEnumerator hideCardCor = null;
    public void HideCard()
    {

        if(hideCardCor != null)
            StopCoroutine(hideCardCor);

        hideCardCor = DelayedHideCard();
        StartCoroutine(hideCardCor);

    }
    private IEnumerator DelayedHideCard()
    {
        yield return new WaitForSeconds(0.1f);

        mouseHoverCard.gameObject.SetActive(false);
        upgradedMouseHoverCard.gameObject.SetActive(false);
        KeywordTooltip.Hide(this);
    }
    public void RemoveFromCurrentCustomDeck(string card)
    {
        int index = SelectedDeckIndex;

        SaveManager.Instance.RemoveCard(card, index, Side);
        AllCardsUIController.UpdateSelectableCards();
        UpdateCardAmount();
        TriggerDeckChanged();

        customDeckUIControllers[index].RemoveCard(card);

    }
    public bool IsCurCustomDeckLocked()
    {
        return Decks[SelectedDeckIndex].isLocked;
    }
    public bool TryAddToCurrentCustomDeck(string card)
    {
        int index = SelectedDeckIndex;

        bool success = SaveManager.Instance.AddCard(card, index, Side);

        if (success) {
            customDeckUIControllers[index].AddCard(card);
            customDeckUIControllers[index].UpdateOrder();
            TriggerDeckChanged();
        }

        UpdateCardAmount();
        return success;
    }

    private void PopulateAllCards()
    {
        AllCardsUIController.Initialize();
    }

    private void UpdateControlButtons(float selectableDeckPanelPosX)
    {
        nextButton.interactable = selectableDeckPanelPosX > -1 * (Decks.Length - 1) * distanceBetweenToDecks + initialSelectablePanelPos.x;
        previousButton.interactable = selectableDeckPanelPosX < initialSelectablePanelPos.x;
    }

    private void ChangeSelectedDeck(int amount)
    {
        var oldIndex = SelectedDeckIndex;
        var newIndex = Mathf.Clamp(oldIndex + amount, 0, Decks.Length - 1);

        if (newIndex == oldIndex)
            return;

        SaveManager.Instance.SetSelectedDeckIndex(Side, newIndex);
        SetSelectableDecksPanelToIndex(newIndex, instant: false);

        AllCardsUIController.UpdateSelectableCards();
        UpdateDeckName(newIndex);
        UpdateCardAmount();
        UpdateLockState();
        TriggerDeckChanged();
    }

    private void SetSelectableDecksPanelToIndex(int index, bool instant)
    {
        selectableDecksPanel.DOComplete();

        var targetX = initialSelectablePanelPos.x + (-index * distanceBetweenToDecks);

        if (instant)
        {
            var pos = selectableDecksPanel.localPosition;
            pos.x = targetX;
            selectableDecksPanel.localPosition = pos;
        }
        else
        {
            selectableDecksPanel.DOLocalMoveX(targetX, 0.2f);
        }

        UpdateControlButtons(targetX);
    }
    private void TriggerDeckChanged()
    {
        bool isMysteryDeck = SelectedDeckIndex == SaveManager.MysteryDeckIndex;

        // Locked decks (the authored default and the mystery deck) are always playable: they were not
        // built under these rules, and the default deck deliberately repeats cards.
        string reason = null;
        bool valid = isMysteryDeck || IsCurCustomDeckLocked() || MatchModifiers.CurrentDeckRules.IsValid(curCustomDeck, out reason);

        DeckChanged?.Invoke(Side, valid);
        DeckInvalidReasonChanged?.Invoke(Side, valid ? null : reason);
    }
    private void UpdateDeckName(int index)
    {
        deckName.text = "Deck-" + (index + 1);

    }
    private void UpdateCardAmount()
    {
        int amount = curCustomDeck.Count;
        DeckRules rules = MatchModifiers.CurrentDeckRules;

        // "7/10" under the classic rules; "7 (5-30)" once a modifier has opened the size up.
        string target = rules.minSize == rules.maxSize
            ? $"{amount}/{rules.maxSize}"
            : $"{amount} ({rules.minSize}-{rules.maxSize})";

        bool valid = IsCurCustomDeckLocked() || rules.IsValid(curCustomDeck, out _);
        cardAmount.text = valid ? $"<color=green>{target}</color>" : $"<color=yellow>{target}</color>";
    }

    // Locked decks (default/mystery) can't be edited: show the lock image and hide the transfer
    // control; unlocked decks show the transfer control instead.
    private void UpdateLockState()
    {
        bool locked = IsCurCustomDeckLocked();

        if (lockImage != null)
            lockImage.SetActive(locked);

        if (transferImage != null)
            transferImage.SetActive(!locked);
    }
}
