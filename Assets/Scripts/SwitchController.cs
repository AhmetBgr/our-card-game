using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class SwitchController : MonoBehaviour
{
    public Animator animator;
    private float longPressDur = 0.15f;
    private float _holdStartTime;
    private bool _isHolding = false;
    private Coroutine _longPressCoroutine;

    [Tooltip("Tutorial-only hint shown beside this switch once the player has nothing left to do — no card " +
             "they can pay for and no unit with an attack still available — so a first-timer knows the turn " +
             "is theirs to end. TutorialPrompt owns the tutorial-only rule and the fade; this only decides " +
             "WHEN. Optional: leave empty for no hint.")]
    [SerializeField] private TutorialPrompt endTurnPrompt;

    //public static event Action PointerLongPress;

    private void OnEnable()
    {
        //GameManager.OnTurnSwitch += PlaySwitchAnim;
    }

    private void OnDisable()
    {
        //GameManager.OnTurnSwitch -= PlaySwitchAnim;

    }

    // Polled rather than driven off a mana-changed event, for the same reason CardController re-checks
    // its own playability every frame: what the player can afford moves on plays, refunds, turn start and
    // card draws, and a predicate cannot fall out of sync with those the way a subscription can. Show and
    // Hide are both idempotent, so calling them every frame costs a bool check once the state has settled.
    private void Update()
    {
        if (endTurnPrompt == null) return;

        if (NothingLeftToDo()) endTurnPrompt.Show();
        else endTurnPrompt.Hide();
    }

    /// <summary>
    /// True when the player is on the clock and has neither a card they can pay for NOR a unit that can
    /// still strike — the turn really is finished, and holding the switch is the only move left. An empty
    /// hand and a bare board reach the same answer, which is correct: that is the same dead end.
    ///
    /// Both halves defer to the definitions the rest of the game already uses, so the hint cannot contradict
    /// what the player is looking at: a card still lit as playable, or a minion still wearing its sword.
    /// </summary>
    private static bool NothingLeftToDo()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null) return false;

        // CanPlayerPlayCards covers whose turn it is and the turn state; isPlayingCard covers the window
        // where a card is mid-resolution and the switch would be the wrong thing to reach for.
        if (!gm.CanPlayerPlayCards || gm.isPlayingCard) return false;

        return !HasPlayableCard(gm) && !HasAvailableAttack(gm);
    }

    /// <summary>
    /// Any card in hand the player can pay for. Affordability goes through
    /// <see cref="CardView.IsPlayableNow"/>, the one definition the cost gear and the playable outline
    /// already share.
    /// </summary>
    private static bool HasPlayableCard(GameManager gm)
    {
        var hand = gm.player.hand;
        if (hand == null) return false;

        foreach (var card in hand)
        {
            if (card == null || card.modal == null) continue;
            if (CardView.IsPlayableNow(card.modal)) return true;
        }

        return false;
    }

    /// <summary>
    /// Any friendly unit that still has an attack AND something in range to spend it on.
    ///
    /// <see cref="MinionController.CanAttack"/> answers both halves and is the very predicate the AI uses
    /// to enumerate its own attack actions, so "has an attack option" means the same thing for both sides.
    /// The hero is checked alongside the minions because it attacks too and is held separately from
    /// <c>minions</c> — exactly as <see cref="GameManager.SetPlayerMinionsReadyToAttack"/> treats it.
    /// </summary>
    private static bool HasAvailableAttack(GameManager gm)
    {
        // CanAttack walks the enemy's roster for a target, so it needs one to walk.
        if (gm.opponent == null) return false;

        if (gm.player.hero != null && gm.player.hero.CanAttack(gm.opponent)) return true;

        var minions = gm.player.minions;
        if (minions == null) return false;

        foreach (var minion in minions)
        {
            if (minion != null && minion.CanAttack(gm.opponent)) return true;
        }

        return false;
    }

    public void PlaySwitchAnim(bool isPlayerTurn)
    {
        if(isPlayerTurn)
        {
            animator.Play("PlayerSwitchAnim");
        }
        else
        {
            animator.Play("OpponentSwitchAnim");
        }
    }

    public void OnMouseEnter()
    {
        if (GameManager.Instance == null || !GameManager.Instance.isPlayerTurn) return;
        GameManager.Instance.ShowMovePreview();
    }

    public void OnMouseExit()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.HideMovePreview();
    }

    public void OnMouseDown() {
        if(!GameManager.Instance.isPlayerTurn) return;

        animator.Play("OpponentFailedSwitch");

        // Start long press coroutine
        _isHolding = true;
        _longPressCoroutine = StartCoroutine(LongPressCheck());
    }

    public void OnMouseUp()
    {
        // Stop long press check if released early
        _isHolding = false;
        if (_longPressCoroutine != null)
        {
            StopCoroutine(_longPressCoroutine);
            _longPressCoroutine = null;
        }
    }
    private IEnumerator LongPressCheck()
    {
        yield return new WaitForSeconds(longPressDur);

        if (_isHolding)
        {
            StartCoroutine(GameManager.Instance.EndPlayerTurn());

            //PointerLongPress?.Invoke();
        }
    }

}
