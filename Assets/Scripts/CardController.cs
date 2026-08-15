using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public class CardController : MonoBehaviour
{
    public CardModal modal;
    public CardView view;
    public CardSO card;
    public DraggableItem draggableItem;

    public CardHandLayout handLayout;

    public bool canPeek = true;
    public bool isPeeking = false;

    // Hand slot this card was in when the drag began, captured so a cancelled drag
    // (or an unplayable drop) returns it to its original position.
    private int _returnIndex = -1;
    public int ReturnIndex => _returnIndex;

    void Start()
    {


        DraggableItem.DragStarted += OnDragStarted;
        DraggableItem.DragEnded += OnDragEnded;
        DraggableItem.DragCancelled += OnDragCancelled;
    }

    private void OnDestroy()
    {
        DraggableItem.DragStarted -= OnDragStarted;
        DraggableItem.DragEnded -= OnDragEnded;
        DraggableItem.DragCancelled -= OnDragCancelled;
    }

    private void Update()
    {
        // The playable outline and the spinning cost gear both claim "you can play this right now", so
        // they share one condition: the card must be the player's own, still in their hand, on their
        // turn, and affordable. Note this is NOT gated on isPlayerMinion — that flag also means "drawn
        // face up", and the play-area reveal flips it on the OPPONENT's played card, which used to make
        // an enemy card light up and spin its gear off the PLAYER's mana. It also can't early-out when
        // the card doesn't qualify: the outline has to be actively cleared when the turn ends.
        bool playable = IsPlayableHandCard();
        view.UpdateGearSpeed(modal, playable);
        // Driven per-frame rather than on a mana-changed event: available mana moves from plays, refunds
        // and turn start, and the gear speed above already rides this same tick.
        view.SetPlayableOutline(playable);
    }

    /// <summary>
    /// Is this card the player's to play this instant — theirs, in their hand, their turn, affordable?
    /// Only drives the highlight; the real play gate is GameManager.PlayCard.
    /// </summary>
    private bool IsPlayableHandCard()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.CanPlayerPlayCards) return false;
        if (!IsPlayerHandCard()) return false;

        return CardView.IsPlayableNow(modal);
    }

    /// <summary>
    /// Is this card sitting in the player's own hand? The half of <see cref="IsPlayableHandCard"/>
    /// that isn't about timing or mana, split out for callers that care where the card is but not
    /// whether it can be played this instant (the tutorial hints).
    /// </summary>
    private bool IsPlayerHandCard()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null || modal == null) return false;
        // owner, not isPlayerMinion: ownership is fixed when the card is instantiated, while
        // isPlayerMinion gets rewritten by the reveal/return-to-deck flip animations.
        if (modal.owner != gm.player) return false;

        return gm.player.hand.Contains(this);
    }
    public void Initialize(Agent owner, bool isPlayerCard)
    {
        modal.UpdateModal(card, owner, isPlayerCard);
        view.UpdateView(modal);
    }
    public IEnumerator CanPlay(Agent owner, Action<bool> result)
    {
        if (modal.cost > owner.availibleMana)
        {
            result?.Invoke(false);
            yield break;
        }

        yield return StartCoroutine(GameManager.Instance.TestCard(this));

        if (GameManager.Instance.isTestingFailed)
        {
            result?.Invoke(false);
            yield break;
        }

        result?.Invoke(true);
    }

    // Kept for the UnityEvent binding on Card.prefab; play-on-click is handled via drag.
    public void OnPointerDown() { }

    public void OnPointerEnter()
    {
        if (!modal.isPlayerMinion) return;

        // Set before the peek guards below: the hover outline should follow the pointer even when the
        // card can't peek (mid-drag, already peeking, no room in the fan).
        view.SetHoveredOutline(true);

        // The tutorial's stat labels ride the same hover, for the opening turns of that match only.
        // In hand rather than the isPlayerMinion check above, which is also true of the OPPONENT's
        // card once the play-area reveal flips it face up.
        view.SetTutorialHints(IsPlayerHandCard() && GameManager.Instance.ShouldShowTutorialHints);

        if (!canPeek) return;
        if (handLayout == null || !handLayout.BeginPeek(this)) return;

        canPeek = false;
        isPeeking = true;

        // Only playable cards get a mana preview — show where the bar would land after playing this.
        if (ManaBarSlider.Instance != null && modal.cost <= GameManager.Instance.player.availibleMana)
        {
            ManaBarSlider.Instance.PreviewPlay(modal.cost);
        }

        transform.SetParent(handLayout.transform.parent);
        transform.SetSiblingIndex(handLayout.transform.parent.childCount - 1);
        transform.localRotation = Quaternion.identity;
        float peekY = handLayout.peekPosition != null
            ? handLayout.transform.parent.InverseTransformPoint(handLayout.peekPosition.position).y
            : -533f;
        transform.localPosition = new Vector3(transform.localPosition.x, peekY, transform.localPosition.z);
        transform.localScale = Vector3.one * 1.5f;

        // After the peek transform, so the tooltip stacks beside the lifted, enlarged card.
        KeywordTooltip.Show(modal.desc, (RectTransform)transform, this);
    }

    public void OnPointerExit()
    {
        if (!modal.isPlayerMinion) return;

        // Cleared unconditionally, mirroring OnPointerEnter — a card that was hovered without peeking
        // would otherwise keep its outline lit after the pointer left.
        view.SetHoveredOutline(false);
        view.SetTutorialHints(false);
        KeywordTooltip.Hide(this);

        if (!isPeeking) return;

        transform.localScale = Vector3.one;
        handLayout.EndPeek();
        if (ManaBarSlider.Instance != null) ManaBarSlider.Instance.ClearPreview();

        canPeek = true;
        isPeeking = false;
    }

    public void EnablePeek()
    {
        canPeek = true;
        isPeeking = false;
    }

    private void OnDragEnded(Transform draggedItem)
    {
        if (draggedItem == transform) return;
        EnablePeek();
    }

    private void OnDragCancelled(Transform draggedItem)
    {
        // Other cards just re-enable peeking, mirroring OnDragEnded.
        if (draggedItem != transform)
        {
            EnablePeek();
            return;
        }

        // Put the dragged card back into the hand fan at its original slot.
        if (handLayout != null)
        {
            handLayout.CancelPeek();
            // Guard against re-adding a card that's somehow still in the list, so the
            // fan never ends up with a duplicate (which throws off every card's angle).
            handLayout.RemoveCard(transform);
            int index = _returnIndex >= 0
                ? Mathf.Clamp(_returnIndex, 0, handLayout.cards.Count)
                : 0;
            handLayout.InsertCardAt(transform, index);
            draggableItem.ParentAfterDrag = handLayout.transform;
        }
        _returnIndex = -1;

        transform.DOComplete();
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        EnablePeek();
    }

    private void OnDragStarted(Transform draggedItem)
    {
        if (draggedItem == transform)
        {
            // This card is the one being dragged — take it out of the hand fan and
            // remember its slot so a cancelled drag can restore it. Doing this here
            // (whether or not the card was peeking) guarantees it leaves the layout's
            // `cards` list exactly once, so reinserting it on cancel can't duplicate it.
            if (handLayout != null)
            {
                if (isPeeking)
                {
                    _returnIndex = handLayout.PeekIndex;
                    handLayout.CancelPeek();
                }
                else
                {
                    _returnIndex = handLayout.RemoveCard(transform);
                }
            }
            if (isPeeking && ManaBarSlider.Instance != null) ManaBarSlider.Instance.ClearPreview();
            transform.localScale = Vector3.one;
            canPeek = false;
            isPeeking = false;
            return;
        }

        // Another card started dragging — if we were mid-peek, settle back into the
        // fan (EndPeek reinserts us) instead of being left orphaned.
        if (isPeeking && handLayout != null)
        {
            transform.localScale = Vector3.one;
            handLayout.EndPeek();
            if (ManaBarSlider.Instance != null) ManaBarSlider.Instance.ClearPreview();
        }
        canPeek = false;
        isPeeking = false;
    }
}
