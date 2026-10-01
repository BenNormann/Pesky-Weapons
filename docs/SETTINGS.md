# SETTINGS

The in-level settings screen (round 10, 2026-09-30): what it shows, what is saved where, how
it takes and gives back the pointer, and how to add a setting or a rebindable action.

**Round 11 (2026-09-30), settings polish:** the SPIKE FILTER switch is gone (the filter is always
on), KEYBINDS has a section gap above it, and every ScrollView on the screen gets a thin dark
scrollbar (section 2.4). **Implemented, untested**: clean compile, `Settings.uxml` re-instantiated
in edit mode, validator 0 problems on `Tutorial`, `Run`, `MainMenu`.

**Implemented, untested.** Checks made: a clean compile after every script change,
`Settings.uxml` instantiated in edit mode (every element `SettingsView` queries exists with the
right type, both stylesheets attached), the keybind list collected from `PeskyControls` in edit
mode (16 rows), every serialized reference read back, and the edit-mode scene validator on
`Boot`, `MainMenu`, `Tutorial`, `Run` and `Dev/FeelBox` (0 problems each). **Nothing was run.**

---

## 1. What it is made of

| File | What |
|---|---|
| `Assets/Prefabs/UI/Settings.prefab` | one GameObject: `UIDocument` (`Settings.uxml`, `UiPanelSettings`, sortingOrder **50**: above `HUD` 0 and `RunHud` 1, below `DebugOverlay` 100) + `SettingsFlow`. Instanced as `_UI/Settings` in **`Tutorial.unity`** and **`Run.unity`**; each instance overrides only the two scene references `orbitCamera` (`_Cameras/Main Camera`) and `sessionRunner` (`_Managers/SessionRunner`) |
| `Assets/UI/Settings.uxml` | the sheet (`settings-root`) and the panel with three pages: `general-page`, `keys-page`, `confirm-page`. Loads `Menu.uss` (cards, buttons, captions, the amber accent) and `Settings.uss` (rows, sliders, the key buttons, the thin dark scrollbars) |
| `Scripts/Game/UI/SettingsFlow.cs` | the controller (`[DefaultExecutionOrder(100)]`): Escape, pointer-lock loss, open / close, the settings, EXIT |
| `Scripts/Game/UI/SettingsView.cs` | the elements, like `MenuView`: queries by name, shows one page, writes values without events, raises plain events |
| `Scripts/Game/UI/KeybindsPage.cs` | the KEYBINDS rows and the interactive rebind (ported from ATCK) |
| `Scripts/Game/GameSettings.cs` | the static that owns the saved preferences (ported in shape from ATCK) |
| `Scripts/Game/Keybinds.cs` | which bindings are rebindable, their names, the rebind operation, the override JSON in PlayerPrefs |
| `Scripts/Game/AudioSettings.cs` | **the audio stub**: one `Apply()` hook |

`MainMenu` and `Boot` have no settings screen; Escape there does nothing new. `Dev/FeelBox`
has none either (its camera still reads the saved sensitivity).

## 2. The screen

**GENERAL** (opens first): `RESUME` (amber), a **MOUSE** card (SENSITIVITY slider 0.20x -
3.00x with its value, then a `KEYBINDS` button a section gap below it), an **AUDIO** card
(MASTER, MUSIC, SFX, VOICE sliders 0 - 100 and a caption saying only MASTER does anything
yet), `EXIT TO MAIN MENU`. **KEYBINDS**: one row per binding, a status line, `RESET ALL` and
`BACK`. **CONFIRM** (a host with others in the room only): "YOU ARE THE HOST. LEAVING ENDS THE
SESSION FOR EVERYBODY IN THIS ROOM ...", `END THE SESSION AND EXIT`, `CANCEL`.

### 2.1 Opening and closing

- **Escape** (the Gameplay map's `Pause` action, so a gamepad's Start too) opens it.
- **Losing the pointer lock any other way** opens it too (alt-tab, clicking out of the page,
  the browser dropping the lock), as in ATCK: once the lock has been held `lockArmSeconds`
  (0.5 s; a fresh request flickers), losing it calls `Open()`. Under the `?nolock=1` harness
  bypass the lock never counts as lost.
- **Opening**: `OrbitCamera.InputEnabled = false` and `OrbitCamera.FreePointer()`.
- **Closing**: `RESUME`, a click on the dimmed sheet outside the panel (on the release, so the
  press never reaches the world), or Escape again - but not within `escapeGraceSeconds`
  (0.4 s) of the screen opening itself on a lost lock, because one press of Escape both drops
  the lock and arrives as a key. Closing sets `OrbitCamera.InputEnabled = true` (the look
  filter then drops the next delta) and calls `OrbitCamera.LockPointer()` (the lock landing
  makes `LookFilter.Track` drop one more). In a browser the lock may need one more click:
  Chrome refuses a new lock for about a second after Escape released one, and then the
  camera's own click-to-lock takes it.
- Escape that cancels a rebind on the KEYBINDS page does not close the screen
  (`KeybindsPage.SwallowsEscape`).

### 2.4 Spacing and scrollbars (round 11)

- A button that closes a card (`card-button`, today KEYBINDS) has a **14 px** top margin, the
  same as the gap between two cards. The rule is `Button.button.card-button`: Menu.uss's
  `Button.button` sets every margin, so a bare `.card-button` lost to it (that was the flush look).
- **Scrollbars**: `Settings.uss` restyles every `ScrollView` under a document that loads it
  (`.unity-scroll-view .unity-scroller...`): 8 px wide, transparent scroller, dark rounded
  track, grey rounded thumb (lighter on hover / drag), the arrow buttons (`__low-button` /
  `__high-button`) hidden, a 10 px gap from the content. Each selector has one class more than
  the default theme's rule for the same part, so it wins. The scroller sits beside the viewport
  (UI Toolkit lays it out in a row), so it never covers the key buttons; the mouse wheel is the
  ScrollView's own and is unchanged. `keys-scroll` also sets `horizontal-scroller-visibility=
  Hidden`. Today the only ScrollView in the settings or menu UI is `keys-scroll` (`Menu.uxml`
  has none); a ScrollView added to the menu would need these rules in `Menu.uss`.

### 2.2 What stops while it is open

`OrbitCamera.InputEnabled` is the one "an overlay owns the input" flag (the labyrinth's Tab
overlay used it first). While it is off: the camera ignores the mouse and never re-locks on a
click; `PlayerSoul` reads no input at all (no launch, possess, release, Orb roll, soul flight -
the flight inputs are cleared; `PossessHeld` is false); `MageNudge` and `MageCurse` already
required it. **The game does not pause** - it is multiplayer - so a weapon in flight keeps
flying and the timer keeps running.

Round 12: the host's PAUSE (`docs/VOTING.md`, a vote meeting) uses the same flag. While the game is
paused `PauseGate` keeps `InputEnabled` off, and `SettingsFlow.Close` asks it (`pauseGate`, an
instance override on each scene's `_UI/Settings`) before switching the input back on: it does not,
and the pointer re-locks only if the vote screen is not holding it free. The pause's end gives the
input back. The subtitle "the run keeps going for everybody else" is still true of the settings
screen itself.

### 2.3 EXIT TO MAIN MENU

`SessionRunner.LeaveToMenu()`: a **client** calls `NetSession.Leave()` (the host sees it go)
and lands on the title page with "you left the room"; a **host with other players** is asked
first (CONFIRM), then `Leave()` sends `SESSION_END(HostLeft)`, every other player gets
`HostLost` and lands on the title page with "the host left the room", and the host lands on the
title page with "you ended the session for everyone"; an **offline** session (tutorial, solo)
just stops, no question, no line. Then `GameLocator.Session = null` and the menu loads by
SessionRunner's serialized `menuScene`, the same path a finished run takes. `_returning` is
set first, so the host's own `SESSION_END` cannot queue a second load. It works in a level
opened straight from the Editor too (that one never bounces to the menu on its own).

## 3. What is saved where

Everything is in **PlayerPrefs** (Windows: the registry under the company / product name; a
browser: its IndexedDB for the page's origin, so a different host or port has its own).

| Key | Type | Default | Owner | Used by |
|---|---|---|---|---|
| `Pesky.MouseSensitivity` | float | **1.0** (0.2 - 3.0, rounded to 0.01) | `GameSettings.MouseSensitivity` | `OrbitCamera`: `lookSensitivity x` this, read every frame (live) |
| `Pesky.Volume.Master` | int 0 - 100 | 100 | `GameSettings.MasterVolume` | `AudioSettings.Apply`: `AudioListener.volume = v / 100` |
| `Pesky.Volume.Music` | int 0 - 100 | 100 | `GameSettings.MusicVolume` | stored and shown only |
| `Pesky.Volume.Sfx` | int 0 - 100 | 100 | `GameSettings.SfxVolume` | stored and shown only |
| `Pesky.Volume.Voice` | int 0 - 100 | 100 | `GameSettings.VoiceVolume` | stored and shown only |
| `Pesky.Bindings` | string (JSON) | absent | `Keybinds` | `InputActionAsset.SaveBindingOverridesAsJson` / `LoadBindingOverridesFromJson` on `PeskyControls` |
| `Pesky.Callsign` | string | | `MenuFlow` (unchanged) | the menu's name field |

`GameSettings` reads all of its keys lazily on first use (so whatever asks first - the camera
in the first frame, the screen - gets the saved values) and writes PlayerPrefs on every change
(`Changed` fires), but only calls `PlayerPrefs.Save()` from `Flush()`: when the screen closes,
when it is disabled (scene change) and on quit. The keybind JSON is saved at once on every
rebind and on RESET ALL.

**When things are applied.** `GameBootstrap.Start` (Boot) calls `AudioSettings.Apply()`.
`SettingsFlow.Awake` in every gameplay scene applies the key overrides to `PeskyControls` and
calls `AudioSettings.Apply()` again: the overrides live on the asset in memory, and a scene
load can unload the asset (MainMenu does not use it), so each gameplay scene re-applies them
before any `Update` reads an action (`OrbitCamera`, `PlayerSoul`, `MageNudge`, `MageCurse` enable
their actions in `OnEnable` and read them in `Update`; applying overrides to enabled actions is
supported). `Dev/FeelBox` has no settings prefab, so it runs on the default keys.

**The spike filter is not a setting** (round 11). It is always on for players: `OrbitCamera`
hands `Assets/Data/LookTuning.asset` straight to `LookFilter` every frame (so tuning it live in
the Inspector works), and `filterSpikes` there is a designer switch only. The round-10 key
`Pesky.MouseSpikeFilter` is deleted from PlayerPrefs by `GameSettings.Load()`, so an old saved
OFF cannot come back; nothing reads it.

## 4. Rebinding

The KEYBINDS page lists every binding of the **Gameplay** map whose authored path starts with
`<Keyboard>` or `<Mouse>` (PeskyControls has no control schemes), composite parts one by one,
**except `Look`** (the mouse itself) **and `Pause`** (Escape: the key that cancels a rebind and
the browser's own lock release; it is not rebindable). 16 rows today: LAUNCH / SOUL UP (Space),
POSSESS (E), LEAVE WEAPON (Q), MOVE FORWARD / BACK / LEFT / RIGHT (W S A D), SOUL DOWN (Shift),
MAP (Tab, nothing listens to it in the run), MAGE: NUDGE (LMB), MAGE: PULL (RMB), MAGE: CURSE
1 - 5 (1 - 5).

Click a key: it reads `PRESS A KEY` (amber) and the action is disabled while
`PerformInteractiveRebinding` listens: any keyboard key or mouse button; never the pointer's
position / delta / press, the wheel, click count or "any key"; **Escape cancels** and keeps the
old key. Accepted: the override is applied and the whole override JSON is saved. A changed key
wears an amber border. `RESET ALL` removes every override and deletes the key. There is **no
conflict check**: two actions may share a key (ATCK does the same). The tutorial signs and the
ability bar's key labels are text and do not follow a rebind.

### How to add a rebindable action

1. Add the action to the `Gameplay` map of `Assets/Input/PeskyControls.inputactions` with a
   `<Keyboard>/...` or `<Mouse>/...` binding (and a gamepad one if wanted - it is not listed).
2. Read it from a script the usual way (`controls.FindActionMap("Gameplay").FindAction(...)`
   on the same serialized `PeskyControls` asset). Nothing else: the page lists it by itself.
3. Optional: give it a readable name in `Keybinds.Names` (otherwise `MyAction2` shows as
   `MY ACTION 2`). To keep it OFF the page, add its name to `Keybinds.Skipped`.

## 5. How to add a setting

1. **`GameSettings`**: a `const string` PlayerPrefs key (`Pesky.Something`), a static field
   with its default, a property whose setter clamps, writes PlayerPrefs and calls `Touch()`,
   and a line in `Load()`.
2. **`Settings.uxml`**: a `setting-row` (a `setting-name` label, the control - `Slider`,
   `SliderInt` with class `setting-slider`, or a `Button` (round 11 removed the only ON / OFF
   switch and its `switch` styles) - and a
   `setting-value` label) inside the right card.
3. **`SettingsView`**: query it, hook its value-changed callback to a new event, and set it in
   `SetValues` with `SetValueWithoutNotify`.
4. **`SettingsFlow`**: on the event, write `GameSettings`; pass the value in `Open()`.
5. **Whatever uses it** reads `GameSettings.X` (cheap; every frame is fine) or listens to
   `GameSettings.Changed`.

## 6. The audio stub

The game has no `AudioMixer` and almost no sound. `AudioSettings.Apply()` is the one hook:
it sets `AudioListener.volume` from MASTER and only logs MUSIC / SFX / VOICE (through
`DebugGate.Log`, so in the Editor, development builds and `?debug=1` pages; once per change of
those three). The screen says so in the caption under the audio sliders. When a mixer exists:
expose a volume parameter per group, give `AudioSettings` a serialized-free way to reach the
mixer (a `GameData` field is the project's pattern), convert 0 - 100 to decibels
(`v <= 0 ? -80 : 20 * log10(v / 100)`) and set them here; nothing else changes. Note the name:
inside `Pesky.Game`, `AudioSettings` is this class; Unity's own must be written
`UnityEngine.AudioSettings`.

## 7. Riskiest untested assumptions

1. **Nothing has been run.** No slider has been dragged, no key rebound.
2. **The pointer lock in a browser.** `LockPointer()` after RESUME may only take effect on the
   next click (Chrome's user-gesture rule and its ~1 s refusal after Escape). The camera's own
   click-to-lock then takes it; the screen does not reopen in the meantime because the lock is
   only armed after it has been held 0.5 s.
3. **Escape in a browser.** Under pointer lock the browser eats the first Escape to release the
   lock; the lock loss opens the screen. Whether Unity also sees the key varies; the 0.4 s grace
   is there so one press cannot open and close it.
4. **UI Toolkit without an EventSystem.** The gameplay scenes have none (the labyrinth's Tab
   overlay worked the same way); the menu has one. If the buttons do not react in a gameplay
   scene, add an `EventSystem` + `InputSystemUIInputModule` like `MainMenu`'s.
5. **Overrides on enabled actions.** They are applied in `SettingsFlow.Awake` (order 100),
   after other scripts' `OnEnable` may have enabled actions. The Input System re-resolves
   enabled actions; no action is read before the first `Update`.
6. **Slider styling** colours Unity's internal `.unity-base-slider__tracker` / `__dragger`; if
   Unity 6.3 names them differently the sliders keep the default look.
7. **The sensitivity multiplies a threshold-in-degrees filter**: at 3.0x a fast flick is more
   likely to look like a spike and be dropped or scaled. Players cannot turn the filter off
   (round 11); if high sensitivities feel sticky, raise `spikeDegrees` in `LookTuning.asset`.
