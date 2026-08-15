using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayArea : Singleton<PlayArea>, IDropHandler
{
    public Transform cardPos;
    public Transform opponentCardPos;

    [Tooltip("Canvas/CardParent - where a card lives while it is being played. Kept off CardPlayArea " +
             "deliberately: this container sits above the hand layouts but below CardSelectionPanel and " +
             "GameOverPanel, so a played card draws over the board without ever covering a modal.")]
    public Transform cardParent;

    /// <summary>
    /// Where a card being played should be parented. Falls back to this object so a scene that has not
    /// wired <see cref="cardParent"/> keeps the old behaviour instead of dropping the card to the root.
    /// </summary>
    public Transform PlayedCardParent => cardParent != null ? cardParent : transform;

    [Header("Played card (player)")]

    [Tooltip("Uniform scale the player's card tweens to as it flies to cardPos. Applied as localScale " +
             "under PlayedCardParent, so it only matches on-screen size while that container sits at " +
             "scale 1. The hand peek lifts a card to 1.5, so anything below that shrinks on play.")]
    [SerializeField] private float playedCardScale = 1f;

    [Tooltip("Seconds for the move/scale tween that carries the played card to cardPos.")]
    [SerializeField] private float playedCardTweenDuration = 0.25f;

    [Header("Played card (opponent)")]

    [Tooltip("Same idea as the player's scale above, and directly comparable to it — the AI's card is " +
             "parented to the very same container. It ships larger on purpose: the AI's card also plays " +
             "a face-reveal flip on arrival and its effects resolve during that window, so it has to be " +
             "readable at a glance in a way your own card (which you chose) does not.")]
    [SerializeField] private float opponentPlayedCardScale = DefaultOpponentPlayedCardScale;

    [Tooltip("Seconds for the move/scale tween that carries the AI's card to opponentCardPos.")]
    [SerializeField] private float opponentPlayedCardTweenDuration = DefaultOpponentPlayedCardTweenDuration;

    [Tooltip("Seconds the AI's card is held at opponentCardPos after its effects resolve, before it " +
             "shrinks away. This is the beat that lets you read what was just played — the player's own " +
             "card has no equivalent and vanishes immediately. Cutting it to 0 makes AI plays hard to follow.")]
    [SerializeField] private float opponentPlayedCardHoldDuration = DefaultOpponentPlayedCardHoldDuration;

    private const float DefaultOpponentPlayedCardScale = 1.5f;
    private const float DefaultOpponentPlayedCardTweenDuration = 0.5f;
    private const float DefaultOpponentPlayedCardHoldDuration = 1f;

    // Static for the same reason as the forged-card accessors below: the AI's play animation is driven
    // from GameManager, not from here.
    public static float OpponentPlayedCardScale => Instance != null ? Instance.opponentPlayedCardScale : DefaultOpponentPlayedCardScale;
    public static float OpponentPlayedCardTweenDuration => Instance != null ? Instance.opponentPlayedCardTweenDuration : DefaultOpponentPlayedCardTweenDuration;
    public static float OpponentPlayedCardHoldDuration => Instance != null ? Instance.opponentPlayedCardHoldDuration : DefaultOpponentPlayedCardHoldDuration;

    [Header("Forged card")]

    [Tooltip("Uniform scale the upgraded copy pops to after the card that forged it is played. It is " +
             "spawned at the same spot the played card just vanished from (Agent.cardPlayPos IS this " +
             "object's cardPos), so this is what the player reads as 'the forged card', not the scale " +
             "above. Parented under UICanvas rather than CardParent — both sit at 0.75, so the two " +
             "numbers are directly comparable.")]
    [SerializeField] private float forgedCardPopScale = DefaultForgedCardPopScale;

    [Tooltip("Seconds for the pop-in from zero to forgedCardPopScale.")]
    [SerializeField] private float forgedCardPopDuration = DefaultForgedCardPopDuration;

    [Tooltip("Seconds the forged card is held at full size before it jumps to the deck.")]
    [SerializeField] private float forgedCardHoldDuration = DefaultForgedCardHoldDuration;

    private const float DefaultForgedCardPopScale = 1.2f;
    private const float DefaultForgedCardPopDuration = 0.25f;
    private const float DefaultForgedCardHoldDuration = 0.5f;

    // Static, and fall back to the constants above, so Agent.SpawnCardToDeck reads them without a
    // null-check at every use: the forge animation runs for BOTH agents, and a scene that plays cards
    // without a PlayArea (tests, a stripped scene) must still animate rather than snap to zero.
    public static float ForgedCardPopScale => Instance != null ? Instance.forgedCardPopScale : DefaultForgedCardPopScale;
    public static float ForgedCardPopDuration => Instance != null ? Instance.forgedCardPopDuration : DefaultForgedCardPopDuration;
    public static float ForgedCardHoldDuration => Instance != null ? Instance.forgedCardHoldDuration : DefaultForgedCardHoldDuration;

    [Header("Tutorial drop-zone hint")]

    [Tooltip("Faded in while the player drags a card, in the TUTORIAL MATCH ONLY, so a first-time player " +
             "can see where a card is meant to go. Leave empty to use this object's own Image — that " +
             "graphic IS the drop target (a transparent raycast catcher filling the canvas rect), so what " +
             "lights up is exactly the area a drop is accepted in. Point it at a dedicated child graphic " +
             "instead to call out a smaller region.")]
    [SerializeField] private Graphic dropZoneHighlight;

    [Tooltip("Colour the highlight fades TO while a card is being dragged. The alpha is what makes the " +
             "zone readable; keep it low, since the default target is a large area sitting over the board. " +
             "The resting colour is read off the graphic at Awake, so it is never hard-coded here and " +
             "whatever you author stays the 'off' state.")]
    [SerializeField] private Color dropZoneHighlightColor = new Color(1f, 1f, 1f, 0.08f);

    [Tooltip("The \"drag here to play\" prompt, brought in and out with the zone. Optional: leave it empty " +
             "for a zone with no wording. The wording itself is authored on the prompt's own label — this " +
             "does not set it — and TutorialPrompt owns the tutorial-only rule, the fade and the " +
             "activation, so it behaves exactly like the cell-pick hint.")]
    [SerializeField] private TutorialPrompt dropZonePrompt;

    [Tooltip("Seconds for the ZONE TINT fade, in each direction. The prompt has its own duration on " +
             "TutorialPrompt; keep the two equal so the tint and the wording arrive together.")]
    [SerializeField] private float dropZoneFadeDuration = 0.15f;

    // Resolved once in Awake so the per-drag path does no lookups. The resting colour is captured from
    // the graphic rather than assumed, so fading back always lands on exactly what was authored.
    private Graphic dropZone;
    private Color dropZoneRestColor;
    private Tween dropZoneTween;

    // True from the moment a drop starts resolving (CanPlay test) until it finishes.
    // Set synchronously so a second card dropped during the async test window is
    // rejected instead of being played on top of the first.
    private bool isResolvingDrop = false;

    protected override void Awake()
    {
        base.Awake();

        dropZone = dropZoneHighlight != null ? dropZoneHighlight : GetComponent<Graphic>();
        if (dropZone != null) dropZoneRestColor = dropZone.color;

        if (dropZonePrompt != null) dropZonePrompt.HideImmediate();
    }

    // Driven by DraggableItem's existing drag events rather than by a hook inside OnDrop: the hint has to
    // appear when the drag STARTS (that is when the player needs to know where to aim) and clear however
    // the drag ends — dropped here, dropped somewhere invalid, or right-click cancelled. DragEnded covers
    // the first, DragCancelled the other two.
    private void OnEnable()
    {
        DraggableItem.DragStarted += ShowDropZone;
        DraggableItem.DragEnded += HideDropZone;
        DraggableItem.DragCancelled += HideDropZone;
    }

    private void OnDisable()
    {
        DraggableItem.DragStarted -= ShowDropZone;
        DraggableItem.DragEnded -= HideDropZone;
        DraggableItem.DragCancelled -= HideDropZone;

        // Never leave the board tinted behind us — the scene can go away mid-drag (restart / exit), and
        // a half-finished fade would otherwise be the last thing written to the graphic.
        if (dropZoneTween != null) dropZoneTween.Kill();
        if (dropZone != null) dropZone.color = dropZoneRestColor;
        if (dropZonePrompt != null) dropZonePrompt.HideImmediate();
    }

    /// <summary>
    /// Tutorial only. Checked per drag rather than cached at Awake so it costs nothing to reason about:
    /// <see cref="GameManager.IsTutorialMatch"/> is latched for the whole match anyway. Cards outside the
    /// player's hand never reach here — DraggableItem lives only on real hand cards and refuses to begin
    /// a drag off the player's turn — so there is no "whose card is this" test to make.
    /// </summary>
    private void ShowDropZone(Transform card)
    {
        if (!GameManager.IsTutorialMatch) return;

        SetDropZoneShown(true);
    }

    // Not gated on the tutorial: hiding has to work unconditionally, or a match that stopped being the
    // tutorial mid-drag (it cannot today, but nothing here should depend on that) would stay lit.
    private void HideDropZone(Transform card) => SetDropZoneShown(false);

    private void SetDropZoneShown(bool shown)
    {
        if (dropZone != null)
        {
            // Killed rather than allowed to stack: picking a card straight back up mid-fade would
            // otherwise run two colour tweens at once and settle on whichever finished last. Unscaled, so
            // a pause menu opened mid-drag (Time.timeScale 0, which does not stop uGUI drag events) can't
            // strand the fade halfway.
            if (dropZoneTween != null) dropZoneTween.Kill();

            dropZoneTween = dropZone
                .DOColor(shown ? dropZoneHighlightColor : dropZoneRestColor, dropZoneFadeDuration)
                .SetUpdate(true);
        }

        // Activation, fading and the tutorial-only rule all belong to TutorialPrompt — the cell-pick hint
        // needs the identical behaviour, and two copies of it would have drifted.
        if (dropZonePrompt == null) return;

        if (shown) dropZonePrompt.Show();
        else dropZonePrompt.Hide();
    }

    public virtual void OnDrop(PointerEventData eventData)
    {
        CardController droppedItem = eventData.pointerDrag.GetComponent<CardController>();

        if (droppedItem == null)
        {
            Debug.Log("cant play");

            return;
        }

        // The drag was cancelled (right click) before release — don't play it.
        if (droppedItem.draggableItem != null && !droppedItem.draggableItem.isdragging)
            return;

        // A card is already resolving or being played — reject this drop. dropHandled
        // is left false so OnEndDrag returns the card to its slot in hand.
        if (isResolvingDrop || GameManager.Instance.isPlayingCard)
            return;

        // Not the player's turn — same treatment. Checked here as well as in PlayCard so the card is
        // never yanked out of the fan and flown to the play area only to be rejected on arrival.
        if (!GameManager.Instance.CanPlayerPlayCards)
            return;

        // Mark the drop as landing on a valid target so OnEndDrag doesn't cancel it.
        if (droppedItem.draggableItem != null)
            droppedItem.draggableItem.dropHandled = true;

        isResolvingDrop = true;

        var layout = droppedItem.handLayout;
        // The card is pulled out of the fan when the drag begins, so its original slot
        // is the index captured on the card rather than a live IndexOf lookup.
        int originalIndex = droppedItem.ReturnIndex;

        bool canPlay = false;
        Debug.Log("testing: ");
        StartCoroutine(droppedItem.CanPlay(GameManager.Instance.player, result =>
        {
            canPlay = result;
            Debug.Log("canplay result: " + result);

            droppedItem.handLayout.CancelPeek();
            Debug.Log($"canplay {canPlay}, isplaying card {GameManager.Instance.isPlayingCard}");
            if (canPlay && !GameManager.Instance.isPlayingCard)
            {
                Debug.Log("shoıuld play");
                droppedItem.isPeeking = false;
                droppedItem.canPeek = false;

                // Take the card out of the layout so its UpdateCardPositions Lerp
                // doesn't fight DOMove and pull the card back to the fan.
                droppedItem.handLayout.RemoveCard(droppedItem.transform);

                // Reparent HERE, not through ParentAfterDrag. Unity sends OnDrop and then OnEndDrag in
                // the same frame the button is released, but this callback only runs once the CanPlay
                // coroutine finishes — a frame or more later. OnEndDrag has therefore already consumed
                // ParentAfterDrag and put the card back under the hand layout, so assigning it now
                // cannot move anything. ParentAfterDrag is still set so the two agree in the event the
                // play test ever completes without yielding.
                droppedItem.draggableItem.ParentAfterDrag = PlayedCardParent;
                droppedItem.transform.SetParent(PlayedCardParent);
                droppedItem.transform.SetAsLastSibling();

                droppedItem.transform.DOMove(cardPos.position, playedCardTweenDuration);
                droppedItem.transform.DOScale(Vector3.one * playedCardScale, playedCardTweenDuration);

                // PlayCard sets isPlayingCard synchronously, so that flag guards further
                // drops from here on; this drop is done resolving.
                isResolvingDrop = false;
                StartCoroutine(GameManager.Instance.PlayCard(droppedItem, GameManager.Instance.player));
            }
            else
            {
                isResolvingDrop = false;
                // RemoveCard first so an already-listed card can't be inserted twice.
                layout.RemoveCard(droppedItem.transform);
                int insertIndex = Mathf.Clamp(originalIndex >= 0 ? originalIndex : 0, 0, layout.cards.Count);
                layout.InsertCardAt(droppedItem.transform, insertIndex);
                droppedItem.transform.DOComplete();
                droppedItem.transform.localRotation = Quaternion.identity;
                droppedItem.transform.localScale = Vector3.one;
                droppedItem.draggableItem.ParentAfterDrag = layout.transform;
                droppedItem.EnablePeek();
            }
        }));






    }
}
