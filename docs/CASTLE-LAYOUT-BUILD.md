# CASTLE LAYOUT — the physical build

Stage 2 of 5 of the tower build, 2026-09-19. Everything here is **implemented, untested**: no play mode,
no tests, no screenshots. The checks that were run are listed at the end.

Each tower is a tall slender round tower with **one room per floor**, and the rooms are **bigger on the
inside**: they link through MagicDoors, so a room may be far wider than the tower shell that surrounds it.
The exterior shells are visual only (no colliders) and the `FloorActivator` switches off every floor
except the one you are on and its door-linked neighbours, so the overlap is never visible in play.

---

## 1. Axes and pitch

| Thing | Value |
|---|---|
| Back tower axis (`Zone1.unity`) | x = 0, z = 102 (directly above Room 5, the Arena) |
| Lift shaft axis (`Zone1.unity`) | x = 26, z = 37 (beside Room 2, the Goblin Room) |
| Main tower axis (`MainTower.unity`) | x = 240, z = 102 |
| Keep (`Keep.unity`) | ground level, x 258 → 355, z = 102 |
| Bridge (`Bridge.unity`) | x 16 → 222, z = 102, deck y 204 → 174 |
| **Floor pitch** | **12 m** — floor index `f` ⇒ floor top at `y = 12 f` |
| Normal room height | 10 m clear (floor top to wall top), roof slab 10.0 → 10.5, next floor's slab starts at 11.5 |
| Tall shaft height | 22 m clear; a tall shaft eats two floor indices |
| Wall thickness | 0.5 m, centred on the polygon edge |
| Doorway | 3.0 wide x 3.5 high, sill at floor level |
| Floor / roof slab | 0.5 m thick; the floor's TOP is the quoted y |

Rooms 1–5 (the cellars) were **not moved**. They still run along +Z from z = 0 to z = 112 with their
floors at y = 0 (Room 1 at y = −2.5). The tower stands on top of them.

Deleted this stage: the placeholder `Shells/Room6..Room10`, and `Hallways/Hallway_5` (it only led to
those shells; the Arena's north door is now the magic doorway to Room 6).

---

## 2. Back tower — `Assets/Scenes/Zone1.unity`

All on the axis x = 0, z = 102 unless the position says otherwise.
Group path is `Environment/<group>/{Geometry,Gameplay,Lighting,Spawns}`.

| # | Group | f | Floor y | Shape | Size (across x depth / inner) | Height | Room id | Sign | Spawn |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `Room1_WeaponRoom` | — | −2.5 | cellar (built) | 16 x 24 | 10 | 301 | 1101-1104 | 1201/1202 |
| 2 | `Room2_Goblin` | — | 0 | cellar (built) | 14 x 14 | 6 | 302 | — | — |
| 3 | `Room3_KeyClimb` | — | 0 | cellar (built) | 14 x 18 | 14 | 303 | — | — |
| 4 | `Room4_Plate` | — | 0 | cellar (built) | 12 x 12 | 6 | 304 | — | — |
| 5 | `Room5_Arena` | — | 0 | cellar (built) | 20 x 20 | 8 | 305 | — | — |
| 6 | `Room06_RopeRoom` | 1 | 12 | Tall shaft (8-sided) | 16 across | 22 | 306 | 1105 | 1203 |
| 7 | `Room07_BarredDoor` | 3 | 36 | Wedge | 18 wide / 8 narrow / 14 deep | 10 | 307 | 1106 | 1204 |
| 8 | `Room08_PotRoom` | 4 | 48 | Hexagon | 18 across | 10 | 308 | 1107 | 1205 |
| 9 | `Room09_Armoury` | 5 | 60 | Octagon | 20 across | 10 | 309 | 1108 | 1206 |
| 10 | `Room10_CarryRoom` | 6 | 72 | L-shape | 22 x 22, arm 9 | 10 | 310 | 1109 | 1207 |
| 11 | `Room11_MagnetRoom` | 7 | 84 | Long gallery | 12 x 30 | 10 | 311 | 1110 | 1208 |
| 12 | `Room12_BeamClimb` | 8 | 96 | Tall shaft (8-sided) | 16 across | 22 | 312 | 1111 | 1209 |
| 13 | `Room13_Counterweight` | 10 | 120 | Ring | outer 24 / core 9 | 10 | 313 | 1112 | 1210 |
| 14 | `Room14_PatrolRoom` | 11 | 132 | Crescent | outer 26 / inner 14 | 10 | 314 | 1113 | 1211 |
| 15 | `Room15_StormRoom` | 12 | 144 | Round (12-sided) | 22 across | 10 | 315 | 1114 | 1212 |
| 16 | `Room16_Barracks` | 13 | 156 | L-shape | 24 x 24, arm 9 | 10 | 316 | 1115 | 1213 |
| 17 | `Room17_LedgeClimb` | 14 | 168 | Tall shaft (8-sided) | 16 across | 22 | 317 | 1116 | 1214 |
| 18 | `Room18_ScalesRoom` | 16 | 192 | Hexagon | 18 across | 10 | 318 | 1117 | 1215 |
| 19 | `Room19_ShaftTop` | — | 192 | Ring at (26, 192, 37) | outer 24 / core 12 | 10 | 319 | 1118 | 1216 |
| 20 | `Room20_GateGuard` | 17 | 204 | Round (12-sided) | 24 across | 12 | 320 | 1119 + 1124 | 1217 |
| a | `RoomA_LiftBottom` | — | 0 | Tall shaft at (26, 0, 37) | 12 across, walls y 0 → 192 | 192 | 321 | 1120 | 1218 |
| b | `RoomB_LiftStop` | — | 60 | Wedge at (26, 60, 47.543) | 14 wide / 6 narrow / 9 deep | 7 | 322 | 1123 | 1221 |
| c | `RoomC_KeyAlcove` | 16 | 192 | Wedge at (40, 192, 102) | 12 wide / 6 narrow / 8 deep | 7 | 323 | 1121 | 1219 |
| d | `RoomD_SecretRoom` | 13 | 156 | Hexagon at (40, 156, 102) | 14 across | 8 | 324 | 1122 | 1220 |

### The lift shaft — one real vertical structure

`Environment/RoomA_LiftBottom` is a single 8-sided shaft 12 m across on the axis (26, z 37), authored as
two wall sections under one `Geometry` group so the doorways land at the right heights:

| Section | y | Doorway |
|---|---|---|
| `Geometry/Shell_Lower` | 0 → 60, with the disc floor at y = 0 | edge 6 (−X) at (20.457, 0, 37) — magic doorway to Room 2 |
| `Geometry/Shell_Upper` | 60 → 192, no floor, no roof | edge 0 (+Z) at (26, 60, 42.543) — the plain opening into Room b |

`RoomVolume_A` was widened by hand to cover the whole shaft: centre (0, 96.5, 0), size 13 x 194 x 13.
The shaft head (y 192 → 202) is the **core wall of Room 19** and is capped by `ShaftHeadRoof`, a DiscRoof
at (26, 202.25, 37), radius 6.3. Room 19's inner wall carries one opening at edge 0 (+Z), (26, 192, 42.543).
Room 19's floor is an **annulus** (12 box segments from r = 5.29 to r = 12.25), so the shaft is open through it.

**Lift** — `Environment/RoomA_LiftBottom/Gameplay/Lift`, `Platform` has `Lift` id **1501**, registered in
`WorldAuthority.lifts`, `clock` = `_Managers/LevelClock`, **`startsOn` = false** (dead until the winch at
Room 19). Stops bottom to top: `Stop_0` y 0.5, `Stop_1` y 60 (Room b), `Stop_2` y 192 (Room 19).
Instance overrides: `travelSpeed` **30** (192 m at the default 3 m/s would be 64 s a leg), `dwellSeconds` 4,
`Surface` scale 7.4 x 0.5 x 7.4, `riderBoxSize` 7.6 x 1.4 x 7.6.
`Stop_0` is 0.5 m above the shaft floor — a 0.5 m hop, which every weapon clears (Hammer apex at 45° = 1.01 m).
Because the lift parks at its **top** stop while it is off, Room 19's shaft head is plugged by the platform
until the winch is turned; after that the middle of Room 19 is an open shaft.

### Exterior shell

`Environment/BackTowerShell/Geometry`: 12 `ShellPanel` instances at circumradius 15 (apothem 14.489),
panel 8.538 x 208 x 0.5, y 9 → 217, plus `ShellCap` (a DiscRoof, radius 15.5, **MeshCollider disabled**)
at y = 217.25. **No colliders anywhere in the shell.** Every room is ≤ 26 m across, so nothing pokes
through; a wider room would.

---

## 3. Door link table (path order)

Every link is a MagicDoor pair sharing a `linkId`, each door pointing at the other with `twin`, both
registered in `WorldAuthority.magicDoors` and their own `Door` in `WorldAuthority.doors`.
Door conditions are **AlwaysOpen** except where noted — later stages set the real ones.

| link | From | MagicDoor / Door id | Position, yaw (+Z = into the room) | To | MagicDoor / Door id | Position, yaw |
|---|---|---|---|---|---|---|
| 1 | 5 Arena | 1301 / **404** | (0, 0, 112.25) 180 | 6 Rope | 1302 / **443** | (0, 12, 94.609) 0 |
| 2 | 6 Rope | 1303 / 406 | (0, 12, 109.391) 180 | 7 Barred Door | 1304 / 407 | (0, 36, 95) 0 |
| 3 | 7 | 1305 / 408 | (0, 36, 109) 180 | 8 Pot Room | 1306 / 409 | (0, 48, 94.206) 0 |
| 4 | 8 | 1307 / 410 | (0, 48, 109.794) 180 | 9 Armoury | 1308 / 411 | (0, 60, 92.761) 0 |
| 5 | 9 | 1309 / 412 | (0, 60, 111.239) 180 | 10 Carry Room | 1310 / 413 | (0, 72, 91) 0 |
| 6 | 10 | 1311 / 414 | (−6.5, 72, 113) 180 | 11 Magnet Room | 1312 / 415 | (0, 84, 87) 0 |
| 7 | 11 | 1313 / 416 | (0, 84, 117) 180 | 12 Beam Climb | 1314 / 417 | (0, 96, 94.609) 0 |
| 8 | 12 | 1315 / 418 | (0, 96, 109.391) 180 | 13 Counterweight | 1316 / 419 | (0, 120, 90.409) 0 |
| 9 | 13 | 1317 / 420 | (0, 120, 113.591) 180 | 14 Patrol | 1318 / 421 | (−10, 132, 102) 0 |
| 10 | 14 | 1319 / 422 | (10, 132, 102) 0 | 15 Storm | 1320 / 423 | (0, 144, 91.375) 0 |
| 11 | 15 | 1321 / 424 | (0, 144, 112.625) 180 | 16 Barracks | 1322 / 425 | (0, 156, 90) 0 |
| 12 | 16 | 1323 / 426 | (−7.5, 156, 114) 180 | 17 Ledge Climb | 1324 / 427 | (0, 168, 94.609) 0 |
| 13 | 17 | 1325 / 428 | (0, 168, 109.391) 180 | 18 Scales | 1326 / 429 | (0, 192, 94.206) 0 |
| 14 | 18 | 1327 / 430 | (0, 192, 109.794) 180 | 19 Shaft Top | 1328 / 431 | (37.591, 192, 37) 270 |
| 15 | 19 | 1329 / 432 | (14.409, 192, 37) 90 | 20 Gate Guard | 1330 / 433 | (0, 204, 90.409) 0 |
| 16 | 2 Goblin Room | 1331 / 434 | (7.25, 0, 37) 270 | a Lift Bottom | 1332 / 435 | (20.457, 0, 37) 90 |
| 17 | 9 Armoury | 1333 / 436 **key 2** | (9.239, 60, 102) 270 | b Lift Stop | 1334 / 437 **key 2** | (26, 60, 52.043) 180 |
| 18 | 18 Scales | 1335 / 438 | (6.75, 192, 98.103) 300 | c Key Alcove | 1336 / 439 | (40, 192, 106) 180 |
| 19 | 16 Barracks | 1337 / 440 | (−12, 156, 102) 90 | d Secret Room | 1338 / 441 | (40, 156, 95.938) 0 |

Plus, not a magic door:

* `Room20_GateGuard/Gameplay/Door_Sealed_ToBridge`, **Door id 442**, mode `Sealed`, at (0, 204, 113.591)
  yaw 180, with `Sign_20_Sealed` (id 1124) reading "TO THE BRIDGE / SEALED". This is Room 20's far
  doorway to the Bridge; it stays shut in this build.
* `Room16_Barracks/Geometry/Shell/CrackedWall_Placeholder` — a plain `WallSegment` (3.4 x 3.7 x 0.6) at
  (−11.45, 157.75, 102) standing in front of link 19's door. **The CrackedWall replaces it next stage.**
* Room 2's east wall was cut for link 16: `Wall_East` deleted, replaced by `Wall_East_Doorway`
  (a `DoorwayWallSegment` at (7.25, 0, 37) yaw 270, 14.5 long, 6 high) and `Wall_East_Base`
  (a `WallSegment` at (7.25, −0.25, 37), 14.5 x 0.5 x 0.5) closing the strip under it.
* Room b's threshold: `RoomB_LiftStop/Geometry/Shell/Threshold`, a `Floor` box (3.5 x 0.5 x 1.0) at
  (26, 59.75, 42.54) filling the 0.5 m band inside the shaft wall so the doorway is not a slot.

Door id 405 was skipped: `Hallways/Hallway_4/Gameplay/KeyDoorPrompt` already owns id 405, so link 1's
Room-6 door is id **443**.

---

## 4. FloorActivator

`_Managers/FloorActivator` (`Assets/Scripts/Game/FloorActivator.cs`), `spawner` = `PlayerSpawner`,
`authority` = `WorldAuthority`, `checkInterval` 0.2, `cullFloors` true, 24 entries. It enables only the
`Geometry` and `Lighting` groups of the room the local player is in plus its door-linked neighbours.
**Gameplay and Spawns are never touched.** Hallways 1–4 are never touched either.

| Room id | Neighbours kept on with it |
|---|---|
| 301 Room 1 | 302 |
| 302 Room 2 | 301, 303, 321 |
| 303 Room 3 | 302, 304 |
| 304 Room 4 | 303, 305 |
| 305 Room 5 | 304, 306 |
| 306 Room 6 | 305, 307 |
| 307 Room 7 | 306, 308 |
| 308 Room 8 | 307, 309 |
| 309 Room 9 | 308, 310, 322 |
| 310 Room 10 | 309, 311 |
| 311 Room 11 | 310, 312 |
| 312 Room 12 | 311, 313 |
| 313 Room 13 | 312, 314 |
| 314 Room 14 | 313, 315 |
| 315 Room 15 | 314, 316 |
| 316 Room 16 | 315, 317, 324 |
| 317 Room 17 | 316, 318 |
| 318 Room 18 | 317, 319, 323 |
| 319 Room 19 | 318, 320, 321, 322 |
| 320 Room 20 | 319 |
| 321 Room a | 302, 319, 322 |
| 322 Room b | 309, 319, 321 |
| 323 Room c | 318 |
| 324 Room d | 316 |

---

## 5. The other three scenes (shells only, not in the build settings, not wired in)

They use the same world space, so opening all four additively shows the whole castle. None of them has a
Camera, an AudioListener, a WorldAuthority or a LevelClock — they are not playable yet. Each has
`_Managers`, `_Cameras`, `_Lighting` (one Directional Light), `_UI` and `Environment/<Room>/…`, with
`gameplayObjects = false`, so a room is a shaped floor, walls with doorway gaps, a roof, torches and a Sign.

### `Assets/Scenes/Bridge.unity` — rooms 21–23, z = 102, no roofs

| # | Group | Centre | Shape | Size | Height | Ends at |
|---|---|---|---|---|---|---|
| 21 | `Room21_NearSpan` | (50.33, 198, 102) | Long gallery, yaw 90 | 8 wide x 68.67 long | 5 | x 16 and 84.7 |
| 22 | `Room22_BallistaGap` | (119, 186, 102) | Long gallery, yaw 90 | 8 x 68.67 | 5 | x 84.7 and 153.3 |
| 23 | `Room23_FarSpan` | (187.67, 174, 102) | Long gallery, yaw 90 | 8 x 68.67 | 5 | x 153.3 and 222 |

The deck runs from y = 204 at the back tower (Room 20's level) down to y = 174 at the main tower
(Room 24's level is 168), a steady 36 m fall over 206 m. Sign ids 1125–1127.

### `Assets/Scenes/MainTower.unity` — rooms 24–41 and e, f, g, axis (240, 102), pitch 12

| # | Group | f | Floor y | Shape | Size | Height | Sign |
|---|---|---|---|---|---|---|---|
| 41 | `Room41_TowerGate` | 0 | 0 | Wedge | 18 / 8 / 14 | 10 | 1145 |
| 40 | `Room40_WellBottom` | 1 | 12 | Round | 22 | 10 | 1144 |
| 39 | `Room39_CartRoom` | 2 | 24 | L-shape | 22 x 22, arm 9 | 10 | 1143 |
| 38 | `Room38_Barracks2` | 3 | 36 | Round | 26 | 10 | 1142 |
| 37 | `Room37_CoilRoom` | 4 | 48 | Hexagon | 18 | 10 | 1141 |
| 36 | `Room36_BladeHall` | 5 | 60 | Crescent | 26 / 14 | 10 | 1140 |
| 35 | `Room35_KeyCage` | 6 | 72 | Tall shaft | 16 | 22 | 1139 |
| 34 | `Room34_GuardHall` | 8 | 96 | Long gallery | 12 x 30 | 10 | 1138 |
| 33 | `Room33_DrainRoom` | 9 | 108 | Wedge | 18 / 8 / 14 | 10 | 1137 |
| 32 | `Room32_SeesawRoom` | 10 | 120 | Long gallery | 12 x 30 | 10 | 1136 |
| 31 | `Room31_RackRoom` | 11 | 132 | Octagon | 20 | 10 | 1135 |
| 30 | `Room30_OrbTrack` | 12 | 144 | Crescent | 26 / 14 | 10 | 1134 |
| 29 | `Room29_PipeRoom` | 13 | 156 | Ring | 24 / core 10 | 10 | 1133 |
| 24 | `Room24_EntryHall` | 14 | 168 | Octagon | 20 | 10 | 1128 |
| 25 | `Room25_SlotRoom` | 15 | 180 | Hexagon | 18 | 10 | 1129 |
| 26 | `Room26_GuardRoom` | 16 | 192 | Round | 22 | 10 | 1130 |
| 27 | `Room27_ChandelierRoom` | 17 | 204 | Octagon | 20 | 10 | 1131 |
| 28 | `Room28_OrbRoom` | 18 | 216 | Round | 24 | 12 | 1132 |
| e | `RoomE_OrbCrawl` | — | 204 | Crescent at (286, 204, 102) | 18 / 10 | 6 | 1146 |
| f | `RoomF_Well` | — | 12 | Tall shaft at (272, 12, 102), no roof | 10 across, walls y 12 → 106 | 94 | 1147 |
| g | `RoomG_Treasury` | — | 108 | Octagon at (286, 108, 102) | 16 | 8 | 1148 |

`Environment/MainTowerShell/Geometry`: 12 `ShellPanel` at circumradius 18 (apothem 17.387),
panel 9.146 x 242 x 0.5, plus `ShellCap` (DiscRoof radius 18.5, collider disabled) at y 242.25. No colliders.

The bridge lands on Room 24 (y 168). Path order inside the tower is 24 → 25 → 26 → 27 → 28 (Orb Room, the
top), then back down 29 → 30 → … → 41, which is why 24 is not the highest floor.

### `Assets/Scenes/Keep.unity` — rooms 42–45 and h, ground level

| # | Group | Centre | Shape | Size | Height | Sign |
|---|---|---|---|---|---|---|
| 42 | `Room42_GreatHall` | (278, 0, 102) | Long gallery, yaw 90 | 18 x 40 | 10 | 1149 |
| 43 | `Room43_Forge` | (307.24, 0, 102) | Octagon, yaw 90 | 20 | 10 | 1150 |
| 44 | `Room44_Gatehouse` | (324.27, 0, 102) | Hexagon, yaw 90 | 18 | 10 | 1151 |
| 45 | `Room45_FrontDoors` | (343.65, 0, 102) | Round, yaw 90 | 24 | 12 | 1152 |
| h | `RoomH_YardPath` | (120, 0, 40) | Long gallery, yaw 90, no roof | 8 x 220 | 6 | 1153 |

The four keep rooms abut exactly at x = 298, 316.5 and 332.1, and 42's west doorway is at x = 258,
just clear of the main tower shell (x ≤ 257.4). `RoomH_YardPath` runs from x 10 (beside the back tower)
to x 230 (beside the main tower) at z = 40.

---

## 6. Arithmetic that was checked

`g = 20`, `range = v² sin(2a) / g`, `apex = (v sin a)² / (2g)`, lift clamped to 15°–80°, launch speeds
Dagger 14, Banana 13, Sword 12, Staff 11, Orb 10, Mace 10, Hammer 9. There are no authored platforming
jumps in this stage (those rooms are empty shapes for later stages), so only the lift crossings matter:

| Crossing | Distance | Worst weapon, minimum-lift range | Margin |
|---|---|---|---|
| Shaft floor → lift platform (`Stop_0` 0.5 m up) | 0.5 m rise | Hammer apex at 45° = **1.01 m**; every weapon clears it | 2.0x |
| Lift platform edge → Room 19's annulus floor | 1.59 m | Hammer at 15° travels **2.03 m** | 1.28x |
| Lift platform edge → Room b's threshold | 0.73 m | Hammer at 15° travels **2.03 m** | 2.8x |

Every launch travels forward, so the Room 19 crossing lands on the far side of the gap by design: the
ring floor is 6 m deep beyond it and the platform is 7.4 m wide, so no weapon can overshoot into the shaft
from either side at minimum lift (Dagger at 15° = 4.90 m, still on the surface).

**No soft locks added.** The shaft is a long drop but not a death pit: there is no fall damage, its floor
(Room a) carries the magic doorway back to Room 2, and the lift returns once the winch is on. Before the
winch, the parked platform plugs the shaft head at Room 19.

---

## 7. Checks run (no play mode, no tests, no screenshots)

* Scripts compile with an empty error console after every change.
* `Pesky / Validate Open Scenes` (`SceneValidator.Validate()`) on `Zone1.unity`: **0 problems**.
* `Environment`'s NavMeshSurface re-baked: 1623 verts / 747 tris, 206 ms.
* Read back from the scene: 1696 GameObjects, 1 Camera, 1 AudioListener; `WorldAuthority` holds
  42 doors, 38 magic doors, 24 rooms, 1 lift; 0 magic doors without a twin, 0 without a gate;
  the lift's three stops at y 0.5 / 60 / 192 with `startsOn` false; links 17's two doors both
  `PartyHasKey` key 2; the sealed door id 442 in mode `Sealed`; `FloorActivator` with 24 entries.
* Every new room's `Lighting` group holds at most 6 lights. (Rooms 1–5 were already over that and were
  left alone — Room 5 has 8 torches plus a point light from an earlier stage.)
* All four scenes saved. Build settings still hold only `Boot` (0) and `Zone1` (1).
