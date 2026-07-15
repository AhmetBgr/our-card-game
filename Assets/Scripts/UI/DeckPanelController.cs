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

    void Awake()
    {
        Side = DeckSelectionContext.SideOf(this);
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
        DeckChanged?.Invoke(Side, isMysteryDeck || curCustomDeck.Count >= SaveManager.Instance.DeckSize);

    }
    private void UpdateDeckName(int index)
    {
        deckName.text = "Deck-" + (index + 1);

    }
    private void UpdateCardAmount()
    {
        int amount = curCustomDeck.Count;

        if (amount < SaveManager.Instance.DeckSize) {
            cardAmount.text = $"<color=yellow>{amount}/{SaveManager.Instance.DeckSize}</color>";
        }
        else
        {
            cardAmount.text = $"<color=green>{amount}/{SaveManager.Instance.DeckSize}</color>";

        }

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
