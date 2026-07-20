using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The hero's passive indicator row. Authored on Hero.prefab, so both PlayerHero and OpponentHero get
/// it; opponent passives are visible on purpose, since hidden opponent passives are exactly the
/// feedback gap this exists to close.
///
/// Discovery is PUSH, not pull. HeroRuntime is AddComponent'ed at runtime by HeroPassiveSystem.Register,
/// so this cannot resolve its data in Start(). It stays inert until Register hands it the runtime —
/// the one moment both prerequisites hold (hero.card is a HeroSO with passives, and the runtime exists).
/// It reads runtime.heroSO.passives rather than HeroSO directly, so it can never disagree with what
/// the system actually registered.
///
/// Everything here is read-only with respect to game state. It must never enqueue ActionHolder verbs
/// or open a triggered-action scope — see the HeroPassiveSystem header for why a second writer races
/// GameManager's scope.
/// </summary>
public class HeroPassiveIndicatorView : MonoBehaviour
{
    [Tooltip("Parent of the icon slots. Should carry a HorizontalLayoutGroup so N icons self-arrange.")]
    [SerializeField] private Transform slotContainer;

    [SerializeField] private HeroPassiveIndicator slotPrefab;

    private HeroRuntime _runtime;

    // Ordered, and the source of truth for Refresh. The dictionary is only a lookup for proc routing.
    private readonly List<HeroPassiveIndicator> _slots = new List<HeroPassiveIndicator>();
    private readonly Dictionary<HeroPassiveSO, HeroPassiveIndicator> _byPassive =
        new Dictionary<HeroPassiveSO, HeroPassiveIndicator>();
    private readonly List<HeroPassiveDisplay> _lastDisplays = new List<HeroPassiveDisplay>();

    public static HeroPassiveIndicatorView For(MinionController hero)
        => hero != null ? hero.GetComponent<HeroPassiveIndicatorView>() : null;

    /// <summary>
    /// Builds one slot per registered passive. Idempotent: it tears down first, so a re-Register or a
    /// scene reload produces N icons, never 2N.
    /// </summary>
    public void Bind(HeroRuntime runtime)
    {
        Clear();
        _runtime = runtime;

        if (runtime == null || runtime.heroSO == null || slotContainer == null || slotPrefab == null)
        {
            SetContainerActive(false);
            return;
        }

        List<HeroPassiveSO> passives = runtime.heroSO.passives;
        if (passives == null || passives.Count == 0)
        {
            SetContainerActive(false);
            return;
        }

        for (int i = 0; i < passives.Count; i++)
        {
            HeroPassiveSO passive = passives[i];
            if (passive == null) continue;

            HeroPassiveDisplay display = passive.GetDisplay(runtime);
            if (!display.visible) continue; // no icon authored, or opted out — render nothing at all

            HeroPassiveIndicator slot = Instantiate(slotPrefab, slotContainer);
            slot.Bind(passive, display);

            _slots.Add(slot);
            _lastDisplays.Add(display);

            // A hero listing the same passive asset twice would collapse in the dictionary; first one
            // wins for proc routing, but both still render and refresh off the ordered list.
            if (!_byPassive.ContainsKey(passive)) _byPassive.Add(passive, slot);
        }

        SetContainerActive(_slots.Count > 0);
    }

    /// <summary>
    /// Re-pulls GetDisplay for every slot. Cheap to call liberally: it early-outs per slot when nothing
    /// the indicator renders has changed.
    /// </summary>
    public void Refresh()
    {
        if (_runtime == null) return;

        for (int i = 0; i < _slots.Count; i++)
        {
            HeroPassiveIndicator slot = _slots[i];
            if (slot == null || slot.Passive == null) continue;

            HeroPassiveDisplay display = slot.Passive.GetDisplay(_runtime);
            if (display.SameAs(_lastDisplays[i])) continue;

            slot.Apply(display);
            _lastDisplays[i] = display;
        }
    }

    /// <summary>Pops the icon belonging to a passive that just fired. No-op for passives with no slot.</summary>
    public void PlayProcFlash(HeroPassiveSO passive)
    {
        if (passive == null) return;
        if (_byPassive.TryGetValue(passive, out HeroPassiveIndicator slot) && slot != null)
            slot.PlayProcFlash();
    }

    private void Clear()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null) Destroy(_slots[i].gameObject);

        _slots.Clear();
        _byPassive.Clear();
        _lastDisplays.Clear();
        _runtime = null;
    }

    private void SetContainerActive(bool value)
    {
        if (slotContainer != null) slotContainer.gameObject.SetActive(value);
    }
}
