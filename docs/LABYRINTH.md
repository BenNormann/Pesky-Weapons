# LABYRINTH

The labyrinth: what it is made of, what travels on the wire, what the host
decides, every number you can turn, and how to add rooms or change the size.

**Implemented, untested** (stages 5 and 6). `Assets/Scenes/Labyrinth.unity`
now exists, holds the `LabyrinthDirector` and 25 authored rooms, and is build
index 3 — see **section 8**. The only checks made were a clean compile, the
project's edit-mode scene validator on `Labyrinth`, `MainMenu`, `Zone1` and
`Dev/FeelBox` (0 problems each), and reading the code back. **Nothing was run:
no play mode, no test, no socket.**

---

## 1. The idea in one paragraph

A **5x5 grid of cells** (the size is data). Each cell holds one **room**. The
grid is **not physical**: the rooms sit far apart in world space and are joined
only by teleport doorways, so the grid is nothing but a host-owned table of
`cell -> room id`. Every room has four doorways, **N E S W**. Opposite edges of
the grid **wrap**: leaving east from the last column arrives in the first column
of the same row. There is exactly **one** doorway in the whole grid that does not
wrap — the outward doorway of the good-end room — and going through it is the
**Exit**. The **start room** (the weapon rack) is the centre cell; the
**Resurrection Room** and the **good end** are in two different corners chosen by
the seed. A doorway resolves its destination **from the table at the moment you
cross it**, so a room the Mage moved while you were in the air changes where you
come out.

---

## 2. The data model

### 2.1 `LabyrinthGrid` — `Assets/Scripts/Sim/LabyrinthGrid.cs`

Plain C#, no Unity, no clock, no randomness beyond the seed. This is the whole
labyrinth as a value.

| | |
|---|---|
| `Width`, `Height`, `CellCount` | the grid |
| `RoomAt(cell)` / `CellOfRoom(roomId)` | the table, both ways |
| `StartCell`, `BadEndCell`, `GoodEndCell` | the three cells that never move |
| `ExitDir` | which doorway of `GoodEndCell` is the Exit |
| `Neighbour(cell, dir)` | where a doorway leads, wrapping; `NoCell` (-1) for the Exit alone |
| `Step(cell, dir)` | the same, ignoring the Exit hole: the **map's** neighbour, which is what a drag uses |
| `Wraps(cell, dir)` | true when that step leaves one edge and arrives at the other |
| `IsExitDoorway(cell, dir)` | the one non-wrapping doorway |
| `AreAdjacent(a, b, allowWrap, out dir)` | orthogonal only, never diagonal |
| `CanSwap(a, b, allowWrap)` | the whole swap rule set bar the cooldown. Leaves the table untouched |
| `ForceSwap(a, b)` | applies a swap the host already decided; checks only the indices |
| `ToExit(from)` / `ToCell(from, target)` | BFS over the door graph, wrapped edges included; returns a `CompassHint` |
| `EveryCellReachesExit()` | the connectivity check a swap must pass |
| `Generate(def, seed)` | the deterministic layout |
| `ToLayout()` / `FromLayout(msg)` / `Write(w)` / `Read(r)` | the wire and the snapshot |
| `DoorOpen` | optional `DoorwayFilter`. **Null today**: all four doorways of every room always work |

**Coordinates.** `cell = row * Width + column`. Column 0 is the west edge,
row 0 is the north edge, so **north is row - 1** and **east is column + 1**.
Directions are `Pesky.Protocol.Heading`: `North 0, East 1, South 2, West 3`, and
`Opposite(d) = (d + 2) & 3`.

**`CompassHint`** is `{ known, spin, doorway }`. Direction only — there is never
a distance, because there is no geometry to measure across.

### 2.2 `LabyrinthState` — `Assets/Scripts/Sim/LabyrinthState.cs`

The labyrinth half of `WorldSim`, reached as `sim.Labyrinth`.

**Shared** (rides the snapshot, public knowledge): `Grid`, `Legend(slot)`,
`IsDown(slot)`, `RespawnTick(slot)`, `Outcome`, `OutcomeTick`, `EscapedMask`,
`MageMask` (zero until the round ends).

**Local only** (never snapshotted, never broadcast, never logged):
`LocalRole`, `LocalCompassKind`, `LocalCompassCell`. They are set from Replies
the host addressed to this peer alone. **No peer's copy of another peer's role or
compass exists anywhere but on the host**, and the host keeps its table in
`LabyrinthRule`, not in the sim.

`Rev` bumps whenever the table or the round changed, so a view can notice
cheaply. It is deliberately **not** `WorldSim.Rev`: that one means "the weapon
and kit tables were replaced, rebuild the scene", and a room swap every twenty
seconds must not put every loose weapon back on its rest pose.

### 2.3 `LabyrinthDef` — `Assets/Scripts/Data/LabyrinthDef.cs`, asset `Assets/Data/Labyrinth.asset`

A `ScriptableObject`, referenced from `GameData.labyrinth`, so `NetSession.Start`
hands it to `WorldSim` and every peer reads the same numbers from the same build.
The asset exists and is linked; it is 5x5 with 25 rooms.

Each `LabyrinthRoomDef` is `{ label, glyph, role }`. **The index in `rooms` is the
room id, and a room id travels on the wire: append only, never reorder.**
`role` is `Blank | Start | BadEnd | GoodEnd`.

The asset as built: id 0 `Weapon Rack` (Start, glyph `R`), id 1
`Resurrection Room` (BadEnd, `X`), id 2 `Gate Hall` (GoodEnd, `G`), ids 3-24
`Room 3`..`Room 24` with their number as the glyph.

### 2.4 Game side

| File | What |
|---|---|
| `Game/LabyrinthRoom.cs` | one authored room: its **room id**, its four doorways in Heading order, a `BoxCollider` footprint (`Contains`), and an `anchor` transform for spawning |
| `Game/LabyrinthDirector.cs` | the only thing that maps sim to scene. `RoomInCell`, `CellOfRoom`, `CellAt(world)`, `DoorwayOf(cell, dir)`, `ResolveTwin(door)`, `DestinationCell/Label/Glyph`, `IsExit`, `ExitDoorway`, `LocalCell`, `CollectPlayerCells` |
| `Game/CompassModel.cs` | what the HUD reads. GREEN: `HasReading`, `Spinning`, `Doorway`, `TargetDoorway`, `WorldDirection` (flat, normalised; it turns on its own while spinning). RED, a Mage's machine only: `HasRedReading`, `RedSpinning`, `RedDoorway`, `RedWorldDirection`. See section 11.3 |
| `Game/RoomFloorNumber.cs` | the room's own number, flat on its floor. See section 11.1 |
| `Sim/ScratchPadState.cs`, `Game/UI/ScratchPadView.cs` | the team's shared scratch pad. See section 11.2 |
| `Game/MagicDoor.cs` | gained a **grid door** mode |
| `Game/WorldAuthorityNet.cs` | the requests, the events, `IHostWorld.CollectPlayerCells` |

**`MagicDoor` grid mode.** Turn on `gridDoor` and fill in `labyrinth` (the
director), `room` (its `LabyrinthRoom`) and `doorwayDir`. `Twin` then resolves
**at the moment it is asked**: this room's cell, the neighbour in that direction,
the room the table currently puts there, that room's opposite doorway. The
serialized `twin` and the link id are ignored. New members: `IsGridDoor`, `Room`,
`DoorwayDir`, `IsExitDoorway`, `DestinationCell`, `DestinationLabel`,
`DestinationGlyph`. `IsPassable` is now `GateOpen && (Twin != null || IsExitDoorway)`,
so the Exit is passable although it has no twin.

**Glyphs never lie; compasses do.** `DestinationLabel` reads straight off the
current table, so a doorway always says truthfully where it leads right now.

---

## 3. The wire

Ten ids in `MsgId`, **0x30-0x39**. Feedback round 2 added the scratch pad's three
on **0x3A-0x3C** (see section 11.2 and NETCODE-STATUS F2), so **0x3D-0x3F** are
what is left free. `Wire.ProtocolVersion` is now **4**.

| Id | Name | Kind | Bytes | Layout after the type byte |
|---|---|---|---|---|
| 0x30 | SWAP_REQ | Intent | 5 | `cellA u16, cellB u16` |
| 0x31 | COMPASS_BEND_REQ | Intent | 5 | `slots u8 (one bit per slot), target u8 (CompassTargetKind), cell u16` |
| 0x32 | LAB_LAYOUT | State | 12 + 2n | `width u8, height u8, startCell u16, badEndCell u16, goodEndCell u16, exitDir u8, count u16, cells u16 x count` (5x5 = **62 bytes**) |
| 0x33 | LAB_SWAPPED | Event | 9 | `tick u32, cellA u16, cellB u16` — **no author** |
| 0x34 | COMPASS_TARGETS | Reply | 4 | `target u8, cell u16` — to one peer, **no author** |
| 0x35 | ROLE_ASSIGN | Reply | 2 | `role u8 (LabyrinthRole)` — to each Mage alone |
| 0x36 | ROUND_RESULT | Event | 8 | `tick u32, outcome u8 (RoundOutcome), escaped u8, mages u8` |
| 0x37 | PLAYER_DOWN | Event | 10 | `tick u32, slot u8, respawnTick u32` |
| 0x38 | PLAYER_RESPAWN | Event | 9 | `tick u32, slot u8, anchorSlot u8 (0xFF nobody), cell u16 (0xFFFF unknown)` |
| 0x39 | LEGEND | Event | 10 | `tick u32, slot u8, legend i32` — absolute, never a delta |

`LAB_LAYOUT`'s count is a raw `u16`, not a `Wire` batch, so it is not capped at
255; `SnapshotPartMsg.MaxBody` is the real ceiling (about 7,900 cells).

**Snapshot.** `SnapshotPartKind` gained `Labyrinth = 5` and `End` moved to `6`.
The part body is `hasGrid bool | [grid] | legend i32 x8 | downMask u8 |
respawnTick u32 x8 | outcome u8 | outcomeTick u32 | escaped u8 | mages u8`.
`SnapshotCodec.Order` ends with `Labyrinth`.

**Replies are how a secret travels.** `NetSession.RaiseReply` now calls
`MessageApplier.ApplyReply`, which handles ROLE_ASSIGN and COMPASS_TARGETS and
nothing else. That one function is reached from both paths: a client's
`SessionRouter.OnReply` default branch, and the host's own `EventSink.LocalReply`.

---

## 4. The rules, and who decides

Everything below is the host. `LabyrinthRule`
(`Assets/Scripts/Session/Rules/LabyrinthRule.cs`) is **both** an `IHostRule` and
the `IIntentValidator` for SWAP_REQ and COMPASS_BEND_REQ, because both powers
need the same secret table, and a validator that could be registered without the
rule would be a way to leak it. It is registered last in
`HostAuthority.CreateDefault()`.

### 4.1 Round start

The first tick in `SessionPhase.Playing`:

1. The grid is regenerated from `WorldSeed + PhaseStartTick * 2654435761`, so a
   second run in the same room is a different labyrinth. Clients do not derive
   it — LAB_LAYOUT carries the table — so the host may reseed freely.
2. `LAB_LAYOUT` goes out (a State: applied to the host's own sim by the same
   `Emit`, and broadcast).
3. Roles are drawn and each Mage gets a `ROLE_ASSIGN` addressed to it alone.
   **Nobody else is told anything**, because a player who is told nothing is a
   weapon and an empty message is still a message.

The draw uses a **host-private `Rng` seeded from a `Guid`**, never `WorldSim.Rng`:
that one is reseeded from SESSION_INFO on every peer, so a client could replay
any draw made from it and name the Mage.

`LabyrinthDef.MageCountFor(playerCount)` = `mageBaseCount`, plus one from
`mageSecondFromPlayers` players up, capped at `mageMaxCount` and at
`playerCount - 1` (never so many Mages that nobody is left to fool).

### 4.2 The Mage's drag (SWAP_REQ)

Accepted only when **all** of these hold, and **refused in silence** otherwise —
no reply, no event, no console line, so a weapon that sends one to see what
happens gets exactly what a Mage on cooldown gets:

- the asker is a Mage and a round is open;
- their own cooldown has expired (`swapCooldownSeconds`, per Mage: **60 s** in
  `Assets/Data/Labyrinth.asset`, **15 s** in `Labyrinth_Tutorial.asset` so the
  lesson is not a wait). The rule reads the asset and nothing else — there is no
  shorter constant hiding anywhere, and the map's cooldown ring is only a local
  guide that can never be more generous than the host;
- the two cells are orthogonally adjacent, never diagonal (`swapAcrossWrap`
  decides whether a drag may leave one edge and arrive at the other);
- neither cell is the start room or either end room;
- **every room can still reach the Exit afterwards** (`EveryCellReachesExit`,
  run on the proposed table and then reverted).

Then `LAB_SWAPPED` goes to everyone. It carries **only the two cells**. Rooms may
be swapped with players inside them; nothing physically moves, the doorways
simply lead elsewhere from that moment.

The connectivity check **cannot fail today**: with all four doorways of every
room working, the wrapped grid is always connected, so a swap only relabels which
room is where. It exists as the hook for the day a doorway can be barred — hang
that rule on `LabyrinthGrid.DoorOpen` and the check and the compass both start
respecting it at once.

### 4.3 The Mage's lie (COMPASS_BEND_REQ)

Same silence on every refusal. Checks: the asker is a Mage, their
`bendCooldownSeconds` has expired, a `Cell` target is in range, the request
actually changes something, and — the important one — after applying it the
number of bent players is **strictly fewer than `bendFractionLimit` of the
non-Mage players**, counting **both Mages' work together**, so two of them cannot
bend the whole room between them. With the default 0.5 that is the minority rule:
2 of 5, 1 of 4, 3 of 7.

A `GoodEnd` target **un-bends**. Each affected player gets a `COMPASS_TARGETS`
addressed to them alone, which does not say who did it, or that anybody did.

### 4.4 The compass

Every player has one. It points along the **shortest path through the doorways,
wrapping edges included**, and what it names is **a doorway of the room you are
in**. Normally the target is the Exit; for a bent player it is the Resurrection
Room or a room the Mage picked, and nothing on that machine can tell the
difference.

It **spins** when the player is standing in its target room, and when there is no
reading at all. One deliberate reading of the rule: on the truthful setting,
standing in the good-end room, it points at the **Exit doorway** rather than
spinning — the Exit is still "a doorway of the room you are in", and pointing at
it is strictly more useful. Change `LabyrinthGrid.ToExit` if that is wrong.

### 4.5 Where everybody is

`IHostWorld.CollectPlayerCells(int[] cellBySlot, bool[] atExitBySlot)`, new this
stage, implemented by `WorldAuthority` and answered by the scene's
`LabyrinthDirector`. It reads **the sim's player rows**, which every peer
streams, so one implementation answers for local and remote players alike:
`CellAt(p.pos)` against each room's footprint, and "within `exitGatherRadius` of
the exit doorway" for the at-exit bit. The rule calls it **five times a second**
(every 4 ticks). A scene without a director returns false and every round rule
sits idle, which is why `Zone1` and the FeelBox are unaffected.

### 4.6 The two endings

Checked in the same 5 Hz scan, the Mage's win first so it beats a simultaneous
escape:

- **Resurrection Room** — strictly more than `resurrectionFraction` of the
  **connected players** (Mages included: they are players) inside that room at
  once. Default 0.5, so a majority. **Instant Mage win.**
- **Exit** — at least `ceil(escapeFraction x nonMageCount)` of the connected
  **non-Mage** players gathered at the good-end doorway. Default 1.0: all of
  them. **The weapons win.**

Either one emits `ROUND_RESULT` — the **one message that ever names the Mages**,
and the round can no longer be played when it goes out — and then `SESSION_END`
with `Escaped` or `CrewLost` and `finalScore` = the total legend. `SessionRunner`
already loads `MainMenu` on `SessionPhase.Ended`, which is the "back to the room
page" the design asks for.

### 4.7 Death, respawn and legend

Legend is a per-player integer and nothing more yet. Nothing in the game calls
death today; the mechanism is built and waiting:

- Host: `WorldAuthority.ReportPlayerDown(slot)` emits `PLAYER_DOWN` (with
  `respawnTick = now + respawnDelaySeconds`) and `LEGEND(slot, legendOnDeath)`.
  Death costs **all** of it.
- `LabyrinthRule` watches the sim each scan; when the respawn tick comes it picks
  the **anchor** and emits `PLAYER_RESPAWN(slot, anchorSlot, cell)`.
- The anchor is "respawn with the group" without a party leader: the cell with
  the most standing players, then the player in it nearest the middle of that
  group. `NoSlot` when nobody is placed yet.
- `WorldAuthority.ReportLegend(slot, legend)` sets it outright at any time.
  Absolute, never a delta, so a dropped event cannot drift.

Splitting up is allowed and nothing punishes it.

---

## 5. Every tunable

All on `Assets/Data/Labyrinth.asset` (`LabyrinthDef`).

| Field | Default | What |
|---|---|---|
| `gridWidth`, `gridHeight` | 5, 5 | clamped to 3..64. Below three a side the centre cell would also be a corner |
| `rooms` | 25 entries | index = room id. Append only |
| `swapCooldownSeconds` | **60** | between one Mage's drags. The tutorial's def is **15** |
| `swapAcrossWrap` | off | may a drag leave one edge and arrive at the other |
| `bendCooldownSeconds` | 15 | between one Mage's bends |
| `bendFractionLimit` | 0.5 | exclusive. 0.5 = strictly fewer than half the non-Mages bent at once |
| `mageBaseCount` | 1 | |
| `mageSecondFromPlayers` | 6 | a second Mage from this many players up |
| `mageMaxCount` | 2 | |
| `escapeFraction` | 1.0 | inclusive. Fraction of non-Mages that must gather at the Exit |
| `resurrectionFraction` | 0.5 | exclusive. Fraction of **all** players in the bad end at once |
| `exitGatherRadius` | 6 m | how close to the exit doorway counts as gathered |
| `respawnDelaySeconds` | 10 | |
| `legendOnDeath` | 0 | what a downed player is left with |
| `showDoorLabels` | **off** | the destination glyph and name above every doorway. Off: a doorway says nothing at all |
| `showFloorNumbers` | on | each room's own number, flat on the middle of its floor |
| `padMaxStrokes` | 300 | strokes the shared scratch pad keeps; past this the **oldest** fall off |
| `padMaxPoints` | 4000 | points across every stroke together (about 16 KB in a snapshot) |
| `padStrokesPerSecond` | 20 | strokes one player may have accepted per second |
| `padStrokeBurst` | 40 | strokes a player may fire off at once before the rate bites |

`LabyrinthDef.Problem()` returns one sentence per problem and `""` when the asset
is usable: too small, too large, fewer rooms than cells, or a missing role.

---

## 6. How to change it

### Add a room / change the grid size

1. Open `Assets/Data/Labyrinth.asset`. Set `gridWidth` / `gridHeight`.
2. **Append** entries to `rooms` until there are at least `width x height` of
   them. Never reorder or insert: the index is the room id and it travels on the
   wire. `Problem()` tells you when the count is short.
3. Author the new rooms in the labyrinth scene (stage 6): one `LabyrinthRoom`
   each, its `roomId` set to its index, its four `MagicDoor`s wired into
   `doorways` in N E S W order with `gridDoor` on, `labyrinth` and `room` filled
   in, and a `footprint` box that covers the floor.
4. Add each room to `LabyrinthDirector.rooms`. Order there does not matter — each
   room carries its own id — but every id must be unique or `Awake` says so.
5. Nothing else. The layout, the wrapping, the compass and the swap rules all
   read the size from the grid.

Growing the grid costs 2 bytes per cell on LAB_LAYOUT and in the snapshot, so a
10x10 is 212 bytes. Room ids are `ushort`, so 65,535 is the hard ceiling and
`MaxSide` (64) is the soft one.

### Move the ends, or make a doorway barrable

- Which corners the ends land in and which way the Exit points are chosen by the
  seed in `LabyrinthGrid.Generate`. That is the only place to change it.
- To bar a doorway, set `LabyrinthGrid.DoorOpen` to a `DoorwayFilter`. The BFS,
  the compass and the swap connectivity check all go through `Passable`, which
  asks it from both sides.

---

## 7. What is NOT done

- **Crossing the Exit does nothing yet.** The win condition is proximity
  (`exitGatherRadius` of the exit doorway), not a traversal. The exit doorway
  reports `IsPassable` but has no twin, so `RequestMagicDoorTraverse` returns
  false and the player is not moved.
- **Nothing calls `ReportPlayerDown`.** What counts as death is not decided.
- **Legend is only an integer.** Nothing awards it.
- **A bent compass is not re-sent on a resync.** The snapshot does not clear it,
  so it survives; a genuine reconnect (which does not exist) would lose it.
- **The Mage cannot see who is bent.** The host knows; nothing reports it back,
  because a reply to the Mage would be a second place the secret lives. The map's
  "BENT n / max" counter therefore counts only what **this** Mage asked for, and
  with two fragments it undercounts.
- **Cooldown rings are a local guide, not the truth.** The host owns the real
  cooldowns and refuses in silence. The swap ring starts when the swap this peer
  asked for actually lands (`LAB_SWAPPED`); the bend ring starts optimistically on
  the ask, because nothing ever comes back. Either way the ring is only ever more
  cautious than the host.
- **Nothing has been run.** No play mode, no test, no socket.

---

## 8. The scene — `Assets/Scenes/Labyrinth.unity`

Build index **3** (`Boot` 0, `MainMenu` 1, `Zone1` 2, `Labyrinth` 3), which is
what `MenuFlow.IsInBuild` was waiting for: HOST now actually reaches it.
2,269 GameObjects, 1,229 static, 1 Camera, 1 AudioListener, 1 WorldAuthority,
1 LevelClock, 1 LabyrinthDirector. Scene validator: **0 problems**.

### 8.1 Roots

| Root | What |
|---|---|
| `_Managers` | `SessionRunner` (GameData), `LevelClock`, `WorldAuthority`, `LabyrinthDirector`, `CompassModel`, `PlayerSpawner`, `PoseStreamer`, `RemotePlayerSpawner` |
| `_Cameras` | `Main Camera` — Camera + AudioListener + `OrbitCamera`, same numbers as `Zone1` |
| `_Lighting` | `Directional Light` (0.12, soft shadows), `Global Volume` (`VP_Zone1`). Flat ambient `0.140 0.140 0.165`, linear fog 14→60 — the tutorial scene's settings |
| `_UI` | `HUD` (the existing `HUD.prefab`), `LabyrinthHud` (`UIDocument` sortingOrder **1**, `Labyrinth.uxml` + `Labyrinth.uss`) |
| `Environment/Rooms` | the 25 room instances |

`WorldAuthority` arrays: `magicDoors` **100**, `rooms` **26** (25 `RoomVolume`
plus the Resurrection Room's own), `weapons` **7**, `labyrinth` = the director.
Every other array is empty: there are no goblins, doors, keys or kit pieces in
the labyrinth yet.

Scene ids run **101..302**, assigned in hierarchy order, one per `ISceneId`
(100 doorways, 26 volumes, 25 signs, 25 room spawn points, 8 soul spawns,
7 weapons, 7 home slots, 4 exit zones).

### 8.2 The lattice

The grid is **not physical**. Room **id** `i` sits at

```
x = (i % 5) * 200      z = -(i / 5) * 200      y = 0
```

so id 0 is at the origin, id 4 at `(800, 0, 0)`, id 24 at `(800, 0, -800)`.
200 m apart, footprints 56 m across: nothing can overlap and nothing can be seen
from anywhere else. **Which CELL a room is in has nothing to do with where it
stands** — that is the host's table, and the doorways are the only way across.

| Room id | Prefab | Role | World position |
|---|---|---|---|
| 0 `Weapon Rack` | `LabyrinthRoom_Start` | Start (always the centre cell) | (0, 0, 0) |
| 1 `Resurrection Room` | `LabyrinthRoom_BadEnd` | BadEnd (a corner, by seed) | (200, 0, 0) |
| 2 `Gate Hall` | `LabyrinthRoom_Exit` | GoodEnd (a different corner) | (400, 0, 0) |
| 3..24 `Room 3`..`Room 24` | `LabyrinthRoom` | Blank | the lattice above |

### 8.3 `Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom.prefab` — the blank room

Grey-box, square, **24 m across and 10 m high**, `World` layer, static geometry.
Local space: **+Z is the room's north, +X its east.**

```
LabyrinthRoom            LabyrinthRoom (roomId, doorways[4], footprint, anchor)
  Geometry               static, World layer, M_Floor / M_Wall
    Floor                25 x 0.5 x 25 at y -0.25          (floor top = y 0)
    Roof                 25 x 0.5 x 25 at y 10.25
    Wall_North|East|South|West
        Left, Right      10.75 wide, 10 high, 0.5 thick; wall centre at +-12.25
        Lintel           3 wide over the opening, from y 3.5 to the roof
  Doorways               a 3.0 x 3.5 opening in the middle of every wall
    Doorway_North        MagicDoor_Grid at (0,0, 12)  yaw 180   doorwayDir 0
    Doorway_East         MagicDoor_Grid at ( 12,0,0)  yaw 270   doorwayDir 1
    Doorway_South        MagicDoor_Grid at (0,0,-12)  yaw   0   doorwayDir 2
    Doorway_West         MagicDoor_Grid at (-12,0,0)  yaw  90   doorwayDir 3
  Fixtures
    Torch_1..4           the four inner corners, (+-11.4, 3.2, +-11.4), range 22
    RoomSign             Sign at (-6.5, 0, -10.5), text = the room's label
  RoomVolume             trigger box 24 x 10 x 24, roomName = the room's label
  Footprint              DISABLED trigger box 56 x 44 x 56 at (0, 10, 0)
  Anchor                 (0, 1.2, 0) - where a player starts or respawns here
  SpawnPoint             (0, 1.2, -4)
```

**Why the doorway yaws are what they are.** A `MagicDoor`'s local **+Z faces OUT
of the alcove, into the room**, because `CrossedFromFront` only accepts a
traveller whose local z goes from positive to negative. So the north doorway,
which stands on the +Z wall, is turned 180 degrees. Verified by reading the
instances back: north `forward = (0,0,-1)`, east `(-1,0,0)`, south `(0,0,1)`,
west `(1,0,0)`.

**The footprint is deliberately huge** (56 x 44 x 56, from y -12 to y +32) because
`CollectPlayerCells` maps a streamed position to a cell by this box alone: a
player above the roof or in the gap between rooms is in no cell and counts for
nothing (NETCODE-STATUS S5.5 #2). At 200 m spacing two footprints still cannot
touch.

### 8.4 `Assets/Prefabs/Kit/Doors/MagicDoor_Grid.prefab` — a grid doorway

`MagicDoor.prefab` with the `Door` and `DoorCondition` removed (a grid doorway
has no gate) and the solid `Panel` deleted (it is always open). What is left is
the alcove — jambs, lintel, back, alcove floor, `NavBlock` — plus the glowing
`Plane`, and:

- `gridDoor` **on**, `twin` and `gate` empty, `linkId` 0. The far side is
  resolved from the table the moment it is asked; the serialized twin is ignored.
- `Glyph` and `DestinationName`, two world-space TextMeshPro labels above the
  opening, driven by **`DoorwayGlyph`** (`Assets/Scripts/Game/DoorwayGlyph.cs`),
  which writes `MagicDoor.DestinationGlyph` / `DestinationLabel` four times a
  second. **Both are switched off in the prefab and by the data** — see
  section 11.1. The logic is untouched: set `LabyrinthDef.showDoorLabels` and
  they come straight back, reading the right way round.

Per instance the scene sets `id`, `authority`, `labyrinth`, `room` and
`doorwayDir`. The scene validator now knows about grid doors: it skips the
twin / link-id checks for them and instead insists on a director, a room, and
that the room lists this very door for that direction.

### 8.5 The three variants

| Variant | Adds |
|---|---|
| `LabyrinthRoom_Start` | `Gameplay/WeaponRack` (the full 7-slot `WeaponRack.prefab`) at local (-7, 0, 10.5) — slid **west so the north doorway stays clear**, because the rack is 10 m wide and the opening is 3 m at x 0 — with `Gameplay/Weapons`: Sword, Dagger, Mace, Staff, Hammer, spare Sword, spare Mace, each on slots 1..7. `SoulSpawns/SoulSpawn_0..7`, an arc of 8 in the south half |
| `LabyrinthRoom_BadEnd` | `ResurrectionRing` (a red `M_Hazard` disc 17 m across with an `M_Floor` disc inside it, so it reads as a ring) and `ResurrectionVolume`, a 17 x 6 x 17 `RoomVolume` named "Resurrection Room" that counts the weapons standing in it |
| `LabyrinthRoom_Exit` | `ExitZones/ExitZone_North|East|South|West` — **all four**, because which corner the good end lands in and which way its Exit faces are chosen by the seed (`LabyrinthGrid.Generate`). Each is a trigger box on the `Trigger` layer with an `ExitZone` that lights up only while **its** doorway is `IsExitDoorway`: a bright `M_Glow` ring **12 m across, centred on the doorway — that is `exitGatherRadius` drawn on the floor** — and a bright arch over it, plus a second gold disc while a weapon is standing in it |

`ExitZone` (`Assets/Scripts/Game/ExitZone.cs`) decides nothing. The round is won
by the host, measuring `exitGatherRadius` from the exit doorway's own transform;
the zone is that spot made visible so the crew can see where to stand.

### 8.6 The UI — `Assets/UI/Labyrinth.uxml` + `Labyrinth.uss`

A second `UIDocument` (sortingOrder 1) over the weapon HUD, driven by
`LabyrinthHud` (`Assets/Scripts/Game/UI/LabyrinthHud.cs`) with the map page in
`LabyrinthMapView` (`Assets/Scripts/Game/UI/LabyrinthMapView.cs`). Same dark
ground and amber accent as the menu.

| Element | What |
|---|---|
| `compass` | a ring and its spikes, top left, painted by `CompassView` with Painter2D — see section 11.3. A spike's angle is `Mathf.DeltaAngle(camera yaw, atan2(dir.x, dir.z))` off `CompassModel`, so it points at the doorway **on screen**; the model turns the vector on its own while spinning, so the spike spins with it. **Nothing else is on it** |
| `map-overlay` | the whole **Tab overlay** (the `Map` action in `PeskyControls`, `<Keyboard>/tab` and `<Gamepad>/select`), now two pages — see section 11.2. `map-page` is a Mage's only: the grid as tiles, north at the top, `cell = row * width + column`, each tile the room's glyph and name; classes mark the current cell (`YOU`), visited cells, the start, the two ends and the exit side (`EXIT N`). `pad-page` is everybody's |
| `mage-bar` | **inside the overlay, so it cannot be seen or picked while the map is closed.** Drag a tile onto an orthogonal neighbour to send `SWAP_REQ`; a ghost follows the pointer and only legal drops highlight (`LabyrinthGrid.CanSwap`), an illegal one simply snaps back. Click a player chip, then a tile or **BAD END**, to send `COMPASS_BEND_REQ`; **UN-BEND** sends `GoodEnd`. Two Painter2D cooldown rings and `BENT n / max`, where max is the strict minority of the non-Mages |
| `role-reveal` | 1.5 s after the layout arrives (so a `ROLE_ASSIGN` can land first), for 6 s: "YOU ARE A WEAPON" or "YOU ARE A FRAGMENT OF THE ARCH MAGE". Shown to that player alone, and shown again if `ROLE_ASSIGN` turns up late |
| `result-banner` | on `ROUND_RESULT`: "THE WEAPONS ESCAPED" / "THE ARCH MAGE WINS" and the fragments' names — the one message that ever names them. `SessionRunner` then loads `MainMenu`, which is the room page |

**Pointer lock.** Opening the map frees the cursor **for a Mage only**
(`OrbitCamera.InputEnabled = false` then `FreePointer`), because only a Mage needs
to click; a weapon keeps looking around while it reads the map. Closing re-locks.
`InputEnabled` must go false as well, or `OrbitCamera` re-locks on the first click
of the drag.

---

## 9. How to add a real room

The blank room is a grey box on purpose. To make one of them an actual place:

1. **Make a variant, never an edit.** Right-click `LabyrinthRoom.prefab` >
   Create > Prefab Variant, or drag an instance into `Assets/Prefabs/Rooms/`.
   Build the new content **inside** the variant. The four doorways, the
   footprint, the anchor, the volume and the sign then keep working for free,
   and a later fix to the blank room still reaches it.
2. **Leave the four doorways where they are**, at (0,0,±12) / (±12,0,0) with
   yaws 180 / 270 / 0 / 90 and `doorwayDir` 0..3. If the room has to be a
   different shape, move them — but keep one doorway per Heading, keep each
   one's local **+Z pointing into the room**, keep them upright (only yaw), and
   keep the `LabyrinthRoom.doorways` array in **N E S W** order. Nothing in the
   code says "square"; only these four facts matter.
3. **Grow the footprint to cover the new shape**, generously in every direction
   including up. A player outside every footprint is in no cell, and counts for
   nothing — not for the endings, not as a respawn anchor.
4. **Put geometry under `Geometry`**, on the `World` layer, marked static. The
   scene validator enforces exactly that for anything on a path containing
   `/Geometry`.
5. **Swap the instance in the scene.** Select `Environment/Rooms/Room_NN_...`,
   use the Inspector's prefab field (or Replace Prefab), then re-set `roomId` to
   **NN** — the instance's room id must stay its index in `LabyrinthDef.rooms`.
   Re-add it to `LabyrinthDirector.rooms` if the replacement dropped the link.
6. **Register whatever you added**: new `MagicDoor`s, `RoomVolume`s, `Door`s,
   `WeaponBody`s, goblins and kit pieces all go into the matching
   `WorldAuthority` array, or the authority cannot see them. Give every new
   `ISceneId` a scene id no other object in `Labyrinth.unity` uses (the ones in
   use today are 101..302).
7. **Change the room's name and glyph** in `Assets/Data/Labyrinth.asset` at index
   NN, then copy it onto the instance's `RoomSign.text` and
   `RoomVolume.roomName`. The map and every doorway glyph read the asset, so
   they follow on their own.
8. **Run `Pesky > Validate Open Scenes`.** It checks the room ids are unique and
   in range, that all four doorways exist, are grid doors, point back at their
   room and carry the right direction, that every room is listed in the
   director, and that the def still has at least `width x height` rooms.

Making the grid bigger is section 6; a new room's **position on the lattice** is
`((id % 5) * 200, 0, -(id / 5) * 200)` today — widen the stride, not the pattern,
if a room ever grows past 100 m.

---

## 10. The second labyrinth: the tutorial's practice grid

`Tutorial.unity` runs a **3x3 labyrinth of nine rooms** under its `Room6_MageTutorial`
root, on the same director, the same grid doorways, the same compass and the same
map — no tutorial copy of anything in this document exists. What differs is data:

| | real | tutorial |
|---|---|---|
| def | `Assets/Data/Labyrinth.asset` (5x5, 25 rooms) | `Assets/Data/Labyrinth_Tutorial.asset` (3x3, 9 rooms) |
| reached through | `GameData.asset` | `GameData_Tutorial.asset`, a copy whose only difference is `labyrinth` |
| lattice | `((id % 5) * 200, 0, -(id / 5) * 200)` | `(300 + (id % 3) * 120, 0, -(id / 3) * 120)` |
| swap / bend cooldown | **60 s** / 15 s | **15 s** / 5 s |
| `resurrectionFraction` | 0.5 | **1**, so a solo player standing in the Resurrection Room cannot end the round |

**3x3 is the smallest labyrinth these rules allow.** `MinSide` is 3, and
`Generate` fixes the centre cell (Start) and two corners (the ends): a 3x1 strip
would have no swappable cell at all, because `CanSwap` refuses any cell holding
one of those three rooms. If a smaller practice grid is ever wanted, the shortest
one that can teach a swap is **six cells in a row** (centre 3 and corners 0 and 5
fixed, 1-2 free and adjacent).

Because the tutorial has one player, `MageCountFor(1)` makes that player the
Mage, so the escape ending can never fire there (`nonMage == 0`). The lit ring at
the exit ends the tutorial through `TutorialTrigger` instead — the same spot, a
different trigger.

---

## 11. Feedback round 2 — what the player is told, and the scratch pad

**Implemented, untested** (2026-09-20). The only checks made were a clean
compile, the edit-mode scene validator on `Boot`, `MainMenu`, `Tutorial` and
`Labyrinth` (0 problems each), every new message encoded and decoded once in the
Editor, and serialized values read back. **Nothing was run.**

### 11.1 The doorways say nothing; a room says its own number

The destination glyph and name over every grid doorway are **gone**, behind data
rather than deleted:

- `LabyrinthDef.showDoorLabels` is **false** on both assets. `DoorwayGlyph` reads
  it through `MagicDoor.Labyrinth` (a new accessor) and switches the two label
  objects off; the resolve-and-write logic is untouched, so turning the toggle on
  brings them back exactly as they were.
- In `Assets/Prefabs/Kit/Doors/MagicDoor_Grid.prefab` the `Glyph` and
  `DestinationName` objects are **inactive** as well, so a doorway is blank even
  with no labyrinth in the scene.
- Both were also **rotated 180 degrees** while they were open. That is the real
  cause of "the numbers above the doors are backwards": a `TextMeshPro` mesh
  faces its own local **-Z**, and at identity rotation on a doorway whose +Z
  points into the room, the readable face pointed into the alcove.

In their place, every room now tells you **its own number and nothing else**:

`Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom.prefab` gained `Fixtures/FloorNumber` — a
world-space `TextMeshPro` (LiberationSans SDF on `M_TextWorld`, size 20, bold,
a 14 x 14 rect) at local **(0, 0.03, 0)**, rotation **(90, 0, 0)**, driven by
`Assets/Scripts/Game/RoomFloorNumber.cs`.

**Why (90, 0, 0).** A TMP's readable face is local **-Z** and the top of its
letters is local **+Y**. Euler (90, 0, 0) turns -Z to world **up** and +Y to the
room's **+Z, which is its north doorway**: the number lies flat on the floor,
face up, top towards north, and reads the right way round from the camera above.
Read back from the prefab: `-forward = (0, 1, 0)` and `up = (0, 0, 1)`.

The text is the room's `LabyrinthRoom.RoomId` at runtime, so all 25 rooms, the
three variants and the tutorial's nine practice rooms get theirs from one prefab
with nothing authored per instance. Set `overrideText` on an instance to say
something else. `RoomFloorNumber` finds the director through the room's **north
doorway** (`room.Doorway(0).Labyrinth`) when its own field is empty, so no
instance needed rewiring — no `Find`, no singleton.

**The room-name Sign was mirrored too, and the fix is project-wide.**
`Assets/Prefabs/Kit/Signs_And_Lights/Sign.prefab`'s `Label` sat at the sign's local `z = -0.06`
(in front of the board's -Z face) but was **rotated 180**, which pointed its
readable face back into the board. The rotation is now identity, so the text
faces the side it physically sits on. No `Sign` anywhere in the project overrides
that rotation, so no sign changes which side it shows — only whether it reads
correctly. To undo: `Sign.prefab` > `Label` > yaw 180.

### 11.2 The Tab overlay is now the shared scratch pad

**For a weapon, holding Tab opens one page: the pad.** The auto-map is gone for
everybody but the Mage — a weapon is shown **no labyrinth information at all**,
because working the maze out is the game. For a **Mage** the overlay has two top
tabs: **MAP** (the interactive grid of section 8.6, drag-to-swap and the bend
chips, unchanged) and **PAD** (the same shared pad everybody else has).

The pad is a plain dark canvas, 760 x 470 in `Labyrinth.uss`:

| | |
|---|---|
| draw | left mouse, in **this player's slot colour** (`SlotColors.For(slot)`) |
| erase | right mouse, or the ERASER button. An eraser is **not a delete**: it is a stroke painted in the canvas colour on top, so replaying the list gives every peer the same picture |
| widths | THIN / MEDIUM / THICK, classes 0-2 |
| CLEAR | **host only**; the button is not drawn at all on a client |
| wiped | at the start of **every round** (PAD_CLEAR on the first tick of `Playing`) |

Shared over the network: strokes are **vector polylines in normalised canvas
coordinates**, a client sends PAD_STROKE_REQ, the host validates size and rate,
assigns a sequence number and broadcasts PAD_STROKE, and every peer paints them
in order. A snapshot part carries the whole list to late joiners. The wire is in
**NETCODE-STATUS.md, section F2** — ids 0x3A-0x3C, 260 / 267 / 5 bytes, up to 64
points per message with longer strokes split on a shared joining point, caps of
300 strokes and 4,000 points, and `Wire.ProtocolVersion` now **4**.

**The drawer does not wait.** `ScratchPadView` paints its own stroke the instant
it is drawn and hands it over when the host's echo names its own slot; a stroke
the host never accepts fades after 3 seconds, which is the only feedback a
refusal ever gives. Offline the round trip is synchronous inside
`NetSession.Send`, so the pad works in the tutorial through the loopback session
like everything else.

**Pointer lock.** Opening the overlay now frees the cursor **for everybody**, not
just a Mage, because everybody draws with the mouse; closing re-locks it, exactly
as the map did.

New files: `Assets/Scripts/Game/UI/ScratchPadView.cs`,
`Assets/Scripts/Sim/ScratchPadState.cs`,
`Assets/Scripts/Session/Rules/PadRule.cs`,
`Assets/Scripts/Protocol/Messages/PadMessages.cs`.

### 11.3 The compass is a ring and its spikes

`Assets/Scripts/Game/UI/CompassView.cs` paints it with Painter2D. There is no
needle, no `N/E/S/W` caption and no destination name any more: **nothing else is
on the compass.**

- A **GREEN** spike from the middle points at the doorway of this room to take.
  Every player has it, **including a player the Mage has bent** — it is still
  green and simply points the wrong way, and nothing on that machine can tell.
- A **Mage** has a second, **RED** spike towards the **Resurrection Room**. His
  green one is always the truth, because the host never bends a Mage.
- A spike **spins** while the player is standing inside that spike's target room,
  and while it has no reading.
- The **green spike is drawn smaller than the red one and on top of it** — 70% of its
  length and 60% of its width by default — so a Mage whose two readings point the same
  way still sees both. All five shape numbers are USS custom properties on `.compass-dial`
  in `Assets/UI/Labyrinth.uss` (`--spike-length`, `--spike-width`, `--spike-tail` describe
  the full-size red spike as fractions of the dial radius; `--green-length-scale` and
  `--green-width-scale` shrink the green one), with the same values as fallbacks in
  `CompassView`. A weapon, who only ever has the green spike, simply gets the smaller one.

`CompassModel` gained the red half: `HasRedReading`, `RedSpinning`, `RedDoorway`
and `RedWorldDirection`, computed from `Grid.ToCell(cell, Grid.BadEndCell)` and
**only when `LabyrinthState.LocalRole` is `Mage`** — a weapon's machine never
computes it, so there is nothing to read out of it.

### 11.4 The swap cooldown is a minute

`Assets/Data/Labyrinth.asset` `swapCooldownSeconds` is now **60**.
`Assets/Data/Labyrinth_Tutorial.asset` is **15**, so the lesson is not a wait.
`LabyrinthRule` reads the asset and nothing else; the 60 in `LabyrinthDef`'s
field initialiser is only what a brand-new asset starts at, and the HUD's ring
is a local guide that can never be more generous than the host.

### 11.5 A doorway opening that leads nowhere is now a validator error

`Pesky > Validate Open Scenes` gained `CheckDoorwayOpenings`. Every wall segment
named `..._Doorway` (the room builder's `DoorwayWallSegment`) must either hold a
`Door` or `MagicDoor` within **2.5 m**, or have floor **3 m out on both sides**
(a raycast down 10 m from 0.75 m above the sill). A magic doorway's alcove is
solid on purpose, which is why a door short-circuits the floor test.

To seal an opening: fill it with wall and **rename the segment** (`..._Sealed`).
It is no longer a doorway, so it is no longer checked.

---

## 12. Room prefabs — the shapes you can drop into a cell

**Authored 2026-09-20 by the cleanup pass. Implemented, untested: nothing was run,
and none of them is placed in `Labyrinth.unity`. They are inventory.**

They live in `Assets/Prefabs/Rooms/Labyrinth/` beside the square `LabyrinthRoom` and its
three variants. Each one was built with the **Room Shape Builder**
(`Pesky / Rooms / Room Shape Builder`) out of `Assets/Prefabs/Rooms/Pieces/*`, then given
the square room's own fixtures — so every one of them carries the same parts as
`LabyrinthRoom.prefab` and obeys the same four facts from section 9.2: one doorway per
Heading, each upright with its local **+Z pointing into the room**, listed in `doorways` in
**N E S W** order, and a footprint that covers the shape.

Every one has: four `MagicDoor_Grid` doorways (`gridDoor` on, `doorwayDir` 0..3, `room`
pointing back at the room), a `Geometry/Shell` on the **World** layer marked fully static,
a roof, **four torches (four lights, under the six-light rule)**, a `RoomVolume`, a
`RoomSign`, a `Fixtures/FloorNumber` (the room's own number, flat on the floor), an
`Anchor`, a `SpawnPoint` and a disabled `Footprint` box. `roomId` is **0** on all of them:
set it per instance.

| Prefab | Shape | Footprint of the room itself | Doorway slots, local | `RoomVolume` | `Footprint` box |
|---|---|---|---|---|---|
| `LabyrinthRoom` | square, 24 across, 10 high | 24 x 24 | N/E/S/W at (0,0,±12) / (±12,0,0) | 24 x 10 x 24 | 56 x 44 x 56 @ y 10 |
| `LabyrinthRoom_Round` | 12-sided, 26 across, 10 high, 6.7 m walls | 26 x 26 | (0,0,±12.31) / (±12.31,0,0) | 26.1 x 11 x 26.1 @ y 5 | 57.1 x 44 x 57.1 @ y 10 |
| `LabyrinthRoom_Octagon` | 8-sided, 26 across, 10 high, 9.9 m walls | 25 x 25 | (0,0,±11.76) / (±11.76,0,0) | 25 x 11 x 25 @ y 5 | 56 x 44 x 56 @ y 10 |
| `LabyrinthRoom_LShape` | L, 30 x 30 with 12 m arms, 10 high | 30 x 30 minus an 18 x 18 bite out of the north-east | N (-9,0,14.75), E (14.75,0,-9), S (0,0,-14.75), W (-14.75,0,0) | 31 x 11 x 31 @ y 5 | 62 x 44 x 62 @ y 10 |
| `LabyrinthRoom_LongGallery` | rectangle 24 x 48, long in Z, 10 high | 24 x 48 | N (0,0,23.75), E (11.75,0,0), S (0,0,-23.75), W (-11.75,0,0) | 25 x 11 x 49 @ y 5 | 56 x 44 x 80 @ y 5 |
| `LabyrinthRoom_TallShaft` | 8-sided, 20 across, **40 high**, 7.7 m walls | 20 x 20 | (0,0,±8.99) / (±8.99,0,0) | 19.5 x 41 x 19.5 @ y 20 | 50.5 x 74 x 50.5 @ y 40 |

**None of them seals a slot.** Every shape here is wide enough on all four sides to host a
3 m opening (the builder wants a wall at least 4 m long), so all four doorways are real.

### 12.1 Sealed slots, if a future shape needs one

`LabyrinthRoom` gained `sealedSides` — four bools in N E S W order — for a shape that
**cannot** host a door on some side. Leave that entry of `doorways` empty and tick the
matching `sealedSides` box; `LabyrinthRoom.IsSealed(dir)` reports it and
`Pesky > Validate Open Scenes` then stops calling the empty slot a mistake (an empty slot
that is *not* declared sealed is still an error).

**A sealed slot is not yet honoured by the grid.** `LabyrinthGrid.Neighbour`, the BFS
behind the compass and `EveryCellReachesExit` all assume all four doorways of every room
work, because `LabyrinthGrid.DoorOpen` is null (section 4.2). Put a sealed room in a cell
today and the compass will happily route the crew through a wall. Before using one, set
`DoorOpen` to a `DoorwayFilter` that asks the `LabyrinthDirector` for the room in that cell
and returns `!room.IsSealed(dir)`; the BFS, the compass and the swap connectivity check all
go through `Passable`, which asks it from both sides, so that one hook fixes all three at
once.

### 12.2 The 1x2 room: what the grid would need

`LabyrinthRoom_LongGallery` is a **single-cell** long room, not a 1x2 one. The grid cannot
host a room that spans two cells: `LabyrinthGrid` is one `roomId` per cell and
`CellOfRoom` is one-to-one, `Neighbour(cell, dir)` is per cell, and `CanSwap` / `ForceSwap`
move exactly one cell's worth. A real 1x2 would need, at least:

1. a room's **footprint in cells** in `LabyrinthDef` (a width and height per room entry),
   with `Generate` placing the multi-cell rooms first and the table storing the same room id
   in every cell it covers;
2. `CellOfRoom` returning the room's **origin** cell, and everything that asks "which cell am
   I in" agreeing on that;
3. `Neighbour` resolving from the **doorway's own cell**, not the room's origin, so the
   gallery's north doorway leaves from the cell it is physically at;
4. a swap rule that moves a whole footprint and refuses overlaps;
5. more than four doorways per room, or a rule that the covered cells' other edges are
   sealed (see 12.1).

Until then a long room simply occupies one cell and is taller inside than the grid implies.

### 12.3 Recipe: swap a blank room for one of these

1. Open `Assets/Scenes/Labyrinth.unity` and pick the room to replace in
   `Environment/Rooms/Room_NN_...`. Note its **room id NN** and its world position (it is
   `((NN % 5) * 200, 0, -(NN / 5) * 200)`).
2. Drag the new prefab into `Environment/Rooms`, set the same position and rotation, and
   name it `Room_NN_<Shape>`.
3. On its `LabyrinthRoom`, set **`roomId` = NN**. The id is the index in
   `LabyrinthDef.rooms` and it travels on the wire, so it must match.
4. Give every `ISceneId` on it an id no other object in `Labyrinth.unity` uses (today's are
   101..302): the four `MagicDoor`s, the `RoomVolume`, the `RoomSign` and the `SpawnPoint`.
5. Fill in each doorway's `authority` (the scene's `WorldAuthority`) and `labyrinth` (the
   scene's `LabyrinthDirector`) — prefabs cannot hold scene references, so these are empty
   until you wire them. `room` and `doorwayDir` come from the prefab already.
6. Register it: the four doorways go in `WorldAuthority.magicDoors`, the `RoomVolume` in
   `WorldAuthority.rooms`, and the room itself in `LabyrinthDirector.rooms` (replacing the
   old entry).
7. Delete the old room object.
8. Set the room's name and glyph in `Assets/Data/Labyrinth.asset` at index NN, then copy the
   name onto `RoomSign.text` and `RoomVolume.roomName`.
9. Run **`Pesky > Validate Open Scenes`**, and save.

A bigger shape does not need more room on the lattice — 200 m apart with the widest of these
62 m across still leaves 138 m of nothing between neighbours — but widen the stride in
section 8.2 before any room grows past 100 m.
