using System;

/// <summary>
/// The Forged in Battle questions asked through <see cref="ConfirmationPopup"/>, worded in one place so
/// the Draft scene's top bar and the pause menu ask exactly the same thing.
/// </summary>
public static class GauntletRunPrompts
{
    public const string AbandonTitle = "Abandon Run?";
    public const string AbandonMessage = "Your hero, deck and progress in this run will be lost.\nThis can't be undone.";
    public const string AbandonConfirm = "Abandon";
    public const string AbandonCancel = "Keep Playing";

    /// <summary>
    /// Asks before giving the run up. On yes the run and its save are deleted
    /// (<see cref="GauntletRun.Abandon"/>), then <paramref name="thenLeave"/> takes the player away.
    /// </summary>
    public static void ConfirmAbandon(Action thenLeave)
    {
        ConfirmationPopup.Show(AbandonTitle, AbandonMessage, () =>
        {
            GauntletRun.Abandon();
            thenLeave?.Invoke();
        }, confirmText: AbandonConfirm, cancelText: AbandonCancel, destructive: true);
    }
}
