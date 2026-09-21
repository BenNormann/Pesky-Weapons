# NETCODE-PORT.md — porting ATCK's multiplayer stack into Pesky Weapons

Source of truth: `C:/Users/benos/Code/ATCK`, read-only. Every citation below is a path
relative to that repo root (the Unity project is the folder `ATCK Unity/`).
Target: `C:/Users/benos/Code/Pesky Weapons`, Unity 6000.3.23f1, URP, WebGL.

What Pesky Weapons needs that ATCK does not have, and which therefore drives every
"adapt" decision in this document:

1. **Full rotation sync.** ATCK's player pose is `pos:f32×3 + yaw:f32 + seq:u16`,
   19 bytes (`ATCK Unity/Assets/Scripts/Protocol/M0Messages.cs:22`). A tumbling weapon
   needs a quaternion and a linear velocity. Section 6 has the replacement layout.
2. **Non-deterministic host-simulated NPCs.** ATCK has **none**. Its aircraft are a
   pure function of the spawn event plus room time
   (`docs/ARCHITECTURE.md:158`), so they never stream a pose. NavMesh goblins cannot be.
   The closest existing template is the periodic `TOWER_STATE` row
   (`ATCK Unity/Assets/Scripts/Session/Rules/TowerRule.cs:34`, `:51`, `:88`, `:199`);
   section 5.6 turns it into an `ENEMY_STATE` schedule.

Everything else — transport, frames, clock, join handshake, host authority, snapshot,
slots — ports essentially unchanged.

---

## 1. Minimal file set

Actions: **copy** = byte-for-byte apart from `ATCK.` → `Pesky.` in the namespace and
`using` lines; **rename** = same, plus a type/const rename; **adapt** = real edits;
**skip** = ATCK gameplay, do not bring.

"ATCK types referenced" is the seam list: those are the compile errors you will see
first, and each one is a decision.

### 1.1 Transport — `ATCK Unity/Assets/Scripts/Transport/`

| File | ~Lines | Action | ATCK types referenced (the seams) |
|---|---|---|---|
| `INetTransport.cs` | 26 | copy | none |
| `IVoiceControl.cs` | 18 | copy | `VoiceMode` |
| `VoiceMode.cs` | 13 | copy | none |
| `LoopbackTransport.cs` | 65 | copy | `INetTransport`, `IVoiceControl` |
| `WebRtcTransport.cs` | 231 | copy | `INetTransport`, `IVoiceControl`. **Do not rename the GameObject** — `GameObjectName = "NetBridge"` (`:24`) is the wire contract with `net.js` (`:340` in net.js). The `AHNet_*` DllImport names (`:52`–`:60`) must match `AHNet.jslib`. Renaming either is a silent no-op. |
| `ATCK.Transport.asmdef` | 14 | rename | → `Pesky.Transport` |

### 1.2 Protocol — `ATCK Unity/Assets/Scripts/Protocol/`

| File | ~Lines | Action | ATCK types referenced |
|---|---|---|---|
| `Tick.cs` | 16 | copy | none. 50 ms / 20 Hz. |
| `NetWriter.cs` | 91 | adapt | none. Copy as-is, then **add** `Cm()`/`PosCm()` (i16 centimetres) and `Quat()` (smallest-three) for section 6. |
| `NetReader.cs` | 94 | adapt | none. Same two additions on the read side. |
| `Frame.cs` | 98 | copy | `MsgId.Frame` only. The 0x7F container and the 16,000-byte ceiling. |
| `Wire.cs` | 42 | adapt | none. Keep `ProtocolVersion`, `NoSlot`, `NoId`, `MaxPlayers`, `MaxBatch`, `HasId`, `BatchCount`. **Delete `NoCompany` (`:23`)**. Reset `ProtocolVersion` to 1 (`:14`). Raise `MaxPlayers` from 8 if you want more than 8 weapons (`:25`; it sizes `PeerSlots._slots`, `SessionRouter._poseSeq` and `Outbox`'s dictionaries — all just capacities, no wire impact, because slots are a `u8`). |
| `Headers.cs` | 48 | adapt | `EventHeader` (keep, `:8`–`:17`). **Delete `AircraftHeader` (`:24`)** — and with it the 5-byte offset special case in `MessageApplier.TryEventTick` (see 1.4). |
| `MsgId.cs` | 105 | adapt | Keep 0x01–0x03 (M0), 0x10–0x19 (session/clock) and 0x7F verbatim. Replace 0x20–0x7E with Pesky's own domains. Keep the reservation comment on `0x1A–0x1F` for host migration (`:26`). |
| `MessageInfo.cs` | 98 | adapt | Every `XxxMsg`. Structure is right; the `BuildTable()` body (`:21`–`:96`) is one line per message and must be rewritten for the new id set. |
| `Enums.cs` | 286 | adapt | Keep only `MsgKind` (`:10`), `SessionPhase` (`:21`), `SessionEndReason` (`:23`, prune reasons), `SnapshotPartKind` (`:82`), `VictimKind` (`:149`), `HitPart` (`:151`), `HitRejectReason` (`:156`), `DeathCause` (`:260`), `PlayerFlags` (`:263`). The other ~30 enums (`ShiftKind`, `DayPhase`, `PlaneKind`, `AtcVerb`, `EconReason`, `TowerState`, …) are ATCK gameplay — skip. |
| `Appearance.cs` | 79 | adapt | none. 6 bytes on `JOIN_REQUEST` and every `PEER_SLOTS` row. Reinterpret the six bytes as weapon type / material / trim colours, or shrink to 2 bytes; the plumbing (`SessionMessages.cs:156`, `:284`; `PeerSlots.cs:43`, `:92`) stays. |
| `M0Messages.cs` | 77 | **adapt (key file)** | none. `TRANSFORM` becomes the tumbling pose — section 6. `HELLO` and `PING` and `SeqIsNewer` (`:68`) stay. |
| `Messages/SessionMessages.cs` | 368 | adapt | `SessionInfoMsg` (`:15`) is half ATCK airfield fields — strip `runwayCount`, `layoutId`, `day`, `dayPhase`, `shiftDays`, `hazard`, `modifiers`, `promoted`, `airportName`; keep `protocolVersion`, `worldSeed`, `phase`, `phaseStartTick`, and add a dungeon/floor id. `SessionPhaseMsg` (`:233`) likewise. **Copy `TimeSyncMsg`, `ClockPingMsg`, `ClockPongMsg`, `JoinRequestMsg`, `SnapshotPartMsg`, `PeerSlotsMsg`, `SessionEndMsg`, `ResyncReqMsg` as-is.** |
| `Messages/PlayerMessages.cs` | 251 | adapt | Template. `PlayerStateMsg` (`:9`) → weapon flags + charge state. `PlayerDeathMsg` (`:46`), `PlayerRespawnMsg` (`:80`) map straight onto weapon-HP death and respawn. `WeaponDrop/PickupReq/Grant` (`:118`, `:176`, `:196`) map onto keys and rune pickups. |
| `Messages/CombatMessages.cs` | 229 | adapt | Read once as the pattern for batched `State` rows (`HealthStateMsg`, `:98`) and batched `Intent` claims (`HitClaimMsg`, `:45`). |
| `Messages/{Aircraft,Atc,Economy,Spin,Structure,Strafe,Ground,Day,Induction,Licence,Vehicle,Score,SeatSlots}Messages.cs` | ~1,900 | **skip** | ATCK gameplay. |
| `ATCK.Protocol.asmdef` | 14 | rename | → `Pesky.Protocol` |

### 1.3 Session — engine files, `ATCK Unity/Assets/Scripts/Session/`

| File | ~Lines | Action | ATCK types referenced |
|---|---|---|---|
| `NetSession.cs` | 356 | adapt | `WorldSim`, `GameData`, `ShiftKind`, `RunContract`, `RunModifier`, `AirportNames`, `Sim.Economy.SharedPot`. Cuts: `StartGame(ShiftKind,…)` (`:205`), `SetAirportName` (`:226`), `Resume`/`SaveResume` (`:55`, `:98`), and `Sim.Economy.SharedPot` in `EndSession` (`:317`). Everything else — the five per-frame steps (`:134`), `Send`/`SendNow` (`:154`, `:181`), resync (`:266`–`:309`) — is verbatim. |
| `NetSessionExtensions.cs` | 77 | copy | `GameData` (one parameter type). This file exists **only** because Game may not reference Transport (`:8`–`:13`); keep that rule. |
| `SessionRouter.cs` | 203 | copy | `MsgId.Transform/Hello/Ping`, `Sim.SetPlayerPose`. Only `AcceptPose` (`:187`) changes, for the new pose payload. |
| `JoinFlow.cs` | 207 | adapt | `BuildSessionInfo()` (`:186`) reads ~12 ATCK sim fields; `OnPeerLeft` (`:59`) reads `Sim.Economy.SharedPot`. The handshake itself (`:78`–`:172`) is verbatim. |
| `MessageApplier.cs` | 90 | adapt | The whole `Apply` switch (`:19`–`:72`) is one case per message — rewrite for the new ids. `IsAircraftEvent` (`:77`) is deleted and `TryEventTick` (`:80`) simplifies to a fixed offset of 1. |
| `EventSink.cs` | 115 | adapt | `AircraftState`. Delete `SpawnHeader`/`NextHeader` (`:62`, `:65`). `LeadTicks = 3` (`:20`), `Emit` (`:69`), `Reply` (`:86`), `ReplyNow` (`:100`) are verbatim. |
| `HostAuthority.cs` | 103 | adapt | Every ATCK rule and validator. Keep `AddRule`/`SetValidator`/`Tick`/`Handle` (`:23`–`:49`) exactly; replace `CreateDefault()` (`:58`–`:101`) entirely. |
| `IHostRule.cs` | 15 | copy | `WorldSim` |
| `IIntentValidator.cs` | 15 | copy | `WorldSim` |
| `HostIds.cs` | 46 | adapt | `Wire.NoId`. Rename the counters: `NextEnemy()`, `NextPickup()`, `NextDoor()`; keep `NextSnapshot()` (`:33`) and the wrap logic (`:39`). |
| `Inbox.cs` | 92 | copy | `INetTransport` |
| `InboxItem.cs` | 13 | copy | none |
| `InboxKind.cs` | 12 | copy | none |
| `Outbox.cs` | 109 | copy | `Frame`, `Wire.MaxPlayers`, `INetTransport` |
| `PendingEvents.cs` | 54 | copy | none |
| `PeerSlots.cs` | 269 | copy | `Appearance`, `PeerSlotsMsg`, `Wire` |
| `RoomClock.cs` | 147 | copy | none (plain `System.Diagnostics.Stopwatch`) |
| `ClockPinger.cs` | 50 | copy | `ClockPingMsg` |
| `SnapshotCodec.cs` | 83 | adapt | `WorldSim.WritePart/ReadPart`, `SnapshotPartKind`. Only the `Order` array (`:18`–`:22`) changes. |
| `SnapshotReceiver.cs` | 122 | copy | `SnapshotPartKind`, `WorldSim` |
| `TransportFactory.cs` | 25 | adapt | none. Add a desktop-transport branch (section 8). |
| `Rules/ClockRule.cs` | 37 | copy | `WorldSim`. The `_nextSyncTick` pattern (`:20`) is the template for every scheduled State row. |
| `Rules/RespawnRule.cs` | 48 | adapt | `Crew`, `EconomyTuning`. Structure is exactly what weapon respawn needs (`:30`–`:46`). |
| `Validators/PickupValidator.cs` | 64 | adapt | `sim.Drops`, `PlayerState.weapons`. The best single worked example of an `IIntentValidator` (range check → `Emit(grant)` → side-effect `Emit(drop)`). |
| `Damage.cs` | 315 | adapt | `AircraftState`, `TowerState`, `Economy`, `KillBounty`. Keep the **principle** — one file is the only way anything loses HP, and it emits the `HEALTH_STATE` row plus the death sequence (`:285`) — and rewrite the body for weapons, goblins and bosses. |
| `Rules/*` (other 36), `Validators/*` (other 11), `SaveFile.cs`, `SaveCodec.cs`, `SaveResume.cs`, `Pricing.cs`, `KillBounty.cs`, `RunwayGate.cs`, `RunwayCraters.cs`, `PavementDamage.cs`, `Wrecks.cs`, `AutomationGate.cs`, `DebugCommands.cs` | ~6,000 | **skip** | ATCK gameplay. |
| `ATCK.Session.asmdef` | 14 | rename | → `Pesky.Session` |

### 1.4 Sim — `ATCK Unity/Assets/Scripts/Sim/`

`WorldSim` is ~1,000 lines of ATCK airfield state and is **not** portable. What you must
recreate is only its *shape*, because Session calls exactly this surface:

- `WorldSim(GameData data, uint seed)` (`Session/NetSession.cs:112`)
- `uint Tick { get; }`, `void AdvanceTo(uint tick)` (`Sim/WorldSim.cs:23`, `:127`)
- `SessionPhase Phase`, `SessionEndReason EndReason`, `int FinalScore`, `uint WorldSeed`
- `void Apply(in XxxMsg)` once per message id (`Sim/WorldSim.cs:162`, `:245`, `:251`, `:301`, `:345`)
- `void SetPlayerPose(byte slot, Vector3 pos, float yaw)` (`Sim/WorldSim.cs:158`) — **this signature changes** to carry a quaternion and a velocity
- `PlayerTable Players` indexed by slot, with `present`, `alive`, `pos`, `yaw`, `pitch`, `flags`
- `bool WritePart(SnapshotPartKind, NetWriter)` / `bool ReadPart(SnapshotPartKind, NetReader)`

| File | ~Lines | Action |
|---|---|---|
| `Sim/Rng.cs` | ~60 | copy — seeded xorshift, the host's world RNG |
| `Sim/SimHash.cs` | ~80 | copy — the determinism/desync check the EditMode tests use |
| `Sim/PlayerTable.cs` + `PlayerState.cs` | ~400 | adapt — the row becomes a weapon: pos, rotation, velocity, hp, flags, held rune slots |
| `Sim/WorldSim.cs` (+ partials), `AircraftTable`, `TowerTable`, `Economy`, `FlightPath`, `Airfield`, `AirportNames`, … | ~5,000 | **skip / rewrite** — new tables: `Enemies`, `Doors`, `Pickups`, `Plates` |
| `ATCK.Sim.asmdef` | 14 | rename → `Pesky.Sim` |

### 1.5 Game layer — `ATCK Unity/Assets/Scripts/Game/`

| File | ~Lines | Action | Notes |
|---|---|---|---|
| `World/GameLocator.cs` | 30 | adapt | Static holder for the live `NetSession`. Drop `PlatformProfile` if you do not want it. |
| `UI/RoomCodes.cs` | 37 | copy | Alphabet + generate + validate. |
| `UI/Clipboard.cs` | 28 | copy | Calls `AHNet_CopyClipboard` from the same jslib (`:16`). |
| `Player/PoseStreamer.cs` | 92 | adapt | The 15 Hz / 5 Hz cadence engine. Section 6 rewrites `SendTransform` (`:82`) and the epsilons (`:21`–`:23`). |
| `Player/RemotePlayerSpawner.cs` | 149 | adapt | One view per remote slot, created on `SlotAdded`, fed from the sim row each frame (`:93`–`:123`). Structure is exactly right; the per-frame feed changes shape. |
| `Player/RemoteCharacter.cs` | 279 | adapt | **Keep the interpolation core**: `InterpDelay = 0.10f` (`:28`), `SampleKeepSeconds = 2f` (`:29`), the `Sample` ring (`:38`, `:135`) and `Interpolate()` (`:196`–`:222`). Delete everything about the humanoid rig, labels aside. |
| `Boot/Bootstrap.cs` | 402 | adapt | The lifecycle spine: creates the one `NetSession` in `Awake` (`:143`), `DontDestroyOnLoad` (`:131`), `[DefaultExecutionOrder(-1000)]` (`:23`) so `Session.Update()` (`:159`) runs before every view, `Host`/`Join`/`PlayOffline` (`:279`–`:295`). Strip the ATCK screens. |
| `Boot/WorldLifecycle.cs` | 123 | adapt | Additive scene load + per-session `WorldRoot` (`:88`–`:97`). |
| `Boot/Layers.cs` | 52 | adapt | Layer-name → index cache. New names in section 9. |
| `UI/Lobby.cs`, `UI/CrewList.cs`, `UI/DeathOverlay.cs`, `Boot/ClickToPlayGate.cs`, `Voice/VoiceDirector.cs`, `Player/RespawnHandler.cs` | ~1,200 | adapt/reference | Read for the pattern; rewrite for the new UI. `ClickToPlayGate` is load-bearing on WebGL (first click unlocks audio + pointer lock). |
| Everything else under `Game/` (~250 files) | — | **skip** | ATCK gameplay and art. |
| `ATCK.Game.asmdef` | 14 | rename | → `Pesky.Game` |

### 1.6 Browser bridge

| File | ~Lines | Action |
|---|---|---|
| `ATCK Unity/Assets/Plugins/WebGL/AHNet.jslib` | 109 | copy verbatim — keep both the `AHNet_*` export names and `window.AH_Net` |
| `ATCK Unity/Assets/Plugins/WebGL/ATCKPlatform.jslib` | ~60 | adapt (optional) — the bridge to `keys.js` fullscreen / keyboard-lock |
| `ATCK Unity/Assets/WebGLTemplates/ATCK/net.js` | 936 | adapt — change `APP_ID` and `ROOM_PREFIX` only (`:44`, `:45`); optionally delete the voice block (`:519`–`:719`) |
| `ATCK Unity/Assets/WebGLTemplates/ATCK/index.html` | 76 | adapt — retitle; keep the script order and `window.AH_UnityInstance` (`:69`) |
| `ATCK Unity/Assets/WebGLTemplates/ATCK/trystero.min.js` | minified | copy verbatim (trystero **0.25.3**, nostr strategy) |
| `ATCK Unity/Assets/WebGLTemplates/ATCK/keys.js` | 220 | adapt (optional) — browser-shortcut swallowing + Keyboard Lock |

### 1.7 Editor, tests, project infrastructure

| File | ~Lines | Action |
|---|---|---|
| `ATCK Unity/Assets/Scripts/Editor/BuildTools.cs` | 179 | adapt — the whole WebGL settings block (`:121`–`:154`) is the reason WebGL works at all |
| `ATCK Unity/Assets/Scripts/Editor/PhysicsLayerSetup.cs` | 106 | adapt — creates layers + writes the collision matrix, idempotent, menu item |
| `ATCK Unity/Assets/Scripts/Editor/ATCK.Editor.asmdef` | 14 | rename |
| `ATCK Unity/Assets/link.xml` | 65 | adapt — mandatory for runtime-created components under IL2CPP stripping |
| `ATCK Unity/Assets/Tests/EditMode/InMemoryTransport.cs` | 95 | copy |
| `ATCK Unity/Assets/Tests/EditMode/InMemoryMesh.cs` | 99 | copy |
| `ATCK Unity/Assets/Tests/EditMode/SessionTests.cs` | ~250 | adapt — the multi-peer harness (`:17`–`:79`) is the single most valuable test file |
| `ATCK Unity/Assets/Tests/EditMode/OutboxTests.cs`, `RoomClockTests.cs`, `ProtocolTestUtil.cs`, `ProtocolTests.cs` | ~300 | copy/adapt |
| `ATCK Unity/Assets/Tests/EditMode/ATCK.Tests.asmdef` | 14 | rename |
| `ATCK Unity/Assets/PlayTest/Editor/Harness.cs` (+ `ATCK.PlayTest.asmdef`) | ~600 | adapt (optional) — editor input-driving harness |
| `.gitignore` | 34 | adapt |
| `.github/workflows/build-and-publish.yml` | 81 | adapt |
| `publish.ps1` | 31 | adapt |

**Total minimal set: 73 files** (56 code files + 6 asmdefs + 6 browser files + link.xml
+ 4 repo-infra files), of which 28 are copy-or-rename and 45 need real adaptation.

---

## 2. Assembly definition graph to recreate

Read from `ATCK Unity/Assets/Scripts/*/ATCK.*.asmdef` and
`ATCK Unity/Assets/{Tests/EditMode,PlayTest/Editor}/*.asmdef`.

```
Pesky.Data        refs: (none)
Pesky.Protocol    refs: (none)
Pesky.Transport   refs: (none)
Pesky.Sim         refs: Pesky.Data, Pesky.Protocol
Pesky.Session     refs: Pesky.Sim, Pesky.Protocol, Pesky.Transport, Pesky.Data
Pesky.Game        refs: Pesky.Session, Pesky.Sim, Pesky.Protocol, Pesky.Data,
                        Unity.InputSystem, Unity.TextMeshPro, UnityEngine.UI,
                        Unity.RenderPipelines.Universal.Runtime
                        [+ Unity.AI.Navigation for NavMesh goblins]
Pesky.Editor      refs: Data, Protocol, Transport, Sim, Session, Game
                        includePlatforms: ["Editor"]
Pesky.Tests       refs: Data, Protocol, Transport, Sim, Session, Game,
                        UnityEngine.TestRunner, UnityEditor.TestRunner
                        includePlatforms: ["Editor"]
                        overrideReferences: true
                        precompiledReferences: ["nunit.framework.dll"]
                        autoReferenced: false
                        defineConstraints: ["UNITY_INCLUDE_TESTS"]
Pesky.PlayTest    refs: Data, Protocol, Sim, Session, Game, Unity.InputSystem
                        includePlatforms: ["Editor"]
```

Every runtime asmdef: `includePlatforms: []`, `excludePlatforms: []`,
`allowUnsafeCode: false`, `autoReferenced: true`, `noEngineReferences: false`,
`defineConstraints: []`, `versionDefines: []`, and `rootNamespace` equal to the name.

Three rules that matter and are easy to get wrong:

- **`Game` must NOT reference `Transport`** (`ATCK Unity/Assets/Scripts/Game/ATCK.Game.asmdef:4`).
  That is deliberate: Game cannot name `INetTransport` or `IVoiceControl`, which is why
  `Session/NetSessionExtensions.cs` exists as the Game-facing edge
  (`ATCK Unity/Assets/Scripts/Session/NetSessionExtensions.cs:8`–`:13`). Keep the wall.
- **`Transport` has no platform constraint** even though `WebRtcTransport` is WebGL-only.
  The platform split is `#if UNITY_WEBGL && !UNITY_EDITOR` *inside* the file
  (`ATCK Unity/Assets/Scripts/Transport/WebRtcTransport.cs:51`, `:106`), not in the asmdef.
- **`Protocol`, `Transport` and `Data` reference nothing**, including each other.
  `Protocol` does use `UnityEngine.Vector3`/`Mathf` (`NetWriter.cs:4`), so
  `noEngineReferences` must stay `false`.

Defines: none anywhere except `Pesky.Tests`'s `UNITY_INCLUDE_TESTS`.

---

## 3. Browser bridge wiring

### 3.1 The three hops

```
C#  WebRtcTransport  --DllImport("__Internal") AHNet_*(string)-->  AHNet.jslib
AHNet.jslib          --window.AH_Net.<fn>(string)------------->   net.js
net.js               --trystero room.makeAction('st').send(Uint8Array)--> peers
net.js               --window.AH_UnityInstance.SendMessage(
                        "NetBridge", "<Method>", "<oneString>")-->  WebRtcTransport
```

### 3.2 Exact names — do not rename any of these

GameObject: **`NetBridge`** (`Transport/WebRtcTransport.cs:24`, asserted at `:79`;
used by `net.js:340`). A wrong name is a silent no-op, not an error.

Global: **`window.AH_Net`** (`net.js:922`), consumed by `AHNet.jslib:8` etc.
Unity instance handle: **`window.AH_UnityInstance`**, assigned by the template
(`WebGLTemplates/ATCK/index.html:69`).

| Direction | Name | Signature |
|---|---|---|
| C# → JS | `AHNet_Start` | `(string roomCode, bool isHost)` |
| C# → JS | `AHNet_Leave` | `()` |
| C# → JS | `AHNet_Broadcast` | `(string payloadB64)` |
| C# → JS | `AHNet_SendTo` | `(string peerId, string payloadB64)` |
| C# → JS | `AHNet_GetSelfId` | `() → string` (mallocs, `AHNet.jslib:38`) |
| C# → JS | `AHNet_GetPeerIds` | `() → JSON array string` (`AHNet.jslib:51`; declared in jslib but **not** imported by `WebRtcTransport` — the C# list is kept from join/leave events instead) |
| C# → JS | `AHNet_UnityReady` | `()` — called from `Awake` (`WebRtcTransport.cs:85`) |
| C# → JS | `AHNet_CopyClipboard` | `(string)` — used by `Game/UI/Clipboard.cs:16` |
| C# → JS | `AHNet_VoiceSetMode` / `AHNet_VoiceSetPeerVolume` / `AHNet_VoiceSetThreshold` | `(int)` / `(string,float)` / `(float)` |
| JS → C# | `OnNetReady(selfId)` | `WebRtcTransport.cs:170` |
| JS → C# | `OnNetError(message)` | `:178` |
| JS → C# | `OnPeerJoined(peerId)` | `:184` |
| JS → C# | `OnPeerLeft(peerId)` | `:191` |
| JS → C# | `OnNetMessage("<peerId>\|<payloadB64>")` | `:198` |
| JS → C# | `OnVoiceActivity("1"\|"0")` | `:220` |
| JS → C# | `OnVoiceError(message)` | `:225` |

### 3.3 Payload encoding, both directions

- **C# → JS**: `Convert.ToBase64String(payload)` (`WebRtcTransport.cs:129`, `:137`),
  marshalled as a UTF-8 string pointer, `UTF8ToString` in the jslib,
  `atob` + per-char loop in `net.js:376`. No `HEAPU8` pointers anywhere — deliberate
  (`AHNet.jslib:4`).
- **JS → C#**: `net.js` packs `peerId + '|' + btoa(bytes)` into a **single** string
  because `SendMessage` takes one argument (`net.js:827`), and C# splits on the first
  `'|'` (`WebRtcTransport.cs:203`–`:210`). Peer ids contain no `|`.
- Cost: base64 is +33% on the wire between C# and JS only; the actual WebRTC payload is
  raw `Uint8Array`.

### 3.4 trystero configuration (`net.js:762`–`:854`)

| Setting | Value | Line |
|---|---|---|
| Library | trystero **0.25.3**, **nostr** strategy, vendored (no CDN) | `WebGLTemplates/ATCK/trystero.min.js:1` |
| `appId` | `'atck-7r2m9wq4'` — **change this for Pesky Weapons**; rooms collide across apps otherwise | `net.js:44` |
| Room name | `ROOM_PREFIX + code` = `'ATCK-' + 'ABC234'` | `net.js:45`, `:778` |
| Action name | `'st'` — one action for everything; the payload's first byte discriminates. Trystero caps action names at 12 bytes | `net.js:48` |
| `relayConfig.redundancy` | `4` | `net.js:49`, `:779` |
| `handshakeTimeoutMs` | `30000` (library default 10 s is too short on shared radio) | `net.js:793` |
| `rtcPolyfill` | `WatchedPeerConnection` — wraps `RTCPeerConnection` to collect ICE diagnostics | `net.js:154`, `:779` |
| Relay URLs | **not configured** — trystero's built-in nostr relay list is used | — |
| ICE | **STUN only, no TURN**: `stun:stun.l.google.com:19302`, `stun:stun1.l.google.com:19302`, `stun:stun.cloudflare.com:3478` | `net.js:55`–`:58` |

**Gotcha:** `index.html:36` defines `window.ATCK_ICE_SERVERS` with a TURN comment, but
`net.js`'s `iceServers()` returns the hard-coded `ICE_SERVERS` constant and never reads
the window global (`net.js:59`). The template hook is dead. If you want TURN in Pesky
Weapons, wire `iceServers()` to read `window.<GAME>_ICE_SERVERS` with the constant as
fallback — a two-line fix, and the single highest-value connectivity change you can make
(symmetric NAT and phone hotspots have no direct path at all today; `net.js:109`).

### 3.5 Room codes and host determination

- Alphabet `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (no I, L, O, 0, 1 — read aloud over
  Discord), length **6** (`Game/UI/RoomCodes.cs:10`–`:11`).
- `Generate()` uses `UnityEngine.Random` (`:13`); `Normalise()` trims + uppercases
  (`:22`); `IsValid()` requires exactly six in-alphabet chars (`:27`).
- Both transports uppercase the code again on `Start` (`WebRtcTransport.cs:103`,
  `LoopbackTransport.cs:43`), and `net.js` a third time (`:771`).
- **Host determination is purely local intent**: whoever presses HOST calls
  `NetSession.Start(..., isHost: true)` (`Game/Boot/Bootstrap.cs:279`). There is no
  election. Clients learn who the host is from **the sender of the first `SESSION_INFO`**
  (`Session/JoinFlow.cs:81`) and pin it thereafter (`:82` rejects a second claimant).
  The old "HOST-" name prefix is gone; `PEER_SLOTS` carries an `isHost` flag instead
  (`docs/ARCHITECTURE.md:284`).
- Two people hosting the same code = two disjoint sessions in one trystero room. Nothing
  detects it.

### 3.6 Liveness, timeouts, diagnostics

All wall-clock, all in JS, because a hidden browser tab gets no `requestAnimationFrame`
and Unity's entire main loop freezes while `setInterval` keeps firing (`net.js:192`–`:197`).

| Timer | Value | Line |
|---|---|---|
| `PING_MS` — 1-byte `0x03` broadcast | 1000 ms | `net.js:198`–`:210` |
| `PEER_TIMEOUT_MS` — silence before `OnPeerLeft` | 15000 ms | `net.js:279` |
| `LIVENESS_TICK_MS` | 1000 ms | `net.js:280` |
| `RELAY_REFRESH_MS` — host-only stale-socket recycle | 180000 ms | `net.js:477` |
| relay watchdog → `OnNetError` if no relay socket OPEN | 12000 ms | `net.js:442` |
| stats + relay-state log | 1000 ms | `net.js:429`, `:433` |
| mic prime before join, give-up | 20000 ms | `net.js:747` |
| prime stream release | 45000 ms | `net.js:753` |

Any received payload (the PING included) refreshes the sender's timestamp and re-raises
`OnPeerJoined` if it had been forgotten (`net.js:824`, `:290`). trystero's own
`onPeerLeave` is kept as the fast path for a clean tab close (`net.js:837`).

The host-only relay refresh exists because a room idle ~5 minutes stopped being
discoverable: a relay WebSocket can stay `OPEN` while no longer delivering, and trystero
only reconnects on `close`. Closing it forces a reconnect and a fresh announce within
5.3 s (`net.js:455`–`:476`). Signalling sockets never touch an `RTCPeerConnection`, so
this is safe mid-game.

Errors surface as `OnNetError` with a full ICE diagnosis appended (`net.js:100`–`:115`,
`:782`–`:791`) and a floating **COPY NETWORK LOG** button (`net.js:241`). Port both;
they are how you will debug your first two-machine test.

### 3.7 Voice, and how separable it is

Voice is raw WebRTC media on the same peer connections — no game messages
(`net.js:519`–`:719`, ~200 contiguous lines). Unity owns only policy
(`Game/Voice/VoiceDirector.cs`), pushing a per-peer gain through
`IVoiceControl.SetPeerGain`. Modes are the net.js integers 0/1/2
(`Transport/VoiceMode.cs:9`–`:11`).

To cut voice: delete `net.js:519`–`:719`, the three `voiceSet*` entries in
`window.AH_Net` (`net.js:929`–`:931`), the three `AHNet_Voice*` jslib exports
(`AHNet.jslib:86`–`:108`), and the `IVoiceControl` implementation in
`WebRtcTransport.cs:141`–`:165` + `:220`–`:229`. `LoopbackTransport`'s no-ops can stay.
Total removal ≈ 260 lines and nothing else breaks.

**Do not delete `primeLocalAddresses` (`net.js:736`–`:760`) with it.** It looks like a
voice feature but is a *connectivity* feature: without microphone permission a browser
only offers its default route's address behind an mDNS `.local` name, which school and
office networks cannot resolve, and a laptop on a hotspot never offers its hotspot
address at all. The join asks for the mic first, waits up to 20 s, and joins either way.
Keep it even in a voiceless build.

### 3.8 WebGL template files

Folder `Assets/WebGLTemplates/<Name>/`, selected as `PlayerSettings.WebGL.template =
"PROJECT:<Name>"` (`Editor/BuildTools.cs:26`, `:150`).

Required: `index.html`, `net.js`, `trystero.min.js`.
Optional: `keys.js` (browser-shortcut swallowing + Keyboard Lock; paired with
`Plugins/WebGL/ATCKPlatform.jslib`).

Script order in `index.html` is load-bearing (`:41`–`:46`): `keys.js`, then
`trystero.min.js`, then `net.js`, then the Unity loader. `keys.js` and `net.js` are
injected with a `?v=Date.now()` cache-buster so they can be edited on the server without
a Unity rebuild; `trystero.min.js` and the jslib cannot (a jslib change needs a **full
rebuild**, not a script recompile — `AHNet.jslib:3`).

The two-sided ready handshake: `net.js` buffers every inbound event until *both*
`window.AH_UnityInstance` is set (`index.html:69`) **and** `NetBridge` has called
`AHNet_UnityReady()` (`WebRtcTransport.cs:85`), whichever is last
(`net.js:328`–`:370`, `:909`). Without it the first packets are dropped and look exactly
like network faults.

---

## 4. Join handshake

### 4.1 Ordered sequence

```
A (host)                                        B (joiner)
──────────────────────────────────────────────────────────────────────────────
transport Ready(selfIdA)
  Slots.SetLocal; HostId = selfIdA
  Slots.Assign(selfIdA, name, isHost:true) → slot 0
  Sim.Apply(PEER_SLOTS); MarkReady()                      JoinFlow.cs:30-40
                                                transport Ready(selfIdB)
                                                  Slots.SetLocal only          :32
  PeerJoined(B)                                 PeerJoined(A)
  → HELLO to B (once per peer)                  → HELLO to A (once per peer)   :69-74
  → Outbox.Enqueue(B, SESSION_INFO)                                            :46
                                          ───►  SESSION_INFO
                                                  HostId = A (first sender wins) :81
                                                  version check                 :83
                                                  Sim.Apply(info)
                                                  Clock.CoarseCheck(info.header.tick) :89
                                                  → Outbox.Enqueue(A, JOIN_REQUEST)   :94-98
  JOIN_REQUEST  ◄───
    version check                                                              :132
    slot = Slots.Assign(peerId, name, false, nowMs, appearance)                :136
    BroadcastSlots()  → PEER_SLOTS to everyone (and Sim.Apply locally)         :160-166
    SendSnapshot(B)   → N × SNAPSHOT parts, then the End part                  :168-172
                                          ───►  PEER_SLOTS
                                                  Slots.Apply → SlotAdded/SlotRemoved  :101-108
                                                  ClockPinger.Arm(Clock.Tick)          :107
                                          ───►  SNAPSHOT part ×N, End
                                                  SnapshotReceiver.Receive              :113
                                                  ApplyTo(Sim); Pending.Clear()        :114-119
                                                  IsSynced = true; MarkReady()         :120-123
                                                CLOCK_PING ×4 two ticks apart, then 10 s
                                          ◄───  CLOCK_PONG (raw, ReplyNow)
```

`PEER_SLOTS` and the snapshot parts are queued into the **same** per-peer Outbox lane
ahead of any new event, and the channel is ordered, so the client builds its world at
tick T and every later event lands after it (`docs/ARCHITECTURE.md:284`).

### 4.2 Version refusal

One constant: `Wire.ProtocolVersion` (`Protocol/Wire.cs:14`, currently **16**). Carried
on `SESSION_INFO` (`Messages/SessionMessages.cs:22`) and `JOIN_REQUEST` (`:154`).
Checked on both sides — client at `JoinFlow.cs:83`, host at `JoinFlow.cs:132`.

**Gotcha:** a mismatch only calls `RaiseError(...)` locally and returns. The host never
tells the joiner, and the joiner never sends `JOIN_REQUEST`. From the player's side the
join simply hangs with a local "NET ERROR" line. Pesky Weapons should add a
`JOIN_REFUSED` reply (id in the free `0x1A`–`0x1F` block is reserved for host migration —
use another) carrying the reason and the host's version.

### 4.3 Slots, max players, late join, host leave

- **Max players = `Wire.MaxPlayers` = 8** (`Protocol/Wire.cs:25`). Slot 0 is always the
  host (`PeerSlots.cs:20`, `:114`). Room full → `Assign` returns `Wire.NoSlot` (0xFF) and
  the host logs "room full, refused" (`JoinFlow.cs:139`).
- **Slot assignment**: an existing peer keeps its slot; otherwise its *quarantined* old
  slot if it has one; otherwise the lowest free slot not in quarantine
  (`PeerSlots.FindFree`, `:121`–`:136`).
- **Quarantine = 120,000 ms** (`PeerSlots.cs:21`). A released slot is held for two
  minutes so a reconnecting peer gets its own slot back and nobody else takes it.
  This matters for Pesky Weapons: it is how a player who reloads the tab keeps their
  weapon's HP and runes.
- **Late join is the same path as first join** — `PeerJoined` → `SESSION_INFO` →
  `JOIN_REQUEST` → slot + `PEER_SLOTS` + fresh snapshot. There is no separate code path.
- **Resync is also the same path**: `RESYNC_REQ` (0x19) → `OnResyncRequest` →
  `SendSnapshot` (`JoinFlow.cs:148`).
- **Host leave**: a client seeing `PeerLeft(hostId)` synthesises a local `SESSION_END`
  with reason `HostLeft` at the current tick, applies it, clears pending events and
  raises `HostLost` (`JoinFlow.cs:57`–`:67`). **No host migration** —
  `MsgId` 0x1A–0x1F are reserved for it (`Protocol/MsgId.cs:26`) and nothing implements it.
  The host's own `Leave()` emits `SESSION_END(HostLeft)` first (`NetSession.cs:250`).

---

## 5. NetSession internals

### 5.1 Per-frame order (`Session/NetSession.cs:134`–`:151`)

1. `while (Inbox.TryPop(out var item)) _router.Handle(item);` — drain the whole queue.
   Lifecycle events (Ready/Error/PeerJoined/PeerLeft) are queued alongside messages so
   nothing is handled out of arrival order (`Session/Inbox.cs:9`–`:12`).
2. Client only: if `HostId != null` and `Pinger.Due(Clock.Tick)`, send one `CLOCK_PING`
   **raw** via `Transport.SendTo` (bypasses the Outbox so queue delay does not skew RTT).
3. `AdvanceHost()` or `AdvanceClient()`.
4. If `Clock.Tick != _lastFlushTick`, `Outbox.Flush(Transport)` — **one FRAME per peer
   per tick**, and only on a tick boundary.

Views read the sim in their own `Update`. `Bootstrap` carries
`[DefaultExecutionOrder(-1000)]` (`Game/Boot/Bootstrap.cs:23`) and calls
`Session.Update()` from its own `Update` (`:159`), so the sim is settled before any view
runs. Reproduce that execution order or you will render one frame stale.

`AdvanceHost` (`:266`): target = `Clock.Tick`; if more than `ResyncBehindTicks` (5 s =
100 ticks) behind, jump the clock forward to `Sim.Tick + 1` and `ResnapshotAll()`;
otherwise step at most `MaxCatchUpTicks` = **20** ticks, calling `Sim.AdvanceTo(t)` then
`Authority.Tick(Sim, t, Sink)` for each.

`AdvanceClient` (`:285`): returns unless `IsSynced && Clock.HasEstimate`; more than 5 s
behind → `RequestResync()` (throttled to one per `ResyncRetryMs` = 5,000 ms, `:304`);
otherwise per tick, first drain `Pending` for every event due at or before `t`, then
`Sim.AdvanceTo(t)`. Clients never run rules.

### 5.2 Send vs SendNow and local loopback

`Send(payload)` (`:154`) switches on `MessageInfo.KindOf(payload[0])`:

- `Stream` → `MessageApplier.Apply(Sim, payload)` **locally first**, then
  `Outbox.EnqueueAll(Transport.PeerIds, payload)`.
- `Intent` → on the host, straight into `Authority.Handle(LocalSlot, payload, Sim,
  Sim.Tick, Sink)` (no wire hop at all); on a client, `Outbox.Enqueue(HostId, payload)`.
- anything else → `RaiseError("Send refused ...: Game only sends streams and intents")`.

`SendNow(payload)` (`:181`) is **only** for `MsgId.Transform`; anything else falls through
to `Send`. It decodes its own payload, calls `_router.AcceptPose(LocalSlot, ...)` so the
local sim row updates through the identical sequence check, then `Transport.Broadcast`.
This is what keeps the 15 Hz pose cadence independent of the 20 Hz frame cadence.

That local loopback is why the host's own position counts for blast radius and its own
intents get validated by the same validator that judges everyone else
(`docs/ARCHITECTURE.md:122`).

### 5.3 The three interface signatures

```csharp
// Session/IHostRule.cs:11
public interface IHostRule
{
    void Tick(WorldSim sim, uint tick, EventSink events);
}

// Session/IIntentValidator.cs:11
public interface IIntentValidator
{
    void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events);
}

// Session/EventSink.cs — the only way a rule or validator produces an outcome
public const int LeadTicks = 3;                       // :20
public WorldSim Sim { get; }                          // :27
public HostIds Ids { get; }                           // :28
public event Action<byte, byte[]> Applied;            // :31  (msgId, payload)
public event Action<byte, byte[]> LocalReply;         // :34
public event Action<byte[]>       Emitted;            // :37  tests record here
public uint  Tick      => Sim.Tick;                   // :51
public uint  EventTick => Sim.Tick + LeadTicks;       // :54
public long  NowMs     { get; }                       // :57
public EventHeader Header() => new EventHeader(EventTick);      // :59
public bool Emit(byte[] payload);                     // :69
public void Reply(byte slot, byte[] payload);         // :86
public void ReplyNow(byte slot, byte[] payload);      // :100
```

A rule reads the sim and emits; it **never** mutates the sim directly
(`IHostRule.cs:7`–`:9`). One rule per domain, one validator per intent family.

### 5.4 How HostAuthority registers rules and validators

`Session/HostAuthority.cs`:

```csharp
public void AddRule(IHostRule rule);                              // :23  ordered list
public void SetValidator(byte msgId, IIntentValidator validator); // :28  dictionary by id
public bool HasValidator(byte msgId);                             // :34
public void Tick(WorldSim sim, uint tick, EventSink events);      // :37  every rule, in order
public bool Handle(byte fromSlot, byte[] payload, WorldSim sim,
                   uint tick, EventSink events);                  // :43  false = unhandled
```

Rules run in **registration order**, once per sim tick, and the order is meaningful:
ATCK comments every ordering dependency inline (`:63`–`:99`). A class may be both — for
example `ClockRule : IHostRule, IIntentValidator` (`Rules/ClockRule.cs:12`), registered
as a rule at `:88` and as the `CLOCK_PING` validator at `:89`.

`CreateDefault()` (`:58`) is the single place a game's whole host-side behaviour is
declared. For Pesky Weapons it becomes roughly:

```csharp
authority.AddRule(new EnemySpawnRule());     // waves / room triggers
authority.AddRule(new EnemyAiRule());        // NavMesh agents, emits ENEMY_STATE (5.6)
authority.AddRule(new BossRule());
authority.AddRule(new DoorRule());           // plates + levers + keys → DOOR_STATE
authority.AddRule(new HealStationRule());
authority.AddRule(new RespawnRule());
var clock = new ClockRule(); authority.AddRule(clock);
authority.SetValidator(MsgId.ClockPing,     clock);
authority.SetValidator(MsgId.HitClaim,      new HitValidator());
authority.SetValidator(MsgId.PickupReq,     new PickupValidator());
authority.SetValidator(MsgId.InteractReq,   new InteractValidator());  // levers, doors, stations
```

`NetSession.Start` calls `HostAuthority.CreateDefault()` only when
`Authority` is still null (`NetSession.cs:116`), so a test can inject its own set by
assigning `Authority` before `Start`.

### 5.5 Tick stamping and the pending queue

- The host stamps every event `Sim.Tick + 3` (`EventSink.cs:54`), i.e. 150 ms ahead, so
  every peer applies it on the same tick (`docs/ARCHITECTURE.md:157`).
- `EventSink.Emit` (`:69`) applies the payload to the **host's own** sim immediately via
  `MessageApplier.Apply`, then raises `Emitted` / `Applied`, then
  `Outbox.EnqueueAll`. **The host therefore sees an event's effect ~3 ticks before every
  client does.** That is by design for ATCK (whose views read positions from a clock-driven
  function), but for physics-driven Pesky Weapons it is worth knowing: a door the host
  opens is open 150 ms earlier on the host's screen.
- `Emit` returns `false` when the sim rejected the payload **and** the kind is neither
  `Event` nor `State` — an Event or State row is still broadcast even if the local sim
  did not keep it (`:74`–`:78`). This is what lets a cosmetic-only event travel.
- Clients: `SessionRouter.OnEvent` (`:83`) requires `FromHost(peerId) && IsSynced`, reads
  the tick with `MessageApplier.TryEventTick` (a header peek, no full decode,
  `MessageApplier.cs:80`), applies at once if `tick <= Sim.Tick`, else
  `Pending.Push(tick, payload)`.
- `PendingEvents` is a plain FIFO (`PendingEvents.cs:18`), justified by "events arrive in
  send order from one host with non-decreasing ticks" (`:6`–`:8`). Since `Header()`
  always uses `Sim.Tick + 3`, that holds. If a Pesky rule ever stamps an event with an
  earlier tick than one already emitted this tick, ordering breaks silently.
- `Pending.Clear()` on snapshot apply (`JoinFlow.cs:119`) and on host loss (`:64`);
  `DropUpTo(tick)` exists for discarding events a snapshot already covers
  (`PendingEvents.cs:44`).

### 5.6 Periodic State rows — the template for host-simulated enemy poses

ATCK's two examples:

- **`TIME_SYNC`**, the simplest: `ClockRule` holds `_nextSyncTick`, and on each tick
  `if (tick < _nextSyncTick) return; _nextSyncTick = tick + SyncIntervalTicks;` then
  `events.Emit(...)` (`Rules/ClockRule.cs:14`, `:18`–`:25`). `SyncIntervalTicks =
  2 * Tick.PerSecond` = 40 ticks = 0.5 Hz.
- **`TOWER_STATE`**, the real template — periodic *plus* immediate on change:
  `TowerRule.StateIntervalTicks = Tick.PerSecond / 2` = 10 ticks = **2 Hz**
  (`Rules/TowerRule.cs:34`); `_nextStateTick` is reset when the phase enters Playing
  (`:88`); a `dirty` flag is raised when any turret changes target (`:94`–`:100`);
  `EmitState` builds **one batched message for all rows** capped at `Wire.MaxBatch`
  (`:199`–`:223`) and emits it.

Notably, `HEALTH_STATE` is **not** periodic at all — it is emitted from `Damage.cs:285`
the moment hp changes, one row at a time.

**Proposal for Pesky Weapons goblins and bosses.** They are NavMesh-driven and therefore
*not* a pure function of any event, so they must be `MsgKind.State` rows, not Events:

```
0x40 ENEMY_STATE   State, host → all, 5 Hz (every 4 ticks) + immediately on any
                   spawn / death / stagger / phase change

  type   u8
  count  u8                             (Wire.BatchCount, ≤ 255)
  rows × count:
    enemyId    u16                      HostIds.NextEnemy()
    kindId     u8                       index into GameData enemy table
    pos        i16 × 3  centimetres     ±327.67 m at 1 cm    (6 B)
    yaw        i16      1/182 deg       full circle          (2 B)
    hp         u16                                            (2 B)
    flags      u8       alive|staggered|attacking|aggroed     (1 B)
    animState  u8                                             (1 B)
                                                    = 15 B per row
```

A 12-goblin room costs `1 + 1 + 12×15 = 182` bytes at 5 Hz = **910 B/s per client**.
One FRAME (16,000 B) holds ~1,060 rows, and `Wire.MaxBatch` caps at 255, so split into
several `ENEMY_STATE` messages if you ever exceed that (the Outbox already spills to the
next flush safely, `Outbox.cs:87`).

Scheduling code, lifted from `TowerRule`:

```csharp
public sealed class EnemyAiRule : IHostRule
{
    public const int StateIntervalTicks = Protocol.Tick.PerSecond / 4;  // 4 ticks, 5 Hz
    uint _nextStateTick; bool _playing;
    readonly List<EnemyStateMsg.Row> _rows = new List<EnemyStateMsg.Row>(64);

    public void Tick(WorldSim sim, uint tick, EventSink events)
    {
        if (sim.Phase != SessionPhase.Playing) { _playing = false; return; }
        if (!_playing) { _playing = true; _nextStateTick = tick; }
        var dirty = StepAgents(sim, tick, events);      // NavMesh + attacks + Damage.*
        if (!dirty && tick < _nextStateTick) return;
        _nextStateTick = tick + StateIntervalTicks;
        EmitState(sim, events);
    }
}
```

Two consequences you must handle, because ATCK has no precedent for either:

1. **The NavMesh agents live on the host only**, driven from `IHostRule.Tick` at 20 Hz.
   Clients hold `Sim.Enemies` rows and a view that interpolates between the last two
   `ENEMY_STATE` samples with the same 100 ms delay as remote players (reuse
   `RemoteCharacter.Interpolate`, `Game/Player/RemoteCharacter.cs:196`). Do **not** run
   `NavMeshAgent` on clients.
2. **Enemies must ride the snapshot.** Add `SnapshotPartKind.Enemies` and a
   `WorldSim.WritePart/ReadPart` case, and put it in `SnapshotCodec.Order`
   (`Session/SnapshotCodec.cs:18`) — otherwise a late joiner sees an empty dungeon until
   the next `ENEMY_STATE`, and a `Removed`-style one-shot row would be lost entirely
   (the exact bug ATCK documents for tower sales, `docs/PLAYTEST-L.md:253`).

### 5.7 Resync rules, gathered

| Rule | Value | Where |
|---|---|---|
| Catch-up cap per frame | 20 ticks (1 s) | `NetSession.cs:21` |
| "Too far behind" threshold | `5 * Tick.PerSecond` = 100 ticks = 5 s | `NetSession.cs:22` |
| `RESYNC_REQ` retry throttle | 5,000 ms | `NetSession.cs:23`, `:306` |
| Host behind → jump clock + `ResnapshotAll()` | — | `NetSession.cs:270`–`:275` |
| Client behind → `RESYNC_REQ` to host, skip the frame | — | `NetSession.cs:290`–`:293` |
| Host answers `RESYNC_REQ` with a whole fresh snapshot | — | `JoinFlow.cs:148` |
| Clock coarse tolerance before re-seeding from `TIME_SYNC` | 1,000 ms | `RoomClock.cs:19`, `:112` |
| Clock estimate | median of last 8 RTT-corrected samples | `RoomClock.cs:17`, `:95`–`:106` |
| Clock ping schedule | 4 pings 2 ticks apart, then every 10 s | `ClockPinger.cs:12`–`:14` |

---

## 6. Player pose streaming, and the tumbling-Rigidbody replacement

### 6.1 What ATCK sends today

**`0x01 TRANSFORM`** — 19 bytes, raw (never inside a FRAME), broadcast via `SendNow`:

```
type u8 | posX f32 | posY f32 | posZ f32 | yaw f32 | seq u16
  0        1..4      5..8       9..12      13..16    17..18
```
`Protocol/M0Messages.cs:19`, `:22`–`:34`. No slot byte — the receiver resolves the slot
from the transport peer id (`Session/SessionRouter.cs:180`).

**`0x20 PLAYER_STATE`** — `MsgKind.Stream`, 7 bytes, through the FRAME:

```
type u8 | slot u8 | pitch i16 (hundredths of a degree) | flags u8 | activeWeapon u8 | emote u8
```
`Protocol/Messages/PlayerMessages.cs:25`–`:26`.

**Rates and epsilons** (`Game/Player/PoseStreamer.cs:18`–`:23`):
`SendHz = 15`, `StateHz = 5`, `KeepaliveSeconds = 1`, `MoveEpsilon = 0.01` m,
`YawEpsilon = 0.5°`, `PitchEpsilon = 1°`. A `TRANSFORM` goes out only when the position
or yaw moved past its epsilon, or the 1 s keepalive is due (`:48`–`:51`).
`Invalidate()` (`:39`) forces the next frame to send — used after a teleport.

**Sequence handling**: `_seq` is a `static ushort` that wraps by design
(`PoseStreamer.cs:26`, `:84`). Per-slot latest-wins in the router, with a wrap-safe
comparison `SeqIsNewer(a,b) => (short)(a - b) > 0`
(`SessionRouter._poseSeq/_poseSeen`, `:20`–`:21`, `:187`–`:194`;
`M0Messages.cs:68`). The first pose for a slot is always taken, and
`_poseSeen[slot]` is reset on `SlotRemoved` (`SessionRouter.cs:26`).

**Remote spawn → interpolate → despawn**:

1. `PeerSlots.Apply(PEER_SLOTS)` diffs the table and raises `SlotAdded` / `SlotRemoved`
   (`Session/PeerSlots.cs:164`–`:202`).
2. `RemotePlayerSpawner.OnSlotAdded` creates one `RemoteCharacter` per remote slot
   (`Game/Player/RemotePlayerSpawner.cs:60`), skipping the local slot.
3. Each frame the spawner feeds the view **from the sim row, not from the wire**:
   if `p.pos`/`p.yaw` changed since last frame it calls `view.OnPose(p.pos, p.yaw)`
   (`:109`–`:115`), then pushes pitch, flags, weapon, alive and look (`:116`–`:121`).
   Only the emote byte is taken straight off the stream, because the sim does not keep
   it (`:76`–`:80`).
4. `RemoteCharacter.OnPose` appends `{ time = Time.unscaledTime, pos, yaw }` to a ring
   and trims past `SampleKeepSeconds = 2f` (`Game/Player/RemoteCharacter.cs:135`–`:140`).
5. `Interpolate()` renders at `Time.unscaledTime - InterpDelay` (`InterpDelay = 0.10f`,
   `:28`, `:198`): clamp-hold before the oldest and after the newest sample, otherwise
   `Vector3.Lerp` + `Mathf.LerpAngle` between the bracketing pair, and derive `Velocity`
   and `_yawRate` from the same pair (`:210`–`:221`).
6. `OnSlotRemoved` → `Remove(slot)` → `Destroy(view.gameObject)` (`:69`, `:83`–`:91`).

### 6.2 Proposal: a free-tumbling Rigidbody pose

Keep id **`0x01`** and the raw `SendNow` path. That is the minimal-diff choice: the
router (`SessionRouter.cs:67`, `:178`), `NetSession.SendNow` (`:184`) and the per-slot
sequence table already special-case `0x01`, and it stays outside the FRAME so its cadence
is independent of the tick. A new `MsgKind.Stream` id would work too, but then the
payload **must** put `slot:u8` immediately after the type byte — `SessionRouter.OnStream`
silently drops any stream whose `payload[1]` is not the sender's slot
(`SessionRouter.cs:173`).

```
0x01 POSE — 19 bytes, raw broadcast, 20 Hz

  offset  size  field
  0       1     type = 0x01
  1       6     position   i16 × 3, centimetres   ±327.67 m @ 1 cm
  7       4     rotation   smallest-three quaternion, 32 bits
  11      6     velocity   i16 × 3, 1/256 m/s     ±127.99 m/s @ 3.9 mm/s
  17      2     seq        u16, wrap-safe
                                                      total 19 B
```

Exactly the same size as ATCK's `TRANSFORM`, with a full orientation and a velocity
instead of a yaw.

**Smallest-three packing** (4 bytes, ~0.1° error, ample for a tumbling weapon):

```csharp
// NetWriter
public NetWriter Quat(Quaternion q)
{
    if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // canonical hemisphere
    float ax=Mathf.Abs(q.x), ay=Mathf.Abs(q.y), az=Mathf.Abs(q.z), aw=Mathf.Abs(q.w);
    int largest = 0; float m = ax;
    if (ay > m) { largest = 1; m = ay; }
    if (az > m) { largest = 2; m = az; }
    if (aw > m) { largest = 3; m = aw; }
    float a, b, c;
    switch (largest)
    {
        case 0:  a=q.y; b=q.z; c=q.w; break;
        case 1:  a=q.x; b=q.z; c=q.w; break;
        case 2:  a=q.x; b=q.y; c=q.w; break;
        default: a=q.x; b=q.y; c=q.z; break;
    }
    if (q[largest] < 0f) { a = -a; b = -b; c = -c; }   // force the dropped term positive
    const float S = 0.70710678f;                       // 1/sqrt(2): bound on the other three
    uint packed = (uint)largest << 30
                | (uint)Pack10(a) << 20
                | (uint)Pack10(b) << 10
                | (uint)Pack10(c);
    return U32(packed);
}
static int Pack10(float v) => Mathf.Clamp(Mathf.RoundToInt((v / 0.70710678f) * 0.5f + 0.5f) * 1023 / 1, 0, 1023);
// (write Pack10 as: Mathf.Clamp(Mathf.RoundToInt(((v / S) * 0.5f + 0.5f) * 1023f), 0, 1023))
```

The reader mirrors it and reconstructs the dropped component as
`sqrt(max(0, 1 - a² - b² - c²))`, then `Normalize()`.

**Rate and epsilons.** A launched, tumbling body turns far faster than a walking figure,
so raise the pose rate and keep the state rate:

| Constant | ATCK | Pesky Weapons | Why |
|---|---|---|---|
| `SendHz` | 15 | **20** | matches the 20 Hz tick; tumbling rotation aliases badly below this |
| `StateHz` (flags) | 5 | 5 | unchanged |
| `KeepaliveSeconds` | 1 | 1 | unchanged |
| `MoveEpsilon` | 0.01 m | 0.01 m | unchanged |
| `YawEpsilon` 0.5° | — | **`RotEpsilon` 1.0°**, via `Quaternion.Angle` | replaces the yaw test |
| — | — | **`VelEpsilon` 0.25 m/s** | a body that has stopped moving but is still spinning must still send |

Bandwidth: 20 Hz × 19 B = **380 B/s per player**, broadcast. In a 4-player mesh each peer
uploads 3 × 380 = 1.1 kB/s and downloads the same; at 8 players, 2.7 kB/s each way.
Negligible next to ATCK's existing traffic, and well inside one FRAME's worth of headroom
— which does not even apply, since this path bypasses the FRAME.

**Remote view.** Reuse `RemoteCharacter`'s sample ring and 100 ms delay verbatim; change
three things in `Interpolate()` (`Game/Player/RemoteCharacter.cs:196`–`:222`):

1. `Mathf.LerpAngle(a.yaw, b.yaw, k)` → `Quaternion.Slerp(a.rot, b.rot, k)`.
2. `Vector3.Lerp(a.pos, b.pos, k)` → **cubic Hermite** using the streamed velocities,
   which is the whole point of putting velocity on the wire — a body in free flight
   between two samples 50 ms apart follows a curve, not a chord:
   ```
   h00 = 2k³-3k²+1 ; h10 = k³-2k²+k ; h01 = -2k³+3k² ; h11 = k³-k²
   pos = h00*a.pos + h10*span*a.vel + h01*b.pos + h11*span*b.vel
   ```
3. `Hold(newest)` when `renderTime >= newest.time` (packet loss / stall) →
   **extrapolate** `newest.pos + newest.vel * dt` and integrate the rotation by the
   last known angular delta, capped at ~250 ms, then freeze. A tumbling weapon that
   snaps to a hold looks broken in a way a standing figure does not.

The remote weapon body itself should be a **kinematic** Rigidbody moved with
`MovePosition`/`MoveRotation` (interpolation on), never a simulated one — exactly the
reasoning ATCK gives for aircraft at `docs/ARCHITECTURE.md:292`: a collider moved without
a Rigidbody becomes a static collider and PhysX rebuilds its tree every frame.

Sim-side seam: `WorldSim.SetPlayerPose(byte slot, Vector3 pos, float yaw)`
(`Sim/WorldSim.cs:158`) becomes
`SetPlayerPose(byte slot, Vector3 pos, Quaternion rot, Vector3 vel)`, and
`SessionRouter.AcceptPose` (`:187`) and `NetSession.SendNow` (`:189`) follow.

---

## 7. Recipes

### 7.1 Adding a new message — every file touched

1. **`Protocol/MsgId.cs`** — one `public const byte`, in the right high-nibble domain.
   Keep the "unused" comments accurate.
2. **`Protocol/Messages/<Domain>Messages.cs`** — the struct:
   ```csharp
   public struct DoorStateMsg
   {
       public const byte Id = MsgId.DoorState;
       public const MsgKind Kind = MsgKind.State;
       public EventHeader header;          // Events only; State/Stream/Intent/Reply have none
       // ... fields ...
       public byte[] Encode() { var w = new NetWriter(Id, capacity); header.Write(w); /*...*/ return w.ToArray(); }
       public static bool TryDecode(byte[] payload, out DoorStateMsg msg)
       {
           msg = default;
           if (!Wire.HasId(payload, Id)) return false;
           var r = new NetReader(payload);        // starts at offset 1, skipping the type byte
           msg.header = EventHeader.Read(r);
           // ... reads in the same order ...
           return !r.Failed;                      // one bounds check for the whole decode
       }
   }
   ```
   Batched lists use `Wire.BatchCount(rows)` (throws past 255) and a `u8` count prefix.
   A `Stream` message **must** carry `slot:u8` as its first field (`SessionRouter.cs:173`).
3. **`Protocol/MessageInfo.cs`** — one line in `BuildTable()`:
   `t[DoorStateMsg.Id] = DoorStateMsg.Kind;` (`:21`–`:96`). The table is built from each
   struct's own `Id`/`Kind`, so the two can never disagree.
4. **`Protocol/Enums.cs`** — any new enum the message carries.
5. **`Sim/WorldSim.cs`** (+ the owning table) — `public void Apply(in DoorStateMsg msg)`,
   idempotent for repeats.
6. **`Session/MessageApplier.cs`** — one `case MsgId.DoorState:` in the switch
   (`:19`–`:72`). Only Events, States and the streams the sim keeps go here.
7. **Producer** — a new `IHostRule` in `Session/Rules/` or `IIntentValidator` in
   `Session/Validators/`, registered in `HostAuthority.CreateDefault()` (`:58`).
   Intents also need `authority.SetValidator(MsgId.X, validator)`.
8. **Consumer** — Game subscribes `NetSession.EventApplied` / `StreamReceived` /
   `ReplyReceived` (`NetSession.cs:73`–`:77`), or simply reads the sim row each frame
   (preferred; see `RemotePlayerSpawner.Update`).
9. **Snapshot** — if a late joiner must see it, add the field to the owning table's
   `WritePart`/`ReadPart`, and a new `SnapshotPartKind` + an entry in
   `SnapshotCodec.Order` (`:18`) if it is a new table.
10. **Test** — `Tests/EditMode/<Domain>MessageTests.cs`, an encode→decode round-trip
    following `CombatMessageTests.cs` / `ProtocolTestUtil.cs`. Also assert the encoded
    size if the budget matters.
11. **`Protocol/Wire.cs:14`** — bump `ProtocolVersion` (see 7.2).
12. **`docs/`** — add the row to the message table.

### 7.2 Bumping the protocol version

One constant: `Wire.ProtocolVersion` (`Protocol/Wire.cs:14`). Its own doc comment states
the rule: *"Bump when any layout in Messages/ changes, or when an existing field gains a
value older peers would simulate differently."*

That second clause is the subtle one and ATCK has a worked example: version 9 was bumped
for **no layout change at all** — `AC_EVADE` gained manoeuvre id 7, so a version-8 peer
would have flown a steered plane somewhere else (`docs/ARCHITECTURE.md:368`). Conversely,
a scope that adds no id, no field, no enum value and no snapshot byte owes no bump, and
the changelog says so explicitly (`docs/PLAYTEST-L.md:140`, `:353`).

Mechanically: change the one number; `JOIN_REQUEST` and `SESSION_INFO` carry it and both
sides refuse a mismatch (`JoinFlow.cs:83`, `:132`). Record the bump and its single reason
in the dated section of your architecture doc, as ATCK does for every one of its sixteen.

---

## 8. Testing

### 8.1 What exists

| Path | Kind | What it gives you |
|---|---|---|
| `Session/TransportFactory.cs:15`–`:19` | runtime | WebGL player → `WebRtcTransport`; **everything else → `LoopbackTransport`** |
| `Transport/LoopbackTransport.cs` | runtime | Editor play mode and Play Offline. `Start` raises `Ready("local")` synchronously, `PeerIds` is always empty, sends are dropped. Session's own local loopback (streams updating the local row, intents reaching `HostAuthority`) still works, so a solo host is fully playable (`:6`–`:12`). |
| `Tests/EditMode/InMemoryTransport.cs` + `InMemoryMesh.cs` | EditMode | N in-process peers on one global FIFO; `Pump()` delivers everything queued including what deliveries queue in turn; `Synchronous` mode delivers inside each send. |
| `Tests/EditMode/SessionTests.cs` | EditMode | **The harness to copy.** Each peer is `new NetSession(() => Ms)` with an injectable millisecond source (`:26`, and `NetSession.cs:80`). `Step()` advances every peer 50 ms then runs **three** update rounds at the same fake time so a `CLOCK_PING` sent in round 1 is answered in round 2 and read in round 3, all at zero fake latency (`:59`–`:74`). Tests cover: solo host ready + traffic; client joins mid-session and ends with the host's state hash; a third peer joining later matches; host loss ends the session (`:9`–`:13`). |
| `Tests/EditMode/OutboxTests.cs`, `RoomClockTests.cs`, `ProtocolTests.cs` | EditMode | FRAME size limit, lane ordering, clock median/coarse-check |
| `PlayTest/Editor/Harness.cs` | Editor | Queues **real** `Keyboard`/`Mouse` state events from `InputSystem.onBeforeUpdate` on Dynamic updates, steps one scripted `IEnumerator` phase per frame, appends to a log file; `Run("phase1")`, `Stop()`, and a `SessionState` kill flag (`:17`–`:58`). Editor-only, single instance, no networking. |

### 8.2 Can two non-WebGL instances connect? **No.**

- `TransportFactory.ForPlatform()` returns `LoopbackTransport` on every non-WebGL target
  (`:15`–`:19`), and `WebRtcTransport.Connect` outside WebGL logs
  *"WebRtcTransport only works in a WebGL player"* and immediately raises
  `OnNetError` (`Transport/WebRtcTransport.cs:111`–`:112`).
- `InMemoryMesh` is in-process only and lives in `ATCK.Tests`, which is
  `includePlatforms: ["Editor"]` with `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — it
  cannot even be referenced from a player build.
- So today the **only** way two real peers meet is two browsers. Every two-machine test
  needs a WebGL build served over HTTP(S).

### 8.3 Smallest way to add a desktop path

`INetTransport` is 8 members and its contract explicitly permits a star topology:
*"a star transport (dedicated server) satisfies that by relaying inside the transport, so
Session never knows the topology"* (`Transport/INetTransport.cs:9`–`:12`). So:

1. **`Transport/TcpTransport.cs`, ~200 lines.** `Start(roomCode, isHost)`: host opens a
   `TcpListener` on a fixed port; a client `TcpClient`s to `roomCode` parsed as
   `host:port`. Length-prefixed (`u32` big or little, pick one) frames on each socket.
   Peer ids: the host is `"host"`, clients get `"p1"`, `"p2"`, … assigned on accept and
   announced. The host relays every `Broadcast` to all other sockets so the mesh contract
   holds. Raise `Ready`, `PeerJoined`, `PeerLeft`, `Message` from the socket threads —
   **that is already safe**, because `Inbox.Push` takes a lock and `NetSession.Update`
   drains on the main thread (`Session/Inbox.cs:16`, `:45`; the same threading contract
   WebGL needs, `INetTransport.cs:11`).
2. **`Session/TransportFactory.cs`** — one branch:
   ```csharp
   public static INetTransport ForPlatform()
   {
   #if UNITY_WEBGL && !UNITY_EDITOR
       return WebRtcTransport.Create();
   #elif PESKY_LAN
       return new TcpTransport();
   #else
       return new LoopbackTransport();
   #endif
   }
   ```
   behind a scripting define so shipping WebGL builds are unaffected.
3. **Nothing else changes.** Not `NetSession`, not the protocol, not the join flow.
   `IVoiceControl` can be a no-op on it, exactly as `LoopbackTransport` does.

This buys you two editor instances (or two desktop players) exercising the *real* join
handshake, snapshot, clock and host authority with real latency — which is currently
impossible and is the single biggest hole in ATCK's test story.

---

## 9. Fresh-project setup checklist

**Unity project**
- [ ] Unity **6000.3.23f1**, URP, target WebGL. (CI pins the same version:
      `.github/workflows/build-and-publish.yml:56`.)
- [ ] `ProjectSettings/TimeManager.asset`: **Fixed Timestep 0.05**, Maximum Allowed
      Timestep 0.1 (`ATCK Unity/ProjectSettings/TimeManager.asset:6`–`:7`). This is local
      physics only — the 20 Hz sim tick does **not** depend on `FixedUpdate`
      (`docs/ARCHITECTURE.md:155`). For a physics-driven game you may want 0.02; if you
      change it, nothing in the netcode breaks, but say so explicitly in your docs
      because the coincidence with `Tick.Ms = 50` is otherwise misleading.
- [ ] **Run In Background = ON**. Without it the tab pauses when unfocused and every
      connection dies the moment a player alt-tabs (`Editor/BuildTools.cs:132`–`:134`;
      `ProjectSettings.asset:89`).
- [ ] Input System package, `activeInputHandler: 1` (new input system only,
      `ProjectSettings.asset:942`). Generate a C# wrapper class from the actions asset
      (ATCK's is `ATCKControls`, `Game/Boot/Bootstrap.cs:139`) and bind the `UI/*` actions
      onto `InputSystemUIInputModule` **by hand** — assigning only `actionsAsset` leaves
      every action reference null and no button ever sees the pointer
      (`Bootstrap.cs:216`–`:228`).
- [ ] `apiCompatibilityLevel: 6` (.NET Standard 2.1), scripting backend IL2CPP for WebGL
      (`ProjectSettings.asset:846`–`:853`, `:940`).
- [ ] Color space **Gamma** (`BuildTools.cs:142`, `ProjectSettings.asset:50`).

**WebGL player settings — apply them from code, not by hand** (`Editor/BuildTools.cs:121`–`:154`)
- [ ] `WebGL.compressionFormat = Brotli` + `decompressionFallback = true` (GitHub Pages
      cannot set `Content-Encoding`)
- [ ] `runInBackground = true`
- [ ] Graphics APIs = `{ OpenGLES3 }` only, `SetUseDefaultGraphicsAPIs(WebGL, false)`;
      `WebGL.threadsSupport = false` (no COOP/COEP on Pages ⇒ no SharedArrayBuffer)
- [ ] `WebGL.exceptionSupport = FullWithStacktrace`
- [ ] `stripEngineCode = true`, `ManagedStrippingLevel.Low`,
      `Il2CppCodeGeneration.OptimizeSize`
- [ ] `WebGL.dataCaching = true`
- [ ] `WebGL.template = "PROJECT:<YourTemplate>"`
- [ ] memory: initial 32 MB, max 2048 MB, geometric growth
      (`ProjectSettings.asset:814`, `:830`–`:835`)
- [ ] Build output to the repo root + write `.nojekyll` (`BuildTools.cs:167`–`:173`)
- [ ] Keep a size budget check (ATCK warns past 40 MB, `BuildTools.cs:31`, `:110`)
- [ ] Menu items: *Apply Player Settings*, *Build Web*, with a build cooldown
      (`BuildTools.cs:43`–`:64`) — automation replays menu items.

**Scenes**
- [ ] `Boot.unity` (a `Bootstrap` object, `DontDestroyOnLoad`) and one gameplay scene
      loaded **additively**, both registered in build settings
      (`BuildTools.cs:27`–`:29`, `:156`; `Game/Boot/WorldLifecycle.cs:72`–`:86`).
- [ ] `Bootstrap` carries `[DefaultExecutionOrder(-1000)]` and a
      `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` self-create guard
      (`Bootstrap.cs:23`, `:115`–`:121`).

**Physics layers** (recreate `Editor/PhysicsLayerSetup.cs` + `Game/Boot/Layers.cs`)
- [ ] ATCK's set: `Ground, Player, Aircraft, Projectile, Tower, Debris, Ragdoll`
      (`Game/Boot/Layers.cs:22`). Pesky Weapons' equivalent:
      `Ground, Weapon(player), Enemy, Projectile, Interactable, Debris, Trigger`.
- [ ] Matrix, written with `Physics.IgnoreLayerCollision` and saved into
      `DynamicsManager.asset` (`PhysicsLayerSetup.cs:65`–`:88`): Debris collides with
      Ground only; Projectile with Enemy, Weapon, Interactable and Ground only.
      **Weapon × Weapon should collide** in a co-op tumbler — that is a deliberate
      departure from ATCK's Aircraft × Aircraft = off.
- [ ] Layers are created in the first free user slots (index ≥ 8) and the whole thing is
      idempotent behind a menu item (`PhysicsLayerSetup.cs:18`, `:21`, `:30`).
- [ ] Add the **NavMesh** packages (`com.unity.ai.navigation`) and bake surfaces per
      dungeon room — host-only, but the asset must exist in every build.

**Stripping**
- [ ] `Assets/link.xml` preserving every component created at runtime
      (`ATCK Unity/Assets/link.xml`). ATCK's list is the right starting point; for Pesky
      Weapons keep the Physics block verbatim (Rigidbody, all colliders, joints,
      PhysicsMaterial — `link.xml:11`–`:23`) and add `UnityEngine.AIModule`
      (`NavMeshAgent`, `NavMeshObstacle`) if you create them from code. Symptom of
      getting this wrong: *"Can't add component because class 'X' doesn't exist!"* in the
      browser console and an invisible/collider-less world.

**Browser**
- [ ] `Assets/WebGLTemplates/<Name>/{index.html, net.js, trystero.min.js}` (+ optional
      `keys.js`), `Assets/Plugins/WebGL/AHNet.jslib`.
- [ ] Change `APP_ID` and `ROOM_PREFIX` in `net.js:44`–`:45`.
- [ ] Keep the `NetBridge` GameObject name and every `AHNet_*` / `On*` name.

**Git / CI**
- [ ] `.gitignore` covering `Library/ Temp/ Obj/ Logs/ UserSettings/ Build/ *.csproj
      *.sln .vs/ .vscode/` plus the build artefacts at the repo root
      (`ATCK/.gitignore`).
- [ ] **ATCK has no `.gitattributes` and no Git LFS.** For Pesky Weapons, add one:
      `*.fbx *.png *.wav *.mp3 *.psd filter=lfs diff=lfs merge=lfs -text` and
      `* text=auto eol=lf`. ATCK got away without it because almost all of its art is
      generated from code; a dungeon crawler will not.
- [ ] CI: `game-ci/unity-builder@v4`, `buildMethod: <NS>.Editor.BuildTools.BuildFromCommandLine`,
      `Library` cached, path-filtered to `Assets/ Packages/ ProjectSettings/`,
      `concurrency: cancel-in-progress`, skipped gracefully when secrets are absent
      (`.github/workflows/build-and-publish.yml:21`–`:59`). A free-runner WebGL build is
      30–60 minutes cold.
- [ ] A `publish.ps1` equivalent for local publishing to the Pages repo
      (`ATCK/publish.ps1`).
- [ ] Batch-mode entry point exits 0/1 so CI fails properly
      (`BuildTools.cs:67`–`:80`).

---

## 10. Gotchas and lessons

### 10.1 Load-bearing details that look cosmetic

1. **`NetBridge` is a magic string.** Rename the GameObject and every inbound event is
   silently dropped — no exception, no log. ATCK guards it with a `Debug.Assert`
   (`Transport/WebRtcTransport.cs:79`). Keep the assert.
2. **Transport events fire outside the Unity frame.** On WebGL they arrive inside a
   trystero/WebRTC/timer callback. Listeners must only enqueue; never touch scene state
   or the sim from them (`Transport/INetTransport.cs:11`, `WebRtcTransport.cs:16`–`:20`).
   `Inbox` is the enforcement point and is lock-protected (`Session/Inbox.cs:16`).
3. **An exception inside `SendMessage` kills the trystero callback chain silently** —
   `net.js` wraps every `deliver` in try/catch (`net.js:333`–`:345`). Never remove it.
4. **Liveness must live in JavaScript.** A hidden browser tab gets no
   `requestAnimationFrame`, so Unity's whole main loop freezes and any C#-driven
   keepalive stops — every peer would then cull the hidden player as timed out.
   `setInterval` still fires in hidden tabs, throttled to ~1/s, which is exactly the
   1 Hz PING cadence (`net.js:192`–`:210`). This is the single most important design
   decision in the bridge.
5. **A jslib change needs a full rebuild**, not a script recompile (`AHNet.jslib:3`).
   `net.js` and `keys.js` do not — they are cache-busted at load
   (`index.html:41`, `:45`).
6. **Two-sided ready handshake or you lose the first packets.** `net.js` buffers until
   both `window.AH_UnityInstance` is set *and* `AHNet_UnityReady()` has been called
   (`net.js:328`–`:370`). Dropped first packets look exactly like network faults.
7. **Every `MsgKind.Stream` message must carry `slot:u8` as its first field.**
   `SessionRouter.OnStream` drops anything whose `payload[1] != slot` with no log
   (`SessionRouter.cs:173`).
8. **`Outbox.TakeFrame` silently drops a single payload too large for any FRAME**, so
   the lane never jams (`Outbox.cs:99`–`:103`). If you add a big message, add a size test.
9. **A one-shot State row can be missed.** State rows are absolute and overwrite; ATCK's
   `TOWER_STATE` is re-sent at 2 Hz, but a `Removed` row is emitted once — a peer that
   misses that frame learns the tower is gone only from the next snapshot
   (`docs/PLAYTEST-L.md:253`). Emit "removed" as an **Event**, or keep the entity in the
   periodic row with a dead flag.
10. **Version mismatch is a silent hang** for the joiner (section 4.2).

### 10.2 Networking decisions and their stated reasons

- **FRAME ≤ 16,000 bytes.** Trystero's single data channel per peer pair is reliable and
  ordered, but `send()` chunks at ~16 KB and can complete a later small send before an
  earlier large one. Keeping every message inside one chunk makes arrival order equal
  send order (`docs/ARCHITECTURE.md:160`; enforced at `Protocol/Frame.cs:15`, `:46` and
  in `SnapshotPartMsg.MaxBody`, `Messages/SessionMessages.cs:191`).
- **Events stamped now + 3 ticks (150 ms)** so every peer applies on the same tick
  (`docs/ARCHITECTURE.md:157`, `EventSink.cs:20`).
- **Wall-clock `Stopwatch`, never `Time.time`**, so a throttled tab catches up under the
  20-ticks-per-frame cap instead of drifting (`docs/ARCHITECTURE.md:154`;
  `RoomClock.cs:10`–`:12`).
- **Sim never reads a clock**; `AdvanceTo(tick)` takes time as a parameter
  (`docs/ARCHITECTURE.md:156`). This is what makes the EditMode tests possible at all.
- **Seeds ride on events** so cosmetic randomness matches without syncing it
  (`docs/ARCHITECTURE.md:159`).
- **STUN only, no relay, by design** — "the game is peer to peer and never relays through
  a server" (`net.js:50`–`:54`). The documented cost: with symmetric NAT on either side
  (phone hotspot, office or campus network) **no direct path exists** and the join simply
  fails with a diagnosis (`net.js:109`).
- **The reliable/ordered channel is not what M0 wanted.** trystero 0.25 exposes no
  per-action `RTCDataChannel` options, so the channel is ordered+reliable rather than
  `{ordered:false, maxRetransmits:0}`. Sequence numbers still discard out-of-order data.
  *"Do not fork the library"* (`net.js:810`–`:813`).
- **Host-only relay refresh on a 3-minute wall clock** because a room idle ~5 minutes
  stopped being discoverable by new joiners; the fix is a socket recycle, not a
  hand-rolled announce, with the full reasoning at `net.js:455`–`:476`.
- **Microphone permission is a connectivity feature** (section 3.7; `net.js:723`–`:728`).
- **No host migration.** Host leaves ⇒ session ends. `0x1A`–`0x1F` reserved
  (`docs/ARCHITECTURE.md:163`, `Protocol/MsgId.cs:26`).
- **Aircraft-to-aircraft collision runs as sphere tests in Sim on the host, not PhysX** —
  it is a sim fact, so it is testable and free of PhysX
  (`docs/ARCHITECTURE.md:292`). For Pesky Weapons the equivalent judgement is:
  *weapon-versus-goblin damage is a host sim fact; weapon-versus-wall bounce is local PhysX.*
- **Bandwidth budget** stated: 19 bytes at 15 Hz for pose, FRAMEs under 16 KB
  (`net.js:373`–`:375`). Voice: at most seven uplink streams, receiver-side gain, not
  range-limited in the first playable; if bandwidth demands it, `net.js` gains a per-peer
  mute using `removeStream`/`addStream` with hysteresis (`docs/ARCHITECTURE.md:300`).
- **Player-count ceiling 8** (`Protocol/Wire.cs:25`); voice is the practical limit, not
  the protocol.

### 10.3 What is documented as unverified with real peers in a browser

This matters for planning, so it is stated plainly:

- The dated sections of `docs/ARCHITECTURE.md` record **no** later entry confirming that
  WebRTC host/join, voice or remote player views were exercised in a browser with a
  second real peer. The strongest verification claim in the whole document is
  *"Verified on the build … 751/751 EditMode tests … No browser console errors"*
  (`docs/ARCHITECTURE.md:1025`, `:1031`) — that is **one** browser, loading the build and
  playing solo, not two peers connecting.
- Consistent with that, everything two-peer is covered only by `InMemoryMesh` in EditMode
  (`Tests/EditMode/SessionTests.cs`), which has zero latency, zero loss, no chunking, no
  ICE and no base64 boundary.
- The parts that have therefore **never been exercised end to end**: real trystero room
  discovery, NAT traversal, the 15 s peer timeout under real packet loss, the host relay
  refresh, snapshot delivery over a real data channel, voice `addStream` to a late joiner,
  and the `COPY NETWORK LOG` diagnosis path.
- The `net.js` code itself is honest about this: the whole ICE diagnosis block
  (`net.js:60`–`:172`), the "known issue" comments (`net.js:455`, `:810`) and the
  `showLogButton` machinery exist precisely because the author expected these to fail
  first in the field.

**Recommendation.** Before writing any Pesky Weapons gameplay on top of this stack, do
one thing ATCK never did: build the WebGL player, host it, and get two machines on
different networks into one room. Add the TURN hook (section 3.4) and the desktop
`INetTransport` (section 8.3) first — they are ~220 lines together and they turn "we
think it connects" into something you can regression-test.

### 10.4 Ten lessons for reusing this stack

1. Copy the **layering rule** before the code: Data → Protocol → Transport → Sim →
   Session → Game, with Game unable to name a transport. It is the reason the whole thing
   is testable.
2. Copy `INetTransport` **exactly**. It is 8 members and it already admits a dedicated
   server, a LAN socket and an in-memory mesh without Session ever knowing.
3. Keep `Sim` clock-free and Unity-free enough to run in EditMode. Everything else
   follows from that.
4. Keep the **one-way rule** for host output: rules and validators only ever speak
   through `EventSink`. No rule mutates the sim.
5. Keep the FRAME size ceiling and add a test for it the day you add your first batched
   message.
6. Put liveness, pings and reconnection in JavaScript, not C#. Hidden tabs.
7. Treat anything the host simulates non-deterministically as a **periodic State row with
   a dirty flag** (the `TowerRule` pattern), and make sure it rides the snapshot.
8. Owner-authoritative pose, host-authoritative everything-that-matters. Health, death,
   respawn and ownership are host facts; views take alive/dead from the sim, never from
   the stream (`docs/ARCHITECTURE.md:134`).
9. Render remotes ~100 ms in the past and interpolate. For a tumbling body, slerp the
   rotation and Hermite the position with the streamed velocity.
10. Build the **two-peer test path** on day one, not after the gameplay. ATCK's single
    biggest gap is that it cannot connect two instances without a browser, and everything
    it has not verified traces back to that.
