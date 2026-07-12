using System;
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

    public Action OnClicked;

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
    }

    public void SetSelected(bool selected)
    {
        // Selected hero is bright (overlay hidden); the rest are dimmed by the overlay.
        if (unselectedOverlay != null)
            unselectedOverlay.SetActive(!selected);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Hero == null) return;

        DeckPanelController.Instance.ShowCard(Hero);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        DeckPanelController.Instance.HideCard();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnClicked?.Invoke();
    }
}
