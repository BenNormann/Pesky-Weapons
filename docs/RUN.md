# RUN

The simplified run (owner decisions of 2026-09-29, `docs/PREMISE.md` last section): five
rooms in a row, one timer, one exit, one hidden Mage with five curses. What it is made of,
what travels on the wire, what the host decides, every number, and how to grow it.

**Implemented, untested** (round 8, 2026-09-29). Checks made: a clean compile after every
script change, every new message encoded and decoded once in the Editor, the Run snapshot
part written and read back, and the edit-mode scene validator on `Boot`, `MainMenu`,
`Tutorial`, `Run` and `Dev/FeelBox` (0 problems each). **Nothing was run: no play mode, no
test, no socket, no build.** Round 9 (same day, also implemented, untested): the Mage's
**ability bar** (section 6.5) replaced the `N` / `C` rings by the crosshair, and the tutorial's
exit ring was shrunk and moved to the far corner of the practice hall (section 7). **Round 12**
(2026-10-01, implemented, untested): **VOTING and the PAUSE** (`docs/VOTING.md`): the exit rule counts
only non-banished weapons, the ability bar is everybody's (a VOTE slot first), and the run timer runs on
GAME time (pauses excluded).

The labyrinth grid, the Tab map / scratch pad, the compass and the Resurrection Room are
**set aside, not deleted**: the code, `Labyrinth.unity` and `docs/LABYRINTH.md` stay in the
project. Section 9 says how to bring them back.

---

## 1. The idea in one paragraph

Everybody spawns as a soul in the **start room** (the rack). It has two doors: **south** is the
run's first door, **north** is a sealed placeholder ("RESERVED", never opens). The host draws
**five distinct rooms** from the scene's pool and tells everybody the order. Each room has
exactly two doorways, **entry (north)** and **exit (south)**; the exit door is **locked until
the room is done** - a blank pool room's placeholder seal is an **impact lever beside the
exit door** - and doors stay two-way. The first player to leave the start room starts a
**5:00 timer** everybody sees. After room 5 comes the **Exit room**; the weapons win when
**every non-Mage player is inside it at once** (the Mage may be anywhere). The Mage wins the
instant the timer runs out. Roles are secret until the result banner.

---

## 2. Data model

### 2.1 `RunState` - `Assets/Scripts/Sim/RunState.cs`

The run half of `WorldSim`, reached as `sim.Run`. Plain C#, public knowledge, rides the
snapshot (`SnapshotPartKind.Run = 7`; `End` moved to 8).

| | |
|---|---|
| `Count`, `RoomAt(k)`, `IndexOf(roomId)` | the sequence of room ids; `Count` is 0 until RUN_LAYOUT |
| `Started`, `StartTick`, `DeadlineTick` | the timer, in room-clock sim ticks |
| `TicksLeft(tick)` | 0 when not started or run out |
| `Rev` | bumps on every change, so a view can notice cheaply. **Not** `WorldSim.Rev` |
| `ToLayout()`, `Write`, `Read` | the wire and the snapshot |

`MaxRooms` is 32 (RUN_LAYOUT's count is a u8; the part stays tiny). A new layout resets the
timer. `SESSION_INFO` reseeding resets it too.

The **outcome** of the round still lives in `LabyrinthState.Outcome` / `MageMask` (filled by
ROUND_RESULT, which the run reuses). Nothing secret is in the sim: who the Mage is stays in
the host's `LabyrinthRule`; a curse is applied only by its victim.

### 2.2 `RunDef` - `Assets/Scripts/Data/RunDef.cs`, assets `Assets/Data/Run.asset` and `Run_Tutorial.asset`

A `ScriptableObject` referenced from `GameData.run`. `GameData.mode` (`GameMode.Labyrinth |
Run`, new) says which round the host runs and the HUD expects; both `GameData.asset` and
`GameData_Tutorial.asset` are `Run`. `Run_Tutorial.asset` differs in one number
(`curseCooldown` 6 s, so the lesson is not a wait). Every tunable is in section 5.

Room ids are still indices into `LabyrinthDef.rooms` (`Labyrinth.asset`), which remains the
list of authored rooms; the nudge tunables and the role counts stay on `LabyrinthDef` too.

### 2.3 Game side

| File | What |
|---|---|
| `Game/RunDirector.cs` | `_Managers/RunDirector`: the start room, the Exit room, its volume box, and the **pool**. `ResolveTwin(door)` from the sequence, `IsExit(door)`, `PlaceAt(world)` (`RunPlace`: Start / Rooms / Exit / Unknown), `CollectRunPool`, `CollectRunPlaces` |
| `Game/LabyrinthDirector.cs` | gained an `[OptionalRef] run` field. When set, `ResolveTwin`, `IsExit`, `DestinationLabel` / `Glyph` delegate to the RunDirector. The hundred grid doorways, `MagicDoor`'s grid mode, `ExitZone` and the room culling keep working unchanged |
| `Game/MageCurse.cs` | `_Managers/MageCurse`: keys 1-5, the aim, the local pre-checks, CURSE_REQ, the refusal line, the cooldown on the ability bar (and the pulse of the slot just cast) |
| `Game/CurseEffects.cs` | `_Managers/CurseEffects`: the curse ON THIS PLAYER (`CurseState` = kind + expiry on `LevelClock`), one effect per curse, the launch hook (`ILaunchCurse` on `WeaponMotor.Curse`) |
| `Game/MageAim.cs` | the shared aim: the body nearest the centre of the screen inside a cone (nudge and curse) |
| `Game/UI/RunHud.cs`, `Assets/UI/Run.uxml` + `Run.uss` | `_UI/RunHud` (`UIDocument`, sortingOrder 1): the timer, the role reveal, the result banner, the victim's curse line, the blindness sheet, the Mage's **ability bar** (round 9, section 6.5), the quiet note |
| `Game/UI/AbilityBar.cs` | round 9: `AbilitySlotDef` (one slot's data), `AbilityCooldownSource` (None / Curse / Nudge) and the plain-C# `AbilityBar` view RunHud builds from its `abilities` list |
| `Game/WeaponMotor.cs` | `ILaunchCurse Curse`: a speed scale and a bend of the direction, applied in `Evaluate` so the trajectory preview shows the cursed launch |
| `Game/OrbitCamera.cs` | `SetSway(yaw, roll)`: a visual wobble on top of the look; the aim (`Yaw` / `Pitch`) is untouched |
| `Game/PracticeDummy.cs` | `Curse(kind, seconds)`: the label over its head shows the curse and a countdown |
| `Game/ExitZone.cs` | `alwaysLive` (the tutorial's ending ring, which belongs to no doorway) |
| `Game/UI/MenuFlow.cs` | START loads `Run`; the room page line is `"<rooms> rooms, <m:ss>"` from `GameData.run` |
| `Scripts/Editor/RunSceneBuilder.cs` | the Editor automation that built the variant, the Run scene and the tutorial hall (`Pesky > Run > ...`), plus the deterministic scene-id pass and a NavMesh rebake |

---

## 3. The wire

`Wire.ProtocolVersion` is **6** (five new ids and a new snapshot part). Layouts in
`Protocol/Messages/RunMessages.cs`; the enums in `Protocol/RunEnums.cs`.

| Id | Name | Kind | Bytes | Layout after the type byte |
|---|---|---|---|---|
| 0x60 | RUN_LAYOUT | State | 2 + 2n | `count u8, roomIds u16 x count` (5 rooms = 12 bytes) |
| 0x61 | RUN_START | Event | 13 | `tick u32, startTick u32, deadlineTick u32` |
| 0x2C | CURSE_REQ | Intent | 3 | `targetSlot u8 (0xFE = the tutorial dummy), curse u8 (CurseKind)` |
| 0x2D | CURSE_EVENT | Event | 9 | `tick u32, targetSlot u8, curse u8, durationTenths u16` - **no author** |
| 0x2E | CURSE_REFUSED | Reply | 5 | `reason u8 (CurseRefusal: 0 NoTarget, 1 OutOfRange, 2 Cooldown, 3 NoSuchCurse), targetSlot u8, waitTenths u16` - to the asker alone |

`CurseKind`: 0 None, 1 Magnetic, 2 Nausea, 3 Slippery, 4 Blindness, 5 Heavy. `RoundOutcome`
gained `TimedOut = 3`. The run domain is 0x60-0x6F (0x62-0x6F free); the curses sit next to
the nudge in the players' domain (0x2F free).

**Snapshot.** `SnapshotPartKind.Run = 7`, `End = 8`, appended to `SnapshotCodec.Order`. Body:
`count u8, roomIds u16 x count, started bool, startTick u32, deadlineTick u32`. A late joiner
gets the sequence and the deadline from it; the seal and door states ride the existing Kit
part (every lever and gate is an ordinary kit piece).

`MessageApplier.Apply` has cases for RUN_LAYOUT, RUN_START and CURSE_EVENT (the last is an
empty `WorldSim.Apply`, exactly like NUDGE_EVENT, so a client's router passes it on to Game).

---

## 4. The rules, and who decides

Everything below is the host. `RunRule` (`Session/Rules/RunRule.cs`) is an `IHostRule` and
the `IIntentValidator` for CURSE_REQ, registered right after `LabyrinthRule` in
`HostAuthority.CreateDefault()`. It runs only when `GameData.mode` is `Run`.

**Roles are LabyrinthRule's, unchanged.** In run mode `LabyrinthRule` keeps only its secret
Mage table (`AssignRoles` -> ROLE_ASSIGN to each player alone, the same path as the labyrinth;
since round 10 every player is told, Weapon or Mage, every round, and every peer resets its role
on the edge into Playing - docs/BUILD-LOG.md round 10)
and the nudge validator; the grid rebuild, LAB_LAYOUT, the respawns, the swap, the bend and
the two labyrinth endings are skipped (`LabyrinthRule.IsRunMode`). `RunRule` reads that table
on the same host through `IsMage(slot)` / `MageMask(sim)` and never copies it anywhere.

### 4.1 Round open, and the layout

On the first `Playing` tick both rules open. The pool is **the scene**, not data
(`IHostWorld.CollectRunPool` -> `RunDirector.pool`), and the host's scene is still loading on
the first ticks, so `RunRule` asks again on every scan (5 Hz) until it answers, then draws
`roomsPerRun` distinct rooms with a partial Fisher-Yates from `WorldSeed ^ PhaseStartTick *
2654435761 ^ 0x52554E` and emits **RUN_LAYOUT**. Until it lands (a second or so) every run
doorway resolves to nothing. Clients never derive the draw.

### 4.2 The timer

Each scan the host asks `IHostWorld.CollectRunPlaces` (streamed poses against the scene's
rooms: `Start`, `Rooms` = any room of this run, `Exit` = inside the Exit room's `RoomVolume`
box, `Unknown` = no pose or no room). The first scan on which any present player is in
`Rooms` or `Exit` emits **RUN_START** with `deadline = now + timerSeconds` (in ticks). "Left
the start room" is measured by arrival, not by the door event, because the host's door sensor
never sees a remote (kinematic) body cross it. Up to 200 ms late. A banished player (a ghost) cannot
start it. Since round 12 RUN_START's `startTick` / `deadlineTick` are **game ticks** (room ticks with
the pauses taken out, `PauseState.GameTick`), so a vote meeting stops the clock; `RunHud` counts down
from `SessionRunner.GameMs`.

### 4.3 The two endings

Checked every scan once the timer runs, the Mage's win first so it beats a simultaneous
gathering:

- **Time out** - `tick >= DeadlineTick` -> `ROUND_RESULT(TimedOut, escaped 0, mages)` and
  `SESSION_END(CrewLost)`. **Instant Mage win.**
- **Exit** - every present **non-Mage, non-banished** player is `Exit` at once (a non-Mage with no
  pose yet counts as not there; the Mage may be anywhere; a banished weapon is neither needed nor
  counted) -> `ROUND_RESULT(Escaped, escapedMask, mages)` and `SESSION_END(Escaped)`. **The weapons
  win.**
- **Only the Mage left** (round 12, widened in round 16) - no non-Mage, non-banished player is present any
  more, whatever took them (a vote, leaving the room, a lost connection) ->
  `ROUND_RESULT(WeaponsGone, 0, mages)` and `SESSION_END(CrewLost)`. **Instant Mage win.** A round
  that OPENED with one present player (`RunRule._soloRound`, the owner walking the rooms alone) never ends
  this way: dev mode.
- Nothing ends while the game is paused (`docs/VOTING.md`); a banishment's ending fires after the thaw.

ROUND_RESULT is still the one message that ever names the Mages. Since round 15 it opens the END SCREEN
(`RunEndScreen` on `_UI/RunHud`, the `end-root` sheet of `Run.uxml`): VICTORY or GAME OVER for THIS
player's side (the slot's bit in `mageMask`, so a banished player sees its original side's result), the
reason, the fragments' names, and BACK TO THE ROOM. While it is up `PauseGate` freezes this machine the way
a vote pause does (not the shared pause state: SESSION_END resets it on the same tick), the cursor is free,
and `SessionRunner.HoldReturnOnEnd` holds the menu load until the button (`ReturnAfterRun`). A new round the
host starts while a peer still sits on the screen reloads the level for that peer. `RunHud`'s one-line
result banner is filled in but no longer shown. A peer that lands with the sim already Ended (a late resync)
reads the outcome from `LabyrinthState` instead of waiting for the event.

### 4.4 The seals and the doors

Nothing new on the wire. A pool room's `Gate_South` is `Door_Lever.prefab` (`DoorCondition`
LeverOn -> the room's `Seal`, an `ImpactLever.prefab`, latching), and the room's south
`MagicDoor` names it as its `gate`. Any weapon impact at 4 m/s or more flips the lever: the
simulating peer reports it (KIT_REQ on a client, checked by the host within 8 m), the host
publishes `KIT_STATE(Lever)`, `EvaluateDoors` sees the condition met and publishes
`KIT_STATE(Door)`, every peer slides the panel and the doorway becomes passable. The start
room's north gate is `Door_Sealed.prefab` (mode Sealed: never satisfied).

Doors are two-way: only the door being **entered** must be open, so the way back from room
k + 1 into room k works while room k's seal is still shut (the traveller comes out in front
of the closed panel).

### 4.5 The curses (CURSE_REQ)

Built exactly like the nudge. Silent unless the asker is a Mage in an open round (no reply,
no event: a weapon that forges one learns nothing). Then, in order, each failure a
CURSE_REFUSED to the asker alone: a real curse id (1-5); his **shared** cooldown
(`curseCooldown`, one for all five keys, spent only on an accepted curse); a target that is
another present player holding a weapon - not a soul, not himself, with at least one POSE -
or the tutorial dummy via `IHostWorld.TryGetPracticeTarget`; within `curseRange` of the
asker's own streamed pose. Any state of the target but a free soul is fine (in the air, on
the floor, stuck in wood). No line-of-sight check. Accepted: **CURSE_EVENT** to everybody
with the target, the curse and `curseDuration`, and **no author**. Round 12: a banished Mage, or
any Mage while the game is paused, is refused in silence; the shared cooldown is kept in game
ticks, so a pause does not run it down.

**Who applies it.** Only the victim. `WorldAuthority.OnCurseEvent` raises `Cursed(kind,
seconds)` when the target is this peer's slot (or calls `PracticeDummy.Curse` on the host for
the dummy) and does nothing else on every other peer: there is no wisp, no marker, nothing
for a bystander to see. A new curse on the same victim replaces the old one.

| Key | Curse | What the victim gets (`CurseEffects`) |
|---|---|---|
| 1 | MAGNETIC | each launch direction is slerped `magneticBend` of the way toward the nearest other weapon (loose or held by anybody) within `magneticRange` of the body |
| 2 | NAUSEA | the camera rolls `nauseaRoll` and yaws `nauseaYaw` degrees on a sine of period `nauseaPeriod` (`OrbitCamera.SetSway`, visual only), and each launch heading is turned by up to `nauseaHeadingDrift` degrees on a slower sine |
| 3 | SLIPPERY | every non-trigger collider of the possessed weapon wears `slipperyMaterial` (`P_Slick`, zero friction); restored on expiry; follows a swap of weapon |
| 4 | BLINDNESS | `RunHud` paints a sheet of `blindnessOpacity` black with a clear circle of `blindnessClearRadius` x screen height in the middle (Painter2D, odd-even fill) |
| 5 | HEAVY | launch speed x `heavySpeedScale` |

The launch effects reach the weapon through `WeaponMotor.Curse` (`ILaunchCurse`), which
`CurseEffects` re-attaches every frame to whatever the player possesses, so a re-possess
mid-curse keeps the curse. The victim's HUD line says `CURSED: <NAME>` and the seconds left;
nobody sees who cast it, because nothing on any machine knows.

### 4.6 The nudge

Unchanged in code and doubled in data: `nudgeImpulse` and `pullImpulse` are **8** on both
`Labyrinth.asset` and `Labyrinth_Tutorial.asset` (and as the field defaults). `MageNudge`
reads its `LabyrinthDef` from the session's `GameData` first, so it works with no
`LabyrinthDirector` in the scene (the tutorial), and its cooldown is the **Nudge / Pull**
slot of the ability bar on `RunHud` (round 9; it used to be a small **N** ring beside the
centre of the screen), drawn for a Mage alone.

---

## 5. Every tunable

All on `Assets/Data/Run.asset` (`RunDef`) unless said otherwise.

| Field | Default | What |
|---|---|---|
| `roomsPerRun` | **5** | pool rooms between the start room and the Exit room; fewer if the pool is smaller; at most 32 |
| `timerSeconds` | **300** | 5:00, from the first player leaving the start room |
| `warningSeconds` | 30 | the HUD's timer turns red inside this |
| `curseRange` | **15 m** | host check, streamed pose to streamed pose |
| `curseCooldown` | **30 s** (tutorial **6**) | one shared cooldown for all five keys |
| `curseDuration` | **20 s** | each curse |
| `curseAimCone` | 8 deg | client only: half-angle around the screen centre |
| `magneticRange` | 20 m | the nearest other weapon within this pulls the launch |
| `magneticBend` | 0.5 | fraction of the angle the launch turns through |
| `nauseaRoll`, `nauseaYaw` | 6, 4 deg | peak sway |
| `nauseaPeriod` | 3 s | per sway cycle |
| `nauseaHeadingDrift` | 12 deg | peak launch-heading wander |
| `slipperyMaterial` | `P_Slick` | the zero-friction physics material |
| `blindnessClearRadius` | 0.12 | of the screen height |
| `blindnessOpacity` | 0.96 | |
| `heavySpeedScale` | **0.5** | |
| `voteCallsPerPlayer` | **1** | round 12: vote calls per player per run (`docs/VOTING.md`) |
| `voteGroupCooldown` | **45 s** | after any meeting ends, game time, before the next call |
| `voteNoVoteBeforeSeconds` | **30 s** | after the run timer starts; none at all before it starts |
| `voteMeetingSeconds` | **25 s** | the meeting, room time (the game is paused) |
| `voteResultSeconds` | **4 s** | the result on screen, still paused, before PAUSE_END |
| `LabyrinthDef.nudgeImpulse`, `pullImpulse` | **8** (was 4) | the nudge, doubled |
| `LabyrinthDef.mageBaseCount` etc. | unchanged | roles are drawn exactly as before |
| `MenuFlow.runLine` | "5 rooms, 5:00" | only used when `GameData.run` is missing |

`RunHud` also has the reveal timing and every banner string as serialized text.

---

## 6. The scene - `Assets/Scenes/Run.unity`

Build index **3** (`Boot` 0, `MainMenu` 1, `Tutorial` 2, `Run` 3). `Labyrinth.unity` is out
of the build and untouched. Built by `Pesky > Run > 2 Build Run Scene From Labyrinth` from a
copy of the labyrinth scene; validator **0 problems**.

### 6.1 Roots

| Root | What |
|---|---|
| `_Managers` | `SessionRunner`, `LevelClock`, `WorldAuthority` (+ `run`), `LabyrinthDirector` (+ `run`), `PlayerSpawner`, `PoseStreamer`, `RemotePlayerSpawner`, `MageNudge` (`runHud`), **`RunDirector`, `MageCurse`, `CurseEffects`**, round 12: **`PauseGate`, `VoteCaller`**. `CompassModel` is gone |
| `_UI` | `HUD`, **`RunHud`** (sortingOrder 1, `Run.uxml` + `Run.uss`; round 12: the same GameObject carries **`VoteScreen`**), `DebugOverlay`, `Settings` (round 10; its `pauseGate` override points at `_Managers/PauseGate`). `LabyrinthHud` is gone |
| `Environment/Rooms` | the same 25 room instances on the same 200 m lattice |

`WorldAuthority` arrays: `magicDoors` **50**, `rooms` **25**, `doors` **24**, `levers` **23**,
`weapons` 21 (three racks since round 19). Scene ids were reassigned by kind (`RunSceneBuilder.AssignSceneIds`): weapons
101+, home slots 201+, volumes 301+, doors 401+, signs 1101+, spawn points 1201+, magic doors
1301+ (two per room), levers 1801+, exit zones 2401+.

### 6.2 The rooms

| Room | Prefab | What |
|---|---|---|
| `Room_00_WeaponRack` (id 0) | `LabyrinthRoom_Start` instance | the rack and 8 soul spawns as before; east and west doorways removed and sealed; north doorway gated by `Gate_North_Reserved` (`Door_Sealed`) with `Sign_Reserved` beside it; **south = the run's first door** |
| `Room_01_Pool`, `Room_03_Pool` .. `Room_24_Pool` (23) | **`LabyrinthRoom_TwoDoor`** | the pool. Room 1 used to be the Resurrection Room; it is an ordinary pool room now (sign and volume say "Room 1"; `Labyrinth.asset` still calls id 1 the Resurrection Room for the labyrinth's sake) |
| `Room_02_GateHall` (id 2) | `LabyrinthRoom_Exit` instance | the Exit room: east and west sealed, only `ExitZone_South` kept; **north = entry from room 5, south = the lit EXIT** (leads nowhere; the volume decides) |

### 6.3 `Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_TwoDoor.prefab` - the two-door room

A **variant of `LabyrinthRoom.prefab`**, so a fix to the square room still reaches it:

```
LabyrinthRoom_TwoDoor        LabyrinthRoom: doorways [N, -, S, -], sealedSides [-, E, -, W]
  Geometry/Wall_East/Seal    cube 0.5 x 3.5 x 3 at (12.25, 1.75, 0), M_Wall, World, static
  Geometry/Wall_West/Seal    the same at (-12.25, 1.75, 0)
  Doorways/Doorway_North     MagicDoor_Grid, the ENTRY (unchanged)
  Doorways/Doorway_South     MagicDoor_Grid, the EXIT, gate = Gate_South
  Doorways/Gate_South        Door_Lever.prefab at (0, 0, -12), LeverOn -> Seal
  Gameplay/Seal              ImpactLever.prefab at (4, 0, -9), latching
```

Per instance the scene sets `roomId`, the sign / volume name, every `authority` /
`labyrinth`, and the ids. The scene validator accepts the two empty doorway slots because
those sides are declared sealed (`SceneValidator.IsSealedDoorwaySlot`, new).

### 6.4 The UI - `Assets/UI/Run.uxml` + `Run.uss`

| Element | What |
|---|---|
| `timer` | top centre, `m:ss`. Hidden until RUN_LAYOUT; dim ("5:00") until the first player leaves; red (`is-warning`) inside `warningSeconds`; hidden after the result |
| `note` | the quiet refusal line, 1.6 s |
| `curse-status` | `CURSED: <NAME>` and the seconds left, the victim only |
| `ability-bar` | round 9: an empty row, bottom centre, 18 px up; `RunHud` fills it with one slot per `abilities` entry (section 6.5). Round 12: shown to everybody while the round runs; the `mageOnly` slots only for a Mage who is not banished |
| `blind-overlay` | painted by `RunHud.OnDrawBlind` while blind |
| `role-reveal`, `result-banner` | as the labyrinth's, with the run's words; the banner also lists the banished with their revealed roles (round 12). Since round 15 the banner is filled in but not shown: `end-root` replaced it |
| `end-root` | round 15: the end screen (`RunEndScreen`): `end-title` VICTORY / GAME OVER (`is-victory` class on the root turns it amber), `end-reason`, `end-mage`, `end-button` BACK TO THE ROOM |
| `ghost-status` | round 12: the banished player's own line (`BANISHED - YOU WERE ...`) |
| `vote-root` | round 12: the vote screen (`docs/VOTING.md` section 7), read by `VoteView` / `VoteScreen` |

No Tab overlay, no map, no pad, no compass: the `Map` input action is still bound but nothing
listens to it in the run. **Nothing sits by the crosshair**: the round-8 `mage-rings` (the
tiny `N` / `C` rings right of the screen centre) were removed in round 9.

### 6.5 The ability bar (round 9; everybody's since round 12)

An MMO-style row of square slots centred at the bottom of the screen, shown to **everybody** while
the round runs (round 12; it used to be a Mage's alone), hidden with the result banner. The
`mageOnly` slots (the nudge and the curses) are displayed for a Mage who is not banished
(`AbilityBar.SetMageSlots`); a weapon, or a banished Mage, sees the VOTE slot alone. Default slots,
left to right:

| Key | Name | Glyph | Cooldown source | Pulses on |
|---|---|---|---|---|
| V | Call vote | V | `Vote` (the 45 s group cooldown or the "no votes yet" wait as a shade; USED / WAIT / PAUSED / OUT / GONE as a word, `RunHud.SetAbilityLock`; `docs/VOTING.md`) | a call |
| LMB / RMB | Nudge / Pull | N/P | `Nudge` (8 s, `LabyrinthDef.nudgeCooldown`), set apart (`gapBefore`) | a nudge or pull |
| 1 | Magnetic | M | `Curse` (set a little apart, `gapBefore`) | curse 1 cast |
| 2 | Nausea | N | `Curse` | curse 2 cast |
| 3 | Slippery | S | `Curse` | curse 3 cast |
| 4 | Blindness | B | `Curse` | curse 4 cast |
| 5 | Heavy | H | `Curse` | curse 5 cast |

Each slot (`Run.uss` `.ability*`, panel pixels, so it scales with `UiPanelSettings`, 1920 x
1080 reference, match width): a 72 px dark frame with a grey-box tile (a flat colour and the
glyph; no art), the key in amber in the top-left corner (11 px when longer than two
characters), the name under it. While its source's cooldown runs, a dark sheet covers the
tile from the top, as tall as the fraction left, so the tile refills from the bottom, and the
whole seconds left show in amber. The shared curse cooldown darkens all five curse slots at
once. The slot of the power just used **pulses** (`is-pulse`: scale 1.14 and a gold border,
`abilityPulseSeconds` 0.18 s plus a 0.12 s transition each way). Cooldowns are the local
guides the powers already kept (optimistic on the ask, corrected by a refusal); the host still
decides.

**Code.** `Game/UI/AbilityBar.cs`: `AbilitySlotDef` (data), `AbilityCooldownSource` (`None`,
`Curse`, `Nudge`), and `AbilityBar`, a plain C# view that `RunHud.OnEnable` builds into the
`ability-bar` element from `RunHud.abilities`. `RunHud.SetCooldown(source, readyAt, seconds)`
feeds a source (`SetCurseCooldown` / `SetNudgeCooldown` are wrappers, called every frame by
`MageCurse` / `MageNudge` while this player is a Mage); `RunHud.PulseAbility(source, curse)`
pulses (called by `MageCurse.Fire` / `MageNudge.Fire` on an accepted ask).

**How to add an entry** (e.g. a future power, or a second nudge slot):

1. Select `_UI/RunHud` in `Run.unity` (and in `Tutorial.unity`, which has its own RunHud),
   open **Ability bar (Mage only) > Abilities**, press **+**.
2. Fill `id` (short, unique; the slot element is named `ability-<id>`), `keyLabel` (what is
   printed in the corner), `displayName` (under the slot), `iconGlyph` (one to three
   characters) and `iconTint` (the tile colour). Tick `gapBefore` to start a new group; untick
   `mageOnly` for a slot everybody should see (round 12; the vote is the only one so far).
3. Pick `cooldown`: an existing source (`Curse`, `Nudge`) or `None` (never darkens). For a
   `Curse` slot set `curse` to the `CurseKind` that pulses it.
4. Drag it to its place in the list; the bar is laid out in list order. Save the scene.

A power with a **cooldown of its own** needs one line of code beyond the data: add a value to
`AbilityCooldownSource`, have the power's script call `runHud.SetCooldown(thatSource, readyAt,
seconds)` each frame while the player may use it, and `runHud.PulseAbility(thatSource,
CurseKind.None)` when it is used. The tables size themselves from the enum. The field default
(`AbilityBar.DefaultSlots()`) is what a new RunHud gets; the scenes carry their own copy of
the list.

---

## 7. The tutorial

`Tutorial.unity`'s `Room6_MageTutorial` is now one room, `PracticeHall` (the old Entry Hall,
unpacked): the 3x3 practice grid, its `LabyrinthDirector`, `CompassModel` and `LabyrinthHud`
are gone; the four grid doorways are gone and their openings sealed; the ending
`ExitZone_South` (with its `TutorialTrigger`, `endsTutorial`) moved in as `ExitRing`,
`alwaysLive`. `_UI/RunHud` starts asleep and `TutorialGate_MageLesson` wakes it
(`wakeRunHud`). `_Managers` gained `MageCurse` and `CurseEffects`; `MageNudge` points at the
RunHud. `GameData_Tutorial.asset` is mode `Run` with `Run_Tutorial.asset`.

**The hall's walk (round 9).** The hall is 24 x 24 m, centred at world (300, 0, 0); you enter
from room 5 by `MagicDoor_Practice_to_5` in the north-east (local (8, 0, 9)). In local
coordinates, in walking order:

| What | Where (local) | Facing |
|---|---|---|
| `Sign_Mage` | (10.5, 0, 4.5), east wall | west, into the hall |
| `Sign_Run` | (10.5, 0, -1), east wall | west |
| `Sign_Nudge` | (10.5, 0, -6.5), east wall (unchanged) | west |
| `PracticeDummy` | (4, 0, -3) (unchanged) | |
| `Sign_Curse` | (8, 0, -10.5), south wall (unchanged) | north |
| `Sign_Exit` | (-5.5, 0, -10.5), south wall, text `EXIT. STAND IN THE LIT RING TO END THE TUTORIAL.` | north |
| `ExitRing` | **(-8.5, 0, -8.5)**, the far south-west corner, yaw 45 (its arch faces the way in) | north-east |
| `RoomSign` (`ENTRY HALL`) | (3.5, 0, 10.5), north wall by the way in | south |
| `Sign_Vote` (round 12) | (-10.5, 0, 2), west wall, yaw -90 (sign id 2078) | east |
| `DummyRestore` (round 12) | (6, 2, 5), a 10 x 4 x 8 trigger box over the entry door's arrival: a repeatable `TutorialTrigger` that switches `PracticeDummy` back on | |

`ExitRing`: the floor disc `Live/GatherRing` is 4 x 0.02 x 4 (a **2 m radius**; was 12 = 6 m),
`Busy/Held` 2.7, the gold bar `Live/Arch` at 3.2 m (was 4.2). Its `BoxCollider` (shared by
`ExitZone` and the `endsTutorial` `TutorialTrigger`) is **3.6 x 3.5 x 3.6** centred (0, 1.75, 0)
and turns with the ring: flush inside the circle at the sides, corners about 0.5 m past it;
world AABB x 288.95..294.05, z -11.05..-5.95, clear of the walls (inner faces at 288 / -12).
Round 8 had it at (-5, 0, -5) with an 11 x 3.5 x 6 box that reached the middle of the hall and
**contained the hall's respawn point** (0, 1.2, -4). The only other `TutorialTrigger` in the
hall is `TutorialGate_MageLesson` (wakes the HUD, `endsTutorial` off); there is no other
`ExitZone` in the scene. No collider on the World layer moved (the ring's visuals and the signs
have none), so the NavMesh was **not** rebaked. These are Inspector edits in `Tutorial.unity`;
`RunSceneBuilder`'s `3 Rework Tutorial Mage Room` is a one-shot migration of the pre-round-8
scene (it cannot run on today's) and still holds the round-8 numbers.

The lessons, as signs: `Sign_Mage` (you are the fragment in here), `Sign_Run` (five rooms,
the seals, the timer, the exit rule), `Sign_Nudge` (the hopping dummy; its last line now
points at the ability bar), `Sign_Curse` ("1-5 casts a curse on who you look at" and the five
effects), `Sign_Exit` (EXIT, the lit ring ends the tutorial). The dummy still hops (nudge /
pull); a curse on it goes through
the real CURSE path with the reserved target `0xFE`, and its label reads e.g. `BLINDNESS 17`
counting down (`PracticeDummy.Curse`). Rooms 1-5 are unchanged. The NavMesh was rebaked
(`Pesky > Run > Rebake NavMesh`).

Because the tutorial has one player, that player is the Mage, no run layout ever arrives (no
`RunDirector`) and no timer shows; the ring ends it as before.

**The practice vote (round 12).** `Sign_Vote` on the west wall says what V does. Pressing V in the
hall (HUD awake) sends VOTE_CALL_REQ with candidate `VoteTarget.Dummy` (0xFE, the reserved id the
nudge and the curses use): the host - this player, loopback - freezes the game (the dummy stops
hopping, the body holds) and opens the real vote screen with `DUMMY` as the only candidate; voting
it out plays `DUMMY WAS BANISHED - they were A WEAPON`, the dummy's GameObject is switched off, and
4 s later the game thaws. Re-entering the hall through the entry door crosses `DummyRestore`, which
switches it back on. The practice vote spends no call and ignores the cooldown and the timing.
Details: `docs/VOTING.md` section 9.

---

## 8. How to add a real room to the pool

1. **Make a variant of `LabyrinthRoom_TwoDoor.prefab`** (or of the square room, then remove
   its east and west doorways, seal them, and tick `sealedSides` E and W). Build the content
   inside the variant. Keep **entry = the north doorway, exit = the south doorway**, each
   upright with local +Z into the room, and keep `doorways` in N E S W order with E and W
   empty. Grow the `Footprint` to cover the shape.
2. **Give it its own completion condition.** The exit door's gate is any `Door` prefab whose
   `DoorCondition` the south `MagicDoor` names as its `gate`: `Door_Lever` (strike the seal),
   `Door_Plate`, `Door_Key`, `Door_RoomCleared` (goblins), `Door_Scales`, ... Delete the
   placeholder `Gameplay/Seal` and `Gate_South` if the room does its own thing. Nothing in the
   run rule knows or cares what the condition is: a locked exit door is just a closed gate.
3. **Drop the instance into `Environment/Rooms`** of `Run.unity` at a free lattice slot
   (`((id % 5) * 200, 0, -(id / 5) * 200)`) or replace a pool room; set `roomId` to its index
   in `Labyrinth.asset` (append a `LabyrinthRoomDef` there if it is a new id: the list is
   append-only). Wire `authority` on every `MagicDoor`, `Door`, `ImpactLever` and other kit
   piece, `labyrinth` on both `MagicDoor`s.
4. **Register it**: both `MagicDoor`s in `WorldAuthority.magicDoors`, the `RoomVolume` in
   `rooms`, every gate in `doors`, every lever in `levers` (and any other kit in its array);
   the `LabyrinthRoom` in **both** `LabyrinthDirector.rooms` and `RunDirector.pool`.
5. **Ids**: run `Pesky > Run > Assign Scene Ids (open scene)` (it re-numbers everything by
   kind, deterministically) or hand out unused ones.
6. `Pesky > Validate Open Scenes`, save. The host draws it like any other pool room.

To make a run longer or shorter, change `RunDef.roomsPerRun`; the pool must hold at least that
many rooms or the run is as long as the pool.

---

## 9. How to re-enable the labyrinth mode

Everything is still there. To play the grid again:

1. `GameData.asset` -> `mode = Labyrinth` (and `GameData_Tutorial.asset` if the tutorial's
   grid comes back). `LabyrinthRule` then rebuilds the grid, sends LAB_LAYOUT and runs the
   swap, the bend, the respawns and the two labyrinth endings; `RunRule` idles.
2. Put `Labyrinth.unity` back in the build settings and point `MenuFlow.labyrinthScene` (the
   `MainMenu` scene's serialized value, "Run" today) at `"Labyrinth"`.
3. The labyrinth scene still has its `LabyrinthHud`, `CompassModel` and 25 four-door rooms;
   `MageNudge` there still points at the `LabyrinthHud`. Its `WorldAuthority` has no `run`, so
   the run rule has nothing to answer. Note the nudge is 8 m/s now, not 4.
4. The tutorial's practice grid was rebuilt, not hidden: bringing it back means the pre-round-8
   `Tutorial.unity` from git and the `Labyrinth_Tutorial.asset` data (still present).

---

## 10. Riskiest untested assumptions

1. **Nothing has been run.** Not solo, not in two tabs.
2. **Removing prefab-instance children** (the start and Exit rooms' east / west doorways, the
   Exit room's three zones) relies on Unity 6's removed-GameObject overrides; the validator and
   the read-back say the objects are gone, but the instances were never loaded in play mode.
3. **The pool arrives late.** RUN_LAYOUT waits for the host's scene; a client that starts
   flying at once meets a south door that leads nowhere for about a second. If the host's
   scene takes longer, so does the door.
4. **The timer starts by arrival, not by the door.** A player who somehow enters a pool room's
   footprint without the door (there is no such way today) would start it too.
5. **The Exit is the volume, not the doorway.** Standing in the Exit room's 24 x 10 x 24 box
   counts; the lit ring at its south doorway is a landmark only.
6. **Two Mages at once** each get the shared cooldown per Mage; there is no co-sign for
   curses (it never applied).
7. **Slippery restores whatever material each collider had**; a weapon that respawns
   mid-curse re-applies its def material itself, so the restore is a no-op then.
8. **Blindness is a UI Toolkit odd-even fill**: `Painter2D.Fill(FillRule.OddEven)` with the
   rectangle and the circle in one path. If the hole does not render, split the sheet into
   four rectangles around the circle instead.
9. **The MainMenu line** reads `GameData.run`; a data asset without one falls back to
   `runLine`.
10. **The vote and the pause** have their own list: `docs/VOTING.md` section 10.
