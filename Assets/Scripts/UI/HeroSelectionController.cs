using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates the hero-selection carousel with one <see cref="HeroButtonHandler"/> per hero in
/// <see cref="HeroDatabase"/>, plus a trailing "Random Hero" slot (mirrors the mystery/random deck).
/// Tracks and persists the choice via SaveData.SelectedHeroIndex. Next/Previous slide the masked
/// content, clicking a hero jumps to it, and the selected entry's name is shown in a label.
/// </summary>
public class HeroSelectionController : MonoBehaviour
{
    [SerializeField] private HeroButtonHandler heroButtonPrefab;
    [Tooltip("Container the hero buttons are instantiated under (SelectableHereos).")]
    [SerializeField] private Transform content;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [Tooltip("Label that shows the currently-selected hero's name.")]
    [SerializeField] private TextMeshProUGUI heroNameText;

    [Header("Random Hero")]
    [SerializeField] private string randomHeroLabel = "Random Hero";
    [Tooltip("Art shown on the Random Hero slot's overlay.")]
    [SerializeField] private Sprite randomHeroArt;

    private readonly List<HeroButtonHandler> heroButtons = new List<HeroButtonHandler>();
    private int selectedIndex; // carousel position: 0..realHeroCount-1 = a hero, last = Random slot
    private float distanceBetweenHeroes;
    private Vector3 initialContentPos;

    /// <summary>Raised with whether a valid hero is currently selected (for the Play gate).</summary>
    public static event Action<bool> HeroSelectionChanged;

    // The Random slot is always the last button in the carousel.
    private int RandomSlotPosition => heroButtons.Count - 1;
    private bool IsRandom(int position) => position == RandomSlotPosition;

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        var heroes = HeroDatabase.Instance.AllHeroes;

        for (int i = 0; i < heroes.Count; i++)
        {
            int index = i;
            var hero = heroes[i];

            var heroButton = Instantiate(heroButtonPrefab, content);
            heroButton.SetHero(hero);
            heroButton.OnClicked = () => SelectPosition(index, animate: true);

            heroButtons.Add(heroButton);
        }

        // Trailing "Random Hero" slot, mirroring the mystery/random deck.
        {
            var randomButton = Instantiate(heroButtonPrefab, content);
            randomButton.SetRandom(randomHeroArt);
            int randomPos = heroButtons.Count; // its position once added
            randomButton.OnClicked = () => SelectPosition(randomPos, animate: true);
            heroButtons.Add(randomButton);
        }

        initialContentPos = content.localPosition;

        var layout = content.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;
        distanceBetweenHeroes = spacing + content.GetChild(0).GetComponent<RectTransform>().rect.width;

        if (nextButton != null) nextButton.onClick.AddListener(() => ChangeSelected(1));
        if (previousButton != null) previousButton.onClick.AddListener(() => ChangeSelected(-1));

        // Restore the persisted choice: the RandomHero sentinel maps to the last slot.
        int stored = SaveManager.Instance.saveData.SelectedHeroIndex;
        int startPos = stored == HeroDatabase.RandomHeroIndex
            ? RandomSlotPosition
            : Mathf.Clamp(stored, 0, RandomSlotPosition);

        // Position the carousel and refresh state without re-saving the loaded value.
        SelectPosition(startPos, animate: false, persist: false);
    }

    private void ChangeSelected(int amount)
    {
        SelectPosition(selectedIndex + amount, animate: true);
    }

    private void SelectPosition(int position, bool animate, bool persist = true)
    {
        if (heroButtons.Count == 0) return;

        position = Mathf.Clamp(position, 0, heroButtons.Count - 1);
        selectedIndex = position;

        if (persist)
        {
            // Persist the sentinel for the Random slot so it survives new heroes being added.
            SaveManager.Instance.saveData.SelectedHeroIndex =
                IsRandom(position) ? HeroDatabase.RandomHeroIndex : position;
            SaveManager.Instance.SaveData();
        }

        MoveContentToIndex(position, animate);
        RefreshSelectedVisuals();
        UpdateControlButtons();
        UpdateHeroName();
        TriggerHeroSelectionChanged();
    }

    private void MoveContentToIndex(int index, bool animate)
    {
        content.DOComplete();

        float targetX = initialContentPos.x + (-index * distanceBetweenHeroes);

        if (animate)
        {
            content.DOLocalMoveX(targetX, 0.2f);
        }
        else
        {
            var pos = content.localPosition;
            pos.x = targetX;
            content.localPosition = pos;
        }
    }

    private void RefreshSelectedVisuals()
    {
        for (int i = 0; i < heroButtons.Count; i++)
            heroButtons[i].SetSelected(i == selectedIndex);
    }

    private void UpdateControlButtons()
    {
        if (previousButton != null) previousButton.interactable = selectedIndex > 0;
        if (nextButton != null) nextButton.interactable = selectedIndex < heroButtons.Count - 1;
    }

    private void UpdateHeroName()
    {
        if (heroNameText == null) return;

        if (IsRandom(selectedIndex))
        {
            heroNameText.text = randomHeroLabel;
            return;
        }

        var hero = heroButtons[selectedIndex].Hero;
        if (hero == null) { heroNameText.text = ""; return; }

        // Show the card name authored on the hero card; fall back to the asset name if unset.
        heroNameText.text = !string.IsNullOrEmpty(hero.cardName) ? hero.cardName : hero.name;
    }

    private void TriggerHeroSelectionChanged()
    {
        HeroSelectionChanged?.Invoke(heroButtons.Count > 0);
    }
}
