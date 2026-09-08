using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wires the game's existing events to sounds, and is the only place that knows both.
///
/// Nothing in the gameplay code plays a sound. Every hook here rides an event that already existed for
/// something else, so adding, muting or re-mapping a sound is an edit to this one file (or, more often,
/// to Resources/AudioLibrary.asset) rather than a hunt through MinionController and GameManager. It is
/// also what stops the sound system becoming load-bearing: delete this file and the game still runs.
///
/// Binds once at startup and never unbinds -- every event it listens to is static, so subscriptions
/// outlive scene loads exactly as the events do, and unsubscribing per-scene would just mean rebinding.
/// </summary>
public static class GameAudioBinder
{
    private static bool _bound;

    // Domain reload being off ("Enter Play Mode Options") leaves _bound true from the previous session
    // while the static events have been reset, which would leave the game silent.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _bound = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bind()
    {
        if (_bound) return;
        _bound = true;

        // Turn flow. The player's turn END rides the switch rather than GameManager.OnTurnEnd: the switch
        // is the only way a player ends a turn (TurnManager is dead code), and it fires on the press,
        // which OnTurnEnd cannot -- it only knows the turn is already over.
        GameManager.OnTurnStarted += OnTurnStarted;
        SwitchController.HoldStarted += OnEndTurnHoldStarted;
        SwitchController.Switched += OnEndTurnSwitched;
        SwitchController.HoldEnded += OnEndTurnHoldEnded;
        SwitchController.SwitchAnimStarted += OnSwitchAnimStarted;
        GameManager.OnCardPlayed += OnCardPlayed;
        GameManager.OnCardPlayCancelled += OnCardPlayCancelled;
        GameManager.OnMinionSummoned += OnMinionSummoned;

        // Board
        MinionController.OnDied += OnMinionDied;
        MinionView.DamageShown += OnDamageShown;
        MinionController.OnCollided += OnMinionCollided;
        MinionController.OnAttacked += OnMinionAttacked;
        MinionController.OnSelectingMinionForAttack += OnSelectingAttackTarget;
        MinionController.OnMoved += OnMinionMoved;

        // Hand
        CardController.Peeked += OnCardPeeked;
        DraggableItem.DragStarted += OnDragStarted;
        DraggableItem.DragEnded += OnDragEnded;
        DraggableItem.DragCancelled += OnDragCancelled;
        Agent.CardDrawn += OnCardDrawn;

        // Player resources
        Player.OnPlayerManaChanged += OnManaChanged;

        // Panels and outcome
        CardChoice.Opened += OnCardChoiceOpened;
        CardChoice.Closed += OnCardChoiceClosed;
        PopupManager.GameOver += OnGameOver;
    }

    // ---------------------------------------------------------------------------------------------
    // Handlers.
    //
    // Every one of these fires during gameplay teardown as well (a scene unload runs minion OnDestroy,
    // which can cascade into deaths), so they all go through Play(), which is null- and quit-safe.
    // ---------------------------------------------------------------------------------------------

    private static void OnTurnStarted(GameState state)
    {
        Play(state == GameState.PlayerTurn ? GameSound.TurnStart : GameSound.OpponentTurnStart);
    }

    // The end-turn switch is a hold, so its sound is a pair: gears running under the press, then the
    // gear-change when it engages. The running loop is started on the press and stopped by BOTH exits
    // below, because the two can happen in either order -- the turn flips at 0.15s while the button is
    // usually still down, and a press abandoned before that never flips at all.
    //
    // Whether stopping fades or cuts is the asset's call (SoundEffect.fadeOut), not this file's -- how a
    // clip wants to end is a property of the clip, and a designer swapping it should not have to come
    // here to change the timing with it.
    private static void OnEndTurnHoldStarted() => Play(GameSound.TurnEndHoldStart);

    private static void OnEndTurnSwitched()
    {
        StopLooping(GameSound.TurnEndHoldStart);
        Play(GameSound.TurnEnd);
    }

    /// <summary>
    /// The press released. Idempotent against <see cref="OnEndTurnSwitched"/> having already stopped the
    /// loop -- stopping a sound that is not playing is a no-op -- which is what lets both exits be
    /// unconditional instead of tracking which one got there first.
    /// </summary>
    private static void OnEndTurnHoldEnded() => StopLooping(GameSound.TurnEndHoldStart);

    /// <summary>
    /// The opponent giving the turn back, cued to the switch animation rather than to a turn-flow event:
    /// GameManager raises OnTurnEnd well away from this moment, and the sound is meant to land with the
    /// switch the player is watching.
    /// </summary>
    private static void OnSwitchAnimStarted(bool throwingToPlayer)
    {
        // The player's own throw already sounds off the hold (TurnEndHoldStart -> TurnEnd), and would
        // otherwise be voiced twice.
        if (!throwingToPlayer) return;

        // SetupGame throws the switch to the player to open the match. That is not a turn ending, and
        // nothing has ended a turn yet, so it must stay silent.
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.currentState == GameState.Setup) return;

        Play(GameSound.OpponentTurnEnd);
    }

    private static void StopLooping(GameSound id)
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        manager.StopSound(manager.Library.Get(id));
    }

    private static void OnCardPlayed(Agent agent, CardSO card) => Play(GameSound.CardPlay);

    /// <summary>
    /// Backing out of a card that was already in the play area. The card goes back to the slot it came
    /// from, which is the same motion an abandoned drag makes, so it shares that sound -- the two are one
    /// gesture to the player (pick up, change your mind) regardless of how far the card got.
    /// </summary>
    private static void OnCardPlayCancelled(Agent agent, CardSO card)
    {
        // The AI's aborted plays come through here too, and nothing was ever shown moving for those.
        if (!(agent is Player)) return;

        Play(GameSound.CardReturn);
    }

    private static void OnCardDrawn(Agent agent, CardSO card)
    {
        // Only the player's draws. The opponent draws off-screen behind its deck, and hearing a card
        // that is never shown reads as a phantom.
        if (agent == null || !(agent is Player)) return;

        Play(GameSound.CardDraw);
    }

    private static void OnMinionSummoned(MinionController minion) => PlayAt(GameSound.MinionSummon, minion);

    private static void OnMinionDied(MinionController minion) => PlayAt(GameSound.MinionDeath, minion);

    /// <summary>
    /// Taking a hit. Rides the damage NUMBER appearing rather than MinionController.OnTookDamage, which
    /// fires the instant the value changes -- a third of a second before the player sees anything, and so
    /// well ahead of the strike it belongs to. What lands on screen and what is heard are now one moment.
    ///
    /// HeroController derives from MinionController, so hero damage arrives here too and is voiced by the
    /// same rule: a sword landing sounds like a sword landing whoever it hit. What separates a hit on the
    /// hero is the screen going red (DamageVignette), not a second vocabulary of hit sounds.
    /// </summary>
    private static void OnDamageShown(MinionController minion, int damage, DamageSource source) =>
        PlayAt(HitSoundFor(source), minion);

    private static GameSound HitSoundFor(DamageSource source)
    {
        switch (source)
        {
            case DamageSource.Melee: return GameSound.MinionHitMelee;
            case DamageSource.Ranged: return GameSound.MinionHitRanged;
            default: return GameSound.MinionHitEffect;
        }
    }

    private static void OnMinionCollided(MinionController minion, MinionController other) =>
        PlayAt(GameSound.MinionCollide, minion);

    /// <summary>
    /// A minion changing cell. Covers being pushed as well as moving under its own steam -- a shove is
    /// the same feet crossing the same board, and MinionController.Move is the one funnel both take.
    /// Played from where the minion starts, which is where the player is looking when the step begins.
    /// </summary>
    private static void OnMinionMoved(MinionController minion) => PlayAt(GameSound.MinionMove, minion);

    /// <summary>A minion's strike. Played from the ATTACKER, not the target: the swing is what makes the
    /// noise, and a ranged shot leaving the bow reads wrong panned to the other end of the board.</summary>
    private static void OnMinionAttacked(MinionController attacker, MinionController target)
    {
        PlayAt(IsRanged(attacker) ? GameSound.MinionAttackRanged : GameSound.MinionAttackMelee, attacker);
    }

    /// <summary>
    /// A minion picked up to attack with, heard on the click that lights its targets -- so the select and
    /// the strike that follows it are the same voice, melee or ranged.
    ///
    /// The attacker is not on the event, so it is read back off the request MinionController.Attack opened
    /// one line earlier. That is also the guard: BeginAttackRequest is inert while testing and off the
    /// player's turn, which leaves ActiveAttacker null, so the AI taking its turn stays silent here.
    /// </summary>
    private static void OnSelectingAttackTarget(List<MinionController> targets)
    {
        MinionController attacker = SelectionManager.Instance.ActiveAttacker;
        if (attacker == null) return;

        PlayAt(IsRanged(attacker) ? GameSound.MinionSelectRanged : GameSound.MinionSelectMelee, attacker);
    }

    /// <summary>
    /// Reach is the audio split -- a range 1 minion swings, a range 2+ one shoots. The same rule already
    /// picks the slash vs arrow animation in MinionController.Attack.
    /// </summary>
    private static bool IsRanged(MinionController minion) =>
        minion != null && minion.modal != null && minion.modal.range >= 2;

    private static void OnCardPeeked(CardController card) => Play(GameSound.CardHover);

    private static void OnDragStarted(Transform card) => Play(GameSound.CardPickUp);

    private static void OnDragEnded(Transform card) => Play(GameSound.CardDrop);

    private static void OnDragCancelled(Transform card) => Play(GameSound.CardReturn);

    private static void OnManaChanged(int value, int oldValue)
    {
        // Ignore the turn-start refill: it lands on the same frame as the turn stinger and would muddy it.
        if (value > oldValue) return;
        if (value == oldValue) return;

        Play(GameSound.ManaSpend);
    }

    private static void OnCardChoiceOpened(IReadOnlyList<CardSO> options, string prompt, bool faceDown) =>
        Play(GameSound.CardChoiceOpen);

    private static void OnCardChoiceClosed() => Play(GameSound.CardChoiceClose);

    private static void OnGameOver(bool won) => Play(won ? GameSound.Victory : GameSound.Defeat);

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private static void Play(GameSound id)
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        manager.Play(id);
    }

    /// <summary>
    /// Plays at a board entity's position, so a sound authored with spatial blend pans with the cell it
    /// came from. Falls back to a flat play when the entity has already been destroyed -- deaths in
    /// particular can be reported after the GameObject is gone.
    /// </summary>
    private static void PlayAt(GameSound id, MinionController minion)
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        if (minion == null) manager.Play(id);
        else manager.PlayAt(id, minion.transform.position);
    }
}
