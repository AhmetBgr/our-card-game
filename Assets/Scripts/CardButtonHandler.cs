using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardButtonHandler : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private TMPro.TextMeshProUGUI cardNameText;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        //Destroy(gameObject);
    }

    public void SetName(string name)
    {
        cardNameText.text = name;
    }
}
