using UnityEngine;

/// <summary>A row for a <see cref="BoolModifierSO"/>: a latching switch plus an On/Off caption.</summary>
public class ModifierToggleRowView : ModifierRowView
{
    [SerializeField] private ToggleButton toggle;

    public override void Bind(GameModifierSO modifier)
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggled);
            toggle.onValueChanged.AddListener(OnToggled);
        }

        base.Bind(modifier);
    }

    public override void Refresh()
    {
        base.Refresh();
        if (toggle != null && Modifier != null) toggle.SetIsOn(MatchModifiers.GetBool(Modifier), notify: false);
    }

    private void OnToggled(bool on) => MatchModifiers.SetValue(Modifier, on ? 1f : 0f);
}
