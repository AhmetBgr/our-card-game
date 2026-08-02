using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MinionRangeHandler : Singleton<MinionRangeHandler>
{
    [Serializable]
    public struct RangeInfo
    {
        public Vector2Int[] indexes;
        public int range;
        public GameObject rangeImageObject;
    }

    public RangeInfo[] ranges;

    [Header("Range Tint")]
    [SerializeField] private Color playerRangeTint = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color opponentRangeTint = new Color(1f, 0.35f, 0.35f, 1f);

    public void ShowRange(Vector2Int index, int range, bool isPlayerMinion)
    {
        Color tint = isPlayerMinion ? playerRangeTint : opponentRangeTint;

        foreach (RangeInfo rangeInfo in ranges)
        {
            bool active = rangeInfo.indexes.Contains(index) && rangeInfo.range == range;
            rangeInfo.rangeImageObject.SetActive(active);
            if (active)
            {
                SpriteRenderer sr = rangeInfo.rangeImageObject.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(tint.r, tint.g, tint.b, sr.color.a);
            }
        }
    }

    public void HideRange()
    {
        foreach (RangeInfo rangeInfo in ranges)
        {
            rangeInfo.rangeImageObject.SetActive(false);
        }
    }

    // The units currently showing their in-range marker. Kept here because only the HOVERED unit gets an
    // OnMouseExit — the marked ones never hear that the hover ended, so someone has to remember them in
    // order to clear exactly the set that was lit.
    private readonly List<MinionController> _markedInRange = new List<MinionController>();

    // The hovered unit the marks belong to, so its death can tear them down (see OnUnitDied).
    private MinionController _markSource;

    private void OnEnable()
    {
        MinionController.OnDied += OnUnitDied;
    }

    private void OnDisable()
    {
        MinionController.OnDied -= OnUnitDied;
    }

    /// <summary>
    /// Light the in-range marker on every enemy unit — minions and the hero — that <paramref name="source"/>
    /// could strike from where it stands, so hovering a unit reads out what it threatens. Enemy is resolved
    /// relative to <paramref name="source"/>, so hovering an OPPONENT minion marks the player's units.
    ///
    /// Clears the previous marks first: moving the cursor straight from one unit onto another delivers the
    /// new enter before the old exit, so without this the first unit's marks would be left behind.
    /// </summary>
    public void ShowTargetsInRange(MinionController source)
    {
        HideTargetsInRange();

        if (source == null || source.modal == null) return;

        Agent enemy = EnemyOf(source);
        if (enemy == null) return;

        if (enemy.minions != null)
        {
            foreach (MinionController minion in enemy.minions)
                MarkIfInRange(source, minion);
        }

        MarkIfInRange(source, enemy.hero);

        _markSource = source;
    }

    public void HideTargetsInRange()
    {
        foreach (MinionController marked in _markedInRange)
        {
            if (marked != null) marked.SetInRangeIndicator(false);
        }

        _markedInRange.Clear();
        _markSource = null;
    }

    private void MarkIfInRange(MinionController source, MinionController target)
    {
        if (target == null || target == source) return;
        if (!RangeUtility.IsInRange(source, target)) return;

        target.SetInRangeIndicator(true);
        _markedInRange.Add(target);
    }

    // The side opposing this unit, read from its owner so an enemy minion's hover marks the player's units
    // rather than its own allies. modal.isPlayerMinion is the fallback for a unit whose owner was never set.
    private static Agent EnemyOf(MinionController unit)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return null;

        bool isPlayers = unit.owner != null ? unit.owner == gm.player : unit.modal.isPlayerMinion;
        return isPlayers ? gm.opponent : gm.player;
    }

    // A death anywhere in the marked set — the hovered unit or one of its targets — invalidates the whole
    // display, and a dying unit never delivers an OnMouseExit (its collider goes away under the cursor), so
    // marks lit for an attacker that traded itself away would otherwise stay up until the next hover.
    private void OnUnitDied(MinionController died)
    {
        if (died == null) return;
        if (died != _markSource && !_markedInRange.Contains(died)) return;

        HideTargetsInRange();
    }
}
