using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CardController : MonoBehaviour// , IPointerEnterHandler,IPointerExitHandler, IPointerDownHandler
{
    public CardModal modal;
    public CardView view;

    public CardTEst card;
    Transform curslot;
    // Start is called before the first frame update
    void Start()
    {
        modal.UpdateModal(card);
        view.UpdateView(modal);
        curslot = transform.parent;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnPointerDown()
    {
        GameManager.Instance.PlayCard(this);
    }
    public void OnPointerEnter()
    {
        HandManager.Instance.AddToTopView(transform);
        transform.localPosition += Vector3.up *220;
        transform.localScale = Vector3.one * 1.4f;
    }

    public void OnPointerExit()
    {
        HandManager.Instance.AddToHand(transform, curslot);
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;

    }
}
