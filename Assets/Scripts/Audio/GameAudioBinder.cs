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

        // Turn flow
        GameManager.OnTurnStarted += OnTurnStarted;
        GameManager.OnTurnEnd += OnTurnEnd;
        GameManager.OnCardPlayed += OnCardPlayed;
        GameManager.OnMinionSummoned += OnMinionSummoned;

        // Board
        MinionController.OnDied += OnMinionDied;
        MinionController.OnTookDamage += OnTookDamage;
        MinionController.OnCollided += OnMinionCollided;

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

    private static void OnTurnEnd(GameState state)
    {
        // Only the player's own turn ending is worth a sound -- the opponent's turn ending is the same
        // moment as the player's turn starting, and two stingers on one beat sound like a mistake.
        if (state == GameState.PlayerTurn) Play(GameSound.TurnEnd);
    }

    private static void OnCardPlayed(Agent agent, CardSO card) => Play(GameSound.CardPlay);

    private static void OnCardDrawn(Agent agent, CardSO card)
    {
        // Only the player's draws. The opponent draws off-screen behind its deck, and hearing a card
        // that is never shown reads as a phantom.
        if (agent == null || !(agent is Player)) return;

        Play(GameSound.CardDraw);
    }

    private static void OnMinionSummoned(MinionController minion) => PlayAt(GameSound.MinionSummon, minion);

    private static void OnMinionDied(MinionController minion) => PlayAt(GameSound.MinionDeath, minion);

    private static void OnTookDamage(MinionController minion, int damage)
    {
        // HeroController derives from MinionController, so hero damage arrives here too -- and a hit on
        // the hero is the one that decides the match, so it gets its own, heavier sound.
        PlayAt(minion is HeroController ? GameSound.HeroHit : GameSound.MinionHit, minion);
    }

    private static void OnMinionCollided(MinionController minion, MinionController other) =>
        PlayAt(GameSound.MinionCollide, minion);

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
