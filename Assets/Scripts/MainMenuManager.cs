using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public Button playButton;
    public Button exitButton;

    // Play requires BOTH a complete deck and a selected hero.
    private bool deckReady;
    private bool heroReady;

    void Start()
    {
        playButton.onClick.AddListener(() => {
            SceneTransitionManager.Instance.TransitionToScene("Game");
        });
        exitButton.onClick.AddListener(() => { Application.Quit(); });
    }
    private void OnEnable()
    {
        DeckPanelController.DeckChanged += OnDeckChanged;
        HeroSelectionController.HeroSelectionChanged += OnHeroSelectionChanged;
    }
    private void OnDisable()
    {
        DeckPanelController.DeckChanged -= OnDeckChanged;
        HeroSelectionController.HeroSelectionChanged -= OnHeroSelectionChanged;
    }
    private void OnDestroy()
    {

    }
    void OnDeckChanged(bool value)
    {
        deckReady = value;
        UpdatePlayButton();
    }
    void OnHeroSelectionChanged(bool value)
    {
        heroReady = value;
        UpdatePlayButton();
    }
    void UpdatePlayButton()
    {
        playButton.interactable = deckReady && heroReady;
    }
}
