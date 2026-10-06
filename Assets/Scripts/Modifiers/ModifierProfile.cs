using System;
using System.Collections.Generic;

/// <summary>One saved modifier value, keyed by <see cref="GameModifierSO.id"/>.</summary>
[Serializable]
public class ModifierValue
{
    public string id;
    public float value;
}

/// <summary>
/// The values a player last chose for one mode. Lives in SaveData (one per mode id) so the popup opens
/// on what was picked last time. Values are looked up by id, so a modifier missing from the profile --
/// a new one, or a fresh save -- reads its default.
/// </summary>
[Serializable]
public class ModifierProfile
{
    public string modeId;
    public List<ModifierValue> values = new List<ModifierValue>();

    public float Get(GameModifierSO modifier)
    {
        if (modifier == null) return 0f;

        for (int i = 0; i < values.Count; i++)
            if (values[i].id == modifier.id) return modifier.Clamp(values[i].value);

        return modifier.DefaultValue;
    }

    public void Set(GameModifierSO modifier, float value)
    {
        if (modifier == null) return;
        value = modifier.Clamp(value);

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i].id != modifier.id) continue;
            values[i].value = value;
            return;
        }

        values.Add(new ModifierValue { id = modifier.id, value = value });
    }

    public void Clear() => values.Clear();
}
