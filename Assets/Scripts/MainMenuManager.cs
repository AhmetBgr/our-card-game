using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public Button playButton;
    public Button exitButton;

    void Start()
    {
        playButton.onClick.AddListener(() => {
            if (SaveManager.Instance.saveData.SelectedDeckIndex == SaveManager.MysteryDeckIndex)
            {
                SaveManager.Instance.GenerateRandomDeck(SaveManager.MysteryDeckIndex);
            }

            SceneTransitionManager.Instance.TransitionToScene("Game");
        });
        exitButton.onClick.AddListener(() => { Application.Quit(); });
    }
    private void OnEnable()
    {
        DeckPanelController.DeckChanged += UpdatePlayButton;

    }
    private void OnDisable()
    {
        DeckPanelController.DeckChanged -= UpdatePlayButton;
    }
    private void OnDestroy()
    {

    }
    void UpdatePlayButton(bool value)
    {
        playButton.interactable = value;
    }
}
