using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DeckViewHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Transform Parent;
    public GameObject Shadow;
    public GameObject Shadow2;

    public List<Image> cards = new List<Image>();
    public Sprite cardBack;
    public Sprite upgradedCardBack;
    public TextMeshProUGUI cardCountText;
    public Image topcard;

    [Header("Messy Deck")]
    [Tooltip("Cards land slightly crooked, the way a real deck sits when you put it down.")]
    [SerializeField] private bool messyDeck = true;

    [Tooltip("Smallest tilt a freshly placed card can get, in degrees.")]
    [SerializeField, Range(0f, 20f)] private float minTilt = 1f;

    [Tooltip("Largest tilt a freshly placed card can get, in degrees.")]
    [SerializeField, Range(0f, 20f)] private float maxTilt = 4f;

    [Tooltip("How much messier a single card landing on the pile makes it, as a fraction of the full tilt range. " +
             "Small values mean the deck creeps towards messy over several cards instead of one card ruining it. " +
             "A whole deck put down at once ignores this and is messy from the start.")]
    [SerializeField, Range(0.05f, 1f)] private float messPerAddedCard = 0.25f;

    [Tooltip("How long a card takes to flop over to its crooked angle once it lands on the pile.")]
    [SerializeField] private float landTiltDuration = 0.18f;

    [SerializeField] private Ease landTiltEase = Ease.OutBack;

    [Tooltip("How long a card flying into the deck takes to fade down to the pile's tint. Keep this comfortably " +
             "shorter than the rest of the flight — the card is destroyed on landing, so a fade that runs the " +
             "whole way is finished only at the instant it disappears and reads as no fade at all.")]
    [SerializeField] private float incomingCardTintDuration = 0.4f;

    [SerializeField] private Ease incomingCardTintEase = Ease.OutQuad;

    [Tooltip("Clicking the deck straightens it out again.")]
    [SerializeField] private bool clickToTidy = true;

    [Tooltip("Clicks needed to fully square the deck up. Each one straightens the pile by the same amount, so " +
             "three clicks means three even steps from messy to square.")]
    [SerializeField, Range(1, 5)] private int clicksToTidy = 2;

    [Tooltip("How long a single card takes to straighten.")]
    [SerializeField] private float tidyDuration = 0.25f;

    [Tooltip("Delay between each card straightening, bottom of the deck first.")]
    [SerializeField] private float tidyStagger = 0.015f;

    [Tooltip("Ease for the last click, the one that squares the deck up completely.")]
    [SerializeField] private Ease tidyEase = Ease.OutBack;

    [Tooltip("Ease for the earlier clicks that only nudge the pile straighter.")]
    [SerializeField] private Ease partialTidyEase = Ease.OutQuad;

    private int originalTopCardIndexAsChild;
    private int cardCount;

    /// <summary>How many cards were showing last time <see cref="UpdateView"/> ran, so we can spot new ones.</summary>
    private int _shownCardCount;

    private bool _isTidy = true;

    /// <summary>How many tidy clicks the player has spent on the current mess.</summary>
    private int _tidyClicks;

    /// <summary>
    /// Cards revealed by a <see cref="UpdateView"/> that asked to defer the mess: they sit straight until the
    /// card flying into the deck actually lands, then <see cref="SettleIncomingCards"/> knocks them crooked.
    /// </summary>
    private readonly List<Transform> _awaitingLanding = new List<Transform>();

    void Start()
    {
    }

    /// <param name="deferMess">
    /// True when a card is currently flying into the deck: the new card shows up straight and only tilts once
    /// the caller reports the flight finished via <see cref="SettleIncomingCards"/>.
    /// </param>
    public void UpdateView(int cardCount, bool isTopCardUpgraded, bool deferMess = false)
    {
        this.cardCount = cardCount;

        if (topcard != null)
        {
            topcard.transform.SetSiblingIndex(originalTopCardIndexAsChild);
            topcard.sprite = cardBack;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            bool visible = i < cardCount;
            cards[i].gameObject.SetActive(visible);

            // Anything showing that wasn't showing before is a card just placed on the pile, so it lands crooked.
            if (visible && i >= _shownCardCount)
            {
                if (deferMess)
                {
                    HoldCardStraight(cards[i].transform);
                }
                else
                {
                    DropCardCrooked(cards[i].transform, landed: false);
                }
            }

            if(i == cardCount - 1)
            {
                topcard = cards[i];
            }
        }
        _shownCardCount = cardCount;
        cardCountText.text = cardCount.ToString();
        //Parent.gameObject.SetActive(cardCount > 0);
        Shadow.SetActive(cardCount > 0);
        Shadow2.SetActive(cardCount > 0);

        if (topcard == null)
        {
            return;
        }


        originalTopCardIndexAsChild = topcard.transform.GetSiblingIndex();
        topcard.transform.SetSiblingIndex(transform.childCount-2);
        topcard.sprite = isTopCardUpgraded ? upgradedCardBack : cardBack;   

        cardCountText.transform.parent.position = topcard.transform.position;   
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardCount == 0) return;
        cardCountText.transform.parent.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        cardCountText.transform.parent.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!clickToTidy) return;
        TidyDeck();
    }

    /// <summary>
    /// Straightens the pile from the bottom card upwards. It takes <see cref="clicksToTidy"/> goes to square the
    /// deck up completely, and every go makes the same amount of progress, so the pile visibly comes straight in
    /// even steps rather than mostly snapping true on the first click. Does nothing once the deck is already tidy.
    /// </summary>
    public void TidyDeck()
    {
        if (_isTidy || cardCount == 0) return;

        _tidyClicks++;
        bool finalPass = _tidyClicks >= clicksToTidy;

        // Each click takes an even share of whatever tilt is left: with three clicks the first leaves two thirds,
        // the second leaves half of that, and the last takes it all — three equal bites out of the original mess.
        // Reading it off the clicks remaining rather than the tilt means a card landing mid-tidy just gets folded
        // into the shares still to come.
        int clicksLeft = Mathf.Max(1, clicksToTidy - _tidyClicks + 1);
        float remainder = finalPass ? 0f : (clicksLeft - 1f) / clicksLeft;

        int straightened = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            Transform card = cards[i].transform;
            card.DOKill();

            // Hidden cards can't run a tween while inactive; just square them up so they're clean when reused.
            if (i >= cardCount)
            {
                card.localRotation = Quaternion.identity;
                continue;
            }

            float tilt = SignedTilt(card);
            if (Mathf.Approximately(tilt, 0f)) continue;

            float target = tilt * remainder;
            DOTween.To(() => tilt, v => { tilt = v; card.localRotation = Quaternion.Euler(0f, 0f, v); }, target, tidyDuration)
                .SetDelay(straightened * tidyStagger)
                .SetEase(finalPass ? tidyEase : partialTidyEase)
                .SetTarget(card);
            straightened++;
        }

        if (!finalPass) return;

        _isTidy = true;
        _tidyClicks = 0;
    }

    /// <summary>
    /// Reports that the cards flying into the deck have landed, so the slots held straight for them can flop
    /// over to their crooked angle. Safe to call when nothing is pending.
    /// </summary>
    public void SettleIncomingCards()
    {
        for (int i = 0; i < _awaitingLanding.Count; i++)
        {
            Transform card = _awaitingLanding[i];

            // The card may have been drawn straight back off the pile while it was still in the air.
            if (card == null || !card.gameObject.activeInHierarchy) continue;

            DropCardCrooked(card, landed: true);
        }

        _awaitingLanding.Clear();
    }

    /// <summary>
    /// Fades the back of a card flying into the deck down to the pile's tint over the rest of its flight, so it
    /// settles into the stack instead of landing bright on dull backs. The card is face down by this point, so
    /// its back is all that shows. The tint is read off the deck's own cards rather than stored separately, so
    /// the incoming card is guaranteed to land on exactly the colour of the pile it joins.
    /// </summary>
    /// <param name="duration">Negative uses <see cref="incomingCardTintDuration"/>.</param>
    public void TintCardToDeck(Image cardBack, float duration = -1f)
    {
        if (cardBack == null || cards.Count == 0 || cards[0] == null) return;

        Color tint = cards[0].color;
        tint.a = cardBack.color.a;

        float time = duration < 0f ? incomingCardTintDuration : duration;

        if (time <= 0f)
        {
            cardBack.color = tint;
            return;
        }

        cardBack.DOColor(tint, time).SetEase(incomingCardTintEase);
    }

    /// <summary>Reveals a card square with the pile, to be knocked crooked once the incoming card lands.</summary>
    private void HoldCardStraight(Transform card)
    {
        card.DOKill();
        card.localRotation = Quaternion.identity;

        if (!_awaitingLanding.Contains(card)) _awaitingLanding.Add(card);
    }

    /// <summary>Drops a card onto the pile at a random angle, leaving the deck needing a tidy.</summary>
    /// <param name="landed">
    /// True for a single card arriving on a pile that's already there: it settles only slightly crooked and
    /// knocks the cards under it a little further out of true, so the deck creeps towards messy one card at a
    /// time. False for a whole deck being put down at once, which is messy from the first moment.
    /// </param>
    private void DropCardCrooked(Transform card, bool landed)
    {
        if (!messyDeck)
        {
            card.DOKill();
            card.localRotation = Quaternion.identity;
            return;
        }

        SetTilt(card, landed ? RandomTilt() * messPerAddedCard : RandomTilt(), landed ? landTiltDuration : 0f);

        if (landed) NudgePileMessier(card);

        // A fresh card means the pile needs the full run of clicks again.
        _isTidy = false;
        _tidyClicks = 0;
    }

    /// <summary>
    /// Knocks the cards already on the pile a little further out of true as a new one lands on them. Each card
    /// drifts by a random step rather than being re-rolled, so the pile keeps the shape it had and just loosens
    /// — a few cards in and it looks properly shuffled-together, without any single card causing a jump.
    /// </summary>
    private void NudgePileMessier(Transform landedCard)
    {
        float limit = Mathf.Max(minTilt, maxTilt);
        float step = limit * messPerAddedCard;

        for (int i = 0; i < cards.Count && i < cardCount; i++)
        {
            Transform card = cards[i].transform;

            // Skip the card that just landed, and any still in the air waiting to be settled.
            if (card == landedCard || _awaitingLanding.Contains(card)) continue;

            float target = Mathf.Clamp(SignedTilt(card) + Random.Range(-step, step), -limit, limit);
            SetTilt(card, target, landTiltDuration);
        }
    }

    /// <summary>Turns a card to a tilt, tweening if given a duration and snapping if not.</summary>
    private void SetTilt(Transform card, float target, float duration)
    {
        card.DOKill();

        if (duration <= 0f)
        {
            card.localRotation = Quaternion.Euler(0f, 0f, target);
            return;
        }

        float from = SignedTilt(card);
        DOTween.To(() => from, v => { from = v; card.localRotation = Quaternion.Euler(0f, 0f, v); }, target, duration)
            .SetEase(landTiltEase)
            .SetTarget(card);
    }

    private float RandomTilt()
    {
        float low = Mathf.Min(minTilt, maxTilt);
        float high = Mathf.Max(minTilt, maxTilt);
        float magnitude = Random.Range(low, high);
        return Random.value < 0.5f ? -magnitude : magnitude;
    }

    /// <summary>Reads a card's tilt back as a signed angle, since localEulerAngles reports -3° as 357°.</summary>
    private static float SignedTilt(Transform card)
    {
        float z = card.localEulerAngles.z;
        return z > 180f ? z - 360f : z;
    }
}
