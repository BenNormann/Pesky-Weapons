# NETCODE-STATUS

What has actually been ported from ATCK's multiplayer stack into
`Pesky Weapons Unity`, stage by stage. The plan is `docs/NETCODE-PORT.md`;
this file is the record of what exists.

**Everything here is implemented, untested.** Nothing was run: no play mode, no
EditMode tests, no browser. The only checks made are "the whole project compiles
with a clean console" and reading the code back.

---

## Stage 1 — the engine is over and it compiles (2026-09-20)

Source: `C:/Users/benos/Code/ATCK/ATCK Unity/Assets/`, read only.
Destination: `C:/Users/benos/Code/Pesky Weapons/Pesky Weapons Unity/Assets/`.

Every C# file was copied **inside the Editor** (`execute_code` → `File.Copy` /
read-replace-write), with a textual `ATCK` → `Pesky` rename, and Unity generated
the `.meta` files on refresh. No ported file was retyped through the model; the
"adapted" files were then cut with `apply_text_edits`. New files
(`WorldSim`, `PlayerState`, `PlayerTable`, `GameData`) were written with
`create_script`.

### 1. Assembly definitions as built

```
Pesky.Data        refs: (none)
Pesky.Protocol    refs: (none)
Pesky.Transport   refs: (none)
Pesky.Sim         refs: Pesky.Data, Pesky.Protocol
Pesky.Session     refs: Pesky.Data, Pesky.Protocol, Pesky.Transport, Pesky.Sim
Pesky.Game        refs: Pesky.Data, Pesky.Protocol, Pesky.Sim, Pesky.Session,
                        Unity.InputSystem, Unity.TextMeshPro, Unity.AI.Navigation,
                        Unity.RenderPipelines.Universal.Runtime,
                        Unity.RenderPipelines.Core.Runtime
                        -- NEVER Pesky.Transport
Pesky.Editor      refs: Data, Protocol, Transport, Sim, Session, Game   [Editor]
Pesky.Tests       refs: Data, Protocol, Transport, Sim, Session, Game,
                        UnityEngine.TestRunner, UnityEditor.TestRunner   [Editor]
                        overrideReferences, nunit.framework.dll,
                        autoReferenced:false, defineConstraints UNITY_INCLUDE_TESTS
```

Every runtime asmdef: `includePlatforms: []`, `excludePlatforms: []`,
`allowUnsafeCode: false`, `autoReferenced: true`, `noEngineReferences: false`,
`rootNamespace` equal to the name. `Pesky.Transport` carries **no** platform
constraint — the WebGL split is `#if UNITY_WEBGL && !UNITY_EDITOR` inside
`WebRtcTransport.cs`.

The **Game-cannot-name-a-transport wall is kept**. `NetSessionExtensions` is the
Game-facing edge (`StartOnline` / `StartOffline` / voice by plain `int`).

New folders: `Assets/Scripts/{Protocol,Protocol/Messages,Transport,Sim,Session,Session/Rules}`,
`Assets/Plugins/WebGL`, `Assets/WebGLTemplates/Pesky`.

### 2. Every file brought over

`Scripts/…` paths are relative to `Assets/` on both sides.

#### Transport — copied verbatim (namespace rename only)

| Source | Destination | Action | Cut |
|---|---|---|---|
| `Scripts/Transport/INetTransport.cs` | `Scripts/Transport/INetTransport.cs` | copied | — |
| `Scripts/Transport/IVoiceControl.cs` | same | copied | — (dormant) |
| `Scripts/Transport/VoiceMode.cs` | same | copied | — (dormant) |
| `Scripts/Transport/LoopbackTransport.cs` | same | copied | — |
| `Scripts/Transport/WebRtcTransport.cs` | same | copied | —. `GameObjectName = "NetBridge"`, the `Debug.Assert` on it and every `AHNet_*` DllImport name are untouched. |
| `Scripts/Transport/ATCK.Transport.asmdef` | `Scripts/Transport/Pesky.Transport.asmdef` | rewritten | — |

#### Protocol

| Source | Destination | Action | Cut |
|---|---|---|---|
| `Scripts/Protocol/Tick.cs` | same | copied | — (50 ms / 20 Hz) |
| `Scripts/Protocol/NetWriter.cs` | same | copied | — (no `Cm`/`Quat` yet — stage 2) |
| `Scripts/Protocol/NetReader.cs` | same | copied | — (same) |
| `Scripts/Protocol/Frame.cs` | same | copied | — (0x7F, 16,000-byte ceiling) |
| `Scripts/Protocol/Appearance.cs` | same | copied | — kept as **six opaque bytes**; the field names are still ATCK's body/hair/face/colour. Reinterpret in a later stage; the plumbing does not care. |
| `Scripts/Protocol/M0Messages.cs` | same | copied | — `TRANSFORM` is still ATCK's 19-byte `pos f32×3 + yaw f32 + seq u16`. **Stage 2 respecifies it.** |
| `Scripts/Protocol/Wire.cs` | same | adapted | `NoCompany` deleted. `ProtocolVersion` **reset to 1**. `MaxPlayers` left at **8** (matches 3-8 players). `NoSlot`, `NoId`, `MaxBatch`, `HasId`, `BatchCount` unchanged. |
| `Scripts/Protocol/Headers.cs` | same | adapted | `AircraftHeader` deleted. Only `EventHeader` (u32 tick) remains, which is why `MessageApplier.TryEventTick` is now a fixed offset of 1. |
| `Scripts/Protocol/MsgId.cs` | same | adapted | 0x01-0x03 (M0), 0x10-0x19 (session/clock) and 0x7F (FRAME) kept verbatim; the 0x1A-0x1F host-migration reservation kept. **All of 0x20-0x7E deleted** and replaced by a comment reserving players 0x20-0x2F, enemies 0x40-0x4F, world 0x50-0x5F. |
| `Scripts/Protocol/MessageInfo.cs` | same | adapted | `BuildTable()` now has the ten session/clock rows plus `MsgId.Frame`. Every ATCK gameplay row deleted. |
| `Scripts/Protocol/Enums.cs` | same | adapted | Kept `MsgKind`, `SessionPhase`, `VoiceFlags`. Rewritten: `SessionEndReason` (HostEnded, HostLeft, **Escaped**, **CrewLost**), `SnapshotPartKind` (**World=0, Players=1, End=2**), `VictimKind` (Weapon/Enemy/Prop/World), `HitRejectReason`, `DeathCause` (Impact/Enemy/Fall/Hazard/OutOfBounds), `PlayerFlags` (Charging/Airborne/Soul/Still). Deleted: `ShiftKind`, `RunModifier`, `DayPhase`, `DayEventKind`, `PlaneKind`, `AircraftPhase`, `PhaseCause`, `CollisionOutcome`, `DestroyCause`, `LandedOutcome`, `DespawnReason`, `AtcVerb`, `AtcResult`, `AtcPlaneFlags`, `Subscription`, `RunwayState`, `HitPart`, `ExplosionKind`, `EconReason`, `SlotStatus`, `EconAccount`, `PurchaseCatalogue`, `StructureKind`, `PurchaseResult`, `TowerState`, `BreakageKind`, `SpinMachine`, `SpinTier`, `UpgradeResult`, `AircraftStatus`, `AcDestroyFlags`. |
| `Scripts/Protocol/Messages/SessionMessages.cs` | same | adapted | `TimeSyncMsg`, `ClockPingMsg`, `ClockPongMsg`, `JoinRequestMsg`, `SnapshotPartMsg`, `PeerSlotsMsg`, `SessionEndMsg`, `ResyncReqMsg` copied byte for byte. `SessionInfoMsg` stripped of `runwayCount`, `layoutId`, `day`, `dayPhase`, `shiftDays`, `hazard`, `modifiers`, `promoted`, `airportName`; it now carries `protocolVersion`, `worldSeed`, `phase`, **`floorId:u8`**, `phaseStartTick`. `SessionPhaseMsg` stripped of `shiftDays`, `hazard`, `modifiers`, `promoted`; it now carries `phase` + `floorId`. |
| `Scripts/Protocol/ATCK.Protocol.asmdef` | `Scripts/Protocol/Pesky.Protocol.asmdef` | rewritten | — |
| all other `Scripts/Protocol/Messages/*.cs` (~1,900 lines) | — | **skipped** | ATCK gameplay |

#### Sim

| Source | Destination | Action | Cut |
|---|---|---|---|
| `Scripts/Sim/Rng.cs` | `Scripts/Sim/Rng.cs` | copied | — seeded xorshift32 |
| `Scripts/Sim/SimHash.cs` | same | copied | — FNV-1a 64 |
| `Scripts/Sim/PlayerState.cs` | same | **rewritten** (ATCK copy discarded) | A weapon row: identity (`present`, `name`, `peerId`, `isHost`), owner stream (`pos`, `yaw`, `flags`, `voice`) and host facts (`hp`, `alive`, `deathTick`, `deathCause`, `lastAttackerSlot`, `respawnTick`) with `Write`/`Read`. Gone: the three weapon slots and sidearm rules, `activeWeapon`, `pitch`, `respawnPad`, `lastSpin`, `AtAtcMap`. |
| `Scripts/Sim/PlayerTable.cs` | same | **rewritten** | Slot array, indexer, `PresentCount`, `SetPose`, `Apply(PeerSlotsMsg)`, `Write`/`Read`. Gone: everything economy, weapon, spin, health, airfield and crater. No longer takes a `WorldSim`. |
| `Scripts/Sim/WorldSim.cs` (+ every other Sim file) | — | **skipped; a new minimal `WorldSim.cs` written instead** | See section 4. |
| `Scripts/Sim/ATCK.Sim.asmdef` | `Scripts/Sim/Pesky.Sim.asmdef` | rewritten | — |

#### Session

| Source | Destination | Action | Cut |
|---|---|---|---|
| `Scripts/Session/Inbox.cs`, `InboxItem.cs`, `InboxKind.cs` | same | copied | — |
| `Scripts/Session/Outbox.cs` | same | copied | — one FRAME per peer per tick |
| `Scripts/Session/PendingEvents.cs` | same | copied | — |
| `Scripts/Session/PeerSlots.cs` | same | copied | — 8 slots, 120 s quarantine, host at slot 0 |
| `Scripts/Session/RoomClock.cs` | same | copied | — median of 8, 1 s coarse tolerance |
| `Scripts/Session/ClockPinger.cs` | same | copied | — 4 pings 2 ticks apart, then 10 s |
| `Scripts/Session/SnapshotReceiver.cs` | same | copied | — sizes its table array from `SnapshotPartKind.End`, so End must stay last |
| `Scripts/Session/IHostRule.cs`, `IIntentValidator.cs` | same | copied | — |
| `Scripts/Session/TransportFactory.cs` | same | copied | — WebGL → `WebRtcTransport`, everything else → `LoopbackTransport`. **No desktop/LAN branch yet** (NETCODE-PORT section 8.3). |
| `Scripts/Session/NetSessionExtensions.cs` | same | copied | — |
| `Scripts/Session/SessionRouter.cs` | same | copied | — unchanged, including `AcceptPose(slot, pos, yaw, seq)` and the per-slot wrap-safe sequence table. Stage 2 changes `AcceptPose` only. |
| `Scripts/Session/Rules/ClockRule.cs` | same | copied | — TIME_SYNC at 0.5 Hz + the CLOCK_PONG answer |
| `Scripts/Session/EventSink.cs` | same | adapted | `SpawnHeader` and `NextHeader` deleted (they took an `AircraftState`). `LeadTicks = 3`, `Emit`, `Reply`, `ReplyNow`, the three events — untouched. |
| `Scripts/Session/HostIds.cs` | same | adapted | Counters renamed to `NextEnemy()`, `NextPickup()`, `NextDoor()`. `ReserveTower` deleted. `NextSnapshot()` and the wrap logic untouched. |
| `Scripts/Session/HostAuthority.cs` | same | adapted | `AddRule` / `SetValidator` / `HasValidator` / `Tick` / `Handle` untouched. `using Pesky.Session.Validators;` removed. **`CreateDefault()` replaced entirely**: it now registers `ClockRule` as a rule and as the `MsgId.ClockPing` validator, and nothing else. |
| `Scripts/Session/MessageApplier.cs` | same | adapted | The switch is now five cases: SESSION_INFO, TIME_SYNC, SESSION_PHASE, PEER_SLOTS, SESSION_END. `IsAircraftEvent` deleted; `TryEventTick` reads the tick at a fixed offset of 1. |
| `Scripts/Session/SnapshotCodec.cs` | same | adapted | `Order` is now `{ World, Players }`. Everything else untouched. |
| `Scripts/Session/JoinFlow.cs` | same | adapted | The whole handshake is verbatim. Cut: the two `Resume.RestoreSlot` calls; `Sim.Economy.SharedPot` → `Sim.FinalScore` on host-leave; `BuildSessionInfo()` rewritten to the five fields SESSION_INFO now carries. |
| `Scripts/Session/NetSession.cs` | same | adapted | The five per-frame steps, `Send`/`SendNow`, `AdvanceHost`/`AdvanceClient`, resync, `Leave` — all verbatim. Cut: the `Resume` property and `Resume = null`; `StartGame(ShiftKind, hazard, RunModifier, promoted)` → `StartGame(byte floorId)` with a no-arg overload; `SetAirportName` deleted; `EndSession`'s `Sim.Economy.SharedPot` → `0`. |
| `Scripts/Session/{Damage,KillBounty,Pricing,SaveFile,SaveCodec,SaveResume,RunwayGate,RunwayCraters,PavementDamage,Wrecks,AutomationGate,DebugCommands}.cs`, `Rules/*` (44 of 45), `Validators/*` (all 14) | — | **skipped** | ATCK gameplay |
| `Scripts/Session/ATCK.Session.asmdef` | `Scripts/Session/Pesky.Session.asmdef` | rewritten | — |

#### New files (not ports)

| File | What it is |
|---|---|
| `Scripts/Data/GameData.cs` | `ScriptableObject`: `weapons[]`, `enemies[]`, `modifiers[]`, `movement`, indexed by the byte ids that travel on the wire. `NetSession.Start` takes it and hands it to `WorldSim`. **No asset instance has been created yet.** |
| `Scripts/Sim/WorldSim.cs` | The minimal sim — see section 4. |

#### Browser bridge

| Source | Destination | Action |
|---|---|---|
| `Plugins/WebGL/AHNet.jslib` | `Plugins/WebGL/AHNet.jslib` | copied **verbatim**, byte for byte. Every `AHNet_*` export and `window.AH_Net` untouched. |
| `Plugins/WebGL/ATCKPlatform.jslib` | `Plugins/WebGL/PeskyPlatform.jslib` | copied, `ATCK` → `Pesky` (so `Pesky_GetDevicePixelRatio`, `Pesky_EnterFullscreen`, …, `window.Pesky_Keys`). **Dormant: no C# imports it yet.** |
| `WebGLTemplates/ATCK/trystero.min.js` | `WebGLTemplates/Pesky/trystero.min.js` | copied verbatim (trystero 0.25.3, nostr strategy) |
| `WebGLTemplates/ATCK/keys.js` | `WebGLTemplates/Pesky/keys.js` | copied, `ATCK` → `Pesky`; pairs with `PeskyPlatform.jslib` above |
| `WebGLTemplates/ATCK/net.js` | `WebGLTemplates/Pesky/net.js` | copied, then four targeted edits (below) |
| `WebGLTemplates/ATCK/index.html` | `WebGLTemplates/Pesky/index.html` | copied, then: `<title>Pesky Weapons</title>`, loading text `PESKY WEAPONS`, `window.ATCK_ICE_SERVERS` → `window.PESKY_ICE_SERVERS`. Script order and `window.AH_UnityInstance` untouched. |

`net.js` edits:

1. `APP_ID` → `'pesky-weapons-9d4kv2'` (was `'atck-7r2m9wq4'`).
2. `ROOM_PREFIX` → `'PESKY-'` (was `'ATCK-'`).
3. `iceServers()` **now reads `window.PESKY_ICE_SERVERS`** and falls back to the
   hard-coded STUN list. ATCK defined the page global and never read it
   (NETCODE-PORT section 3.4); that dead hook is now live, so pasting TURN
   servers into `index.html` reaches the peer connection.
4. The log header and the COPY NETWORK LOG title say "Pesky Weapons".

Everything else in `net.js` — the 1 Hz PING, the 15 s peer timeout, the host
relay refresh, the ICE diagnosis, `primeLocalAddresses`, the two-sided ready
handshake, the voice block — is unchanged.

**The three magic names are consistent across C#, jslib and net.js:**
GameObject `NetBridge`, `window.AH_Net`, `window.AH_UnityInstance`.

### 3. Identity

| | |
|---|---|
| trystero `appId` | `pesky-weapons-9d4kv2` |
| room prefix | `PESKY-` (room name = `PESKY-` + the six-character code) |
| trystero action | `st` (unchanged) |
| `Wire.ProtocolVersion` | **1** |
| `Wire.MaxPlayers` | 8 |
| WebGL template folder | `Assets/WebGLTemplates/Pesky` (nothing selects it yet — `PlayerSettings.WebGL.template` is untouched, and the build target was **not** switched) |

### 4. The minimal WorldSim

`Pesky.Sim.WorldSim` implements exactly the surface the ported session calls:

```
WorldSim(GameData data, uint seed)
uint Tick               void AdvanceTo(uint tick)     // sets Tick; no work yet
uint WorldSeed          Rng Rng                        // reseeded from SESSION_INFO
SessionPhase Phase      uint PhaseStartTick
SessionEndReason EndReason   int FinalScore            // always 0 in stage 1
byte FloorId                                            // replaces ATCK's day clock
uint HostRoomMs                                         // last TIME_SYNC
PlayerTable Players

Apply(in SessionInfoMsg) / (in TimeSyncMsg) / (in SessionPhaseMsg)
      / (in PeerSlotsMsg) / (in SessionEndMsg)
SetPlayerPose(byte slot, Vector3 pos, float yaw)
bool WritePart(SnapshotPartKind, NetWriter) / bool ReadPart(SnapshotPartKind, NetReader)
ulong Hash()
```

The snapshot is two parts. `World` carries
`Tick u32 | WorldSeed u32 | RngState u32 | Phase u8 | PhaseStartTick u32 | FloorId u8 | EndReason u8 | FinalScore i32`;
`Players` carries eight `PlayerState` rows. `SnapshotCodec.ReadTable` requires
the reader to land exactly on the end of each body, so `WritePart` and `ReadPart`
must stay mirror images.

### 5. What is wired into gameplay: nothing

The solo game is untouched. No scene, prefab, material or existing script under
`Scripts/Game` was modified; `Pesky.Game` simply gained three asmdef references
(Session, Sim, Protocol) it does not use yet. `Zone1.unity` was open and clean
at the start and at the end, and nothing was saved over it. The whole project
compiles with no CS errors (the repeating `NoSubscription … generators.ai.unity.com`
console lines are Unity's own AI package and are unrelated).

### 6. The exact seams left for the next stage

**`Protocol/M0Messages.cs` — `TRANSFORM` (0x01).** Still ATCK's
`pos f32×3 + yaw f32 + seq u16`, 19 bytes. NETCODE-PORT section 6.2 replaces it
with `pos i16×3 cm | rot smallest-three u32 | vel i16×3 (1/256 m/s) | seq u16`,
also 19 bytes. Doing that touches, in this order:
`NetWriter`/`NetReader` (add `Cm`/`PosCm`/`Quat` and their readers) →
`M0Messages` (encode/decode) → `SessionRouter.AcceptPose(byte, Vector3, float, ushort)` →
`NetSession.SendNow` (the `TryDecodeTransform` call at the top) →
`WorldSim.SetPlayerPose` → `PlayerTable.SetPose` → `PlayerState` (`yaw` becomes
`rot` + `vel`, and its `Write`/`Read` change, which changes the snapshot body) →
bump `Wire.ProtocolVersion` to 2.

**`Session/MessageApplier.Apply`** — one `case` per new Event/State/kept-Stream
id. Its `TryEventTick` assumes every Event's tick is at offset 1; do not invent
a wider header.

**`Session/SnapshotCodec.Order`** — a new table needs a new
`SnapshotPartKind` **inserted before `End`** (End is the terminator and
`SnapshotReceiver` sizes its array from its numeric value), an entry in `Order`,
and a `WritePart`/`ReadPart` case.

**`Session/HostAuthority.CreateDefault()`** — currently the clock alone. Each
rule and validator is registered here, in a meaningful order. Intents also need
`authority.SetValidator(MsgId.X, …)`.

**`Sim/WorldSim`** — needs the `Enemies`, `Doors`, `Pickups` and `Plates`
tables, their `Apply` overloads, their snapshot parts, and real work inside
`AdvanceTo` (today it only moves `Tick`). `FinalScore` is hard-wired to 0 in
`NetSession.EndSession`; the escape/wipe outcome should fill it.

**`Protocol/MsgId`** — 0x20-0x7E is entirely free. The comment there reserves
players 0x20-0x2F, enemies 0x40-0x4F, world 0x50-0x5F.

**`Protocol/Appearance`** — six opaque bytes with ATCK's field names
(`body`, `hair`, `face`, `color0..2`). Reinterpret as weapon type / material /
trim, or shrink; the JOIN_REQUEST and PEER_SLOTS plumbing does not care.

**`Session/TransportFactory`** — no desktop branch, so two editor instances
still cannot meet (NETCODE-PORT section 8.3 has the ~200-line `TcpTransport`).

**Not done and known missing:** `JOIN_REFUSED` (a version mismatch is still a
silent hang for the joiner, NETCODE-PORT section 4.2); `Editor/BuildTools.cs`
and the WebGL player settings block; `Editor/PhysicsLayerSetup.cs`;
`Assets/link.xml`; the EditMode test harness (`InMemoryTransport`,
`InMemoryMesh`, `SessionTests`); `Game/UI/RoomCodes.cs`, `Clipboard.cs`,
`GameLocator.cs`, `Bootstrap.cs`, `PoseStreamer.cs`, `RemotePlayerSpawner.cs`,
`RemoteCharacter.cs`; a `GameData` asset instance; and
`PlayerSettings.WebGL.template = "PROJECT:Pesky"`.

---

## Stage 2 — the game is on the netcode (2026-09-20)

**Implemented, untested.** Checks made: clean compile, the edit-mode scene
validator (0 issues in `Zone1` and `Dev/FeelBox`), serialized values read back,
and every fixed-size message encoded once in the Editor to confirm its byte
count. Nothing was run. `Wire.ProtocolVersion` is now **2** (reason: POSE
respecified, fourteen new ids, three new snapshot parts).

Housekeeping to know about: `Sim/WorldSim.cs` was deleted through the MCP
(it went to the OS trash) and re-created, so its `.meta` GUID is new; it is a
plain C# class and nothing references the GUID. `Sim/PlayerState.cs` was edited
in place. Section 6 above ("seams left for the next stage") is now consumed,
except its **Not done** list, which still stands apart from `PoseStreamer` and
`RemotePlayerSpawner` / `RemotePlayerView` (written here, not ported).

### S2.1 The shape in one paragraph

Every gameplay scene has a `SessionRunner` (`_Managers/SessionRunner`, execution
order -1000). In `Awake` it adopts the session a lobby left in the static
`SessionRunner.Shared`, or, when there is none, starts an **offline session on
`LoopbackTransport`** (`StartOffline("SOLO", "Player", GameData)`), pumps
`NetSession.Update()` once so the local slot exists before any `Start`, and as
host calls `StartGame()` (Lobby -> Playing; there is no lobby yet). So solo play
and the FeelBox are a host with zero peers and need no setup. `WorldAuthority`
is still the facade every view listens to; what changed is that a `Request*`
no longer changes the scene. It publishes (host) or asks (client), the session
applies the message to the `WorldSim` on every peer through
`MessageApplier.Apply`, and the scene changes **only** in the
`NetSession.EventApplied` handler (`WorldAuthorityNet.cs`), which then raises
the same C# event as before. Offline that round trip is synchronous inside the
`Request*` call (`Send` -> validator -> `EventSink.Emit` -> `Applied` ->
`EventApplied`), so `RequestPossess` still returns true at once for a solo player.

### S2.2 Authority split

| Thing | Who decides | How |
|---|---|---|
| A player's own body (their weapon, or their soul when free): position, rotation, velocity, launches, wall jumps, sticking in wood, kill-Y recovery, MagicDoor trips | **the owner** | streamed as POSE; nobody ever corrects it |
| "My weapon touched X" (an enemy, a key, a rope, a pot, a lever, a cracked wall, an anvil, another player) | **the peer that simulates the weapon reports it**: the player holding it, or the host while it is loose (`WorldAuthority.Simulates`) | HIT_CLAIM / KIT_REQ / BAT_CLAIM; a remote driven (kinematic) copy never reports anything |
| Who holds a weapon; break vs drop on release | host | POSSESS_REQ / RELEASE_REQ -> WEAPON_OWNER / WEAPON_BROKEN |
| Weapon hp, breaking, respawn, modifiers | host | WEAPON_DAMAGED, `WeaponRule`, KIT_STATE(Anvil / Rune) |
| Enemy AI, perception, targets, attacks, porter carry | host only (NavMeshAgent brains) | ENEMY_STATE rows, WEAPON_DAMAGED, KIT_STATE(PorterCarry) |
| Enemy hp, shield, death, knockback | host (damage computed from `GameData`) | HIT_CLAIM -> ENEMY_HEALTH |
| Doors, plates, scales, counterweights, porter gates, magnets, lifts, lightning | host alone (its own physics and conditions) | KIT_STATE; a client never asks for these |
| Keys, runes, ropes, pots, levers, cracked walls, anvils | the touching peer asks, host validates against its own scene (`IHostWorld.ValidateKit`: piece state, `CanCut` / `CanSmash` / `CanFlip` / `CanBreak` with the claimant's weapon as the host sees it, within 8 m) | KIT_REQ -> KIT_STATE |
| The shove from an enemy hit or a bat | host decides the vector, **the target's owner applies it locally** (`WeaponBody.Knockback`); the host applies it for a loose weapon | WEAPON_DAMAGED.knock / BAT_EVENT |
| Loose (unheld) weapons | **not synchronised**: every peer simulates them locally from the rest pose the releaser sent. They can drift apart; possession snaps them to the owner's POSE | WEAPON_OWNER(free, pose) |
| Level clock | host room clock | `LevelClock` re-bases onto `SessionRunner.LevelMs` (= room ms since `PhaseStartTick`) when more than 50 ms off; between re-bases it runs on Unity time so `FixedUpdate` still sees fixed steps |

Two players can never hold one weapon: the host answers requests one at a time
and `EventSink.Emit` applies WEAPON_OWNER to the host's sim before the next
request is read, so the second POSSESS_REQ finds `ownerSlot != NoSlot`.

### S2.3 Every message

Positions are **f32 x3, world space, not quantised** (the chosen answer to "the
world is wider than +-327 m": wider components, no room anchor to look up).
Velocities are i16 x3 at 1/256 m/s, rotations a 32 bit smallest-three
quaternion, hp a u16 in tenths, speeds a u16 in hundredths, yaw a u16 over the
full circle. Helpers: `NetWriter/NetReader.Pos/Vel3/Quat/Hp/Speed/Yaw`. Every
Event starts `type u8 | tick u32` (so `TryEventTick` stays a fixed offset).

| Id | Name | Kind | Bytes | Rate | Layout after the type byte |
|---|---|---|---|---|---|
| 0x01 | POSE | raw (M0, `SendNow`, never in a FRAME) | 26 | 20 Hz while moving, 1 Hz keepalive | `flags u8 (PoseFlags: 1 Teleport, 2 Animate, 4 Soul), pos f32x3, rot u32, vel i16x3, seq u16` |
| 0x20 | POSSESS_REQ | Intent | 3 | on E | `weaponId u16` |
| 0x21 | RELEASE_REQ | Intent | 25 | on Q | `weaponId u16, pos, rot, vel` |
| 0x22 | WEAPON_OWNER | Event | 31 | on change | `tick, weaponId u16, ownerSlot u8 (0xFF free), hasPose u8, pos, rot, vel` (pose = the loose weapon's rest pose) |
| 0x23 | WEAPON_STATE | State | 2 + rows | when the host loads a level | `count u8` then rows: `weaponId u16, defId u8, flags u8 (1 Broken), ownerSlot u8, hp u16, maxHp u16, respawnTick u32, homeSlot u16, modCount u8, modifierId u8 x n` (16 + n each). On a row the sim already has, only hp / maxHp / modifiers are taken |
| 0x24 | WEAPON_BROKEN | Event | 12 | on break | `tick, weaponId u16, cause u8 (BreakCause), respawnTick u32` |
| 0x25 | WEAPON_RESPAWNED | Event | 7 | 10 s after a break | `tick, weaponId u16` |
| 0x26 | WEAPON_DAMAGED | Event | 19 | per enemy hit / hazard | `tick, weaponId u16, enemyId u16 (0xFFFF hazard), damage u16, newHp u16, knock i16x3` |
| 0x27 | BAT_CLAIM | Intent | 20 | per bat, max 4 Hz locally | `targetSlot u8, velocityChange i16x3, point f32x3` |
| 0x28 | BAT_EVENT | Event | 25 | per validated bat | `tick, fromSlot u8, targetSlot u8, velocityChange i16x3, point f32x3` |
| 0x40 | ENEMY_STATE | State | 2 + 25 x rows | **10 Hz** (every 2 ticks) **plus at once on a state change**; a row is only sent when that goblin moved > 1 cm, turned > 1 deg, changed, or 1 s passed | `count u8` then rows: `enemyId u16, kind u8, pos f32x3, yaw u16, hp u16, shieldHp u16, state u8 (GoblinBrain.State), flags u8 (EnemyFlags: 1 Alive, 2 Hostile, 4 Awake, 8 Carrying), targetWeaponId u16` |
| 0x41 | HIT_CLAIM | Intent | 19 | per contact, local 0.35 s gate | `enemyId u16, weaponId u16, relativeSpeed u16, point f32x3` (never a damage number) |
| 0x42 | ENEMY_HEALTH | Event | 35 | per validated hit | `tick, enemyId u16, attackerSlot u8, weaponId u16, damage u16, newHp u16, newShieldHp u16, flags u8 (HitFlags: 1 HitShield, 2 ShieldBroke, 4 Died, 8 Blocked), knock i16x3, point f32x3` |
| 0x50 | KIT_STATE | Event | 19 | on change | `tick, kind u8 (KitKind), pieceId u16, state u8, actor u16 (weapon id, 0xFFFF none), value i32, clockMs u32` |
| 0x51 | KIT_REQ | Intent | 9 | per contact | `kind u8, pieceId u16, state u8, actor u16, speed u16` |
| 0x52 | WORLD_RESET | State | 5 | when the host loads a level | `sceneHash u32`; empties the weapon, enemy and kit tables |

`KitKind`: 0 Door, 1 Plate, 2 Key, 3 Rune (`value` = modifier id), 4 Anvil
(heals `actor` to full), 5 Magnet, 6 Lift (`clockMs` = cycle start), 7 Rope
(`clockMs` = cut time), 8 Pot, 9 Lever, 10 Counterweight (`value` = target
offset in mm, `clockMs`), 11 Scales, 12 CrackedWall, 13 PorterGate, 14 Lightning
(cosmetic bolt; the damage is a WEAPON_DAMAGED), 15 PorterCarry (`pieceId` = the
porter's enemy id; state 1 picked up, 0 dropped in alarm, 2 placed).

Scene ids travel as u16, so every `ISceneId` must stay in 0..65534 (today:
weapons 1-116, goblins 501-512). `WeaponDef.id`, `EnemyDef.id` and
`ModifierDef.id` travel as u8 and are **array indices into `GameData`**
(`Assets/Data/GameData.asset`: weapons[1..7], enemies[1..3], modifiers[1];
index 0 is empty on purpose). Append only.

Host validation. **HIT_CLAIM** (`HitValidator`): claimant holds the weapon (or
is the host and the weapon is loose); per weapon-per-enemy cooldown
(`GameData.hitCooldownSeconds`, 7 ticks); claimant's streamed pose within 6 m of
the enemy row; damage = `WeaponDef.damage` x smoothstep(`impactSpeedMin`..
`impactSpeedMax`, speed clamped to 60) x modifiers, then the shield boss rules
(`shieldMinMass`, `bladedDamageBonus`); knock from `EnemyDef`. **BAT_CLAIM**
(`BatValidator`): both slots hold weapons, streamed poses within 5 m, 0.5 s per
pair, clamp to `GameData.batMaxSpeed`. **POSSESS / RELEASE**
(`PossessValidator`): row free and unbroken / row held by the asker;
`WorldSim.InCombat(weapon)` (damage dealt or taken within 5 s, or a hostile
enemy row targeting it) turns a release into WEAPON_BROKEN(ReleasedInCombat).
No range check on possess (loose weapons are not synchronised, so the host
cannot know where the client sees one). **KIT_REQ** (`KitValidator` +
`WorldAuthority.ValidateKit`). Nothing is gated on `SessionPhase`.

Friend ballistics v0 (`WeaponBody.NoteBat` -> `WorldAuthority.RequestBat`): when
the locally possessed weapon gets `OnCollisionEnter` against a remote driven
weapon and the relative speed (my pre-step velocity minus their streamed
velocity) is at least `batMinSpeed` (4 m/s), it claims
`dv = relVel x (1 + batRestitution) x mA / (mA + mB)` clamped to `batMaxSpeed`
(18 m/s). The target's owner applies it with `Knockback` on BAT_EVENT.

### S2.4 The sim

`WorldSim` (plain C#) now has four tables, each a snapshot part
(`SnapshotPartKind`: World 0, Players 1, **Weapons 2, Enemies 3, Kit 4**, End 5):
`PlayerTable` (identity + pose `pos/rot/vel/poseFlags` + `weaponId`),
`WeaponTable` (`WeaponState`: owner, hp, maxHp, broken, respawnTick, homeSlot,
modifiers, combatUntilTick, rest pose), `EnemyTable` (`EnemyState`), `KitTable`
(last KIT_STATE per piece). `World` gained `SceneHash`. `WorldSim.Rev` (local
only) bumps on WORLD_RESET, WEAPON_STATE and every snapshot part; a client's
`WorldAuthority.Update` sees it move and runs `SyncFromSim()` (idempotent:
break / respawn / hp / modifiers / owner / rest pose per weapon, then every kit
entry replayed through `ApplyKit(live: false)`; goblins read their own rows).
That is how a late joiner and a resync rebuild the scene.

Host rules, in `HostAuthority.CreateDefault()` order: `WeaponRule` (hp <= 0 ->
WEAPON_BROKEN; respawn tick reached -> WEAPON_RESPAWNED; owner's slot empty ->
WEAPON_OWNER free, which is what frees a leaver's weapon), `EnemyStateRule`
(TowerRule pattern: next-state tick + dirty flag; rows come from Game through
`IHostWorld.CollectEnemyRows`), `ClockRule`.

Two additions to the ported engine, both because Pesky's host logic partly
lives in Game (NavMesh brains, physics kit): `NetSession.HostEmit(payload)` /
`HostHeader()` (host only, Event or State only, it is `EventSink.Emit`), and
`NetSession.HostWorld` / `EventSink.World` (`IHostWorld`, implemented by
`WorldAuthority`). `Pesky.Game` still never references `Pesky.Transport`.

### S2.5 Game side, file by file

| File | What |
|---|---|
| `Game/SessionRunner.cs` | new. Session everywhere, auto loopback, auto `StartGame`, `LevelMs`, `HostLost`, `Leave` on quit |
| `Game/WorldAuthorityNet.cs` | new. The bridge: `OnNetEvent`, `ReconcileOwner`, `SubmitKit` / `ApplyKit`, `SyncFromSim`, host registration (WORLD_RESET + WEAPON_STATE in `Start`), `IHostWorld`, `RequestBat`, `RequestEnemyAttack`, `RemotePossessor` |
| `Game/WorldAuthority.cs`, `...Kit.cs`, `...Puzzles.cs` | every `Request*` / `Report*` is now a thin call into the bridge; signatures and C# events unchanged. `ApplyLever` removed |
| `Game/WeaponBodyNet.cs` (+ 6 one-line hooks in `WeaponBody.cs`, now `partial`) | net driven (no own respawn timer, `SetHpFromNet`, `ApplyRespawn`), remote driven (kinematic, `RemoteMove`, streamed ANIMATE overrides `IsAnimate`), `PlaceAtRest`, `NoteBat`, `IsLocallyPossessed`. `ApplyDamage` routes through the authority in a session |
| `Game/GoblinBrainNet.cs` (+ 3 hooks in `GoblinBrain.cs`) | host: `NetCollect` builds the row; client: **puppet** mode, which is the EnemyView (agent off, no perception, eased over 100 ms toward the latest row, state / markers / blade / hp from the row). `LandHit` now calls `RequestEnemyAttack` so damage and shove travel together |
| `Game/PoseStreamer.cs` | new. 20 Hz POSE of the local body, epsilons 1 cm / 1 deg / 0.25 m/s, 1 s keepalive, Teleport flag on three poses after a MagicDoor trip, a soul/weapon swap or an implausible jump |
| `Game/RemotePlayerSpawner.cs`, `RemotePlayerView.cs`, `Prefabs/Player/RemotePlayer.prefab` | new. One view per other present slot, polled from the sim (despawn when the slot empties or the session ends). 100 ms behind, Hermite + slerp, 250 ms extrapolation, snap on Teleport. Soul orb (no collider) or the possessed scene weapon (kinematic, colliders on). TMP name label |
| `Game/LevelClock.cs` | follows `SessionRunner.LevelMs` |
| `Game/PlayerSpawner.cs` | spawn point = local slot % points |
| `Game/AnvilStation.cs`, `DoorPrompt.cs` | prompts only for the locally possessed weapon |
| `Game/FloorActivator.cs` | rooms other players stand in stay enabled too (on the host their floors carry goblins, sight lines and loose weapons) |
| `Data/GameData.cs` | + impact and bat tuning; asset created |

Scenes wired and saved: `Zone1`, `Dev/FeelBox` (`_Managers/SessionRunner`,
`PoseStreamer`, `RemotePlayerSpawner`; `WorldAuthority.sessionRunner`,
`LevelClock.session`, `PlayerSpawner.session`). `Boot` is untouched.

### S2.6 Riskiest untested assumptions — check these first

1. **The synchronous offline round trip.** Everything solo depends on
   `Send(intent)` -> validator -> `Emit` -> `EventApplied` -> scene change all
   happening inside the `Request*` call, and on `SessionRunner.Awake` having a
   local slot before anything else runs. First test: FeelBox, press E on a
   weapon. If nothing happens, look at `NetSession.Send` (needs
   `Slots.HasLocalSlot`) and the console for `[net]` lines.
2. **No damage if POSE or GameData is missing.** `HitValidator` needs
   `GameData` (null = every claim dropped) and rejects a claim whose streamed
   pose is more than 6 m from the enemy row. A scene without `PoseStreamer`
   still works (poseCount 0 skips the range test), a wrong pose does not.
   Enemy rows only exist after the first sim tick (50 ms after load).
3. **Re-entrant `HostEmit`.** `ApplyKit` calls `EvaluateDoors()` (and lever ->
   lift) from inside an `EventApplied` callback, so the inner KIT_STATE is
   queued to clients **before** the outer one, both with the same tick. Every
   handler is idempotent and order-independent as far as I can reason, but this
   is the ordering ATCK's PendingEvents comment warns about.
4. **Kinematic remote weapons in PhysX.** Assumed: trigger volumes
   (`RoomVolume`, plates, pans, anvils) see a kinematic body moved with
   `MovePosition`; a local dynamic weapon gets `OnCollisionEnter` against it;
   `Rigidbody.linearVelocity` of a kinematic body is usable by
   `PressurePlate`'s rest-speed test. Also the target of a bat is pushed
   **twice**: once by local contact physics against the kinematic copy, once by
   BAT_EVENT ~150 ms + RTT later. Tune `GameData.batRestitution` / `batMaxSpeed`
   or drop one of the two if it feels wrong.
5. **Host perception of remote players** rides `PoseFlags.Animate` ->
   `WeaponBody.IsAnimate`. At 20 Hz with a 100 ms render delay a client can be
   "seen moving" slightly late and "seen still" slightly late; playing dead
   near a goblin may feel different for clients than for the host.
6. **State rows apply at once, Events 3 ticks later.** A puppet goblin can show
   Dead (ENEMY_STATE) ~150 ms before the ENEMY_HEALTH event flashes it and
   raises `EnemyDied`. Harmless by design, but visible.
7. **`LevelClock` re-base.** 50 ms tolerance; a hitch longer than Unity's
   `maximumDeltaTime` re-bases and every scheduled mover jumps. Riders on lifts
   are the thing to watch. Clients need `RoomClock.HasEstimate` first.
8. **`FloorActivator`** now keeps remote players' rooms enabled on every peer.
   Without that the host's goblins would see through disabled walls and loose
   weapons would fall through disabled floors; with it, more rooms render.
9. **Loose weapons are per-peer physics.** They will drift. A client can
   possess a weapon that is somewhere else on the host; the host accepts it and
   everyone snaps to the owner's POSE.
10. **Two machines have never met.** `TransportFactory` still has no desktop
    branch, so clients, puppets, remote views, snapshots of the new tables and
    late join are reasoned about only. Nothing loads the level on a client
    (`SessionRunner.Shared` is the hook a lobby stage must fill).
11. Dev probes (`FeelProbe`, `Zone1Probe`) compile but assume synchronous hp
    changes; in a session a break lands on the next sim tick.

### S2.7 Not done

`JOIN_REFUSED`; a desktop/LAN transport; menus / lobby / level loading on
clients; a HUD line for "host left" (only a `[net]` warning and
`SessionRunner.HostLost`); position sync of loose weapons after release;
host range check on possess; EditMode round-trip tests for the new messages;
`FinalScore`; the six `Appearance` bytes are still ATCK's names.

---

## Stage 3 — a desktop transport, so two instances can meet (2026-09-20)

**Implemented, untested.** Checks made: a clean compile after every change (Unity
console: 0 errors, 0 warnings), the new types read back through the Editor
(`TransportFactory.ForPlatform()` really returns a `TcpTransport`, the address
parser answers correctly), and arithmetic on the framing. **No socket has ever
been opened**: no play mode, no build, no two instances.

ATCK had no desktop path at all (`NETCODE-PORT.md` section 8.2). This is section
8.3 built, plus the thread hygiene and the address plumbing that section skipped.

### S3.1 What was added

| File | What |
|---|---|
| `Scripts/Transport/TcpLink.cs` | new. `TcpOp` (the opcodes), `TcpWire` (framing and string helpers), `TcpLink` (one socket, one reader thread, one writer thread) |
| `Scripts/Transport/TcpTransport.cs` | new. `INetTransport` over TCP: host listener + relay, client connect, peer table, the shutdown hooks, LAN address listing |
| `Scripts/Session/TransportFactory.cs` | edited. `ForPlatform()` is now WebGL to `WebRtcTransport`, **everything else to `TcpTransport`**; `Offline()` is still `LoopbackTransport`. Added `DefaultPort` and `HostAddresses(port)` so Game can show the room code without naming a transport |
| `Scripts/Game/SessionRunnerDev.cs` | new. `partial SessionRunner`: the dev switch that decides host / join / offline while there is no lobby |
| `Scripts/Game/SessionRunner.cs` | edited, two lines: the class is now `partial`, and `Awake` calls `StartAutoSession()` instead of always starting offline |
| `Scripts/Editor/NetDevWindow.cs` | new. The `Pesky/Net` menu and a small window over the EditorPrefs it writes |

No scene, prefab, asset or project setting was changed. `PlayerSettings.runInBackground`
was already **true** and was left alone; `TcpTransport.Start` also sets
`Application.runInBackground = true` at runtime, because two instances on one
desktop both have to keep ticking while unfocused.

### S3.2 The wire under the wire

Session's bytes are the payload; these frames are the transport's own and never
reach `MessageApplier`.

```
frame  := len u32 (little endian, counts the opcode) | op u8 | body
string := len u16 | UTF-8 bytes
```

| Op | Name | Direction | Body |
|---|---|---|---|
| 0x01 | Hello | client to host | magic u32 `0x314E5750` ("PWN1") + wire version u8 (1) |
| 0x02 | Welcome | host to client | your id, the host id, count u8, then every peer already here |
| 0x03 | Joined | host to client | peer id |
| 0x04 | Left | host to client | peer id |
| 0x05 | Relay | client to host | target id (empty = every other peer) + payload |
| 0x06 | Deliver | host to client | origin id + payload |
| 0x07 | Ping | both | empty; sent after 2 s of an idle writer, dropped by the reader |

Peer ids: the host is **`"host"`**, clients are **`"p1"`, `"p2"`, ...** assigned
on accept and never reused inside one session. A client learns the host's id from
Welcome, so nothing hard-codes it on that side.

Routing (the star that pretends to be a mesh, which `INetTransport` explicitly
allows):

| Call | On the host | On a client |
|---|---|---|
| `Broadcast` | Deliver(from = "host") to every link | Relay(target = empty) to the host, which delivers it to every *other* client and raises `Message` locally |
| `SendTo(peer)` | Deliver(from = "host") to that link | Relay(target = peer); the host raises `Message` if the target is itself, otherwise forwards Deliver(from = the asking client) |

So client-to-client works and `PeerIds` is the whole session on every peer.
Order per peer pair holds: each link has exactly one reader and one writer thread,
and the host relays in the order it reads.

Handshake order, which is what `JoinFlow` depends on: the host raises
`Ready("host")` inside `Start`; a client raises `Ready(selfId)` only when Welcome
lands, then `PeerJoined(host)` and one `PeerJoined` per other peer. `Inbox` is a
FIFO, so the session sees them in that order.

### S3.3 Threads, and nothing left running

One accept thread on the host, one connect thread on a client, and a reader plus a
writer thread per socket. All `IsBackground`. The Unity main thread never blocks on
a socket: `Broadcast` / `SendTo` only enqueue and the writer thread does the
`Write`. `Ready`, `Error`, `PeerJoined`, `PeerLeft` and `Message` therefore fire
**off the frame**, which is the contract WebGL already imposed and the reason
`Inbox` only enqueues.

Liveness is the socket's own receive timeout: 10 s, with a Ping after every 2 s of
silence, so a dead peer becomes `PeerLeft` (or, for a client losing the host,
`PeerLeft(hostId)` then `JoinFlow` then `HostLost`) without any timer thread.

`Leave()` stops the listener, closes every socket (which throws the blocked reads
and writes out of their calls) and joins every thread with a 500 ms cap, warning if
one overstays. It is also called from `Application.quitting`, from
`EditorApplication.playModeStateChanged == ExitingPlayMode` and from
`AssemblyReloadEvents.beforeAssemblyReload`, so exiting play mode or recompiling
never leaves a socket open in the Editor.

### S3.4 How to test two instances

The pairing to try first: **the Editor hosts, a Windows build joins localhost.**
There is still no lobby, so the dev switch chooses (`Game/SessionRunnerDev.cs`):

- Editor: menu **Pesky > Net > Host on this machine** (or **Dev Session Switch**
  for a window with the mode, the address and a player name; it also lists this
  machine's addresses). It writes EditorPrefs and **persists across Unity
  sessions** — put it back with **Pesky > Net > Play Offline** when you are done,
  or solo play will try to host.
- Build: the command line — `-peskyhost [port]`, `-peskyjoin host[:port]`,
  `-peskyname Name`. With none of them a build plays offline exactly as today.

Steps:

1. Editor: **Pesky > Net > Host on this machine** (port 7777).
2. Build a Windows x64 player (Boot is scene 0 and loads Zone1 by itself). Keep it
   windowed so both are visible.
3. Press Play in the Editor and let it reach Zone1. The console prints
   `[tcp] hosting on port 7777; peers join with <ip>:7777  or  localhost:7777`.
   Windows Firewall may ask on the first listen — allow it on private networks.
4. Run the build from PowerShell:
   `& "...\Pesky.exe" -peskyjoin localhost:7777 -peskyname Build -screen-fullscreen 0 -screen-width 1280 -screen-height 720`
5. Expect `[tcp] peer joined: p1` in the Editor console and, in the build's log
   (`%USERPROFILE%\AppData\LocalLow\<company>\<product>\Player.log`),
   `[tcp] connected as p1; host is host, 0 other peer(s)` followed by the `[net]`
   join handshake. Then a remote weapon should appear in each instance.
6. Two machines: the same, but the joiner passes the host's LAN IPv4 taken from the
   host's own log line (`-peskyjoin 192.168.1.5:7777`), and both machines must
   allow port 7777.
7. Two builds instead: give one `-peskyhost` and the other `-peskyjoin localhost`.
   Two Editors on one machine is not possible (one Unity instance per project), so
   the Editor is always one side of the pair.

Reading the result: `[tcp]` lines are the transport, `[net]` lines are the session.
A client that connects but never joins is a protocol-version or handshake problem,
not a socket one.

### S3.5 Known limits of this transport

1. **No security.** Five bytes of magic and a version, nothing else. Anyone who can
   reach the port joins and can send any message the protocol allows; the host's
   validators are the only defence. LAN and trusted networks only, never a public IP.
2. **No NAT traversal and no relay.** Same LAN, a VPN, or a forwarded port. The
   browser build keeps WebRTC for the open internet.
3. **Star, not mesh.** Client to client costs a hop through the host, so that
   latency is host RTT plus peer RTT, and the host is the single point of failure
   (which the design already assumed).
4. **IPv4 only.** The listener binds `IPAddress.Any`; the parser splits on the last
   `:`, so a bare IPv6 literal is misread. Host names and dotted quads work.
5. **The address is upper-cased** by `NetSession.Start` (`RoomCode.ToUpperInvariant()`),
   so `localhost` reaches the transport as `LOCALHOST`. DNS is case-insensitive and
   IP literals have no letters, so this is harmless — but a case-sensitive host name
   would not survive it.
6. **Timeouts are generous:** 10 s of silence drops a peer, which is long enough
   that a real stall will feel like a freeze before it becomes a disconnect.
7. **Back-pressure is a cliff:** 512 queued frames per link, then that peer is
   dropped with "send queue overflowed".
8. **Seven clients** (`Wire.MaxPlayers` minus the host), hard-coded as
   `TcpTransport.MaxPeers` because Transport may not reference Protocol. Keep the
   two in step.
9. **No voice.** `TcpTransport` does not implement `IVoiceControl`, so
   `NetSession.Voice` is null and `HasVoice()` is false on desktop. Every voice
   extension already null-guards.
10. **Not compiled into a WebGL player.** Both files are wrapped in
    `#if !UNITY_WEBGL || UNITY_EDITOR`, the exact complement of the
    `TransportFactory` branch, so a browser build never sees `System.Net.Sockets`.

### S3.6 Riskiest untested assumptions

1. **Nothing has been run.** A mistake in the length prefix or a string field shows
   up as an immediate disconnect with `[tcp] bad frame length` or a silent drop.
   Read `TcpWire.Wrap` against `TcpLink.ReadLoop` first if so: `len = 1 + body`,
   and the reader takes 5 bytes (the length plus the opcode) then `len - 1`.
2. **A client has no local slot for its first frames.** `Ready` arrives
   asynchronously, so unlike loopback a client's `SessionRunner.Awake` finishes with
   `LocalSlot == Wire.NoSlot`. Anything that reads the slot in `Start`
   (`PlayerSpawner` picks its spawn point that way) sees `NoSlot` on a client. This
   is the first thing likely to look wrong.
3. **Nothing synchronises which level is loaded.** Both instances must already be in
   the same scene; Boot to Zone1 on both is the only reason the recipe works.
4. **A recompile during play closes the sockets** (the domain-reload hook), so
   editing a script mid-session drops the connection rather than corrupting it.
5. **Firewall.** The first host in the Editor pops a Windows dialog; declining it
   leaves clients unable to connect while the host still says "hosting".
6. Every stage-2 risk in S2.6 is still unproven and only now becomes reachable —
   this is the first time two peers can exist at all.

### S3.7 Not done

The lobby (a scene, a room-code field, a start button, and loading the level on
clients) — the dev switch is scaffolding, not a lobby, and should be deleted when a
real one exists. `JOIN_REFUSED`. Reconnection: a dropped client stays dropped. Host
migration. Encryption. IPv6. UPnP or any public path for desktop. Tests for the
framing (two `TcpTransport`s over real loopback sockets would be an integration
test, not an EditMode one).

---

## Stage 4 — a main menu and a room page (2026-09-20)

**Implemented, untested.** Checks made: a clean compile after every change (0 CS
errors), the edit-mode scene validator (0 issues in `MainMenu`, `Zone1` and
`Dev/FeelBox`), the build settings and every serialized value read back through
the Editor, and `Menu.uxml` instantiated in edit mode to confirm that every one
of the twenty-one element names `MenuView` queries exists with the right type and
that `Menu.uss` is attached. **No play mode, no build, no socket.**

This is the lobby S3.7 said was missing. The dev switch it replaces is gone.

### S4.1 The scene flow

```
Boot (index 0)        _Bootstrap/GameBootstrap  ->  loads "MainMenu"
MainMenu (index 1)    _UI/Menu (UIDocument + MenuFlow)
                        HOST / JOIN  -> room page -> START -> "Labyrinth"
                        TUTORIAL     -> offline session   -> "Zone1"
Zone1 (index 2)       unchanged; the tutorial scene until the tutorial stage
```

Build settings are exactly those three, in that order, all enabled. `Labyrinth`
does **not** exist: every load goes through `MenuFlow.IsInBuild(name)` first, so
a missing scene prints a line on the status row and nothing happens. START
checks the scene **before** it broadcasts, so a build without the labyrinth
never leaves half the room in a phase the others cannot follow.

### S4.2 What was added and changed

| File | What |
|---|---|
| `Scripts/Game/GameLocator.cs` | new. The one static that outlives a scene load, as in ATCK: `Session`, `FromMenu`, and a one-shot `Message` / `MessageIsError` the next screen prints. Resets itself on `SubsystemRegistration`. |
| `Scripts/Game/SceneNames.cs` | new. `Boot`, `MainMenu`, `Labyrinth`, `Tutorial` — defaults for serialized fields, not lookups. |
| `Scripts/Game/UI/RoomCodes.cs` | new. ATCK's alphabet ported (`ABCDEFGHJKMNPQRSTUVWXYZ23456789`, six characters, no I L O 0 1) **plus** the desktop address shape. `IsAddressBased` is the exact complement of `TransportFactory.ForPlatform`'s branch. |
| `Scripts/Game/UI/Clipboard.cs` | new, ported. `AHNet_CopyClipboard` on WebGL, `GUIUtility.systemCopyBuffer` elsewhere. |
| `Scripts/Game/UI/SlotColors.cs` | new. Eight colours, one per slot; slot 0 (the host) is the amber accent. |
| `Scripts/Game/UI/MenuView.cs` | new. The two pages as elements: queries `Menu.uxml` by name, builds the eight crew rows, raises seven plain events. No session, no scene, no rules. |
| `Scripts/Game/UI/MenuFlow.cs` | new. The controller: owns the `NetSession` while the menu is up, pumps it once a frame, answers the view, and hands the session to the next scene through `GameLocator`. |
| `UI/Menu.uxml`, `UI/Menu.uss` | new. A centred column, max 560 px, one accent (amber `rgb(226,186,74)`) on a dark ground, cards for the groups, one status row per page. |
| `UI/HudPanelSettings.asset` | **renamed** to `UI/UiPanelSettings.asset` (`AssetDatabase.RenameAsset`, so the GUID is unchanged and `Prefabs/UI/HUD.prefab` still resolves it). It is now the shared PanelSettings: scale with screen size, 1920x1080 reference, match width. |
| `Scenes/MainMenu.unity` | new. `_Managers/EventSystem` (EventSystem + InputSystemUIInputModule, its eight UI actions bound to `Assets/Input/InputSystem_Actions.inputactions`), `_Cameras/Main Camera` (solid colour, culling mask 0), `_UI/Menu` (UIDocument -> `Menu.uxml` + `UiPanelSettings`; `MenuFlow` -> document, `Menu.uss`, `GameData`). |
| `Scenes/Boot.unity` | `GameBootstrap.sceneName` is now `MainMenu` (was `Zone1`). Nothing else in Boot changed. |
| `Scripts/Session/NetSession.cs` | **added `ReturnToLobby()`**: host only, emits `SESSION_PHASE(Lobby)` through the same `EventSink.Emit` that starts a run, so a finished room can start another. |
| `Scripts/Game/SessionRunner.cs` | `Shared` now forwards to `GameLocator.Session`; `Awake` adopts that session or starts its own offline one; it subscribes to `PhaseChanged`; a run that ends (or a host that leaves) loads `menuScene` (serialized, default `MainMenu`) — but only when `GameLocator.FromMenu`, so a level opened straight from the Editor is untouched. |
| `Scripts/Game/SessionRunnerDev.cs`, `Scripts/Editor/NetDevWindow.cs` | **deleted** (S3.7 said to). The `Pesky > Net` menu and `-peskyhost` / `-peskyjoin` / `-peskyname` are gone with them. |

### S4.3 What a room code is now

| | Browser (WebGL player) | Desktop (editor and standalone) |
|---|---|---|
| host opens with | `RoomCodes.Generate()` — six characters | `":" + TransportFactory.DefaultPort` (`:7777`) |
| room page shows | that code | `TransportFactory.HostAddresses()[0]`, the first LAN IPv4 with the port; the rest on a small "also reachable at" line |
| joiner types | six characters, filtered and upper-cased as typed | `host` or `host:port`, checked for spaces, an empty host and a port in 1..65535 |
| COPY copies | the code | the address shown big |

`NetSession.Start` upper-cases the room code (S3.5 item 5), so a desktop client's
room page prints `LOCALHOST:7777`. Harmless: DNS is case-insensitive and IP
literals have no letters.

### S4.4 The session across scenes

One `NetSession`, created by `MenuFlow` and left in `GameLocator.Session`.
Exactly one thing pumps it at a time: `MenuFlow.Update` while the menu scene is
loaded, `SessionRunner.Update` while a level is. `MenuFlow` is an ordinary scene
object (no `DontDestroyOnLoad`), so the hand-over is the scene load itself; the
few unpumped frames of a load are well inside the 10 s transport timeout.

Starting a run: START is the host's, enabled from one player, and only in the
`Lobby` phase. `Session.StartGame()` emits `SESSION_PHASE(Playing)`; **every peer
including the host loads the level from its own `PhaseChanged`**, so there is one
code path and no "tell the others" step.

Ending one: `SessionPhase.Ended` makes `SessionRunner` load `MainMenu` with "the
run is over", and the menu shows the **room page** again; a host there calls
`ReturnToLobby()` so START works for the next run. A host that leaves sends
`SESSION_END(HostLeft)`, which reaches the others as `HostLost`: the session is
dropped and they land on the **title page** with "the host left the room". Both
can fire in the same frame; the dead flag sticks, the later message wins, and
`SceneManager.LoadScene` is issued once.

Both `MenuFlow` and `SessionRunner` **queue** these endings and act on them after
`NetSession.Update()` has returned, because `HostLost` and `Error` are raised
from inside the inbox drain and leaving a session mid-drain is asking for it.

### S4.5 What the player is told

| Situation | Where it is detected | What the line says |
|---|---|---|
| version mismatch, joining | the client, `JoinFlow.OnSessionInfo` | "version mismatch: the host is running a different build of the game" |
| host not reachable | the client, `TcpTransport` connect | "host not found - could not reach 192.168.1.5:7777 ..." |
| bad address typed | `RoomCodes.NormaliseJoin`, before anything starts | "an address has no spaces in it", "the port after ':' must be a number from 1 to 65535", ... |
| room full | **the host only** (`JoinFlow.OnJoinRequest`) | the joiner gets the timeout line instead: `JOIN_REFUSED` still does not exist |
| no answer | the menu, after `joinTimeoutSeconds` (12 s) | "no answer from the host: it may be running a different version, be full, or not be reachable" |
| host left | `HostLost` | "the host left the room" |
| run over | `PhaseChanged(Ended)` | "the run is over", on the room page |
| labyrinth missing | `MenuFlow.IsInBuild` | "the labyrinth scene (Labyrinth) is not in the build settings yet" |

### S4.6 Two instances, now that the dev switch is gone

The S3.4 recipe still holds, but the mode is chosen in the menu rather than in
EditorPrefs or on a command line:

1. Editor: Play from `Boot` (or `MainMenu`), type a callsign, press **HOST**. The
   room page shows this machine's LAN address with the port; the console prints
   the same from `[tcp]`. Allow the Windows Firewall prompt on the first listen.
2. Build a Windows x64 player and run it. In its menu, type the host's address
   into the code field (`192.168.1.5:7777`, or `localhost:7777` on one machine)
   and press **JOIN**.
3. The host's room page should show two crew rows; press **START**. Both peers
   load the labyrinth — which does not exist yet, so until the labyrinth stage
   lands, point `MenuFlow.labyrinthScene` at `Zone1` in the Inspector to walk the
   whole flow end to end.

### S4.7 Riskiest untested assumptions

1. **Nothing has been run.** No button has ever been clicked.
2. **USS internals.** The text fields are styled through Unity's own class names
   (`.unity-base-text-field__input`, `.unity-base-field__label`). If Unity 6.3
   spells them differently the fields fall back to the default theme's look:
   ugly, not broken.
3. **Focus and navigation.** Tab, arrows and gamepad rely on UI Toolkit's default
   traversal in document order, and the first focus is set from a scheduled
   callback one frame after a page shows. Untested on a gamepad.
4. **The EventSystem's actions** come from `Assets/Input/InputSystem_Actions.inputactions`
   (`PeskyControls` has no UI map). All eight UI actions were bound explicitly and
   read back, but nothing has clicked a button through them.
5. **`ReturnToLobby` is a new edge on the phase machine.** `Ended -> Lobby` has
   never been sent before. `WorldSim.Apply(SessionPhaseMsg)` just sets the phase,
   and `EventSink.Emit` applies it to the host's own sim before the call returns,
   which is what makes START work immediately afterwards. A second run rebuilds
   the tables from the `WORLD_RESET` + `WEAPON_STATE` that `WorldAuthorityNet`
   sends when the level loads again.
6. **`GameLocator.FromMenu` is the only thing** stopping a level opened straight
   from the Editor from bouncing to the menu when its solo run ends.
7. **The first LAN address may be the wrong one** on a machine with a VPN or a
   virtual adapter; the alternatives line lists the rest, and `localhost:7777` is
   always last in it.
8. **Late join into a run in progress** hands the client `PhaseChanged(Playing)`
   from the snapshot before `Ready`, so it loads the level straight from the menu
   without ever seeing the room page. Reasoned, never seen.
9. **`MenuFlow` pumps the session only while the menu scene is loaded.** Anything
   that outlives the menu but not a level (nothing today) would stall.

### S4.8 Not done

`JOIN_REFUSED` (still, so "room full" is a timeout for the joiner). No settings
screen, no audio, no character or weapon picker, no appearance (the six
`Appearance` bytes are still ATCK's names). No pause menu and no in-level
"leave", so a player inside a level can only quit the application. No
reconnection and no host migration. The labyrinth size line is a serialized
sentence picked by crew count, not anything a generator reads. Nothing gates on
`SessionPhase` yet.

## Stage 5 — the labyrinth, as logic and data (2026-09-20)

**Implemented, untested.** Checks made: a clean compile after every change (0 CS
errors), the edit-mode scene validator on `MainMenu`, `Zone1` and `Dev/FeelBox`
(0 issues each), and the new asset read back through the Editor. **No play mode,
no test, no socket, no scene touched.** The full description of the model, the
rules and every tunable is `docs/LABYRINTH.md`; this section is only what changed
in the netcode.

`Wire.ProtocolVersion` is now **3** (reason: ten new ids and a new snapshot part).

### S5.1 What was added

| File | What |
|---|---|
| `Scripts/Data/LabyrinthDef.cs` | new. The ScriptableObject: grid size, room list (`LabyrinthRoomDef` = label / glyph / role), the Mage's limits, the round's thresholds, `MageCountFor`, `Problem()` |
| `Assets/Data/Labyrinth.asset` | new, 5x5 with 25 rooms (0 Weapon Rack / Start, 1 Resurrection Room / BadEnd, 2 Gate Hall / GoodEnd, 3-24 blank). Linked from `GameData.labyrinth` |
| `Scripts/Protocol/LabyrinthEnums.cs` | new. `Heading`, `LabyrinthRole`, `CompassTargetKind`, `RoundOutcome` |
| `Scripts/Protocol/Messages/LabyrinthMessages.cs` | new. The ten message structs |
| `Scripts/Sim/LabyrinthGrid.cs` | new. Plain C#: the table, wrapping, the one non-wrapping Exit doorway, swap validation with a connectivity hook, BFS shortest path, deterministic generation, the wire and snapshot codecs |
| `Scripts/Sim/LabyrinthState.cs` | new. `sim.Labyrinth`: the grid, legend, who is down, the outcome — and three **local-only** fields (own role, own compass) that are never snapshotted |
| `Scripts/Session/Rules/LabyrinthRule.cs` | new. Both `IHostRule` and the `IIntentValidator` for the Mage's two intents. Roles, swaps, bends, respawns, the two endings |
| `Scripts/Game/LabyrinthRoom.cs` | new. An authored room: room id, four doorways, footprint, anchor |
| `Scripts/Game/LabyrinthDirector.cs` | new. The one sim-to-scene mapping, under `_Managers` |
| `Scripts/Game/CompassModel.cs` | new. What the HUD reads |

Edited: `MsgId` (0x30-0x3F), `MessageInfo`, `Enums` (`SnapshotPartKind.Labyrinth = 5`,
`End = 6`), `Wire` (version 3), `GameData` (`labyrinth`), `WorldSim` (the
`Labyrinth` property, eight `Apply` overloads, the snapshot part, `Hash`, and a
rebuild when SESSION_INFO reseeds), `MessageApplier` (six cases **plus the new
`ApplyReply`**), `SnapshotCodec.Order`, `IHostWorld` (`CollectPlayerCells`),
`HostAuthority.CreateDefault` (the rule and its two validators, last),
`NetSession.RaiseReply` (now applies replies to the sim first), `MagicDoor` (grid
door mode) and `WorldAuthorityNet` (the director reference, seven C# events, the
requests, `ReportPlayerDown` / `ReportLegend`, `CollectPlayerCells`).

### S5.2 The ten messages

0x30 SWAP_REQ (I, 5) | 0x31 COMPASS_BEND_REQ (I, 5) | 0x32 LAB_LAYOUT (S, 12+2n,
62 bytes at 5x5) | 0x33 LAB_SWAPPED (E, 9) | 0x34 COMPASS_TARGETS (**Reply**, 4)
| 0x35 ROLE_ASSIGN (**Reply**, 2) | 0x36 ROUND_RESULT (E, 8) | 0x37 PLAYER_DOWN
(E, 10) | 0x38 PLAYER_RESPAWN (E, 9) | 0x39 LEGEND (E, 10). Layouts are in
`docs/LABYRINTH.md` section 3. 0x3A-0x3F are free.

### S5.3 How a secret stays secret

This is the first thing in the project that must not be told to everyone, so it
is worth being explicit about where the bodies are:

1. **The host's table of who is a Mage lives in `LabyrinthRule`**, a field of a
   plain C# object on the host. It is not in `WorldSim`, so it cannot reach a
   snapshot by accident.
2. **A Mage learns its own role from a `Reply`** — `EventSink.Reply(slot, ...)`,
   which enqueues to exactly one peer id. Nobody else is sent anything at all:
   a player who is told nothing is a weapon.
3. **The role draw uses a host-private `Rng` seeded from a `Guid`**, never
   `WorldSim.Rng`. That one is reseeded from SESSION_INFO on every peer, so a
   client could replay any draw made from it and name the Mage.
4. **`LAB_SWAPPED` and `COMPASS_TARGETS` carry no author.** A bent player learns
   only its own new target, and not that anybody set it.
5. **Every refusal is silent** — no reply, no event, no `Debug.Log`. A weapon
   that sends a SWAP_REQ to probe gets exactly what a Mage on cooldown gets.
6. **`ROUND_RESULT` is the one message that names the Mages**, and it goes out
   only when the round can no longer be played.
7. `LabyrinthState.MageMask` is in the snapshot but is **zero until
   ROUND_RESULT**, so a mid-round snapshot reveals nothing.

### S5.4 Two seams worth knowing

**`MessageApplier.ApplyReply`.** New. `NetSession.RaiseReply` calls it before
raising `ReplyReceived`, which covers both paths at once: a client's
`SessionRouter.OnReply` default branch, and the host's own
`EventSink.LocalReply`. Add a case there for any future host-to-one-peer secret.

**`SnapshotPartKind.Labyrinth = 5`.** `End` moved from 5 to 6, which resizes
`SnapshotReceiver`'s table array. Any future part goes **before `End`** the same
way, with an entry in `SnapshotCodec.Order` and a `WritePart` / `ReadPart` case.

Note also that the labyrinth deliberately does **not** bump `WorldSim.Rev`. Rev
means "the weapon and kit tables were replaced, rebuild the scene from them", and
a client running `SyncFromSim` on every room swap would put every loose weapon
back on its rest pose. Views watch `LabyrinthState.Rev` instead.

### S5.5 Riskiest untested assumptions

1. **Nothing has been run**, and there is no scene to run it in: no
   `LabyrinthDirector` exists in any scene, so at runtime today every rule in
   here is idle. The first real test is stage 6's scene.
2. **`CollectPlayerCells` is the whole round loop's eyesight.** It maps a
   streamed position to a room by `BoxCollider` footprint. A player above or
   below a footprint, or in the gap between rooms, is in no cell and counts for
   nothing — including for the respawn anchor. Footprints must be generous.
3. **The win checks run at 5 Hz off streamed poses.** A client's position is
   ~100 ms old on the host, so "everybody at the exit" is decided slightly late.
4. **Room swaps mid-flight.** `MagicDoor.Twin` resolves at the moment it is
   asked, which is the point — but it is asked inside `FixedUpdate`, and a swap
   that lands between the sensor's "before" sample and its "now" sample sends the
   traveller somewhere the client had not drawn yet. Reasoned, never seen.
5. **`MagicDoor.Twin` now does a linear scan** of the table per door per fixed
   step (25 cells x ~100 doors). Fine at grey-box size, not free.
6. **The connectivity check cannot fail today** (the wrapped grid is always
   connected with all doorways open), so that code path has never rejected
   anything and never will until a doorway can be barred.
7. **`Finish` emits ROUND_RESULT and SESSION_END with the same effect tick.**
   Clients apply both from `PendingEvents` in push order, which is assumed
   stable.
8. **The host regenerates the layout every round** (`WorldSeed +
   PhaseStartTick * 2654435761`), writing to its own sim outside the event path
   and then immediately broadcasting LAB_LAYOUT, which is what every peer
   including the host keeps. Correct as reasoned, but it is the one place the
   host's sim is touched by something other than an applied message.
9. **`exitGatherRadius` is measured to the exit doorway's transform**, which is
   the doorway plane's origin on the floor. If the authored doorway sits in an
   alcove the radius may need to grow.
10. **Nothing calls `ReportPlayerDown`**, so the respawn path, the anchor choice
    and the legend reset have never executed at all.

---

## Stage 6 — the labyrinth scene and its UI (2026-09-20)

**IMPLEMENTED, UNTESTED.** The full scene layout, the room prefabs and the UI
are written up in **docs/LABYRINTH.md sections 8 and 9**; this is only what a
netcode reader needs.

### S6.1 The labyrinth is no longer idle

`Assets/Scenes/Labyrinth.unity` exists, is **build index 3**, and its
`_Managers` carries `SessionRunner`, `LevelClock`, `WorldAuthority`,
`LabyrinthDirector`, `CompassModel`, `PlayerSpawner`, `PoseStreamer` and
`RemotePlayerSpawner`. `WorldAuthority.labyrinth` points at the director, so
`IHostWorld.CollectPlayerCells` now answers for real and **every rule in
`LabyrinthRule` runs for the first time**: round start, the two endings, the
respawn scan. `MenuFlow.IsInBuild(labyrinthScene)` now passes, so HOST reaches
the labyrinth instead of warning.

25 rooms, 100 grid doorways, 26 `RoomVolume`s and 7 weapons are registered in
the authority. Room id = index in `Assets/Data/Labyrinth.asset`, ids 0/1/2 are
Start / BadEnd / GoodEnd exactly as `LabyrinthGrid.Generate` expects.

### S6.2 What sends on the wire from the UI

Only two things, both already specified in stage 5, both refused in silence:

- `WorldAuthority.RequestRoomSwap(cellA, cellB)` from the map's drag. The view
  pre-checks with `LabyrinthGrid.CanSwap` so an illegal drop never leaves the
  machine, but the host re-checks everything anyway.
- `WorldAuthority.RequestCompassBend(mask, kind, cell)` from a player chip plus
  a tile / BAD END / UN-BEND.

Nothing else. The HUD reads `RoleLearned`, `RoundEnded` and `RoomsSwapped`, and
sends nothing in reply to any of them.

### S6.3 Secrets, in scene terms

- The whole Mage bar lives **inside** the map overlay, which is `display: none`
  while the map is closed — so it is not drawn and cannot even be picked.
- The role reveal reads `WorldAuthority.LocalRole`, which reads this peer's own
  sim. It is shown 1.5 s after the layout arrives so a `ROLE_ASSIGN` can land
  first, and again if one turns up late.
- `ROUND_RESULT`'s `mageMask` is the only place a name is printed, and only in
  the end banner.
- Doorway glyphs come from `MagicDoor.DestinationLabel/Glyph`, which read the
  shared table. They are public knowledge and they never lie.

### S6.4 Changes to existing files

| File | Change |
|---|---|
| `Assets/Scripts/Editor/SceneValidator.cs` | grid doorways are exempt from the twin / link-id checks (they have no authored twin) and are instead checked for a director, a room, and that the room lists them for that direction. New `CheckLabyrinth`: unique room ids in range, four doorways each pointing back, every room listed in the director, `LabyrinthDef.Problem()` clean, at least `width x height` rooms |
| `Assets/Input/PeskyControls.inputactions` | new `Gameplay/Map` button action, `<Keyboard>/tab` and `<Gamepad>/select`. Added through the `InputActionSetupExtensions` API, not by hand |
| `Assets/UI/` | new `Labyrinth.uxml` and `Labyrinth.uss` |

New scripts: `Game/DoorwayGlyph.cs`, `Game/ExitZone.cs`,
`Game/UI/LabyrinthHud.cs`, `Game/UI/LabyrinthMapView.cs`. No change to
`Pesky.Protocol`, `Pesky.Sim`, `Pesky.Session` or `Pesky.Transport`:
**the wire is untouched, `Wire.ProtocolVersion` is still 3.**

### S6.5 Riskiest untested assumptions

Stage 5's list (S5.5) all still stands, and #2 and #9 now have concrete numbers
to be wrong about. On top of it:

1. **Nothing has been run.** No play mode, no test, no socket. The scene has
   never been entered.
2. **100 `MagicDoor`s each run a `FixedUpdate` sensor scan** over every weapon
   and every soul, and each asks `IsPassable`, which does a linear scan of the
   25-cell table. That is ~100 x (7 + players) segment tests plus 100 table
   scans every physics step, in a scene where 99 of the doors have nobody near
   them. Fine at grey-box size, measured never.
3. **101 realtime lights** (100 torches, range 22, plus the directional). URP's
   per-object additional-light limit is 4 and each room has exactly 4, so it
   should fit — but this was reasoned from the room layout, not seen.
4. **The compass needle's screen angle** is
   `Mathf.DeltaAngle(cameraYaw, atan2(dir.x, dir.z))` fed into a UI Toolkit
   `Rotate`, which is clockwise-positive with y down. Reasoned to be correct;
   if it is mirrored, negate that one expression.
5. **The cooldown rings use `Painter2D.Arc`**, which compiles but has never
   drawn a pixel here.
6. **The Mage's drag uses pointer capture on a tile.** `PointerUpEvent` is
   assumed to reach the captured tile wherever the pointer ends up, and the drop
   cell is found by hit-testing `worldBound` over the tiles.
7. **Pointer lock is only released for a Mage.** If a weapon ever needs to click
   the map, `SetMapOpen` is the one place to change.
8. **`LabyrinthHud` binds in `OnEnable` off `document.rootVisualElement`**, the
   same pattern `HudController` uses in `Zone1`. If that root is null on the
   first enable, the whole page silently never binds.
9. **Both `UIDocument`s share `UiPanelSettings`** and are ordered by
   `sortingOrder` (HUD 0, labyrinth 1). Assumed, not seen.
10. **The weapon rack was slid 7 m west** so the 10 m rack stops blocking the
    3 m north doorway. Read back from the instance, never launched through.

---

## Stage 7 — the tutorial scene, and the owner's test guide (2026-09-20)

**IMPLEMENTED, UNTESTED.** Checks made: a clean compile after every script change
(`EditorUtility.scriptCompilationFailed` false, 0 CS errors), the edit-mode scene
validator on **all four build scenes** (`Boot`, `MainMenu`, `Tutorial`,
`Labyrinth`: 0 problems each), the build list and every serialized value read
back through the Editor, and a NavMesh bake. **No play mode, no test, no socket,
no screenshot.** The wire is untouched: `Wire.ProtocolVersion` is still **3**.

The owner's hand-test guide is **`docs/TEST-CHECKLIST.md`** — how to run solo,
how to pair the Editor with a Windows build, and the ordered list of checks. It
also gathers every stage's riskiest assumptions in one place.

### S7.1 The scene flow, final for this pass

```
Boot (0)       -> MainMenu
MainMenu (1)   HOST / JOIN -> room page -> START -> Labyrinth
               TUTORIAL    -> offline loopback session -> Tutorial
Tutorial (2)   rooms 1-5 of the old Zone1 + a practice labyrinth -> back to the menu
Labyrinth (3)  the real thing
```

**Build settings are exactly those four, in that order, all enabled.** `Zone1`,
`Bridge`, `MainTower` and `Keep` are still in the project, still open fine in the
Editor, and are **out of the build**. `MenuFlow.tutorialScene` was `Zone1` in the
scene (stage 4's placeholder) and is now `Tutorial`.

### S7.2 `Assets/Scenes/Tutorial.unity`

Made with `AssetDatabase.CopyAsset` from `Zone1.unity` and then cut down inside
the Editor. `Zone1.unity` itself was never opened for writing and its file on
disk is untouched.

Removed from the copy: `Room06_RopeRoom` … `Room20_GateGuard`, `RoomA_LiftBottom`,
`RoomB_LiftStop`, `RoomC_KeyAlcove`, `RoomD_SecretRoom`, `BackTowerShell`,
`Room2_Goblin/.../MagicDoor_2_to_A` (its twin was in the lift shaft) and
`_Managers/FloorActivator` — 22 objects. Deleting them left **113 null entries**
in `WorldAuthority`'s arrays, which were then compacted through `SerializedObject`
(every Pesky array in the scene was swept, not just the authority's).

Kept and unchanged: rooms 1-5 with their platforming, goblins, key, plate, anvil
and rune, the four hallways, `_Managers` (minus the FloorActivator), `_Cameras`,
`_Lighting`, `_UI/HUD`. The arena's far doorway is still there, renamed
`MagicDoor_5_to_Practice`, and its twin is now the practice labyrinth's portal.

`Environment`'s `NavMeshSurface` was re-baked. Note: `Zone1` keeps its NavMeshData
**embedded in the scene file**; the Tutorial's is a proper asset at
`Assets/Scenes/Tutorial/NavMesh-Environment.asset` (built with `BuildNavMesh()`
then `CreateAsset`, which is what the Bake button does). 1,267 objects, 679 static.

### S7.3 `Room6_MageTutorial` — the practice labyrinth

Under a new scene root **`Room6_MageTutorial`**, 9 rooms on a 3x3 lattice at 120 m spacing
from `(300, 0, 0)`: room id `i` sits at `(300 + (i%3)*120, 0, -(i/3)*120)`, far
from Zone1's geometry (which ends at z 115, x ±11) and far enough apart that the
56 m footprints cannot touch. The prefabs are the labyrinth's own —
`LabyrinthRoom` (7 of them), `LabyrinthRoom_BadEnd` (room 1),
`LabyrinthRoom_Exit` (room 2). **No tutorial copy of any labyrinth system
exists**: the director, the grid doorways, the compass, the map, the Mage's two
powers and both endings are the same components and the same rule.

**Deviation from the brief, deliberate: the practice grid is 3x3, not 3x1.**
Three reasons, all of them in the existing rules: `LabyrinthDef.Problem()`
refuses a side smaller than 3 (`MinSide`); `LabyrinthGrid.Generate` puts Start in
the centre cell and the two ends in two **corners**, so in a 3-cell grid every
cell is fixed; and `CanSwap` refuses a swap touching a fixed cell — a 3x1 strip
could not teach the one thing the lesson is for. 3x3 is the smallest grid the
real rules allow, and it teaches wrapping as well. Six cells in a row would also
have worked; nine is the honest miniature.

New data assets:

| Asset | What |
|---|---|
| `Assets/Data/Labyrinth_Tutorial.asset` | `LabyrinthDef`, 3x3, 9 rooms (0 Entry Hall/Start, 1 Resurrection Room/BadEnd, 2 Gate Hall/GoodEnd, 3-8 blank). Swap and bend cooldowns **5 s** (a tutorial should not wait 20), and **`resurrectionFraction = 1`** so a lone player standing in the Resurrection Room can never trip the Mage's instant win: the check is `inBad > fraction * present`, and 1 > 1 is false |
| `Assets/Data/GameData_Tutorial.asset` | a copy of `GameData.asset` whose `labyrinth` is the above. Weapons, enemies, modifiers and every tuning number are the same values — **a copy, so it does not follow later edits to `GameData`** |

`MenuFlow` gained an optional `tutorialData`; the only offline session the menu
starts is the tutorial, so `StartSession` uses it there and `gameData` everywhere
else. `Tutorial.unity`'s own `SessionRunner.gameData` is the tutorial asset too,
so opening the scene straight from the Editor behaves the same.

Wiring in the scene: `_Managers/LabyrinthDirector` (fallback def = the tutorial
def, 9 rooms) and `_Managers/CompassModel`; `_UI/LabyrinthHud` (a second
`UIDocument`, sortingOrder 1, `Labyrinth.uxml` + `Labyrinth.uss`, the same
`UiPanelSettings` as the HUD); `WorldAuthority.labyrinth` = the director, with
37 magic doors, 15 room volumes and 5 doors registered. Scene ids for everything
new run **2001..2075** (Zone1's are 101..1301).

The lesson, in order, is authored as signs and one gate:

1. `Room6_MageTutorial/PRoom_00_EntryHall/TutorialGate_MageLesson` — a 22x8x22 trigger over
   the whole room. The first weapon into it calls `LabyrinthHud.Wake()`.
2. Three signs in the Entry Hall: one on the hidden Arch Mage ("in here, that is
   you"), one on the compass and the doorway glyphs, one on Tab, the drag-swap
   and the dummy chip.
3. The role reveal is timed **from the wake**, so "YOU ARE A FRAGMENT OF THE ARCH
   MAGE" lands 1.5 s after the player walks in rather than at scene load.
4. A sign in the Resurrection Room and a sign in the Gate Hall.
5. The four `ExitZone`s of the Gate Hall each carry a `TutorialTrigger` with
   `endsTutorial`. Their collider is **only enabled while that doorway is the
   Exit** (`ExitZone` already does that), so the lit ring is the only one that
   can fire, and it is the same spot the real round is won on.

The player arrives from the arena through `MagicDoor_Practice_to_5`, a plain
(non-grid) `MagicDoor` instance inside the Entry Hall, link id 1, twinned with the
arena's door, its `DoorCondition` set to AlwaysOpen. It is deliberately **not**
one of the room's four doorways — those must stay grid doors, and the validator
enforces it.

### S7.4 The four script changes

| File | Change |
|---|---|
| `Scripts/Game/TutorialTrigger.cs` | **new**. A trigger box that fires once for the first weapon that enters: switches objects on/off, optionally wakes the labyrinth HUD, and optionally ends the run. Ending is `NetSession.EndRun(Escaped)` — the same call a rule makes — followed by `Leave()`, so the menu finds a session that is not started and shows the **title** page rather than a room page offering to START a run on the tutorial's data |
| `Scripts/Game/UI/LabyrinthHud.cs` | `startAsleep` (draw nothing until `Wake()`), `practiceChipName`, `Wake()`. Both new fields are off/empty in `Labyrinth.unity`, so the real HUD is unchanged. `Wake()` also re-arms the role reveal |
| `Scripts/Game/UI/LabyrinthMapView.cs` | `PracticeName`: one extra chip on the Mage bar, slot **8** (outside the eight real ones). Selecting it and picking a tile or BAD END sets a local flag and writes a line under the bar — **no `COMPASS_BEND_REQ` is sent for it and no peer knows it exists**. Chip building moved into `AddChip`; `Bend` grew one branch; `RefreshHint` prints the dummy's state instead of the "BENT n / max" line while a practice name is set |
| `Scripts/Game/UI/MenuFlow.cs` | optional `tutorialData` (above) |

No change to `Pesky.Protocol`, `Pesky.Sim`, `Pesky.Session` or `Pesky.Transport`.

### S7.5 What the tutorial actually exercises

Because there is exactly one player, `LabyrinthDef.MageCountFor(1)` is 1: **the
tutorial player is always the Mage**, from the real `AssignRoles`, through a real
`ROLE_ASSIGN` reply. That also means `CheckEndings` can never reach the escape
branch (`nonMage == 0`), which is why the lit ring ends the tutorial through
`TutorialTrigger` instead. Everything else is the real path: LAB_LAYOUT,
SWAP_REQ → LAB_SWAPPED, the cooldowns, the compass, the map, the glyphs,
`CollectPlayerCells`, and `SESSION_END` → `SessionRunner` → the menu.

While the player is still in rooms 1-5 they are inside no labyrinth footprint, so
they are in no cell, the compass has no reading and both endings stay quiet.

### S7.6 Riskiest untested assumptions

1. **Nothing has been run.** The tutorial has never been walked.
2. **`TutorialTrigger` only sees weapons** (souls are on a layer that never
   touches Trigger, exactly as `ExitZone` assumes). A player who arrives in the
   Entry Hall as a soul never wakes the HUD.
3. **`LabyrinthHud.Wake()` and the sleeping HUD.** The object is active from the
   start (so `UIDocument` binds normally) and simply draws nothing; if `_awake`
   were wired the other way round the whole page would stay blank for good.
   `ShowRole()` is gated on it too, because `ROLE_ASSIGN` lands seconds after the
   scene loads — an ungated reveal would sit on the screen through rooms 1-5,
   since the `Update` that hides it is the one that is asleep.
4. **The portal's orientation.** `MagicDoor_Practice_to_5` stands at local
   (8, 0, 9) with yaw 180, forward `(0,0,-1)`, so an arrival from the arena is
   thrown south into the room. Read back from the instance, never travelled.
5. **The four `ExitZone` triggers end the run.** It relies on `ExitZone`
   disabling the collider of the three doorways that are not the Exit — its own
   `Update`, running at 4 Hz. In the first quarter-second of the scene all four
   may be enabled; the player is three rooms away at the time.
6. **`EndRun` then `Leave` in the same frame.** `Leave` sees the phase already
   `Ended` so it does not send `HostLeft`; `SessionRunner` has already queued the
   trip to the menu and does it on the next `Update`, where `Session.Update()`
   returns early because the session is no longer started. Reasoned, not seen.
7. **The practice grid is generated from the round's seed**, so which corner is
   the Resurrection Room and which the Gate Hall changes every time, as does
   which side the Exit is on. The signs never name a direction.
8. **`GameData_Tutorial` is a copy.** Change a weapon in `GameData.asset` and the
   tutorial keeps the old number.
9. **The map's dummy chip** shares the Mage bar with the real ones. It is skipped
   by slot number (8), so any future code that assumes a chip's `userData` is a
   real slot must check.
10. **The bake.** The NavMesh now covers 565 x 368 m because the practice rooms
    are in the same scene and the surface collects everything. No goblin can ever
    reach them; it is bake time, not runtime cost.

### S7.7 Not done

The tutorial teaches nothing about **other players** — it cannot: it is one
player on loopback. Batting, possession contests, goblins seeing a friend move
and both round endings are only in `TEST-CHECKLIST.md`, as things to try with two
instances. There is still no pause menu and no in-level LEAVE, so the only way
out of the tutorial other than the exit ring is quitting. `JOIN_REFUSED`,
reconnection and host migration are still missing.

---

## Feedback round 2 — the scratch pad, and what changed on the wire (2026-09-20)

**Implemented, untested.** Checks made: a clean compile after every change, the
edit-mode scene validator on `Boot`, `MainMenu`, `Tutorial` and `Labyrinth`
(0 problems each), every new message encoded and decoded once in the Editor to
confirm its byte count, and a full snapshot built and read back. **Nothing was
run: no play mode, no test, no socket.**

`Wire.ProtocolVersion` is now **4** (reason: three new ids and a new snapshot
part).

### F2.1 The three new messages — the shared scratch pad, 0x3A-0x3C

The pad is the one shared surface in the game that **is not a secret**: every
peer keeps the same ordered list of strokes and paints them in the same order, so
everybody sees the same picture. A Mage drawing a lie on it is a legitimate move.

| Id | Name | Kind | Bytes | Layout after the type byte |
|---|---|---|---|---|
| 0x3A | PAD_STROKE_REQ | Intent | 4 + 4n (max **260**) | `flags u8 (PadFlags: 1 Erase), width u8 (class 0..2), count u8, (x u16, y u16) x count` |
| 0x3B | PAD_STROKE | Event | 11 + 4n (max **267**) | `tick u32, seq u16, slot u8, flags u8, width u8, count u8, (x u16, y u16) x count` |
| 0x3C | PAD_CLEAR | Event | **5** | `tick u32` |

0x3D-0x3F are free.

**Coordinates** are normalised canvas coordinates quantised to u16: 0 is the
left / top edge, 65535 the right / bottom, so the drawing is the same on every
screen whatever the window size. **At most 64 points ride one message**
(`PadStrokeReqMsg.MaxPoints`), which is 260 bytes — far below the 16,000 byte
FRAME ceiling. A longer stroke is split by the drawer into several messages that
**share their joining point**, so the line has no gap.

**Snapshot.** `SnapshotPartKind` gained `Pad = 6` and `End` moved to `7` (which
resizes `SnapshotReceiver`'s table array, exactly as `Labyrinth = 5` did).
`SnapshotCodec.Order` ends with `Pad`. The part body is
`count u16 | per stroke: seq u16, slot u8, flags u8, width u8, n u8, points u16 x 2n | lastSeq u16`.
Measured in the Editor at the asset's caps: **16,248 bytes, 2 chunks**, and the
biggest payload in a whole snapshot is 15,996 of the 16,000 allowed.

**A FRAME can never burst.** `Outbox.TakeFrame` already packs only what fits one
frame and spills the rest to the next flush, oldest first, so even eight players
emptying their burst at once costs extra ticks, never an oversized frame.

### F2.2 `PadRule` — `Assets/Scripts/Session/Rules/PadRule.cs`

Registered last in `HostAuthority.CreateDefault()`, as both an `IHostRule` and
the `IIntentValidator` for PAD_STROKE_REQ.

- **Wipes the pad on the first tick of `SessionPhase.Playing`** (PAD_CLEAR), so a
  new round starts on blank paper. `LabyrinthRule` is registered before it, so
  LAB_LAYOUT goes out first.
- **Caps**, all from `LabyrinthDef`: 1..64 points per message, width class 0..2,
  and a per-slot token bucket refilled at `padStrokesPerSecond` (20) up to
  `padStrokeBurst` (40). The buckets **start full**, so the first line a player
  draws is never the one thrown away.
- **Refusals are silent**, like every other refusal in this project. The drawer
  finds out only because its own optimistic copy fades after 3 s.
- `PadRule.ClearPayload` is the one place a PAD_CLEAR is built, so the host's own
  CLEAR button goes out the same way.

### F2.3 `ScratchPadState` — `Assets/Scripts/Sim/ScratchPadState.cs`

`sim.Pad`. An ordered `List<PadStroke>` plus `LastSeq` and a local-only `Rev` a
view can watch. **Appending is the shared order**: events reach every peer in
tick order and, inside a tick, in the host's push order, so the sequence number
is carried for diagnostics and for a drawer to recognise its own stroke coming
back — never for sorting.

Both caps trim the **oldest** strokes: `padMaxStrokes` (300) and `padMaxPoints`
(4,000 across every stroke together). Like `LabyrinthState`, it deliberately does
**not** bump `WorldSim.Rev` — a stroke every few seconds must not make a client
rebuild its weapons from the rows.

### F2.4 Game side

`WorldAuthority` gained `Pad`, `RequestPadStroke(points, erase, width)`,
`RequestPadClear()` (host only, refused on a client) and two C# events,
`PadStroked(slot)` and `PadCleared`. Nothing else on the wire changed: the
compass restyle, the floor numbers and the doorway labels are all view and data.

**The drawer does not wait for the host.** `ScratchPadView` paints its own
strokes the moment they are drawn and hands each one over when
`PadStroked(localSlot)` says the sim has it (oldest first, FIFO). Offline that
round trip is synchronous inside `NetSession.Send`, so the optimistic copy is
added **before** the send and popped immediately.

### F2.5 Riskiest untested assumptions

1. **Nothing has been run.** No play mode, no test, no socket.
2. **A refused stroke lingers for 3 s** on the drawer's own screen and on nobody
   else's. That is the only feedback a rate-capped player ever gets.
3. **Stroke order between peers is assumed to be the host's push order.** Two
   players drawing at the same instant may end up in a different order than
   either of them saw locally, which matters only where an eraser crosses
   somebody else's line.
4. **The canvas is a fixed 760 x 470 px in USS**, so every peer shares the same
   aspect and the normalised coordinates line up. Change the size in USS and old
   strokes in a live snapshot stretch.
5. **`PointerCaptureOutEvent` is assumed to fire when the overlay closes**;
   `ScratchPadView.EndStroke` flushes and releases by hand as well.
6. **The eraser paints in `ScratchPadView.Canvas`**, which is also written onto
   the element's `backgroundColor` at runtime so the USS and the code cannot
   drift. If a future page tints the canvas, the eraser stops matching.
7. **8 players x 40 strokes of 267 bytes** is 85 KB of broadcast in one burst.
   The Outbox spills it over about six ticks; never measured.
