using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.UI;

public class DeckPanelController : Singleton<DeckPanelController>
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

    private List<CustomDeckUIController> customDeckUIControllers = new();
    public AllCardsUIController AllCardsUIController;

    private SaveData saveData => SaveManager.Instance.saveData;
    public List<string> curCustomDeck => saveData.Decks[saveData.SelectedDeckIndex].Deck;

    private float distanceBetweenToDecks;
    private Vector3 initialSelectablePanelPos;

    public static event Action<bool> DeckChanged;

    void Start()
    {
        PopulateAllCards();

        initialSelectablePanelPos = selectableDecksPanel.localPosition;

        foreach (var item in SaveManager.Instance.saveData.Decks)
        {
            var deckView = Instantiate(deckPrefab, selectableDecksPanel);
            deckView.Initialize(item);

            customDeckUIControllers.Add(deckView);
        }

        var horizantolLayout = selectableDecksPanel.GetComponent<HorizontalLayoutGroup>();
        distanceBetweenToDecks = horizantolLayout.spacing + selectableDecksPanel.GetChild(0).GetComponent<RectTransform>().rect.width;

        nextButton.onClick.AddListener(() => ChangeSelectedDeck(1));
        previousButton.onClick.AddListener(() => ChangeSelectedDeck(-1));

        var selectedDeckIndex = SaveManager.Instance.saveData.SelectedDeckIndex;

        SetSelectableDecksPanelToIndex(selectedDeckIndex, instant: true);

        UpdateDeckName(selectedDeckIndex);
        UpdateCardAmount();

        AllCardsUIController.UpdateSelectableCards();

    }
    public void ShowCard(string cardName, Vector3 screenPos)
    {
        if (hideCardCor != null)
            StopCoroutine(hideCardCor);

        var card  = DeckDatabase.Instance.GetCard(cardName);
        //mouseHoverCard.card = card;
        var modal = mouseHoverCard.GetComponent<CardModal>();
        modal.UpdateModal(card);
        mouseHoverCard.GetComponent<CardView>().UpdateView(modal);
        mouseHoverCard.gameObject.SetActive(true);

        if (card.upgradedVersion == null) return;

        modal = upgradedMouseHoverCard.GetComponent<CardModal>();
        modal.UpdateModal(card.upgradedVersion);
        upgradedMouseHoverCard.GetComponent<CardView>().UpdateView(modal);
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
        SaveManager.Instance.RemoveCard(card, SaveManager.Instance.saveData.SelectedDeckIndex);
        AllCardsUIController.UpdateSelectableCards();
        UpdateCardAmount();
        TriggerDeckChanged();

        customDeckUIControllers[SaveManager.Instance.saveData.SelectedDeckIndex].RemoveCard(card);

    }
    public bool IsCurCustomDeckLocked()
    {
        return saveData.Decks[saveData.SelectedDeckIndex].isLocked;
    }
    public bool TryAddToCurrentCustomDeck(string card)
    {
        bool success = SaveManager.Instance.AddCard(card, SaveManager.Instance.saveData.SelectedDeckIndex);

        if (success) { 
            customDeckUIControllers[SaveManager.Instance.saveData.SelectedDeckIndex].AddCard(card);
            customDeckUIControllers[SaveManager.Instance.saveData.SelectedDeckIndex].UpdateOrder();
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
        nextButton.interactable = selectableDeckPanelPosX > -1 * (SaveManager.Instance.saveData.Decks.Length - 1) * distanceBetweenToDecks + initialSelectablePanelPos.x;
        previousButton.interactable = selectableDeckPanelPosX < initialSelectablePanelPos.x;
    }

    private void ChangeSelectedDeck(int amount)
    {
        var oldIndex = SaveManager.Instance.saveData.SelectedDeckIndex;
        var newIndex = Mathf.Clamp(oldIndex + amount, 0, SaveManager.Instance.saveData.Decks.Length - 1);

        if (newIndex == oldIndex)
            return;

        SaveManager.Instance.saveData.SelectedDeckIndex = newIndex;
        SetSelectableDecksPanelToIndex(newIndex, instant: false);

        AllCardsUIController.UpdateSelectableCards();
        UpdateDeckName(newIndex);
        UpdateCardAmount();
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
        DeckChanged?.Invoke(curCustomDeck.Count >= SaveManager.Instance.DeckSize);

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

        if (saveData.Decks[saveData.SelectedDeckIndex].isLocked)
        {
            cardAmount.text = cardAmount.text + " - LOCKED";
        }

    }
}
