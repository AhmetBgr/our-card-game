using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gives every interactive UI element in a loaded scene its hover and click sound, without anyone having
/// to remember to add a component.
///
/// The alternative -- a UISoundTrigger dropped on each control by hand -- is the version that rots: it is
/// invisible when missing, so a button added six months from now is simply silent and nobody notices
/// until a player does. A pass over the loaded scene means new UI is audible by default and staying
/// silent is the thing that takes a deliberate act (an exclusion in the library, or a UISoundTrigger set
/// to None).
///
/// Runs per scene load, and can be re-run by hand on UI spawned later -- see <see cref="Bind"/>.
/// </summary>
public static class UISoundBinder
{
    private static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _hooked = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        if (_hooked) return;
        _hooked = true;

        SceneManager.sceneLoaded += OnSceneLoaded;

        // AfterSceneLoad runs past the first scene's own sceneLoaded, so that one has to be caught here
        // or the main menu would be the one scene in the game with no button sounds.
        for (int i = 0; i < SceneManager.sceneCount; i++) BindScene(SceneManager.GetSceneAt(i));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindScene(scene);

    private static void BindScene(Scene scene)
    {
        if (!scene.isLoaded) return;

        AudioLibrary library = AudioManager.Instance != null ? AudioManager.Instance.Library : null;
        if (library == null || !library.autoBindUISounds) return;

        foreach (GameObject root in scene.GetRootGameObjects()) Bind(root, library);
    }

    /// <summary>
    /// Adds sound triggers to every Selectable under <paramref name="root"/>. Call this after spawning UI
    /// at runtime (a panel instantiated from a prefab) so it gets the same treatment scene UI does.
    /// Idempotent: elements that already have a trigger are left alone.
    /// </summary>
    public static void Bind(GameObject root) =>
        Bind(root, AudioManager.Instance != null ? AudioManager.Instance.Library : null);

    private static void Bind(GameObject root, AudioLibrary library)
    {
        if (root == null || library == null) return;

        // Inactive children included: most panels in this game start disabled and are switched on later,
        // and a pass that skipped them would leave exactly the menus the player spends time in silent.
        Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);

        foreach (Selectable selectable in selectables)
        {
            if (selectable == null) continue;

            // An explicit trigger is an authored override; never second-guess it.
            if (selectable.GetComponent<UISoundTrigger>() != null) continue;

            if (library.IsExcludedFromAutoBind(selectable.gameObject.name)) continue;

            var trigger = selectable.gameObject.AddComponent<UISoundTrigger>();
            trigger.hoverSound = GameSound.UIHover;
            trigger.clickSound = ClickSoundFor(selectable);
        }
    }

    /// <summary>
    /// The click sound that suits a control's shape. A toggle flipping and a button firing are different
    /// gestures, and a slider clicks on every drag frame, so it gets nothing.
    /// </summary>
    private static GameSound ClickSoundFor(Selectable selectable)
    {
        if (selectable is Toggle) return GameSound.UIToggle;
        if (selectable is Slider || selectable is Scrollbar) return GameSound.None;

        return GameSound.UIClick;
    }
}
