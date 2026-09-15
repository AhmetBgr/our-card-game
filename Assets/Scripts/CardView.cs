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

    /// <summary>The frame Image, exposed so components (e.g. the metallic sheen driver) can reach it without a duplicate serialized reference.</summary>
    public Image FrameImage => frame;

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
    [Tooltip("Labels that name the card's attack/health/cost. Shown only during the tutorial match, while this card is hovered in hand.")]
    [SerializeField] private GameObject tutorialHints;
    [Tooltip("The attack and health labels inside Tutorial Hints. Hidden on spells, which show neither stat.")]
    [SerializeField] private GameObject tutorialAttackHint;
    [SerializeField] private GameObject tutorialHealthHint;

    [SerializeField] private TextMeshProUGUI nametext;
    [SerializeField] private TextMeshProUGUI desctext;
    [SerializeField] private TextMeshProUGUI attacktext;
    [SerializeField] private TextMeshProUGUI healthtext;
    [SerializeField] private TextMeshProUGUI costText;
    [Tooltip("Names the card's type on its face: \"Minion\" or \"Spell\".")]
    [SerializeField] private TextMeshProUGUI cardTypeText;
    [SerializeField] private Transform costTransform;

    [Header("Forge sparks")]
    [Tooltip("Thrown off every punch of the forge morph, at the thing being struck. A UIParticle burst " +
             "(see UIForgeSpark), not a world particle system -- the card is UI, and a plain " +
             "ParticleSystem would draw behind the canvas. Leave empty for a morph with no sparks.")]
    [SerializeField] private GameObject forgeSparkPrefab;

    [Tooltip("Burst size for the small blows -- the three stat numbers and the description -- as a " +
             "MULTIPLIER of the size authored on the prefab, so retuning the burst there still scales " +
             "both sizes with it. 1 is the prefab's own size.")]
    [SerializeField] private float forgeSparkElementScale = 1f;

    [Tooltip("The same multiplier for the card-face blow. Bigger, because it is the beat where the card " +
             "stops being one card and starts being the other.")]
    [SerializeField] private float forgeSparkCardScale = 2.2f;

    [Tooltip("Seconds before a spent burst is cleaned up. Must outlast the particles' own lifetime, or " +
             "they are destroyed mid-flight.")]
    [SerializeField] private float forgeSparkCleanupDelay = 1.5f;

    [Header("Stat change punch")]
    [Tooltip("What the stat-change punch scales. Leave empty to use the card face (the parent of the frame image), which is the whole drawn card. Not the root: CardHandLayout lerps every hand card's root scale back to 1 every frame and would flatten the punch.")]
    [SerializeField] private Transform statPunchTarget;
    [Tooltip("Wait before the punch plays, so it lands with the effect that caused the stat change instead of ahead of it. The numbers themselves still update immediately.")]
    [SerializeField] private float statPunchDelay = 0.5f;
    [Tooltip("Scale punch played on the card when one of its stats changes while the player can see it (an in-hand buff).")]
    [SerializeField] private float statPunchScale = 0.15f;
    [SerializeField] private float statPunchDuration = 0.35f;
    [SerializeField] private int statPunchVibrato = 0;

    private Tween gearRotateTween;

    // Last stat line drawn, and which card it belonged to, so a change can be told apart from this
    // view simply being pointed at a different card. Tracked whether or not the card is visible, so a
    // buff landing on a face-down card is absorbed silently instead of punching later on the reveal.
    private CardModal _statsSource;
    private string _statsCardName;
    private int _lastAttack;
    private int _lastHealth;
    private int _lastCost;
    private bool _hasStats;

    private Tween statPunchTween;

    // Set while the forge morph is driving the card, so the automatic punch above stays out of the way
    // of the punches that morph is already playing. See UpdateViewWithoutStatPunch.
    private bool _suppressStatPunch;

    // Scale the punch started from, so it can be restored exactly when a second change interrupts it
    // mid-swing instead of compounding off a half-punched size.
    private Vector3 statPunchBaseScale = Vector3.one;

    private bool IsStatPunching => statPunchTween != null && statPunchTween.IsActive() && statPunchTween.IsPlaying();

    /// <summary>
    /// The card as drawn: everything on the face hangs off the frame image's parent, so scaling it
    /// scales the whole card. Resolved rather than wired, since <see cref="VisualRect"/> already treats
    /// the frame as "the card the player sees". Falls back to the root if a prefab has no frame.
    /// </summary>
    private Transform StatPunchTarget
    {
        get
        {
            if (statPunchTarget != null) return statPunchTarget;
            if (frame != null && frame.transform.parent != null) return frame.transform.parent;
            return transform;
        }
    }

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
        statPunchTween?.Kill();
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

        // One definition of "this is a spell", so the type label can never name a type the frame
        // art disagrees with.
        bool isSpell = card.attack == 0 && card.health == 0;

        if (card.isUpgraded)
        {
            frame.sprite = isSpell ? upgradedSpellFrame : upgradedMinionFrame;

        }
        else {
            frame.sprite = isSpell ? spellFrame : minionFrame;

        }

        if (cardTypeText != null) cardTypeText.text = isSpell ? "Spell" : "Minion";

        minionTypeIconObjects[0].transform.parent.gameObject.SetActive(faceUp);

        // Keyed off health, not attack: a 0-attack minion is still a minion and must show its attack
        // stat. Only spells (attack == 0 && health == 0) hide it, and health == 0 already covers those.
        bool showsStats = card.health > 0 && faceUp;
        attacktext.transform.parent.gameObject.SetActive(showsStats);
        healthtext.transform.parent.gameObject.SetActive(showsStats);

        // The tutorial labels annotate those same two boxes, so they follow them exactly rather than
        // re-deriving the condition — a label pointing at a stat the card doesn't draw is worse than
        // no label. Set here rather than in SetTutorialHints because these are children of that root:
        // their state survives the root being toggled on and off by hover.
        if (tutorialAttackHint != null) tutorialAttackHint.SetActive(showsStats);
        if (tutorialHealthHint != null) tutorialHealthHint.SetActive(showsStats);

        // Last, so the punch only fires once the pass that changed the numbers has finished drawing them.
        ApplyStatChangePunch(card, faceUp);
    }

    /// <summary>
    /// <see cref="UpdateView"/> without the automatic stat punch, for a caller that is already
    /// animating the change itself. The forge morph is the case this exists for: it punches each part
    /// of the card in turn and then reconciles through here, and the built-in punch would fire a
    /// fourth time on top of the three it just played -- but only for an upgrade that keeps the card's
    /// NAME, since <see cref="ApplyStatChangePunch"/> treats a rename as a different card entirely.
    /// That makes it a bug that shows up on some cards and not others, which is the worst kind.
    ///
    /// The baseline bookkeeping is still updated, so the next ordinary UpdateView compares against
    /// what is actually on screen. Mirrors MinionView.UpdateViewWithoutStatFlash.
    /// </summary>
    public void UpdateViewWithoutStatPunch(CardModal card)
    {
        _suppressStatPunch = true;
        try { UpdateView(card); }
        finally { _suppressStatPunch = false; }
    }

    /// <summary>
    /// Punch the whole card whenever one of its stats changes — a card sitting in hand getting buffed
    /// or discounted (ActionHolder's ChangeCardAttack / ChangeCardHealth / ChangeMinionsCost) is easy
    /// to miss otherwise, since nothing about the card moves.
    /// </summary>
    private void ApplyStatChangePunch(CardModal card, bool faceUp)
    {
        // A view showing a different card is showing a new statline, not a changed one. The deck panel
        // and the info card reuse a single object for every card the pointer touches, so identity is
        // checked by modal AND card name — those objects keep the same CardModal and only its contents
        // get rewritten.
        bool sameCard = _hasStats && _statsSource == card && _statsCardName == card.name;
        bool statsChanged = card.attack != _lastAttack || card.health != _lastHealth || card.cost != _lastCost;

        // One punch per change, not one per stat: a buff that moves attack and health together is a
        // single event and should read as a single hit.
        if (sameCard && faceUp && statsChanged && !_suppressStatPunch)
            PlayStatPunch();

        _statsSource = card;
        _statsCardName = card.name;
        _lastAttack = card.attack;
        _lastHealth = card.health;
        _lastCost = card.cost;
        _hasStats = true;
    }

    private void PlayStatPunch()
    {
        if (!gameObject.activeInHierarchy) return;

        Transform target = StatPunchTarget;

        // A punch already in flight is rewound to the scale it started from before the new one begins,
        // so back-to-back buffs each punch from the authored size instead of compounding off a
        // half-punched one.
        if (IsStatPunching)
        {
            statPunchTween.Kill();
            target.localScale = statPunchBaseScale;
        }
        else
        {
            statPunchBaseScale = target.localScale;
        }

        statPunchTween = target.DOPunchScale(statPunchBaseScale * statPunchScale, statPunchDuration,
            vibrato: statPunchVibrato, elasticity: 0f).SetDelay(statPunchDelay);
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
    /// One blow of the forge morph landing, numbered from 1 in the order they are struck: 1 the stats,
    /// 2 the card's own identity, 3 the description. Raised at the moment of impact -- the same instant
    /// the values swap -- so a sound cued to it lands with the punch rather than near it.
    ///
    /// A static event off the VIEW for the same reason MinionView.DamageShown is one: the view is what
    /// knows when the visual actually happens, and the audio system is not allowed to reach into the
    /// animation to find out. Purely additive; nothing in the game depends on it.
    ///
    /// Only the morph raises this. The plain pop-and-fly has no stages, so it is silent.
    /// </summary>
    public static event Action<int> ForgeStageStruck;

    /// <summary>
    /// The forge: this card visibly TURNS INTO its upgrade, in place, without ever leaving the screen.
    /// Three beats, each swapping what it covers at the moment of impact -- the stats, then the card
    /// itself, then the description -- which is the order the player reads a card in. The first two are
    /// scale punches; the description only sparks (see stage 3 for why).
    ///
    /// Written INTO the caller's sequence rather than playing one of its own, so the lead-in, the morph
    /// and the flight to the deck are a single tween with a single lifetime (see Agent.ForgeCardInPlace).
    /// It splits responsibility the same way <see cref="PlayTurnIntoAnimation"/> above does -- the view
    /// animates its own widgets, the caller decides where the card goes -- but it deliberately does NOT
    /// copy that method's build-and-play shape: nesting a sequence that has already started running is
    /// a DOTween footgun, and defaultAutoPlay is All in this project.
    ///
    /// Nothing here touches the modal. The caller reconciles that once at the end, through
    /// <see cref="UpdateViewWithoutStatPunch"/> -- these writes are the transition, that write is the
    /// truth, and the card back in particular is picked from isUpgraded and can only be set from there.
    /// </summary>
    public void AppendForgeMorph(Sequence seq, CardSO upgraded)
    {
        if (seq == null || upgraded == null) return;

        // A stat punch can be sitting on a half-second delay (CardView.statPunchDelay) from an in-hand
        // buff or discount that landed just before the play. It targets the very node stage 2 punches,
        // and restores a scale captured before any of this started -- so left alone it snaps the card
        // face mid-morph. Nothing about that punch is wanted here: the morph is already showing the
        // change it was going to announce.
        CancelStatPunch();

        // Where the morph starts on the caller's timeline. Read once: the sequence is being built, and
        // every blow below is placed as an absolute offset from here.
        float t0 = seq.Duration(false);

        float punch = PlayArea.ForgedMorphElementPunch;
        float cardPunch = PlayArea.ForgedMorphCardPunch;
        float duration = PlayArea.ForgedMorphPunchDuration;
        float stagger = PlayArea.ForgedMorphStatStagger;
        float gap = PlayArea.ForgedMorphStageGap;

        // Every punch is placed at an absolute time on one sequence rather than appended in order. The
        // stat punches overlap each other by design, and mixing Append with Join to express that means
        // the layout depends on the sequence's running duration as it is built -- which is both hard to
        // read and easy to break by inserting a stage. A cursor says exactly when each blow lands.
        //
        // Appended INTO the caller's sequence rather than returned as one of its own: DOTween's
        // defaultAutoPlay is All, so a sequence built and handed back has already started running by
        // the time the caller nests it, and nesting an advanced sequence is where DOTween misbehaves.
        float t = t0;

        // Stage 1 -- the numbers, struck as one blow with a hair of stagger. Each stat the card actually
        // SHOWS punches, whether or not its value moved: a stat line that changes in part still changes
        // as a line. A stat the card does not show is skipped outright -- a spell has no attack or
        // health box (UpdateView deactivates both when health is 0), and striking one anyway threw
        // sparks off an invisible corner of the card.
        //
        // Collected first so the stagger runs over what is actually being struck: on a spell the cost
        // is the only blow, and it should land on the beat rather than two stagger steps late.
        var statLabels = new List<TextMeshProUGUI>();
        var statValues = new List<string>();

        if (IsShown(attacktext)) { statLabels.Add(attacktext); statValues.Add(upgraded.attack.ToString()); }
        if (IsShown(healthtext)) { statLabels.Add(healthtext); statValues.Add(upgraded.health.ToString()); }
        if (IsShown(costText)) { statLabels.Add(costText); statValues.Add(upgraded.cost.ToString()); }

        for (int i = 0; i < statLabels.Count; i++)
        {
            // Copied per iteration so each callback closes over its own label rather than the last one.
            TextMeshProUGUI label = statLabels[i];
            string value = statValues[i];

            // The stage's sound fires on the first of the staggered blows only: the three numbers are
            // one hit split across 0.1s, and three overlapping copies of the same clip would flam.
            bool isFirst = i == 0;

            InsertPunch(seq, t + stagger * i, label.transform, punch, duration, () =>
            {
                if (label == null) return;
                label.text = value;
                PlayForgeSpark(label.transform, forgeSparkElementScale);
                if (isFirst) ForgeStageStruck?.Invoke(1);
            });
        }

        // Only as long as the blows that actually happened. A card showing nothing here (no stats and
        // no cost, which nothing does today) spends no time on the stage at all.
        if (statLabels.Count > 0) t += stagger * (statLabels.Count - 1) + duration + gap;

        // Stage 2 -- the card itself. Punches the FACE, not the root: the root's scale is the lead-in
        // and the flight to the deck, and punching it would fight them.
        InsertPunch(seq, t, StatPunchTarget, cardPunch, duration, () =>
        {
            ApplyForgedIdentity(upgraded);
            PlayForgeSpark(StatPunchTarget, forgeSparkCardScale);
            ForgeStageStruck?.Invoke(2);
        });

        t += duration + gap;

        // Stage 3 -- the description. Deliberately NOT punched, unlike the blows above: this is a
        // paragraph of body text, not a single glyph, and scaling a text block up and back reads as the
        // wording going soft and snapping back rather than as the card being struck.
        //
        // It still owns the beat and still throws sparks, so the change is marked -- and it still runs
        // when the base description is BLANK, which four of the upgrades in the game are, where the
        // wording appears out of nothing.
        seq.InsertCallback(t + duration * 0.5f, () =>
        {
            if (desctext == null) return;

            desctext.text = CardTextFormatter.Format(upgraded.desc, HighlightConfig);
            PlayForgeSpark(desctext.transform, forgeSparkElementScale);
            ForgeStageStruck?.Invoke(3);
        });

        // Pins the sequence's length to the last blow even if every widget above was null, so whatever
        // the caller appends next starts after the morph rather than on top of it.
        seq.InsertCallback(t + duration, () => { });
    }

    /// <summary>
    /// Drop any stat punch in flight or still on its delay, putting the face back to the scale that
    /// punch started from. Public because the forge morph has to clear the decks before it starts
    /// punching the same transform; the punch itself is fire-and-forget everywhere else.
    /// </summary>
    public void CancelStatPunch()
    {
        if (statPunchTween == null) return;

        bool wasLive = statPunchTween.IsActive();
        statPunchTween.Kill();
        statPunchTween = null;

        // Only restore if the tween had actually started moving the transform -- a punch killed while
        // still on its SetDelay never left the base scale, and writing it back would be a no-op at best
        // and a stale value at worst.
        if (wasLive && StatPunchTarget != null) StatPunchTarget.localScale = statPunchBaseScale;
    }

    /// <summary>
    /// Everything stage 2 swaps: who the card is, rather than what its numbers are. The frame and the
    /// type label are derived exactly as <see cref="UpdateView"/> derives them, off the UPGRADED card,
    /// so a base minion whose upgrade is still a minion keeps its frame family and only gains the gold.
    /// </summary>
    private void ApplyForgedIdentity(CardSO upgraded)
    {
        if (nametext != null) nametext.text = upgraded.cardName;
        if (art != null) art.sprite = upgraded.cardArt;

        bool isSpell = upgraded.attack == 0 && upgraded.health == 0;

        if (frame != null)
        {
            frame.sprite = upgraded.isUpgraded
                ? (isSpell ? upgradedSpellFrame : upgradedMinionFrame)
                : (isSpell ? spellFrame : minionFrame);
        }

        if (cardTypeText != null) cardTypeText.text = isSpell ? "Spell" : "Minion";

        // Range can change on an upgrade (a melee unit given reach), and the icon is part of the card's
        // identity rather than its stat line, so it swaps with the art rather than with the numbers.
        int selectedIcon = Mathf.Clamp(upgraded.range - 1, 0, minionTypeIconObjects.Length - 1);
        for (int i = 0; i < minionTypeIconObjects.Length; i++)
        {
            if (minionTypeIconObjects[i] != null) minionTypeIconObjects[i].SetActive(selectedIcon == i);
        }
    }

    /// <summary>
    /// One spark burst at <paramref name="at"/>, thrown as a punch lands.
    ///
    /// Parented to the card FACE rather than to the struck widget, and positioned by world point: the
    /// widget itself is mid-punch and its scale is moving, and a burst hung off it would be squeezed by
    /// the very animation it is meant to punctuate. The face still carries it along the flight to the
    /// deck, which is right -- the sparks belong to this card, not to the screen.
    ///
    /// A fresh instance per blow rather than one replayed: the three stat punches overlap by design, and
    /// a single system moved between them would drag its live particles across the card.
    /// </summary>
    private void PlayForgeSpark(Transform at, float scale)
    {
        if (forgeSparkPrefab == null || at == null) return;

        Transform parent = StatPunchTarget;
        if (parent == null) return;

        GameObject burst = Instantiate(forgeSparkPrefab, parent);
        burst.transform.position = at.position;

        // Over the card art rather than under it -- the face's other children are the frame and the
        // stats, and a spark behind those is a spark nobody sees.
        burst.transform.SetAsLastSibling();

        var particle = burst.GetComponent<Coffee.UIExtensions.UIParticle>();
        if (particle != null)
        {
            // Size is set through UIParticle, NOT through transform.localScale: autoScaling is on in
            // Transform mode, so UIParticle writes localScale itself every frame (to the canvas factor,
            // about 12 here) and anything written there is gone by the next frame. The multiplier is
            // applied to whatever the prefab authors rather than to 1, so retuning the burst on the
            // asset still scales both sizes with it.
            particle.scale *= scale;
            particle.Play();
        }

        Destroy(burst, forgeSparkCleanupDelay);
    }

    /// <summary>
    /// Is this label actually on the card right now? Checks the whole chain, not just the label: the
    /// attack and health numbers are hidden by deactivating their PARENT box (see UpdateView), so the
    /// label itself is still active on a spell while nothing is drawn.
    /// </summary>
    private static bool IsShown(TextMeshProUGUI label) =>
        label != null && label.gameObject.activeInHierarchy;

    /// <summary>
    /// A punch with a MIDDLE, laid onto <paramref name="seq"/> at absolute time <paramref name="at"/>:
    /// swell to (1 + amount) of the current scale, fire <paramref name="onPeak"/> at full swell, then
    /// settle back. DOPunchScale is the usual way to do this and is wrong here -- it peaks on its first
    /// vibrato swing, so there is no moment you can point at and call the middle, which is exactly what
    /// "change the value halfway through" needs.
    ///
    /// A null transform still fires the callback on time, so a prefab missing one widget loses that
    /// widget's punch and nothing else.
    /// </summary>
    private static void InsertPunch(Sequence seq, float at, Transform t, float amount, float duration, TweenCallback onPeak)
    {
        float half = duration * 0.5f;

        if (t == null)
        {
            seq.InsertCallback(at + half, onPeak);
            return;
        }

        // Captured at BUILD time, which is correct: nothing else animates these transforms' scales, and
        // reading it inside the tween would compound if a morph were ever restarted mid-swing.
        Vector3 baseScale = t.localScale;
        seq.Insert(at, t.DOScale(baseScale * (1f + amount), half).SetEase(Ease.OutQuad));
        seq.InsertCallback(at + half, onPeak);
        seq.Insert(at + half, t.DOScale(baseScale, half).SetEase(Ease.OutBack));
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
    /// Show or hide the stat-naming hint labels. Unlike the hover outline this is not gated on
    /// affordability — the tutorial is teaching what the numbers mean, which is worth reading on a
    /// card the player can't pay for yet. The caller decides when it applies (see
    /// <see cref="CardController.OnPointerEnter"/>).
    /// </summary>
    public void SetTutorialHints(bool on)
    {
        if (tutorialHints != null) tutorialHints.SetActive(on);
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
