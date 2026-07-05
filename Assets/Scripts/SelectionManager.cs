using System;
using System.Collections.Generic;

/// <summary>
/// Central owner of the player's interactive selection lifecycles: picking a target for an attack
/// and picking a minion for a card/spell effect. Its job is to guarantee that starting OR cancelling
/// any selection FIRST tears down the previous one — restoring every highlighted entity's highlight
/// and collider — so no selection context can bleed into another. This is what keeps a minion that
/// was just used as a spell target fully attack-capable afterward.
///
/// Result delivery stays with the caller: the owning code passes a <c>resolve</c> callback that writes
/// the canonical result sink (e.g. <c>ActionHolder.selectedMinion</c> for spells, or a local for
/// attacks) and keeps its existing <c>while (result == null ...)</c> wait pattern. The AI never touches
/// this manager — it writes those sinks directly via the OnWaiting*Select events — and every Begin*
/// call is inert while testing or off the player's turn, so the headless TestCard path is unaffected.
///
/// Click routing no longer depends on a sticky <c>Player.curState</c> enum: <see cref="TryResolveClick"/>
/// asks "is there an active request, and is this a valid target?" A stuck state can no longer misroute
/// clicks, because the active request is always torn down on completion, cancel, preemption, card play,
/// and turn end.
/// </summary>
public class SelectionManager
{
    private static SelectionManager _instance;
    public static SelectionManager Instance
    {
        get
        {
            if (_instance == null) _instance = new SelectionManager();
            return _instance;
        }
    }

    public enum SelectionKind { Minion, AttackTarget }

    private sealed class SelectionRequest
    {
        public SelectionKind Kind;
        public HoverIntent Intent;
        public readonly List<MinionController> Highlighted = new List<MinionController>();
        public readonly HashSet<MinionController> ValidTargets = new HashSet<MinionController>();
        public MinionController Attacker;
        public CardSO SourceCard;
        public Action<MinionController> Resolve;
    }

    private SelectionRequest _active;

    /// <summary>True while an interactive minion/attack selection is waiting for a click.</summary>
    public bool HasActiveMinionRequest => _active != null;

    /// <summary>
    /// The minion whose attack selection is currently active (null otherwise). Used so that clicking
    /// the attacker again cancels its own attack instead of being treated as an invalid target.
    /// </summary>
    public MinionController ActiveAttacker =>
        _active != null && _active.Kind == SelectionKind.AttackTarget ? _active.Attacker : null;

    /// <summary>
    /// What the active request means for a hovered minion, so hover feedback can be picked from it.
    /// Falls back to <see cref="HoverIntent.SeeRange"/> when nothing is being selected.
    /// </summary>
    public HoverIntent ActiveIntent => _active != null ? _active.Intent : HoverIntent.SeeRange;

    /// <summary>
    /// True if <paramref name="minion"/> is a valid target of the active request (i.e. a lit, clickable
    /// candidate). Used so hover feedback like the death preview only fires on minions that can actually
    /// be picked, not on any minion hovered while a request happens to be open.
    /// </summary>
    public bool IsActiveTarget(MinionController minion) =>
        _active != null && minion != null && _active.ValidTargets.Contains(minion);

    /// <summary>
    /// The card driving the active minion pick (null for an attack pick or when nothing is active), so a
    /// hovered candidate can preview the card's outcome — e.g. show the skull when a damage spell is lethal.
    /// </summary>
    public CardSO ActiveCard => _active != null ? _active.SourceCard : null;

    /// <summary>Begin a spell/card minion pick over <paramref name="candidates"/>.</summary>
    public void BeginMinionRequest(IEnumerable<MinionController> candidates, Action<MinionController> resolve,
        HoverIntent intent = HoverIntent.ToSelectGenerally, CardSO sourceCard = null)
    {
        BeginRequest(SelectionKind.Minion, null, candidates, resolve, intent, sourceCard);
    }

    /// <summary>Begin an attack-target pick by <paramref name="attacker"/> over <paramref name="targets"/>.</summary>
    public void BeginAttackRequest(MinionController attacker, IEnumerable<MinionController> targets, Action<MinionController> resolve)
    {
        BeginRequest(SelectionKind.AttackTarget, attacker, targets, resolve, HoverIntent.ToAttack, null);
    }

    private void BeginRequest(SelectionKind kind, MinionController attacker, IEnumerable<MinionController> candidates, Action<MinionController> resolve, HoverIntent intent, CardSO sourceCard)
    {
        // Inert under testing and off the player's turn — the AI / TestCard paths resolve selections
        // by writing the result sinks directly and must not see manager-driven highlights.
        var gm = GameManager.Instance;
        if (gm == null || gm.isTesting || !gm.isPlayerTurn) return;

        // Any new selection (minion, attack, or cell) preempts the previous one with a full teardown.
        EndRequest();
        if (GridCellSelectionManager.Instance != null) GridCellSelectionManager.Instance.EndSelection();

        var request = new SelectionRequest
        {
            Kind = kind,
            Intent = intent,
            Attacker = attacker,
            SourceCard = sourceCard,
            Resolve = resolve,
        };

        if (candidates != null)
        {
            foreach (var minion in candidates)
            {
                if (minion == null) continue;
                if (!request.ValidTargets.Add(minion)) continue; // de-dupe
                request.Highlighted.Add(minion);
                if (minion.selectable != null) minion.selectable.SetSelectable(true);
            }
        }

        if (kind == SelectionKind.AttackTarget)
        {
            // While picking a target, only the enemy targets should be lit — turn off every friendly
            // attack highlight (the targets are enemies, so this never unlights them).
            var player = gm.player;
            if (player != null)
            {
                foreach (var friendly in player.minions)
                {
                    if (friendly == null) continue;
                    if (friendly.selectable != null) friendly.selectable.SetSelectable(false);
                    friendly.HideAttackHighlight();
                }
                if (player.hero != null)
                {
                    if (player.hero.selectable != null) player.hero.selectable.SetSelectable(false);
                    player.hero.HideAttackHighlight();
                }
            }

            // The attacker keeps its collider (so clicking it again cancels) but shows no highlight.
            if (attacker != null && attacker.selectable != null) attacker.selectable.SetClickableWithoutHighlight();
        }

        _active = request;
    }

    /// <summary>
    /// Routes a player click through the active request. Returns true if a request is active and the
    /// click was consumed — whether or not <paramref name="clicked"/> was a valid target — so the caller
    /// does NOT fall through to "begin a new attack". Returns false only when no request is active.
    /// </summary>
    public bool TryResolveClick(MinionController clicked)
    {
        if (_active == null) return false;
        if (clicked == null || !_active.ValidTargets.Contains(clicked))
            return true; // an active selection swallows clicks on invalid targets
        var resolve = _active.Resolve;
        if (resolve != null) resolve(clicked);
        return true;
    }

    /// <summary>End the active request after it resolved successfully.</summary>
    public void Complete() => EndRequest();

    /// <summary>Cancel the active request (right-click, attacker re-click, spell preempt, turn end, ...).</summary>
    public void Cancel() => EndRequest();

    private void EndRequest()
    {
        if (_active == null) return;

        var request = _active;
        _active = null; // clear first so the resting-state restore below sees no active request

        foreach (var minion in request.Highlighted)
        {
            if (minion == null) continue;
            if (minion.selectable != null) minion.selectable.SetSelectable(false);
        }
        request.Highlighted.Clear();

        // Restore resting clickability. While a card is resolving we leave highlights off — the card's
        // own completion (GameManager.ExecuteActions -> SetPlayerMinionsReadyToAttack) restores them —
        // so we don't flash attack options between a multi-target spell's selection steps.
        var gm = GameManager.Instance;
        if (gm != null && !gm.isPlayingCard)
        {
            gm.SetPlayerMinionsReadyToAttack();
        }
    }
}
