# KIT — the pieces rooms are built from

Status of everything below: **implemented, untested** unless an older BUILD-LOG section says it was measured.
Scripts are in `Assets/Scripts/Game`, namespace `Pesky.Game`.

## Where the pieces live (2026-09-20 cleanup)

Every kit prefab is under `Assets/Prefabs/Kit/<group>/`, so you can find one by what it *does*.
Nothing was renamed and nothing lost its GUID — every scene reference survived the move.

| Folder | Prefabs |
|---|---|
| `Kit/Doors/` | `Door` + variants `Door_AlwaysOpen` `Door_Key` `Door_Lever` `Door_Plate` `Door_RoomCleared` `Door_Scales` `Door_Sealed`, `MagicDoor`, `MagicDoor_Grid`, `PorterGate` |
| `Kit/Pickups/` | `Key`, `RunePickup` |
| `Kit/Plates_And_Pans/` | `Plate`, `ScalesLock`, `CounterweightPair` |
| `Kit/Movers/` | `ClockMover`, `Lift`, `DropRamp`, `Rope`, `ImpactLever`, `Winch` |
| `Kit/Breakables/` | `Pot`, `CrackedWall`, `WoodBlock` |
| `Kit/Hazards/` | `LightningField`, `MagnetZone` |
| `Kit/Stations/` | `Anvil`, `HammerStand`, `WeaponRack`, `WeaponRack_NoOrb`, `WeaponRack_Start` |
| `Kit/Markers/` | `RoomVolume`, `SpawnPoint`, `PatrolRoute` |
| `Kit/Signs_And_Lights/` | `Sign`, `Torch` |
| `Prefabs/Enemies/` | `Goblin`, `GoblinSleeper`, `GoblinPorter`, `GoblinBoss` |
| `Prefabs/Rooms/Pieces/` | the Room Shape Builder's blocks: `WallSegment`, `DoorwayWallSegment`, `DiscFloor`, `DiscRoof`, `Floor`, `Roof`, `Wall`, `Ledge`, `Step`, `DoorwayWall` |
| `Prefabs/Rooms/Labyrinth/` | `LabyrinthRoom` + `_Start` `_BadEnd` `_Exit`, and the five ready-made shapes (see `docs/LABYRINTH.md` section 12) |

**Inventory, not dead wood.** Most of these are in **no build scene today**: the whole
`Kit/` set bar the doors, racks, signs, torches, volumes and spawn points used by the
tutorial, plus `GoblinSleeper` / `GoblinPorter` / `GoblinBoss` and the five room shapes.
They are kept on purpose, for building the labyrinth's real puzzle rooms. They all still
compile against the networked `WorldAuthority` (`KIT_STATE`, see the netcode section at the
end of this file). The only pieces actually **removed** by the cleanup were the tower's:
`ShellPanel.prefab` (the tower's exterior shell) and `FloorActivator.cs` (the tower's
floor culler). See `docs/CLEANUP-AUDIT.md`.

## Rules that apply to every piece
- **Shared state goes through `WorldAuthority`** (`_Managers/WorldAuthority`): request -> validate -> apply ->
  C# event. Kit objects and views only react to the events. The tower-kit half of the class is in
  `WorldAuthorityKit.cs` (`partial class`).
- Every interactive piece implements `ISceneId`: give each instance a **unique non-zero `id`** and add it to the
  matching `WorldAuthority` array in the Inspector, or the authority cannot see it.
  Id blocks: 101+ weapons, 201+ home slots, 301+ rooms, 401+ doors (a MagicDoor's own `Door` too), 501+ enemies,
  601+ plates, 701+ keys, 801+ runes, 901+ anvils, 1001+ clock movers, 1101+ signs, 1201+ spawn points,
  1301+ magic doors, 1401+ magnets, 1501+ lifts,
  **1601+ ropes, 1701+ pots, 1801+ levers and winches, 1901+ counterweight pairs, 2001+ scales locks,
  2101+ lightning fields, 2201+ cracked walls, 2301+ porter gates**.
  `MassPan`, `DropRamp` and `PatrolRoute` have NO id: they belong to the piece that owns them.
- Prefabs cannot hold scene references: after placing one, wire `authority` / `clock` on the instance.
- Run **Pesky / Validate Open Scenes** after every scene edit (duplicate ids, empty references, layers, static
  flags, MagicDoor pairing, MagnetZone trigger, Lift not static). Mark a legitimately empty reference `[OptionalRef]`.
- Anything on a schedule is a pure function of `LevelClock.Ms`.
- Geometry: World layer, static, `P_World`. Moving platforms: World layer, NOT static, `P_Slick` surface.

## Existing pieces (brief)
| Piece | Rule | Tunables | Host-owned state | Place and wire |
|---|---|---|---|---|
| `Door.prefab` + variants `Door_AlwaysOpen` / `Door_RoomCleared` / `Door_Plate` / `Door_Key` / `Door_Sealed` | Solid World-layer panel (blocks weapons and souls). Opens when its `DoorCondition` is satisfied; opening LATCHES. | `Door.openOffset` (3.1,0,0), `slideSeconds` 0.8; `DoorCondition.mode`, `room`, `plate`, **`keyId`** | open flag | Root on the doorway's floor centre. Wire `authority`; `room` / `plate` / `keyId` by mode. Register in `doors`. |
| `DoorPrompt` (component, Trigger layer box in front of a door) | Sensor only: while a possessed weapon stands in it and the door is shut the HUD says what the door wants ("KEY 1  NEEDED"). | box size | none | Wire `authority`, `door`. |
| `Key.prefab` (`KeyPickup`) | Touch pickup on the Pickup layer (weapons only, never a free soul). **Keys are party inventory by key id (int), never lost**; several can be held. `DoorCondition.PartyHasKey` asks for one `keyId`. HUD lists them ("KEY 1   KEY 2"). | **`keyId`**, spin / bob | taken flag; party key set in `WorldAuthority` (`HasKey(id)`, `HeldKeys`) | Wire `authority`, set `keyId`. Register in `keys`. Zone1: Room3 key = key 1, `Door_Key` = key 1. |
| `Plate.prefab` (`PressurePlate`) | Sums the mass of slow weapons in its trigger; at the threshold it LATCHES. | `threshold` 10, `restSpeed` 1.5 | latched flag | Register in `plates`; point a `Door_Plate` at it. |
| `HammerStand.prefab`, `WeaponRack.prefab` (7 slots), `WeaponRack_NoOrb` (6), **`WeaponRack_Start`** (3: `Slot_1` Sword, `Slot_2` Dagger, `Slot_5` Mace) | `WeaponHomeSlot`s: a weapon is authored on its slot, is held kinematic there until first touched and respawns there 10 s after breaking. | `WeaponBody.respawnDelay` 10 | weapon HP / broken / modifiers | Place the weapon prefab under `Gameplay/Weapons`, wire `WeaponBody.homeSlot`, register in `weapons`. |
| `Anvil.prefab` (`AnvilStation`) | Out of combat, hold E 2 s inside the trigger = full HP. | `chargeSeconds` 2 | none (HP is on the weapon) | Register in `anvils`. |
| `RunePickup.prefab` | Touch = the `ModifierDef` attaches to THAT weapon (lost when it breaks). Can wait for a room to be cleared. | `modifier` | taken flag | Register in `runes`. |
| `ClockMover.prefab` | Kinematic platform between two authored points, pure function of the clock. Riders carried by `RiderCarry`. | `periodSeconds` 8, `phase`, `smooth`, rider box | none (clock only) | Wire `clock`, `pointA`, `pointB` (scene objects). |
| `RoomVolume.prefab` | Trigger box that owns a room's enemies; `Cleared` when they are all dead. Goblins only aggro inside their own room. | box | cleared (derived) | Register in `rooms`. |
| `Sign.prefab`, `SpawnPoint.prefab`, `Torch.prefab` | Sign: text board, no collider. **Its readable face is the sign's local −Z**: point the sign's +Z at the wall behind it / away from the reader. (Stage 4 fixed the prefab: `Label` was on the +Z side of the board while facing −Z, so every sign read through its own board. `Label` is now at local z **−0.06**.) SpawnPoint: soul spawn. Torch: point light range 9, intensity 4.5, no shadows — **at most 6 per room**. | text / light | none | — |
| `Assets/Prefabs/UI/HUD.prefab` (new this stage, made from Zone1's HUD object) | UI Toolkit HUD (`Assets/UI/Hud.uxml` + `Hud.uss`). Pure view. | hint strings, **`magicFlashSeconds` 0.15** | none | Under `_UI`; wire `authority`, `spawner`. |
| Rooms kit: `Floor`, `Wall`, `Ledge`, `Roof`, `Step`, `DoorwayWall` | Unit cubes scaled per instance; `DoorwayWall` cuts a 3 x 3.5 opening. | — | — | Under `<Room>/Geometry`, static, World. |

## WeaponDef tags — `Assets/Data/Weapons/*.asset`
Four bools the kit asks about: `bladed` (cuts rope, sticks in wood), `blunt` (smashes pots / cracked walls),
`metal` (magnets, lightning), `wooden`. Plus `tipAxis` (local grip-to-point direction, (0,0,1) for every prefab).

| Weapon | bladed | blunt | metal | wooden |
|---|---|---|---|---|
| Sword, Dagger | yes | | yes | |
| Mace, Hammer | | yes | yes | |
| Staff | | yes | | yes |
| Banana, Orb | | | | |

## MagicDoor — `MagicDoor.prefab` (variant of `Door.prefab`), `MagicDoor.cs`
**Rule.** A doorway with a glowing purple plane (`M_Magic`). A possessed weapon, a loose weapon or a free soul
whose centre crosses the plane **from the front** is moved to the twin door. Position offset, rotation, linear
and angular velocity and the orbit camera's yaw are all turned by `twin.rotation * Y180 * inverse(this.rotation)`,
so "forward into A" is "forward out of B" at the same speed: a launch carries straight through. Two-way.
0.15 s purple HUD flash for the local player. 0.5 s re-entry cooldown per traveller (covers every magic door).
The trajectory preview ends at the plane. Goblins are never moved (they are not travellers; the prefab carries a
Not Walkable `NavMeshModifierVolume` over its alcove so the NavMesh stays out of it).
It respects the normal door conditions: the prefab's own `Door` + `DoorCondition` (default AlwaysOpen) is the gate
— closed = solid panel, plane hidden, no travel. Only the door being ENTERED has to be open.
No trigger collider is used: every physics step the door compares each traveller's previous and current position
against its plane rectangle, so layers do not matter and a fast launch cannot tunnel past it.

**Tunables (prefab, `MagicDoor`).** `width` 2.9, `height` 3.45 (the opening), `exitClearance` 0.6 (centre comes out
at least this far in front of the twin's plane), `reentryCooldown` 0.5, `maxStep` 2 (a bigger jump in one tick is a
teleport, not a crossing). Flash length: `HudController.magicFlashSeconds`. Flash colour: `.magic-flash` in `Hud.uss`.

**Host-owned state.** None of its own (the gate's open flag is the `Door`'s). Traversal is
`WorldAuthority.RequestMagicDoorTraverse(door, weapon | soul)` -> `WeaponBody.Warp` / `PlayerSoul.Warp` -> event
`MagicDoorTraversed(MagicDoorTraversal)` (from, to, weapon, soul, fromPosition, toPosition, turn). Remote views must
SNAP on that event. `PlayerSoul` turns the camera (`OrbitCamera.Warp`) off the same event.

**Place and wire.** Root = floor level, centre of a 3 x 3.5 doorway, on the wall's centre line, **+Z pointing into
the room**, upright (yaw only), scale 1. The prefab brings its own frame and a solid alcove that reaches **2.0 m
behind the root**: keep that space free of other colliders (a long weapon's tip pokes through the plane before its
centre crosses). The room floor must reach the wall's outer face. Per instance: `MagicDoor.id` (1301+), `linkId`
(same number on both doors of the pair; it is the stable name of the link for later cross-scene pairing — with an
empty `twin` the door is matched to the registered door sharing its `linkId`), `twin` (each points at the other),
`authority`; `Door.id` (401+), `Door.authority`, and the `DoorCondition` mode / target. Register the `MagicDoor` in
`WorldAuthority.magicDoors` **and its `Door` in `doors`** (an unregistered door never opens, not even AlwaysOpen).

## Stick in wood — `WoodSurface.cs`, `WoodBlock.prefab`, logic in `WeaponBody`
**Rule.** `WoodSurface` is a marker: the colliders on that object and its children are wood. A **bladed** weapon
(possessed or loose) that hits wood at `stickMinSpeed` or more, point-first within `stickMaxAngleDeg` of straight
in, with the contact on the point side of its centre of mass, sticks: it goes kinematic at the contact pose (pushed
`stickSinkDepth` into the wood, backed out again when freed). While stuck it **counts as grounded**: launch allowed,
wall-jump counter reset. A launch, a goblin hit (`WeaponBody.Knockback`), Release (Q), a break, a teleport or the wood
being disabled frees it. A launch from a wooden WALL whose aim points into the wall rebounds like a wall jump (without
using one up); from a wooden floor / ceiling the into-surface part of the aim is dropped. Wood on a moving Rigidbody is
followed. After being freed it cannot re-stick for `stickRearmSeconds`.
**Added so that sticking is aimable:** an airborne bladed weapon now turns its point into its velocity like an arrow
(angular velocity only; the arc is untouched). Set `bladeAlignRate` to 0 to get the old free tumble back.

**Tunables (`Assets/Data/MovementTuning.asset`).** `stickMinSpeed` 6, `stickMaxAngleDeg` 60, `stickSinkDepth` 0.1,
`stickRearmSeconds` 0.3, `bladeAlignRate` 10, `bladeAlignDelay` 0.15, `bladeAlignMinSpeed` 3.
Per weapon: `WeaponDef.bladed`, `WeaponDef.tipAxis`. Per surface: `WoodSurface.stickable`.

**Host-owned state.** The stuck flag and pose live on the `WeaponBody`; `WorldAuthority` relays `WeaponStuck(weapon,
wood)` / `WeaponUnstuck(weapon)`.

**Place and wire.** `WoodBlock.prefab` is a unit cube (M_Wood, World, static, P_World, `WoodSurface`): scale it into a
beam, a plank wall or a post. Or add `WoodSurface` to any collider object (or its parent). Nothing to wire.

## MagnetZone — `MagnetZone.prefab`, `MagnetZone.cs`
**Rule.** A box volume that pulls **metal** weapons (possessed or loose) toward its `target`; the Staff, Banana, Orb,
souls and goblins are unaffected. `mode` TowardPoint = to the target's position (weapons gather under a magnet
block); TowardPlane = to the plane through the target with normal `target.up` (a strip: weapons rise to a height and
keep their place along it). Outside `holdDistance` the pull is an acceleration `pullAcceleration * lerp(1,
edgeStrength, (d / range) ^ falloffPower)`, capped at `maxPullSpeed`. Inside `holdDistance` a damped spring holds the
weapon on the target and cancels gravity, and the weapon **counts as grounded**, so it can launch off the magnet —
nothing can be stranded. For `launchReleaseSeconds` after a launch the magnet ignores that weapon, so the launch
flies its previewed arc; then it is caught again. (So a metal weapon crosses a pit under a magnet strip hop by hop;
the Staff cannot.)

**Tunables (prefab).** `pullAcceleration` 45 m/s^2 (gravity is 20; defaults give at least 27 anywhere in range, so
it always lifts), `range` 8, `edgeStrength` 0.6, `falloffPower` 1, `maxPullSpeed` 8, `holdDistance` 0.9,
`holdSpring` 40, `holdDamping` 12.6 (critical), `holdPlanarDamping` 3 (plane mode), `launchReleaseSeconds` 0.6,
`startsOn` true, `weaponMask`.

**Host-owned state.** On / off: `WorldAuthority.RequestSetMagnet(id, on)` -> `ApplyOn` -> `MagnetChanged`.

**Place and wire.** Prefab root = the ceiling fixing point: `Block` (3 x 0.5 x 3, M_Magnet, solid) hangs below it,
`Target` is 1.2 m under the block (keep it clear of geometry so a held weapon has room to launch), `OnGlow` shows
while on, the trigger `BoxCollider` (Trigger layer; it only defines the volume) is 5 x 8 x 5 reaching 8.5 m down —
resize it per room. Put it under `<Room>/Gameplay` (not static if it rides a mover). Set `id` (1401+), register in
`magnets`.

## Lift — `Lift.prefab`, `Lift.cs`, `RiderCarry.cs`
**Rule.** A platform moving **vertically** between N authored stops as a pure function of `LevelClock.Ms` plus two
host-owned values (on / off, and the clock time it was switched on). Off = parked at its **top** stop. On = dwell at
the top, then top, ..., 1, 0, 1, ..., top-1, repeat; each leg is `dwellSeconds` + distance / `travelSpeed`, eased.
Riders: the ClockMover rider fix, now shared in `RiderCarry` (zero-friction `P_Slick` surface + ONE explicit
`Rigidbody.position` delta per physics step; `ClockMover` uses the same helper). Because nothing grips on P_Slick,
grounded weapons on the lift are braked horizontally (`riderBrake`) so a landing weapon settles; a weapon that
launched less than `brakeLaunchGrace` ago is never braked, so a launch off the lift keeps its arc.
`Lift.DwellingAt` gives the stop index while waiting (-1 while travelling) for later stop doors.

**Tunables (prefab, `Platform`).** `dwellSeconds` 4, `travelSpeed` 3, `smooth` true, `startsOn` false, rider box
(0,0.75,0) / (3.2,1.4,3.2), `riderMask` Weapon|Enemy, `riderBrake` 14, `brakeLaunchGrace` 0.75.

**Host-owned state.** `IsOn`, `StartMs`: `WorldAuthority.RequestSetLift(id, on)` -> `ApplyPower(on, clock.Ms)` ->
`LiftChanged`. The Winch (later stage) calls it once; it latches on. Switching it off snaps it to the top stop
(riders are not dragged along a snap).

**Place and wire.** Prefab root `Lift` = a fixed anchor at the shaft's centre; child `Platform` (kinematic Rigidbody +
`Lift`, `Surface` 3 x 0.5 x 3 with the walking surface at the Platform origin); `Stops/Stop_0..` are authored scene
objects, **bottom to top, only their height is used** — add more stops per instance and list them in `stops`. Sink
the bottom stop's slab into a 0.5 m pit for a flush floor, or put `Stop_0` 0.5 m above the floor (a 0.5 m hop: every
weapon clears it, Hammer apex at 45 deg = 1.01 m). Wire `clock`, set `id` (1501+), register in `lifts`.

## Test placement
`Assets/Scenes/Dev/FeelBox.unity` -> `Environment/DevBox/Gameplay/KitTest`: `MagicDoor_A` (13,-0.05,-3, faces -X) <->
`MagicDoor_B` (8,-0.05,-13, faces +Z), `WoodBlock_Wall` and `WoodBlock_Beam` on the west wall, `MagnetZone` hanging at
(10,7.5,10), `Lift` at (-11,0.45,-12) with `startsOn` = true and a 6 m top stop. FeelBox now has a `WorldAuthority`
and the HUD so these work there.

## Room shell pieces — `Assets/Prefabs/Rooms/Pieces`
The blocks the Room Shape Builder assembles a room out of (they were added for the tower build and
are still exactly what the labyrinth's room shapes are made of). All are World layer, `P_World`,
static, and go under `<Room>/Geometry`.

| Piece | Rule | Tunables (per instance) | Host state | Place and wire |
|---|---|---|---|---|
| `WallSegment.prefab` | A straight unit cube wall. Flat faces, so wall rebounds stay readable. | scale = (length, height, 0.5); rotate by yaw so **local +Z is the inward normal** | none | Centre it on the wall's centre line at floor + height/2. |
| `DoorwayWallSegment.prefab` | The same wall with a **3.0 x 3.5** opening. Root sits at FLOOR level, root scale stays 1. | children `Left` / `Right` / `Lintel` are resized per instance: side width = L/2 − 1.5, lintel from y 3.5 to the wall top | none | Root at the doorway's floor centre, +Z into the room. A MagicDoor or Door goes at exactly the same transform. |
| `DiscFloor.prefab` | Flattened 20-sided cylinder mesh with a **MeshCollider** — the floor of every round-ish room. | scale = (2r, 0.25, 2r) ⇒ radius r, 0.5 thick; centre y = floorTop − 0.25 | none | Use r = across/2 + 0.5 so the floor reaches past the wall's outer face (MagicDoors need that). |
| `DiscRoof.prefab` | The same disc with `NavMeshModifier.ignoreFromBuild = true`, so **roofs are excluded from the NavMesh**. Not Navigation-static. | scale as DiscFloor; centre y = wallTop + 0.25 | none | One per round-ish room. |
| ~~`ShellPanel.prefab`~~ | **Removed 2026-09-20**: it was the tower's exterior shell (a collider-less wall cube), and the labyrinth has no exterior. |

Walls are 0.5 m thick and centred on the polygon edge; each segment is built `edgeLength + 0.5` long so
corners mitre. Non-round rooms (Long gallery, L-shape, Wedge) use the old box `Floor` / `Roof` prefabs
instead of the discs; Ring and Crescent floors are an **annulus of 12 overlapping box segments**.

## Room Shape Builder — `Assets/Scripts/Editor/RoomShapeBuilder.cs` (`Pesky.Editor`)
**Rule.** An editor tool that AUTHORS a grey-box room in the open scene out of the prefabs above —
nothing is generated at runtime. Menu **Pesky / Rooms / Room Shape Builder** opens a window with the
spec fields and two buttons ("Describe edges" logs the polygon's edge table; "Build room in the open
scene" builds it and logs where the doorways landed). The same work can be driven from code with
`RoomShapeBuilder.Build(RoomShapeSpec, out List<Doorway>)`.

Every shape is a closed polygon of straight walls; the inward normal is derived from the polygon's
winding, so L-shapes and crescents work like the regular ones.

| Shape | What it is |
|---|---|
| `Round` | 12-sided |
| `Octagon` | 8-sided |
| `Hexagon` | 6-sided |
| `TallShaft` | 8-sided, meant for a big `height` |
| `Wedge` | trapezoid: `inner` narrow end at −Z, `across` wide end at +Z, `depth` deep |
| `Ring` | 12-sided outer wall plus an **8-sided core** (`inner`), annulus floor and roof; `coreFloor` caps the core |
| `LongGallery` | rectangle `across` x `depth`, long in Z |
| `LShape` | `across` x `depth` with two arms `inner` wide |
| `Crescent` | half annulus, outer `across` / inner `inner`, a cap at each end (edges 8 and 17) |

**Spec fields.** `groupName`, `signText`, `roomId` / `signId` / `spawnId`, `shape`, `floorCentre` (the
floor's TOP surface at the room centre), `across`, `depth`, `height` (clear, floor top to wall top),
`inner`, `yaw`, `doorwayAngles` (compass degrees, 0 = +Z, 90 = +X) **or** `doorwayEdges` (explicit
indices, which win), `innerDoorwayAngles` (Ring core), `buildFloor`, `buildRoof`, `coreFloor`,
`torches` (clamped to 6), `gameplayObjects` (false = a pure shell: no RoomVolume, no SpawnPoint),
`createGroups` (false = build only a geometry section into `parentPath`), `parentPath`, `sectionName`.

**What one call makes.** `Environment/<groupName>/{Geometry,Gameplay,Lighting,Spawns}`; the geometry in
`Geometry/<sectionName>`; a `RoomVolume` and a `SpawnPoint` and a `Sign` (id + name) when
`gameplayObjects`; torches on the wall midpoints in `Lighting`. It returns one `Doorway` per opening
(edge index, floor-centre position, yaw whose **+Z points into the room**) — feed those straight into a
MagicDoor's transform.

**Gotchas.** A doorway needs its wall segment to be at least 4.0 m long (3 m opening + jambs), or the
builder logs a warning and makes a solid wall instead: a 12-sided polygon under about 16 m across has
chords that are too short. Call it twice with `createGroups = false` to stack wall sections at different
heights in one room (that is how the lift shaft gets a doorway at y 0 and another at y 60).

## FloorActivator — REMOVED 2026-09-20
It culled the tower's stacked floors, which only `Zone1` / `MainTower` needed. The labyrinth's rooms
sit 200 m apart and are entered only through teleport doors, so nothing can see two of them at once
and there is nothing to cull. The script and the tower scenes are in the system trash; the old wiring
table is in `docs/archive/CASTLE-LAYOUT-BUILD.md` section 4 if it is ever wanted back.

## Puzzle kit (stage 3) — `Assets/Prefabs/Kit`, scripts in `Assets/Scripts/Game`
Everything below goes through `WorldAuthority` (the puzzle half is `WorldAuthorityPuzzles.cs`, `partial class`),
**latches where it says it latches**, and is registered in the matching Inspector array or it does nothing.
A piece that reacts to being HIT carries its own **solid (non-trigger) collider on the same GameObject as the
script** — a static collider is the one that receives the collision message for the weapon that hit it. The
scene validator enforces that.

### Rope + DropRamp — `Rope.prefab`, `DropRamp.prefab`
**Rule.** `Rope` is a taut cord with a capsule collider. A **bladed** weapon hitting it at `minCutSpeed`
(5 m/s) or more cuts it; blunt weapons bounce off, which is the whole puzzle (a Mace must soul-swap for a
blade). Cutting LATCHES: the rope hides its taut mesh, shows its cut ends, disables its collider and records
the LevelClock time. `DropRamp` has **no host state of its own** — its pose is a pure function of
(`rope.IsCut`, `rope.CutMs`, `clock.Ms`), so every client draws the same fall with no message. It is a
kinematic Rigidbody moved with MovePosition / MoveRotation, so it shoves rather than tunnels, and it does
**not** carry riders (nothing is standing on a ramp that is still up).

| Tunable | Where | Default |
|---|---|---|
| `requiresBladed`, `minCutSpeed` | `Rope.prefab` | true, 5 |
| `mode` (Swing / Drop), `swingDegrees`, `dropOffset`, `fallSeconds`, `smooth` | `DropRamp.prefab` | Swing, **40**, (0,-4,0), 1.2, true |

**Place and wire.** Ramp root = the **hinge**, with the deck child lying along **+Z** in front of it; a
**positive** `swingDegrees` lowers that far end. For a deck `L` long, the far end drops `L*sin(a)` and pulls
back to `L*cos(a)`, so a hinge at height `h` lands on the floor when `h = L*sin(a)` — 8 m deck at 40 deg needs
`h = 5.14`. Hang the Rope at the deck's held far end. Per instance: `Rope.id` (1601+), `authority`, `clock`,
`ramp`; `DropRamp.rope`, `clock`. Register the Rope in `ropes`. Neither may be marked static.

### Pot — `Pot.prefab`
**Rule.** Only a **blunt** weapon at `minSmashSpeed` (5 m/s) or more smashes it (Mace, Hammer, Staff). Bladed
weapons and the Banana bounce off. Smashing LATCHES: the pot hides, its shards appear, and whatever was
hiding inside is switched on. The `Contents` child is an **inactive** parent: drop an `ImpactLever` or a
`Key.prefab` under it, or leave it empty for a pot that hides nothing.
**Tunables.** `requiresBlunt` true, `minSmashSpeed` 5. **Host state.** smashed flag.
**Place and wire.** Under `<Room>/Gameplay`, World layer, NOT static. Set `id` (1701+), `authority`; register
in `pots`. A key or lever inside keeps its own id and its own registration.

### ImpactLever and Winch — `ImpactLever.prefab`, `Winch.prefab` (variant)
**Rule.** ANY weapon impact at `minSpeed` (4 m/s) or more flips it — bladed, blunt, metal, wooden, it does not
care. `latching = false` toggles on every hit; `latching = true` turns on once, for good. Wire `lift` and it
becomes the **Winch**: turning on calls `RequestSetLift(thatLift, true)`. `rearmSeconds` 0.5 means one landing
is one flip. A door follows a lever through the new `DoorCondition.Mode.LeverOn` (`Door_Lever.prefab`).
**Tunables.** `minSpeed` 4, `latching` true, `startOn` false, `rearmSeconds` 0.5, handle `offAngle` -35 /
`onAngle` 35 / `swingSeconds` 0.25. **Host state.** on/off.
**Place and wire.** World layer, NOT static. `id` (1801+), `authority`, and `lift` for a winch; register in
`levers`. **Room 19's winch:** wire its `lift` to the Lift with id 1501 and leave `latching` on.

### MassPan — `MassPan` (no prefab of its own; it is the pan inside the two scales prefabs)
**Rule.** A kinematic pan that knows the weapon mass RESTING on it (slower than `restSpeed` 1.5) and can be
told to move to a height. Riders use the Lift fix (`RiderCarry`): zero-friction `P_Slick` surface, one
explicit delta per physics step, then grounded riders are braked (`riderBrake` 14) so a landing weapon
settles; a weapon that launched less than `brakeLaunchGrace` 0.75 s ago is never braked.
Structure: pan ROOT on the **Trigger** layer with a kinematic Rigidbody and a trigger BoxCollider (the
sensing volume), child `Surface` on the **World** layer with the solid collider. NOT static. No id.

### CounterweightPair — `CounterweightPair.prefab`
**Rule.** Two linked pans. The heavier pan sinks by `travel` and the lighter one rises by the same; inside
`deadzone` (0.5 kg) they level out. The host owns ONE number, the target offset (-1, 0 or +1), and the pans'
heights are a pure function of (startOffset, targetOffset, startMs) and the clock. Park the Hammer on one pan
and fly as a soul onto the weapon that went up — that is Room 13.
**Tunables (prefab).** `travel` 3, `deadzone` 0.5, `easeSpeed` 1.5 m/s, `smooth` true. Pans authored at
local y 3, frame 9.2 m wide.
**Geometry rule.** Author the pans at `y = travel` above the root so the LOADED pan comes to rest at floor
level; nothing can then be stranded on a pan. A weapon must also be able to reach a pan at rest: apex
`(v sin a)^2 / 2g`, g = 20, at 80 deg gives Dagger 4.75, Banana 4.10, Sword 3.49, Staff 2.93, Orb / Mace 2.42,
Hammer 1.96 m. **Never require a weapon to land on a pan higher than its own apex less 20 percent.**
**Host state.** target offset + the clock time it changed.
**Place and wire.** `id` (1901+), `authority`, `clock`; register in `counterweights`.

### ScalesLock — `ScalesLock.prefab`
**Rule.** Three pans, each asking for a weight class, all at the same moment. With the slice-1 masses:
light `<= 1.5` = Banana 0.5 / Dagger 1; medium `2..5` = Staff 2.5 / Sword 3 / Orb 4; heavy `>= 8` = Mace 8 /
Hammer 14. The moment all three are right it **LATCHES**, so the weapons can be taken back afterwards and the
door stays open (`Door_Scales.prefab`, `DoorCondition.Mode.ScalesSatisfied`). A pan sinks with its load and
turns green when it is happy.
**Tunables.** `lightMax` 1.5, `mediumMin` 2, `mediumMax` 5, `heavyMin` 8, `sinkDepth` 0.35. Pans authored at
y 1.0 (Hammer apex 1.96 clears that with margin, so every weapon can be launched onto one).
**Host state.** latched flag. **Place and wire.** `id` (2001+), `authority`; register in `scales`.
Room 18 wants Dagger (light), Sword (medium), Hammer (heavy).

### LightningField — `LightningField.prefab`
**Rule.** A trigger box over an open-roof area. Strikes on a schedule that is a **pure function of
LevelClock.Ms** (`periodSeconds` 6, `phaseSeconds`), with a `telegraphSeconds` 1 ground glow under the victim
first. It hits only the **highest METAL weapon inside the box**, possessed or loose — so crossing as the
wooden Staff is one answer and leaving a metal weapon standing as the lightning rod is the other. With no
metal weapon inside, nothing happens at all. Damage goes through
`WorldAuthority.RequestLightningStrike` -> `RequestDamageWeapon`.
**Tunables.** `periodSeconds` 6, `phaseSeconds` 0, `telegraphSeconds` 1, `strikeDamage` **40**, `boltSeconds`
0.25, `boltHeight` 6, trigger box 12 x 10 x 12 centred (0,5,0). At 40 a Dagger (70 HP) survives one strike and
breaks on the second; Sword 100 and Mace 120 take three, Hammer 140 four.
**Host state.** None of its own - the schedule is the clock and the damage is the weapon's HP.
**Place and wire.** Root at FLOOR level in the open-roof room, **Trigger** layer, resize the box to the room.
`id` (2101+), `authority`, `clock`; register in `lightningFields`.

### CrackedWall — `CrackedWall.prefab`
**Rule.** Breaks once, LATCHED, when a weapon of `minMass` 8 or more hits it at `minSpeed` 6 m/s or more —
the Mace (8) or the Hammer (14). Anything lighter or slower only puffs (a short tint flash). Every weapon's
horizontal launch speed at minimum lift is `v cos 15`: Mace 9.66 and Hammer 8.69, both clear of 6 with the 20
percent margin.
**Tunables.** `minMass` 8, `minSpeed` 6, `puffSeconds` 0.18. Slab authored 4 x 4 x 0.5 — **resize the root
BoxCollider `size` and the `Slab` child scale together**.
**Host state.** broken flag.
**Place and wire.** Under `<Room>/Gameplay` (not `/Geometry`), World layer, **NOT static**, root at floor
level with +Z through the wall. `id` (2201+), `authority`; register in `crackedWalls`.
**Zone1 TODO (next stage):** it replaces `Room16_Barracks/Geometry/Shell/CrackedWall_Placeholder`, which
stands in front of `MagicDoor_16_to_D`. That placeholder is still there — this stage did not furnish rooms.

### PorterGate — `PorterGate.prefab`
**Rule.** A solid panel that blocks weapons and souls and slides open only while one of its `porters` is
within `openRange` **and carrying something**. It does NOT latch: it shuts behind the porter. That is the
Carry Room: you cannot open it, so you play dead and the porter opens it for itself.
**Caveat (grey-box).** Goblins move with a NavMeshAgent and are not stopped by the panel, so any goblin can
walk through a shut gate. Keep a porter gate on the porter's route and out of a fight.
**Tunables.** `openRange` 4, `requireCarrying` true, `openOffset` (0,3.6,0), `slideSeconds` 0.6.
**Host state.** open flag. **Place and wire.** NOT static. `id` (2301+), `authority`, `porters`; register in
`porterGates`.

### PatrolRoute — `PatrolRoute.prefab`
Pure data plus a gizmo: `points` (child transforms, in order), `mode` Loop / PingPong, `pauseSeconds` 1 (the
pause is the window you sneak through). The goblin owns the index it is on. No id, nothing to register.

## Goblin variants (stage 3) — `Assets/Prefabs/Enemies`
`GoblinBrain` is now `partial`; the variants live in `GoblinBrainRoles.cs`. New `EnemyDef` blocks: view cone,
patrol, sleep, porter, shield boss.

| Piece | Rule | Tunables (EnemyDef unless said) | Host state | Place and wire |
|---|---|---|---|---|
| **View cone (all goblins)** | A goblin only sees inside a **110 deg** cone about its forward axis, plus anything within `closeSightRange`. Line of sight still has to be clear. This is what makes "move only when nobody looks" a puzzle. | `viewConeDeg` 110, `closeSightRange` 1.5 | none | Nothing to wire. The cone is drawn as a gizmo when the goblin is selected. |
| **Patrol** | Wire a `PatrolRoute` on the goblin and it walks the route instead of wandering, at `patrolSpeed`, pausing `PatrolRoute.pauseSeconds` at each point. It goes back to the route after a fight. | `patrolSpeed` 1.6, `patrolArrive` 0.6 | none | `GoblinBrain.patrol`. |
| **Sleep** — `GoblinSleeper.prefab` | `startAsleep`: it perceives **nothing** (no cone, no room aggro, a RoomVolume entry does not wake it) and shows a "z". It wakes on an ANIMATE weapon within `wakeAnimateRange` **4 m** or a weapon impact of `wakeImpactSpeed` 4 m/s within `wakeImpactRange` **8 m**, and then wakes sleeping goblins within `wakeNeighbourRadius` 6 m. Those neighbours do **not** wake further neighbours, so there is no chain across the room. | `wakeAnimateRange` 4, `wakeImpactRange` 8, `wakeImpactSpeed` 4, `wakeNeighbourRadius` 6 | asleep / awake | `startAsleep` on the instance, and its `room` — a sleeper with no RoomVolume can never hear anything (the validator says so). |
| **Porter** — `GoblinPorter.prefab`, def `Assets/Data/Enemies/GoblinPorter.asset` | `role = Porter`. A weapon that has been INANIMATE for `porterInanimateSeconds` **2 s**, seen within `porterNoticeRange` 8 m, is walked to (Fetch), picked up (`porterReach` 2 m), carried along its PatrolRoute (Carry) through its PorterGate, and set down on `dropPoint` after `porterPlaceSeconds` (Place). It will not pick anything up again for `porterCooldownSeconds` 5 s, and never re-fetches a weapon already sitting on its stand. **Turn ANIMATE in its hands and it drops you and turns hostile.** While carried the weapon keeps gravity off, counts as GROUNDED (so a launch is allowed) and its velocity is never written back, so the launch takes. | `porterReach` 2, `porterInanimateSeconds` 2, `porterNoticeRange` 8, `porterCarrySpeed` 2.2, `porterPlaceSeconds` 0.6, `porterCooldownSeconds` 5 | the carried weapon | `role`, `carrySocket` (prefab child at (0,2.05,0.35), above the capsule so the weapon does not fight the body), `dropPoint` (a scene Transform beyond the gate — a `HammerStand` slot works), `patrol`, `room`, `authority`. The validator insists on all three. |
| **Shield boss** — `GoblinBoss.prefab`, def `Assets/Data/Enemies/GoblinBoss.asset` | `role = ShieldBoss`, body 1.8x. While `shieldHp > 0` **only a weapon of `shieldMinMass` 8 or more touches it at all** (Mace, Hammer) — everything else is turned away for 0 damage and only angers it. When the shield breaks the shield plate and its bar vanish and the body takes normal damage with `bladedDamageBonus` **1.5x** for blades. Same silver-arc telegraph, slower and wider. Two world-space bars: body and shield. | `shieldMaxHp` 120, `shieldMinMass` 8, `bladedDamageBonus` 1.5; body 140 HP, `moveSpeed` 2.2, `windupSeconds` 0.9, `strikeSeconds` 0.45, `strikeHalfAngleDeg` 100, `strikeReach` 3.4, `attackDamage` 30, `attackCooldown` 1.8, `viewConeDeg` 140 | shield HP, body HP | `role`, `def`, `shieldRenderer`, `shieldBar`, `shieldBarFill` (all wired in the prefab), plus `authority` / `room` per instance. Ordinary goblins keep `shieldMaxHp` 0 and `bladedDamageBonus` 1, so the damage arithmetic is unchanged for them. |

**Damage path (changed).** `WorldAuthority.RequestHitEnemy` now asks `GoblinBrain.ResolveDamage(weapon, raw)`
for a `HitOutcome` (damage, newHp, newShieldHp, hitShield, shieldBroke, blocked) and applies it with
`ApplyHit(outcome, knock, weapon)`. New event `WorldAuthority.EnemyShieldBroken(GoblinBrain)`.
`WeaponBody` gained `IsCarried` / `SetCarried` / `CarryTo`, and `LastImpactTime` / `LastImpactSpeed` (what a
sleeping goblin listens for).

## Test placement (stage 3) — `Assets/Scenes/Dev/FeelBox.unity`
All under `Environment/DevBox/Gameplay/KitTest` in the 30 x 30 dev box, wired to FeelBox's own
`WorldAuthority` and `LevelClock` and registered. **Nothing here has been run.**

| Object | Where | Notes |
|---|---|---|
| `DropRamp` + `Rope` | hinge (-6,5,4), rope (-6,5,11.5) | 8 m deck, `swingDegrees` 40; cut the rope with the Sword or the Dagger and the far end lands at (-6,0,10.1). |
| `Pot` (id 1701) with `Contents/ImpactLever` (id 1801, non-latching) | (3,0,10) | Smash it with the Mace, Hammer or Staff; the lever appears and can then be flipped. |
| `Winch` (id 1802) | (-7,0,-11) | Wired to the existing `Lift` (id 1501), whose `startsOn` is now **false** — hit the winch to start the lift. |
| `CounterweightPair` (id 1901) | (7,0,7), `travel` **1.5**, pans at y 1.5 | Every weapon clears a 1.5 m pan (Hammer apex 1.96). |
| `ScalesLock` (id 2001) | (-6,0,-6) | Pans at y 1.0: light / medium / heavy left to right. |
| `LightningField` (id 2101) | (0,0,-10) | 12 x 10 x 12; 6 s period, 1 s glow, 40 damage. |
| `CrackedWall` (id 2201) | (-11,0,1) | Only the Mace or the Hammer at 6 m/s or more. |

The goblin variants are **not** placed anywhere: FeelBox has no NavMeshSurface and no RoomVolume, and
patrol / sleep / porter all need both. They go in when the rooms are furnished.

## Lift — placement notes added by the tower build
A long shaft needs the travel speed raised on the instance: Zone1's lift runs 192 m, so `travelSpeed` is
**30** there (the prefab default 3 would be 64 s a leg). Scale `Platform/Surface` and `riderBoxSize`
together to fill a wide shaft — Zone1 uses a 7.4 x 7.4 platform in a 12 m 8-sided shaft, leaving a
1.6 m gap at the sides that every weapon clears at minimum lift (Hammer 2.03 m). While the lift is off it
parks at its TOP stop, which plugs the shaft head: useful, because the shaft head is the middle of a Ring
room with no floor.

## Furnishing notes (stage 4) — how the kit was used in rooms 6-12, a and b

No new scripts or prefabs this stage. These are the placement rules learned while furnishing, and they
belong with the pieces they are about.

### DropRamp as a **drop-bridge** (not a swing ramp)
`DropRamp.mode = Drop` turns the ramp into a deck that falls straight down onto a gap and becomes a flat
bridge. That is much friendlier than the Swing ramp, which lands as a 30-45 degree slope you have to
launch up hop by hop.
* Author the deck at its HELD height; `dropOffset` is the local-space move to the LANDED pose
  (`ApplyPose` lerps home -> home + rot*dropOffset), so the held pose is what you place in the Editor.
* `Deck` is a unit cube child at local z = length/2 with scale (width, 0.4, length). Its top surface is
  0.2 above the root, so to land flush with a floor at `y` the landed root must be at `y - 0.2`.
* Size the deck exactly lip to lip, so the bridge fills the gap with no step at either end.
* **Hold the deck out of reach.** A held deck is a solid collider: if a weapon can land on it, it walks
  across and skips the puzzle. 5.0 m above the floor beats every weapon (Dagger apex at 80 deg = 4.75 m).

### Rope as a floor-to-deck stay
The `Rope` root is the BOTTOM of the cord and the cord runs 3 m **up** from it (scale the root in y to
lengthen it: y 1.6 = a 4.8 m cord). A rope hung from the deck downwards is therefore the only way to put
the cord where a weapon can hit it: **put the root on the floor under the deck's end and scale it up to
the deck.** A cord that starts at the deck's height cannot be cut at all, because a weapon's speed at
its own apex is only `v cos(lift)`.
Cut speed check: a weapon `h` above its launch point is doing `sqrt(v^2 - 2gh)`; the Rope wants 5 m/s, so
the Sword can cut up to h = 2.98 m and the Dagger up to h = 4.0 m. Cut the cord low, near the floor.

### PorterGate needs a NavMeshModifier
The gate panel is a solid collider, so the bake carves the NavMesh away under a shut gate and the porter
can no longer path through its own gate. **Add `NavMeshModifier` with `ignoreFromBuild` = true to the
gate's `Panel` child** (Room 10 does), then re-bake. The panel still blocks weapons and souls.
Make the wall around it at least `openOffset.y` + opening height tall (Room 10: 7.5 m wall, 3 x 3.5
opening, `openOffset` (0,3.6,0)) so the open panel hides inside the wall instead of poking out of it.
Because the gate does not latch, the far side needs its own way back: Room 10 uses a 4-step staircase
(1.5 m risers) up the far face of the wall, and a sheer face on the near side, so the return is one-way.

### Porter wiring recipe (Room 10)
`GoblinPorter` instance: `authority`, `room` (its RoomVolume), `patrol` (a `PatrolRoute` whose points run
from its noticing ground, through the gate, to the stand), `dropPoint` = the **`Slot` child of a
`HammerStand`** placed beyond the gate. `TickCarry` places the weapon as soon as it is within
`porterReach` (2 m, horizontal) of `dropPoint`, so the last route point must sit inside that. Register the
porter in `WorldAuthority.enemies` AND in its `RoomVolume.enemies`, and list it in `PorterGate.porters`.

### MagnetZone as a crossing (Room 11)
`mode = TowardPlane` with the `Target` a few metres over the walking level makes a strip a metal weapon
skates along: it is lifted to the plane, launches, flies free for `launchReleaseSeconds` 0.6, and is
caught again. Horizontal progress per hop is `v cos(lift) * 0.6` — the Hammer at minimum lift makes 5.2 m,
so a 12 m crossing is 3 hops. Scale `Block` and `OnGlow` in z into a rail and resize the root's trigger
`BoxCollider` to the crossing only (Room 11: centre (0,-4,0), size 9 x 12 x 12 on a root at y 92.5), so
metal weapons are not yanked off the landings at either end.

### Wooden posts as a climb (Room 12)
Four `WoodBlock` posts scaled (0.9, height, 0.9) standing on a ring of radius 3.2 around a shaft: a bladed
weapon sticks into a post's side, counts as grounded, and launches to the next post 4.53 m away and about
2 m higher. Do **not** use horizontal beams you stick to from below: a launch off a wooden ceiling drops
the into-surface part of the aim, so you cannot climb off one. Vertical faces rebound like a wall jump.
Reach check at +2.0 m rise, `x_max = (v/g) * sqrt(v^2 - 2gh)`: Dagger 7.54 m (1.66x the 4.53 needed),
Sword only 4.80 m — so a post climb on this spacing is **Dagger-only**, and the Dagger's home slot goes at
the bottom of the shaft.

### Moving an authored doorway up a wall (Room 12)
A `DoorwayWallSegment` cuts its opening at the root (floor) height. To put the opening higher without
rebuilding the wall: keep `Left` / `Right`, resize `Lintel` into the **sill** below the new opening, and add
two `WallSegment` children — one filling the old opening, one above the new one. Then move the `MagicDoor`
(and its `Door`) to the new sill height. A magic door's twin does **not** have to be at the same height.

### Weapon home slot recipe
`HammerStand` is the one-slot stand: place it, give its `Slot` child a `WeaponHomeSlot.id`, then place the
weapon prefab **at the Slot's world position and rotation**, parented under the room's `Gameplay`, and wire
`WeaponBody.homeSlot` to that slot. Register the weapon in `WorldAuthority.weapons`. A stand with no weapon
on it is a legal drop point (Room 10's porter stand).

---

## Netcode rules for kit pieces (netcode stage 2, 2026-09-20). IMPLEMENTED, UNTESTED.

No new kit pieces were added. What changed is how every existing piece's shared
state travels; the full wire table is in `docs/NETCODE-STATUS.md` section S2.3.

- A piece still never changes shared state itself. It calls a
  `WorldAuthority.Request*` / `Report*`, and it changes only in its `Apply*`
  method, which is now called from **one** place: `WorldAuthority.ApplyKit`
  (`WorldAuthorityNet.cs`), on every peer, when the session delivers a
  **KIT_STATE** event `(kind, pieceId, state, actor weapon, value, clockMs)`.
- Two families. **Contact pieces** (key, rune, rope, pot, lever, cracked wall,
  anvil): the peer that *simulates* the touching weapon reports it
  (`WorldAuthority.Simulates`: the player holding it, or the host while it is
  loose). The host publishes at once; a client sends KIT_REQ and the host
  re-checks the piece's own rule (`CanCut`, `CanSmash`, `CanFlip`, `CanBreak`,
  taken / available) against its own scene, within 8 m. **Host pieces** (door,
  plate, scales, counterweight, porter gate, magnet, lift, lightning, porter
  carry): only the host's copy reports; on a client the `Report*` call does
  nothing and the piece waits for KIT_STATE.
- Anything timed carries the host's `LevelClock.Ms` in `clockMs` (rope cut,
  lift start, counterweight target), and `LevelClock` follows the session's
  room clock, so a mover is the same function of the same number on every peer
  and a late joiner sees it where it really is.
- **Adding a piece:** give it a `KitKind` value (append only, `Protocol/GameEnums.cs`),
  a case in `ApplyKit` that is **idempotent** (a piece already in that state
  returns without raising its event, because a snapshot is replayed through the
  same method with `live: false`), a case in `ValidateKit` if clients may ask
  for it, and a `FillKit` case if it needs `value` or `clockMs`. Bump
  `Wire.ProtocolVersion`.
- Scene ids are u16 on the wire: keep every `ISceneId` in 0..65534 and unique
  per kind.

### Is it net-synced yet? (one row per kit piece)

Every row below already travels on **KIT_STATE**; "family" says who is allowed to report the
change. Anything not listed has no shared state at all, so there is nothing to sync.

| Piece | `KitKind` | Family | Notes |
|---|---|---|---|
| `Door` (+ every variant) | `Door` 0 | host | the open flag; opening latches |
| `Plate` | `Plate` 1 | host | latched flag |
| `Key` | `Key` 2 | contact | taken flag + the party key set |
| `RunePickup` | `Rune` 3 | contact | taken flag |
| `Anvil` | `Anvil` 4 | contact | the repair, not the HP (that is WEAPON_STATE) |
| `MagnetZone` | `Magnet` 5 | host | on / off |
| `Lift` | `Lift` 6 | host | on / off + the host clock ms it started |
| `Rope` | `Rope` 7 | contact | cut flag + cut clock ms. `DropRamp` has **no state of its own** |
| `Pot` | `Pot` 8 | contact | smashed flag |
| `ImpactLever` / `Winch` | `Lever` 9 | contact | on / off |
| `CounterweightPair` | `Counterweight` 10 | host | target offset + clock ms, compared in whole mm |
| `ScalesLock` | `Scales` 11 | host | latched flag |
| `CrackedWall` | `CrackedWall` 12 | contact | broken flag |
| `PorterGate` | `PorterGate` 13 | host | open flag, does not latch |
| `LightningField` | `Lightning` 14 | host | cosmetic only: the schedule is the clock, the damage is a weapon message |
| goblin porter carry | `PorterCarry` 15 | host | which weapon is in its hands |
| `MagicDoor` / `MagicDoor_Grid` | — | host | traversal is its own path (`RequestMagicDoorTraverse` -> `MagicDoorTraversed`), not KIT_STATE |
| `ClockMover` | — | none | a pure function of `LevelClock.Ms`; identical on every peer with no message |
| `WeaponRack` / `HammerStand` / `WoodBlock` / `RoomVolume` / `SpawnPoint` / `Sign` / `Torch` / `PatrolRoute` | — | none | no shared state |

**Untested.** Every row is implemented and none of it has ever been run between two peers.
- Counterweight targets are compared and sent in whole millimetres, otherwise
  the pair would re-report every physics step.
- `AnvilStation` and `DoorPrompt` now react only to the **locally** possessed
  weapon (`WeaponBody.IsLocallyPossessed`), so another player standing at an
  anvil does not raise your prompt.

---

## Labyrinth pieces (stage 6, 2026-09-20). IMPLEMENTED, UNTESTED.

Full context in `docs/LABYRINTH.md` sections 8 and 9. These are the new pieces
and the rules for using them.

### `MagicDoor_Grid.prefab` — `Assets/Prefabs/Kit`

A `MagicDoor` whose far side is **whatever room the labyrinth table currently
puts next door**, resolved the moment it is asked. Built from `MagicDoor.prefab`
with the `Door`, the `DoorCondition` and the solid `Panel` removed: a grid
doorway has no gate and is always open.

Rules for placing one:

- Fill in `labyrinth` (the scene's `LabyrinthDirector`), `room` (its
  `LabyrinthRoom`) and `doorwayDir`, and put it in that room's `doorways` array
  at the same index. `gridDoor` must be on; `twin`, `gate` and `linkId` stay
  empty — the validator will not ask for them, and will complain if the room
  does not list the door back.
- Keep it **upright**, yaw only, with its local **+Z facing into the room**. A
  traveller is only accepted crossing from +Z to -Z.
- It still needs `authority` and a unique `id` like any other `ISceneId`.
- The opening is 2.9 x 3.45; author a wall hole of about 3.0 x 3.5 around it.

### `DoorwayGlyph.cs` — the truthful sign

On the doorway root, wired to the `MagicDoor` and two world-space TextMeshPro
labels. Four times a second it writes `DestinationGlyph` and `DestinationLabel`.
**Glyphs never lie; compasses do** — a doorway always says truthfully where it
leads right now, including straight after a Mage moves a room.

### `LabyrinthRoom.prefab` and its three variants — `Assets/Prefabs/Rooms`

A 24 m square, 10 m high grey-box room with four centred doorways, four torches,
a `RoomVolume`, a room-name `Sign`, a big disabled `Footprint` box, an `Anchor`
and a `SpawnPoint`. `LabyrinthRoom_Start` adds the weapon rack and eight soul
spawns; `LabyrinthRoom_BadEnd` adds the red Resurrection ring and its counting
volume; `LabyrinthRoom_Exit` adds four `ExitZone`s, one per doorway.

The **footprint** is what `CollectPlayerCells` uses to say which cell a player is
in. Make it generous in every direction, including up: outside every footprint a
player counts for nothing, for the endings or for a respawn anchor.

Nothing in the code says "square". A rectangular or odd-shaped room only has to
keep one doorway per Heading, each upright with +Z into the room, listed in
N E S W order, and a footprint that covers it.

### `ExitZone.cs`

A trigger box in front of one doorway of the good-end room. It goes live only
while that doorway is `IsExitDoorway` — the corner and the direction are chosen
by the seed, so all four doorways carry one. Live, it shows a bright ring
**exactly `exitGatherRadius` across, centred on the doorway**, and a second disc
while a weapon stands in it. It decides nothing: the host wins the round by
measuring that radius itself.
