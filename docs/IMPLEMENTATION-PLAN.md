# IMPLEMENTATION-PLAN.md — Pesky Weapons one-shot grey-box slice

Inputs, treated as decided: `docs/DESIGN.md` (gameplay authority) and `docs/NETCODE-PORT.md`
(networking authority). Conflicts are listed in §11, never silently resolved.

Target: `C:/Users/benos/Code/Pesky Weapons/Pesky Weapons Unity`, Unity 6000.3.23f1, URP 17.3.
Port source: `C:/Users/benos/Code/ATCK` (read-only).

**Working rule.** Every change to the Unity project goes through MCP for Unity, as a human
would work in the Editor. No hand-written `.unity` / `.prefab` / `.asset` / `.meta` YAML, ever.
`read_console` after every compile; `manage_camera` screenshot after every visual change.

**Agent rule.** One Opus builder at a time, strictly serial (the Editor is one shared resource).
The orchestrator reviews only at milestone gates. M0–M7 run back to back once approved.

> **SCOPE CUT (2026-09-18): `docs/SLICE-1.md` is authoritative and is being built first**: solo,
> offline, the tutorial room plus four rooms, to nail the mechanics. The milestones below (the
> netcode port onward) resume only after the owner approves the feel.

**Status: DRAFT, not approved.** Nothing is built until the owner signs off §11. Where this
document says "the owner's decision / note / M0 list" it means the orchestrator's
recommendation; the owner has so far decided only two things: all Unity work goes through the
MCP as a human developer would do it, and sub-agents run on Opus, one at a time.

**Orchestrator amendments after review (2026-09-18):**
1. **Owner gate after M1.** Zone 1's geometry (gap widths, ledge heights, the anti-skip numbers)
   is derived from the launch tuning. The owner plays the feel box and signs off the feel
   BEFORE M2-M7 run, otherwise a tuning change invalidates the room metrics.
2. **The two-browser WebRTC test moves up to straight after M3** (call it M3b), not M7. ATCK's
   WebRTC path has never been verified with two real peers, so it is the largest technical
   risk and must fail early if it is going to fail. The project is tiny at that point, so the
   platform switch to WebGL and back is cheap. M7 remains the full-game WebGL build.

**Porting method** (orchestrator's recommendation, pending §11 approval): portable ATCK files are **copied
inside the Editor** — one `execute_code` call per batch doing `File.Copy` + textual
`ATCK.` → `Pesky.` replace + `AssetDatabase.Refresh()`. "Adapt" files then get targeted
`script_apply_edits` / `apply_text_edits`. **A ported file is never regenerated from model
output.** New gameplay scripts use `create_script`.

---

## 1. Assemblies

| asmdef | References | Contents |
|---|---|---|
| `Pesky.Data` | — | ScriptableObject schemas + `GameData` catalogue |
| `Pesky.Protocol` | — | `Tick`, `Wire`, `MsgId`, `MessageInfo`, `NetWriter/Reader`, `Frame`, `Headers`, `Enums`, `Messages/*` |
| `Pesky.Transport` | — | `INetTransport`, `LoopbackTransport`, `WebRtcTransport`, `TcpTransport`, `IVoiceControl`, `VoiceMode` |
| `Pesky.Sim` | Data, Protocol | `WorldSim`, tables, `Rng`, `SimHash` |
| `Pesky.Session` | Sim, Protocol, Transport, Data | `NetSession`, router, join flow, outbox/inbox, clock, rules, validators, `Damage` |
| `Pesky.Game` | Session, Sim, Protocol, Data, InputSystem, UI, URP Runtime, **Unity.AI.Navigation** | Views, player, world kit, UI, boot |
| `Pesky.Editor` | all, `includePlatforms:["Editor"]` | `BuildTools`, `PhysicsLayerSetup`, `Zone1Validator` |
| `Pesky.Tests` | all + TestRunner, Editor-only, `overrideReferences`, `nunit.framework.dll`, `autoReferenced:false`, `defineConstraints:["UNITY_INCLUDE_TESTS"]` | EditMode |

**Game must NOT reference Transport** (NETCODE §2). `Session/NetSessionExtensions.cs` is the
Game-facing edge. All runtime asmdefs: `includePlatforms:[]`, `allowUnsafeCode:false`,
`noEngineReferences:false`, `rootNamespace` = asmdef name.

## 2. Assets/ folder tree (final)

```
Assets/
  Data/            GameData.asset
    Weapons/       10 × WeaponDef
    Modifiers/     16 × ModifierDef
    Enemies/       7 × EnemyDef, Hobnail.BossDef
    Rooms/         12 × RoomDef (R1-R11 + S1)
    Zones/         5 × ZoneDef
  Materials/       Grey, Stone, Wood, Straw, Metal, Bronze, Verdigris, Goblin, Lantern,
                   Hazard, Trigger, Banana, BossDisc  (URP/Lit, flat)
  Plugins/WebGL/   AHNet.jslib, PeskyPlatform.jslib
  Prefabs/
    Weapons/  Enemies/  Kit/  World/
  Scenes/          Boot.unity, FeelBox.unity, Zone1.unity
  Scripts/
    Data/  Protocol/{Messages/}  Transport/  Sim/  Session/{Rules,Validators}/
    Game/{Boot,Player,World,Enemies,UI,Debug}/  Editor/
  Tests/EditMode/
  WebGLTemplates/Pesky/  index.html, net.js, trystero.min.js, keys.js
  link.xml
  Settings/        (URP template assets, kept)
```

Folders created with `manage_asset create_folder`. `SampleScene`, `TutorialInfo/`,
`Readme.asset` deleted in M0.

## 3. Scenes and boot flow

| Scene | Contents |
|---|---|
| `Boot.unity` | `Bootstrap` (`[DefaultExecutionOrder(-1000)]`, `DontDestroyOnLoad`), `NetBridge` (WebRtcTransport — **name is the wire contract**), `EventSystem` + `InputSystemUIInputModule` (UI actions bound **by hand**), `UIDocument` root, `ClickToPlayGate` |
| `FeelBox.unity` | 40×40×12 m box, 4 test walls, ramp, wood panel, stone slab. M1 only; kept for regression |
| `Zone1.unity` | R1–R11 + S-1, `NavMeshSurface`, `WorldRoot` anchor |

Flow: `Boot` → `ClickToPlayGate` (first click unlocks audio + pointer lock; load-bearing on
WebGL) → Lobby (host / join / offline, name, room code, weapon select) → `WorldLifecycle`
additively loads `Zone1` under a per-session `WorldRoot` → `Bootstrap.Update()` calls
`Session.Update()` before any view runs.

## 4. Layers and collision matrix

Layers created in the first free user slots by `PhysicsLayerSetup` (idempotent, menu item) and
mirrored in `Game/Boot/Layers.cs`; also via `manage_editor add_layer` as the MCP-visible path.

`8 Ground · 9 Weapon · 10 Enemy · 11 Projectile · 12 Interactable · 13 Debris · 14 Trigger`

| | Ground | Weapon | Enemy | Projectile | Interactable | Debris | Trigger |
|---|---|---|---|---|---|---|---|
| Ground | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| Weapon | | **✓** | ✓ | ✓ | ✓ | ✗ | ✓ |
| Enemy | | | ✓ | ✓ | ✗ | ✗ | ✓ |
| Projectile | | | | ✗ | ✓ | ✗ | ✗ |
| Interactable | | | | | ✗ | ✗ | ✗ |
| Debris | | | | | | ✗ | ✗ |
| Trigger | | | | | | | ✗ |

Weapon × Weapon **on** — deliberate departure from ATCK's Aircraft × Aircraft (NETCODE §9).
Enemy × Interactable **off** so goblins cannot solve mass puzzles. Surface behaviour
(`Stone` chipping, `Wood` stick, `Straw` soft) is a **tag**, not a layer:
tags `Stone`, `Wood`, `Straw` added with `manage_editor add_tag`.

Physics (`manage_physics`): **gravity (0, −20, 0)**, default solver iterations 6,
default material friction 0.4 / bounce 0.0, bounce threshold 2.0.
Time: **fixed timestep 0.02**, max allowed 0.1 (see §11-C5). Run In Background ON.

## 5. Message table (Pesky Weapons)

Kept verbatim from ATCK: `0x01` (re-specified), `0x02 HELLO`, `0x03 PING`,
`0x10 SESSION_INFO`, `0x11 TIME_SYNC`, `0x12 CLOCK_PING`, `0x13 CLOCK_PONG`,
`0x14 JOIN_REQUEST`, `0x15 SNAPSHOT`, `0x16 SESSION_PHASE`, `0x17 PEER_SLOTS`,
`0x18 SESSION_END`, `0x19 RESYNC_REQ`, `0x7F FRAME`. **`0x1A–0x1F` stay reserved for host
migration.** `Wire.ProtocolVersion` resets to **1**; `NoCompany` deleted; `MaxPlayers` 8.
`EventHeader` = `tick u32` at offset 1 (fixed offset, `AircraftHeader` deleted).

| id | Name | Kind | Rate / trigger | Body after type byte | M |
|---|---|---|---|---|---|
| 0x01 | POSE | raw | 20 Hz `SendNow` | `pos i16×3 cm` `rot u32 smallest-three` `vel i16×3 @1/256 m/s` `seq u16` = **19 B** | M3 |
| 0x20 | PLAYER_STATE | Stream | 5 Hz | `slot u8` `flags u8` `weaponId u8` `pitch i16` = 6 B | M3 |
| 0x21 | PLAYER_DEATH | Event | on death | hdr `slot u8` `cause u8` `killer u8` `pos i16×3` | M5 |
| 0x22 | PLAYER_RESPAWN | Event | on respawn | hdr `slot u8` `anvilId u8` `hp u16` `pos i16×3` `tarnish u8` | M5 |
| 0x23 | WEAPON_EQUIP | Event | rack / reforge | hdr `slot u8` `weaponId u8` `hp u16` | M3 |
| 0x24 | PICKUP_REQ | Intent | on touch | `pickupId u16` `tick u16` | M6 |
| 0x25 | PICKUP_GRANT | Event | validated | hdr `slot u8` `pickupId u16` `kind u8` `payloadId u8` | M6 |
| 0x26 | REFORGE_REQ | Intent | hold E 2.5 s / hilt | `anvilId u8` `mode u8` `hiltSlot u8` | M6 |
| 0x27 | REFORGE_STATE | State | on change | `partyAnvil u8` `count u8` rows{`slot u8`,`lastReforgeTick u16`} | M6 |
| 0x28 | HILT_STATE | State | 2 Hz + dirty | `count u8` rows{`slot u8`,`flags u8`,`pos i16×3`} | M6 |
| 0x29 | PING_MARK | Event | Q | hdr `slot u8` `kind u8` `pos i16×3` | M6 |
| 0x2A | JOIN_REFUSED | Reply | version / full | `reason u8` `hostVersion u16` | M2 |
| 0x40 | ENEMY_STATE | State | **10 Hz** (every 2 ticks) + dirty | `count u8` rows{`enemyId u16`,`kindId u8`,`pos i16×3 cm`,`yaw i16`,`hp u16`,`flags u8`,`animState u8`} = 2+15n | M5 |
| 0x41 | ENEMY_EVENT | Event | spawn/attack/stagger/death/duck | hdr `enemyId u16` `kind u8` `attackId u8` `param i16` | M5 |
| 0x42 | BOSS_STATE | State | 10 Hz + dirty | `bossId u16` `phase u8` `hp u16` `attackId u8` `attackStartTick u16` `nextBounceTick u16` `pos i16×3` `yaw i16` = 19 B | M5 |
| 0x43 | BOSS_EVENT | Event | phase / attack start | hdr `bossId u16` `kind u8` `attackId u8` `startTick u16` | M5 |
| 0x50 | HIT_CLAIM | Intent | client impact | `victimKind u8` `victimId u16` `contact i16×3` `closingSpeed u16 @1/256` `hitPart u8` `tick u16` = 15 B | M5 |
| 0x51 | HEALTH_STATE | State | on hp change | `count u8` rows{`victimKind u8`,`victimId u16`,`hp u16`,`flags u8`} | M5 |
| 0x52 | HIT_RESULT | Reply | per claim | `victimId u16` `dmg u16` `reject u8` | M5 |
| 0x53 | HAZARD_CLAIM | Intent | censer / brazier / peel | `hazardId u16` `tick u16` | M5 |
| 0x54 | IMPULSE | Event | knockback to an owner | hdr `slot u8` `impulse i16×3` `cause u8` | M5 |
| 0x60 | DOOR_STATE | State | on change | `doorOpenMask u32` `keyMask u8` `wallBrokenMask u16` | M4 |
| 0x61 | PLATE_STATE | State | 2 Hz + dirty | `count u8` rows{`plateId u8`,`massTenths u16`,`flags u8`} | M4 |
| 0x62 | PLATE_CONTACT | Intent | 10 Hz while resting | `plateId u8` `massTenths u16` | M4 |
| 0x63 | LEVER_STATE | State | on change | `groupId u8` `leverBits u8` `windowStartTick u16` `latched u8` | M4 |
| 0x64 | LEVER_HIT | Intent | impact ≥ N·s | `leverId u8` `impulseNs u16 tenths` `tick u16` | M4 |
| 0x65 | TILE_BREAK | Event | first contact | hdr `tileId u16` `breakTick u16` | M4 |
| 0x66 | TILE_TOUCH | Intent | first contact | `tileId u16` `tick u16` | M4 |
| 0x67 | WALL_STATE | State | on change | `count u8` rows{`wallId u8`,`hp u16`} | M4 |
| 0x68 | WALL_HIT | Intent | impact | `wallId u8` `impulseNs u16` `tick u16` | M4 |
| 0x69 | INTERACT_REQ | Intent | E | `kind u8` `targetId u16` | M4 |
| 0x6A | ROOM_STATE | State | on change | `count u8` rows{`roomId u8`,`flags u8`} | M4 |
| 0x70 | PARTY_STATE | State | 0.1 Hz | `doorMask u32` `keyMask u8` `soulMask u16` `weaponUnlockMask u16` `anvilId u8` `tarnish u8` `zoneId u8` `roomMask u16` = 16 B | M6 |
| 0x71 | MODIFIER_STATE | State | on change | `count u8` rows{`slot u8`,`soulMask u16`,`head u8`,`haft u8`,`grip u8`} | M6 |
| 0x72 | WEAPON_SELECT | Intent | at a Rack | `rackId u16` `weaponId u8` | M3 |
| 0x73 | WEAPON_UNLOCK | Event | banana etc. | hdr `weaponMask u16` | M6 |
| 0x74 | TARNISH | Event | on death timeout | hdr `stacks u8` | M6 |

Free: `0x2B–0x2F`, `0x30–0x3F`, `0x44–0x4F`, `0x55–0x5F`, `0x6B–0x6F`, `0x75–0x7E`.
**36 new ids.** Every `Stream` message carries `slot:u8` first (NETCODE §10.1.7).
`SnapshotPartKind.Order` = `Players, Enemies, Boss, Doors, Plates, Pickups, Party, End`.

Bandwidth check: 4 players × 20 Hz × 19 B = 1.1 kB/s each way per peer; 12 goblins at 10 Hz =
`(2 + 12×15) × 10` = 1.8 kB/s per client. Well inside the 16,000 B FRAME ceiling; a
`FrameSizeTests` case asserts a 255-row `ENEMY_STATE` still fits.

## 6. ScriptableObjects

| Type (asmdef `Pesky.Data`) | Fields (one line) | Instances |
|---|---|---|
| `WeaponDef` | mass, dmgMult, S, L, handling H, hp, angle profile (point/edge/head/flat), stickChance, bounciness, businessAxis, body prefab, unlockBit | 10 (DESIGN §4) |
| `ModifierDef` | slot (Head/Haft/Grip/Soul), stat deltas, hook id, isGate, pickup prefab | 16 (DESIGN §5) |
| `EnemyDef` | hp, dmg, speed, aggro/attack radii, telegraph/recover seconds, staggerable, armour mult, prefab | 7 |
| `BossDef` | phase HP thresholds, per-attack {id, telegraph s, radius, dmg, cooldown}, hpBase, hpPerPlayer | 1 (Hobnail) |
| `RoomDef` | bounds min/max, ceiling, doorIds, spawn table, clock phase offsets, `RequiresMass`, `AltSolutionId`, gate list | 12 |
| `ZoneDef` | id, room list, gate ability, boss, next zone | 5 (Zones 2–5 empty stubs) |
| `GameData` | arrays of every def above + id→index tables; the `GameData` type `WorldSim(GameData, seed)` takes | 1 |

All created with `manage_scriptable_object` (requires the `scripting_ext` tool group).

---

## 7. Milestones

### M0 — Project setup

**Goal.** A project that compiles empty, with every tool, package, setting, layer, folder and
asmdef in place, and a git history to roll back to.

| # | Task | Tool |
|---|---|---|
| 1 | `git init`, ATCK-derived `.gitignore` (`Library/ Temp/ Obj/ Logs/ UserSettings/ Build/ *.csproj *.sln* .vs/`), `.gitattributes` (`* text=auto eol=lf`, LFS on `*.fbx *.png *.wav *.mp3 *.psd`), initial commit | Bash (outside the Editor) |
| 2 | Delete the stray root Unity project (§11-O5) **after owner approval** | Bash |
| 3 | Enable MCP tool groups: `scripting_ext`, `probuilder`, `testing`, `ui`, `docs` | `manage_tools` |
| 4 | Add packages: `com.unity.probuilder`, confirm `com.unity.ai.navigation` 2.0.14, Input System 1.20, Test Framework | `manage_packages` |
| 5 | Delete `SampleScene`, `TutorialInfo/`, `Readme.asset` | `manage_asset` |
| 6 | Create the §2 folder tree | `manage_asset create_folder` |
| 7 | Layers 8–14 + tags `Stone/Wood/Straw` | `manage_editor add_layer` / `add_tag` |
| 8 | Gravity −20, fixed timestep 0.02, max 0.1, bounce threshold 2.0, collision matrix per §4 | `manage_physics`, `manage_editor` |
| 9 | Run In Background ON, colour space (§11-O6), API compat .NET Standard 2.1, active input handler = new only | `manage_editor` |
| 10 | Create the 8 asmdefs with the §1 reference graph | `create_script` (`.asmdef` via `manage_asset`) + `execute_code` for the JSON bodies |
| 11 | Create `Boot.unity`, `FeelBox.unity`, `Zone1.unity`; add to build settings | `manage_scene`, `manage_build` |
| 12 | 13 flat URP/Lit materials | `manage_material` |
| 13 | Commit `M0: project setup` | Bash |

**Acceptance.** `read_console` clean after refresh; `manage_editor get_state` shows the layers,
gravity −20 and fixed timestep 0.02; all 8 asmdefs present; three scenes in build settings.
**Risk:** ProBuilder may not resolve on Unity 6000.3. **Fallback:** primitives only (DESIGN §4
gives a primitive equivalent for every weapon) and drop the `probuilder` group.
**Build target stays StandaloneWindows64** through M6 — M3's TcpTransport test needs a Windows
player and each switch is a full reimport. WebGL *player settings* are written here by
`BuildTools`; the *active target* switches once, in M7.

### M1 — Feel box (the make-or-break day)

**Goal.** One weapon, one empty box, offline, tuned until launching is fun. Nothing else exists.

| # | Task | Tool |
|---|---|---|
| 1 | `WeaponDef` schema + `RustyArmingSword.asset` | `create_script`, `manage_scriptable_object` |
| 2 | Build the sword body prefab from primitives (1.0×0.12×0.04 box + cross-guard + pommel sphere), Rigidbody mass 3, drag 0.15 / angular 0.35, layer Weapon | `manage_gameobject`, `manage_components`, `manage_prefabs` |
| 3 | Scripts: `WeaponBody`, `WeaponMotor`, `LaunchInput`, `OrbitCamera`, `StickAnchor`, `TuningPanel` | `create_script` |
| 4 | Assemble `FeelBox` (40×40×12 box, 4 walls, ramp, wood panel, stone slab, straw) | `manage_probuilder` / `manage_gameobject` primitives |
| 5 | Input actions asset: Move, Look, Launch, Interact, Ping, Aim, Party | `manage_asset` + Input System window via `execute_code` |
| 6 | Play mode; tune S / L / H / retain 0.80 / flight-bias gain live | `manage_editor play`, `manage_camera` screenshot |

**Scripts (new, `Pesky.Game`):** `WeaponBody` (owner state: grounded test + 0.12 s coyote,
wallCharge, gustCharge, stick), `WeaponMotor` (shuffle force/torque, launch impulse per DESIGN
§2, air control, wall rebound with the 3.0 m/s incoming check, flight-bias torque), `LaunchInput`
(`d̂` sampled over 0.10 s), `OrbitCamera` (4.0→6.0 m boom, FOV 65→78, damped 0.08 s, spherecast
0.3 m, never inherits tumble), `StickAnchor` (wood embed, 2.5 s hang, resets wallCharge),
`TuningPanel` (live sliders).

**Acceptance.** In play mode: flat range 6.0 ± 0.3 m and apex 0.90 ± 0.1 m for the sword;
exactly one wall rebound per airborne period; a rebound refused when resting against a wall
(incoming < 3 m/s); camera never spins. Verified by `manage_editor play` + 6 `manage_camera`
screenshots at fixed ticks + a `Debug.Log` trace read with `read_console`.
**Risk:** the core loop is not fun. **Fallback:** stop the one-shot here and re-tune — the owner
wants this gate before anything else is built.

### M2 — Netcode port compiles, loopback session, lobby

**Goal.** The ATCK stack lives in `Pesky.*`, compiles, and a solo host reaches `Playing`
through `LoopbackTransport`, driven from a working lobby.

| # | Task | Tool |
|---|---|---|
| 1 | Copy pass 1 — Transport (6), Protocol copies (`Tick`, `Frame`, `NetWriter/Reader`), `link.xml`: `File.Copy` + `ATCK.`→`Pesky.` + `AssetDatabase.Refresh()` | `execute_code` (1 call) |
| 2 | Copy pass 2 — Session copies (`Inbox`, `InboxItem`, `InboxKind`, `Outbox`, `PendingEvents`, `PeerSlots`, `RoomClock`, `ClockPinger`, `SnapshotReceiver`, `IHostRule`, `IIntentValidator`, `NetSessionExtensions`, `SessionRouter`, `Rules/ClockRule`), Sim (`Rng`, `SimHash`), Game (`RoomCodes`, `Clipboard`) | `execute_code` |
| 3 | Copy pass 3 — Tests (`InMemoryTransport`, `InMemoryMesh`, `OutboxTests`, `RoomClockTests`, `ProtocolTestUtil`), Editor (`BuildTools`, `PhysicsLayerSetup`), browser files | `execute_code` |
| 4 | Adapt `Wire` (version→1, drop `NoCompany`), `Headers` (drop `AircraftHeader`), `Enums` (keep 9, drop ~30), `MsgId` (§5), `MessageInfo.BuildTable`, `Appearance` | `script_apply_edits` |
| 5 | New Sim: `WorldSim` (the exact NETCODE §1.4 surface, `SetPlayerPose(slot,pos,rot,vel)`), `PlayerTable`/`PlayerState`, `EnemyTable`, `DoorTable`, `PickupTable`, `PlateTable`, `BossState` | `create_script` |
| 6 | Adapt `NetSession` (cut `StartGame(ShiftKind)`, `SetAirportName`, `Resume`, `SharedPot`), `JoinFlow` (+`JOIN_REFUSED`), `MessageApplier`, `EventSink`, `HostAuthority.CreateDefault`, `HostIds`, `SnapshotCodec.Order`, `TransportFactory` | `script_apply_edits` |
| 7 | Adapt `SessionMessages` (strip airfield fields, add `zoneId`/`roomId`); `Damage.cs` rewritten for weapon HP | `create_script` + edits |
| 8 | Adapt `Bootstrap`, `WorldLifecycle`, `Layers`, `ClickToPlayGate`, `GameLocator` | edits |
| 9 | UI Toolkit in code: `LobbyUI` (name / HOST / JOIN / OFFLINE / room code + copy / weapon select), `HudUI` skeleton | `create_script` |
| 10 | Adapt `SessionTests`; add `PoseCodecTests`, `FrameSizeTests` | `create_script`, `run_tests` |

**Acceptance.** `run_tests EditMode` green, including the ported multi-peer harness: solo host
reaches ready; a second in-memory peer joins mid-session and ends with the **same `SimHash`**;
a third joining later matches; host loss ends the session. Then `manage_editor play`, press
OFFLINE, and `read_console` shows `phase=Playing`, `slot=0`, no errors.
**Risk:** the seam list is longer than estimated and the builder starts rewriting ported files.
**Fallback:** hard rule — a ported file that will not compile is fixed by deleting the ATCK-only
member, never by regeneration; if a file resists, skip it and stub the seam.

### M3 — Weapon roster, pose streaming, two-instance TcpTransport test

**Goal.** Four playable weapons, the 19-byte POSE on the wire at 20 Hz, remotes interpolated
100 ms behind, and two real instances connected without a browser.

| # | Task | Tool |
|---|---|---|
| 1 | `NetWriter.Quat()` / `Cm()` / `PosCm()` and the `NetReader` mirrors (smallest-three, NETCODE §6.2) | `script_apply_edits` |
| 2 | `M0Messages` → `POSE` (0x01, 19 B); `SessionRouter.AcceptPose` and `NetSession.SendNow` follow the new signature | edits |
| 3 | `PoseStreamer` adapt: SendHz 20, StateHz 5, MoveEps 0.01 m, **RotEps 1.0°** via `Quaternion.Angle`, **VelEps 0.25 m/s**, keepalive 1 s | edits |
| 4 | `RemoteWeapon` (from `RemoteCharacter`): keep the sample ring + `InterpDelay 0.10f`; `Slerp` rotation, **cubic Hermite** position from streamed velocity, extrapolate ≤250 ms on stall then freeze; **kinematic** Rigidbody via `MovePosition`/`MoveRotation` | edits |
| 5 | `RemoteWeaponSpawner` (from `RemotePlayerSpawner`), fed from the sim row each frame | edits |
| 6 | 10 `WeaponDef` assets + 10 body prefabs (4 playable, 6 locked) | `manage_scriptable_object`, `manage_prefabs`, `batch_execute` |
| 7 | `WeaponFactory` (builds a body from a `WeaponDef`), `WeaponRack` prefab + `WEAPON_SELECT` intent | `create_script`, `manage_prefabs` |
| 8 | New `Transport/TcpTransport.cs` (~200 lines: host `TcpListener`, length-prefixed frames, host relays `Broadcast`, peer ids `host`/`p1`…), `TransportFactory` branch behind `PESKY_LAN` | `create_script` |
| 9 | Build a Windows player with `PESKY_LAN`; run Editor (host) + player (client) | `manage_build` |

**New scripts:** `WeaponFactory`, `TcpTransport`, `NetOverlay` (F3: per-peer clock offset, RTT,
pose Hz), plus the adapts above.
**Prefabs (10):** `W_Sword, W_Dagger, W_Rapier, W_Quarterstaff, W_BoneWand, W_Orb, W_Mace,
W_BallChain, W_Warhammer, W_Banana` — each: body primitives, `Rigidbody`, `Collider(s)`,
`WeaponBody`, `WeaponMotor`, `PoseStreamer`, `ImpactReporter`, layer Weapon.

**Acceptance.** Editor hosts, Windows player joins by `127.0.0.1:port`; each sees the other's
weapon tumble; `NetOverlay` shows POSE at 20 ± 1 Hz and clock offset stable under ±25 ms; the
remote body is kinematic (asserted in `read_console`). Verified by two `manage_camera`
screenshots plus console traces. **Risk:** TcpTransport is new code raising events off the main
thread. **Fallback:** it only has to satisfy `INetTransport`'s 8 members and push into the
lock-protected `Inbox`; if it misbehaves, revert to `InMemoryMesh` and defer all two-peer
verification to M7, declared at the gate.

### M4 — World kit prefabs, Zone 1 grey-box, NavMesh

**Goal.** Every kit prefab exists, Zone 1 is laid out on the 2 m grid exactly as DESIGN §6
specifies, and the NavMesh bakes.

| # | Task | Tool |
|---|---|---|
| 1 | Kit scripts (below) | `create_script` |
| 2 | Kit prefabs from primitives / ProBuilder | `manage_probuilder`, `manage_gameobject`, `manage_components`, `manage_prefabs` |
| 3 | 12 `RoomDef` assets with bounds, doors, gates, spawn tables | `manage_scriptable_object` |
| 4 | Lay out R1…R11 + S-1 per §9 | `batch_execute` of `manage_gameobject` create+transform |
| 5 | World messages 0x60–0x6A + `DoorRule`, `TileRule`, the four intent validators | `create_script` |
| 6 | `NavMeshSurface` on R4/R5/R8/R11 floors, agent radius 0.4, height 1.2, step 0.4, slope 30°; bake | `manage_components`, `execute_code` (`NavMeshSurface.BuildNavMesh`) |
| 7 | `Zone1Validator` editor check: every gate rise ≥ 3.5 m, every mandatory gap ≤ 3.2 m, every `RequiresMass` room has an `AltSolutionId` | `create_script`, `execute_menu_item` |

**New scripts — `Pesky.Game/World`:** `ClockMover` (pure function of room ms; every scheduled
mover derives from it), `PressurePlate` (+ ratchet + twin modes), `ImpactLever`, `KeyedDoor`,
`KeyPickup`, `ModifierPickup`, `ReforgeAnvil`, `WeaponRack`, `CrumbleFloor`, `RespawnPad`,
`BreakableWall`, `SwingingCenser`, `StickyPanel`, `WindColumn`, `BananaPeel`, `RoomVolume`,
`Portcullis`. **`Pesky.Session`:** `Rules/DoorRule`, `Rules/TileRule`,
`Validators/{PlateContact, LeverHit, TileTouch, WallHit, Interact}Validator`.

**Prefabs (18).** Each is primitives/ProBuilder + collider + the named script, layer
Interactable: `K_PressurePlate` (slab + 4 posts + trigger), `K_RatchetPlate` (+ pawl),
`K_ImpactLever` (0.8 m cylinder + HingeJoint), `K_ClockPlatform` (3×3×0.4 kinematic cube +
`ClockMover`), `K_CrumbleTile` (1.2×1.2×0.2), `K_KeyedDoor` (2.4×3.0×0.6 slab + trigger),
`K_BreakableWall` (6-piece cluster), `K_StickyPanel` (tag Wood), `K_WindColumn`, `K_Censer`
(sphere + 5 m arm + `ClockMover`), `K_KeyPickup`, `K_ModifierPickup`, `K_Anvil` (+ `RespawnPad`),
`K_WeaponRack`, `K_Portcullis`, `W_OakTable` (tag Wood), `W_StrawPile` (tag Straw), `W_Plinth`.

**Acceptance.** `Zone1Validator` passes with zero failures; NavMesh bakes on all four rooms
(`read_console` reports the bake); a `manage_camera` top-down screenshot of `Zone1` matches the
DESIGN §6 map room-for-room; in play mode one weapon walks the whole critical path with doors
forced open and never falls out of world.
**Risk:** hand-placing ~400 pieces by MCP calls is the biggest token sink in the one-shot.
**Fallback:** one `batch_execute` per room driven by the `RoomDef` bounds; if the budget bites,
build R1–R5 + R8–R11 and defer R6/R7 (both optional) to M6.

### M5 — Enemies, combat, boss

**Goal.** Host-only NavMesh enemies streamed at 10 Hz, one `Damage` module, and Hobnail through
all three phases.

| # | Task | Tool |
|---|---|---|
| 1 | Messages 0x40–0x43, 0x50–0x54 + `MessageInfo` + `MessageApplier` cases + snapshot parts `Enemies`, `Boss` | `create_script`, edits |
| 2 | `EnemyDef` ×7, `BossDef` ×1 | `manage_scriptable_object` |
| 3 | Enemy prefabs + boss prefab | `manage_prefabs` |
| 4 | `EnemySpawnRule`, `EnemyAiRule` (the `TowerRule` `_nextStateTick` + dirty pattern, 10 Hz), `EnemyHostAgent`, `BossRule`, `HitValidator`, `PoseHistory`, `Damage` finish | `create_script` |
| 5 | `EnemyView`, `BossView`, `TelegraphDecal` — clients interpolate, **never** run `NavMeshAgent` | `create_script` |
| 6 | `ImpactReporter` on every weapon: local contact → `HIT_CLAIM` with victim id, closing speed, hit part, contact point, tick | `create_script` |
| 7 | Register everything in `HostAuthority.CreateDefault()` in the documented order | edits |
| 8 | `HitValidatorTests`, `EnemyMessageTests` | `run_tests` |

**Scripts:** `Rules/{EnemySpawnRule, EnemyAiRule, BossRule}`, `Validators/HitValidator`,
`PoseHistory` (250 ms ring of every body's pose, host-side), `Enemies/{EnemyHostAgent, EnemyView,
BossView, TelegraphDecal}`, `Player/ImpactReporter`.
**Prefabs (5):** `E_ScrapGoblin` (0.8 m capsule + 2 cube ears, `NavMeshAgent`, `EnemyView`,
trigger collider, layer Enemy), `E_LanternGoblin` (+ 0.25 sphere lamp), `E_CrockeryHulk` (2.2 m
barrel + 2 cylinder arms, spigot child collider), `E_ForkMob` (3 thin boxes — **stub**),
`B_Hobnail` (2.4 m torso + 2 shoulders + 2 legs + 2 arms + **1.2 m belly disc child at y=2.6**,
`NavMeshAgent`, `BossView`).

**Acceptance.** Host + one TCP client: 8 goblins in R4 die to thrown weapons; `HIT_CLAIM` from
the client is accepted only when the contact point is within 1.5 m of the host's interpolated
pose at that tick and `closingSpeed ≤ 1.30 × weapon.S` (both rejection paths asserted via
`HIT_RESULT` in `read_console`); D-D opens on clear; Hobnail runs 3 phases, the belly disc reads
×4.0 above 12 m/s, and the Phase 3 charge stuns him against the wall. Verified with screenshots
at each phase change plus the EditMode validator tests.
**Risk:** the host sees every event 3 ticks (150 ms) before clients (NETCODE §5.5), so a
telegraph rendered "now" on the host desyncs from the clients' view.
**Fallback / rule:** every view renders from `attackStartTick`, never from local time — the host
included. Asserted by a test comparing two peers' telegraph start ticks.

### M6 — Modifiers, reforge, racks, keys, respawn, late-join snapshot

**Goal.** The progression layer: pickups, sockets, Soul Slot gate abilities, death/wisp/hilt,
and a late joiner who arrives correctly equipped.

| # | Task | Tool |
|---|---|---|
| 1 | Messages 0x24–0x29, 0x70–0x74 | `create_script` |
| 2 | 16 `ModifierDef` assets; 5 with placed pickups (Wind, Fire, Grip Cloth, Metal, Scope) | `manage_scriptable_object`, `manage_prefabs` |
| 3 | `PickupValidator` (adapt), `InteractValidator`, `ReforgeRule`, `RespawnRule` (adapt), `PartyStateRule` | `create_script` |
| 4 | `ModifierRuntime` (applies stat deltas + hooks to `WeaponMotor`), `SoulWisp`, `HiltPickup` | `create_script` |
| 5 | Snapshot parts `Doors`, `Plates`, `Pickups`, `Party` in `WritePart`/`ReadPart` + `SnapshotCodec.Order` | edits |
| 6 | `HudUI` complete (HP, 3 sockets + Soul Slot, keys, crew list, room code, room-name toast), `DeathOverlay`, `PartyPanel`, `PingMarker` | `create_script` |
| 7 | Place every Zone 1 pickup per §9; finish R6 and R7 if deferred from M4 | `manage_gameobject` |

**Prefabs (4):** `P_SoulWisp` (0.35 m sphere), `P_Hilt` (0.4 m capsule, trigger),
`P_BananaPeel` (flat disc trigger), `P_PingMarker`.

**Acceptance.** Two peers: one dies, becomes a wisp, the other carries the Hilt to RA-2 and
reforges them at 100%; the party gains no Tarnish. Then a **third peer joins mid-session** and
`read_console` shows it received `Players, Enemies, Boss, Doors, Plates, Pickups, Party, End`
parts, spawns at the party anvil with a Rusty Arming Sword, every party Soul ability, empty
sockets and 6 s invulnerability, and its `SimHash` matches the host's.
**Risk:** a one-shot `Removed` State row is missed and a late joiner sees a stale world
(NETCODE §10.1.9). **Fallback:** nothing is removed by a one-shot row — dead enemies keep a row
with the dead flag until the next snapshot, and removals are Events.

### M7 — WebGL build and the two-browser test (the early-risk milestone)

**Goal.** Prove what ATCK never proved: two real browsers in one room, seeing each other tumble.

| # | Task | Tool |
|---|---|---|
| 1 | Switch the active build target to WebGL | `manage_build` |
| 2 | `net.js`: change `APP_ID` and `ROOM_PREFIX`; **wire `iceServers()` to read `window.PESKY_ICE_SERVERS`** with the constant as fallback (the dead hook, NETCODE §3.4) | `execute_code` |
| 3 | `index.html`: retitle, keep the script order `keys.js → trystero.min.js → net.js → loader` and `window.AH_UnityInstance`; define `window.PESKY_ICE_SERVERS` | `execute_code` |
| 4 | `BuildTools.ApplyPlayerSettings`: Brotli + fallback, runInBackground, OpenGLES3 only, threadsSupport false, `FullWithStacktrace`, strip Low / OptimizeSize, dataCaching, template `PROJECT:Pesky`, memory 32→2048 MB geometric | `execute_menu_item` |
| 5 | Verify `link.xml` covers Rigidbody, every collider, joints, PhysicsMaterial, `UnityEngine.AIModule` | `manage_asset` |
| 6 | Build; check console | `manage_build`, `read_console` |
| 7 | Serve over HTTP, two tabs, host + join by room code | outside the Editor |

**Acceptance.** Two browser tabs join by room code; both render the other weapon tumbling with
< 10 cm platform divergence on the R8 24 s loop; the handshake completes (`SESSION_INFO →
JOIN_REQUEST → PEER_SLOTS → SNAPSHOT ×N → CLOCK_PING`); no `Can't add component` errors; COPY
NETWORK LOG gives a clean ICE diagnosis. Then repeat across two machines.
**Risk:** STUN-only leaves symmetric NAT and hotspots with no path, and the whole WebRTC path is
unverified with two real peers. **Fallback:** the TURN hook plus an owner credential (O3);
failing that, ship TcpTransport for LAN and declare browser multiplayer unverified.

### M8 — Verification pass

**Goal.** Run the DESIGN §11 acceptance test end to end and record what is stubbed.

Tasks: full `run_tests` EditMode; `Zone1Validator`; a scripted two-browser run of the §11
acceptance test with a screenshot at each of its 13 beats; an F3 clock-offset capture at 200 ms
simulated latency; a build-size check against the 40 MB warning; update `docs/` with the message
table and the `ProtocolVersion` bump reason.

**Acceptance.** The DESIGN §11 acceptance test completes: two players, mace + banana, Chipping
Hall → Rebound Shaft → Bronze Key → Refectory (8 goblins, Grip Cloth each) → oven wall (Fire
Rune) → Plate Gallery **both routes** → Scriptorium clock platforms with sub-10 cm divergence →
Verdigris Key → RA-2 → S-1 back to R1 in under 15 s → Gantry → Hobnail dead. One death revived
Hilt-to-Anvil. A third player joins mid-boss into the Bell Alcove with the Wind Rune already in
its Soul Slot. No desync, no soft-lock, under 25 minutes.
**Risk:** a late failure here has no milestone left to absorb it. **Fallback:** the test is
decomposed into 13 independently re-runnable beats; a failing beat is a bug report against the
owning milestone, not a re-plan.

---

## 8. Totals

| | Count |
|---|---|
| Ported files (copy / rename / adapt) | **73** (28 copy-or-rename, 45 adapt) per NETCODE §1 |
| New scripts | **89** (Data 7, Protocol 4, Transport 1, Sim 7, Session 18, Game 47, Editor 1, Tests 4) |
| Prefabs | **37** (10 weapons, 18 kit/world, 5 enemies+boss, 4 player-state) |
| ScriptableObject types | **7** — 54 asset instances (10 weapons, 16 modifiers, 7 enemies, 1 boss, 12 rooms, 5 zones, 1 GameData) |
| New message ids | **36** (+ `0x01 POSE` re-specified; 13 session/M0 ids kept verbatim) |
| Scenes | 3 |
| Layers | 7 (indices 8–14) |

---

## 9. Zone 1 build sheet

World axes +X east, +Z north, +Y up. Floors y = 0 unless noted. All walls 0.4 m thick, door
openings 2.4 × 3.0 m. Everything lands on the 2 m grid.

| Room | Bounds (x, z) · size, h | Doors | Kit prefabs placed | Enemies |
|---|---|---|---|---|
| **R1** Reliquary | −8..8, −8..8 · 16×16, h 9; ceiling hole **H-1** (−6,6) | D-A (0,8) N open; D-Bronze (8,0) E | 4 × `W_Plinth` spawn (±3,±3); `K_Anvil` RA-1 (0,−5); `K_WeaponRack` WR-1 (0,5); `W_StrawPile` (−6,6) | — |
| **R2** Chipping Hall | −4..4, 8..32 · 8×24, h 6; trough z12..30 @ y −4 | D-A (0,8); D-B (0,32); S-1 mouth (−4,26) W | crossings: gaps 2.4 @z14, 3.0 @z20, 3.2 @z26 with a 1.2 m mid-pillar; ramp z12→14, y −4→0; Ribbon pickup (0,21,−4) | — |
| **R3** Rebound Shaft | −6..4, 32..42 · 10×10, h 16 | D-B (0,32) S | 12 ledges 1.6×1.6, rise 0.55, spacing 2.0, y 0.60→6.65 CW spiral; top 3×3 @ y 7.2; final 4.6 m crossing, 0.6 m pillar at 2.3 m; `K_KeyPickup` **Bronze**; H-1 hole | — |
| **R4** Refectory | 8..30, −8..8 · 22×16, h 7 | D-Bronze (8,0) W; D-D (20,8) N | 4 × `W_OakTable` (13,−3)(18,3)(22,−4)(26,2); `K_WeaponRack` WR-1b (10,−6); `K_BreakableWall` oven 120 HP (28,6) → **Fire Rune** | **6 ScrapGoblin** (12,−4)(15,2)(19,−2)(23,4)(26,−5)(28,1); **2 LanternGoblin** (17,6)(25,−6) |
| **R5** Plate Gallery | 12..28, 8..24 · 16×16, h 8 | D-D (20,8); D-E (20,24); C-1 (28,16) E | `K_RatchetPlate` (20,12), 6.0 kg / 1.5 s; 3 × `K_ImpactLever` x=12 @ z 14/18/22, ≥12 N·s in 4 s; `K_BreakableWall` 80 HP (13,20) → **Metal Rune**; C-1 wall 80 HP (28,16) | — |
| **R6** Larder | 28..36, 12..20 · 8×8, h 4 | C-1 (28,16) W | **Banana unlock** (32,16); **Pickled Egg** (30,18); **Googly Eyes** (34,14) | 3 × ForkMob (**stub**) |
| **R7** Twin Sconces | 4..12, 24..32 · 8×8, h 6 | D-F (12,28) E open | 2 × `K_PressurePlate` twin 4.0 kg (6,26)(10,30), 0.4 s window → niche @ y 3.0 → **Sniper Scope**; solo route 5 × `K_CrumbleTile` west wall, y 1.2/1.9/2.6/2.6/3.0, 2.6 m apart | — |
| **R8** Scriptorium | 12..32, 24..44 · 20×20, h 11 | D-E (20,24); D-F (12,28) W; D-Verdigris (20,44) | 4 × `K_ClockPlatform` 24 s loop, phases 0/6/12/18 @ (16,28)(28,28)(16,40)(28,40); 2 × `K_Censer` 65°, 5 m, 3.2 s; island 4×4 @ y 6.5 centre (22,34) + **Verdigris Key**; **Beeswax** (14,26,y 2.0) | — |
| **R9** Undercroft | 14..26, 44..56 · 12×12, h 6 | D-Verdigris (20,44); D-S1 (14,50) W bar-lift; D-G (20,56) arch | `K_Anvil` RA-2 (20,50); `K_WeaponRack` WR-2 (17,53); bar-lift lever | — |
| **R10** Gantry | 14..26, 56..76 · 12×20, h 10; 3 m gantry @ y 4.0 over a 10 m drop, cellar stairs south to R9 | D-G (20,56); D-Bell (20,76) portcullis | 14 × `K_CrumbleTile`; 3 × `K_Censer`; 2 × `K_ClockPlatform`; 4 × `K_StickyPanel` beams | — |
| **R11** Bell Chamber | 7..33, 76..102 · 26×26, h 12 | `K_Portcullis` (20,76), inward always, both ways on kill | 4 ledges 1.8×1.8 @ y 3.5 (11,80)(29,80)(11,98)(29,98); Great Bell r 2.0 (20,89,y 8); Bell Alcove `RespawnPad` (20,77.5); bell → 3rd `K_Anvil` on death | **Hobnail** (20,89); phase 2 summons 2+P ScrapGoblins per Bounce, cap 6 |
| **S-1** | 3 m corridor (14,50) → (−10,50) → (−10,26) → (−4,26) into R2's west wall; clears R3 (x=−6) by 2.5 m | D-S1 two-way once opened | — | — |

Doors are `K_KeyedDoor` instances; `DOOR_STATE`'s `doorOpenMask` bit order is
`D-A, D-B, D-Bronze, D-D, D-E, C-1, D-F, D-Verdigris, D-S1, D-G, D-Bell, H-1`.
`keyMask` bits: `Bronze=0, Verdigris=1`.
**Crockery Hulk has no placement in DESIGN §6** — see §11-O9.

---

## 10. What is stubbed after the one-shot

- Zones 2–5: `ZoneDef` assets and empty folders. No geometry, no bosses 2–5.
- 6 of 10 weapons: def + prefab authored, **locked at every Rack**.
- 11 of 16 modifiers: `ModifierDef` only, no pickup placed, no runtime hook.
- Fork Mob (off-NavMesh flyer), Mopwraith, Ember Imp, Arcane Bookworm: `EnemyDef` only.
  Crockery Hulk: prefab + def exist, unplaced pending O9.
- Kit 12 (bell tone lock). Kit 5 (censer) ships hazard-only, never a gate.
- Bone Wand bolt, Sniper Scope zoom, Squeaky Pommel aggro: hooks declared, unimplemented.
- Voice: dormant in `net.js`. **`primeLocalAddresses` stays** — it is connectivity, not voice.
- Host migration (`0x1A–0x1F` reserved); `PARTY_STATE` is emitted but the `localStorage`
  resume hop is not consumed.
- Audio beyond 4 placeholder SFX; all art; CI workflow, `publish.ps1`, `PlayTest/Harness.cs`.

---

## 11. Open issues / decisions for the owner

**Decisions needed before M0 starts**

| # | Decision |
|---|---|
| O1 | **Approve the copy-inside-Editor porting method.** Needs the `scripting_ext` group on and `execute_code` doing `File.Copy` + namespace find-and-replace inside `Assets/`. If refused, the owner copies the 73 files manually before M2. |
| O2 | **TcpTransport: yes or no.** ~200 lines behind a `PESKY_LAN` define. Yes → M3 gets a real two-instance test with real latency. No → every two-peer claim waits for M7 and M3 degrades to `InMemoryMesh`. |
| O3 | **TURN provider, or accept STUN-only.** M7 revives ATCK's dead `window.<GAME>_ICE_SERVERS` hook. STUN-only means symmetric NAT and phone hotspots have **no path at all**. |
| O4 | **`git init`** — not a repo yet. Plan assumes yes, with a Unity `.gitignore` and a `.gitattributes` adding LFS (ATCK has neither). |
| O5 | **The stray root Unity project.** `C:/Users/benos/Code/Pesky Weapons/` holds `Assets/` (empty), `Library/`, `Logs/`, `Temp/`, `UserSettings/` and `ProjectSettings/ProjectVersion.txt` = `UnknownUnityVersion` — an accidental shell wrapping the real project. Approve deleting all six before `git init`. |
| O6 | **Colour space.** ATCK's `BuildTools` sets **Gamma**; the URP 17.3 template ships **Linear**. Pick now — changing later reimports every texture. |
| O7 | **Enemy state rate** 10 Hz (owner + DESIGN) vs 5 Hz (NETCODE). Plan uses 10 Hz; confirm the extra ~900 B/s per client. |
| O8 | **ProBuilder** is not installed and its tool group is off, but DESIGN specifies ProBuilder solids. Install, or accept primitive-only grey-box. |
| O9 | **Crockery Hulk has no home.** DESIGN §11 ships it; DESIGN §6 places it in no room. Proposal: one Hulk as a mid-boss on the R10 gantry. |
| O10 | **Voice: keep or cut** (~260 lines). Plan keeps it dormant; `primeLocalAddresses` stays either way. |
| O11 | **Build target timing.** Plan holds StandaloneWindows64 to M6 and switches once in M7 (M3 needs a Windows player; a switch is a full reimport). The owner's M0 list said "WebGL platform switch" — confirm the deferral. |
| O12 | **UI.** DESIGN §11 says uGUI; the owner's decision says UI Toolkit in code. Plan follows the owner. |
| O13 | ATCK at `C:/Users/benos/Code/ATCK` must stay present and unmodified for the whole one-shot. |

**Conflicts found between DESIGN.md and NETCODE-PORT.md** (resolution rule applied: NETCODE wins
on networking, DESIGN wins on gameplay — each is flagged, not silently resolved)

| # | DESIGN | NETCODE-PORT | Plan |
|---|---|---|---|
| C1 | §7: tick is **60 Hz** | §1.2: **50 ms / 20 Hz** | **20 Hz** (NETCODE). DESIGN §11's "150 ms apply delay" = `LeadTicks 3 × 50 ms`, so §7 reads as a slip. |
| C2 | §11.4: pose stream **15 Hz** | §6.2: **20 Hz** | **20 Hz** (NETCODE). |
| C3 | §8: enemies **10 Hz** + separate 3 Hz HP, 14 B row | §5.6: **5 Hz**, 15 B row, hp inside | **10 Hz, 15 B row**, no separate HP stream. Follows the owner's note, which overrides NETCODE here — see O7. |
| C4 | §7: median of **32** samples, ±1 tick per 30 | `RoomClock`: median of **8**, 1 s coarse re-seed, **no rate limiter** | **RoomClock verbatim.** The ±1/30 clamp does not exist in the ported code; adding it is ~10 new lines — not planned. |
| C5 | §12.4: fixed timestep **0.02** | §9: **0.05**, "you may want 0.02" | **0.02** (DESIGN). Documented consequence: `fixedDeltaTime` and `Tick.Ms = 50` are now decoupled. |
| C6 | §10: each client simulates the other bodies it sees | §6.2: remotes **must be kinematic** | **Kinematic remotes** (NETCODE). The restitution-0.4 bounce becomes one-sided — each owner bounces off the other's kinematic view. No puzzle depends on it. |
| C7 | §7/§9: all peers render a telegraph from the same tick | §5.5: the **host applies events 150 ms early** | Rule: every view renders from `attackStartTick`, host included. Tested at M5. |
| C8 | §10: the Q ping is a **6-byte** event | `EventHeader` u32 + slot + kind + cm pos | `PING_MARK` is 13 B. No gameplay impact. |
| C9 | §12.5: host loss → `localStorage` resume blob | §4.3: **no migration**, `SESSION_END(HostLeft)` | `PARTY_STATE` is emitted; the `localStorage` hop is **stubbed** (§10). NETCODE's file list has nothing for it. |
| C10 | §11.7: kit 5 (censer) "can slip" | — | R8 (critical path) and R10 both place censers. Ships in M4 as a hazard only; no gate depends on it. |

---

*End of plan. M0–M7 run back to back on approval; M8 is the verification gate.*
