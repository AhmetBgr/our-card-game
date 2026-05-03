using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public Button playButton;
    // Start is called before the first frame update
    void Start()
    {
        playButton.onClick.AddListener(() => { SceneTransitionManager.Instance.TransitionToScene("Game"); });
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
