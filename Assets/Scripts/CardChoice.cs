using System;
using System.Collections.Generic;

/// <summary>
/// Central owner of "choose one of N cards" requests — the generic core behind the Discover mechanic.
///
/// Split from its UI the same way <see cref="SelectionManager"/> is split from the highlight/collider
/// work on each entity: this class owns the request lifecycle and click routing, and a listener
/// (<see cref="CardSelectionPanel"/>, on the Canvas) draws whatever is currently open. Any effect that
/// needs the player to pick one card out of a handful can call <see cref="Begin"/> — rolling a spell to
/// cast, adding a card to hand, picking a minion to summon — without touching UI code.
///
/// Named for the mechanic rather than the panel so it stays distinct from <see cref="SelectionManager"/>,
/// which owns the board-entity (minion / cell / attack target) selections.
///
/// Result delivery stays with the caller, like SelectionManager: the caller passes a <c>resolve</c>
/// callback that writes the canonical sink (<c>ActionHolder.chosenCard</c>) and keeps its existing
/// <c>while (result == null ...)</c> wait pattern.
///
/// Inert while testing and off the player's turn: those paths resolve the pick by writing
/// <c>ActionHolder.chosenCard</c> directly (via <c>ActionHolder.OnWaitingCardChoice</c>), so the headless
/// dry-run and the AI never wait on a panel that is never shown.
/// </summary>
public class CardChoice
{
    private static CardChoice _instance;
    public static CardChoice Instance
    {
        get
        {
            if (_instance == null) _instance = new CardChoice();
            return _instance;
        }
    }

    private sealed class ChoiceRequest
    {
        public readonly List<CardSO> Options = new List<CardSO>();
        public string Prompt;
        public Action<CardSO> Resolve;
    }

    private ChoiceRequest _active;

    /// <summary>
    /// True while the player is being asked to pick a card. Cancelling a card play is blocked while this
    /// holds: once the options are on screen the roll has been revealed, and letting the player back out
    /// would turn cancel into a free reroll (see GameManager.CancelPlayingCard).
    /// </summary>
    public bool HasActiveRequest => _active != null;

    /// <summary>The options currently on offer, empty when nothing is open.</summary>
    public IReadOnlyList<CardSO> ActiveOptions =>
        _active != null ? (IReadOnlyList<CardSO>)_active.Options : Array.Empty<CardSO>();

    /// <summary>Raised when a request opens, with the options to show and the prompt to label them.</summary>
    public static event Action<IReadOnlyList<CardSO>, string> Opened;

    /// <summary>Raised when the active request ends, however it ended (picked, cancelled, torn down).</summary>
    public static event Action Closed;

    /// <summary>
    /// Ask the player to pick one of <paramref name="options"/>. Duplicate and null entries are dropped,
    /// so a caller can pass a rolled list without pre-cleaning it. No-op if nothing usable is left.
    /// </summary>
    public void Begin(IEnumerable<CardSO> options, string prompt, Action<CardSO> resolve)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.isTesting || !gm.isPlayerTurn) return;

        // Any new choice preempts the previous one with a full teardown, mirroring SelectionManager.
        End();

        var request = new ChoiceRequest { Prompt = prompt, Resolve = resolve };

        if (options != null)
        {
            foreach (var option in options)
            {
                if (option == null) continue;
                if (request.Options.Contains(option)) continue; // de-dupe
                request.Options.Add(option);
            }
        }

        if (request.Options.Count == 0) return;

        _active = request;
        Opened?.Invoke(request.Options, prompt);
    }

    /// <summary>
    /// Route a click on one of the shown options. Returns false when nothing is open or the clicked card
    /// is not a live option, so the caller can ignore stray clicks.
    ///
    /// The request is torn down BEFORE the resolve callback runs, so anything the callback triggers sees
    /// <see cref="HasActiveRequest"/> already false.
    /// </summary>
    public bool TryResolveClick(CardSO picked)
    {
        if (_active == null) return false;
        if (picked == null || !_active.Options.Contains(picked)) return false;

        var resolve = _active.Resolve;
        End();
        resolve?.Invoke(picked);
        return true;
    }

    /// <summary>
    /// Tear the request down without picking. For lifecycle teardown (the waiting coroutine ending, turn
    /// end, game over) — NOT a player-facing "back out", which the Discover rules deliberately forbid.
    /// </summary>
    public void Cancel() => End();

    private void End()
    {
        if (_active == null) return;

        _active = null; // clear first so listeners see no active request
        Closed?.Invoke();
    }
}
