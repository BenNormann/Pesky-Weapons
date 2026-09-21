# CLEANUP AUDIT — 2026-09-20

A full pass over `Pesky Weapons Unity/Assets`: what the game actually uses, what was
removed, what was kept on purpose as inventory, and what was deliberately left alone.

**Nothing was run.** No play mode, no test, no screenshot. The only checks made were a
clean compile, `Pesky > Validate Open Scenes` on all five scenes (0 problems each),
`manage_scene validate`, and reading serialized values back.

**Everything removed went to the SYSTEM TRASH** (`AssetDatabase.MoveAssetToTrash`, driven
from a throwaway Editor menu item that was itself trashed afterwards). Nothing was
hard-deleted and nothing was removed from the shell. Drag it back out of the recycle bin if
any of this was wrong.

---

## 1. How the used set was computed

`AssetDatabase.GetDependencies` walked outward from the five scenes that are the live game:

| Scene | Why |
|---|---|
| `Assets/Scenes/Boot.unity` | build index 0 |
| `Assets/Scenes/MainMenu.unity` | build index 1 |
| `Assets/Scenes/Tutorial.unity` | build index 2 |
| `Assets/Scenes/Labyrinth.unity` | build index 3 |
| `Assets/Scenes/Dev/FeelBox.unity` | the owner's TEST BENCH — kept with everything it needs |

Two things the raw dependency walk gets wrong, and how each was handled:

- **Scripts.** A dependency walk only finds a `MonoBehaviour` that is on a prefab or in a
  scene. Every script flagged "unused" was checked by grepping the source for its type name
  before any judgement. That is how `Pesky.Protocol` / `Sim` / `Session` / `Transport` —
  about 100 files that no scene references directly — were correctly kept, and how
  `FloorActivator` was proved to be used by nothing but the tower scenes.
- **A false positive.** `Tutorial.unity` still pointed at `Assets/Scenes/Zone1/LightingData.asset`
  (it was duplicated from `Zone1`), and that asset points back at `Zone1.unity` — so the walk
  claimed the whole tower was live. `Zone1`, `Bridge`, `MainTower`, `Keep` and `SampleScene`
  were made walk stoppers and the set recomputed; `Tutorial` turned out to have **0 lightmaps
  and 0 light probes**, so the link was cut before the folder was trashed and nothing was lost.

Assets under `Assets/` before: **376**. After: **346** (30 fewer: 35 removed, 5 new room
prefabs added). No empty folders. No loose files left in the root of `Assets/`.

---

## 2. Repairs made BEFORE anything was removed

| What | Why |
|---|---|
| `FeelBox` : deleted `_Managers/FeelProbe` | the play-mode probe object; it was the one and only reference to the stray `Assets/Rune_Metal` |
| `Tutorial` : `Lightmapping.lightingDataAsset` set to null | it pointed at `Zone1`'s baked data and carried 0 lightmaps and 0 probes |
| `Tutorial` : `NavMeshSurface "Environment"` given `Assets/Scenes/Tutorial/NavMesh-Environment.asset` | **found broken:** the surface held a *scene-embedded* copy of the NavMesh while the baked asset of the same name sat in the scene's folder unreferenced. `sourceBounds` are identical on both, so this is the same bake, now referenced the proper Unity way instead of being duplicated inside the scene file |
| `Assets/Settings/SampleSceneProfile.asset` renamed to `VP_Pipeline.asset` | **not a dead template asset.** It is the `m_VolumeProfile` of `PC_RPAsset`, i.e. the project's live post-processing (Tonemapping, Bloom, Vignette) — `VP_Zone1` and `VP_FeelBox` are both empty profiles, so all of the game's post comes from this one file. Renaming keeps the GUID and the look, and loses the template name |
| Quality level 0 "Mobile" repointed from `Mobile_RPAsset` to `PC_RPAsset` | so the Mobile render pipeline assets could go. Both quality levels now use the PC pipeline; `GraphicsSettings.defaultRenderPipeline` was already `PC_RPAsset` |
| `Assets/InputSystem_Actions.inputactions` moved to `Assets/Input/` | GUID preserved; `MainMenu`'s EventSystem still resolves it (verified by re-running the dependency walk) |

---

## 3. REMOVED (35 assets, all in the system trash)

### 3.1 DEPRECATED — the old linear tower direction

| Path | Evidence |
|---|---|
| `Assets/Scenes/Zone1.unity` | not in build settings; only "used" through the lighting-data loop of section 1 |
| `Assets/Scenes/Bridge.unity` | not in build settings, referenced by nothing |
| `Assets/Scenes/MainTower.unity` | ditto |
| `Assets/Scenes/Keep.unity` | ditto |
| `Assets/Scenes/Zone1/` (folder + `LightingData.asset`) | the tower's baked lighting; `Tutorial`'s link to it was cut first (section 2) |
| `Assets/Scripts/Game/FloorActivator.cs` | tower-only floor culler. Grep: the only occurrence of the name in the project was its own declaration. The labyrinth's rooms are 200 m apart and reached only by teleport door, so nothing can see two at once |
| `Assets/Prefabs/Rooms/ShellPanel.prefab` | the tower's exterior shell (a wall cube with no collider). Referenced by no surviving scene and **not** by `RoomShapeBuilder` (which names only `WallSegment`, `DoorwayWallSegment`, `DiscFloor`, `DiscRoof`, `Floor`, `Roof`) |

### 3.2 TEMPLATE LEFTOVER — the Unity URP sample project

| Path | Evidence |
|---|---|
| `Assets/Scenes/SampleScene.unity` | not in build settings; its only dependant was the profile now called `VP_Pipeline` |
| `Assets/TutorialInfo/` (`Scripts/Readme.cs`, `Scripts/Editor/ReadmeEditor.cs`, `Icons/URP.png`, `Layout.wlt`) | the template's Readme inspector. Nothing else referenced it — removing it is also what emptied `Assembly-CSharp`, which held no game code |
| `Assets/Readme.asset` | the template Readme data, only usable by the script above |
| `Assets/Settings/Mobile_RPAsset.asset` | only referenced by quality level 0, repointed first (section 2) |
| `Assets/Settings/Mobile_Renderer.asset` | only referenced by `Mobile_RPAsset` |

### 3.3 BUILDER LEFTOVER — things earlier agents left behind

| Path | Evidence |
|---|---|
| `Assets/Scripts/Game/Dev/` (`FeelProbe.cs`, `Zone1Probe.cs`) | play-mode probes. `FeelProbe` was on one object in `FeelBox` (removed first), `Zone1Probe` was on nothing. Agents no longer test, so both are dead by policy as well as by reference |
| `Assets/Screenshots/` (15 PNGs) | agent screenshots of Zone1 and the FeelBox; referenced by nothing, and screenshots are forbidden now |
| `Assets/Rune_Metal` | a stray `ModifierDef` saved **without the `.asset` extension** in the root of `Assets/`, a duplicate of `Assets/Data/Modifiers/Rune_Metal.asset`. Its only referrer in the whole project was `FeelProbe.testModifier` |
| `Assets/Plugins/WebGL/PeskyPlatform.jslib` | exports `Pesky_GetDevicePixelRatio`, `Pesky_QueryFlag`, `Pesky_EnterFullscreen`, `Pesky_DownloadText`, `Pesky_PickTextFile` and five more. **Grep for `Pesky_` across `Assets/Scripts` returns nothing** — no `DllImport` on the C# side calls any of them. It also still defaults a download to `atck-shift.json` |
| `Assets/Materials/M_GoblinWindup.mat` | orphan: no prefab, scene or script references it. The goblin's windup telegraph uses `M_Silver` |

---

## 4. KEPT AS INVENTORY — unused today, deliberate

The owner: *"YES I want the puzzle pieces and even some of the other shaped rooms added as
prefabs that are easy to use when developing the actual labyrinth puzzles later."*

### 4.1 Reorganised so they are easy to find

Every kit prefab moved into `Assets/Prefabs/Kit/<group>/` with `AssetDatabase.MoveAsset`, so
**every GUID and every scene reference survived**. Nothing was renamed. The full index is in
`docs/KIT.md` under "Where the pieces live".

| Group | Count | |
|---|---|---|
| `Kit/Doors/` | 11 | `Door` + 7 variants, `MagicDoor`, `MagicDoor_Grid`, `PorterGate` |
| `Kit/Pickups/` | 2 | `Key`, `RunePickup` |
| `Kit/Plates_And_Pans/` | 3 | `Plate`, `ScalesLock`, `CounterweightPair` |
| `Kit/Movers/` | 6 | `ClockMover`, `Lift`, `DropRamp`, `Rope`, `ImpactLever`, `Winch` |
| `Kit/Breakables/` | 3 | `Pot`, `CrackedWall`, `WoodBlock` |
| `Kit/Hazards/` | 2 | `LightningField`, `MagnetZone` |
| `Kit/Stations/` | 5 | `Anvil`, `HammerStand`, `WeaponRack` x3 |
| `Kit/Markers/` | 3 | `RoomVolume`, `SpawnPoint`, `PatrolRoute` |
| `Kit/Signs_And_Lights/` | 2 | `Sign`, `Torch` |
| `Prefabs/Rooms/Pieces/` | 10 | the Room Shape Builder's blocks |
| `Prefabs/Rooms/Labyrinth/` | 9 | `LabyrinthRoom` + 3 variants + 5 new shapes |
| `Prefabs/Enemies/` | 4 | `Goblin`, `GoblinSleeper`, `GoblinPorter`, `GoblinBoss` — already tidy, not moved |

`RoomShapeBuilder`'s ten hard-coded prefab paths were updated to match, and the moved paths
were rewritten across `KIT.md`, `LABYRINTH.md`, `TEST-CHECKLIST.md` and `NETCODE-STATUS.md`.
`BUILD-LOG.md` was left with its old paths on purpose: it is a dated log of what happened
then, not a reference.

### 4.2 In no build scene today, and kept anyway

`GoblinSleeper`, `GoblinPorter`, `GoblinBoss` (+ their `EnemyDef`s, which `GameData`
references, and `M_Shield`), `Door_AlwaysOpen`, `Door_Lever`, `Door_Scales`, `Door_Sealed`,
`PorterGate`, `PatrolRoute`, `DiscFloor`, `DiscRoof`, and the five new room shapes. All of
them still compile against the networked `WorldAuthority`; the per-piece `KIT_STATE` table is
in `docs/KIT.md`.

### 4.3 Five new room-shape prefabs

`LabyrinthRoom_Round`, `_Octagon`, `_LShape`, `_LongGallery`, `_TallShaft`, in
`Assets/Prefabs/Rooms/Labyrinth/`. Built with the Room Shape Builder out of
`Rooms/Pieces/*`, then given the square room's own fixtures. Each has four grid doorways
(N E S W, `doorwayDir` and back-reference verified by reading the saved prefabs), a static
World-layer shell, a roof, four torches, a `RoomVolume`, a `RoomSign`, a floor number, an
`Anchor`, a `SpawnPoint` and a `Footprint`. `roomId` is 0 on all of them. **None is placed in
`Labyrinth.unity`.** Sizes, doorway coordinates and the swap-in recipe are
`docs/LABYRINTH.md` section 12.

`LabyrinthRoom` gained `sealedSides` (four bools, N E S W) so a future shape can declare a
slot a wall; `SceneValidator` now only complains about an empty doorway slot that is *not*
declared sealed. **None of the five uses it** — every one can host all four doors. The grid
does not honour a sealed slot yet: see LABYRINTH.md 12.1 for the one hook that would fix it.

---

## 5. LEFT ALONE ON PURPOSE

| What | Why |
|---|---|
| `Assets/Plugins/WebGL/AHNet.jslib` | **in use.** `WebRtcTransport` and `Clipboard` `DllImport` ten `AHNet_*` exports from it, and `net.js` is the other half. The `AHNet` name is ATCK's, but it is a browser-bridge magic name matched in three places and there is no way to test a rename |
| `Assets/WebGLTemplates/Pesky/keys.js` | `index.html` loads it, and its keydown handler (swallowing Ctrl+S, F5, Tab...) works on its own. Only its `window.Pesky_Keys` half was orphaned with `PeskyPlatform.jslib` |
| `__atckWatched` / `__atckKey` / `__atckHooked` in `net.js` | private property names stuck on `RTCPeerConnection` objects. Renaming them is textually safe but touches the one file in the project that has never been proved to work, and I cannot run a browser |
| ATCK mentions in C# **comments** (`PlayerState`, `EnemyStateRule`, `GameLocator`, `M0Messages`, `SessionRunner`, `WorldAuthority`, `RoomCodes`) | they name the source project a file was ported from, which is exactly what `docs/NETCODE-PORT.md` is about. Provenance, not leftovers |
| Enums and constants | checked: `Protocol/Enums.cs`, `GameEnums.cs`, `LabyrinthEnums.cs` and `MsgId.cs` hold **no** aircraft / airfield / ATC / economy / licence / tower leftovers. Every id and every enum member is Pesky's. Nothing to strip, and nothing renamed, so **no byte layout and no message id changed** |
| `IVoiceControl`, `VoiceMode`, `VoiceFlags` | dormant on purpose — the netcode port was told to keep the voice seam |
| `Assets/Settings/DefaultVolumeProfile.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `PC_RPAsset`, `PC_Renderer`, `VP_Pipeline` | no scene references them; `ProjectSettings/GraphicsSettings` and `QualitySettings` do |
| `Assets/Tests/EditMode/` | `LaunchAimTests` + its asmdef. Nothing runs them (agents may not), but the folder is part of the layout |
| `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`, `Assets/TextMesh Pro/` | template-shaped, both required by packages the game uses |
| `Assets/Data/GameData_Tutorial.asset` | **a true duplicate of `GameData.asset` in every field but one** (`labyrinth`: `Labyrinth_Tutorial.asset` instead of `Labyrinth.asset`) — every weapon, enemy, modifier and tuning number is identical. It was **not** folded back, because it cannot be done as a reference change: the `LabyrinthDef` reaches `WorldSim` only through `GameData`, so sharing one asset needs a code change (a per-scene `LabyrinthDef` override on `SessionRunner`, or a second field the tutorial scene sets). That is a netcode change I could not test. **Until it is done, any edit to `GameData.asset` must be mirrored onto `GameData_Tutorial.asset` by hand** |
| `Assets/Scenes/Tutorial/NavMesh-Environment.asset` | now referenced (section 2) |

### The one thing I could not remove through the Editor

There is a **second, empty Unity project skeleton at the repository root**, beside the real
one — a leftover of the temporary two-instance dev switch:

```
C:\Users\benos\Code\Pesky Weapons\Assets\                      (empty)
C:\Users\benos\Code\Pesky Weapons\Logs\                        (empty)
C:\Users\benos\Code\Pesky Weapons\UserSettings\                (empty)
C:\Users\benos\Code\Pesky Weapons\Temp\UnityLockfile           (stale)
C:\Users\benos\Code\Pesky Weapons\ProjectSettings\ProjectVersion.txt   ("UnknownUnityVersion")
```

Only one Editor is running and it is on `Pesky Weapons Unity`, so all of this is dead — but
it is **outside** the real project, so `AssetDatabase` cannot reach it, and the standing rule
is that nothing in this repo gets deleted from the shell. **Left for the owner to delete by
hand.** It matters a little: the Hub or an agent could open the repository root and get a
broken empty project. `Pesky Weapons Unity/Pesky Weapons.slnx` (next to
`Pesky Weapons Unity.slnx`) looks like the same leftover; solution files regenerate, so
deleting it is free.

---

## 6. Docs

Moved to `docs/archive/` with a `README.md` saying what they were: `DESIGN.md`,
`IMPLEMENTATION-PLAN.md`, `CASTLE-LAYOUT-BUILD.md`, `BACK-TOWER-TEST-CHECKLIST.md`,
`level-design/castle-layout.html`, `level-design/back-tower-rooms.html`. The now-empty
`docs/level-design/` folder is gone.

Kept and current: `PREMISE.md`, `LABYRINTH.md` (+ section 12, the room prefabs),
`KIT.md` (+ the folder index and the net-sync table, minus `FloorActivator` and `ShellPanel`),
`NETCODE-PORT.md`, `NETCODE-STATUS.md`, `TEST-CHECKLIST.md`, `SLICE-1.md`, `BUILD-LOG.md`,
`research/`.

`SLICE-1-REPORT.md` was kept rather than archived — it is the current controls reference and
stage 7 updated it. Only its stale "`Zone1` is still in the project" sentence was corrected.

---

## 7. One change that is not cleanup

Asked for by the owner while this pass was running: in `Assets/Scripts/Game/UI/CompassView.cs`
the **green spike is now visibly smaller than the red one** — 70% of its length and 60% of its
width — and is still painted **last**, so it sits on top and a Mage whose two readings line up
can see both. A weapon, which only ever has the green spike, simply gets the smaller one.

The five numbers are not magic: they are USS custom properties on `.compass-dial` in
`Assets/UI/Labyrinth.uss` (`--spike-length`, `--spike-width`, `--spike-tail` describe the
full-size red spike as fractions of the dial radius; `--green-length-scale` and
`--green-width-scale` shrink the green one), read through `CustomStyleResolvedEvent` and
clamped, with the same values as fallbacks in code if the block is ever removed. Documented in
`docs/LABYRINTH.md` section 11.3. **Compile-checked only; not run.**

---

## 8. Final state

- **Compile:** clean. The console holds nothing but the known
  `NoSubscription ... generators.ai.unity.com` noise from Unity's AI package.
- **`Pesky > Validate Open Scenes`:** 0 problems on `Boot`, `MainMenu`, `Tutorial`,
  `Labyrinth` and `Dev/FeelBox`.
- **Missing scripts / dangling references:** 0 in all five, plus 0 broken prefab instances.
  `manage_scene validate` agrees.
- **Build settings:** exactly `Boot` 0, `MainMenu` 1, `Tutorial` 2, `Labyrinth` 3, all enabled.
- **Scenes:** all saved, none dirty.
- **Folders:** no empty folders, no loose files in the root of `Assets/`. Layout is
  `Data`, `Input`, `Materials`, `Physics`, `Plugins`, `Prefabs` (by kind), `Scenes`,
  `Scripts` (by assembly), `Settings`, `Tests`, `UI`, `UI Toolkit`, `WebGLTemplates`,
  `TextMesh Pro`.
