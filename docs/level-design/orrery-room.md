# The Orrery (2026-10-15)

Grey-box, built by `Assets/Scripts/Editor/OrreryRoomBuilder.cs` (**Pesky / Rooms / Build The Orrery**). It makes the
prefab `Assets/Prefabs/Rooms/Labyrinth/RoomV6_Orrery.prefab` (a variant of the two-door room) and its own test scene
`Assets/Scenes/Dev/OrreryTest.unity` (a copy of BatTest: the room alone, the rack on the entry wall). To tune it, change
a number at the top of the builder and run the menu item again. It is **not in the run yet**: the rooms overhaul has
landed on `main`, so it can be installed into `Run.unity`; that is left as a follow-up.

## Why it exists

The goblins keep a star-clock. A brass orrery turns slowly on a dais in the middle of a round hall, its planets on rods
around a lit sun that hangs from the roof. The portal out of the room only opens when the machine is "tuned": three
star levers lit, one on each of the three perches the goblin astronomers work from. The floor is their star chart: two rings round the
dais. The lines across it are real wires (below), so the eye goes lever, wire, machine, door. Nothing in the room is a sign; the wires and the machine tell it.

## Shape

32 m octagon (flat to flat), 16 m high. Entry north and exit south, both at floor level. Stone pilasters stand at the
eight corners, a brass band runs round the walls at 9.6 m, and the roof is a timber dome: eight rafters to a boss, a ring
beam, and six small constellations in lit points between the rafters (the Ladle over the entry, the Anvil over the
shelf, the Hook over the loft, the Blade over the gallery).

| Piece | Where | Reached by |
|---|---|---|
| **Dais** | centre, 1 m high, 4.5 m radius, on a 0.5 m step ring | a hop (every weapon clears 1 m) |
| **Scribe's loft** | west wall, 2.5 m, 3.5 x 8 m | a goblin stair of 0.5 m risers facing the dais |
| **Brass shelf** | east wall, 4.5 m, 3.5 x 8 m | the floor launch pad, up 15 / out 5.5 |
| **Star gallery** | south wall over the exit, 7.5 m, 12 x 4 m | the dais launch pad only, up 18 / out 8.8 |

## Puzzle

Three latching `ImpactLever`s, one per perch, and the exit is a `Door` on `DoorCondition.Mode.LeversOn` with all three.
1. **Loft:** walk up the stair and hit the lever. It teaches the lever.
2. **Shelf:** stand on the floor pad; it throws you onto the shelf. It teaches the pad.
3. **Gallery:** the only way up is the pad on the dais, and the dais is swept by the orrery's arm (10 s a turn, a knee-
   height bar from the hub to just past the dais edge). Hop onto the dais behind the arm, onto the pad, and you are
   thrown over the exit onto the gallery between the two braziers. Get the timing wrong and the arm knocks you off.

**One player:** the levers latch, so one weapon can do all three in turn (about 60-75 s). **Three players:** one each,
and the gallery is the one worth arguing about. **The Mage:** the dais pad is the obvious place for a nudge, and a
mid-flight nudge to the gallery puts the body back on the floor.

## Kit used

`Spinner` (as the orrery's arm; its rotor also carries the axle, the planets and the sun, collider-less above the hub),
`LaunchPad` x 2, `ImpactLever` x 3, `Door` (LeversOn), and five props, each there for a reason (review on #16 / #17: no art
for the sake of it): `Deco_Brazier` x 2 on the gallery (they light and frame the hardest lever), `Deco_Banner` x 2 (they
mark the exit, so the eye finds the door from the entry) and `Deco_Table` x 1 on the loft (it says "this is where the
astronomers work" and sits beside the lever). An earlier cut also had a cauldron, sacks, a bone pile, a stool, two
crystals and three more banners; none of them did anything and they are gone. Every prop is flush to a wall or 1.5 m clear.

**Alignment and clearance (review fix, PR #17).** Every perch stands on posts that sit UNDER its slab with their outer
faces flush with the slab's edges (loft x -13.0..-12.5, shelf x 12.5..13.0, gallery x +-5.5..6.0 and z -12.5..-12.0), tied
by a beam under the front edge. The lit lines that climb the posts follow them. A script check on the built room lists
every prop whose bounds overlap anything else by more than 3 cm: all are clear. The wall skirting stands 0.12 m off
the wall and the corner pilasters 0.9 m, so props near a wall are placed 0.5 m off it.

**No coplanar faces (review fix, PR #17).** The scene validator (from the rooms overhaul, now on main) lists any two faces in the same plane that
point the same way and overlap, because they z-fight. The room follows the same rules: the perch beams are inset from the
posts (0.46 wide against 0.5, 7.96 against 8 m, 1 cm lower) and the lips 2 cm short; even stair steps are 2 cm narrower;
alternate ring beams are 0.35 and 0.31 deep; alternate constellation lines sit 8 mm apart; the ceiling stars are static;
the gate sits 1 cm up so its slab and the frame's jambs do not share the floor plane. Checked with that validator on this
room: only the three warnings every single-room test scene has (no LabyrinthDirector).

Lights: four torches (one at the entry, one over the scribe's desk, one low on the east wall under the shelf, one on the
wall above the gallery) and one violet point light at the sun. Five in all, under the six-per-room rule.

## Numbers (play-tested in OrreryTest)

* Floor pad (up 15, out 5.5): a Sword came to rest 1.45 m in from the shelf's face. The Banana slid to the back wall.
* Dais pad (up 18, out 8.8): a Mace came to rest 1.7 m in from the gallery's face.
* The arm spans 0.39-0.71 m over the dais: it clears the dais pad's base, knocks standing and hopping bodies, and leaves
  a body lying flat alone (tested: a Mace that fell over lay under it untouched).
* Door: shut with one and with two levers on, open on the third.
* Validator on OrreryTest: only the three warnings every single-room test scene has (no LabyrinthDirector).

## The wires and the machine (round 30, review on #17)

**A switch is a wire** (rooms-overhaul, `docs/level-design/rooms-v5-gatehouse.md`). Each star lever has a visible conduit:
from the lever's foot along its perch, down the post, across the floor to a **junction** at the foot of the orrery
(`PowerJunction`, `requireAll`, so it is live only with all three), and one line leaves it, under the gallery and up the
wall beside the door into the door's own supply wire. The exit has **no sliding slab**: it is a dead portal (dark, its
wire black) that lights when the junction does. Dead wires are black, live ones purple; follow a black wire from a lever
and you reach the junction, follow the junction's and you reach the door.

**The machine answers.** `OrreryMachine` links each planet to a lever (the higher the perch, the higher the world:
loft -> the small inner world, shelf -> the blue one, gallery -> the red one). While its lever is off a planet hangs at
its own bearing; when the lever turns on, its arm swings round (2.5 s) to the shared bearing, so three lit levers line the
three worlds up in one row, the sun gathers the power (light 2.2 -> 6.5, its `_Live` 1 -> 2.4) and the exit opens. It
only reads `ImpactLever.IsOn`, which already travels as KIT_STATE, so it costs no message and every peer shows the same
machine. It is a view of the puzzle, not a second puzzle: the door still opens on `LeversOn`.

**Z-fighting.** The scene validator lists coplanar faces. The room follows RoomsV3Builder's rules: rails have a thickness
per axis (0.24 along x, 0.23 along z, 0.20 vertical) and overrun their ends by 0.09; even stair steps are 2 cm narrower;
beams and lips are inset a hair (0.46 against a 0.5 post, 7.96 against 8 m, 1 cm lower); alternate ring beams are 0.35 and
0.31 deep; alternate constellation lines sit 8 mm apart. The kit prefabs `Deco_Banner` (rod, cloth and tails) and
`LaunchPad` (the two chevrons) had the same problem and are fixed. Validator on the test scene: only the three warnings
every single-room test scene has (no LabyrinthDirector); on `Run.unity`: 0 problems.

## Not done

Installing it into `Run.unity`, two-player testing, and a real hop into the arm by a player (the knock was tested on a
placed body, not on a live hop). The planets are visual only: the door still opens on `LeversOn`; making the alignment
itself the puzzle (levers cycling a planet through several bearings) would need a synced counter per lever, a netcode change.
