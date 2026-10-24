using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One perk on offer: the passive's own chip (<see cref="PassiveChipButton"/>) over an info panel that
/// spells the perk out. Authored as a prefab -- Assets/Prefabs/UI/PerkOption.prefab -- so that every
/// surface that offers a perk hands out the same option instead of building one in code: the Draft
/// scene's perk draft (<see cref="DraftController"/>) and the end-of-match New Perk milestone
/// (<see cref="PerkSelectionPanel"/>) both instantiate it, and what a perk option looks like is
/// changed once, on the prefab.
///
/// The prefab owns its own size, fonts and hover, so a caller only hands it a perk, a click handler and
/// its turn to pop in. Display-only options (the perks a run has already taken) are spawned with no
/// handler, which leaves them unhovered and unclickable.
///
/// Holds no rules: it draws the perk it is handed and reports the click, like the chip it is built around.
/// </summary>
public class PerkOptionView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("The passive's chip, at the top of the option. Its own hover tooltip is switched off when " +
             "a perk is shown, because the info panel below it already says the same thing.")]
    [SerializeField] private PassiveChipButton chip;

    [Tooltip("The perk's name, the heading of the info panel.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("The perk's description, highlighted the way card text is.")]
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("How much a hovered option swells, and how long it takes.")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverDuration = 0.1f;

    [Tooltip("How long the option takes to pop in when it is revealed.")]
    [SerializeField] private float revealDuration = 0.25f;

    [Header("Being taken")]
    [Tooltip("How hard the option is punched the moment it is clicked.")]
    [SerializeField] private float pickPunchScale = 0.18f;
    [SerializeField] private float pickPunchDuration = 0.25f;

    [Tooltip("How the options beside the taken one bow out.")]
    [SerializeField] private float discardDuration = 0.25f;
    [SerializeField] private float discardScale = 0.85f;

    // Same lazy Resources load CardView and PassiveChipButton use, so an option's description is
    // highlighted from the one config the match uses. Null-safe: a missing asset renders plain text.
    private static CardTextHighlightConfig highlightConfig;
    private static CardTextHighlightConfig HighlightConfig =>
        highlightConfig != null ? highlightConfig : (highlightConfig = Resources.Load<CardTextHighlightConfig>("CardTextHighlightConfig"));

    /// <summary>The perk this option offers, or null before <see cref="Show"/>.</summary>
    public HeroPassiveSO Perk { get; private set; }

    /// <summary>
    /// Taking this option. Left null for a display-only option, which is also what makes it inert:
    /// nothing with no handler hovers or clicks.
    /// </summary>
    public Action<PerkOptionView> OnClicked;

    /// <summary>
    /// Switched off by the owner once the choice is spent, so the options beside the picked one stop
    /// answering the pointer while they are still on screen.
    /// </summary>
    public bool Interactable { get; set; } = true;

    private bool _hovered;

    /// <summary>The take / discard animation, so a second one never runs on top of the first.</summary>
    private Sequence _anim;

    private CanvasGroup _group;

    /// <summary>
    /// Added on demand rather than authored, so the prefab needs nothing extra for the animation and an
    /// option built in code still fades.
    /// </summary>
    private CanvasGroup Group
    {
        get
        {
            if (_group == null && !TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
            return _group;
        }
    }

    private bool CanInteract => Interactable && OnClicked != null;

    /// <summary>Draws a perk. Null empties the option.</summary>
    public void Show(HeroPassiveSO perk)
    {
        Perk = perk;

        if (chip != null)
        {
            chip.ShowPassive(perk);

            // The info panel spells the perk out, so the chip's tooltip would only repeat it.
            if (chip.TryGetComponent(out UITooltipTrigger chipTooltip)) chipTooltip.enabled = false;

            // The chip handles its own clicks and so swallows the one meant for the option.
            chip.OnClicked = _ => Click();
        }

        if (nameText != null) nameText.text = perk != null ? perk.passiveName : string.Empty;
        if (descriptionText != null)
            descriptionText.text = perk != null ? CardTextFormatter.Format(perk.description, HighlightConfig) : string.Empty;
    }

    /// <summary>
    /// Pops the option in from nothing. <paramref name="delay"/> is its turn in the row, so a row of
    /// options arrives one after another.
    /// </summary>
    public void PlayReveal(float delay = 0f)
    {
        KillAnim();
        transform.DOKill();
        Group.alpha = 1f;
        Interactable = true;
        transform.localScale = Vector3.zero;
        transform.DOScale(1f, revealDuration).SetDelay(delay);
    }

    /// <summary>
    /// This perk is the one taken: a punch to acknowledge the click, where it stands.
    /// <paramref name="onDone"/> runs when the punch has played out, which is the panel's cue to unlock
    /// the perk on the hero panel.
    /// </summary>
    public void PlayTaken(Action onDone = null)
    {
        Interactable = false;
        KillAnim();
        transform.DOKill();
        transform.localScale = Vector3.one;

        _anim = DOTween.Sequence();
        _anim.Append(transform.DOPunchScale(Vector3.one * pickPunchScale, pickPunchDuration, 6, 0.6f));
        if (onDone != null) _anim.OnComplete(() => onDone());
    }

    /// <summary>The perks that were not taken, bowing out so the taken one is the only thing moving.</summary>
    public void PlayDiscarded()
    {
        Interactable = false;
        KillAnim();
        transform.DOKill();

        _anim = DOTween.Sequence();
        _anim.Append(transform.DOScale(discardScale, discardDuration).SetEase(Ease.InQuad));
        _anim.Join(Group.DOFade(0f, discardDuration));
    }

    private void KillAnim()
    {
        if (_anim != null && _anim.IsActive()) _anim.Kill();
        _anim = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanInteract) return;

        _hovered = true;
        ScaleTo(hoverScale);
    }

    // Not gated on CanInteract: an option switched off while the pointer is on it still has to come
    // back down, or it stays swollen for as long as it is on screen.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_hovered) return;

        _hovered = false;
        ScaleTo(1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Click();
    }

    private void Click()
    {
        if (!CanInteract) return;
        OnClicked?.Invoke(this);
    }

    private void ScaleTo(float scale)
    {
        transform.DOKill();
        transform.DOScale(scale, hoverDuration);
    }

    void OnDestroy()
    {
        KillAnim();
        transform.DOKill();
    }
}
