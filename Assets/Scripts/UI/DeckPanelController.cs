using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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

        nextButton.onClick.AddListener(() => MoveSelectableDecksPanel(-1));
        nextButton.onClick.AddListener(() => UpdateSelectedDeckIndex(1));

        previousButton.onClick.AddListener(() => MoveSelectableDecksPanel(1));
        previousButton.onClick.AddListener(() => UpdateSelectedDeckIndex(-1));

        var selectedDeckIndex = SaveManager.Instance.saveData.SelectedDeckIndex;

        for (int i = 0; i < selectedDeckIndex; i++)
        {
            MoveSelectableDecksPanel(-1);
        }
        if(selectedDeckIndex == 0)
            UpdateControlButtons(initialSelectablePanelPos.x);

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
    }
    public bool TryAddToCurrentCustomDeck(string card)
    {

        bool success = SaveManager.Instance.AddCard(card, SaveManager.Instance.saveData.SelectedDeckIndex);

        if (success)
            customDeckUIControllers[SaveManager.Instance.saveData.SelectedDeckIndex].AddCard(card);

        return success;
    }

    private void PopulateAllCards()
    {
        AllCardsUIController.Initialize();

        /*var allCardSOs = DeckDatabase.Instance.AllCards;

        foreach (var cardSO in allCardSOs)
        {
            var cardButton = Instantiate(cardButtonPrefab, allCardsPanel);
            cardButton.SetName(cardSO.name);
        }*/
    }

    private void MoveSelectableDecksPanel(int direction)
    {

        selectableDecksPanel.DOComplete();

        var value = selectableDecksPanel.localPosition.x + (direction * distanceBetweenToDecks);

        selectableDecksPanel.DOLocalMoveX(value, 0.2f);

        UpdateControlButtons(value);

    }

    private void UpdateControlButtons(float selectableDeckPanelPosX)
    {
        nextButton.interactable = selectableDeckPanelPosX > -1 * (SaveManager.Instance.saveData.Decks.Length - 1) * distanceBetweenToDecks + initialSelectablePanelPos.x;
        previousButton.interactable = selectableDeckPanelPosX < initialSelectablePanelPos.x;
    }
    private void UpdateSelectedDeckIndex(int amount)
    {
        var value = SaveManager.Instance.saveData.SelectedDeckIndex + amount;

        value = Mathf.Clamp(value, 0, SaveManager.Instance.saveData.Decks.Length - 1);

        SaveManager.Instance.saveData.SelectedDeckIndex = value;
        AllCardsUIController.UpdateSelectableCards();
        UpdateDeckName(value);
        UpdateCardAmount();
    }
    private void UpdateDeckName(int index)
    {
        deckName.text = "Deck-" + (index + 1);

    }
    private void UpdateCardAmount()
    {
        int amount = curCustomDeck.Count;
        cardAmount.text = (amount)+ "/" + SaveManager.Instance.DeckSize;

    }
}
