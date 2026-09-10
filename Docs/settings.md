# Settings

Everything the player can set about how the game presents itself: the volume buses, the mute switch, and
the interface options. Scripts live in `Assets/Scripts/Settings/`, the panel that shows them in
`Assets/Scripts/UI/SettingsPanelController.cs`, and the tool that builds that panel in
`Assets/Scripts/Editor/SettingsPanelBuilder.cs`.

## The pieces

| | |
|---|---|
| `SettingsData` | The payload of `settings.json`. One field per setting, each with its default as an initializer. |
| `GameSettings` | Static. Loads, persists, and raises `Changed`. The only place a setting is read or written. |
| `SettingsPanelController` | The panel: seeds its controls from the settings and writes straight back. |
| `VolumeSlider` | Binds one Slider to one bus. Follows the setting as well as pushing to it. |
| `SettingsPanelBuilder` | Editor tool. Builds `SettingsPanel.prefab` and instances it in both hosts. |
| `Button_SettingsToggle.prefab` | The switch every toggle row uses. Instanced, so restyling it restyles every row. |
| `Slider_Settings.prefab` | The bar every volume row uses, on the same terms. |

## Reading and writing a setting

```csharp
GameSettings.MasterVolume = 0.5f;          // clamped, persisted, and announced
bool expanded = GameSettings.ShowActionLog;

GameSettings.Changed += Refresh;           // anything showing a setting subscribes
```

There is no instance to find and nothing to add to a scene. `GameSettings` is static because settings have
to answer before any scene object exists — `AudioManager` levels its first voice off them, and that voice
can be the hover sound of the first button in the main menu.

Never mutate `GameSettings.Data` directly: the properties are what clamp the value, persist it, and tell
the UI. Reading it is fine.

## Where it is stored

| | |
|---|---|
| Windows / editor / standalone | `settings.json` under `Application.persistentDataPath`, written to a `.tmp` sibling and swapped in |
| WebGL | The `SettingsJson` PlayerPrefs key, flushed with `PlayerPrefs.Save()` on every write |

The browser is the odd one out for the same reason it is in [`SaveManager`](../Assets/Scripts/SaveManager.cs):
`persistentDataPath` there points into an in-memory emscripten filesystem that the engine only flushes to
IndexedDB from the PlayerPrefs subsystem, so a `System.IO` write looks like it succeeded and is thrown away
on the next page load.

**Separate from the save on purpose.** Settings belong to the device rather than the profile, they must be
readable before a save exists, and clearing a save — or a corrupt one — must never cost the player their
volume. `Tools ▸ Save Data ▸ Clear Save Data` leaves settings alone, and `Tools ▸ Settings ▸ Clear
Settings` leaves decks and the high score alone.

**Writes are coalesced.** A slider drag raises a change every frame; a file write (or an IndexedDB flush)
per frame would be felt. Changes mark the settings dirty and a hidden `SettingsFlusher` writes them 0.35 s
after the last one, plus on pause, focus loss and quit. Call `GameSettings.Flush()` to force it.

## Adding a setting

1. Add a field to `SettingsData`, **with its default as an initializer** — that default is also what a
   file written before the field existed deserializes to, so no version bump and no migration.
2. Add a property to `GameSettings` next to the others (`SetFloat`/`SetBool` do the clamping, persisting
   and announcing).
3. Read it where it matters, and subscribe to `GameSettings.Changed` if what you are showing has to follow
   it live.
4. To put it on the panel, add a row in `SettingsPanelBuilder` and run **Tools ▸ Settings ▸ Install
   Settings Panels**.

Both controls a row can hold are prefabs, instanced into every row, so each is styled in one place:

| | | A row overrides only |
|---|---|---|
| `Button_SettingsToggle.prefab` | the switch on a toggle row | — |
| `Slider_Settings.prefab` | the bar on a volume row | `VolumeSlider.bus`, `VolumeSlider.valueLabel` |

Anything else set per instance — a colour, a size, a sprite — is how two rows drift apart, so put it in
the prefab instead.

The slider carries a transparent **Hit Area** child, and that is not decoration: Unity's slider factory
puts no graphic on the slider's own object, so out of the box the only things a pointer can hit are the
13px painted track and the handle, and a press that visibly lands on the bar does nothing. The Hit Area
covers the control and uses negative `raycastPadding` (negative *grows* the rect) to reach out to the full
60px height of a row. If you restyle the slider, keep it.

One trap on the switch: change its sprites on the **ToggleButton** component (the `Off` and `On` sets),
never on its Image. `ToggleButton.ApplyVisuals` writes the Image's sprite from those sets on validate,
awake and enable, so anything set directly on the Image is stamped back over.

## The panel

The panel is one prefab — `Assets/Prefabs/UI/SettingsPanel.prefab` — instanced in both places it can be
opened from, so a row added to it appears in both at once and there is no second copy to keep in step:

- **Pause menu** (`Assets/Prefabs/UI/EscMenu.prefab`) — the Settings button latches like How To Play and
  shares the left column with the rules, so only one of the two is ever up. Escape backs out of the
  settings before it closes the menu.
- **Title screen** (`Assets/Scenes/MainMenu.unity`) — the Settings button in the menu column, under
  Play, opens it centred on the canvas.

Each host overrides exactly two things on its instance, and nothing else:

| Override | Pause menu | Title screen |
|---|---|---|
| `Window` position | `(-131.5, 0)`, in the rules' column | `(0, 0)` |
| `Backdrop` active | off — the pause menu already dims the screen, and a second block would cover the button column | on, the way the credits panel dims the menu |

**Once the prefab exists, the prefab is the panel** — edit it in the editor like any other asset.
`Install Settings Panels` will not touch a prefab, an instance or a Settings button that is already there:
it fills in only what is missing, writes nothing at all when nothing was missing, and never re-places or
re-styles what it finds. It is safe to re-run at any time.

The one destructive entry says so in its name. `Rebuild Settings Panel Prefab (discards hand-edits)`
builds the asset from scratch under a throwaway Canvas (a UI hierarchy saved without one loses its Images
and its TMP children) and verifies what landed on disk — the right tool after changing the builder, and
the wrong one after an afternoon of tuning the panel by hand. Instances keep their prefab link either way.

The controls never hold a value. Each is seeded from `GameSettings` and writes straight back, and the panel
re-seeds on `Changed` — which is what makes **Defaults**, and the action log's own toggle button out on the
board, show up on the panel with no code between them.

## Migrations

Both run once, on a device that has no settings file yet, and are then invisible:

- Volumes written by the earlier audio system to the `audio.master` / `audio.sfx` / `audio.music` /
  `audio.muted` PlayerPrefs keys are adopted, and those keys deleted.
- `ShowActionLog` and `HoverTiltEnabled`, which used to ride along in the JSON save, are handed over by
  `SaveManager` after it loads. The two fields are still in `SaveData`, unread by the game, because they
  are what this reads from.

## Editor tools

| Menu item | |
|---|---|
| Tools ▸ Settings ▸ Install Settings Panels | Fills in whatever is missing — prefab, instances, buttons — and leaves everything else exactly as it is. Safe to re-run. |
| Tools ▸ Settings ▸ Rebuild Settings Panel Prefab (discards hand-edits) | Rebuilds the panel prefab from the code. Throws away anything tuned in it by hand. |
| Tools ▸ Settings ▸ Rebuild Settings Slider Prefab (discards hand-edits) | The same, for the volume bar. |
| Tools ▸ Settings ▸ Reveal Settings File | Opens `settings.json` in the file browser. |
| Tools ▸ Settings ▸ Print Settings | Dumps the current settings to the console. |
| Tools ▸ Settings ▸ Clear Settings | Deletes the file and returns to defaults. Leaves the save alone. |
