using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the room making noise, and swaps rooms when the scene does.
///
/// The counterpart to <see cref="GameAudioBinder"/>, and deliberately separate from it. The binder maps
/// events to sounds -- every id it plays is a REACTION, cued to something the player or the board just
/// did. Ambience has no event to hang off: the room is making its noise whether or not anyone is
/// playing, and the whole point is that it carries the stretches where nothing is happening. So it needs
/// something that runs on the scene rather than on the game, which is what this is, and there is nothing
/// in the binder for it to share.
///
/// Two beds, one per room: <see cref="GameSound.AmbienceMenu"/> and <see cref="GameSound.AmbienceMatch"/>.
/// Re-requesting the bed already playing is a no-op inside <see cref="AudioManager"/>, so bouncing
/// between two menu scenes does not restart the menu's room tone -- only actually changing rooms
/// crossfades.
///
/// Zero-setup, like <see cref="AudioManager"/> itself: nothing has to exist in a scene, and it survives
/// scene loads so it is still there to make the swap.
/// </summary>
[DisallowMultipleComponent]
public class AmbienceDirector : MonoBehaviour
{
    /// <summary>
    /// The one scene that gets the match bed. Everything else -- the main menu, the custom-game screen --
    /// is menu-side, so the rule is stated as "is it the match" rather than as a list of menus that
    /// would need editing every time a screen is added.
    ///
    /// The name matches the default the rest of the game already loads by (MainMenuManager.gameSceneName,
    /// EscMenuController.gameSceneName). Those are serialized fields a scene could override; this is a
    /// constant because the director builds itself and has no inspector to be overridden in.
    /// </summary>
    private const string MatchScene = "Game";

    /// <summary>
    /// Seconds to cross from one room to the other. Long: a bed that snaps in announces itself, and the
    /// one thing ambience must not do is be noticed starting.
    /// </summary>
    private const float CrossfadeDuration = 3f;

    private static AmbienceDirector _instance;

    // Statics survive entering play mode when domain reload is disabled ("Enter Play Mode Options"),
    // which would otherwise leave a dead instance here and a silent room. Same guard as AudioManager.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    /// <summary>
    /// Builds the director once the first scene is up. AfterSceneLoad rather than BeforeSceneLoad
    /// because it touches <see cref="AudioManager.Instance"/>, which builds a GameObject.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        _instance = new GameObject(nameof(AmbienceDirector)).AddComponent<AmbienceDirector>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// The first scene is already loaded by the time the bootstrap runs, so its sceneLoaded has been and
    /// gone -- this is what starts the room for it. Every scene after that arrives through
    /// <see cref="OnSceneLoaded"/>.
    /// </summary>
    private void Start() => Apply(SceneManager.GetActiveScene());

    /// <summary>
    /// Additive loads are ignored: they layer something onto the scene the player is already in rather
    /// than moving them to a new room, and treating one as a room change would crossfade the bed out
    /// from under them.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        Apply(scene);
    }

    /// <summary>Each room's layers, in the order they sit on <see cref="AudioManager"/>'s ambience sources.</summary>
    private static readonly GameSound[] MenuLayers =
        { GameSound.AmbienceMenu, GameSound.AmbienceMenu2, GameSound.AmbienceMenu3, GameSound.AmbienceMenu4 };

    private static readonly GameSound[] MatchLayers =
        { GameSound.AmbienceMatch, GameSound.AmbienceMatch2, GameSound.AmbienceMatch3, GameSound.AmbienceMatch4 };

    private void Apply(Scene scene)
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        GameSound[] layers = scene.name == MatchScene ? MatchLayers : MenuLayers;

        for (int layer = 0; layer < AudioManager.AmbienceLayers; layer++)
        {
            SoundEffect bed = layer < layers.Length ? manager.Library.Get(layers[layer]) : null;

            // A layer with nothing mapped to it -- or mapped to an asset whose clip slot is still empty --
            // leaves the manager doing nothing, which means the OTHER room's layer would keep playing over
            // the top. Stopping makes an unfilled id read as silence rather than as the wrong room.
            if (bed == null || !bed.HasAudibleClip) manager.StopAmbience(layer, CrossfadeDuration);
            else manager.PlayAmbience(layer, bed, CrossfadeDuration);
        }
    }
}
