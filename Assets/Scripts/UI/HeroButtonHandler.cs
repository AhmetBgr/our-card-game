using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Menu button for a single selectable hero. Mirrors <see cref="CardButtonHandler"/>:
/// hovering shows the shared card preview (a HeroSO is a CardSO, so the same CardModal/CardView
/// preview renders it), and pointer-down raises <see cref="OnClicked"/> so the owning
/// <see cref="HeroSelectionController"/> can select it.
/// </summary>
public class HeroButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public Button Button;
    public HeroSO Hero;

    [Tooltip("Image that displays the hero portrait.")]
    [SerializeField] private Image artImage;

    [Tooltip("Overlay shown when this hero is NOT selected (dims the unselected heroes).")]
    [SerializeField] private GameObject unselectedOverlay;

    [Tooltip("Image shown only on the Random Hero slot (mirrors the random deck indicator). Disabled by default.")]
    [SerializeField] private GameObject randomImage;

    [Header("Stats")]
    [Tooltip("Displays the hero's attack value.")]
    [SerializeField] private TextMeshProUGUI attackText;
    [Tooltip("Displays the hero's health value.")]
    [SerializeField] private TextMeshProUGUI healthText;
    [Tooltip("Melee attack icon (range 1). Enabled for melee heroes.")]
    [SerializeField] private GameObject attackIcon1;
    [Tooltip("Ranged attack icon (range >= 2). Enabled for ranged heroes.")]
    [SerializeField] private GameObject attackIcon2;

    public Action OnClicked;

    /// <summary>
    /// Deck panel that renders this button's hover preview. Injected by the owning
    /// <see cref="HeroSelectionController"/>, since the hero panel is a sibling of the deck panel
    /// rather than a child of it.
    /// </summary>
    public DeckPanelController PreviewPanel;

    /// <summary>True for the synthetic "Random Hero" slot (no specific HeroSO).</summary>
    public bool IsRandom { get; private set; }

    void Awake()
    {
        if (Button == null)
            Button = GetComponent<Button>();
    }

    public void SetHero(HeroSO hero)
    {
        Hero = hero;
        IsRandom = false;

        if (artImage != null)
            artImage.sprite = hero.cardArt != null ? hero.cardArt : hero.minionArt;

        if (randomImage != null)
            randomImage.SetActive(false);

        if (attackText != null)
            attackText.text = hero.attack.ToString();

        if (healthText != null)
            healthText.text = hero.health.ToString();

        UpdateAttackIcon(hero.range);
    }

    /// <summary>Configure this button as the "Random Hero" slot: show the random image overlay with the given art.</summary>
    public void SetRandom(Sprite art)
    {
        Hero = null;
        IsRandom = true;

        if (randomImage != null)
        {
            var img = randomImage.GetComponent<Image>();
            if (img != null && art != null)
            {
                img.sprite = art;
                img.enabled = true;
            }

            randomImage.SetActive(true);
        }

        // The random slot has no concrete stats to show.
        if (attackText != null)
            attackText.text = string.Empty;

        if (healthText != null)
            healthText.text = string.Empty;

        if (attackIcon1 != null)
            attackIcon1.SetActive(false);

        if (attackIcon2 != null)
            attackIcon2.SetActive(false);
    }

    // Melee (range 1) shows attack icon 1; ranged (range >= 2) shows attack icon 2. Mirrors the
    // in-game MinionView convention. Both refs are optional, so minion-only prefabs stay unaffected.
    private void UpdateAttackIcon(int range)
    {
        bool isRanged = range >= 2;

        if (attackIcon1 != null)
            attackIcon1.SetActive(!isRanged);

        if (attackIcon2 != null)
            attackIcon2.SetActive(isRanged);
    }

    public void SetSelected(bool selected)
    {
        // Selected hero is bright (overlay hidden); the rest are dimmed by the overlay.
        if (unselectedOverlay != null)
            unselectedOverlay.SetActive(!selected);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Hero == null || PreviewPanel == null) return;

        PreviewPanel.ShowCard(Hero);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (PreviewPanel == null) return;

        PreviewPanel.HideCard();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnClicked?.Invoke();
    }
}
