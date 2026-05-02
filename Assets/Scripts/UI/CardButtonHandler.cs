using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class CardButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerDownHandler
{
    public Button Button;
    public CardSO Card;

    [SerializeField] private TMPro.TextMeshProUGUI cardNameText;
    [SerializeField] private TMPro.TextMeshProUGUI costText;


    public string cardName;

    public Action OnClicked;
    // Start is called before the first frame update
    void Awake()
    {
        Button = GetComponent<Button>();
        //Button.onClick.AddListener(() => OnClicked?.Invoke());
    }

    public void SetName(string name)
    {
        cardName = name;
        cardNameText.text = name;
    }

    public void SetCost(int cost)
    {
        costText.text = cost.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        DeckPanelController.Instance.ShowCard(cardName, GetCardPosition(eventData.position));
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        DeckPanelController.Instance.HideCard();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        //DeckPanelController.Instance.SetPosition(GetCardPosition(eventData.position));
    }

    private Vector2 GetCardPosition(Vector2 mousePos)
    {
        var screenWidth = Screen.width;
        var screenHeight = Screen.height;

        var cardPreviewWidth = 120f;
        var cardPreviewHeight = 150f;

        // Flip horizontal offset if too close to the right edge
        float offsetX = (mousePos.x + cardPreviewWidth*2 > screenWidth)
            ? -cardPreviewWidth
            : cardPreviewWidth;

        // Flip vertical offset if too close to the top edge
        float offsetY = (mousePos.y + cardPreviewHeight*2 > screenHeight)
            ? -cardPreviewHeight
            : cardPreviewHeight;

        var offset = new Vector2(offsetX, offsetY);
        var pos = mousePos + offset;

        return pos;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnClicked?.Invoke();
    }
}
