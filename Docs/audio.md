# Audio

Ported from [terra-loop](https://github.com/AhmetBgr/terra-loop) and reworked. Scripts live in
`Assets/Scripts/Audio/`, authored assets in `Assets/Audio/`, and the one asset the game loads by name is
`Assets/Resources/AudioLibrary.asset`.

Nothing needs to be added to a scene. There is no SoundController prefab to drop in and no wiring to
forget — the first sound request builds the manager, which finds the library itself.

## The pieces

| | |
|---|---|
| `SoundEffect` | One authored sound: clips, level, pitch jitter, bus, throttle. A *description* — it never owns an AudioSource. |
| `AudioManager` | The voice pool, the buses, and the music track. Auto-bootstraps, survives scene loads. |
| `AudioLibrary` | Maps each `GameSound` id to a `SoundEffect`. The asset a designer edits. |
| `GameAudioBinder` | Subscribes game events to sounds. The only file that knows about both. |
| `UISoundBinder` / `UISoundTrigger` | Hover and click sounds for every Button/Toggle in a loaded scene. |
| `VolumeSlider` | Binds a UI Slider to the Master, Music, SFX or Ambience bus. |

## Playing a sound

```csharp
AudioManager.Instance.Play(GameSound.CardPlay);                  // flat 2D
AudioManager.Instance.PlayAt(GameSound.MinionHit, minion.transform.position);
mySoundEffect.Play();                                            // straight from an asset reference
AudioManager.Instance.PlayMusic(track, fadeDuration: 1.5f);      // crossfades from whatever is playing
```

Every call is null-safe and returns the `AudioSource` it landed on, or `null` if the sound was throttled,
muted, or has no clips. Hold the return value only if you intend to stop or fade it.

## Adding a sound

1. Drop the clips into a folder under `Assets/Audio/Clips/`. Several takes in one folder become a
   variation set automatically.
2. Create the asset: **Assets → Create → Audio → Sound Effect**, assign the clips.
3. Map it: open `Assets/Resources/AudioLibrary.asset`, add a row, pick the `GameSound` id.

To make a *new* game moment audible, add an id to the `GameSound` enum (**at the end** — values are
serialized by number) and a handler in `GameAudioBinder`. A row with no sound assigned is silence, which
is how an event stays wired up before its clip exists.

## Where the sounds come from

Every hook rides an event that already existed. No gameplay code plays a sound, and deleting
`GameAudioBinder.cs` leaves the game running silently rather than broken.

Nine events were added for this, each marked additive in its own doc comment: `Agent.CardDrawn`,
`CardController.Peeked`, `PopupManager.GameOver`, `GameManager.OnCardPlayCancelled`,
`MinionController.OnAttacked`, `MinionView.DamageShown`, and `SwitchController.HoldStarted` /
`Switched` / `HoldEnded`.

### Striking and being struck

An attack is two sounds from two sides, and each is picked by the attacker's reach — the same
`modal.range >= 2` test that already chooses the arrow animation over the slash, so the sound can never
disagree with what is on screen:

| Moment | Event | Sound |
|---|---|---|
| The swing / the shot | `MinionController.OnAttacked` | `MinionAttackMelee` or `MinionAttackRanged`, at the **attacker** |
| The damage number lands | `MinionView.DamageShown` | `MinionHitMelee` / `MinionHitRanged` / `MinionHitEffect`, at the **victim** |

Being hit deliberately does **not** ride `MinionController.OnTookDamage`. That fires the instant health
changes, which is `damageIndicatorVisualDelay` (0.35s) before the player sees anything — the value is
pushed immediately so the view can tell damage from a buff, and only the *visual* is delayed. Riding the
push would sound the hit while the strike was still swinging. `DamageShown` is raised from
`PlayDamageIndicator`, so the number and the noise are one moment by construction rather than by two
timers that agree today.

How the hit arrived travels with it as a `DamageSource`, defaulted to `Effect` so the dozen spell call
sites need no change: only `MinionController.Attack` knows it is an attack, and only its two
`TakeDamage` calls say so (the counter-attack reports the *defender's* reach, since the counter is the
defender's strike). `MinionController` latches it onto the view immediately before pushing the new
health and the view consumes it in the same synchronous call, so nothing can slip between and mislabel
a hit.

A hero keeps its single, heavier `HeroHit` whatever dealt the damage — a hit on the hero decides the
match, and that reading matters more than what it was hit with.

### The end-turn switch

Ending a turn is a *hold*, so it is a pair of sounds rather than one, and it needs three events rather
than two:

| Moment | Event | Sound |
|---|---|---|
| Press begins | `HoldStarted` | gears start running — **looping**, so an indefinite hold sustains |
| Released before 0.15s | `HoldEnded` | loop fades out (0.08s). This *is* the "that did not take" feedback |
| Hold survives 0.15s | `Switched` | loop stops, the gear-change plays, the turn flips |

`Switched` cannot be the thing that stops the loop on its own: a successful hold does **not** release the
button — the turn flips at `longPressDur` while the player is usually still pressing — so `HoldEnded` is
the guaranteed closing bracket. Both exits stop the loop unconditionally, because stopping a sound that
is not playing is a no-op and that is cheaper than tracking which exit got there first.

The loop is stopped with a short fade rather than cut, since a sustained mechanical bed chopped
mid-cycle clicks audibly.

Note the player's turn end deliberately does **not** ride `GameManager.OnTurnEnd`. The switch is the only
way a player ends a turn (`TurnManager` is dead code), and only the switch knows about the press — the
half of the interaction that happens before the turn is over. Subscribing to both would sound it twice.

## What changed from the original

The original was a `SoundEffect` ScriptableObject plus a `SoundController` singleton. Kept the shape;
fixed and extended the rest.

**Bugs fixed**

- `Stop()` threw a `NullReferenceException` whenever the source was null, and its guard was inverted.
- The play cursor was a `[SerializeField]`, so playing a sound in the editor dirtied the `.asset` on disk
  and the sequence carried between sessions. It is runtime-only state now.
- `PlayOneShot` was called on a source whose `clip` and `loop` had just been set — `PlayOneShot` ignores
  `loop`, so looping sounds silently never looped.
- `useRandomVolume` and its range existed but the custom inspector never drew them.
- The inspector wrote straight to the target and called `SetDirty` every repaint, so merely *looking* at a
  sound asset marked it changed. It uses `SerializedProperty` now: undo, multi-edit and prefab overrides
  all work.
- The editor previewer was built in `OnEnable` of every sound asset — one hidden GameObject per asset —
  and `OnDisable` threw if one had not been created. One shared previewer, made on first use.
- `Mathf.Log10(0)` in the volume slider path produced `-Infinity` dB at zero.
- The `SoundController` prefab shipped with `PlayOnAwake` enabled.

**Added**

- **Voice cap and stealing.** The pool grows to 32 and then steals the oldest voice no more important than
  the incoming sound. The original grew unboundedly via `AddComponent<AudioSource>`.
- **Throttling.** A minimum retrigger interval and a per-sound concurrency cap, plus a stack falloff so six
  minions dying in one frame swells instead of summing into distortion (measured: 0.55 → 0.41 → 0.31 →
  0.23, fourth copy onward rejected).
- **Positional playback.** Voices are child GameObjects, so a sound can be heard where it happened. The
  original stacked every source on one object, which can only ever be 2D.
- **Buses.** Separate Master/Music/SFX/Ambience volumes, kept in the settings file (see
  [settings.md](settings.md)) and squared so the slider tracks perceived loudness rather than spending
  most of its travel sounding identical.
- **The library.** The original carried one `public SoundEffect` field per event on the controller, so
  adding a sound meant editing the controller and re-wiring every scene holding one.
- **`RandomNoRepeat`** play order, so a variation set never plays the same clip twice running.
- **Automatic UI sounds**, so a button added later is audible by default rather than silently mute.
- **Music** with crossfade. There was no music channel at all.
- DOTween is no longer needed for delays, and the mixer asset is no longer needed for volume.

### Why volume is not on an AudioMixer

The original drove an `AudioMixer` exposed parameter. Applying the bus gain per voice instead means there
is no `.mixer` asset whose parameter names have to stay in sync with the code, no silent no-op on a build
where that asset failed to load, and no interaction with snapshot transitions (which ignore `SetFloat`).
It also behaves identically on WebGL. `AudioManager.mixerGroup` is still there if you later want mixer
effects — routing and volume are independent.

## Clip credits

Clips came across from terra-loop: Kenney (CC0) interface and impact packs, plus individual Freesound and
Zapsplat cuts. 85 clips, ~5 MB, imported as mono / decompress-on-load.
