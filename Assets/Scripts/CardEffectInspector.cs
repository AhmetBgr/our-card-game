using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;

/// <summary>
/// Reads a card's actual on-play effect straight from its serialized OnPlay calls, instead of relying on
/// the AI hint fields (aiIntent / aiEffectMagnitude), which are frequently left unset on damage cards
/// (e.g. Rust Shot). UnityEvent exposes persistent-call method names and targets publicly, but NOT their
/// argument values — so the int argument of a call is read via reflection into Unity's serialized internals.
/// </summary>
public static class CardEffectInspector
{
    // ActionHolder methods that reduce a targeted minion's health by their (negative) int argument.
    // Both route through MinionController.TakeDamage, so armor applies to the result.
    private static readonly HashSet<string> DamageMethods = new HashSet<string>
    {
        "ChangeMinionHealth",
        "ChangeTargetMinionHealth",
    };

    private static readonly FieldInfo PersistentCallsField =
        typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Total damage the card's OnPlay would deal to a single targeted minion (0 if it deals none). Sums
    /// every damage-method call so a multi-hit spell reads correctly. Damage is returned pre-armor; the
    /// caller applies the target's armor to match <see cref="MinionController.TakeDamage"/>.
    /// </summary>
    public static int GetTargetedDamage(CardSO card)
    {
        if (card == null || card.OnPlay == null || PersistentCallsField == null) return 0;

        int total = 0;
        int count = card.OnPlay.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (!DamageMethods.Contains(card.OnPlay.GetPersistentMethodName(i))) continue;

            int arg = GetIntArgument(card.OnPlay, i);
            if (arg < 0) total += -arg;
        }
        return total;
    }

    private static int GetIntArgument(UnityEventBase evt, int index)
    {
        object group = PersistentCallsField.GetValue(evt);
        if (group == null) return 0;

        FieldInfo callsField = group.GetType().GetField("m_Calls", BindingFlags.NonPublic | BindingFlags.Instance);
        IList calls = callsField != null ? callsField.GetValue(group) as IList : null;
        if (calls == null || index < 0 || index >= calls.Count) return 0;

        object call = calls[index];
        FieldInfo argsField = call.GetType().GetField("m_Arguments", BindingFlags.NonPublic | BindingFlags.Instance);
        object args = argsField != null ? argsField.GetValue(call) : null;
        if (args == null) return 0;

        FieldInfo intField = args.GetType().GetField("m_IntArgument", BindingFlags.NonPublic | BindingFlags.Instance);
        return intField != null ? (int)intField.GetValue(args) : 0;
    }
}
