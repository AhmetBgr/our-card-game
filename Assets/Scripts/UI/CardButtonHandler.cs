using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class CardButtonHandler : MonoBehaviour
{
    public Button Button;

    [SerializeField] private TMPro.TextMeshProUGUI cardNameText;


    public Action OnClicked;
    // Start is called before the first frame update
    void Awake()
    {
        Button = GetComponent<Button>();
        Button.onClick.AddListener(() => OnClicked?.Invoke());
    }

    public void SetName(string name)
    {
        cardNameText.text = name;
    }
}
