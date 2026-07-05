using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MinionController : MonoBehaviour
{
    public SelectableEntity selectable;
    public CardModal modal;
    //public MinionModal modal;
    //public ShowInfo showInfo;

    public MinionView view;
    public CardSO card;
    public GridEntity gridEntity;
    public Animator animator;
    public MinionAnimationController animationController;
    public Agent owner;

    public SelectionType selectionType;

    public Vector3Int plannedMoveDir = Vector3Int.zero;
    public bool isMovementValidated = false;
    public bool isAttackedThisTurn = false;
    public bool canAttack = false;
    public int age = 0;

    public MinionController LastTarget;

    [Header("Attack Ready Indicator")]
    // The sword icon shown on a minion while it still has its attack this turn. It reflects attack
    // *eligibility* only: it stays lit even when no enemy is currently in range (the minion is still
    // "ready", it just has nothing to hit yet). Being selectable/clickable still requires a target.
    public SpriteRenderer attackHighlight;
    // Source of truth for whether the sword indicator should rest lit (set by SetReadyToAttack). Transient
    // overlays that share the minion's face — the move-preview arrow, attack-target selection — hide the
    // sword renderer without clearing this, and RefreshAttackHighlight() restores it from this flag when
    // the overlay goes away.
    private bool _attackReady;

    [Header("Move Preview")]
    public SpriteRenderer moveArrow;
    public Color moveArrowColor = Color.white;
    public Color moveArrowCollideColor = Color.yellow;

    [Header("Attack Preview")]
    // Shown while this minion is hovered as an attack target and the pending attacker's strike would kill it.
    public GameObject skull;
    public float skullBreathMinAlpha = 0.25f;
    public float skullBreathDuration = 0.6f;
    private SpriteRenderer _skullRenderer;
    private Color _skullBaseColor;
    private bool _skullResolved;
    private Tween _skullTween;

    [Header("Stat Change Preview")]
    // Shown while this minion is hovered as a card target and the card would change its stats WITHOUT
    // killing it (a buff, or a non-lethal debuff). Two separate labels sit above the minion's attack and
    // health numbers, each tinted for its stat (attack yellow, health red) and breathing alpha in sync.
    // The skull owns the lethal case, so the skull and these labels are never visible at once.
    public TMP_Text statChangeAttackText;
    public TMP_Text statChangeHealthText;
    // The background containers that hold each label. These are what we actually show/hide and breathe (the
    // text stays active inside), so the label's background image toggles and fades along with the number.
    public CanvasGroup statChangeAttackGroup;
    public CanvasGroup statChangeHealthGroup;
    // The number's color encodes the direction of the change: green for a buff, red for a debuff. (Which
    // stat changed is already clear from position — each label sits above its own attack/health number.)
    public Color statBuffColor = new Color(0.25f, 0.9f, 0.3f, 1f);  // green
    public Color statDebuffColor = new Color(1f, 0.3f, 0.3f, 1f);   // red
    public float statChangeBreathMinAlpha = 0.25f;
    public float statChangeBreathDuration = 0.6f;
    private Tween _statChangeTween;
    // The background image behind each number (the container's Graphic) is what breathes; the number text
    // itself stays solid so the value is always readable. Cached (with its authored color) on first show.
    private Graphic _statChangeAttackBg;
    private Graphic _statChangeHealthBg;
    private Color _statChangeAttackBgBase;
    private Color _statChangeHealthBgBase;
    private bool _statChangeBgResolved;

    public SelectionType SelectionType { get => SelectionType.Minion; }
    public static event Action<List<MinionController>> OnSelectingMinionForAttack;
    public static event Action<MinionController> OnDied;
    public static event Action<MinionController, MinionController> OnCollided;
    public static event Action<MinionController, int> OnTookDamage;


    private void OnEnable()
    {
        GameManager.OnTurnEnd += OnTurnSwitch;
        // React to the normal selection highlight turning on/off so the sword indicator can yield to it
        // (they must never light at once) and return when the selection clears.
        if (selectable != null) selectable.SelectableChanged += OnSelectableChanged;
    }

    private void OnDisable()
    {
        GameManager.OnTurnEnd -= OnTurnSwitch;
        if (selectable != null) selectable.SelectableChanged -= OnSelectableChanged;
        _skullTween?.Kill();
        _skullTween = null;
        _statChangeTween?.Kill();
        _statChangeTween = null;
    }

    private void OnSelectableChanged(bool isSelectable) => ApplyAttackHighlight();

    protected virtual void Start()
    {
        /*if(showInfo != null) 
            showInfo.card = card;*/
    }
    public void Initialize(Agent owner, bool isPlayerMinion)
    {
        modal.UpdateModal(card, owner, isPlayerMinion);
        view.UpdateView(modal);
        PlayAppearAnimation();
    }

    protected virtual void PlayAppearAnimation()
    {
        view.PlayAppearAnimation();
    }
    private void OnTurnSwitch(GameState curState)
    {
        age++;
        isAttackedThisTurn = false;
        isMovementValidated = false;

        // Clear the attack indicator on every turn end; it is re-lit for the player's minions at the
        // start of their turn via SetReadyToAttack.
        _attackReady = false;
        if (attackHighlight != null)
            attackHighlight.enabled = false;

    }

    protected virtual void OnMouseDown()
    {
        if (GameManager.Instance.currentState == GameState.EndGame)
            return;

        // Clicking the attacker again backs out of its own in-progress attack selection.
        if (SelectionManager.Instance.ActiveAttacker == this)
        {
            SelectionManager.Instance.Cancel();
            return;
        }

        // An active selection (spell-target or attack-target) consumes the click; only when nothing
        // is being selected does a click on an attack-ready minion begin a fresh attack.
        if (SelectionManager.Instance.TryResolveClick(this))
            return;

        // Don't let a click start an attack while a card is resolving.
        if (canAttack && !GameManager.Instance.isPlayingCard)
        {
            StartAttack(GameManager.Instance.opponent);
        }
    }
    protected virtual void OnMouseEnter()
    {
        if (GameManager.Instance.currentState == GameState.EndGame)
            return;

        GameManager.Instance.player.handManager.ShowInfoCard(card);

        // A card being dragged owns the hover; don't react unless a selection request is active.
        if (DraggableItem.AnyCardDragging && !SelectionManager.Instance.HasActiveMinionRequest)
            return;

        // What hovering this minion means right now: an active selection request dictates it (attack pick,
        // push pick, generic pick); otherwise we're just inspecting, so show the range.
        HoverIntent intent = SelectionManager.Instance.HasActiveMinionRequest
            ? SelectionManager.Instance.ActiveIntent
            : HoverIntent.SeeRange;

        ApplyHoverIntent(intent);

        // Independently of the intent-specific feedback above, light the skull if picking this minion
        // right now would kill it (an attack strike, its retaliation, or a lethal damage spell). Guarded
        // by HasActiveMinionRequest so it never shows while merely inspecting range.
        if (SelectionManager.Instance.HasActiveMinionRequest)
            ShowDeathPreview();
    }

    // Central dispatch for hover feedback. Only the SeeRange and ToPush cases do something today; the
    // others are wired here so their behavior can be filled in later without touching OnMouseEnter.
    protected virtual void ApplyHoverIntent(HoverIntent intent)
    {
        switch (intent)
        {
            case HoverIntent.SeeRange:
                MinionRangeHandler.Instance.ShowRange(gridEntity.GetGridIndex(), modal.range);
                break;
            case HoverIntent.ToPush:
                ShowPushArrow();
                break;
            case HoverIntent.ToAttack:
                // TODO: show weapon image
                break;
            case HoverIntent.ToSelectGenerally:
                // TODO: generic select feedback
                break;
        }
    }

    // The attacker whose retaliation skull we lit while this minion was hovered, so OnMouseExit can hide
    // it too (only the hovered target itself gets an OnMouseExit).
    private MinionController _previewedAttacker;

    // While this minion is hovered as a valid pick, light the skull on whatever the pick would kill. Two
    // sources: an attack (this minion takes the attacker's strike, and the attacker takes the retaliation
    // back if it is in this minion's range — so its skull can light from taking damage too), or a lethal
    // damage spell targeting this minion. Uses TakeDamage's armor math (damage after armor >= health means
    // dead). No health text is touched; the skull is the only feedback.
    private void ShowDeathPreview()
    {
        var selection = SelectionManager.Instance;
        if (!selection.IsActiveTarget(this))
        {
            HideDeathPreview();
            return;
        }

        MinionController attacker = selection.ActiveAttacker;
        if (attacker != null)
        {
            int dmgToTarget = Mathf.Max(attacker.modal.attack - modal.armor, 0);
            bool targetDies = dmgToTarget >= modal.health;
            SetSkull(targetDies);
            // Non-lethal hit: preview the health the target will lose (the skull owns the lethal case).
            SetHealthChangePreview(targetDies ? 0 : -dmgToTarget);

            // Retaliation: the attacker takes this minion's strike back only if it is in this minion's range
            // (mirrors Attack()'s RangeUtility.IsInRange(target, attacker) check).
            if (RangeUtility.IsInRange(this, attacker))
            {
                int dmgToAttacker = Mathf.Max(modal.attack - attacker.modal.armor, 0);
                bool attackerDies = dmgToAttacker >= attacker.modal.health;
                attacker.SetSkull(attackerDies);
                attacker.SetHealthChangePreview(attackerDies ? 0 : -dmgToAttacker);
                _previewedAttacker = attacker;
            }
            return;
        }

        // Spell/card pick: a lethal card lights the skull; a non-lethal stat change shows the delta label.
        PreviewCardEffect(selection.ActiveCard);
    }

    // Death-preview entry points for area (cell-selection) spells, which preview every minion sitting
    // under the spell's hovered area rather than a single directly-hovered target. GridCellSelectionManager
    // calls these for each occupant of the previewed cells.
    public void ShowCardDeathPreview(CardSO sourceCard)
    {
        PreviewCardEffect(sourceCard);
    }

    public void HideCardDeathPreview()
    {
        SetSkull(false);
        HideStatChange();
    }

    // Card-hover feedback dispatch shared by single-target (ShowDeathPreview) and area (ShowCardDeathPreview)
    // previews: a card that would kill this minion lights the skull; otherwise, if it would change this
    // minion's stats, the delta label shows instead. The two are mutually exclusive.
    private void PreviewCardEffect(CardSO card)
    {
        bool lethal = card != null && WouldCardKill(card);
        SetSkull(lethal);
        SetStatChange(lethal ? null : card);
    }

    // Compute the net attack/health change this card would apply to this minion (armor mitigates a health
    // debuff, matching TakeDamage), then show the breathing label if anything actually changes. A null
    // card, or one that changes nothing, hides it.
    private void SetStatChange(CardSO card)
    {
        if (card == null) { HideStatChange(); return; }

        CardEffectInspector.StatDelta delta = CardEffectInspector.GetTargetedStatChange(card);
        int dA = delta.attack;
        int dH = delta.health;
        if (dH < 0) dH = -Mathf.Max(-dH - modal.armor, 0); // a health debuff is reduced by armor, like damage

        if (dA == 0 && dH == 0) { HideStatChange(); return; }

        ShowStatChange(dA, dH);
    }

    // Preview a pure health change on this minion (used by the attack hover: the damage a strike or its
    // retaliation would deal, passed as a negative delta). Zero hides the labels — e.g. a lethal hit, where
    // the skull owns the feedback instead.
    private void SetHealthChangePreview(int healthDelta)
    {
        if (healthDelta == 0) { HideStatChange(); return; }
        ShowStatChange(0, healthDelta);
    }

    // Fill each stat's label above its number (an unchanged stat's label stays hidden), then breathe the
    // labels' background images in sync for as long as the preview is up (mirrors the skull); the number
    // text stays solid. Resets alpha to full first so re-shows never inherit a mid-fade value.
    private void ShowStatChange(int dA, int dH)
    {
        ApplyStatDelta(statChangeAttackText, statChangeAttackGroup, dA);
        ApplyStatDelta(statChangeHealthText, statChangeHealthGroup, dH);
        ResolveStatChangeBgs();

        _statChangeTween?.Kill();
        SetStatChangeAlpha(1f);
        _statChangeTween = DOTween
            .To(() => 1f, SetStatChangeAlpha, statChangeBreathMinAlpha, statChangeBreathDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    // Show one stat's delta ("+2" / "-1"), or hide it when unchanged. The number is tinted by direction
    // (green buff / red debuff); the background image keeps its authored color. The background container is
    // what toggles (its CanvasGroup if wired, else the text's parent), so the label's background shows/hides
    // together with the number; the text just carries the value.
    private void ApplyStatDelta(TMP_Text text, CanvasGroup group, int delta)
    {
        GameObject container = ContainerOf(text, group);
        if (container == null) return;

        if (delta == 0) { container.SetActive(false); return; }

        if (text != null)
        {
            text.color = delta > 0 ? statBuffColor : statDebuffColor;
            text.text = delta.ToString("+0;-0");
        }

        container.SetActive(true);
    }

    // The object we show/hide (and breathe) for a label: its background container — the wired CanvasGroup,
    // else the text's parent — falling back to the text itself if it has no parent.
    private static GameObject ContainerOf(TMP_Text text, CanvasGroup group)
    {
        if (group != null) return group.gameObject;
        if (text == null) return null;
        return text.transform.parent != null ? text.transform.parent.gameObject : text.gameObject;
    }

    // Breathe only the labels' background images, fading each between its authored alpha (a == 1) and
    // statChangeBreathMinAlpha of it (mirrors the skull/move-arrow scaling). The number text is untouched.
    private void SetStatChangeAlpha(float a)
    {
        FadeBg(_statChangeAttackBg, _statChangeAttackBgBase, a);
        FadeBg(_statChangeHealthBg, _statChangeHealthBgBase, a);
    }

    private static void FadeBg(Graphic bg, Color baseColor, float a)
    {
        if (bg == null) return;
        Color c = baseColor;
        c.a = baseColor.a * a;
        bg.color = c;
    }

    // Cache each label's background Graphic (the container's Image) and its authored color, so breathing
    // fades relative to that base rather than snapping to a flat alpha. Resolved once; containers are fixed.
    private void ResolveStatChangeBgs()
    {
        if (_statChangeBgResolved) return;

        _statChangeAttackBg = BgOf(statChangeAttackText, statChangeAttackGroup);
        if (_statChangeAttackBg != null) _statChangeAttackBgBase = _statChangeAttackBg.color;

        _statChangeHealthBg = BgOf(statChangeHealthText, statChangeHealthGroup);
        if (_statChangeHealthBg != null) _statChangeHealthBgBase = _statChangeHealthBg.color;

        _statChangeBgResolved = true;
    }

    private static Graphic BgOf(TMP_Text text, CanvasGroup group)
    {
        GameObject container = ContainerOf(text, group);
        return container != null ? container.GetComponent<Graphic>() : null;
    }

    private void HideStatChange()
    {
        _statChangeTween?.Kill();
        _statChangeTween = null;

        GameObject atk = ContainerOf(statChangeAttackText, statChangeAttackGroup);
        if (atk != null) atk.SetActive(false);
        GameObject hp = ContainerOf(statChangeHealthText, statChangeHealthGroup);
        if (hp != null) hp.SetActive(false);
    }

    // Show/hide the skull, breathing its alpha up and down for as long as it stays shown (mirrors the move
    // arrow). Enabling resets the alpha to full first so repeated shows never drift dimmer; disabling kills
    // the tween. Idempotent when already in the requested state.
    private void SetSkull(bool willDie)
    {
        if (skull == null) return;

        if (!willDie)
        {
            _skullTween?.Kill();
            _skullTween = null;
            skull.SetActive(false);
            return;
        }

        if (skull.activeSelf && _skullTween != null) return; // already breathing

        ResolveSkullRenderer();
        skull.SetActive(true);
        if (_skullRenderer == null) return;

        _skullRenderer.color = _skullBaseColor; // reset to full so re-shows don't inherit a mid-fade alpha
        _skullTween?.Kill();
        _skullTween = _skullRenderer
            .DOFade(skullBreathMinAlpha * _skullBaseColor.a, skullBreathDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void ResolveSkullRenderer()
    {
        if (_skullResolved) return;
        _skullResolved = true;
        _skullRenderer = skull.GetComponent<SpriteRenderer>();
        if (_skullRenderer != null) _skullBaseColor = _skullRenderer.color;
    }

    // True if playing sourceCard on this minion would kill it. A destroy/banish removes it outright;
    // otherwise the card's OnPlay damage (read from its actual serialized calls, not the AI hints, which
    // are often unset) is applied through this minion's armor to match TakeDamage.
    private bool WouldCardKill(CardSO sourceCard)
    {
        if (sourceCard.aiRemovesTarget) return true;

        int damage = CardEffectInspector.GetTargetedDamage(sourceCard);
        if (damage <= 0) return false;

        int effectiveDamage = Mathf.Max(damage - modal.armor, 0);
        return effectiveDamage >= modal.health;
    }

    private void HideDeathPreview()
    {
        SetSkull(false);
        HideStatChange();

        if (_previewedAttacker != null)
        {
            _previewedAttacker.SetSkull(false);
            _previewedAttacker.HideStatChange();
            _previewedAttacker = null;
        }
    }

    protected void OnMouseExit()
    {
        GameManager.Instance.player.handManager.HideInfoCard();
        MinionRangeHandler.Instance.HideRange();
        HideMoveArrow();
        HideDeathPreview();
    }

    private void OnDrawGizmosSelected()
    {
        int range = modal != null ? modal.range : (card != null ? card.range : 0);
        if (range <= 0) return;

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        Vector3 size = new Vector3(0.9f, 0.9f, 0.1f);
        foreach (var off in RangeUtility.RangeOffsets(range))
        {
            // grid offset (dx,dy) -> world offset (dx,-dy); cellSize is 1
            Vector3 cell = transform.position + new Vector3(off.x, -off.y, 0f);
            DrawThickWireCube(cell, size, 4f);
        }
#endif
    }

#if UNITY_EDITOR
    private static void DrawThickWireCube(Vector3 center, Vector3 size, float thickness)
    {
        Vector3 e = size * 0.5f;
        Vector3 a = center + new Vector3(-e.x, -e.y, -e.z);
        Vector3 b = center + new Vector3( e.x, -e.y, -e.z);
        Vector3 c = center + new Vector3( e.x,  e.y, -e.z);
        Vector3 d = center + new Vector3(-e.x,  e.y, -e.z);
        Vector3 a2 = center + new Vector3(-e.x, -e.y,  e.z);
        Vector3 b2 = center + new Vector3( e.x, -e.y,  e.z);
        Vector3 c2 = center + new Vector3( e.x,  e.y,  e.z);
        Vector3 d2 = center + new Vector3(-e.x,  e.y,  e.z);

        UnityEditor.Handles.DrawLine(a, b, thickness);
        UnityEditor.Handles.DrawLine(b, c, thickness);
        UnityEditor.Handles.DrawLine(c, d, thickness);
        UnityEditor.Handles.DrawLine(d, a, thickness);

        UnityEditor.Handles.DrawLine(a2, b2, thickness);
        UnityEditor.Handles.DrawLine(b2, c2, thickness);
        UnityEditor.Handles.DrawLine(c2, d2, thickness);
        UnityEditor.Handles.DrawLine(d2, a2, thickness);

        UnityEditor.Handles.DrawLine(a, a2, thickness);
        UnityEditor.Handles.DrawLine(b, b2, thickness);
        UnityEditor.Handles.DrawLine(c, c2, thickness);
        UnityEditor.Handles.DrawLine(d, d2, thickness);
    }
#endif

    public virtual void SetReadyToAttack()
    {
        // Eligibility: does this minion still have its attack this turn? This is independent of whether
        // a target is currently in range.
        bool eligible = modal.canAttack && modal.canAttackManually && !isAttackedThisTurn && age >= 1;

        if (!eligible)
        {
            canAttack = false;
        }
        else{
            List<MinionController> targets = new List<MinionController>();
            targets.AddRange(GameManager.Instance.opponent.minions);
            targets.Add(GameManager.Instance.opponent.hero);
            bool targetExists = false;

            foreach (var minion in targets)
            {
                if (RangeUtility.IsInRange(this, minion))
                {
                    targetExists = true;
                    break;
                }
            }

            // canAttack (selectable/clickable) still requires a target to be in range.
            canAttack = targetExists;

        }

        // The attack highlight reflects eligibility, not range: it stays lit whenever the minion can
        // still attack this turn, even if there is no enemy in range to hit yet.
        _attackReady = eligible;
        ApplyAttackHighlight();

        // An attack-ready minion shows the attack highlight (above), NOT the normal selection highlight,
        // yet must stay clickable so the player can start its attack. The normal highlight is reserved
        // for other selection flows (spell / push / attack-target picking driven by SelectionManager).
        //showInfo.gameObject.SetActive(!canAttack);
        if (canAttack)
            selectable.SetClickableWithoutHighlight();
        else
            selectable.SetSelectable(false);

    }
    // Force the attack indicator off for a transient overlay (attack-target selection, move-preview
    // arrow) without clearing _attackReady, so RefreshAttackHighlight() / SetReadyToAttack can restore it.
    public void HideAttackHighlight()
    {
        if (attackHighlight != null)
            attackHighlight.enabled = false;
    }

    // Restore the attack indicator to its resting state after an overlay that hid it goes away.
    public void RefreshAttackHighlight()
    {
        ApplyAttackHighlight();
    }

    // The sword indicator and the normal selection highlight are mutually exclusive: whenever the minion
    // is lit as a selection target (a spell / attack-target pick driven by SelectionManager), the sword
    // yields, and it returns once that highlight clears. So it shows only when the minion rests
    // attack-ready AND is not currently highlighted as a selectable target.
    private void ApplyAttackHighlight()
    {
        if (attackHighlight != null)
            attackHighlight.enabled = _attackReady && (selectable == null || !selectable.IsSelectable);
    }

    public virtual bool CanAttack(Agent opponent)
    {
        if (!modal.canAttack || !modal.canAttackManually || isAttackedThisTurn || age == 0) return false;

        //var grid = GridManager.Instance.GetGrid();
        List<MinionController> targets = new List<MinionController>();
        targets.AddRange(opponent.minions);
        targets.Add(opponent.hero);
        foreach (var minion in targets)
        {
            if(RangeUtility.IsInRange(this, minion))
            {
                return true;
            }
        }
        return false;
    }

    public void StartAttack(Agent opponent, MinionController target = null)
    {
        StartCoroutine(Attack(opponent, target));
    }

    public virtual IEnumerator Attack(Agent opponent, MinionController target = null)
    {
        Debug.Log("starting Attack");

        // Cards flagged canAttack = false can never attack (manual selection or triggered/auto attacks).
        if (!modal.canAttack) yield break;

        List<MinionController> selectableminions = new List<MinionController>();
        List<MinionController> targets = new List<MinionController>();
        targets.AddRange(opponent.minions);
        targets.Add(opponent.hero);
        foreach (var minion in targets)
        {
            if (RangeUtility.IsInRange(this, minion))
            {
                selectableminions.Add(minion);
            }
        }

        Debug.Log("selectiable minion count: " + selectableminions.Count);

        MinionController chosen = null;

        if (target != null)
        {
            // Triggered / AI attack: the target is already decided, so bypass the interactive
            // selection request entirely (no highlights, no wait).
            chosen = target;
        }
        else
        {
            if (selectableminions.Count == 0) yield break; // nothing in range — don't open a dangling selection

            // Interactive attack: the SelectionManager lights the valid targets and routes the click.
            SelectionManager.Instance.BeginAttackRequest(this, selectableminions, picked => chosen = picked);
            OnSelectingMinionForAttack?.Invoke(selectableminions);

            while (chosen == null && SelectionManager.Instance.HasActiveMinionRequest && !GameManager.Instance.isTesting)
            {
                yield return null;
            }

            if (chosen == null)
            {
                // Cancelled (right-click / attacker re-click / spell preempt / turn end).
                SelectionManager.Instance.Cancel();
                yield break;
            }
        }

        Debug.Log("damaging minion");

        Vector3 dir = (chosen.transform.position - transform.position).normalized;
        // Lunge plays 20% faster (duration / 1.2).
        transform.DOPunchPosition(dir*0.2f, 0.5f / 1.2f, vibrato: 1).SetEase(Ease.InOutBack).SetDelay(0.5f);
        chosen.TakeDamage(modal.attack);
        chosen.transform.DOPunchPosition(dir * 0.03f, 0.15f, vibrato: 5).SetDelay(0.75f);
        if (RangeUtility.IsInRange(chosen, this)) // target retaliates if attacker is in ITS range
        {
            TakeDamage(chosen.modal.attack);
            // Counter-attack is delayed an extra 0.25s so it reads as a response to the strike
            // rather than overlapping it.
            if (chosen.modal.range < 2)
            {
                StartCoroutine(chosen.animationController.PlaySlashAnimation(-dir, 0.75f));
            }
            else
            {
                StartCoroutine(chosen.animationController.PlayArrowAnimation(-dir, transform.position, 0.9f, animationController.PlayArrowHitAnimation));
            }
        }

        if(modal.range < 2)
        {
            StartCoroutine(animationController.PlaySlashAnimation(dir, 0.5f));
        }
        else
        {
            StartCoroutine(animationController.PlayArrowAnimation(dir, chosen.transform.position, 0.65f, chosen.animationController.PlayArrowHitAnimation));
        }
        LastTarget = chosen;
        isAttackedThisTurn = true;
        canAttack = false;

        // Tear down the selection (de-highlights every lit target) now that the attack resolved, then
        // restore resting attack-readiness for the remaining minions. State is set above first so this
        // minion is correctly recomputed as no-longer-attackable.
        SelectionManager.Instance.Complete();
        GameManager.Instance.SetPlayerMinionsReadyToAttack();

        yield break;
    }
    public bool TakeDamage(int damage)
    {
        int effectiveDamage = Mathf.Max(damage - modal.armor, 0);
        modal.health -= effectiveDamage;

        DOVirtual.DelayedCall(0.75f, () => view.UpdateView(modal));
        Debug.Log("minion take damage: " + modal.name);

        if (modal.health <= 0)
        {
            // Lethal hit: go straight to death and deliberately do NOT fire OnTookDamage. The minion is
            // dying, so its OnDeath trigger supersedes. Firing OnTookDamage first would wrap a took-damage
            // triggered-action scope around the death: the death gets deferred behind it, then flushed from
            // inside the took-damage scope's finally, and when that outer scope disposes its Restore()
            // clobbers the death trigger's freshly-set ActionHolder selection state with stale leftovers —
            // which made OnDeath effects (e.g. the seed's +1/+1) intermittently no-op.
            Debug.Log("minion died: " + modal.name);
            Die();
            return true;
        }

        if (effectiveDamage > 0)
        {
            Debug.Log("on took damage invoke");
            OnTookDamage?.Invoke(this, effectiveDamage);
        }

        return false;
    }

    protected virtual void Die()
    {
        OnDied?.Invoke(this);
        selectable.SetSelectable(false);
        gridEntity.RemoveFromGridCell();
        if (GameManager.Instance.isPlayerTurn)
        {
            GameManager.Instance.player.minions.Remove(this);
        }
        else
        {
            GameManager.Instance.opponent.minions.Remove(this);
        }
        Invoke("PlayDeathAnimation", 1f);

        GridManager.Instance.GetCell(transform.position).cellObj.GetComponent<CellController>().AddToMinionsDiedHere(card, modal.isPlayerMinion);
    }

    protected virtual void PlayDeathAnimation()
    {
        //view.FadeOutArtImage(1.35f);
        animator.enabled = true;
        animator.speed = 3.34f;
        if((modal.range == 1  || modal.range == 1) && modal.defHealth < 5)
        {
            animator.Play("MeleeBronzeDeath");
        }
        else if ((modal.range == 1 || modal.range == 1) && modal.defHealth < 10)
        {
            animator.Play("MeleeSilverDeath");
        }
        else if ((modal.range == 1 || modal.range == 1) && modal.defHealth < 15)
        {
            animator.Play("MeleeGoldDeath");
        }
        else if (modal.range == 2 && modal.defHealth < 5)
        {
            animator.Play("RangedBronzeDeath");
        }
        else if (modal.range == 2 && modal.defHealth < 10)
        {
            animator.Play("RangedSilverDeath");
        }
        else if (modal.range == 2 && modal.defHealth < 15)
        {
            animator.Play("RangedGoldDeath");
        }
    }
    public void DestroySelf()
    {
        Destroy(gameObject);
    }
    public virtual void Move(Vector3Int pos)
    {
        gridEntity.WorldPos = pos;
        // Recompute attack-readiness once the move finishes: a changed position changes which targets
        // are in range (both for this minion and for any minion that could reach it). We hook OnComplete
        // because RangeUtility reads transform.position, which only reaches `pos` when the tween ends.
        transform.DOMove(pos, 0.25f).OnComplete(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.SetPlayerMinionsReadyToAttack();
        });
        plannedMoveDir = Vector3Int.zero;
        isMovementValidated = false;

        GridManager.Instance.InvokeGridChanged();
    }

    // True if the cell one step along `pushDir` is in-grid and empty. `pushDir` is the summoning
    // agent's forward direction (up for the player, down for the opponent), so any occupant — friendly
    // or enemy — can be pushed out of the way as long as the cell ahead of it is free.
    // Used by spawn-on-occupied-cell logic: a start cell is only valid if its occupant can be pushed.
    public bool CanBePushedForward(Vector3Int pushDir)
    {
        Vector3Int target = Vector3Int.RoundToInt(transform.position) + pushDir;
        Vector2Int idx = GridManager.Instance.PosToGridIndex(target);
        if (GridManager.Instance.IsOutSideOfGrid(idx)) return false;
        return GridManager.Instance.GetCell(idx).obj == null;
    }

    // Move this minion one cell along `pushDir`, bypassing age/canMove gating (Move() does no checks).
    public void PushForward(Vector3Int pushDir)
    {
        Vector3Int target = Vector3Int.RoundToInt(transform.position) + pushDir;
        Move(target);
    }

    // Swap world positions with another minion in one grid refresh so neither slot is briefly vacant.
    public void SwapPositionsWith(MinionController other)
    {
        Vector3 posA = gridEntity.WorldPos;
        Vector3 posB = other.gridEntity.WorldPos;

        gridEntity.WorldPos = posB;
        other.gridEntity.WorldPos = posA;

        transform.DOMove(posB, 0.25f).OnComplete(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.SetPlayerMinionsReadyToAttack();
        });
        other.transform.DOMove(posA, 0.25f);

        plannedMoveDir = Vector3Int.zero;
        other.plannedMoveDir = Vector3Int.zero;

        GridManager.Instance.InvokeGridChanged();
    }

    public virtual void FailedMove(Vector3 dir, MinionController  collidedEntity = null)
    {
        Debug.Log("failed move");
        transform.DOPunchPosition(dir/20, 0.25f, vibrato: 0).SetEase(Ease.OutCubic);
        plannedMoveDir = Vector3Int.zero;
        isMovementValidated = false;

        if (collidedEntity != null) {
            OnCollided?.Invoke(this, collidedEntity);
        }
    }
    // Movement-preview arrow shown while the player hovers the turn-switch button. The arrow points in
    // this minion's forward direction (up for the player, down for the opponent) and is enabled only when
    // the minion would actually try to advance next turn: white when the cell ahead is free (it will
    // move or will follow the column forward), yellow only when the cell ahead stays blocked. It stays
    // hidden when the minion can't move at all — can't-move flag, summoned this turn (age < 1), or facing
    // the board edge. Collision is resolved via CellAheadClears, which walks the column so a minion tucked
    // behind an ally that is itself advancing shows white (it follows into the vacated cell), and only a
    // real blocker (edge, wall, enemy, or a stuck ally) shows yellow.
    public void ShowMoveArrow()
    {
        if (moveArrow == null) return;

        if (!modal.canMove || age < 1)
        {
            HideMoveArrow();
            return;
        }

        Vector3Int dir = modal.isPlayerMinion ? Vector3Int.up : Vector3Int.down;
        Vector3Int dest = Vector3Int.RoundToInt(transform.position) + dir;
        Vector2Int idx = GridManager.Instance.PosToGridIndex(dest);

        if (GridManager.Instance.IsOutSideOfGrid(idx))
        {
            HideMoveArrow();
            return;
        }

        bool willCollide = !CellAheadClears(dir);
        EnableMoveArrow(willCollide);
    }

    // Whether the cell one step along `dir` will be empty by the time this minion resolves its move
    // at turn end. The turn-end movement loops resolve minions front-first and each Move updates the
    // grid immediately, so an occupied cell isn't necessarily a collision: if the blocker is a
    // same-side minion that will itself advance, this minion follows it into the vacated cell (the
    // whole column shifts in one step). Only a permanent blocker keeps the cell filled — the board
    // edge, a wall/non-minion, an enemy (which doesn't move on this side's phase), or an ally that is
    // itself blocked. Walks the column recursively, matching the resolution order exactly.
    private bool CellAheadClears(Vector3Int dir)
    {
        Vector3Int dest = Vector3Int.RoundToInt(transform.position) + dir;
        Vector2Int idx = GridManager.Instance.PosToGridIndex(dest);

        if (GridManager.Instance.IsOutSideOfGrid(idx)) return false;

        GameObject occupant = GridManager.Instance.GetCell(idx).obj;
        if (occupant == null) return true;

        if (!occupant.TryGetComponent(out MinionController ahead)) return false;
        if (ahead.modal.isPlayerMinion != modal.isPlayerMinion) return false;
        if (!ahead.modal.canMove || ahead.age < 1) return false;

        return ahead.CellAheadClears(dir);
    }

    // Push preview shown while hovering a push card's candidate: the arrow always shows (the intent IS to
    // push this minion), pointing in the summoner's forward direction. White when the cell ahead is free
    // (the push lands), yellow when it's blocked by the board edge or an occupant (the push will fail /
    // collide). Ignores canMove/age because a push bypasses them. Pushes only happen on the player's turn,
    // so the direction is always up and the arrow needs no rotation.
    public void ShowPushArrow()
    {
        if (moveArrow == null) return;
        bool willCollide = !CanBePushedForward(ActionHolder.SummonerPushDir());
        EnableMoveArrow(willCollide);
    }

    // Enable the arrow in white/yellow for as long as it stays shown.
    private void EnableMoveArrow(bool willCollide)
    {
        if (moveArrow == null) return;

        Color c = willCollide ? moveArrowCollideColor : moveArrowColor;
        moveArrow.color = c;
        moveArrow.gameObject.SetActive(true);

        // The move arrow and the attack sword share the minion's face — while the arrow shows, hide the
        // sword. It's restored from _attackReady in HideMoveArrow when the arrow goes away.
        HideAttackHighlight();
    }

    public void HideMoveArrow()
    {
        if (moveArrow != null) moveArrow.gameObject.SetActive(false);

        // Bring the attack indicator back to its resting state now that the arrow is gone.
        RefreshAttackHighlight();
    }

    public virtual MoveInfo CanMove(Vector3Int pos)
    {
        var moveInfo = new MoveInfo();

        moveInfo.CanMove = false;
        moveInfo.CollidedEntity = null;

        if(!modal.canMove) return moveInfo;

        if(age < 1)
        {
            Debug.Log("cant move because age: " + age);
            return moveInfo;
        }

        if (GridManager.Instance.IsOutSideOfGrid(GridManager.Instance.PosToGridIndex(pos)))
        {
            Debug.Log("-2: " + GridManager.Instance.PosToGridIndex(pos));

            plannedMoveDir = Vector3Int.zero;
            return moveInfo;
        }

        GameObject objectAtDest = GridManager.Instance.GetCell(GridManager.Instance.PosToGridIndex(pos)).obj;

        if (objectAtDest != null) {
            // The minion wanted to advance (it passed the can-move / age / edge gates) but the cell
            // ahead is taken — a real failed move. Blocked distinguishes this from the earlier returns,
            // where the minion simply isn't trying to move. CollidedEntity is the blocker when it's a
            // minion, null when it's a wall (still blocked).
            moveInfo.Blocked = true;
            moveInfo.CollidedEntity = objectAtDest.GetComponent<MinionController>();
            return moveInfo;
        }

        moveInfo.CanMove= true;
        return moveInfo;
    }

    public struct MoveInfo
    {
        public bool CanMove;
        // True when the minion tried to advance but was blocked by an occupant (minion or wall), as
        // opposed to not moving at all (can't-move flag, summoned this turn, or facing the board edge).
        public bool Blocked;
        public MinionController CollidedEntity;
    }
}
