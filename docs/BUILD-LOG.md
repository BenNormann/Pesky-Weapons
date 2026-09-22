# BUILD-LOG

Running log of what each build stage actually created in `Pesky Weapons Unity`.
Authoritative spec is `docs/SLICE-1.md`. Exact asset paths and scene object paths below.

---

## Stage 1 — Project setup (no gameplay code)

Unity 6000.3.23f1, URP 17.3, Input System 1.20, AI Navigation 2.0.14. All work done through
MCP for Unity. No `.unity` / `.prefab` / `.asset` / `.mat` / `.meta` file was hand-written or
shell-written; the only shell write is this Markdown file.

### MCP session setup
- Tool groups activated: `scripting_ext`, `ui`, `testing`, `docs` (on top of the always-on `core`).
- `probuilder` NOT activated — `com.unity.probuilder` is not installed in this project.
- **`batch_execute` hard limit in this project is 25 commands.** Verified: a 26-command batch is
  rejected with `batch_execute supports up to 25 commands (configured in Unity); received 26`.
- `create_script` is NOT batchable (`Unknown or unsupported command type: create_script`) — script
  creation must be individual tool calls.

### Physics
- Gravity set to `(0, -20, 0)` — read back via `manage_physics get_settings`: `gravity=[0,-20,0]`.
- Fixed timestep left at default — measured `Time.fixedDeltaTime = 0.02` (reported as 0.01999999).

### Layers (slots assigned by Unity)
| Slot | Layer |
|---|---|
| 8 | World |
| 9 | Weapon |
| 10 | Soul |
| 11 | Enemy |
| 12 | Trigger |
| 13 | Pickup |
| 14 | Debris |

### Collision matrix (3D), read back with `manage_physics get_collision_matrix`
Only the seven project layers are shown; the five built-in layers (Default, TransparentFX,
Ignore Raycast, Water, UI) were left at their Unity defaults (all-collide) because no gameplay
object lives on them.

|            | World | Weapon | Soul | Enemy | Trigger | Pickup | Debris |
|---|---|---|---|---|---|---|---|
| **World**   | ✓ | ✓ | ✓ | ✓ | ✗ | ✗ | ✓ |
| **Weapon**  | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ | ✗ |
| **Soul**    | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Enemy**   | ✓ | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ |
| **Trigger** | ✗ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ |
| **Pickup**  | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Debris**  | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |

This is exactly the spec: Soul touches World only; Debris touches World only; Weapon touches
World/Weapon/Enemy/Trigger/Pickup; Enemy touches World/Weapon/Enemy/Trigger.

**Note for the next stage:** because nothing but Weapon is listed as touching Pickup, `Pickup`
collides with `Weapon` only — including `Pickup` vs `World` = OFF. Pickups therefore cannot rest
on the floor physically; author them as trigger volumes placed by hand (which is how the Key /
Rune are described in SLICE-1 anyway). Same for `Trigger` vs `World` = OFF. If a pickup ever needs
to be a falling rigidbody, that row has to change and the change belongs in this log.

### Folders created
```
Assets/Scenes/Dev
Assets/Scripts, Assets/Scripts/{Data,Game,Editor}
Assets/Tests, Assets/Tests/EditMode
Assets/Prefabs, Assets/Prefabs/{Weapons,Player,Enemies,Kit,Rooms}
Assets/Data, Assets/Data/{Weapons,Enemies,Modifiers}
Assets/Materials
Assets/Physics
Assets/Input
Assets/UI
```
Also created incidentally: `Assets/Screenshots` (MCP screenshot output) and
`Assets/Settings/VP_Zone1.asset`, `Assets/Settings/VP_FeelBox.asset` (URP VolumeProfiles).
`Assets/Scenes` already existed. SampleScene, TutorialInfo and Readme were left untouched.

### Assembly definitions
The MCP has no tool that creates an `.asmdef` asset, so these four were written with
`execute_code` (`File.WriteAllText` into `Assets/...` + `AssetDatabase.Refresh`), which is the
Editor-API fallback the stage brief allows. They are real imported AssemblyDefinitionAssets.

| Path | Name | References | Platforms |
|---|---|---|---|
| `Assets/Scripts/Data/Pesky.Data.asmdef` | Pesky.Data | (none) | all |
| `Assets/Scripts/Game/Pesky.Game.asmdef` | Pesky.Game | Pesky.Data, Unity.InputSystem, Unity.AI.Navigation, Unity.RenderPipelines.Universal.Runtime, Unity.RenderPipelines.Core.Runtime | all |
| `Assets/Scripts/Editor/Pesky.Editor.asmdef` | Pesky.Editor | Pesky.Data, Pesky.Game | Editor only |
| `Assets/Tests/EditMode/Pesky.Tests.asmdef` | Pesky.Tests | Pesky.Data, Pesky.Game, UnityEngine.TestRunner, UnityEditor.TestRunner | Editor only, `nunit.framework.dll`, `UNITY_INCLUDE_TESTS` |

UI Toolkit needs no asmdef reference (it lives in the built-in `UnityEngine.UIElementsModule`).

Placeholder types (delete once real types land):
- `Assets/Scripts/Data/PeskyDataAssemblyMarker.cs` — `Pesky.Data.PeskyDataAssemblyMarker`
- `Assets/Scripts/Game/PeskyGameAssemblyMarker.cs` — `Pesky.Game.PeskyGameAssemblyMarker`; its
  `ReferenceProbe()` touches `InputActionAsset`, `NavMeshSurface` and `UniversalAdditionalCameraData`
  so a broken package reference would fail the compile rather than fail silently later.
- `Assets/Scripts/Editor/PeskyEditorAssemblyMarker.cs` — `Pesky.Editor.PeskyEditorAssemblyMarker`
- `Assets/Tests/EditMode/AssemblyWiringTests.cs` — `Pesky.Tests.AssemblyWiringTests`

Verified after compile with `CompilationPipeline.GetAssemblies`:
`Pesky.Game -> compiled; Pesky.Data -> compiled; Pesky.Editor -> compiled; Pesky.Tests -> compiled`
(1 source file each). EditMode run of assembly `Pesky.Tests`: **1 total, 1 passed, 0 failed**
(`Pesky.Tests.AssemblyWiringTests.RuntimeAssembliesAreReferenced`, 0.66 s).

### Input
`Assets/Input/PeskyControls.inputactions` — one action map `Gameplay`, built with the Input System
Editor API (`InputActionSetupExtensions` → `asset.ToJson()` → write + `AssetDatabase.ImportAsset`),
not by duplicating the template. Loaded back as an `InputActionAsset` and enumerated:

| Action | Type / expected control | Bindings |
|---|---|---|
| Look | Value / Vector2 | `<Mouse>/delta` (scaleVector2 0.06), `<Gamepad>/rightStick` (scaleVector2 2) |
| Jump | Button | `<Keyboard>/space`, `<Gamepad>/buttonSouth` |
| Possess | Button | `<Keyboard>/e`, `<Gamepad>/buttonWest` |
| Release | Button | `<Keyboard>/q`, `<Gamepad>/buttonNorth` |
| Roll | Value / Vector2 | 2DVector composite W/S/A/D, `<Gamepad>/leftStick` |
| Pause | Button | `<Keyboard>/escape`, `<Gamepad>/start` |

**Owner's rule honoured:** WASD appears in exactly one place in the whole project — the `Roll`
action, which is the Orb-only rolling torque. There is no movement action bound to WASD. Nothing
in a later stage may bind WASD to anything else, and `Roll` must only be read by the Orb.

No control schemes and no generated C# wrapper class were added — gameplay code should reference
the asset through a serialized `InputActionAsset` field (SLICE-1 §7), so the wrapper is not needed.
`Assets/InputSystem_Actions.inputactions` (template) is untouched and unused.

### Materials — `Assets/Materials`, all `Universal Render Pipeline/Lit`, flat colours (17)
| Asset | Base colour (linear 0-1) | Extra |
|---|---|---|
| M_Floor | 0.43, 0.43, 0.45 | smoothness 0.1 |
| M_Wall | 0.31, 0.31, 0.35 | smoothness 0.1 |
| M_Ledge | 0.35, 0.47, 0.59 | smoothness 0.1 |
| M_Hazard | 0.78, 0.24, 0.16 | |
| M_Door | 0.59, 0.43, 0.27 | |
| M_DoorLocked | 0.47, 0.16, 0.16 | |
| M_Key | 0.86, 0.71, 0.24 | |
| M_Plate | 0.63, 0.63, 0.67 | |
| M_Anvil | 0.24, 0.24, 0.26 | |
| M_Rune | 0.59, 0.27, 0.78 | |
| M_Goblin | 0.24, 0.63, 0.27 | |
| M_GoblinWindup | 0.86, 0.20, 0.20 | |
| M_Soul | 0.24, 0.86, 0.94 | emissive: `_EmissionColor` (0.2, 1.7, 1.9), `_EMISSION` keyword on, RealtimeEmissive |
| M_Metal | 0.71, 0.71, 0.75 | metallic 0.85, smoothness 0.6 |
| M_Wood | 0.55, 0.39, 0.24 | |
| M_Banana | 0.94, 0.86, 0.24 | |
| M_Sign | 0.90, 0.90, 0.86 | |

Gotcha for later: `manage_material set_material_color` auto-detects a 0-255 range as soon as any
component exceeds 1, so it cannot express an HDR emission colour — M_Soul's emission was set with
`execute_code` on the material asset instead.

### Physics materials — `Assets/Physics` (read back)
| Asset | dynamicFriction | staticFriction | bounciness | combine |
|---|---|---|---|---|
| P_Weapon.physicMaterial | 0.6 | 0.6 | 0.1 | friction Average / bounce Average |
| P_Bouncy.physicMaterial | 0.4 | 0.4 | 0.6 | friction Average / bounce Maximum |
| P_World.physicMaterial | 0.8 | 0.8 | 0.0 | friction Average / bounce Average |

Gotcha: `manage_physics create_physics_material` treats `path` as the **folder**, not the file —
passing a full `.physicMaterial` path creates a folder of that name. Pass `path="Assets/Physics"`
plus `name`. (The first attempt made nested folders; they were deleted and the materials recreated.)

### Prefabs — `Assets/Prefabs/Rooms` (grey-box kit, unit cubes, scaled per instance)
| Prefab | Material | Physics material | Layer | Static |
|---|---|---|---|---|
| `Assets/Prefabs/Rooms/Floor.prefab` | M_Floor | P_World | World | yes (all flags) |
| `Assets/Prefabs/Rooms/Wall.prefab` | M_Wall | P_World | World | yes (all flags) |
| `Assets/Prefabs/Rooms/Ledge.prefab` | M_Ledge | P_World | World | yes (all flags) |

Gotcha: `manage_gameobject action=create` accepts `is_static` but does **not** apply it (result came
back `isStatic:false`). Static flags were set on the three prefab assets with
`GameObjectUtility.SetStaticEditorFlags` + `PrefabUtility.SavePrefabAsset`; instances then inherit
static correctly. Check `isStatic` in the result of every object you expect to be static.

### Scenes
All three created from the `empty` template and saved.

**`Assets/Scenes/Boot.unity`** (build index 0) — one root object `_Bootstrap` (Transform only).
Deliberately has no camera or light, per SLICE-1 §7. It has **no loader script yet** — the
"Boot loads Zone1" behaviour is a later stage's job and `_Bootstrap` is the object to put it on.

**`Assets/Scenes/Zone1.unity`** (build index 1) — root order exactly as SLICE-1 §7:
```
_Managers
_Cameras
  Main Camera            Camera + AudioListener, tag MainCamera, pos (0,4,-10) rot (15,0,0)
_Lighting
  Directional Light      Light, Directional, intensity 1, Soft shadows, pos (0,10,0) rot (50,-30,0)
  Global Volume          Volume, global, priority 0, profile Assets/Settings/VP_Zone1.asset
                         (Tonemapping Neutral + Bloom intensity 0.4 threshold 1)
_UI
Environment              NavMeshSurface (added, DEFAULT settings, NOT BAKED — no geometry yet)
Hallways
Shells
```
Exactly one Camera and exactly one AudioListener in the scene.

**`Assets/Scenes/Dev/FeelBox.unity`** (not in the build) — same root groups, plus:
```
_Cameras/Main Camera     pos (0,12,-22) rot (22,0,0)
_Lighting/Directional Light, _Lighting/Global Volume (VP_FeelBox.asset, Tonemapping + Bloom)
Environment/DevBox/Geometry/
  Floor        Floor.prefab   pos (0, -0.25, 0)      scale (30, 0.5, 30)   top surface y = 0
  Wall_North   Wall.prefab    pos (0, 4, 15.25)      scale (31, 8, 0.5)
  Wall_South   Wall.prefab    pos (0, 4, -15.25)     scale (31, 8, 0.5)
  Wall_East    Wall.prefab    pos (15.25, 4, 0)      scale (0.5, 8, 30)
  Wall_West    Wall.prefab    pos (-15.25, 4, 0)     scale (0.5, 8, 30)
  Step         Ledge.prefab   pos (0, 0.5, 4)        scale (4, 1, 2)       top y = 1.0
  Ledge        Ledge.prefab   pos (-13, 2.25, -6)    scale (4, 0.5, 4)     top y = 2.5
```
Interior is exactly 30 x 30 m (wall inner faces at x/z = ±15.0), walls 8 m high and 0.5 m thick;
N/S walls are 31 long so the four corners close. The Ledge's west edge sits flush against the
west wall's inner face (x = -15.0) so it can be used to test the wall-jump rebound from §2.
Every geometry object is a prefab instance, static (all flags) and on layer World (8) —
confirmed in the instantiation results (`isStatic:true, layer:8`).

### Build settings
`manage_scene get_build_settings`:
```
0  Assets/Scenes/Boot.unity    enabled
1  Assets/Scenes/Zone1.unity   enabled
```
Nothing else. SampleScene and FeelBox are deliberately out.

### Acceptance checks — measured results
| Check | Result |
|---|---|
| Console has no errors | PASS for this stage's work. The only console errors are pre-existing, repeating `Error reason is 'NoSubscription' … generators.ai.unity.com` messages from Unity's AI generator service (present before any change and again after a console clear). Zero compile errors, zero errors from any asset or scene created here. |
| Four asmdefs compile | PASS — all four report `compiled` from `CompilationPipeline.GetAssemblies`; EditMode test in `Pesky.Tests` passed 1/1. |
| Layers and matrix read back correctly | PASS — layers 8-14 as tabled; matrix read back matches the spec table exactly. |
| Input asset lists six actions with the right bindings | PASS — reloaded from disk as an `InputActionAsset`: 1 map, 6 actions, bindings as tabled. |
| Three scenes exist, saved, reopen cleanly | PASS — each was loaded from disk after saving and its hierarchy re-read; no errors, contents intact. |
| Build settings list exactly Boot and Zone1 | PASS — indices 0 and 1, both enabled, nothing else. |
| Screenshot of FeelBox looks right | PASS — `Assets/Screenshots/FeelBox_Stage1.png`, positioned capture from (22, 20, -28) looking at the floor: flat 30x30 floor, four walls closed at the corners, the step near the middle and the raised ledge against the west wall. |

### Left stubbed / notes for the next stage
- `_Bootstrap` in Boot.unity has no components. Boot does not load Zone1 yet.
- `NavMeshSurface` on `Zone1/Environment` is at default settings and has **not** been baked
  (nothing to bake). Next stage should set its layer mask to `World`, `Collect Objects = Children`,
  author the geometry, then bake.
- The four assembly marker types are placeholders; delete each once its assembly has real content.
- `Assets/Prefabs/{Weapons,Player,Enemies,Kit}` and `Assets/Data/*` are empty.
- `Assets/UI` is empty — the UI Toolkit HUD (UXML/USS/PanelSettings) has not been started.
- No `WorldAuthority`, no weapon/soul/goblin code, no ScriptableObjects yet.
- The grey-box kit is only Floor / Wall / Ledge. Door, Plate, Anvil, Rune, Key, Sign, Rack and the
  weapon prefabs still need authoring; their materials already exist under `Assets/Materials`.
- Remember: WASD is bound only to `Roll`, and `Roll` is for the Orb alone.

---

## Stage 2 — Core mechanics in FeelBox (SLICE-1 sections 2, 3, 4)

All Unity work through MCP for Unity (script tools, `manage_scriptable_object`, `manage_gameobject`,
`manage_components`, `manage_prefabs`, `manage_material`, `manage_scene`, play mode via `manage_editor`).
No `.unity` / `.prefab` / `.asset` / `.mat` / `.meta` was hand-written. The only shell write is this file.
`execute_code` was used for read-back checks, to set the static flags on the rack plinth before it was
saved as a prefab, and to start / read the play-mode probe.

### MCP gotchas found this stage
- `create_script` compiles synchronously enough that one `execute_code` returning
  `EditorApplication.isCompiling` + `EditorUtility.scriptCompilationFailed` + `Type.GetType(...)` is a
  reliable "did it compile" check (the console is polluted by the pre-existing Unity AI
  `NoSubscription` errors, so reading it for compile errors is noisy).
- `execute_code` uses the CodeDom compiler here: **C# 6 only** (no `out var`, tuples, local functions).
- `manage_components set_property`: a single object reference takes an instance id (it resolves the
  component from the GameObject id) or an asset path; **arrays of scene references need
  `[{"instanceID": -123}, ...]`** — a bare `[-123]` silently writes nulls. Colors need
  `{"r":..,"g":..,"b":..,"a":..}`, not an array.
- Private `[SerializeField]` fields are settable through the MCP; same-object references are filled by
  `Reset()` when the component is added.
- Foreground `sleep` is blocked in this harness; wait with a background `sleep N` and an `until` loop.
- `run_tests` right after leaving play mode can fail to initialise; re-run with `init_timeout` 60000.

### Scripts
| Path | Type | Notes |
|---|---|---|
| `Assets/Scripts/Data/WeaponDef.cs` | `Pesky.Data.WeaponDef` (SO) | id, displayName, launchSpeed, launchTorque, mass, linearDrag, angularDrag, physicsMaterial, damage, maxHp, canRoll, rollTorque, prefab |
| `Assets/Scripts/Data/MovementTuning.cs` | `Pesky.Data.MovementTuning` (SO) | shared numbers, see below |
| `Assets/Scripts/Data/ModifierDef.cs` | `Pesky.Data.ModifierDef` (SO) | id, displayName, damageMultiplier (minimal; gives the modifier list a real type) |
| `Assets/Scripts/Game/LaunchAim.cs` | static, PURE | `LiftDegrees`, `PitchForLift`, `Heading`, `Right`, `Grounded`, `WallJump`, `Direction`, `ElevationDegrees` |
| `Assets/Scripts/Game/LevelClock.cs` | MonoBehaviour | `Ms` (long) = `Time.timeAsDouble` based, `SetMs` re-bases it; no static instance |
| `Assets/Scripts/Game/WeaponHomeSlot.cs` | MonoBehaviour | serialized `id`, `Position`, `Rotation`, gizmo |
| `Assets/Scripts/Game/IWeaponPossessor.cs` | interface | `NotifyCombat()` |
| `Assets/Scripts/Game/WeaponBody.cs` | MonoBehaviour | Rigidbody setup from the def (`ApplyDef`, also a ContextMenu), contact tracking, last safe position, kill-Y `Recover`, HP, modifiers, possession state, `Break` + timed respawn, stable `id`, `Teleport`. Events: `Broken`, `Respawned`, `HpChanged`, `ModifiersChanged`, `Recovered` |
| `Assets/Scripts/Game/WeaponMotor.cs` | MonoBehaviour + `LaunchResult` enum | `SetAim(yaw,pitch)`, `TryLaunch()`, `Evaluate(out velocity)`, `SetRoll(Vector2)`; never reads input |
| `Assets/Scripts/Game/OrbitCamera.cs` | MonoBehaviour | Look action -> yaw/pitch (clamp -35..+75), distance 5, SmoothDamp of the target POSITION only, spherecast + ray pull-in against World, click locks the pointer, Pause (Esc) frees it, `SetLook`, `SetTarget`, `InputEnabled` |
| `Assets/Scripts/Game/TrajectoryPreview.cs` | MonoBehaviour | LineRenderer, 24 points over 1 s, g = 20 from the tuning asset, cut at World, hidden while a launch would be refused |
| `Assets/Scripts/Game/PlayerSoul.cs` | MonoBehaviour, `IWeaponPossessor` | reads Jump / Possess / Release / Roll from the serialized `InputActionAsset`; public `SetThrust`, `TryPossess`, `Possess`, `Release`, `TryLaunch`, `SetRoll`, `Teleport`, `NotifyCombat`, `AddTargeter/RemoveTargeter`, `InCombat`, `Candidate` (for the HUD prompt), `InputEnabled`. Events `Possessed`, `Released(weapon, broke)` |
| `Assets/Scripts/Game/PlayerSpawner.cs` | MonoBehaviour | instantiates the soul prefab at `spawnPoints[localSpawnIndex]`, sets the camera yaw from the spawn point, `soul.Init(camera)`; `LocalSoul` |
| `Assets/Scripts/Game/Dev/FeelProbe.cs` | `Pesky.Game.Dev.FeelProbe` | dev telemetry for FeelBox only: drives the game through the same public methods the input calls and writes a pipe-separated `Report`. Context-menu entries `Run All`, `Run Launches`, ... |
| `Assets/Scripts/Editor/WeaponPrefabTools.cs` | menu `Pesky/Weapons/Apply Defs To Prefabs` | copies mass / drag / physics material from every WeaponDef onto its prefab so the authored prefab shows the runtime numbers |
| `Assets/Tests/EditMode/LaunchAimTests.cs` | 6 EditMode tests | see acceptance table |

Deleted: all four `Pesky*AssemblyMarker` placeholders and `AssemblyWiringTests.cs` (every assembly now
has real content; `Pesky.Editor` holds `WeaponPrefabTools`).

No `GameObject.Find`, `FindObjectOfType` or `Resources.Load` in gameplay code. (`GetComponent` on the
weapon the soul just possessed and `GetComponentsInChildren` on a weapon's own colliders are used.)

### Design decisions worth knowing
- **Camera pitch convention:** positive pitch looks DOWN. Lift map is linear and clamped:
  pitch +20 (resting) -> 15 deg, pitch -35 -> 80 deg; 45 deg lift is pitch -5.38. `LaunchAim.PitchForLift`
  is the inverse.
- **Launch = SET velocity** to `dir * launchSpeed`, plus `g * fixedDeltaTime / 2` (= +0.2 m/s) on Y when
  `MovementTuning.compensateIntegrator` is on. PhysX integrates semi-implicitly, which loses exactly
  `g*dt*t/2` of height; without the term the 50 Hz simulation flies 13-17 % low on the apex of a
  minimum-lift hop. With it the body lands on the same parabola that the preview draws and that room
  ballistics (g = 20) assume. launchSpeed numbers are untouched. Turn the flag off to get the raw value.
- **Launch torque** is an angular velocity change in rad/s (`ForceMode.VelocityChange`, mass independent),
  about the launch's right-hand axis, NOSE UP. Nose-down tumble digs a long weapon's tip into the floor
  on a 15 deg hop.
- **Grounded while asleep:** a sleeping Rigidbody gets no `OnCollisionStay`, so `WeaponBody.FixedUpdate`
  keeps refreshing the contact state the body fell asleep with. Verified below.
- **Wall-jump counter** lives in `WeaponMotor` and resets on the first ground contact after the wall jump.
  Grounded always wins over wall. Ground layers = World + Weapon + Enemy; wall layer = World only.
- **Last safe position** is recorded only on a ground contact with a static World collider (no Rigidbody on
  the other side, so never a moving platform or another weapon) while slower than 1 m/s.
- **Break** hides renderers, disables colliders, freezes the body (constraints, not kinematic) and keeps the
  GameObject active so its own `Update` runs the 10 s timer; respawn = home slot pose, full HP, no modifiers.
- **Roll:** `WeaponMotor.SetRoll` zeroes the input unless `def.canRoll`; only `Orb.asset` has it.
- **Orbit camera:** pivot offset raised to (0, 1, 0). With 0.4 the camera hit the floor as soon as the view
  tilted above horizontal, i.e. on every 45 deg jump. It still pulls in for very high lobs (pitch below about
  -10). `minDistance` 0.1 and a backup raycast keep it inside the room when the target hugs a wall.

### Data assets
`Assets/Data/MovementTuning.asset`: minLift 15, maxLift 80, restingCameraPitch 20, maxLiftCameraPitch -35,
launchCooldown 0.25, compensateIntegrator true, groundedNormalY 0.6, groundedGrace 0.1, wallNormalY 0.3,
wallGrace 0.15, groundMask World|Weapon|Enemy (2816), wallMask World (256), wallJumpsPerAirtime 1,
wallReboundBlend 0.5, wallMinDot 0.3, wallDotMargin 0.05, gravity 20, killY -20, recoverLift 0.5,
safeSpeed 1, previewSeconds 1, previewPoints 24.

`Assets/Data/Modifiers/Metal.asset`: id 1, damageMultiplier 1.25 (used by the probe; Rune_Metal can use it).

`Assets/Data/Weapons/*.asset` (launchSpeed / mass / damage / HP exactly as SLICE-1 section 4):
| Asset | id | launchSpeed | mass | damage | maxHp | launchTorque rad/s | linearDrag | angularDrag | physics material | canRoll / rollTorque |
|---|---|---|---|---|---|---|---|---|---|---|
| Sword | 1 | 12 | 3 | 15 | 100 | 2.5 | 0.05 | 1 | P_Weapon | no |
| Dagger | 2 | 14 | 1 | 9 | 70 | 4 | 0.05 | 1 | P_Weapon | no |
| Staff | 3 | 11 | 2.5 | 12 | 90 | 2 | 0.05 | 3 | P_Weapon | no |
| Orb | 4 | 10 | 4 | 10 | 90 | 0 | 0.05 | 3 | P_Weapon | yes / 10 N*m |
| Mace | 5 | 10 | 8 | 20 | 120 | 2 | 0.05 | 3 | P_Weapon | no |
| Hammer | 6 | 9 | 14 | 28 | 140 | 1.5 | 0.05 | 1.5 | P_Weapon | no |
| Banana | 7 | 13 | 0.5 | 6 | 60 | 5 | 0.05 | 1 | P_Bouncy | no |
Each def's `prefab` field points at its prefab. Tuning done: only drag / torque (the values above were the
first pass and every weapon settled, so nothing was changed afterwards). No launchSpeed was touched.

New material: `Assets/Materials/M_Preview.mat` (URP Unlit, white) for the trajectory line.

### Prefabs
Weapons, `Assets/Prefabs/Weapons/<Name>.prefab`: root (layer Weapon) = Rigidbody + WeaponBody + WeaponMotor,
long axis along local +Z so identity rotation lies flat; children are primitives that keep their own
collider (compound collider), all on layer Weapon. Rigidbody on the prefab (written by the menu tool):
mass from the def, Interpolate, Continuous Dynamic, free rotation, maxAngularVelocity 25. Centre of mass is
PhysX's automatic one from the compound colliders (measured local COM in brackets).
| Prefab | Children: primitive, local pos, scale, material | COM (local) |
|---|---|---|
| Sword | Blade Cube (0,0,0.25) 0.1x0.05x0.9 M_Metal; Guard Cube (0,0,-0.22) 0.4x0.06x0.08 M_Metal; Grip Cube (0,0,-0.4) 0.06x0.06x0.28 M_Wood | (0,0,0.04) |
| Dagger | Blade Cube (0,0,0.1) 0.08x0.04x0.4 M_Metal; Guard Cube (0,0,-0.12) 0.2x0.05x0.05 M_Metal; Grip Cube (0,0,-0.23) 0.05x0.05x0.18 M_Wood | (0,0,-0.02) |
| Staff | Shaft Cylinder rot (90,0,0) scale 0.08x0.9x0.08 (1.8 m long, r 0.04, capsule collider) M_Wood | (0,0,0) |
| Orb | Ball Sphere scale 0.7 (r 0.35) M_Metal | (0,0,0) |
| Mace | Shaft Cylinder (0,0,-0.1) rot (90,0,0) 0.07x0.4x0.07 M_Wood; Head Sphere (0,0,0.4) scale 0.32 M_Metal | (0,0,0.33) |
| Hammer | Shaft Cylinder (0,0,-0.15) rot (90,0,0) 0.08x0.5x0.08 M_Wood; Head Cube (0,0,0.4) 0.5x0.3x0.3 M_Metal | (0,0,0.35) |
| Banana | Mid Capsule rot (90,0,0); TipA Capsule (0.07,0,0.22) rot (90,35,0); TipB Capsule (0.07,0,-0.22) rot (90,-35,0); all scale 0.12x0.14x0.12, M_Banana, P_Bouncy | (0.05,0,0) |

`Assets/Prefabs/Player/PlayerSoul.prefab`: root (layer Soul) Rigidbody (no gravity, mass 1, FreezeRotation,
Continuous Speculative, Interpolate) + SphereCollider r 0.25 + PlayerSoul (controls = PeskyControls,
body, soulCollider, visual, preview wired). Children: `Visual` (Sphere scale 0.5, M_Soul, no collider) with
`Glow` (Point light, range 4, intensity 2, cyan, no shadows); `TrajectoryPreview` (LineRenderer M_Preview
width 0.06 world space + TrajectoryPreview with the tuning asset). Soul numbers on the component: accel 18,
max 7, releaseDrag 4, possessRange 2, weaponMask Weapon, combatSeconds 5, popOutLift 0.5.

`Assets/Prefabs/Kit/WeaponRack.prefab`: root `WeaponRack` (World) > `Plinth` (Cube 10 x 0.3 x 2 at y 0.15,
M_Wood, P_World, layer World, static all flags) + `Slot_1`..`Slot_7` (WeaponHomeSlot ids 1-7) at local
x = -3.75, -2.5, -1.25, 0, 1.25, 2.5, 3.75, y = 0.7, z = 0.

### FeelBox scene (`Assets/Scenes/Dev/FeelBox.unity`, saved, still not in the build)
```
_Managers/LevelClock            LevelClock
_Managers/PlayerSpawner         PlayerSpawner: soulPrefab = PlayerSoul.prefab, spawnPoints = [SoulSpawn_1], orbitCamera = Main Camera
_Managers/FeelProbe             FeelProbe: spawner, orbitCamera, clock, tuning, weapons[7], launchOrigin, wallOrigin, testModifier = Metal
_Cameras/Main Camera            + OrbitCamera (controls = Assets/Input/PeskyControls.inputactions, pivotOffset (0,1,0), distance 5, pitch -35..75, minDistance 0.1)
Environment/DevBox/Gameplay/WeaponRack                 WeaponRack.prefab at (0, 0, -12)
Environment/DevBox/Gameplay/Weapons/{Sword,Dagger,Staff,Orb,Mace,Hammer,Banana}
                                prefab instances at x = -3.75 .. 3.75 step 1.25, y 0.7, z -12 (they drop onto the plinth);
                                overrides: WeaponBody.id = 1..7, WeaponBody.homeSlot = Slot_1..Slot_7
Environment/DevBox/Spawns/SoulSpawn_1         (0, 1.5, -8) yaw 180 (faces the rack)
Environment/DevBox/Spawns/Probe_LaunchOrigin  (-10, 0, 9) yaw 90
Environment/DevBox/Spawns/Probe_WallOrigin    (-12, 0, 13.2) yaw 270 (3 m from the west wall, 1.8 m from the north wall)
```
One Camera, one AudioListener. Screenshot: `Assets/Screenshots/FeelBox_Stage2_Rack.png`.

### Verification — play mode, final run (after the camera change), measured by `FeelProbe`
Method: the probe teleports a weapon to `Probe_LaunchOrigin`, waits until it is still and grounded, moves the
soul behind it and calls `PlayerSoul.TryPossess()`, sets the camera with `OrbitCamera.SetLook`, then calls
`PlayerSoul.TryLaunch()` — the same calls the Jump / Possess actions make. It samples
`Rigidbody.worldCenterOfMass` after every physics step. "Apex" = highest COM minus launch COM vs
`vy^2 / 2g`. "Landing" = COM at the first ground contact later than 0.06 s after launch; its horizontal
distance is compared with the ballistic distance to that same COM height (a tumbling long weapon touches
down tail-first while its COM is still up to 0.2 m high, so the flat-ground range `v^2 sin2θ / g` is listed
separately). g = 20. Device input and mouse look are disabled while the probe runs.

| Weapon | lift | result | apex m meas / ideal | landing dist m meas / ballistic | flat-ground range | t land s | rest dist m | settle s | mid-air 2nd launch |
|---|---|---|---|---|---|---|---|---|---|
| Sword | 15 | Launched | 0.24 / 0.24 (-0.7 %) | 2.30 / 2.25 (+2.0 %) | 3.60 | 0.20 | 4.45 | 0.78 | - |
| Sword | 45 | Launched | 1.77 / 1.80 (-1.4 %) | 6.62 / 6.70 (-1.2 %) | 7.20 | 0.80 | 7.75 | 0.92 | RefusedAirborne |
| Dagger | 15 | Launched | 0.33 / 0.33 (-0.6 %) | 3.98 / 3.91 (+2.0 %) | 4.90 | 0.30 | 6.04 | 0.78 | - |
| Dagger | 45 | Launched | 2.41 / 2.45 (-1.7 %) | 9.29 / 9.60 (-3.2 %) | 9.80 | 0.98 | 9.19 | 1.08 | RefusedAirborne |
| Staff | 15 | Launched | 0.20 / 0.20 (-0.5 %) | 1.69 / 1.63 (+3.5 %) | 3.03 | 0.16 | 4.00 | 0.80 | - |
| Staff | 45 | Launched | 1.49 / 1.51 (-1.3 %) | 5.62 / 5.76 (-2.3 %) | 6.05 | 0.74 | 5.98 | 0.62 | RefusedAirborne |
| Orb | 15 | Launched | 0.17 / 0.17 (-1.0 %) | 2.64 / 2.51 (+5.5 %) | 2.50 | 0.28 | 8.46 | 5.44 | - |
| Orb | 45 | Launched | 1.23 / 1.25 (-1.3 %) | 5.05 / 5.11 (-1.2 %) | 5.00 | 0.74 | 7.21 | 5.04 | RefusedAirborne |
| Mace | 15 | Launched | 0.17 / 0.17 (-1.0 %) | 1.35 / 1.37 (-1.9 %) | 2.50 | 0.14 | 3.34 | 0.86 | - |
| Mace | 45 | Launched | 1.23 / 1.25 (-1.3 %) | 4.58 / 4.69 (-2.3 %) | 5.00 | 0.66 | 6.15 | 4.38 | RefusedAirborne |
| Hammer | 15 | Launched | 0.13 / 0.14 (-0.5 %) | 1.73 / 1.73 (-0.2 %) | 2.03 | 0.20 | 2.88 | 1.30 | - |
| Hammer | 45 | Launched | 1.00 / 1.01 (-1.1 %) | 3.63 / 3.71 (-2.1 %) | 4.05 | 0.58 | 4.00 | 0.76 | RefusedAirborne |
| Banana | 15 | Launched | 0.28 / 0.28 (-0.8 %) | 2.99 / 2.96 (+0.9 %) | 4.23 | 0.24 | 3.57 | 2.58 | - |
| Banana | 45 | Launched | 2.08 / 2.11 (-1.6 %) | 8.26 / 8.41 (-1.7 %) | 8.45 | 0.94 | 7.93 | 2.00 | RefusedAirborne |

Worst apex error -1.7 %, worst landing error +5.5 % (limit 15 %). Every weapon settled (still + grounded for
0.4 s) after every launch; longest is the Orb at 5.4 s because it rolls out (rest 8.5 m after a 2.5 m hop —
WASD can brake it). In every row the weapon was picked by `TryPossess`, the preview was visible before the
launch (8-24 points) and the out-of-combat release left the weapon unbroken and free.
**For room geometry:** a minimum-lift hop touches down tail-first well short of `v^2 sin2θ / g` (Sword 2.3 m,
Staff 1.7 m, Mace 1.35 m, Hammer 1.7 m to first contact); use the 45 deg numbers and COM clearance for gaps.

Wall jump (all seven; approach = 45 deg launch at the west wall from 3 m, then `TryLaunch` while airborne and
touching, aim yaw 315 = into the wall and toward north; the rebound then reaches the north wall in the air):
| Weapon | approach | on the wall | wall normal | dot(v, n) after | counter | 2nd wall touch, same airtime | counter after landing | grounded relaunch |
|---|---|---|---|---|---|---|---|---|
| Sword | Launched | WallJumped | (1,0,0) | 0.65 | 1 | RefusedWallJumpUsed | 0 | Launched |
| Dagger | Launched | WallJumped | (1,0,0) | 0.65 | 1 | RefusedWallJumpUsed | 0 | Launched |
| Staff | Launched | WallJumped | (1,0,0) | 0.64 | 1 | RefusedWallJumpUsed | 0 | Launched |
| Orb | Launched | WallJumped | (1,0,0) | 0.64 | 1 | RefusedWallJumpUsed | 0 | Launched |
| Mace | Launched | WallJumped | (1,0,0) | 0.64 | 1 | RefusedWallJumpUsed | 0 | Launched |
| Hammer | Launched | WallJumped | (1,0,0) | 0.64 | 1 | RefusedWallJumpUsed | 0 | Launched |
| Banana | Launched | WallJumped | (1,0,0) | 0.65 | 1 | RefusedWallJumpUsed | 0 | Launched |

| Check | How | Result |
|---|---|---|
| Grounded launch from a settled resting pose | after the run all 7 weapons `IsSleeping()` = true and `IsGrounded` = true; possessed the sleeping Mace (and the Hammer in the first run) and called `TryLaunch` in the same frame | PASS — `Launched`, v = (7.07, 7.27, 0) = 10 m/s at 45 deg + 0.2 compensation |
| Second launch in mid-air refused | `TryLaunch` 0.35 s after a 45 deg launch, airborne | PASS — `RefusedAirborne` x7 |
| Exactly one wall jump, rebounds, second refused until landing | table above | PASS x7, dot 0.64-0.65 > 0.3 |
| Orb rolls with Roll, Sword does not | `SetRoll((0,1))` 1.5 s then `((1,0))` 1 s, camera yaw 90 | PASS — Orb 3.68 m forward, 3.98 m/s, then 1.86 m to the right; Sword 0.00 / 0.00 / 0.00 |
| Soul thrust moves and stops | `SetThrust(true)` 1 s along camera (yaw 30, pitch -30), then false | PASS — 3.60 m/s at 0.2 s, 7.00 m/s at 1 s, dot(move, camera forward) = 1.00; 0.87 m/s 0.5 s after release, 0.00 at 2 s |
| Soul collides with World | 1.5 s of thrust straight down at the floor | PASS — y = 0.25 (radius 0.25, floor top 0) |
| Possess picks by view angle | soul 1.2 m from the Dagger on the rack with Sword and Staff 1.25 m either side | PASS — `Candidate` = Dagger, `TryPossess` true, visual hidden, camera target = weapon |
| Release out of combat | Dagger with 1 modifier and 20 damage | PASS — not broken, hp 50/50 kept, modifiers 1, free again, soul visible 0.5 m above it |
| Release in combat breaks the weapon | `weapon.NotifyCombat()` then `Release()` | PASS — InCombat true, broken, modifiers 0, soul free 0.5 m above the break point |
| Broken weapon returns after 10 s, no modifiers | same Dagger | PASS — still broken at 9 s, back after 10.00 s, 0.00 m from Slot_2, hp 70/70, modifiers 0, free |
| HP 0 breaks and the soul pops out | `ApplyDamage(9999)` on the possessed Banana | PASS — broken, soul free and visible 0.5 m above the death point, InCombat true; respawned on Slot_7 at 60/60 |
| Falling below y = -20 recovers | Staff teleported to (40, 2, 40), outside the box | PASS — `Recovered` after 1.52 s, lowest y -20.23, settled 0.00 m from its last safe position, hp untouched |
| LevelClock | delta over 5.5 s | PASS — 5524 ms vs `Time.time` 5523.3 ms |
| Weapon rotation never affects the camera | while the Mace tumbled at euler (352,128,78) | PASS — angle between camera rotation and Euler(pitch, yaw, 0) = 0.0000, roll 0 |
| Camera pull-in | soul 0.5 m from the south wall, camera behind it | PASS after the fix — camera at z -14.75 (wall face -15), 0.27 m from the pivot; 5.00 m in open space. Before the fix `minDistance` 0.6 pushed it to z -15.06, inside the wall |
| Real input path (first run) | queued Input System keyboard state events | PASS — Space moved the free soul 15 m, E possessed the Sword, Space launched it 4.45 m, Q released it, W drove `motor.RollInput` to (0,1) on the Orb and left the Sword at (0,0) |
| EditMode tests | `run_tests` assembly `Pesky.Tests` | PASS — 6/6: lift map ends, grounded never below 15 deg (pitch -90..90, all yaws), heading follows yaw, wall jump dot > 0.3 and upward for 7 normals x all yaw x all pitch, reflection keeps the tangential aim, grounded beats wall |
| Console | edit and play mode | No errors or warnings from this project. The only entries are the pre-existing Unity AI `NoSubscription` errors (same as stage 1) and MCP-for-Unity transport warnings |
| FeelBox saved | `scene.isDirty` | false; build list still Boot + Zone1 only |

Not machine-verified: pointer lock on click / Esc (needs a real mouse; during the first run the pointer did
become locked and the yaw changed, which means someone clicked into the Game view — that is also why the
probe now disables camera and soul device input while it runs).

### Left stubbed / broken
- The debris puff on break is not made; a broken weapon just vanishes.
- There is no `WorldAuthority` yet. Possess, release, break, damage and modifiers are direct public calls
  (`PlayerSoul.Possess/Release`, `WeaponBody.Break/ApplyDamage/AddModifier`). The next stage should route
  them through WorldAuthority as request -> validate -> event; the methods are already the right seams.
- No HUD: `PlayerSoul.Candidate`, `InCombat`, `CombatSecondsLeft`, `WeaponBody.Hp/Modifiers` and the events
  are there for it.
- Impact damage, goblins, plates, doors, keys: not started. "A goblin is targeting you" is
  `PlayerSoul.AddTargeter/RemoveTargeter`.
- The Orb sits on the flat plinth with nothing to stop it rolling off if it is nudged.
- Ball and chain: not made (spec says stubbed).

### Notes for the next stage
- Put weapons in Zone1 as prefab instances, give each a unique `WeaponBody.id` and a `homeSlot`; drop a
  `WeaponRack.prefab` for Room1 and a single `WeaponHomeSlot` on the Room4 stand.
- `_Managers` in Zone1 needs `LevelClock` and `PlayerSpawner` (soul prefab, spawn points, the Main Camera's
  `OrbitCamera`); the Main Camera needs `OrbitCamera` with `controls` = PeskyControls and pivotOffset (0,1,0).
- Moving things take a serialized `LevelClock` reference and read `Ms`.
- Weapons only treat layer World as a wall and World / Weapon / Enemy as ground (masks on the tuning asset).
  Moving platforms should be kinematic Rigidbodies so they never become a "last safe position".
- The camera pulls in when it looks up from near the floor; rooms with low ceilings will pull it in too.
- `FeelProbe` can be reused: it only needs the serialized references listed above.


---

## Stage 3 — Authority seam, kit, Goblin, HUD and the Zone1 rooms (SLICE-1 sections 5, 6, 7)

All Unity work through MCP for Unity. No `.unity` / `.prefab` / `.asset` / `.mat` / `.meta` was hand-written
or shell-written; the only shell write is this file. `execute_code` was used the way a human uses a small
editor utility: bulk prefab instantiation into the scene (`PrefabUtility.InstantiatePrefab`), bulk Inspector
wiring (`SerializedObject`), the NavMesh bake, ballistics maths and driving the play-mode probe. Every asset
is a real Editor-created asset.

### MCP gotchas found this stage
- **The Unity Editor's player loop does not advance while the Editor window is unfocused**, even with
  `Application.runInBackground = true`: `Time.frameCount` stayed at 1 for minutes while `realtimeSinceStartup`
  ran. MCP requests are still served (the socket handler ticks), but no frame runs. The fix used here:
  `Time.captureDeltaTime = 0.02f` plus a loop of `EditorApplication.Step()` inside `execute_code`. That gives a
  **deterministic 50 Hz play-mode run, ~150 frames per second of wall time** (exactly one fixed step per
  frame). Chunk it at ~1500 Step() calls per `execute_code` call — 3000 overruns the MCP response timeout.
- `PlayerSettings.runInBackground` was turned **on** (it was off). Set it in EDIT mode: a change made during
  play mode is reverted when play mode exits.
- `manage_asset action=rename` failed (`destination` resolved to `Assets/<name>`); `AssetDatabase.RenameAsset`
  via `execute_code` is the fallback. Note `Assets/Data/Modifiers/Metal.asset` promised by the Stage 2 log
  **did not exist on disk** — the folder was empty. `Rune_Metal.asset` was created fresh (see below).
- `manage_ui create_panel_settings` wants `reference_resolution` as `{"x":..,"y":..}`, not `[w,h]`.
- **A `<ui:Style src="project://database/...">` placed directly under `<ui:UXML>` is silently ignored** —
  `VisualTreeAsset.stylesheets` came back empty and every element resolved to the default 14 px dark text.
  Moving it *inside* the root `VisualElement` fixes it. `HudController` also adds the stylesheet at runtime
  from a serialized `StyleSheet` field as a belt-and-braces fallback.
- `manage_camera screenshot` renders the **currently open scene's** Main Camera. Taking a positioned shot
  while `Boot.unity` is open gives a solid colour; open `Zone1.unity` first.
- Editing a script while play mode is running triggers a recompile + domain reload that drops the MCP
  connection for ~20-30 s. Stop play mode before editing scripts.
- `create_script` still refuses to overwrite; use `delete_script` then `create_script` for a full rewrite.
- `execute_code` is still CodeDom / C# 6 (no `out var`, no local functions, no `$""`). Lambdas and
  `System.Func` / `System.Action` delegates work and are the way to factor helpers.

### Scripts added — `Pesky.Data`
| Path | Type | Notes |
|---|---|---|
| `Assets/Scripts/Data/EnemyDef.cs` | `Pesky.Data.EnemyDef` (SO) | radius, height, moveSpeed, angularSpeed, acceleration, maxHp, aggroRange, attackRange, windupSeconds, attackDamage, attackKnockback, attackCooldown, knockbackPerDamage, maxKnockbackSpeed, knockbackDamping, hitFlashSeconds, windupSwell |

### Scripts added — `Pesky.Game`
| Path | Type | Notes |
|---|---|---|
| `Assets/Scripts/Game/WorldAuthority.cs` | `WorldAuthority` + `ISceneId` + `OptionalRefAttribute` | the netcode seam, see below |
| `Assets/Scripts/Game/GoblinBrain.cs` | `GoblinBrain` | FSM Idle / Chase / Windup / Recover / Dead |
| `Assets/Scripts/Game/RoomVolume.cs` | `RoomVolume` | room id + name, its enemies, the weapons inside it, `Wake()`, `Cleared` |
| `Assets/Scripts/Game/Door.cs` | `Door` | slides its panel on the authority's `DoorOpened` event |
| `Assets/Scripts/Game/DoorCondition.cs` | `DoorCondition` | one component, `Mode` = AlwaysOpen / RoomCleared / PlateLatched / PartyHasKey / Sealed (the five kit prefab variants) |
| `Assets/Scripts/Game/KeyPickup.cs` | `KeyPickup` | touch pickup, `keyId`, spin + bob |
| `Assets/Scripts/Game/RunePickup.cs` | `RunePickup` | hidden until `revealWhenRoomCleared.Cleared`, touch attaches its `ModifierDef` |
| `Assets/Scripts/Game/PressurePlate.cs` | `PressurePlate` | sums the mass of **resting** weapons (`restSpeed` 1.5 m/s) and reports it every FixedUpdate |
| `Assets/Scripts/Game/AnvilStation.cs` | `AnvilStation` | charges while `PlayerSoul.PossessHeld` and out of combat; 2 s = request full HP |
| `Assets/Scripts/Game/ClockMover.cs` | `ClockMover` | position is a pure function of `LevelClock.Ms`; explicit rider carry |
| `Assets/Scripts/Game/Sign.cs` | `Sign` | `[ExecuteAlways]`, world-space legacy `TextMesh` |
| `Assets/Scripts/Game/SpawnPoint.cs` | `SpawnPoint` | id + `Kind`, gizmo |
| `Assets/Scripts/Game/HudController.cs` | `HudController` | UI Toolkit view, pure reader |
| `Assets/Scripts/Game/GameBootstrap.cs` | `GameBootstrap` | Boot loads `Zone1` |
| `Assets/Scripts/Game/Dev/Zone1Probe.cs` | `Pesky.Game.Dev.Zone1Probe` | **dev only, not in any saved scene** — drives the whole run and writes a report |

### Scripts added — `Pesky.Editor`
| Path | Notes |
|---|---|
| `Assets/Scripts/Editor/SceneValidator.cs` | menu **Pesky / Validate Open Scenes** plus `public static List<string> Validate()` |

### Stage 2 scripts refactored onto the seam
- `WeaponBody` and `WeaponHomeSlot` now implement `ISceneId` (`SceneId` alongside the existing `Id`).
- `PlayerSoul`: the old `Possess` body is now **`ApplyPossess`** (authority only) and the old out-of-combat
  detach is **`ApplyRelease`** (authority only). `TryPossess()`, `Possess(w)` and `Release()` now call
  `WorldAuthority.RequestPossess` / `RequestRelease` when an authority is set and fall back to the direct path
  when it is null (so `FeelBox` still works unchanged). Added `PossessHeld` (E held, for the anvil),
  `Authority`, `SetAuthority` and `Init(OrbitCamera, WorldAuthority)`.
- `PlayerSpawner`: serialized `authority`, plus a `SoulSpawned` event so the HUD can bind to a soul created at
  runtime (Awake-time subscription, no polling).
- `OrbitCamera.target` is marked `[OptionalRef]` — it is filled at runtime, so the validator must not flag it.
- `ClockMover` bug found and fixed in play mode: setting `Rigidbody.position` already moves the Transform, so
  also adding the delta to `transform.position` **doubled** the carry (measured rider 2.661 m vs platform
  1.330 m). See "moving platform" below for the second half of that fix.

### WorldAuthority — the single place shared state changes
Serialized registries wired in the Inspector (no Find calls anywhere): `weapons[8]`, `enemies[4]`, `doors[4]`,
`plates[1]`, `keys[1]`, `runes[1]`, `anvils[1]`, `rooms[5]`. Tunables: `speedMin` 3, `speedMax` 12,
`hitCooldown` 0.35.

**Requests** (validate then apply then raise): `RequestHitEnemy(weaponId, enemyId, relativeSpeed, point)`,
`RequestDamageWeapon(weapon|id, amount, source)`, `RequestPossess(soul, weapon)`, `RequestRelease(soul)`,
`RequestBreak(weapon|id)`, `RequestPickup(pickupId, taker)` (handles both keys and runes),
`ReportPlateMass(plateId, mass)`, `RequestAnvilUse(anvilId, weapon)`, `RequestDoorCheck(door|id)` +
`EvaluateDoors()`, `SetTargeting(enemy, soul)`, `SetNearAnvil(anvil, inside)`.

**Events**: `EnemyDamaged`, `EnemyDied`, `WeaponDamaged`, `WeaponBroken`, `WeaponRespawned`, `Possessed`,
`ReleasedWeapon`, `PickupTaken`, `PlateLatched`, `DoorOpened`, `KeyGained`, `ModifierAttached`, `Healed`.

Notes for the ATCK port:
- `ImpactDamage(weaponDamage, relativeSpeed)` is pure: `damage * smoothstep(3, 12, relSpeed)`, then multiplied
  by the weapon's `DamageMultiplier`. The per-(weapon, enemy) cooldown lives in a `Dictionary<long,float>`
  keyed by `(weaponId << 32) ^ enemyId`.
- A weapon that dies from damage does **not** go through `RequestBreak`; `WeaponBody.ApplyDamage` calls
  `Break()`, which raises `WeaponBody.Broken`. The authority subscribes to every registered weapon and turns
  that into `WeaponBroken` + `ReleasedWeapon(soul, weapon, true)` using a `weapon -> soul` map it keeps on
  possess/release (the possessor is already cleared by the time `Broken` fires).
- Keys are a `HashSet<int>` on the authority, never on a weapon, so they survive death.
- `EvaluateDoors()` runs on `Start` and after `EnemyDied`, `PlateLatched` and `KeyGained`.

### Data assets
- `Assets/Data/Enemies/Goblin.asset` (`EnemyDef`) — id 1, radius 0.5, height 1.4, moveSpeed 3,
  angularSpeed 720, acceleration 20, maxHp 30, aggroRange 8, attackRange 1.4, windupSeconds 0.5,
  attackDamage 15, attackKnockback 8, attackCooldown 1.2, knockbackPerDamage 0.25, maxKnockbackSpeed 9,
  knockbackDamping 6, hitFlashSeconds 0.12, windupSwell 1.25. **Exactly section 5.**
- `Assets/Data/Modifiers/Rune_Metal.asset` (`ModifierDef`) — id 1, displayName `Rune_Metal`,
  damageMultiplier **1.25**. Created fresh; the Stage 2 `Metal.asset` was not on disk, so `FeelProbe`'s
  `testModifier` field in `FeelBox.unity` is **empty** (see "left broken").
- New physics material `Assets/Physics/P_Slick.physicMaterial` — friction 0/0, bounciness 0, combine
  **Minimum** on both. Used only on the moving platform's surface, see below.

### Prefabs added
`Assets/Prefabs/Rooms` (grey-box kit, World layer, static, P_World):

| Prefab | Contents |
|---|---|
| `DoorwayWall.prefab` | empty root + 4 cubes `Left` / `Right` / `Lintel` / `Sill`; the root sits at the doorway's floor level and the four children are overridden per instance to cut a 3 x 3.5 opening in any wall |
| `Step.prefab` | unit cube, M_Ledge |

(`Floor`, `Wall`, `Ledge` are from Stage 1.)

`Assets/Prefabs/Enemies/Goblin.prefab` — root (layer **Enemy**): `CapsuleCollider` centre (0, 0.7, 0) r 0.5
h 1.4, kinematic `Rigidbody` (no gravity, Interpolate, ContinuousSpeculative), `NavMeshAgent` (r 0.5, h 1.4,
speed 3, angular 720, accel 20, stopping 0.98), `GoblinBrain`. Child `Body`: Capsule primitive at local
(0, 0.7, 0) scale (1, 0.7, 1) = world r 0.5 h 1.4, M_Goblin, collider removed. Tinting uses a
`MaterialPropertyBlock` (`_BaseColor` / `_Color`), so no material instances are created.

`Assets/Prefabs/Kit`:

| Prefab | Contents |
|---|---|
| `Door.prefab` | root (World) + `Panel` cube 3 x 3.5 x 0.4 at local (0, 1.75, 0), M_Door, P_World; `Door` + `DoorCondition`; `openOffset` (3.1, 0, 0), `slideSeconds` 0.8 |
| `Door_AlwaysOpen` / `Door_RoomCleared` / `Door_Plate` / `Door_Key` / `Door_Sealed` | **prefab variants** of `Door.prefab`; only `DoorCondition.mode` and the panel material differ (M_Door for AlwaysOpen, M_DoorLocked for the rest) |
| `Key.prefab` | root layer **Pickup**, SphereCollider trigger r 0.6 centre (0, 0.5, 0); `Visual` = Shaft / Bow / Bit cubes in M_Key, no colliders |
| `Plate.prefab` | root layer **Trigger**, BoxCollider trigger centre (0, 0.7, 0) size (2.4, 1.4, 2.4); child `Pad` cube 2.4 x 0.15 x 2.4 on layer World with a solid collider (M_Plate, P_World); threshold 10, restSpeed 1.5 |
| `HammerStand.prefab` | root (World) + `Plinth` cube 1.6 x 0.6 x 1.0 (M_Wood, static) + child `Slot` at local (0, 0.8, 0) with a `WeaponHomeSlot` |
| `Anvil.prefab` | root layer **Trigger**, BoxCollider trigger centre (0, 1.25, 0) size (3.4, 2.5, 3.4); `Base` (M_Anvil) + `Top` (M_Metal) solid World-layer static cubes; `chargeSeconds` 2 |
| `RunePickup.prefab` | root layer **Pickup**, SphereCollider trigger r 0.7 centre (0, 0.6, 0); `Visual/Gem` cube scale 0.4 rotated (45, 0, 45) in M_Rune |
| `ClockMover.prefab` | root (World) kinematic `Rigidbody` (Interpolate) + `ClockMover`; child `Surface` cube 3 x 0.5 x 3 at local (0, -0.25, 0) so the **walking surface is the root origin**, M_Ledge, **P_Slick**; rider box centre (0, 0.7, 0) size (3.2, 1.4, 3.2), mask Weapon \| Enemy |
| `Sign.prefab` | root (World) + `Post` + `Board` (M_Wood / M_Sign, colliders removed so signs never block) + `Label` with a legacy `TextMesh` (built-in `LegacyRuntime.ttf`, characterSize 0.06, fontSize 64, middle-centre) at local (0, 1.45, -0.07) rotated (0, 180, 0) so the sign **reads from -Z** |
| `RoomVolume.prefab` | root layer **Trigger**, BoxCollider trigger, `RoomVolume` |
| `SpawnPoint.prefab` | empty + `SpawnPoint` |

### The moving platform (ClockMover) — robust rider carry
`Evaluate(ms)` is pure: `t = repeat(ms/1000 / period + phase, 1)`, triangle `1 - |2t-1|`, optional
`SmoothStep`, `Lerp(pointA, pointB, s)`. `FixedUpdate` computes the target, `MovePosition`s the kinematic body
there, and adds the same delta to every non-kinematic `Rigidbody` found by an `OverlapBoxNonAlloc` in the rider
box (velocity untouched, so a launch off the platform keeps its own arc).

Two bugs had to be fixed before the carry was exact:

1. `rb.position += delta` **and** `rb.transform.position += delta` double-moved the rider (measured 2x).
2. After removing that, PhysX friction was *also* carrying the rider on top of the explicit delta (measured
   rider 4.169 m vs platform 2.912 m). The fix is the `P_Slick` physics material (friction 0, combine Minimum)
   on the platform surface, so **all** of the carry is the deterministic explicit delta.

Measured after the fix: `platformDz = 2.444, riderDz = 2.444, carryError = 0.000` for both the Sword and the
Hammer; both stayed on top and crossed the pit.

### HUD (UI Toolkit)
- `Assets/UI/Hud.uxml`, `Assets/UI/Hud.uss`, `Assets/UI/HudPanelSettings.asset`
  (ScaleWithScreenSize, reference resolution 1920 x 1080, sortOrder 0).
- `Zone1/_UI/HUD` = `UIDocument` (panelSettings + Hud.uxml) + `HudController` (document, authority, spawner,
  style wired).
- Elements: `flash` (event banner), `key-tag`, `combat-tag` (**IN COMBAT**), `prompt` (possess / anvil),
  `anvil-track` + `anvil-fill` (hold progress), `weapon-panel` (`weapon-name`, `hp-track` / `hp-fill` with a
  `.low` red state under 33 %, `hp-text`, `modifiers`), `hint` (soul hints: free = "SPACE thrust  E possess",
  possessing = "SPACE launch  Q release").
- It only reads: `PlayerSpawner.SoulSpawned` / `LocalSoul`, `PlayerSoul.Weapon` / `Candidate` / `InCombat`,
  `WeaponBody.Hp` / `MaxHp` / `Modifiers`, `WorldAuthority.HasAnyKey` / `KeyCount` / `NearAnvil` and the
  authority events.
- Verified live: `Assets/Screenshots/Zone1_Stage3_HUD_panel.png` shows the styled HUD with "DOOR OPEN",
  KEY + IN COMBAT tags, `Sword 58 / 100` with a green bar, "no modifiers" and the hint line.

### Scenes

**`Assets/Scenes/Boot.unity`** (index 0) — `_Bootstrap` now carries `GameBootstrap` (`sceneName = "Zone1"`,
`loadOnStart = true`). Nothing else; still no camera or light. Verified on disk: its components are
`Transform, GameBootstrap` only (the dev probe is added in memory for a run and never saved).

**`Assets/Scenes/Zone1.unity`** (index 1) — 301 GameObjects, 150 static, **1 Camera, 1 AudioListener**,
9 lights (1 directional + 5 room point lights + 3 on prefabs), NavMesh baked (476 verts / 212 tris).

```
_Managers/LevelClock              LevelClock
_Managers/WorldAuthority          WorldAuthority (all 8 registries wired)
_Managers/PlayerSpawner           PlayerSpawner: soulPrefab=PlayerSoul.prefab,
                                  spawnPoints=[SoulSpawn_1, SoulSpawn_2],
                                  orbitCamera=Main Camera, authority=WorldAuthority
_Cameras/Main Camera              Camera + AudioListener + OrbitCamera (controls=PeskyControls,
                                  pivotOffset (0,1,0), minDistance 0.1)
_Lighting/Directional Light       intensity 1.3
_Lighting/Global Volume           VP_Zone1
_UI/HUD                           UIDocument + HudController
Environment                       NavMeshSurface: collectObjects=All, useGeometry=PhysicsColliders,
                                  layerMask=World(8), agentTypeID 0 — BAKED
Environment/<RoomN_Name>/{Geometry,Gameplay,Lighting,Spawns}
Hallways/Hallway_1..5/{Geometry, (Gameplay on Hallway_4)}
Shells/Room6..Room10/Geometry
```

Ambient lighting set to Flat `RGBA(0.42, 0.44, 0.50)` so the grey-box reads without a skybox.

### BUILD SHEET — final coordinates
Walls 0.5 m thick; doorways 3 wide x 3.5 high with the sill at the room's floor level; hallways 4 wide
(x -2 .. 2), 6 long, 4 high, no ceiling. **All floors are at y = 0 except Room1, whose floor is at y = -2.5 so
that its 2.5 m exit ledge tops out at exactly y = 0 and the rest of the level needs no vertical offset.**

| Room | Interior x | Interior z | Floor top y | Wall top y | Size (spec) |
|---|---|---|---|---|---|
| Room1_WeaponRoom | -8 .. 8 | 0 .. 24 | -2.5 | 7.5 | 16 x 24 x 10 |
| Hallway_1 | -2 .. 2 | 24 .. 30 | 0 | 4 | 4 x 6 x 4 |
| Room2_Goblin | -7 .. 7 | 30 .. 44 | 0 | 6 | 14 x 14 x 6 |
| Hallway_2 | -2 .. 2 | 44 .. 50 | 0 | 4 | 4 x 6 x 4 |
| Room3_KeyClimb | -7 .. 7 | 50 .. 68 | 0 | 14 | 14 x 18 x 14 |
| Hallway_3 | -2 .. 2 | 68 .. 74 | 0 | 4 | 4 x 6 x 4 |
| Room4_Plate | -6 .. 6 | 74 .. 86 | 0 | 6 | 12 x 12 x 6 |
| Hallway_4 (Key Door) | -2 .. 2 | 86 .. 92 | 0 | 4 | 4 x 6 x 4 |
| Room5_Arena | -10 .. 10 | 92 .. 112 | 0 | 8 | 20 x 20 x 8 |
| Hallway_5 (sealed) | -2 .. 2 | 112 .. 118 | 0 | 4 | shell approach |
| Shell Room6 | -6 .. 6 | 118 .. 130 | 0 | 6 | 12 x 12 x 6 |
| Shell Room7 | -6 .. 6 | 130.5 .. 142.5 | 0 | 6 | 12 x 12 x 6 |
| Shell Room8 | -6 .. 6 | 143 .. 155 | 0 | 6 | 12 x 12 x 6 |
| Shell Room9 | -6 .. 6 | 155.5 .. 167.5 | 0 | 6 | 12 x 12 x 6 |
| Shell Room10 | -6 .. 6 | 168 .. 180 | 0 | 6 | 12 x 12 x 6 |

Doorway wall centres (z): 24.25, 29.75, 44.25, 49.75, 68.25, 73.75, 86.25, 89 (inside the hallway), 91.75,
112.25, 117.75, 130.25, 142.75, 155.25, 167.75; solid end wall at 180.25.

**Room1 platforming** (coordinates are each block's top surface):

| Piece | x range | z range | Top y | Rise from |
|---|---|---|---|---|
| Floor_South | -8 .. 8 | 0 .. 12 | -2.5 | - |
| Step_Tutorial | -8 .. 8 | 9 .. 12 | -1.7 | +0.8 from the floor |
| Pit_Floor | -8 .. 8 | 12 .. 16 | -4.5 | the 4.0 m gap, 2.0 m deep |
| PitStep_1 | -8 .. -3 | 12 .. 16 | -3.5 | +1.0 out of the pit, then +1.0 to either floor |
| Floor_North | -8 .. 8 | 16 .. 24 | -2.5 | - |
| Climb_1 | 2 .. 8 | 16.5 .. 24 | -1.3 | +1.2, against the east wall |
| ExitLedge | -8 .. 2 | 21 .. 24 | **0.0** | +1.3 (2.5 m above the room floor); carries the exit doorway |

**Room3:**

| Piece | x range | z range | Top y |
|---|---|---|---|
| Floor_South | -7 .. 7 | 50 .. 54 | 0 |
| Pit_Floor | -7 .. 7 | 54 .. 60 | -3.0 (the 6 m pit) |
| PitStep_1 | 3 .. 7 | 54 .. 60 | -1.5 (steps out, against the east wall) |
| Floor_North | -7 .. 7 | 60 .. 68 | 0 |
| Ledge_A | -7 .. -2 | 60 .. 68 | 1.6 |
| Ledge_B | -7 .. -2 | 62 .. 68 | 3.2 |
| Ledge_C_Key | -7 .. -2 | 64.5 .. 68 | **5.0** (the Key ledge, NW corner) |

**Gameplay objects** (world positions; all ids unique in the scene — 40 ids, validator-checked):

| Object | Path | id | Position |
|---|---|---|---|
| WeaponRack | `Environment/Room1_WeaponRoom/Gameplay/WeaponRack` | slots 201-207 | (0, -2.5, 3) |
| Sword / Dagger / Staff / Orb / Mace / Hammer / Banana | `.../Gameplay/Weapons/<name>` | 101-107 | x = -3.75, -2.5, -1.25, 0, 1.25, 2.5, 3.75 at y -1.8, z 3 |
| SoulSpawn_1 / _2 | `Environment/Room1_WeaponRoom/Spawns` | 1201 / 1202 | (-1.5, -1, 6) / (1.5, -1, 6), yaw 180 (facing the rack) |
| Sign_1 .. Sign_4 | `.../Gameplay/Signs` | 1101-1104 | (-5.5, -2.5, 4.5) yaw 180; (5.5, -2.5, 8); (5.5, -1.7, 11.5); (-5.5, -2.5, 17) |
| RoomVolume_1 .. _5 | `.../Gameplay/RoomVolume_N` | 301-305 | origins / sizes below |
| Goblin_1 | `Environment/Room2_Goblin/Gameplay` | 501 | (0, 0, 38) |
| Door_Room2 (RoomCleared) | `Environment/Room2_Goblin/Gameplay` | 401 | (0, 0, 44.25) |
| ClockMover_A / _B / _Platform | `Environment/Room3_KeyClimb/Gameplay` | platform 1001 | (0, 0, 55.5) / (0, 0, 58.5); period 8 s, smooth |
| Key | `Environment/Room3_KeyClimb/Gameplay` | 701 (keyId 1) | (-4.5, 5.0, 66.25) |
| HammerStand + Slot | `Environment/Room4_Plate/Gameplay` | slot 208 | (-3.5, 0, 77) |
| Hammer_2 | `Environment/Room4_Plate/Gameplay` | 108 | (-3.5, 0.85, 77) |
| Plate | `Environment/Room4_Plate/Gameplay` | 601 | (0, 0, 81), threshold 10 |
| Door_Room4 (PlateLatched) | `Environment/Room4_Plate/Gameplay` | 402 | (0, 0, 86.25) |
| Door_Key (PartyHasKey, keyId 1) | `Hallways/Hallway_4/Gameplay` | 403 | (0, 0, 89), openOffset (0, -3.6, 0) — it drops into the floor because the 4 m hallway has no room for a sideways slide |
| Goblin_2 / _3 / _4 | `Environment/Room5_Arena/Gameplay` | 502 / 503 / 504 | (-6, 0, 104) / (6, 0, 104) / (0, 0, 109) |
| Anvil | `Environment/Room5_Arena/Gameplay` | 901 | (8, 0, 94) — 10.2 m from the nearest goblin, so it sits outside the 8 m aggro bubble and can be used out of combat |
| RunePickup_Metal | `Environment/Room5_Arena/Gameplay` | 801 | (0, 0, 96), revealWhenRoomCleared = RoomVolume_5 |
| Door_Sealed | `Environment/Room5_Arena/Gameplay` | 404 | (0, 0, 112.25) |

RoomVolume trigger boxes (origin, size): 301 (0, -5, 12) 16 x 12.5 x 24 · 302 (0, -0.5, 37) 14 x 6.5 x 14 ·
303 (0, -4.5, 59) 14 x 18.5 x 18 · 304 (0, -0.5, 80) 12 x 6.5 x 12 · 305 (0, -0.5, 102) 20 x 8.5 x 20.
`enemies[]`: 302 = [Goblin_1], 305 = [Goblin_2, Goblin_3, Goblin_4], the rest empty (so `Cleared` is true).

Point lights (one per room, under `<Room>/Lighting`): Room1 (0, 3, 12) r34 i9 · Room2 (0, 4, 37) r22 i7 ·
Room3 (0, 8, 59) r32 i9 · Room4 (0, 4, 80) r20 i7 · Room5 (0, 5, 102) r32 i9.

### Jump validation

**1. Ballistics from the WeaponDef numbers** (g = 20, lift clamped 15-80 deg, plus the +0.5·g·dt integrator
compensation the launch applies). The model that matters is not "max range": the weapon can stand back on the
take-off surface, so a jump is possible iff there is an angle whose near root (`xNear`, how far back you must
stand to clear the face) fits on the take-off run and whose far root (`xLand`) fits on the landing surface.

The first pass of the level failed this. With a minimum lift of 15 deg **every** launch travels forward, so at
the steepest 80 deg the Dagger still lands 3.1-3.3 m away; 0.8 m treads and 2 m ledges were unreachable for the
fast weapons. **The geometry was changed, never the feel numbers:** the four 0.5 m Room1 pit steps and the five
0.8 m Room3 pit steps were replaced by one wide wall-backed step each, the Room3 pit was made 3.0 m deep
instead of 4.0, the Room1 exit climb became two wide tiers (Climb_1 6 x 7.5 m, ExitLedge 10 x 3 m), and the
Room3 key climb became three nested blocks 5 m wide against the NW corner. A buried ledge was also caught here
(`Climb_1` had been authored at y -3.1, i.e. under the floor, instead of -1.9).

Final check, all seven weapons on every mandatory jump — **0 failures**:

| Jump | rise | run | depth | worst weapon |
|---|---|---|---|---|
| R1 floor -> Step | 0.80 | 9.0 | 3.0 | Dagger 23 deg, stand back 3.9 |
| R1 Step -> north floor over the 4.0 m pit | -0.80 (gap 4.0) | - | 8.0 | Hammer 46 deg, lands 4.83 m (margin 0.98 m) |
| R1 pit -> PitStep_1 | 1.00 | 11.0 | 5.0 | Dagger 26 deg, stand back 4.2 |
| R1 PitStep_1 -> north floor | 1.00 | 4.0 | 8.0 | Dagger 26 deg |
| R1 north floor -> Climb_1 | 1.20 | 10.0 | 6.0 | Dagger 29 deg, stand back 4.4 |
| R1 Climb_1 -> ExitLedge | 1.30 | 6.0 | 10.0 | Dagger 30 deg, stand back 4.9 |
| R3 pit -> PitStep_1 | 1.50 | 10.0 | 4.0 | Dagger 33 deg, stand back 5.0 |
| R3 PitStep_1 -> north floor | 1.50 | 6.0 | 8.0 | Dagger 33 deg |
| R3 north floor -> Ledge_A | 1.60 | 9.0 | 5.0 | Dagger 34 deg, stand back 5.1 |
| R3 Ledge_A -> Ledge_B | 1.60 | 2.0 | 6.0 | Dagger 56 deg (short run, deep landing) |
| R3 Ledge_B -> Ledge_C (Key) | 1.80 | 2.5 | 3.5 | Dagger 69 deg |

Useful number for later: a single hop of **2.5 m is UNREACHABLE for the Hammer** (max apex 1.96 m at 80 deg
lift), which is why the 2.5 m exit ledge is two tiers. Nothing in the level excludes any weapon; the Room3 key
ledge, which SLICE-1 allows to exclude Mace and Hammer, is reachable by all seven as built.

**No death pits.** The only two pits are Room1 (2.0 m deep, `PitStep_1` out, 5 m wide against the west wall)
and Room3 (3.0 m deep, `PitStep_1` out, 4 m wide against the east wall). Both are two hops from the bottom to
either floor, proven above for all seven weapons. A weapon that misses everything still gets
`WeaponBody.Recover` at y = -20.

**2. Play-mode proof.** `Zone1Probe` solves the take-off point and lift angle from the weapon's own
`launchSpeed` and the authored geometry, teleports the weapon to that take-off point, waits for it to settle
and be grounded, aims the real `OrbitCamera` and calls `PlayerSoul.TryLaunch()` — the same call the Jump action
makes. **22 of 22 jumps MADE, 0 failures**, Sword + Dagger + Hammer:

| Chain | Sword | Dagger | Hammer |
|---|---|---|---|
| R1 floor -> Step (0.8) | 80 deg, landY -1.65 (top -1.70) | 80 deg, -1.66 | 80 deg, -1.56 |
| R1 pit -> PitStep_1 (1.0) | -3.48 (top -3.50) | -3.47 | -3.31 |
| R1 floor -> Climb_1 (1.2) | -1.28 (top -1.30) | -1.28 | -1.07 |
| R1 Climb_1 -> ExitLedge (1.3) | 0.11 (top 0.00) | 0.02 | 0.16 |
| R1 Step -> across the 4.0 m pit | 61 deg, landY -2.50 at z 18.9 | 71 deg, -2.48 at z 18.2 | 46 deg, -2.40 at z 16.3 |
| R1 ExitLedge -> Hallway_1 | landY 0.03 at z 27.7 | - | - |
| R3 floor -> Ledge_A (1.6) | 3.24 | 3.21 | (not required) |
| R3 Ledge_A -> Ledge_B (1.6) | 3.21 (top 3.20) | 3.25 | (not required) |
| R3 Ledge_B -> Ledge_C (1.8) | 5.01 (top 5.00) | 5.07 | (not required) |

**The Hammer reaches the Room4 plate** (the SLICE-1 requirement): after clearing the whole Room1 chain it was
launched along +Z repeatedly — Hallway_1 (z 26) to the Room3 south floor in **7 launches** (z 55.4), carried
across the 6 m pit by the ClockMover (crossedPit = true, z 62.0), then Room3 north (z 61) to Room4 in
**5 launches** (z 84.2, y 0.19). Resting on the plate: `mass = 14.0` vs threshold 10, `latched = true`,
`Door_Room4` opened (slide 1.00). A fly-through over the plate on the way correctly did **not** latch it:
`STEP5 plate fly-through | latched=False | mass=0.0`.

### Acceptance checks — measured results

| Check | How | Result |
|---|---|---|
| Validator reports zero problems | `Pesky.Editor.SceneValidator.Validate()` on Zone1 | **PASS — 0 problems.** Negative control: setting one geometry object to layer Default and non-static made it report exactly 2 problems; restoring it returned to 0. It checks 40 unique scene ids (no duplicates, no zeros), every empty serialized reference on a `Pesky.*` component that is not `[OptionalRef]`, DoorCondition modes that need a target, geometry layer and static flags (131 geometry renderers, **131 static, 131 on layer World**), `WeaponBody`->Weapon, `GoblinBrain`->Enemy, `KeyPickup` / `RunePickup`->Pickup, `RoomVolume` / `PressurePlate` / `AnvilStation`->Trigger, RoomVolume trigger colliders, doors with no condition, ClockMovers wrongly marked static, and exactly one Camera / AudioListener / WorldAuthority / LevelClock |
| NavMesh baked | `NavMeshSurface.BuildNavMesh()` then `NavMesh.CalculateTriangulation()` | **PASS** — 476 verts / 212 tris; `NavMesh.SamplePosition` within 2 m succeeds at all four goblin spawns |
| Possess the Sword, leave Room1 | probe STEP1 + the R1 chain | **PASS** — possessed Sword (100/100 hp), 6 jumps out of the room, ended in Hallway_1 at z 27.7 |
| Kill the Room2 goblin with launches, its door opens | probe STEP3 | **PASS** — the room woke the goblin on entry, 2 launch hits killed it (30 -> 0 hp, state Dead), `EnemyDied` fired, `Door_Room2` open with slide 1.00 |
| Collect the Key in Room3 | probe STEP4 | **PASS** — climbed A / B / C, `KeyPickup.Taken = true`, `WorldAuthority.HasKey(1) = true`, `KeyGained` + `PickupTaken` fired |
| Moving platform carries a rider | probe RIDE | **PASS** — platform 2.444 m, rider 2.444 m, **carryError 0.000**, stillOnTop true, crossedPit true (Sword and Hammer) |
| Swap to the Hammer via the soul, latch the plate | probe STEP5 / STEP6 | **PASS** — out-of-combat `Release()` left the Hammer unbroken at 140 hp and freed the soul, then possessed `Hammer_2`; plate mass 14 >= 10, latched, door open |
| The Key Door opens | probe STEP6 | **PASS** — `Door_Key` (PartyHasKey) open, slide 1.00, 3 `DoorOpened` events total |
| Wake the three Room5 goblins, take damage, IN COMBAT, Release breaks the weapon | probe STEP7 | **PASS** — all three woke on entry; the weapon took 15 damage after 0.6 s (140 -> 125), `InCombat = true`, `RequestAnvilUse` was **refused while in combat**, `Release()` **broke** the weapon and freed the soul |
| Heal at the Anvil out of combat | probe STEP8 | **PASS** — `NearAnvil` = the anvil, combat cleared after 5.0 s, `RequestAnvilUse` restored 60 -> 120/120, `Healed` fired |
| Kill all three, Rune_Metal appears, damage rises | probe STEP9 | **PASS** — the rune's `IsAvailable` was false while the room was uncleared and true after (roomCleared true); touched it, `modifiers = 1`, `ModifierAttached` fired, impact damage at 12 m/s went **20.00 -> 25.00** (x1.25) |
| Die once and re-possess from the rack | probe STEP10 / STEP11 | **PASS** — the weapon broke, **modifiers lost (0)**, soul free, `TryPossess()` at the rack took the Orb, the party still held the key; the broken Mace came back **8.8 s** later (10 s timer, measured from mid-timer) at 0.00 m from its home slot with 120/120 hp |
| All 13 authority events fire | probe event counters | **PASS** — EnemyDamaged 9, EnemyDied 4, WeaponDamaged 8, WeaponBroken 2, WeaponRespawned 2, PickupTaken 2, PlateLatched 1, DoorOpened 3, KeyGained 1, ModifierAttached 1, Healed 1 |
| HUD reacts | `manage_ui get_visual_tree` + `render_ui` in play mode | **PASS** — see `Assets/Screenshots/Zone1_Stage3_HUD_panel.png` |
| EditMode tests still pass after the refactor | `run_tests` assembly `Pesky.Tests` | **PASS — 6/6**, 0.38 s |
| Console clean | `read_console` types error + warning, edit and play mode | **PASS for this stage's work.** The only entries are the same pre-existing Unity AI `NoSubscription` errors reported in Stages 1 and 2. Zero compile errors, zero project errors or warnings |
| Both scenes saved, build settings | `scene.isDirty`, `EditorBuildSettings.scenes` | **PASS** — Zone1 dirty = false, Boot dirty = false; build list is exactly `Boot` (0) and `Zone1` (1), both enabled |
| Screenshots | `Assets/Screenshots/` | `Zone1_Stage3_TopDown_3.png` (whole level), `Zone1_Stage3_Room1_top.png`, `Zone1_Stage3_Room1_1.png`, `Zone1_Stage3_Room2.png`, `Zone1_Stage3_Room3.png`, `Zone1_Stage3_Room4.png`, `Zone1_Stage3_Room5.png`, `Zone1_Stage3_HUD_panel.png`. All were looked at: the rooms chain correctly along +Z; Room1 shows rack -> step -> pit -> climb -> exit ledge; Room3 shows the three stacked ledges with the gold Key on top; Room5 shows three goblins and the sealed door |

### Left broken / stubbed / not verified
- **The Anvil's physical "hold E for 2 s" was NOT machine-verified.** The gate itself was proven through the
  authority (`RequestAnvilUse` refused in combat, accepted out of combat, full HP restored, `Healed` raised)
  and `AnvilStation` charges off `PlayerSoul.PossessHeld`, but two attempts to drive a real key hold with
  `InputSystem.QueueStateEvent(Keyboard, KeyboardState(Key.E))` produced `peakProgress = 0.00`. The Input
  System swallows synthetic device input while the Editor is unfocused and the player loop is being stepped
  manually; setting `backgroundBehavior = IgnoreFocus` and
  `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` did not help. **Test this by hand, or
  re-run `Zone1Probe` with the Unity window focused** (Stage 2's real-input test passed when the window had
  focus). The `Solve` / `Hop` machinery in the probe is otherwise reusable.
- `FeelBox.unity`'s `FeelProbe.testModifier` is **empty** — `Assets/Data/Modifiers/Metal.asset` promised by the
  Stage 2 log is not on disk. Point it at `Assets/Data/Modifiers/Rune_Metal.asset` if the probe is re-run.
- The plate's "resting" gate is a speed test (`< 1.5 m/s` inside the trigger), not a real contact test. A
  weapon wedged against the plate's side at low speed would still count.
- `Zone1Probe` teleports weapons to each take-off point rather than playing the level continuously. The
  launches themselves are real (`PlayerSoul.TryLaunch` through the OrbitCamera aim), but the route between
  jumps is not. The Hammer's Room1 -> Room4 run IS continuous launching apart from the room-to-room seams.
- The goblin knockback is applied with `NavMeshAgent.Move` (so it stays on the navmesh); it never ragdolls.
- The debris puff on break is still not made — a broken weapon just vanishes (carried over from Stage 2).
- Ball and chain: still stubbed, as the spec says.
- Shells Room6-Room10 are floor + two side walls + a doorway wall each, no ceilings and no gameplay; the
  hallways have no ceilings either (they would trap the orbit camera). Deliberate.
- `PlayerSettings.runInBackground` was turned ON. Leave it on if you want automated play-mode runs to work.
- The rune's visual is hidden only at runtime (`OnEnable`), so it is visible in the Editor scene view.

### Notes for the next stage
- **Everything that changes shared state must go through `WorldAuthority`.** Add a request method plus an
  event; never a direct call from a view. The ATCK port replaces the apply half of each request, and the
  events are the contract the HUD and the kit objects already depend on.
- New interactive objects must implement `ISceneId` with a unique id and be added to the right
  `WorldAuthority` registry array, or the validator will not see them. Current id blocks:
  101-108 weapons · 201-208 home slots · 301-305 rooms · 401-404 doors · 501-504 enemies · 601 plate ·
  701 key · 801 rune · 901 anvil · 1001 clock mover · 1101-1104 signs · 1201-1202 spawn points.
- Run **Pesky / Validate Open Scenes** (or `SceneValidator.Validate()`) after any scene edit. Mark a serialized
  reference that is legitimately filled at runtime with `[OptionalRef]`.
- Re-bake the NavMesh (`Environment`'s `NavMeshSurface`) after any geometry change.
- Level geometry rule of thumb, from the ballistics above: **a landing surface needs at least ~3.3 m of depth
  or a wall behind it**, and the take-off surface needs up to ~5 m of run for the Dagger and the Banana. Rises
  above 2.0 m need two hops for the Hammer; 2.5 m is impossible for it in one.
- Moving platforms: kinematic Rigidbody, `P_Slick` on the surface, explicit rider carry. Do not rely on
  friction, and do not move both `Rigidbody.position` and `transform.position`.
- To drive play mode with the Editor unfocused: `Time.captureDeltaTime = 0.02f` plus a loop of
  `EditorApplication.Step()` in `execute_code`, ~1500 steps per call.

---

## Stage 2 — re-run of 2026-09-18 (owner: "I can do all the play testing ... stop QA")

The workflow started Stage 2 again with notes that said `Assets/Data`, `Assets/Prefabs/{Weapons,Player,Kit}`
were empty. They are not: everything in the Stage 2 section above (and the Stage 3 refactor on top of it) is on
disk. **Nothing was rebuilt, no script, asset, prefab or scene was changed in this run** — rebuilding would
have destroyed the Stage 3 `WorldAuthority` refactor of `PlayerSoul` / `PlayerSpawner` / `WeaponBody`.

When this run connected, the Editor was **in play mode in `Assets/Scenes/Zone1.unity`** (the owner play
testing). To stay out of his way the run made only read-only calls: no play / stop, no scene switch, no script
edit (a recompile would have dropped his session), no test run.

What was checked (read-only):
- `EditorApplication.isCompiling = false`, `EditorUtility.scriptCompilationFailed = false`.
- All 11 Stage 2 types resolve: `WeaponDef`, `MovementTuning`, `LaunchAim`, `WeaponBody`, `WeaponMotor`,
  `TrajectoryPreview`, `OrbitCamera`, `PlayerSoul`, `WeaponHomeSlot`, `LevelClock`, `PlayerSpawner`.
- All 18 Stage 2 assets load: `MovementTuning.asset`, the 7 `Assets/Data/Weapons/*.asset`, the 7
  `Assets/Prefabs/Weapons/*.prefab`, `Assets/Prefabs/Player/PlayerSoul.prefab`,
  `Assets/Prefabs/Kit/WeaponRack.prefab`, `Assets/Scenes/Dev/FeelBox.unity`.
- launchSpeed / mass read back from the assets, untouched and equal to SLICE-1 section 4: Sword 12 / 3,
  Dagger 14 / 1, Staff 11 / 2.5, Orb 10 / 4, Mace 10 / 8, Hammer 9 / 14, Banana 13 / 0.5; every def's `prefab`
  reference is set.
- `FeelBox.unity` on disk holds `LevelClock`, `PlayerSpawner`, `FeelProbe`, `Main Camera`, `SoulSpawn_1`,
  `WeaponRack` and the seven weapon instances.
- Console (errors) during the owner's play session: only the pre-existing Unity AI `NoSubscription` entries.

**Skipped in this run, on the owner's instruction and because he was mid-session:** the EditMode test run and
the FeelBox play-mode smoke check. Their last recorded results are in the sections above (EditMode 6/6 after
the Stage 3 refactor; FeelBox launch / mid-air refusal / possess / release / in-combat break all PASS in the
original Stage 2 run). FeelBox has NOT been re-run since the Stage 3 refactor; it relies on the
`authority == null` direct-path fallback in `PlayerSoul`, which Stage 3 wrote but did not re-test in FeelBox.

### Every tuning knob (asset or prefab path -> field)
Edit these in the Inspector; none needs a code change.

| Where | Fields (current value) |
|---|---|
| `Assets/Data/Weapons/<Sword,Dagger,Staff,Orb,Mace,Hammer,Banana>.asset` (`WeaponDef`) | `launchSpeed`, `launchTorque` (rad/s, nose up), `mass`, `linearDrag`, `angularDrag`, `physicsMaterial`, `damage`, `maxHp`, `canRoll`, `rollTorque` (Orb only: true / 10) — per-weapon values in the Stage 2 table above. After changing mass / drag / material run the menu **Pesky / Weapons / Apply Defs To Prefabs** so the prefabs show the same numbers (runtime applies the def anyway) |
| `Assets/Data/MovementTuning.asset` (`MovementTuning`) — lift map | `minLiftDeg` 15, `maxLiftDeg` 80, `restingCameraPitchDeg` 20, `maxLiftCameraPitchDeg` -35 |
| same — launch | `launchCooldown` 0.25, `compensateIntegrator` true (+g*dt/2 on Y so the body follows the previewed parabola) |
| same — grounded / wall | `groundedNormalY` 0.6, `groundedGrace` 0.1, `groundMask` World+Weapon+Enemy, `wallNormalY` 0.3, `wallGrace` 0.15, `wallMask` World, `wallJumpsPerAirtime` 1, `wallReboundBlend` 0.5, `wallMinDot` 0.3, `wallDotMargin` 0.05 |
| same — recovery / preview | `killY` -20, `recoverLift` 0.5, `safeSpeed` 1, `gravity` 20 (preview only; real gravity is Project Settings > Physics, -20), `previewSeconds` 1, `previewPoints` 24 |
| `Assets/Prefabs/Player/PlayerSoul.prefab` (`PlayerSoul`) | `thrustAccel` 18, `maxSpeed` 7, `releaseDrag` 4, `possessRange` 2, `weaponMask` Weapon, `combatSeconds` 5, `popOutLift` 0.5 |
| same prefab, child `TrajectoryPreview` | `TrajectoryPreview.blockMask` World; `LineRenderer` width 0.06, material `Assets/Materials/M_Preview.mat` |
| `Main Camera` in each scene (`OrbitCamera`; scene override, not a prefab) | `lookSensitivity` 2, `pivotOffset` (0,1,0), `distance` 5, `minPitch` -35, `maxPitch` 75, `restingPitch` 20, `followSmoothTime` 0.08, `collisionMask` World, `collisionRadius` 0.25, `minDistance` 0.1. Set it in BOTH `FeelBox.unity` and `Zone1.unity` |
| `Assets/Input/PeskyControls.inputactions` | mouse sensitivity = the `scaleVector2` 0.06 processor on `Look` / `<Mouse>/delta` (gamepad 2) |
| `Assets/Prefabs/Weapons/*.prefab` (`WeaponBody`) | `respawnDelay` 10, `maxAngularVelocity` 25; collider shapes and child transforms set the centre of mass |
| `Assets/Physics/P_Weapon.physicMaterial`, `P_Bouncy.physicMaterial` (Banana), `P_World.physicMaterial` | friction / bounciness (values in the Stage 1 table) |

### Found in passing
- `Assets/Rune_Metal` is a stray **folder** at the Assets root (almost certainly left by the failed
  `manage_asset rename` noted in Stage 3). It was left alone; delete it in the Project window if it is empty.

---

## Stage 3 — re-run of 2026-09-18 (owner: "I can do all the play testing and give feedback. stop QA")

The workflow started Stage 3 again. **Stage 3 is already complete on disk** (the section above) and the
owner is play testing in the Editor, so nothing was rebuilt and **no Unity MCP call was made at all in this
run** — no play/stop, no scene load, no script edit (a recompile would drop his session), no validator run,
no NavMesh bake, no test run, no screenshot. The Editor was left entirely alone.

Done in this run, on disk only, under `docs/`:
- Wrote **`docs/SLICE-1-REPORT.md`**, which was the one Stage 3 deliverable genuinely missing (the Stage 3
  acceptance list never recorded it and it was not on disk). ~400 words: how to play (open `Boot.unity`,
  Play, click to lock the pointer), the control table, every tuning knob with its asset path and fields,
  and the stubbed / rough list. Its contents are drawn from the Stage 1-3 sections of this log; nothing in
  it was re-measured.

**Not re-verified in this run, on the owner's instruction:** the validator, the NavMesh bake, the Boot
play-mode smoke check, the console sweep and the top-down screenshot. Their last measured results are the
Stage 3 "Acceptance checks" table above (all PASS at the end of the original Stage 3 run). Treat them as
"last known good", not as verified today.

Open items unchanged and still waiting on the owner's feedback: FeelBox not re-run since the Stage 3
refactor; `FeelProbe.testModifier` empty (point it at `Assets/Data/Modifiers/Rune_Metal.asset`); stray
`Assets/Rune_Metal` folder at the Assets root; no debris puff on break; the Anvil's physical 2 s hold never
machine-verified.
---

## Tower build, Stage 1 of 5 — core mechanics (2026-09-19). Everything here is IMPLEMENTED, UNTESTED.

No play mode, no tests, no screenshots (owner's rule). Checks done: scripts compile with an empty error console
after every script change, `SceneValidator.Validate()` = 0 problems in `Zone1.unity` and `FeelBox.unity`, serialized
values read back. The Editor was not running when the stage started; it was launched on this project. Full rules,
tunables and wiring for every piece are in **`docs/KIT.md`** (new).

### Scripts
| File | What |
|---|---|
| `Assets/Scripts/Data/WeaponDef.cs` | + `bladed`, `blunt`, `metal`, `wooden`, `tipAxis` |
| `Assets/Scripts/Data/MovementTuning.cs` | + `stickMinSpeed` 6, `stickMaxAngleDeg` 60, `stickSinkDepth` (asset 0.1), `stickRearmSeconds` 0.3, `bladeAlignRate` 10, `bladeAlignDelay` 0.15, `bladeAlignMinSpeed` 3 |
| `Assets/Scripts/Game/MagicDoor.cs` (new) | plane-crossing sensor, `TurnTo`, `MapPosition`, `SegmentCrosses` (preview) |
| `Assets/Scripts/Game/WoodSurface.cs` (new) | marker |
| `Assets/Scripts/Game/MagnetZone.cs` (new) | pull + hold, metal only |
| `Assets/Scripts/Game/Lift.cs` (new) | N stops, pure function of the clock + (on, startMs) |
| `Assets/Scripts/Game/RiderCarry.cs` (new) | the rider fix as a shared helper; `ClockMover.CarryRiders` now calls it (same behaviour) |
| `Assets/Scripts/Game/WorldAuthorityKit.cs` (new) | `partial WorldAuthority`: arrays `magicDoors` / `magnets` / `lifts`, soul registry (`RegisterSoul`, `Souls`, `SoulOf`), `HeldKeys`, `RequestMagicDoorTraverse`, `RequestSetMagnet`, `RequestSetLift`, events `MagicDoorTraversed`, `MagnetChanged`, `LiftChanged`, `WeaponStuck`, `WeaponUnstuck`; struct `MagicDoorTraversal` |
| `WorldAuthority.cs` | now `partial`; `Awake` / `OnDestroy` call `AwakeKit` / `OnDestroyKit` |
| `WeaponBody.cs` | stick in wood (`TryStick`, `Unstick`, `IsStuck`, `StuckNormal`, events `Stuck` / `Unstuck`), `Warp`, `Knockback`, `MarkLaunched`, `SecondsSinceLaunch`, `MarkSupported`, `TipDirection`, `AlignBlade`; `IsGrounded` is true while stuck; `Teleport` / `Break` / `HoldAtHome` free a stuck weapon |
| `WeaponMotor.cs` | launch frees a stuck weapon; a launch from wood never aims into it (wall = wall-jump rebound, not counted) |
| `PlayerSoul.cs` | registers with the authority, `Warp`, turns the camera on `MagicDoorTraversed`, Release frees a stuck weapon, hands the authority to the preview |
| `OrbitCamera.cs` | `Warp(turn, from, to)`: yaw turned, smoothed pivot carried across |
| `TrajectoryPreview.cs` | arc ends at an open magic door's plane |
| `HudController.cs` | key list ("KEY 1   KEY 2"), "KEY n  NEEDED", "KEY n  TAKEN", purple flash (`magicFlashSeconds` 0.15) |
| `GoblinBrain.cs` | the hit's shove goes through `WeaponBody.Knockback` (frees a stuck weapon) |
| `Assets/Scripts/Editor/SceneValidator.cs` | MagicDoor pairing / link id / upright, MagnetZone layer + trigger, Lift not static |
| `Assets/UI/Hud.uxml`, `Hud.uss` | `magic-flash` full-screen element (edited through `manage_ui`) |

### Assets
- Materials: `Assets/Materials/M_Magic.mat` (purple, emissive x2.5), `M_Magnet.mat` (flat dark red).
- Prefabs: `Assets/Prefabs/Kit/MagicDoor.prefab` (VARIANT of `Door.prefab`: + `Frame` {JambLeft, JambRight, Lintel,
  Back, AlcoveFloor; M_Anvil, static, P_World}, `Plane` quad M_Magic no collider, `NavBlock` Not Walkable volume),
  `WoodBlock.prefab`, `MagnetZone.prefab`, `Lift.prefab`, `WeaponRack_Start.prefab` (variant of `WeaponRack`, slots
  1 / 2 / 5 only), `Assets/Prefabs/UI/HUD.prefab` (made from Zone1's HUD object so FeelBox can share it).
- `Assets/Data/Weapons/*.asset`: tags set and read back (table in KIT.md).

### Scenes
- `Assets/Scenes/Zone1.unity`: Room1 rack swapped to `WeaponRack_Start` with Replace Prefab (slot ids 201 / 202 / 205
  and the weapons' slot references kept); Room1 `Staff` 103, `Hammer` 106, `Banana` 107 deleted;
  `WorldAuthority.weapons` = Sword 101, Dagger 102, Mace 105, Hammer_2 108. Room3 `Key` keyId 1 and `Door_Key` keyId 1
  read back (already 1). `_UI/HUD` is now an instance of `HUD.prefab`. No geometry changed, NavMesh not re-baked.
- `Assets/Scenes/Dev/FeelBox.unity`: + `_Managers/WorldAuthority` (7 weapons, 2 doors, 2 magic doors, magnet, lift),
  `PlayerSpawner.authority` wired, `_UI/HUD`, and `Environment/DevBox/Gameplay/KitTest` (coordinates in KIT.md).
  FeelBox used to run on PlayerSoul's no-authority fallback; it now runs through the authority like Zone1.

### Decisions the owner should know about
- **Bladed weapons now fly point-first** (`bladeAlignRate` 10; 0 = the old tumble). Launch does not orient a weapon,
  so without this "point-first" would be luck and the Beam Climb unplayable. Only angular velocity is touched.
- MagicDoor uses a plane-crossing test on the authority's weapons and souls, not a trigger (the Soul layer collides
  with World only, and a trigger can be tunnelled).
- The magnet HOLDS a weapon in the air under it and the held weapon counts as grounded; the magnet lets go for 0.6 s
  after a launch. Room 11 can be a magnet strip (TowardPlane) over the pit.
- The Lift keeps the instructed P_Slick + explicit delta, and adds `riderBrake` because nothing settles on P_Slick.
- Prefab assembly, the rack swap and FeelBox placement were done with Editor-API calls through `execute_code`
  (PrefabUtility / SerializedObject), not by writing asset files.

### Not done / stubbed
- Nothing was run: every behaviour above is unverified at runtime, including the HUD flash and the UXML change.
- No Winch, lever or stop doors (later stages); the lift's only switch is `RequestSetLift` (FeelBox: `startsOn`).
- Cross-scene magic-door pairing is only prepared (`linkId` + registry lookup); both doors must be in one scene.
- A magic door only checks the gate of the door being entered.
- `EditMode` tests were not extended (not allowed to run them).

### Notes for the next stage
- Place a MagicDoor exactly like a Door (floor centre of the doorway, +Z into the room) and keep 2.0 m free behind
  it. Register its `Door` in `doors` as well as the `MagicDoor` in `magicDoors`.
- The Arena's `Door_Sealed` (id 404) is the door to replace with a MagicDoor whose DoorCondition is the Arena's
  condition; its twin goes in room 6.
- Lift: bottom stop needs a 0.5 m pit for a flush floor; stops are scene objects listed bottom to top.

## Tower build, Stage 2 of 5 — the physical castle layout (2026-09-19). Everything here is IMPLEMENTED, UNTESTED.

No play mode, no tests, no screenshots. Checks: scripts compile with an empty error console after every
change, `SceneValidator.Validate()` on `Zone1.unity` = **0 problems**, NavMesh re-baked, serialized values
read back. Full numbers (axes, pitch, every room's floor/height/shape/size, the door link table,
the FloorActivator table and the other three scenes) are in **`docs/CASTLE-LAYOUT-BUILD.md`** (new).
Kit rules for the new pieces are in **`docs/KIT.md`**.

### Scripts
| File | What |
|---|---|
| `Assets/Scripts/Editor/RoomShapeBuilder.cs` (new) | `Pesky.Editor`. `RoomShape` (Round, Octagon, Hexagon, Wedge, Ring, LongGallery, TallShaft, LShape, Crescent), `RoomShapeSpec`, `Doorway`, `static RoomShapeBuilder.Build(spec, out doorways)` / `Describe(spec)`, and `RoomShapeBuilderWindow` on menu **Pesky / Rooms / Room Shape Builder**. Authors a room in the open scene from prefab instances and reports where each doorway landed. |
| `Assets/Scripts/Game/FloorActivator.cs` (new) | `Pesky.Game`. Enables only the Geometry + Lighting groups of the local player's room and its door-linked neighbours; never touches Gameplay or Spawns. Driven by `WorldAuthority.MagicDoorTraversed` plus a 0.2 s `RoomVolume.ContainsPoint` poll. |

### Prefabs added — `Assets/Prefabs/Rooms`
`WallSegment.prefab`, `DoorwayWallSegment.prefab` (Left / Right / Lintel, 3.0 x 3.5 opening),
`DiscFloor.prefab` and `DiscRoof.prefab` (flattened 20-sided cylinder + MeshCollider; the roof carries
`NavMeshModifier.ignoreFromBuild`), `ShellPanel.prefab` (no collider, exterior shells).
All World layer, `P_World`, static (the roof and the shell panel without Navigation static).

### `Assets/Scenes/Zone1.unity` — still the Back Tower scene, still build index 1
* **Deleted:** `Shells/Room6..Room10` and `Hallways/Hallway_5`.
* **Rooms 1-5 untouched** (the cellars, floors at y = 0 / Room 1 at -2.5) except Room 2's east wall, which
  was cut into `Wall_East_Doorway` + `Wall_East_Base` at (7.25, 0, 37) for the magic doorway to the shaft.
* **Rooms 6-20 stacked on the tower axis x = 0, z = 102, floor pitch 12 m**, one per floor, tall shafts
  (6, 12, 17) spanning two indices at 22 m clear. Floor tops: 6 = 12, 7 = 36, 8 = 48, 9 = 60, 10 = 72,
  11 = 84, 12 = 96, 13 = 120, 14 = 132, 15 = 144, 16 = 156, 17 = 168, 18 = 192, 20 = 204. Shapes as the
  room table (Rope Room tall shaft, Barred Door wedge, Pot Room hexagon, Armoury octagon, Carry Room
  L-shape, Magnet Room long gallery, Beam Climb tall shaft, Counterweight ring, Patrol crescent, Storm
  round, Barracks L-shape, Ledge Climb tall shaft, Scales hexagon, Gate Guard round).
* **The lift shaft is one real structure** at x = 26, z = 37 (beside Room 2): `RoomA_LiftBottom`, an
  8-sided shaft 12 m across running y 0 to 192 as two wall sections (`Shell_Lower` 0-60 with the disc
  floor and the doorway to Room 2, `Shell_Upper` 60-192 with the opening into Room b).
  `RoomB_LiftStop` (wedge, y 60) is flush against its +Z wall; `Room19_ShaftTop` (ring, outer 24 /
  core 12, y 192) is the ring round the shaft head, its floor an annulus so the shaft stays open,
  capped at y 202.25.
* **`Lift`** in `RoomA_LiftBottom/Gameplay`, `Platform` id **1501**, registered in `lifts`, `clock` wired,
  `startsOn` **false**. Stops y 0.5 / 60 / 192. Instance overrides: `travelSpeed` 30, `dwellSeconds` 4,
  `Surface` 7.4 x 0.5 x 7.4, `riderBoxSize` 7.6 x 1.4 x 7.6.
* **Side rooms** `RoomC_KeyAlcove` (40, 192, 102) and `RoomD_SecretRoom` (40, 156, 102).
* **19 MagicDoor pairs = 38 doors**, link ids 1-19, MagicDoor ids 1301-1338, Door ids 404 and 406-443
  (405 was skipped: `Hallway_4/Gameplay/KeyDoorPrompt` already owns it). Path order
  5-6-7-8-9-10-11-12-13-14-15-16-17-18-19-20, plus 2 to a, 9 to b (both doors `PartyHasKey` key **2**),
  18 to c, 16 to d. All other conditions are AlwaysOpen for now. All registered in `magicDoors` and `doors`.
* Room 5's `Door_Sealed` (old id 404) was **replaced** by `MagicDoor_5_to_6`, whose own Door reuses id 404.
* Room 20's far doorway: `Door_Sealed_ToBridge` (id 442, mode Sealed) + `Sign_20_Sealed` (id 1124).
* `Room16_Barracks/Geometry/Shell/CrackedWall_Placeholder` - a plain WallSegment blocking the door to d.
  **The CrackedWall replaces it next stage.**
* **`_Managers/FloorActivator`** with 24 entries (rooms 1-20 and a-d), `spawner` and `authority` wired.
* `Environment/BackTowerShell/Geometry` - 12 `ShellPanel` at circumradius 15, y 9 to 217, plus a
  collider-disabled `ShellCap`. Visual only.
* Room ids 306-324, sign ids 1105-1124, spawn ids 1203-1221; `WorldAuthority` now holds 42 doors,
  38 magic doors, 24 rooms, 1 lift. Scene: 1696 GameObjects, 1 Camera, 1 AudioListener.
* NavMesh re-baked on `Environment`: 1623 verts / 747 tris (206 ms). Scene saved.

### New scenes — authored, saved, NOT in the build settings, NOT wired into the game
Shells only (shaped floor, walls with doorway gaps, roof, torches, a Sign with number and name); no
Camera, AudioListener, WorldAuthority or LevelClock, so Validate Open Scenes is not meaningful on them yet.
Each has `_Managers`, `_Cameras`, `_Lighting` (one Directional Light), `_UI`, `Environment/<Room>/...`.
World positions are consistent with Zone1, so opening all four additively shows the whole castle.
* `Assets/Scenes/Bridge.unity` - rooms 21-23, three 8 x 68.67 spans at z = 102, x 16 to 222, deck falling
  y 204 to 174, no roofs.
* `Assets/Scenes/MainTower.unity` - rooms 24-41 plus e, f, g on the axis x = 240, z = 102, same 12 m pitch,
  41 Tower Gate at y = 0 up to 28 Orb Room at y = 216; the bridge lands on 24 Entry Hall (y = 168).
  `MainTowerShell` at circumradius 18, y 0 to 242.
* `Assets/Scenes/Keep.unity` - rooms 42-45 abutting along x 258 to 355 at y = 0 beside the main tower's
  foot, plus `RoomH_YardPath` running x 10 to 230 at z = 40.

### Numbers that matter
Floor pitch 12 m; walls 0.5 thick; doorways 3.0 x 3.5; normal rooms 10 m clear, tall shafts 22 m;
rooms 12-26 m across. Only three crossings were authored, all checked with g = 20:
lift bottom stop 0.5 m hop (Hammer apex at 45 deg = 1.01 m, 2.0x margin), lift platform to Room 19's ring
floor 1.59 m (Hammer at 15 deg = 2.03 m, 1.28x), lift platform to Room b's threshold 0.73 m (2.8x).

### Not done / stubbed / notes for the next stage
* Nothing was run. Every behaviour above is unverified at runtime, `FloorActivator` most of all: if it
  misbehaves, set `cullFloors` = false on `_Managers/FloorActivator` to show every room again.
* The rooms are **empty shapes**. No gameplay was placed in 6-20 or a-d: no ropes, pots, magnet, beams,
  scales, pans, goblins, anvils, Staff, keys, winch or runes. The Staff, key 2 and the winch are all
  later stages, so right now links 17 (key 2) and the lift can never be opened in play.
* Torches only - the new rooms have no room point light, unlike rooms 1-5. They may read dark.
* The disc floor of a round room over-hangs its walls by 0.25 m (it has to, so MagicDoors have floor out
  to the wall's outer face). For the wedge and crescent the box/annulus floor also reaches behind the
  walls. Invisible from inside; visible in the Scene view.
* Ring and crescent floors are 12 overlapping boxes, so they have overlapping coplanar faces.
* Rooms 1-5 still carry more than 6 lights each (Room 5 has 8 torches plus a point light) from an earlier
  stage. Left alone.
* A MagicDoor's 2 m alcove overlaps the room's floor slab where they meet; both are static, so it is only
  coplanar faces, not a physics problem.
* The lift's cabin is 7.4 m square in a 12 m shaft, so there are about 1.6 m gaps at its sides. A weapon
  can fall down the shaft; it lands on Room a's floor, which has the magic doorway back to Room 2, so it
  is not a soft lock. Before the winch is turned, the parked platform plugs the shaft head at Room 19.
* Cross-scene magic doors are still not possible (both doors must be in one scene), so the Bridge,
  MainTower and Keep scenes are not linked to Zone1 yet. Room 20's far door is sealed for that reason.
* The Editor is left on `Boot.unity`.

---

## Stage 3 — Puzzle kit and goblin AI (code + prefabs + data)

Everything in this section is **implemented, untested**. Nothing was run: no play mode, no tests, no probes.
The only checks were "it compiles and the console is clean", `Pesky / Validate Open Scenes`, reading back
serialized values, and the ballistics arithmetic below. The rules for every piece are in `docs/KIT.md`.

### New scripts — `Assets/Scripts/Game` (namespace `Pesky.Game`)
| File | What it is |
|---|---|
| `Rope.cs` | Taut cord; a BLADED weapon at 5 m/s or more cuts it. Latches, records `CutMs`. |
| `DropRamp.cs` | Kinematic ramp whose pose is a pure function of (`rope.IsCut`, `rope.CutMs`, clock). Swing or Drop. |
| `Pot.cs` | Smashed only by a BLUNT weapon at 5 m/s or more. Latches; switches on its `Contents` child. |
| `ImpactLever.cs` | Any weapon impact at 4 m/s or more flips it. `latching` variant; `lift` wired = the Winch. |
| `MassPan.cs` | One kinematic pan: resting weapon mass + `MoveTo(y)` with the `RiderCarry` fix. No id. |
| `CounterweightPair.cs` | Two linked pans; the host owns one number (target offset) and the heights are a pure function of it plus the clock. |
| `ScalesLock.cs` | Three pans wanting light / medium / heavy at once; LATCHES when all three are right. |
| `LightningField.cs` | Clock-scheduled strike on the highest METAL weapon in a trigger box, with a 1 s ground glow. |
| `CrackedWall.cs` | Breaks (latched) on mass >= 8 at >= 6 m/s; lighter hits only puff. |
| `PatrolRoute.cs` | Waypoint list + gizmo. Loop / PingPong, `pauseSeconds`. No id. |
| `PorterGate.cs` | Non-latching panel that opens only for a CARRYING porter within range. |
| `GoblinBrainRoles.cs` | `partial GoblinBrain`: `Role` enum, sleep, patrol, the porter carry FSM, the shield boss, gizmos. |
| `WorldAuthorityPuzzles.cs` | `partial WorldAuthority`: 8 new registry arrays, 11 events, the request methods. |

### Changed scripts
* `WorldAuthority.cs` — `Awake` calls `AwakePuzzles()`; `RequestHitEnemy` now routes through
  `GoblinBrain.ResolveDamage` / `ApplyHit(HitOutcome, ...)` so the shield boss can filter a hit. An ordinary
  goblin has `shieldMaxHp` 0 and `bladedDamageBonus` 1, so its arithmetic is unchanged.
* `GoblinBrain.cs` — now `partial`. New states `Asleep 8, Fetch 9, Carry 10, Place 11`. `CanSee` adds the
  **110 degree view cone**. `Update` skips all perception while asleep. `TickIdle` walks a `PatrolRoute` when
  one is wired. `Perceive` calls `PorterPerceive` after the animate check. `Wake` (room entry) no longer wakes
  a sleeper. `Awake` calls `AwakeRoles`.
* `WeaponBody.cs` — `IsCarried` / `SetCarried(bool)` / `CarryTo(pos, rot)` (gravity off, velocity never
  written back, counts as GROUNDED so a launch out of the porter's hands takes), `LastImpactTime` /
  `LastImpactSpeed` recorded in `OnCollisionEnter` (what a sleeping goblin listens for), and `Break` now drops
  a carried weapon.
* `EnemyDef.cs` — new blocks: view cone (`viewConeDeg` 110, `closeSightRange` 1.5), patrol (`patrolSpeed` 1.6,
  `patrolArrive` 0.6), sleep (`wakeAnimateRange` 4, `wakeImpactRange` 8, `wakeImpactSpeed` 4,
  `wakeNeighbourRadius` 6), porter (`porterReach` 2, `porterInanimateSeconds` 2, `porterNoticeRange` 8,
  `porterCarrySpeed` 2.2, `porterPlaceSeconds` 0.6, `porterCooldownSeconds` 5), shield boss
  (`shieldMaxHp` 0, `shieldMinMass` 8, `bladedDamageBonus` 1).
* `DoorCondition.cs` — two new modes: `LeverOn = 5` (an `ImpactLever`) and `ScalesSatisfied = 6` (a
  `ScalesLock`). Both are covered by `IsMisconfigured`, so the validator catches an empty target.
* `SceneValidator.cs` — three new passes, below.

### New prefabs
`Assets/Prefabs/Kit/`: `Rope.prefab`, `DropRamp.prefab`, `Pot.prefab`, `ImpactLever.prefab`, `Winch.prefab`
(variant of ImpactLever, plus a drum), `CounterweightPair.prefab`, `ScalesLock.prefab`,
`LightningField.prefab`, `CrackedWall.prefab`, `PorterGate.prefab`, `PatrolRoute.prefab`,
`Door_Lever.prefab` and `Door_Scales.prefab` (variants of `Door.prefab`).
`Assets/Prefabs/Enemies/`: `GoblinSleeper.prefab` (variant, `startAsleep`), `GoblinPorter.prefab` (variant,
role Porter, `CarrySocket` child at (0,2.05,0.35)), `GoblinBoss.prefab` (variant, role ShieldBoss, body 1.8x,
`Shield` plate, a second `ShieldBar`, capsule r 0.9 h 2.6).

### New data and materials
`Assets/Data/Enemies/GoblinPorter.asset` (id 2) and `GoblinBoss.asset` (id 3: shield 120, min mass 8, blade
bonus 1.5, body 140 HP, attack 30, windup 0.9, reach 3.4, cone 140).
`Assets/Materials/`: `M_Rope`, `M_Pot`, `M_Cracked`, `M_Glow` (emissive), `M_Shield`. URP Lit, flat colour.

### Id blocks added
1601+ ropes, 1701+ pots, 1801+ levers and winches, 1901+ counterweight pairs, 2001+ scales locks,
2101+ lightning fields, 2201+ cracked walls, 2301+ porter gates. `MassPan`, `DropRamp` and `PatrolRoute`
carry no id — the piece that owns them does.

### Validator, extended
`Pesky / Validate Open Scenes` gained three passes on top of the old four:
* `CheckPuzzleKit` — layers (LightningField and MassPan on Trigger); NOT-static for everything that moves or
  disappears (DropRamp, MassPan, Rope, Pot, CrackedWall, PorterGate, ImpactLever); a **solid** collider on the
  same object for every piece that reacts to being hit and a **trigger** collider for every sensing volume;
  the Rope to DropRamp back-reference; a winch that is not latching; a CounterweightPair with the same pan
  twice; a ScalesLock that is not exactly 3 distinct pans; a PorterGate with no porters.
* `CheckEnemyRoles` — a Porter without `carrySocket` / `dropPoint` / `patrol`, a ShieldBoss whose def has
  `shieldMaxHp` 0, a sleeper with no RoomVolume.
* `CheckRegistry` — **every** `WeaponBody`, `GoblinBrain`, `Door`, `PressurePlate`, `KeyPickup`, `RunePickup`,
  `AnvilStation`, `RoomVolume`, `MagicDoor`, `MagnetZone`, `Lift`, `Rope`, `Pot`, `ImpactLever`,
  `CounterweightPair`, `ScalesLock`, `LightningField`, `CrackedWall` and `PorterGate` in the open scenes must
  appear in one of the WorldAuthority's serialized arrays. An unregistered piece looks fine in the Inspector
  and silently does nothing at runtime; this is what catches it.
Zone1 and FeelBox both validate **0 problems** with all of this on.

### Test placement — `Assets/Scenes/Dev/FeelBox.unity` only
Under `Environment/DevBox/Gameplay/KitTest`: `DropRamp` hinge (-6,5,4) with `Rope` at (-6,5,11.5);
`Pot` (1701) at (3,0,10) hiding a non-latching `ImpactLever` (1801); `Winch` (1802) at (-7,0,-11) wired to the
existing `Lift` (1501) — **that lift's `startsOn` was turned off** so the winch has something to do;
`CounterweightPair` (1901) at (7,0,7) with `travel` 1.5 and pans at y 1.5; `ScalesLock` (2001) at (-6,0,-6);
`LightningField` (2101) at (0,0,-10); `CrackedWall` (2201) at (-11,0,1). All wired to FeelBox's
`WorldAuthority` and `LevelClock` and registered. Scene saved.

### Arithmetic (g = 20, `range = v^2 sin 2a / g`, `apex = (v sin a)^2 / 2g`, 20 percent margin)
* Apex at 80 degrees, the highest a weapon can place itself: Dagger 4.75, Banana 4.10, Sword 3.49,
  Staff 2.93, Orb 2.42, Mace 2.42, Hammer 1.96 m. **No pan or ledge may sit higher than the weakest weapon
  that has to reach it, less 20 percent.** FeelBox's counterweight pans at 1.5 m and the ScalesLock's pans at
  1.0 m clear the Hammer (1.96 x 0.8 = 1.57). The `CounterweightPair` prefab's default pans at 3.0 m do
  **not**: only Sword, Dagger and Banana can land there. That is fine for Room 13 (you walk a pan down to the
  floor first) but it must be re-checked when the room is built.
* Horizontal launch speed at the 15 degree minimum lift is `v cos 15`: Hammer 8.69, Mace 9.66, Orb 9.66,
  Staff 10.6, Sword 11.6, Banana 12.6, Dagger 13.5 m/s. Every impact threshold in the kit clears that with
  margin: CrackedWall 6 (Mace 1.61x, Hammer 1.45x), Rope 5 (Sword 2.3x, Dagger 2.7x), Pot 5 (Mace 1.9x,
  Hammer 1.7x, Staff 2.1x), ImpactLever 4 (every weapon, 2.2x or better).
* Lightning 40 damage against metal HP: Dagger 70 survives one strike and breaks on the second, Sword 100
  takes three, Mace 120 three, Hammer 140 four. Staff, Banana and Orb are never hit.
* Counterweight geometry rule: author the pans at `y = travel` so the LOADED pan comes to rest at floor level.
  Nothing can then be stranded on a pan, and no pit is created.

### Not done / stubbed / notes for the next stage
* **Nothing was run.** Play mode was never entered; no tests, no screenshots, no runtime measurement.
* **The view cone changes goblins that already exist.** `Assets/Data/Enemies/Goblin.asset` picked up the new
  defaults (`viewConeDeg` 110, `closeSightRange` 1.5, `shieldMaxHp` 0, `bladedDamageBonus` 1), so the goblins
  in rooms 2 and 5 now only notice what is in front of them. That is the intent, but it makes them easier;
  raise `viewConeDeg` on `Goblin.asset` towards 360 to get the old all-round awareness back.
* **No rooms were furnished** — that is stage 4. Rooms 6-20 and a-d are still empty shapes.
  `Room16_Barracks/Geometry/Shell/CrackedWall_Placeholder` is **still the placeholder**: swap in
  `CrackedWall.prefab` (World layer, NOT static, under `Gameplay`, id 2201+, registered in `crackedWalls`)
  when Room 16 is built. Room 19's winch: place `Winch.prefab` and wire `lift` to the Lift with id 1501.
  Key 2 in Room c opens link 17 (doors 436/437).
* **The goblin variants are placed nowhere.** FeelBox has no NavMeshSurface and no RoomVolume, and patrol,
  sleep and porter all need both, so they cannot be tried until a room is furnished and `Environment`'s
  NavMeshSurface is re-baked.
* A `PorterGate` panel does not stop goblins: they move with `NavMeshAgent.Move`, which ignores physics
  colliders. It stops weapons and souls, which is what the puzzle needs, but do not use one as a general
  goblin barrier.
* `DropRamp` does not carry riders. It is a kinematic body that shoves what is in its way as it falls, so a
  weapon standing on a ramp while it drops is pushed, not carried smoothly.
* Patrol and the porter use `NavMeshAgent` pathing, so they are NOT a pure function of `LevelClock` the way
  the pans, the ramp and the lightning are. Enemies stay host-authoritative for the netcode port.
* A launch out of a porter's hands costs one physics step: the porter sees `IsAnimate` on the next
  `FixedUpdate` and lets go then. The launch velocity itself survives.
* The lightning bolt and ground glow are driven locally from each client's own clock evaluation, not from the
  `LightningStruck` event. They agree as long as the clock does; if the netcode port re-bases the clock
  mid-strike a client may skip a cycle's visual.
* `ScalesLock.Fits` treats an EMPTY light pan as unsatisfied (`mass > 0`), so all three pans must actually
  hold something.
* FeelBox's `Lift` no longer starts on (the winch starts it). Set `startsOn` back if you want the old
  behaviour.
* The `Contents` child of `Pot.prefab` is the parent for whatever hides inside; it starts inactive. Anything
  under it (a lever, a `Key.prefab`) keeps its own id and must still be registered with the authority.
* The Editor is left on `Boot.unity`.


---

## Tower build, Stage 4 of 5 — furnishing rooms 6-12 and the lift stops a, b (2026-09-19)

Everything in this section is **implemented, untested**. Nothing was run: no play mode, no tests, no
probes, no screenshots. The only checks were `Pesky / Validate Open Scenes` (**0 problems**, run three
times), a NavMesh re-bake, reading serialized values back out of the scene, and the ballistics below.
All work was done through the MCP with editor automation (`execute_code`) placing prefab instances; no
asset file was hand-written. Scene: `Assets/Scenes/Zone1.unity`, saved. Per-room test steps for the
owner are in `docs/BACK-TOWER-TEST-CHECKLIST.md`.

### Room 6 `Room06_RopeRoom` — the rope and the drop-bridge (floor y 12, 8-sided, 16 across)
The disc `Floor` was **deleted** and replaced by a boxed floor with a 7.0 m chasm across the room:

| Object (`Geometry/Shell`) | Prefab | Position | Scale | Spans |
|---|---|---|---|---|
| `Floor_Near` | Floor | (0, 10.5, 96.43) | 17 x 3 x 4.14 | y 9-12, z 94.36-98.50 |
| `Floor_Far` | Floor | (0, 10.5, 107.57) | 17 x 3 x 4.14 | y 9-12, z 105.50-109.64 |
| `Pit_Floor` | Floor | (0, 8.75, 102) | 17 x 0.5 x 7 | top y 9.0 (Room 5's roof ends at 8.5) |
| `Pit_Wall_W` / `_E` | WallSegment | (-8.25 / 8.25, 10.5, 102) | 0.5 x 3 x 7 | closes the pit's sides |
| `Pit_Step` | Ledge | (0, 9.75, 99.25) | 16 x 1.5 x 1.5 | top y 10.5, the way out |

`Gameplay/DropRamp_06` at (0, **16.8**, 98.5), `mode` **Drop**, `dropOffset` (0,-5,0), `fallSeconds` 1.2;
`Deck` resized to local (0,0,3.5) scale 4 x 0.4 x **7**. Landed it sits at y 11.8 so the deck's top is
flush at y 12.0 and it spans z 98.5-105.5 exactly lip to lip.
`Gameplay/Rope_06` (id **1601**) at (0, 12, 98.35), root scale y **1.6** so the cord runs y 12.0 to 16.8
from the floor up to the deck's near end. Wired `ramp` and `rope` both ways, plus `authority`, `clock`.
`Stand_Dagger_06` (`HammerStand`, slot id **211**) at (-3.5, 12, 97.4) with `Dagger_06` (weapon id **111**).
Signs **1160** "ONLY A BLADE CUTS THE ROPE / Q TO LEAVE, E TO TAKE THE DAGGER" and **1161** "THE BRIDGE
STAYS DOWN". One extra torch in the pit (5 lights). `RoomVolume_06` grown to centre (0,9.75,0) size
17 x 25.5 x 17 so the pit is inside the room (world y 9.0-34.5).

### Room 7 `Room07_BarredDoor` — connector (floor y 36, wedge)
`Gameplay/Door_TowerBarred`: `Door_Sealed` variant, **Door id 444**, mode `Sealed`, at (-6, 36, 108.55)
yaw 180 on the wide wall beside the magic doorway; three `WallSegment` bars (3.3 x 0.16 x 0.16) at
y 36.5 / 37.5 / 38.5. Signs **1162** "TOWER DOOR / BARRED FROM OUTSIDE" and **1163** "UP THE TOWER /
THE ONLY WAY ON" (beside `MagicDoor_7_to_8`, the first obvious way up). One duplicate torch left over
from stage 2 was deleted and two were added (4 lights).

### Room 8 `Room08_PotRoom` — pots and the hidden lever (floor y 48, hexagon 18)
Five `Pot` instances (ids **1701-1705**) at (-4.5,102.5), (-1.5,99.5), (1.5,103.5), (4.5,100.5), (0,106).
`Pot_04` (id 1704) hides `Lever_08`, an `ImpactLever` id **1801**, `latching` = true, under its `Contents`
child at local (0,-0.2,0). `MagicDoor_8_to_9`'s own `Door` (id 410) is now `DoorCondition.Mode.LeverOn`
pointing at that lever — **the first real door condition in the tower**. `LeverDoorPrompt_08` (DoorPrompt
id **451**, Trigger layer, 3.6 x 3.2 x 2.6) at (0, 49.6, 108.4).
`Stand_Mace_08` (slot **212**) at (-4, 48, 97) with `Mace_08` (weapon **112**) — the blunt weapon the room
needs, so the room is self-contained. Signs **1164** "POTS ARE THICK / ONLY BLUNT SMASHES THEM" and
**1165** "A LEVER OPENS THIS DOOR / IT IS IN ONE OF THE POTS". 5 lights.
Note: only the door being ENTERED has to be open, so the shut door 410 blocks 8 to 9 while the twin
(411, AlwaysOpen) still lets you come back 9 to 8. No soft lock.

### Room 9 `Room09_Armoury` — rest (floor y 60, octagon 20)
`Anvil_09` (`AnvilStation` id **902**) at (-5, 60, 99). `Stand_Staff_09` (slot **213**) at (-5, 60, 105)
with `Staff_09` (weapon **113**) — the Staff's home slot, as the room table asks. `KeyDoorPrompt_09`
(DoorPrompt id **452**) at (8, 61.6, 102) in front of `MagicDoor_9_to_B` (key 2). Signs **1166** "ANVIL /
HOLD E, OUT OF COMBAT" and **1167** "STAFF: WOODEN / NO MAGNET, NO LIGHTNING". 6 lights.

### Room 10 `Room10_CarryRoom` — the porter (floor y 72, L-shape)
`Geometry/Shell/Wall_Gate`: a `DoorwayWallSegment` at (-6.5, 72, 104) across the west arm, resized to a
**9 m long, 7.5 m tall** wall (`Left`/`Right` 3 x 7.5 x 0.5 at local x -3 / +3, `Lintel` 3 x 4 x 0.5 at
local y 5.5) with the standard 3 x 3.5 opening.
`Gameplay/PorterGate_10` (id **2301**) at the same transform, `openOffset` (0,3.6,0) so the open panel
hides inside the wall. Its `Panel` child carries a **`NavMeshModifier` with `ignoreFromBuild`** so the
bake does not cut the porter's own path through the gate.
Far-side return stair (`Back_Step_1..4`, Ledge, x -10.75 to -8.0): tops at y 73.5 / 75.0 / 76.5 / 78.0,
1.5 m risers, 1.5 m treads (the last 0.75 m, hard against the wall). The near face is a sheer 7.5 m, so
the stair is a one-way way back.
`Stand_Drop_10` (`HammerStand`, slot **214**, no weapon) at (-9, 72, 110.8) — its `Slot` is the porter's
`dropPoint`. `PatrolRoute_Porter` (PingPong, pause 1 s): (4,95.5), (-6.5,95.5), (-6.5,101.5), (-8,110);
the last point is 1.39 m from the drop slot, inside `porterReach` 2.
`GoblinPorter_10` (enemy id **511**) at (-6.5,72,96), wired `authority` / `room` / `patrol` / `dropPoint`.
`Goblin_Patrol_10` (enemy id **512**) at (-3.5,72,108) on `PatrolRoute_Guard` (Loop, pause 1.5 s) north of
the gate. Both are in `RoomVolume_10.enemies` and `WorldAuthority.enemies`.
Signs **1168** "GOBLINS IGNORE A WEAPON / THAT LIES STILL", **1169** "THE PORTER OPENS ITS OWN GATE /
PLAY DEAD AND BE CARRIED", **1170** "STEPS OVER THE WALL / THE WAY BACK". 6 lights.

### Room 11 `Room11_MagnetRoom` — the magnet strip (floor y 84, gallery 12 x 30)
No floor surgery: the walking level is **raised to y 88** at both ends and the original floor is the
"pit" 4 m below.

| Object (`Geometry/Shell`) | Position | Scale | Spans |
|---|---|---|---|
| `Step_In_1` / `Step_In_2` | (0,84.75,88.6) / (0,85.5,89.8) | 12.5 x 1.5 x 1.2 / 12.5 x 3 x 1.2 | tops y 85.5, 87.0 |
| `Deck_Entry` | (0,86,93.2) | 12.5 x 4 x 5.6 | top y 88, z 90.4-96.0 |
| `Deck_Exit` | (0,86,110.8) | 12.5 x 4 x 5.6 | top y 88, z 108.0-113.6 |
| `Step_Out_2` / `Step_Out_1` | (0,85.5,114.2) / (0,84.75,115.4) | as above | down to the exit door |
| `Stone_1` / `Stone_2` | (4.4,86,99.67) / (4.4,86,104.33) | 1.6 x 4 x 2 | the Staff's route, tops y 88 |
| `PitStep_1` / `PitStep_2` | (-3.625,84.75,98.4) / (-3.625,85.5,97.2) | 5.25 x 1.5 x 1.2 / 5.25 x 3 x 1.2 | the way out of the pit, back to `Deck_Entry` only |

`Gameplay/MagnetZone_11` (id **1401**) at (-1.5, 92.5, 102), `mode` **TowardPlane**, `Block` scaled
3 x 0.5 x 12, `OnGlow` 2.6 x 0.04 x 11.6, trigger box centre (0,-4,0) size 9 x 12 x 12 (world x -6 to 3,
y 82.5 to 94.5, z 96 to 108), `Target` at (-1.5, **90.8**, 102) — 2.8 m over the decks.
`Stand_Sword_11` (slot **215**) at (-4, 88, 93) on the entry deck with `Sword_11` (weapon **115**), so a
metal weapon is always available. Signs **1171** "THE MAGNET LIFTS METAL / AND CARRIES IT ACROSS" and
**1172** "WOOD GETS NO LIFT / THE STAFF TAKES THE STONES". Two duplicate torches from stage 2 deleted,
two added (4 lights).

### Room 12 `Room12_BeamClimb` — the post climb (floor y 96, 8-sided shaft, 22 m tall)
**The exit doorway was moved from y 96 to y 108** so the climb has somewhere to go.
`Geometry/Shell/Wall_0_Doorway`: `Lintel` became the sill (local y 7.75, scale 3 x 8.5 x 0.5, world
y 99.5-108); new children `Fill_Low` (3 x 3.5 x 0.5 at y 97.75, fills the old opening) and `Lintel_High`
(3 x 6.5 x 0.5 at y 114.75). `Gameplay/MagicDoor_12_to_13` (MagicDoor 1315 / Door 418) moved to
(0, **108**, 109.391); its twin in Room 13 was **not** moved — a magic door pair does not need matching
heights.
Four `WoodBlock` posts (`Post_N/E/S/W`) 0.9 x 14 x 0.9 at radius 3.2 from the shaft centre, y 96-110:
(0,105.2), (3.2,102), (0,98.8), (-3.2,102). `Ledge_Exit` (5 x 0.5 x 3.1) at (0,107.75,107.95), top y 108,
in front of the door. `Stand_Dagger_12` (slot **216**) at (-3.5, 96, 98) with `Dagger_12` (weapon **116**).
Sign **1173** "A BLADE STICKS IN WOOD / LAUNCH OFF IT, POST TO POST". Two high torches added (6 lights).

### Rooms a and b — lift landings
`RoomA_LiftBottom/Geometry/Shell_Lower/Landing_A` (Ledge 1.5 x 0.5 x 4) at (21.45, 0.25, 37): fills the
gap between the magic doorway's threshold and the lift platform's west edge (x 22.3) at `Stop_0` (y 0.5),
without ever touching the platform's travel. Sign **1174** "THE LIFT IS DEAD / UNTIL THE WINCH AT THE TOP".
`RoomB_LiftStop/Geometry/Shell/Landing_B` (Ledge 3.5 x 0.5 x 1.27) at (26, 59.75, 41.435): top y 60,
z 40.80-42.07, bridging the platform's north edge (z 40.7) to the existing `Threshold` (z 42.04-43.04) at
`Stop_1` (y 60) — a 0.1 m step instead of a 1.34 m jump. `KeyDoorPrompt_B` (DoorPrompt id **453**) at
(26, 61.6, 50.7) in front of `MagicDoor_B_to_9`. Sign **1175** "KEY 2 OPENS THIS DOOR / TO THE ARMOURY".
One torch added to Room b (3 lights). Room a already had 6 and was left alone.

### Ids used this stage
Weapons **111** Dagger_06, **112** Mace_08, **113** Staff_09, **115** Sword_11, **116** Dagger_12.
Home slots **211-216** (214 is the porter's empty drop stand). Enemies **511** porter, **512** patrol
goblin. Door **444** (the barred tower door). DoorPrompts **451, 452, 453**. Anvil **902**.
Signs **1160-1175**. Rope **1601**. Pots **1701-1705**. Lever **1801**. Magnet **1401**. PorterGate **2301**.
All of them are registered in the matching `WorldAuthority` array (weapons 9, enemies 6, doors 43,
anvils 2, ropes 1, pots 5, levers 1, magnets 1, porterGates 1).

### Weapon availability (no soft locks)
The cellars give Sword, Dagger, Mace (`WeaponRack_Start`) and a Hammer (Room 4). On top of that every
room that needs a particular kind of weapon now has one on a home slot **in that room**: Room 6 a Dagger
(bladed), Room 8 a Mace (blunt), Room 9 the Staff, Room 11 a Sword (metal), Room 12 a Dagger (bladed).
A broken weapon comes back on its own slot after 10 s, so a room can never be left without its tool.

### Arithmetic checked (g = 20, `range = v^2 sin 2a / g`, `apex = (v sin a)^2 / 2g`, 20 percent margin)
Launch speeds: Dagger 14, Banana 13, Sword 12, Staff 11, Orb 10, Mace 10, Hammer 9.
Best flat range (45 deg) x 0.8: Dagger 7.84, Banana 6.76, Sword 5.76, Staff 4.84, Mace/Orb 4.00, Hammer 3.24.
Apex at 80 deg x 0.8: Dagger 3.80, Banana 3.28, Sword 2.79, Staff 2.34, Mace/Orb 1.94, Hammer 1.57.

| Crossing | Needed | Worst weapon that must make it | Margin |
|---|---|---|---|
| Room 6 pit, y 9 to step y 10.5 to floor y 12 | 1.5 m rises, wall-backed | Hammer apex 1.96 | 1.31x |
| Room 6 chasm **without** the bridge, 7.0 m flat | 7.0 m | intentionally only the Dagger (7.84) | everyone else is short |
| Room 6 rope cut, cord y 12.0-16.8 | 5 m/s at the contact | Sword at 2.5 m up = 6.63 m/s | 1.33x |
| Room 6 held deck at y 16.6 (must be unreachable) | 4.6 m of rise onto a 0.4 m deck | Dagger apex 4.75 arrives at zero speed | cannot be landed on |
| Room 10 return stair, 4 x 1.5 m risers | 1.5 m | Hammer apex 1.96 | 1.31x |
| Room 10 last hop over the 7.5 m wall from the 6.0 m step | 1.5 m in 1.0 m of run | Hammer at 80 deg: 1.96 m up in 0.69 m | 1.31x |
| Room 11 steps up to the decks, 1.5 / 1.5 / 1.0 | 1.5 m | Hammer apex 1.96 | 1.31x |
| Room 11 stone route, 2.67 m gaps at one level | 2.67 m | Hammer 3.24 | 1.21x |
| Room 11 magnet crossing, 12 m | 3 hops of `v cos15 * 0.6` | Hammer 5.2 m a hop | 1.30x |
| Room 11 deck face from the pit (meant to be hard) | 4.0 m sheer | only the Dagger (apex 4.75) | everyone else walks back to the entry steps |
| Room 12 post to post, 4.53 m across and 2.0 m up | `x_max = (v/g) sqrt(v^2-2gh)` | Dagger 7.54 m | 1.66x (Sword 4.80 = 1.06x, so it is Dagger-only) |
| Room 12 stick speed on arrival (needs 6 m/s) | 6 m/s | Dagger at 40 deg arrives at 10.8 m/s | 1.80x |
| Room 12 last hop, post y 106 to `Ledge_Exit` y 108 | 2.0 m up, 0.75 m across | Dagger apex 4.75 | 2.4x |
| Room a, shaft floor to `Landing_A` / `Stop_0` | 0.5 m | Hammer apex at 45 deg = 1.01 | 2.0x |
| Room b, platform edge to `Landing_B` | 0.1 m step | every weapon | trivial |

### Kit / prefab changes
`Assets/Prefabs/Kit/Sign.prefab`: the `Label` child was on the **+Z** side of the board (local z +0.06)
while facing -Z, so every sign in the game read through its own board. It is now at local z **-0.06**.
The readable face of a Sign is its local **-Z**; point its +Z at the wall behind it.
Side effect worth knowing: re-saving the prefab asset **cleared the GameObject-name override on the sign
instances created earlier in the same session** (they all went back to "Sign"); they were renamed again
afterwards. Watch for that if a kit prefab is ever re-saved mid-session.

### Not done / stubbed / notes for the next stage
* **Nothing was run.** No play mode, no tests, no screenshots, no runtime measurement. Everything above
  is arithmetic, serialized read-back and the validator.
* **The porter's NavMesh path through its own gate is unverified.** The `NavMeshModifier` is on the panel
  and the surface was re-baked (2156 verts / 990 tris, 201 ms), but whether the agent actually walks the
  route is a play-mode question. If it stalls at the gate, check the NavMesh under (-6.5, 72, 104).
* **Key 2 does not exist yet** (it lives in Room c, stage 5), so `MagicDoor_9_to_B` / `MagicDoor_B_to_9`
  (doors 436 / 437) can never open in this build, and the lift is still dead until the Room 19 winch.
  Room b is reachable only by that door, so today it can only be looked at from the shaft.
* **Known bypasses, left in on purpose.** A Dagger can clear Room 6's 7.0 m chasm without the bridge, and
  can climb Room 11's 4 m deck face out of the pit. Both are the "blade" answer anyway and neither is a
  soft lock. Nothing else has the range.
* `Goblin_Patrol_10` can walk through the shut `PorterGate` like any goblin (the stage-3 caveat): it is a
  weapon-and-soul barrier only.
* Room 10's guard and porter share a room volume, so killing both marks Room 10 cleared. No door depends
  on that.
* Rooms 1-5 still carry 7-9 lights each from an earlier stage; every room furnished this stage is at 6 or
  fewer. Two duplicate torches in Room 11 and one in Room 7 (stage-2 leftovers) were deleted.
* Rooms **13-20, c and d are still empty shapes** — that is stage 5, along with the Room 16 cracked-wall
  placeholder, the Room 19 winch (wire `lift` to Lift id 1501) and key 2 in Room c.
* The Editor is left with `Zone1.unity` open and saved.

---

## Netcode port, Stage 1 of 7 — the engine is over and it compiles (2026-09-20). IMPLEMENTED, UNTESTED.

Full record, with the file-by-file table and every seam left open, is in `docs/NETCODE-STATUS.md`.
Plan: `docs/NETCODE-PORT.md`. Summary only here.

Brought ATCK's multiplayer engine into the project as four new assemblies. Every C# file was copied
**inside the Editor** with `execute_code` (`File.Copy` / read-replace-write, `ATCK` → `Pesky`, then
`AssetDatabase.Refresh`); Unity generated all `.meta` files. No ported file was retyped through the
model. The "adapt" files were then cut with `apply_text_edits`; the four genuinely new files were
written with `create_script`.

* **New asmdefs**: `Pesky.Protocol` (no refs), `Pesky.Transport` (no refs), `Pesky.Sim` (Data, Protocol),
  `Pesky.Session` (Data, Protocol, Transport, Sim). `Pesky.Game` gained Session, Sim, Protocol and
  **never** Transport; `Pesky.Editor` and `Pesky.Tests` gained all five.
* **New folders**: `Assets/Scripts/{Protocol,Protocol/Messages,Transport,Sim,Session,Session/Rules}`,
  `Assets/Plugins/WebGL`, `Assets/WebGLTemplates/Pesky`.
* **Copied verbatim**: the five Transport files (WebRTC + loopback + voice interfaces),
  `Tick`/`NetWriter`/`NetReader`/`Frame`/`Appearance`/`M0Messages`, `Rng`/`SimHash`, and the
  Session engine — `Inbox`, `InboxItem`, `InboxKind`, `Outbox`, `PendingEvents`, `PeerSlots`,
  `RoomClock`, `ClockPinger`, `SnapshotReceiver`, `SessionRouter`, `TransportFactory`,
  `NetSessionExtensions`, `IHostRule`, `IIntentValidator`, `Rules/ClockRule`.
* **Adapted (ATCK gameplay cut out)**: `Wire` (`NoCompany` gone, **ProtocolVersion reset to 1**),
  `Headers` (`AircraftHeader` gone), `MsgId` (0x20-0x7E emptied), `MessageInfo` (ten session rows +
  FRAME), `Enums` (~30 ATCK enums deleted, six rewritten for Pesky), `SessionMessages`
  (`SESSION_INFO` and `SESSION_PHASE` now carry a `floorId` instead of the airfield and day clock;
  the other eight messages byte for byte), `NetSession` (save/resume, shift/hazard/modifiers and
  airport naming cut), `JoinFlow` (resume and economy cut, `BuildSessionInfo` rewritten),
  `MessageApplier` (five cases; `TryEventTick` is now a fixed offset of 1), `EventSink`
  (aircraft header helpers gone), `HostAuthority` (`CreateDefault` is the clock and nothing else),
  `HostIds` (`NextEnemy`/`NextPickup`/`NextDoor`), `SnapshotCodec` (`Order` = World, Players).
* **Written fresh**: `Scripts/Sim/WorldSim.cs` (a players table, the session header and the two
  snapshot parts — nothing else), `Scripts/Sim/PlayerState.cs`, `Scripts/Sim/PlayerTable.cs`,
  `Scripts/Data/GameData.cs`.
* **Browser bridge**: `AHNet.jslib` verbatim; `ATCKPlatform.jslib` → `PeskyPlatform.jslib` and
  `keys.js` renamed as a pair (dormant, nothing imports them yet); `trystero.min.js` verbatim;
  `net.js` and `index.html` retitled. Own identity: trystero `appId` **`pesky-weapons-9d4kv2`**,
  room prefix **`PESKY-`**. The three magic names — GameObject `NetBridge`, `window.AH_Net`,
  `window.AH_UnityInstance` — are unchanged and consistent across C#, jslib and net.js.
  `net.js`'s `iceServers()` now actually reads `window.PESKY_ICE_SERVERS` from `index.html`
  (ATCK defined that global and ignored it), so TURN can be pasted in without a rebuild.

### Not done / notes for the next stage
* **Nothing was run.** No play mode, no tests, no browser, no build. The only check is that the whole
  project compiles with no CS errors in the console. Everything is implemented, untested.
* **Nothing is wired into gameplay.** No scene, prefab, material or existing `Scripts/Game` file was
  touched. `Zone1.unity` was open and clean before and after; nothing was saved over it. The build
  target was not switched and `PlayerSettings.WebGL.template` was not set.
* **`TRANSFORM` (0x01) is still ATCK's 19-byte position + yaw.** Stage 2 respecifies it as
  position + smallest-three quaternion + velocity; `NETCODE-STATUS.md` section 6 lists every file
  that change touches, in order, ending with a bump of `Wire.ProtocolVersion` to 2.
* **No `GameData` asset instance exists** — the ScriptableObject class is there, the `.asset` is not.
* Still missing from the port: `BuildTools.cs` and the WebGL player settings block,
  `PhysicsLayerSetup.cs`, `link.xml`, the EditMode test harness, the Game-layer files
  (`Bootstrap`, `PoseStreamer`, `RemoteCharacter`, `RemotePlayerSpawner`, `RoomCodes`, `Clipboard`),
  a `JOIN_REFUSED` reply for version mismatch, and a desktop transport.

---

## Netcode port, Stage 2 of 7 — the game is on the netcode (2026-09-20). IMPLEMENTED, UNTESTED.

Full record: `docs/NETCODE-STATUS.md`, "Stage 2". Kit rules: `docs/KIT.md`,
"Netcode rules for kit pieces". Nothing was run; checks were compile, the scene
validator (0 issues in `Zone1` and `Dev/FeelBox`) and read-backs.

- **Session everywhere.** `_Managers/SessionRunner` (order -1000) in `Zone1`
  and `Dev/FeelBox`: adopts `SessionRunner.Shared` or starts an offline
  `LoopbackTransport` session and `StartGame()`s it. `LevelClock` follows the
  room clock (`SessionRunner.LevelMs`).
- **Protocol v2.** POSE 0x01 is now 26 bytes (flags, f32 position, smallest-three
  rotation, velocity, seq). New ids 0x20-0x28 (possess / release / weapon owner,
  state, broken, respawned, damaged / bat claim, bat event), 0x40-0x42 (enemy
  state, hit claim, enemy health), 0x50-0x52 (kit state, kit req, world reset).
  New files: `Protocol/GameEnums.cs`, `Protocol/Messages/{Player,Enemy,World}Messages.cs`.
- **Sim.** `WorldSim` re-created with player / weapon / enemy / kit tables and
  snapshot parts; `Sim/{WeaponTable,EnemyTable,KitTable}.cs`.
- **Session.** `IHostWorld`, `NetSession.HostEmit` / `HostHeader` / `HostWorld`,
  `Rules/{WeaponRule,EnemyStateRule}.cs`,
  `Validators/{PossessValidator,HitValidator,BatValidator,KitValidator}.cs`.
- **Game.** `SessionRunner`, `WorldAuthorityNet` (the bridge), `WeaponBodyNet`,
  `GoblinBrainNet` (host rows + client puppet), `PoseStreamer`,
  `RemotePlayerSpawner`, `RemotePlayerView`, `Prefabs/Player/RemotePlayer.prefab`,
  `Data/GameData.asset` (weapons[1..7], enemies[1..3], modifiers[1], indexed by
  def id). Every `WorldAuthority.Request*` kept its signature and its C# event.
- **Exact names to grep:** `Simulates`, `ReconcileOwner`, `SubmitKit`, `ApplyKit`,
  `SyncFromSim`, `NetCollect`, `PuppetUpdate`, `SetRemoteDriven`, `RemoteMove`,
  `NoteBat`, `RequestBat`, `RequestEnemyAttack`, `CopyRemotePoints`.
- **Process notes.** `script_apply_edits` and `apply_text_edits` echo the whole
  edit back, so large changes went into new partial files and the edits to
  existing MonoBehaviours were kept to one-line hooks. `apply_text_edits` needs a
  `precondition_sha256`. `delete_script` was refused once by the permission
  system (`PlayerState.cs`, then edited in place) and allowed once
  (`WorldSim.cs`, re-created; new GUID, plain class, harmless).
  `manage_scriptable_object` patches: `{"propertyPath": "x.Array.size", "op":
  "array_resize", "value": n}` and `{"propertyPath": "x.Array.data[i]", "op":
  "set", "ref": {"path": "Assets/..."}}`. Scene object references in
  `manage_components set_property` take instance ids, not hierarchy paths.

## Netcode port, Stage 3 of 7 — a desktop transport (2026-09-20). IMPLEMENTED, UNTESTED.

Full detail in `docs/NETCODE-STATUS.md` section "Stage 3". Nothing was run: no
play mode, no build, no socket. Clean compile, 0 console errors and 0 warnings.

- **`Transport/TcpTransport.cs` + `Transport/TcpLink.cs`** — plain TCP behind
  `INetTransport`, so two non-browser instances can finally meet (ATCK had no
  desktop path; this is `NETCODE-PORT.md` section 8.3). The host listens on 7777
  and **relays**, so `Broadcast` and `SendTo` behave like a full mesh even
  between two clients. Frames are `len u32 | op u8 | body`; opcodes Hello,
  Welcome, Joined, Left, Relay, Deliver, Ping. Peer ids are `host`, `p1`, `p2`.
  Liveness is the socket receive timeout (10 s) fed by a 2 s idle ping.
- **Threads:** accept, connect, and a reader + writer per socket, all background.
  The main thread only enqueues. `Leave()` closes and joins them, and also runs
  from `Application.quitting`, `ExitingPlayMode` and `beforeAssemblyReload`, so
  nothing survives a domain reload.
- **`Session/TransportFactory.cs`** — `ForPlatform()` is now WebGL to
  `WebRtcTransport`, editor and standalone to `TcpTransport`; `Offline()` still
  `LoopbackTransport`. New `TransportFactory.DefaultPort` and
  `HostAddresses(port)` (the LAN IPv4 list) so Game can show the room code
  without naming a transport. The room code on desktop **is** the address.
- **The dev switch** (`Game/SessionRunnerDev.cs`, a `partial SessionRunner`, plus
  `Editor/NetDevWindow.cs`): there is no lobby yet, so host / join / offline is
  chosen by the `Pesky > Net` menu in the Editor (EditorPrefs, they persist) or
  by `-peskyhost [port]` / `-peskyjoin host[:port]` / `-peskyname Name` on a
  build's command line. Default is unchanged: offline on loopback. Delete this
  when the lobby scene exists.
- **Exact names to grep:** `TcpTransport`, `TcpLink`, `TcpWire`, `TcpOp`,
  `StartAutoSession`, `DevAddress`, `HostAddresses`, `NetDevWindow`,
  `Pesky.DevNetMode`.
- **Process notes.** Both transport files are wrapped in
  `#if !UNITY_WEBGL || UNITY_EDITOR`, the exact complement of the
  `TransportFactory` branch, so a WebGL player never compiles
  `System.Net.Sockets`. `PlayerSettings.runInBackground` was already true and was
  not touched. No scene, prefab or asset was modified in this stage.

## Netcode port, Stage 4 of 7 — main menu and room page (2026-09-20). IMPLEMENTED, UNTESTED.

Full notes: `docs/NETCODE-STATUS.md` section "Stage 4". Checks made: clean
compile, the edit-mode scene validator on `MainMenu`, `Zone1` and `Dev/FeelBox`
(0 issues), serialized values and build settings read back, and `Menu.uxml`
instantiated in edit mode to prove every element `MenuView` queries exists. No
play mode, no build.

- **Scene flow is now** `Boot` (0) -> `MainMenu` (1) -> the level. Build settings
  are `Boot`, `MainMenu`, `Zone1`, in that order. `Boot/_Bootstrap`'s
  `GameBootstrap.sceneName` changed from `Zone1` to `MainMenu`; Boot is still
  nothing but that one object.
- **`Scenes/MainMenu.unity`** (new): `_Managers/EventSystem` (EventSystem +
  InputSystemUIInputModule, its eight UI actions bound to
  `Assets/InputSystem_Actions.inputactions`), `_Cameras/Main Camera` (solid
  colour, culling mask 0, tagged MainCamera), `_UI/Menu` (UIDocument ->
  `UI/Menu.uxml` + `UI/UiPanelSettings.asset`, and `MenuFlow` with its document,
  `UI/Menu.uss` and `Data/GameData.asset` serialized).
- **UI (UI Toolkit):** `UI/Menu.uxml` + `UI/Menu.uss`. A centred column, max
  560 px wide, dark ground, one accent (amber `rgb(226,186,74)`), cards for the
  groups. TITLE = title, callsign field (remembered in `PlayerPrefs` under
  `Pesky.Callsign`), HOST, JOIN with the code field beside it, TUTORIAL, a status
  row, QUIT (hidden on WebGL). ROOM = the code big with COPY, the eight crew rows
  (slot colour, callsign, HOST and YOU badges, OPEN), the crew count, the
  labyrinth-size line, START (host, enabled from one player), LEAVE.
- **`UI/HudPanelSettings.asset` was renamed** to `UI/UiPanelSettings.asset` with
  `AssetDatabase.RenameAsset`, so the GUID survived and `Prefabs/UI/HUD.prefab`
  still resolves it. The HUD and the menu now share one PanelSettings.
- **New scripts** under `Scripts/Game`: `GameLocator.cs` (the one static handle
  that outlives a scene load: `Session`, `FromMenu`, a one-shot `Message`),
  `SceneNames.cs`, and under `Scripts/Game/UI`: `RoomCodes.cs` (ATCK's six-letter
  alphabet plus the desktop `host:port` shape), `Clipboard.cs`, `SlotColors.cs`,
  `MenuView.cs` (the elements) and `MenuFlow.cs` (the controller).
- **`NetSession.ReturnToLobby()`** added: host only, emits `SESSION_PHASE(Lobby)`,
  so a room that finished a run can start another.
- **`SessionRunner`** now adopts `GameLocator.Session` (its `Shared` property
  forwards there) and, when the menu started the session, loads `menuScene` on
  `SessionPhase.Ended` or a lost host. Both endings are queued and acted on after
  `NetSession.Update()` returns.
- **Deleted:** `Scripts/Game/SessionRunnerDev.cs` and
  `Scripts/Editor/NetDevWindow.cs`. The `Pesky > Net` menu and the
  `-peskyhost` / `-peskyjoin` / `-peskyname` command-line switches are gone; host
  and join are chosen in the menu now.
- **Exact names to grep:** `MenuFlow`, `MenuView`, `GameLocator`, `SceneNames`,
  `RoomCodes`, `SlotColors`, `ReturnToLobby`, `IsInBuild`, `Pesky.Callsign`,
  `UiPanelSettings`.
- **Watch out:** the `Labyrinth` scene does not exist, so START checks the build
  settings first and says so instead of throwing; to walk the whole flow today,
  point `MenuFlow.labyrinthScene` at `Zone1` in the Inspector.

## Stage 5 — the labyrinth core (code and data only)

The grid, the Mage's two powers, the compass and the round rules, with no scene.
Full description in `docs/LABYRINTH.md`; netcode detail in `docs/NETCODE-STATUS.md`
section "Stage 5". Implemented, untested: clean compile, scene validator 0 issues
on `MainMenu` / `Zone1` / `Dev/FeelBox`, nothing run.

- **The grid is a table, not geometry.** `Pesky.Sim.LabyrinthGrid` (plain C#) is
  `cell -> room id` for a **5x5 grid whose size is data**. Opposite edges wrap;
  exactly one doorway does not, the outward one on the good-end room, and that is
  the **Exit**. Start room = centre cell, the two ends = two different corners
  chosen by the session seed. `cell = row * Width + column`, north is row - 1,
  east is column + 1, headings are `North 0, East 1, South 2, West 3`.
- **New data asset** `Assets/Data/Labyrinth.asset` (`LabyrinthDef`), linked from
  `GameData.labyrinth`. 25 rooms: id 0 `Weapon Rack` (Start), 1
  `Resurrection Room` (BadEnd), 2 `Gate Hall` (GoodEnd), 3-24 blank. **The index
  in `rooms` is the room id and it travels on the wire: append only.**
- **Ten new messages, `MsgId` 0x30-0x39**, and `Wire.ProtocolVersion` is now
  **3**. Two of them (`ROLE_ASSIGN`, `COMPASS_TARGETS`) are **Replies**, which is
  how a secret reaches one player and nobody else.
- **`SnapshotPartKind.Labyrinth = 5`; `End` moved to 6.** Any future part goes
  before `End` the same way.
- **`LabyrinthRule`** is both the host rule and the validator for the Mage's two
  intents. It assigns roles at round start from a **host-private Rng** (never
  `WorldSim.Rng`, which every peer can replay), validates swaps (adjacent, never
  diagonal, never the three fixed rooms, cooldown per Mage, plus a connectivity
  hook), enforces the **minority limit** on compass bends across both Mages, runs
  the respawns, and ends the round on "enough of the crew at the Exit" or "a
  majority in the Resurrection Room".
- **Every refusal is silent** and `ROUND_RESULT` is the only message that ever
  names the Mages.
- **`MagicDoor` gained a grid-door mode**: `gridDoor`, `labyrinth`, `room`,
  `doorwayDir`. `Twin` then resolves from the table **at the moment it is asked**,
  and `DestinationLabel` / `DestinationGlyph` give a doorway a truthful glyph.
  Glyphs never lie; compasses do.
- **New Game scripts:** `LabyrinthRoom.cs` (room id, four doorways, footprint,
  anchor), `LabyrinthDirector.cs` (the one sim-to-scene mapping, under
  `_Managers`), `CompassModel.cs` (`HasReading`, `Spinning`, `Doorway`,
  `WorldDirection`).
- **`IHostWorld.CollectPlayerCells(int[], bool[])`** is new: the host asks the
  scene where everybody is, five times a second, from the streamed player rows.
- **Exact names to grep:** `LabyrinthGrid`, `LabyrinthState`, `LabyrinthDef`,
  `LabyrinthRule`, `LabyrinthDirector`, `LabyrinthRoom`, `CompassModel`,
  `CompassHint`, `DoorwayFilter`, `ApplyReply`, `CollectPlayerCells`,
  `ReportPlayerDown`, `RequestRoomSwap`, `RequestCompassBend`, `IsExitDoorway`,
  `DestinationLabel`.
- **Watch out:** no scene has a `LabyrinthDirector`, so all of this is idle at
  runtime today; `Labyrinth.unity` still does not exist and is still not in the
  build settings. Crossing the Exit doorway does nothing — the win condition is
  proximity to it. Nothing calls `ReportPlayerDown`, so death and respawn have
  never executed.

## Netcode port, Stage 6 of 7 — the labyrinth scene and its UI (2026-09-20). IMPLEMENTED, UNTESTED.

- **`Assets/Scenes/Labyrinth.unity`** — new, build index **3**. 2,269
  GameObjects, 1,229 static, 1 Camera / AudioListener / WorldAuthority /
  LevelClock / LabyrinthDirector. Roots `_Managers`, `_Cameras`, `_Lighting`,
  `_UI`, `Environment/Rooms`. 25 room instances on a 200 m lattice
  (`x = (id % 5) * 200`, `z = -(id / 5) * 200`), ids 0/1/2 = Start / Resurrection
  Room / Gate Hall. Scene ids 101..302. `WorldAuthority`: `magicDoors` 100,
  `rooms` 26, `weapons` 7, `labyrinth` = the director.
- **New prefabs**: `Assets/Prefabs/Kit/MagicDoor_Grid.prefab`,
  `Assets/Prefabs/Rooms/LabyrinthRoom.prefab` and its variants
  `LabyrinthRoom_Start`, `LabyrinthRoom_BadEnd`, `LabyrinthRoom_Exit`.
- **New scripts**: `Game/DoorwayGlyph.cs`, `Game/ExitZone.cs`,
  `Game/UI/LabyrinthHud.cs`, `Game/UI/LabyrinthMapView.cs`.
- **New UI**: `Assets/UI/Labyrinth.uxml`, `Assets/UI/Labyrinth.uss` — compass,
  hold-Tab map with the Mage's drag and compass-bend chips, role reveal, round
  banner.
- **Changed**: `Assets/Scripts/Editor/SceneValidator.cs` (grid doorways exempt
  from the twin check, new `CheckLabyrinth`); `Assets/Input/PeskyControls.inputactions`
  (new `Gameplay/Map` action on Tab, added through the InputSystem API).
- **The wire is untouched.** `Wire.ProtocolVersion` is still 3.
- **Checks made**: clean compile after every script change; project scene
  validator 0 problems on `Labyrinth`, `Zone1`, `MainMenu` and `Dev/FeelBox`;
  serialized values read back (doorway forwards, door wiring, weapon home slots,
  exit zones, build settings). **Nothing was run** — no play mode, no tests, no
  screenshots.
- **Exact names to grep:** `LabyrinthHud`, `LabyrinthMapView`, `DoorwayGlyph`,
  `ExitZone`, `MagicDoor_Grid`, `LabyrinthRoom_Start`, `LabyrinthRoom_BadEnd`,
  `LabyrinthRoom_Exit`, `CheckLabyrinth`, `map-overlay`, `mage-bar`,
  `compass-needle`, `role-reveal`, `result-banner`.
- **Watch out:** the exit doorway's direction is chosen by the seed, so all four
  doorways of the Gate Hall carry an `ExitZone` and only one lights up. Cooldown
  rings on the map are a local guide, not the host's truth. The Mage's "BENT n"
  counter only counts what this Mage asked for. Crossing the Exit still does
  nothing — the win is proximity. Nothing calls `ReportPlayerDown`.

## Netcode port, Stage 7 of 7 — the tutorial scene and the test guide (2026-09-20). IMPLEMENTED, UNTESTED.

- **`Assets/Scenes/Tutorial.unity`** — new, build index **2**, made by
  duplicating `Zone1.unity` in the Editor and cutting the towers out of the copy:
  `Room06`…`Room20`, `RoomA`…`RoomD`, `BackTowerShell`, `MagicDoor_2_to_A` and
  `_Managers/FloorActivator` are gone (22 objects), and the 113 null entries that
  left in `WorldAuthority`'s arrays were compacted. Rooms 1-5 and the four
  hallways are untouched. 1,267 objects, 679 static. NavMesh re-baked to
  `Assets/Scenes/Tutorial/NavMesh-Environment.asset`. **`Zone1.unity` itself was
  never written to** and is still openable from the Editor — it is just out of
  the build.
- **Build settings are now exactly** `Boot`, `MainMenu`, `Tutorial`, `Labyrinth`.
  `MenuFlow.tutorialScene` changed from `Zone1` to `Tutorial`; TUTORIAL still
  starts an offline loopback session and loads it at once.
- **`Room6_MageTutorial` (new scene root)** — the Mage lesson: 9 rooms of the real
  labyrinth on a 3x3 grid, 120 m apart from `(300, 0, 0)`, built from
  `LabyrinthRoom` / `_BadEnd` / `_Exit`. `_Managers/LabyrinthDirector`,
  `_Managers/CompassModel` and `_UI/LabyrinthHud` (sortingOrder 1) drive it with
  the same components the real labyrinth uses. Scene ids 2001..2075. The arena's
  far doorway (`MagicDoor_5_to_Practice`, was `MagicDoor_5_to_6`) is twinned with
  `MagicDoor_Practice_to_5` in the Entry Hall.
- **3x3, not the 3x1 the brief asked for** — `LabyrinthDef.MinSide` is 3, and in
  a 3-cell grid the Start and both ends occupy every cell, so no swap could ever
  be legal and the lesson could not be taught. Reasoning in NETCODE-STATUS S7.3.
- **New assets**: `Assets/Data/Labyrinth_Tutorial.asset` (3x3, 9 rooms, 5 s
  cooldowns, `resurrectionFraction` 1 so a solo player cannot trip the Mage's
  instant win) and `Assets/Data/GameData_Tutorial.asset` (a copy of `GameData`
  pointing at it; **it will not follow later edits to `GameData.asset`**).
- **New script**: `Game/TutorialTrigger.cs` — a one-shot trigger box that
  switches objects, wakes the labyrinth HUD, and (on the Gate Hall's four
  `ExitZone`s, whose colliders are live only for the real Exit) ends the run with
  `NetSession.EndRun(Escaped)` + `Leave()`, which lands the player back on the
  menu's title page.
- **Edited**: `LabyrinthHud` (`startAsleep`, `practiceChipName`, `Wake()`),
  `LabyrinthMapView` (`PracticeName` — a local-only "DUMMY" chip on the Mage bar
  that sends nothing), `MenuFlow` (optional `tutorialData`). The wire is
  untouched: `Wire.ProtocolVersion` is still 3.
- **New doc**: `docs/TEST-CHECKLIST.md` — how to run solo, how to pair the Editor
  with a Windows build from Build Profiles, 13 ordered checks from "connect" to
  "late join", the tutorial's own walk-through, and every stage's riskiest
  assumptions in one list. `docs/SLICE-1-REPORT.md` gained the Tab / map keys.
- **Checks made**: clean compile after every change; the scene validator on all
  four build scenes (0 problems each); build list and serialized values read
  back. **Nothing was run** — no play mode, no tests, no screenshots.
- **Exact names to grep**: `TutorialTrigger`, `TutorialGate_MageLesson`,
  `PRoom_00_EntryHall`, `MagicDoor_Practice_to_5`, `Labyrinth_Tutorial`,
  `GameData_Tutorial`, `startAsleep`, `practiceChipName`, `PracticeName`,
  `tutorialData`.
- **Watch out**: the tutorial player is always the Mage (one player, one
  fragment), so the labyrinth's own escape ending cannot fire there. The HUD
  stays blank until a weapon enters the Entry Hall. Souls do not trigger
  `TutorialTrigger`. Opening `Tutorial.unity` straight from the Editor works and
  does **not** return to the menu when the run ends (`GameLocator.FromMenu`).

---

## Feedback round 2 — the owner's five notes (2026-09-20)

**Implemented, untested.** Clean compile after every change; the edit-mode scene
validator on `Boot`, `MainMenu`, `Tutorial` and `Labyrinth` — **0 problems
each**; every new message encoded and decoded once in the Editor; a full snapshot
built and read back; serialized values read back. **Nothing was run — no play
mode, no tests, no screenshots.** Full write-ups: `docs/LABYRINTH.md` section 11
and `docs/NETCODE-STATUS.md` section F2.

1. **Door labels off, a number on the floor.** `LabyrinthDef.showDoorLabels` is
   new and **false**; `DoorwayGlyph` reads it through the new
   `MagicDoor.Labyrinth` accessor and hides the two labels, and they are
   **inactive in `MagicDoor_Grid.prefab`** as well. The logic is intact — turn
   the toggle on and they come back. `LabyrinthRoom.prefab` gained
   `Fixtures/FloorNumber`: a world-space TMP at local (0, 0.03, 0), rotation
   **(90, 0, 0)**, driven by the new `Game/RoomFloorNumber.cs`, which writes
   `LabyrinthRoom.RoomId` at runtime. All 25 rooms, the three variants and the
   tutorial's nine practice rooms picked it up from the one prefab.
   **The mirroring was real.** A `TextMeshPro` mesh faces its own local **-Z**
   (proved against Unity's Quad and against the MagicDoor's own glowing plane,
   which is a Quad deliberately yawed 180 so it faces into the room). The
   doorway labels were at identity on a door whose +Z faces the room, and
   `Sign.prefab`'s `Label` was yawed 180 while sitting on the board's -Z face.
   Both are fixed. **The Sign fix reaches every sign in the project**; no Sign
   anywhere overrides that rotation, so no sign changes which side it shows,
   only whether it reads correctly. Revert: `Sign.prefab` > `Label` > yaw 180.
2. **A shared scratch pad, and it is the Tab menu.** A weapon holding Tab now
   gets **only** a blank dark canvas — no auto-map, no cells, no room names. A
   Mage gets two top tabs, MAP and PAD. Strokes are vector polylines in
   normalised u16 canvas coordinates: PAD_STROKE_REQ (0x3A) -> host validates
   size and rate -> PAD_STROKE (0x3B) broadcast with a sequence number and the
   drawer's slot, painted with Painter2D; PAD_CLEAR (0x3C) at the start of every
   round and from a host-only CLEAR button. An eraser is a stroke in the canvas
   colour, not a delete. 64 points per message (260 / 267 / 5 bytes), longer
   strokes split on a shared joining point, snapshot part `Pad = 6` (`End` moved
   to 7) capped at 300 strokes / 4,000 points = about 16 KB. **The cursor is now
   freed for everybody** while the overlay is open, not just for a Mage.
   New: `Protocol/Messages/PadMessages.cs`, `Sim/ScratchPadState.cs`,
   `Session/Rules/PadRule.cs`, `Game/UI/ScratchPadView.cs`.
3. **The compass is a ring and spikes.** New `Game/UI/CompassView.cs` paints it:
   a **green** spike from the middle toward the doorway to take (still green and
   simply wrong for a bent player), plus a **red** one toward the Resurrection
   Room **on a Mage's machine only**. A spike spins inside its target room.
   Nothing else is on it — the needle, the N/E/S/W caption and the destination
   name are gone from `Labyrinth.uxml`. `CompassModel` gained the red half.
4. **Swap cooldown 60 s.** `Assets/Data/Labyrinth.asset` is **60**,
   `Labyrinth_Tutorial.asset` is **15** so the lesson is not a wait.
   `LabyrinthRule` reads the asset and nothing else; the only other number is
   `LabyrinthDef`'s field initialiser, now also 60, for a brand-new asset.
5. **The tutorial hole was Room 2's east wall.** `Environment/Room2_Goblin/
   Geometry/Wall_East_Doorway` — the opening the deleted lift shaft beside the
   Goblin Room used to connect to — led straight into the void: a probe found no
   floor for 15 m east of it. Filled with a 3.0 x 3.5 x 0.5 `Plug` on
   `M_Wall`, World layer, the same static flags as the jambs, and the segment
   renamed **`Wall_East_Sealed`**. Nothing orphaned was left near it. Tutorial
   NavMesh re-baked. **Every other opening in the scene checks out**: the other
   nine all have a door within 2 m or floor on both sides.
   `SceneValidator.CheckDoorwayOpenings` is new and enforces it; renaming the
   sealed segment back to `..._Doorway` makes it fire, which is how it was
   proved.

- **Exact names to grep**: `PadStrokeReq`, `PadStroke`, `PadClear`, `PadRule`,
  `ScratchPadState`, `ScratchPadView`, `CompassView`, `RoomFloorNumber`,
  `showDoorLabels`, `showFloorNumbers`, `padMaxStrokes`, `Wall_East_Sealed`,
  `CheckDoorwayOpenings`, `FloorNumber`, `HasRedReading`.
- **Watch out**: `Wire.ProtocolVersion` is **4** — rebuild both sides before
  pairing two instances. The `Sign.prefab` rotation fix is the one change with
  project-wide reach. A rate-capped pad stroke is refused in silence and fades
  off the drawer's own screen after 3 s.


---

## 2026-09-20 — project cleanup, kit inventory, room shapes, compass spikes

Full detail in `docs/CLEANUP-AUDIT.md`. In short:

- **Removed** (35 assets, all to the SYSTEM TRASH via `AssetDatabase.MoveAssetToTrash`, nothing
  hard-deleted, nothing removed from the shell): the old tower direction (`Zone1`, `Bridge`,
  `MainTower`, `Keep`, `Scenes/Zone1/LightingData`, `FloorActivator.cs`, `ShellPanel.prefab`);
  the URP template leftovers (`SampleScene`, `TutorialInfo/`, `Readme.asset`, `Mobile_RPAsset`,
  `Mobile_Renderer`); and the builder leftovers (`Scripts/Game/Dev/` with `FeelProbe` and
  `Zone1Probe`, `Assets/Screenshots/`, the stray extensionless `Assets/Rune_Metal`,
  `PeskyPlatform.jslib`, the orphan `M_GoblinWindup.mat`).
- **Repaired first:** `Tutorial`'s `NavMeshSurface` held a scene-embedded copy of the NavMesh while
  the baked `Scenes/Tutorial/NavMesh-Environment.asset` sat unreferenced — it now references the
  asset. `Tutorial` also still pointed at `Zone1`'s lighting data (0 lightmaps, 0 probes): cut.
  `SampleSceneProfile.asset` turned out to be the project's **live** post-processing profile
  (`PC_RPAsset.m_VolumeProfile`), so it was **renamed** `VP_Pipeline.asset`, not removed.
  Quality level 0 repointed to `PC_RPAsset`. `InputSystem_Actions.inputactions` moved to
  `Assets/Input/` (GUID kept; the MainMenu EventSystem still resolves it).
- **Inventory:** every kit prefab moved into `Assets/Prefabs/Kit/<group>/` (Doors, Pickups,
  Plates_And_Pans, Movers, Breakables, Hazards, Stations, Markers, Signs_And_Lights) and the room
  blocks into `Prefabs/Rooms/Pieces/`, all with `AssetDatabase.MoveAsset` so every GUID and scene
  reference survived. `RoomShapeBuilder`'s ten hard-coded paths were updated to match.
- **Five new room shapes** in `Assets/Prefabs/Rooms/Labyrinth/`: `LabyrinthRoom_Round`,
  `_Octagon`, `_LShape`, `_LongGallery`, `_TallShaft`. Built with the Room Shape Builder, each with
  four grid doorways N E S W, a static World shell, a roof, four torches, `RoomVolume`, sign, floor
  number, anchor, spawn point and footprint. Not placed in any scene. `LabyrinthRoom` gained
  `sealedSides` (and `SceneValidator` now honours it). See `docs/LABYRINTH.md` section 12.
- **Compass:** `CompassView`'s GREEN spike is now 70% of the RED one's length and 60% of its width
  and is painted on top of it, so a Mage sees both when they line up. The five shape numbers are USS
  custom properties on `.compass-dial` in `Assets/UI/Labyrinth.uss`, with matching fallbacks in code.
- **Docs:** `DESIGN.md`, `IMPLEMENTATION-PLAN.md`, `CASTLE-LAYOUT-BUILD.md`,
  `BACK-TOWER-TEST-CHECKLIST.md` and `level-design/*.html` moved to `docs/archive/` with a README.

**Nothing was run.** Checks: clean compile; `Pesky > Validate Open Scenes` 0 problems on all four
build scenes plus `Dev/FeelBox`; 0 missing scripts and 0 dangling references; build settings still
exactly Boot, MainMenu, Tutorial, Labyrinth; no empty folders; 376 assets before, 346 after.

---

## Web build and publishing setup (2026-09-21)

The game can now be built for the browser and published to GitHub Pages. Full instructions
live in the new **`docs/WEB-BUILD.md`**; this is the record of what changed.

**Added**

- `Assets/Scripts/Editor/BuildTools.cs` — ported from `ATCK Unity/Assets/Scripts/Editor/BuildTools.cs`
  (copied inside the Editor with `execute_code`, `ATCK` → `Pesky`, then edited). Menu items
  **Pesky > Apply Player Settings**, **Pesky > Build Web** and
  **Pesky > Build Windows (two-player test)**, plus `BuildFromCommandLine` (exits 0/1 for CI).
  Differences from ATCK's: output goes to `<repo>/Builds/Web` (what `publish.ps1` expects) instead
  of the repository root; the scene list is read from the **build settings** rather than hard-coded;
  the colour space is left alone; a `Build Windows` path was added; and a wrong active build target
  makes the menu item switch platform and ask to be run again rather than build through a domain
  reload. ATCK's five-minute cooldown and `isBuildingPlayer` guard were kept — automation replays
  menu items, and a WebGL build is 10–25 minutes.
- `Assets/link.xml` — ported from ATCK's, trimmed to the assemblies this project actually has
  (ATCK's `UnityEngine.TextRenderingModule` and `UnityEngine.InputModule` are gone: neither package
  is in `manifest.json`) and extended with `UnityEngine.AIModule` and `UnityEngine.UIElementsModule`.
- `docs/WEB-BUILD.md` — building, publishing, the one-time GitHub Pages steps, every player settings
  choice and why, the platform-conditional map, the TURN hook, and the Windows test build.

**Player settings now applied from code** (WebGL): template `PROJECT:Pesky`, Brotli **with**
decompression fallback, Run In Background on, `OpenGLES3` only with threads off, full exceptions
with stack traces, engine stripping + managed stripping Low + `link.xml`, IL2CPP `OptimizeSize`,
data caching on, 64 MB initial / 2048 MB max geometric memory, company `Ben Normann`, product
`Pesky Weapons`. **The colour space stays Linear** — WebGL 2 supports it and the project is authored
that way; ATCK's `ColorSpace.Gamma` line was deliberately not ported.

**Platform conditionals: audited, nothing had to change.** After switching the active target to
WebGL, `UNITY_WEBGL` is defined in the Editor too, so every browser-only path was checked:
`WebRtcTransport` (usings, nine `AHNet_*` DllImports, every call site), `TransportFactory`
(`ForPlatform` / `DefaultPort` / `HostAddresses`), `RoomCodes.IsAddressBased` and `Clipboard` all
already use `#if UNITY_WEBGL && !UNITY_EDITOR`, and `TcpTransport` / `TcpLink` use the exact
complement `#if !UNITY_WEBGL || UNITY_EDITOR`. So the Editor still gets TCP/loopback while the
target is WebGL, and a real browser player contains no sockets or threads at all. `MenuFlow`'s QUIT
button uses a runtime `Application.platform` test, which is correct as it stands. Every `AHNet_*`
`DllImport` was matched against `Plugins/WebGL/AHNet.jslib`'s exports (all ten present, including
`AHNet_CopyClipboard`), and the three bridge names (`NetBridge`, `window.AH_Net`,
`window.AH_UnityInstance`) match across C#, the jslib and `net.js`.

**The build was run. It succeeded.** `Builds/Web` = **16.2 MiB (16,964,942 bytes)**:
`index.html`, `net.js`, `trystero.min.js`, `keys.js`, `.nojekyll` and
`Build/Web.{loader.js,data.unityweb,framework.js.unityweb,wasm.unityweb}` — the `.unityweb`
extension confirms the Pages-compatible decompression fallback is active. No `TemplateData/`
(the template has no assets of its own, and `index.html` never references it). `[Pesky] build
result: Succeeded, size 16 MB, 0 errors, 0 warnings`; the only build warnings in the console come
from `com.unity.ai.inference`'s Sentis shaders, none from our code, and `link.xml` resolved every
assembly it names.

**Two things worth knowing.** The *first* attempt failed with a bare `Build was canceled.`:
`com.unity.test-framework.performance` creates `Assets/Resources/PerformanceTestRunInfo.json` and
`PerformanceTestRunSettings.json` from its build preprocessor, and creating assets mid-build makes
Unity abort. Those two files (plus their `.meta`s) now exist in the project and the retry — and a
later full run of the menu item itself — both succeeded. Second: the unused AI / multiplayer-centre
/ visual-scripting packages are a meaningful share of the 16 MB if the download size ever matters.

**Nothing was run**: no play mode, no tests, no browser. The checks made were a clean compile before
and after the platform switch (0 CS errors), every player setting read back through the Editor, the
`DllImport`-to-jslib-export audit, and one complete WebGL build. **The active build target was left
on WebGL.**

## Feedback round 3 — four play-test notes (2026-09-21)

**Implemented, untested.** Clean compile after every script change (0 CS errors);
`Pesky > Validate Open Scenes` reads **0 problems** on Boot, MainMenu, Tutorial,
Labyrinth and Dev/FeelBox; the layer, the collision matrix, every prefab and every
scene instance read back through the Editor. **Nothing was run — no play mode, no
tests, no screenshots.** Build target left on **WebGL**.

1. **"All signs are backwards."** The prefabs were not the fault. Measured, not
   assumed: a `TextMeshPro` mesh renders and reads from its own local **−Z**
   (mesh normals `(0,0,-1)`, the BL→TL→TR winding, and — decisively — a
   backface-culled `MeshCollider` raycast that hits the TMP mesh *and* Unity's
   Quad only from the −Z side; the Quad is the control, and it is the same mesh
   the MagicDoor's plane yaws 180° to face into the room). So round 2's model was
   right and `Sign.prefab`'s `Label` is already correct at identity on the board's
   −Z face — **reverting its yaw would have made every sign blank**, culled from
   the room and occluded by its own board from behind. The real fault was
   **placement**: every hand-placed instructional sign in `Tutorial.unity` sat at
   yaw 0 with its readable −Z face **1.5 m from a wall**, while the 25 prefab-driven
   `RoomSign`s were right. Seven were yawed 180° — `Sign_Map`, `Sign_Resurrection`,
   `Sign_Exit`, `Sign_Compass`, `Sign_Mage`, `Sign_1`, `Sign_4`. The doorway
   `Glyph` / `DestinationName` in `MagicDoor_Grid.prefab` (yaw 180 on a door whose
   +Z faces the room) and `LabyrinthRoom.prefab`'s `Fixtures/FloorNumber`
   (rot `(90,0,0)`: readable face up, top of the digit toward the north doorway)
   were each checked against the same rule and are **correct**; no scene or variant
   overrides either. New `SceneValidator.CheckSignFacing` flags any sign whose
   readable side has less room than its back, so this cannot come back.
2. **Souls can no longer use teleport doors.** New layer **`SoulBarrier` (15)**
   colliding with **`Soul` (10) and nothing else**; a `SoulBlock` non-trigger
   `BoxCollider` filling the opening on `MagicDoor.prefab`, `MagicDoor_Grid.prefab`
   and every labyrinth room prefab (the five shaped ones needed it added directly);
   `MagicDoor` no longer watches souls for a crossing; and
   `WorldAuthority.RequestMagicDoorTraverse(door, soul)` returns **false**, so the
   host refuses a forged traversal too. Weapons, goblins, the NavMesh and the
   trajectory preview are untouched — no other mask contains layer 15. A soul
   pushing at an opening gets a HUD banner (`HudController.soulDoorHint`).
3. **The camera can no longer leave the map.** `OrbitCamera` used to raise its
   pivot 1 m above the target unconditionally, so against a ceiling the pivot
   — and therefore the start of the pull-in cast — was already outside the room.
   Now the pivot is reached by a sphere cast **from the target**, stopping a `skin`
   short of whatever is in the way, and the pull-in cast starts from that clamped
   pivot. The probe radius is widened to cover the **near clip plane's far corner**
   so the plane cannot poke through, and a cast that starts inside a collider
   narrows its probe instead of snapping the camera onto the pivot. Pulling in is
   instant, easing back out is smoothed (`distanceEaseTime`). New serialized
   tunables: `skin`, `pivotProbeRadius`, `fitRadiusToNearClip`, `distanceEaseTime`.
4. **Compass bend never worked below 4 crew.** `LabyrinthRule.OnBend` refused
   unless `bentAfter < bendFractionLimit * nonMages`, and a strict minority of 0, 1
   or 2 non-Mages is **zero** — so in the tutorial and in any small test the host
   silently refused every bend. New `LabyrinthDef.minBendTargets` (default 1) and
   `LabyrinthDef.MaxBentFor(nonMages)`, used by the rule *and* by the map view, so
   the two cannot disagree. Refusals are now said out loud on the Mage's hint line,
   the practice stand-in passes the same cooldown and the same limit (and counts as
   one crew member so a solo tutorial has somebody to bend), and each chip shows
   where that compass was sent. **Not done:** the stand-in still has no grey-box
   body or world-space compass ring in the practice labyrinth — see the report.

## Feedback round 4: camera (2026-09-21)

Owner: "When looking up from a weapon the camera clips through the floors too."
Implemented, untested (compile clean, scene validator 0 problems on the five scenes).

**What was wrong** (`Assets/Scripts/Game/OrbitCamera.cs`, round 3's version):
1. `ClampPivot` gave up and left the pivot **on the target** whenever a 0.2 m sphere
   at the target touched World. A weapon lying on a floor has its origin ~3 cm above
   it, so that was *always* true: the pivot sat on the floor, never 1 m up.
2. `FreeDistance` clamped its answer to `minDistance` (0.1): with the pivot on the
   floor and the view pitched up, the camera was pushed 0.1 m along a **downward**
   vector no matter what the casts said, i.e. to or under the floor surface.
3. The scene cameras had **near clip 0.3**, so "cover the near plane" asked for a
   ~0.5 m probe. It never fits near a floor, was always halved down to ~3 cm, and at
   that size it protects nothing (and, still overlapping, the sphere cast really is
   blind to the floor, as the owner suspected).

**What it does now.** One probe sphere (`collisionRadius`, never smaller than the
near clip plane's far corner needs) is walked target -> anchor -> pivot -> camera,
every link starting where the previous one was proven free:
- `FreeAnchor`: the probe (+`skin`) at the target is pushed out of floor / wall /
  corner with `OverlapSphere` + `ComputePenetration` (nearest-point fallback), and
  must not end up across a surface (`Linecast`). In a gap too tight for it (under a
  rail) the probe **shrinks** (bisection down to `minProbeRadius`) and the camera's
  **near clip plane shrinks with it**, so the plane is always inside a free sphere.
- `LiftPivot`: sphere sweep from the anchor up to the raised pivot, eased like the
  distance (down at once, up gently), then settled.
- `Free`: sphere cast **plus** a plain ray (hard limit, backs off the full radius).
  No forced minimum distance any more: distance 0 is the (free) pivot.
- `Settle`: the final camera position is depenetrated again and line-checked
  against the pivot; if anything is between them the camera stays on the pivot.
- `OrbitPitch`: with the pivot held against a floor or roof (under a rail, a soul
  at the ceiling) the orbit **flattens** by up to `maxPitchEase` to keep
  `comfortDistance` instead of landing on the target; the view keeps its full pitch.
- Mask is still World only (layer 8); SoulBarrier (15) never blocks the camera.

Tunables (Collision header): `collisionMask` 256, `collisionRadius` 0.25, `skin`
0.04 (was 0.08), `minProbeRadius` 0.05, `comfortDistance` 1 (0 = off),
`maxPitchEase` 25, `distanceEaseTime` 0.12. Removed: `minDistance`,
`pivotProbeRadius`, `fitRadiusToNearClip`.

Scenes: Main Camera **near clip 0.3 -> 0.1** and `skin` 0.04 in Tutorial, Labyrinth
and Dev/FeelBox. Geometry audit (read-only): every floor / wall / roof / ledge /
step / rack piece in those scenes and in the room + piece prefabs has an enabled
non-trigger collider on World, none thinner than 0.2 m except the rack rails;
doorway alcoves are closed by a World `Back`. Nothing needed fixing.

Runtime helper: a hidden trigger `SphereCollider` ("OrbitCamera Probe", parked at
y = -10000) exists only because `ComputePenetration` needs a live collider.
