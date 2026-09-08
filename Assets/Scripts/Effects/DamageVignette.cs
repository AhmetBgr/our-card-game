using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The screen flushing red at its edges when the PLAYER'S hero is hit.
///
/// Installs itself at startup and brings everything it needs with it -- its own GameObject, its own
/// Volume, and a VolumeProfile built in memory -- so there is nothing to wire into a scene and nothing
/// to un-wire: delete this file and the game still runs, exactly as GameAudioBinder does for sound.
///
/// It deliberately does NOT touch Scenes/Game/Global Volume Profile.asset. Writing to a profile asset at
/// runtime writes through to the file in the editor, and that profile's own (black) vignette is a look
/// somebody authored -- this one is a hit reaction, so it rides on top at a higher priority and blends
/// back out to nothing. Its Volume lives on the Default layer because that is what the Main Camera's
/// volume mask is set to.
///
/// Rides MinionView.DamageShown for the same reason the hit sound does: the health value drops a third
/// of a second before the number lands on screen, and a flash on the drop reads as belonging to the
/// previous beat rather than to the strike the player is watching.
/// </summary>
[DisallowMultipleComponent]
public class DamageVignette : MonoBehaviour
{
    // ---------------------------------------------------------------------------------------------
    // Tuning. Constants rather than serialized fields on purpose: nothing places this component, so an
    // inspector value would have nowhere to live. Retuning is an edit here.
    // ---------------------------------------------------------------------------------------------

    /// <summary>The colour of the ring. Deep and desaturated rather than pure red, which at full screen
    /// coverage reads as a rendering fault instead of as blood.</summary>
    private static readonly Color VignetteColor = new Color(0.65f, 0.04f, 0.05f);

    /// <summary>How far in from the edge the red reaches at full weight, and how soft that edge is. The
    /// flash never covers the middle of the board -- the player has to keep playing through it.</summary>
    private const float Intensity = 0.55f;
    private const float Smoothness = 0.5f;

    /// <summary>In fast so the hit feels struck, out slowly so the screen bleeds back to normal.</summary>
    private const float RiseSeconds = 0.07f;
    private const float FallSeconds = 0.5f;

    /// <summary>
    /// A 1-damage chip still has to register, so the flash starts most of the way up; the scaling is
    /// there to make a big hit feel bigger, not to make a small one invisible.
    /// </summary>
    private const float MinStrength = 0.55f;

    /// <summary>Damage that earns the full-strength flash. Anything above it looks the same.</summary>
    private const int FullStrengthDamage = 6;

    /// <summary>Above the scene's own Global Volume (priority 0), so this blends over that look.</summary>
    private const float VolumePriority = 100f;

    // Domain reload being off ("Enter Play Mode Options") would otherwise leave this true from the
    // previous session, and the second play would have no vignette -- the same trap GameAudioBinder
    // guards against.
    private static bool _installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _installed = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (_installed) return;
        _installed = true;

        GameObject host = new GameObject("Damage Vignette");
        DontDestroyOnLoad(host);
        host.AddComponent<DamageVignette>();
    }

    private Volume _volume;
    private VolumeProfile _profile;
    private Sequence _flash;

    private void Awake()
    {
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.name = "Damage Vignette (runtime)";

        // Never saved and never shown: this profile exists only for as long as the match does.
        _profile.hideFlags = HideFlags.HideAndDontSave;

        Vignette vignette = _profile.Add<Vignette>(true);
        vignette.color.Override(VignetteColor);
        vignette.intensity.Override(Intensity);
        vignette.smoothness.Override(Smoothness);
        vignette.rounded.Override(false);

        _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = VolumePriority;

        // sharedProfile, not profile: assigning `profile` makes the Volume clone the asset, and there is
        // nothing to protect from edits here -- we own this one outright.
        _volume.sharedProfile = _profile;

        // Weight 0 is the resting state, so the effect costs nothing visually until a hit lands.
        _volume.weight = 0f;

        MinionView.DamageShown += OnDamageShown;
    }

    private void OnDestroy()
    {
        MinionView.DamageShown -= OnDamageShown;

        _flash?.Kill();

        if (_profile != null) Destroy(_profile);
    }

    /// <summary>
    /// Only the player's own hero. Damage to the opponent's hero is good news, and minions are covered by
    /// their own damage indicator -- a screen-wide reaction to every hit on the board would be constant.
    /// </summary>
    private void OnDamageShown(MinionController minion, int damage, DamageSource source)
    {
        if (!(minion is HeroController) || minion.modal == null) return;
        if (!minion.modal.isPlayerMinion) return;

        Flash(damage);
    }

    private void Flash(int damage)
    {
        float t = Mathf.InverseLerp(1f, FullStrengthDamage, damage);
        float peak = Mathf.Lerp(MinStrength, 1f, t);

        // A second hit arriving mid-fade restarts the flash, and never by dropping the screen back down
        // to a weaker peak than it is already showing -- two hits in a row should build, not stutter.
        _flash?.Kill();
        peak = Mathf.Max(peak, _volume.weight);

        _flash = DOTween.Sequence()
            .Append(DOTween.To(Weight, SetWeight, peak, RiseSeconds).SetEase(Ease.OutQuad))
            .Append(DOTween.To(Weight, SetWeight, 0f, FallSeconds).SetEase(Ease.InQuad))
            // Unscaled: the pause menu freezes the game with Time.timeScale, and a player who pauses on
            // the frame they were hit should not be left staring at a held red screen.
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private float Weight() => _volume.weight;

    private void SetWeight(float value) => _volume.weight = value;
}
