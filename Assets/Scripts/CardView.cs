using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Unity.Collections.LowLevel.Unsafe;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private Image frame;

    /// <summary>
    /// The visible card's rect, for anything that needs to sit flush against what the player sees
    /// (keyword tooltips). The root RectTransform is a larger layout slot whose edges extend past
    /// the drawn card, so aligning to it looks off; the frame image is the card as drawn.
    /// </summary>
    public RectTransform VisualRect => frame != null ? frame.rectTransform : (RectTransform)transform;

    [SerializeField] private Sprite minionFrame;
    [SerializeField] private Sprite spellFrame;
    [SerializeField] private Sprite upgradedMinionFrame;
    [SerializeField] private Sprite upgradedSpellFrame;

    [SerializeField] private GameObject[] minionTypeIconObjects;

    //[SerializeField] private Image _iconRenderer;
    [SerializeField] private Image cardBack;

    /// <summary>The back that covers the face on a face-down card — the only thing visible once it's flipped.</summary>
    public Image CardBack => cardBack;

    [SerializeField] private Sprite cardBackImage;
    [SerializeField] private Sprite upgradedCardBackImage;


    [Tooltip("Lit while the card is affordable right now.")]
    [SerializeField] private GameObject highlightOutline;
    [Tooltip("Lit while the pointer is over the card, but only while the playable outline is also lit.")]
    [SerializeField] private GameObject hoveredOutline;

    [SerializeField] private TextMeshProUGUI nametext;
    [SerializeField] private TextMeshProUGUI desctext;
    [SerializeField] private TextMeshProUGUI attacktext;
    [SerializeField] private TextMeshProUGUI healthtext;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Transform costTransform;

    private Tween gearRotateTween;

    // Latest requests from the two independent drivers of the hover outline: the pointer
    // (OnPointerEnter/Exit) and affordability (pushed every frame from CardController.Update).
    // Kept separately so either can change without the other having to be re-sent.
    private bool _hovered;
    private bool _playable;

    // Loaded once from Resources so no per-prefab inspector wiring is needed. Drives keyword
    // highlighting in card descriptions (see CardTextFormatter). Null-safe: if the asset is
    // missing, descriptions render as plain text.
    private static CardTextHighlightConfig highlightConfig;
    private static CardTextHighlightConfig HighlightConfig =>
        highlightConfig != null ? highlightConfig : (highlightConfig = Resources.Load<CardTextHighlightConfig>("CardTextHighlightConfig"));

    private void Start()
    {
        gearRotateTween?.Kill();
        gearRotateTween = costTransform.DOLocalRotate(Vector3.forward * 360, 10f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1);
        gearRotateTween.timeScale = 0;
    }



    private void OnDestroy()
    {

    }

    /// <summary>
    /// Debug-only override that renders this card face up even when it belongs to the opponent
    /// (see Debugger's hold-Alt peek). Deliberately a view flag rather than a flip of
    /// CardModal.isPlayerMinion: that field is gameplay state — it drives target-side filtering in
    /// OpponentBrained.SelectMinion and gates hover/peek in CardController — so writing to it to
    /// change what's drawn would quietly change how the card behaves.
    ///
    /// NonSerialized so it can never be left on in a prefab or scene and ship a face-up opponent hand.
    /// </summary>
    [NonSerialized] public bool debugRevealFaceUp;

    public void UpdateView(CardModal card)
    {
        if (card == null) return;
        UpdateTexts(card);

        art.sprite = card.cardArt;

        // The card's own side decides this everywhere except under the debug peek above.
        bool faceUp = card.isPlayerMinion || debugRevealFaceUp;

        costTransform.gameObject.SetActive(faceUp);
        costText.gameObject.SetActive(faceUp);
        cardBack.gameObject.SetActive(!faceUp);
        // enabled is driven alongside SetActive, not left to the prefab: CardPreview Variant ships with
        // this Image component disabled, so activating the GameObject alone drew nothing and the card
        // FACE showed through underneath — which leaked the opponent's cards in the selection panel.
        // Setting both here means no prefab can disagree about whether a hidden card is actually hidden.
        cardBack.enabled = !faceUp;
        cardBack.sprite = card.isUpgraded ? upgradedCardBackImage : cardBackImage;

        if (card.isUpgraded)
        {
            frame.sprite = card.attack == 0 && card.health == 0 ? upgradedSpellFrame : upgradedMinionFrame;

        }
        else { 
            frame.sprite = card.attack ==0 && card.health ==0 ? spellFrame : minionFrame;

        }

        minionTypeIconObjects[0].transform.parent.gameObject.SetActive(faceUp);

        // Keyed off health, not attack: a 0-attack minion is still a minion and must show its attack
        // stat. Only spells (attack == 0 && health == 0) hide it, and health == 0 already covers those.
        attacktext.transform.parent.gameObject.SetActive(card.health > 0 && faceUp);
        healthtext.transform.parent.gameObject.SetActive(card.health > 0 && faceUp);


    }
    /// <summary>
    /// Turn-into-another-card flip: one full 360° spin around Y, with `onHalfway` fired at the halfway
    /// point (180°) so the card's identity is swapped while it is turned away from the viewer and its
    /// face is unreadable, instead of popping in place.
    ///
    /// Two +180° LocalAxisAdd steps rather than one absolute 360° tween: added rotation lands back on
    /// whatever rotation the card started at, so this is safe on a card sitting at a hand-fan angle and
    /// can't fight the play/discard tweens by snapping it upright.
    /// </summary>
    public void PlayTurnIntoAnimation(Action onHalfway, float duration)
    {
        float half = duration * 0.5f;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalRotate(new Vector3(0f, 180f, 0f), half, RotateMode.LocalAxisAdd)
            .SetEase(Ease.InSine));
        seq.AppendCallback(() => onHalfway?.Invoke());
        seq.Append(transform.DOLocalRotate(new Vector3(0f, 180f, 0f), half, RotateMode.LocalAxisAdd)
            .SetEase(Ease.OutSine));
    }

    /// <summary>
    /// Can the player pay for this card right now? Single definition so the cost gear, the playable
    /// outline and the selection panel can't drift apart on what "affordable" means.
    ///
    /// Affordability ONLY — it says nothing about whose card this is or whether it's their turn. A card
    /// sitting in the hand needs those too (see CardController.IsPlayableHandCard); the selection panel
    /// deliberately doesn't, since its options are offered to the player even on the opponent's turn
    /// (a triggered draw that empties the deck) and should still show what they could afford.
    /// </summary>
    public static bool IsPlayableNow(CardModal card)
    {
        if (card == null) return false;

        var gm = GameManager.Instance;
        if (gm == null || gm.player == null) return false;

        return gm.player.availibleMana >= card.cost && gm.currentState != GameState.EndGame;
    }

    /// <summary>Light or clear the "you can afford this" outline.</summary>
    public void SetPlayableOutline(bool on)
    {
        _playable = on;
        if (highlightOutline != null) highlightOutline.SetActive(on);
        ApplyHoveredOutline();
    }

    /// <summary>
    /// Light or clear the pointer-over outline. The request is remembered either way, but the outline
    /// only actually shows while the card is highlighted as playable — hovering a card you can't afford
    /// gives no outline, and it lights the moment the card becomes affordable under the pointer.
    /// </summary>
    public void SetHoveredOutline(bool on)
    {
        _hovered = on;
        ApplyHoveredOutline();
    }

    private void ApplyHoveredOutline()
    {
        if (hoveredOutline != null) hoveredOutline.SetActive(_hovered && _playable);
    }

    /// <summary>
    /// Spin the cost gear at a rate matching the card's cost, or freeze it when the card isn't playable.
    /// `playable` is passed in rather than derived from IsPlayableNow so the gear and the outline can't
    /// disagree: the caller decides once (see CardController.IsPlayableHandCard, which adds the
    /// ownership / in-hand / whose-turn tests that affordability alone doesn't cover).
    /// </summary>
    public void UpdateGearSpeed(CardModal card, bool playable)
    {
        if (gearRotateTween == null || card == null) return;

        // Spin rate scales with cost, but a playable card must always visibly spin — otherwise a
        // zero-cost card freezes (timeScale 0) and looks unaffordable. Floor the speed at 1 so a
        // cost-0 card spins like a cost-1 card.
        gearRotateTween.timeScale = playable ? Mathf.Max(card.cost, 1f) : 0f;
    }
    private void UpdateTexts(CardModal card)
    {
        nametext.text = card.name;
        desctext.text = CardTextFormatter.Format(card.desc, HighlightConfig);

        attacktext.text = card.attack.ToString();
        healthtext.text = card.health.ToString();
        costText.text = card.cost.ToString();
        int selectedIcon = 0;

        if(card.range == 1)
        {
            selectedIcon = 0;
        }
        else if(card.range == 2)
        {
            selectedIcon = 1;
        }
        else if (card.range == 3)
        {
            selectedIcon = 2;
        }
        for(int i = 0; i < minionTypeIconObjects.Length; i++) 
        {
            minionTypeIconObjects[i].SetActive(selectedIcon == i);
        }
    }

}
