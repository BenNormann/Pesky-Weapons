# Rooms v4: the Circuit Room and the Chasm, revamped (2026-10-09)

Owner's brief: both rooms were too simple and made no sense as rooms goblins live in. Revamp them with real
effort, grey-box still, no signs (a narrator will hint later), decorations that block sight and movement and
are easy to swap for art later (a few `Deco_*` prefabs the builder places; change the prefab, every copy
follows). Nothing may wedge a weapon into a corner: decorations are flush against a wall or at least 1.5 m
from anything, with plain box colliders.

## The magic that runs the labyrinth

The generator in the Circuit Room powers EVERY teleport door on the map. Conduits leave it in all
directions and run through the walls to the doors; most are intact. The energy is visible: **`M_MagicFlow`**
(shader `Pesky/MagicFlow`), medium dark purple with specks of light lavender drifting through it, and the
same material on every teleport doorway's portal, so a door and the wire feeding it read as one system.
A conduit with no power is the same material, dead: dim, still, purple-grey (`_Live` 0).

Kit (round 22):
- **`CircuitBridge`**: a break in a conduit, two stubs with a cradle between them. A METAL weapon lying still
  across BOTH stubs closes it. The gap decides who fits: 1.05 m takes a Sword (a Dagger is 0.6 m), 0.5 m
  takes a Dagger. Host-decided like a hold plate (KIT_STATE Circuit, protocol 9). While the near stub is fed
  and the break is open it throws sparks.
- **`Circuit`**: the breaks in order from the source and the conduit segments between them. Segment i is
  live when breaks 0..i-1 are closed. Whole when every break is closed. A view with no state of its own.
- **Door on `CircuitLive`**: the doorway's own barrier (the Door slab, M_MagicFlow dead) slides away when the
  circuit is whole and the portal lights. No opener, no lever: power is the door.
- **`MagicBridge`**: a span of pure magic that is solid and lit only while its circuit is whole; dead it is
  NOT THERE: not drawn, no collider.
- **Seal** (round 23): whole once is whole for good. The moment every break is closed the circuit seals: each
  break fills itself in (the `Fill` piece), the weapons may leave, the whole run pulses with power. Every
  break prefab carries `Attach_A` / `Attach_B` wiring points on its stubs' outer faces and the builder runs
  each conduit exactly to them, every rail end half a rail past its point: no gaps.

## Room 4: CIRCUIT ROOM, the goblins' power room

**Why it exists.** The generator lives here and the goblins keep it: a workshop around a magical engine.
The conduit that feeds THIS room's exit door was broken in two places; every other conduit leaving the room
is intact and glowing, so the one dark run is the puzzle, visibly.

**Shape.** 24 x 30 m, 8 m high. Entry north at floor level, exit south (x +4). The GENERATOR at the room's
heart: a 2 m crystal column floor to ceiling in a four-post cage, a glowing halo ring at 4 m, a dark base.
Four intact conduits leave the base and run out through the four walls at hip height (0.6 m), glowing. The
fifth, the dead one, leaves west, runs along the west wall, DOWN into the MAINTENANCE PIT (6 x 8 m, 2.5 m
deep, in the south-west, goblin steps of 0.5 m down into it), across the pit's floor past BREAK 1 (1.05 m,
Sword-sized), UP the pit's east wall, and along the south wall to the GALLERY (a mezzanine 10 m wide, 3 m
up, along the south-east, reached by a goblin stair of 0.5 m risers), across the gallery past BREAK 2
(1.05 m), down through the gallery's slab and into the exit door's own feed strip. Conduits are collider-less
rails along ceilings and walls (0.3 m off the face), dropping to floor level only along a wall's base to reach
a break; both cradles sit flush against a wall with their stubs on that wall's rail line.

**Puzzle.** Two Sword-sized metal bodies held still at once, one in the pit, one on the gallery. Segment by
segment the conduit lights as power reaches it; the fed, open break sparks; when both are closed the door's
barrier lights and slides. The rack has three Swords and Mace or Hammer also span 1.05 m, so this never
deadlocks. Interlock: it takes two still bodies; the third player (and the Mage) are free, and the Mage's
best play is to make one of the two move.

**Decorations.** Crates and barrels in stacks that hide the pit's break from the entry and break sight across
the room; a shelf against the east wall. No pillars (owner, round 27). Flush or 1.5 m clear.

## Room 5: THE CHASM, the fallen bridge (finale, holds the EXIT)

**Why it exists.** A cistern cut the goblins' way to the exit; they bridged it with pure magic. We arrive on
the wrong side and the bridge is dead: the conduit feeding it has a short break on THEIR side.

**Shape.** 24 x 38 m, 30 m high. Entry north and exit south both at the SAME level, 12 m above the cistern
floor (round 24: the owner's 2x), so from the entry you see straight across the gap to the lit exit door:
that is where you go. The NEAR BANK (12 m deep) and the FAR BANK (12 m deep) are stone platforms; between
them the CISTERN, 14 m across, floor at 0, with the old bridge's rubble in it. The way back up if you fall:
a MAGNET on an arm over the near lip, east of the bridge line (a `MagnetZone`, the FeelBox one). Metal bodies
are pulled up out of the cistern to the hold point under its red head, 4 m above the bank and a metre out
over the drop; from there a launch toward the bank is a free one (the magnet's box covers the cistern and the
lip but not the bank's top, so a body that lands on the bank is out of its reach at once; the hold counts as
grounded, so a launch is always allowed). For the wooden Staff and the Banana, which no magnet lifts, a
goblin stair runs ALONG the near bank's face, its foot near the middle of the cistern floor and its top step
at the bank's north-west corner, every step flush against the face. The MAGIC BRIDGE spans the gap at bank
level, 4 m wide, and dead it is not there. On the
far bank, beside the bridge's end, the conduit comes out of the wall to BREAK 3 (0.5 m, Dagger-sized), then
into the bridge. Two goblins on the far bank, one on the near bank; all three attack a weapon that moves.

**The width is the math** (round 23; the first cut was 28 m and nothing crossed it). Gravity is 20 m/s2 and a
braced bat leaves at the 35 degree lift floor at batBraceSpeed x mA / (mA + mB). At 19 (round 23): Hammer on
Dagger 17.7 m/s flew 14.7 m, Mace 13.4, Orb 10.9, Sword 9.7: only the heavies crossed the 12 m between the
abutment tips, barely, and the owner wanted it "super far". At 26 (round 24, `batBraceSpeed`): Hammer on
Dagger 24.3 m/s flies 27.7 m, Mace 25 m, Orb 20 m, Sword 18 m, Staff 16 m: everything crosses, and the heavies
carry a Dagger clean to the far wall (25 m from the near tip, 1.7 m up it). Tune `batBraceSpeed` to taste;
the braced bat is never clamped by `batMaxSpeed` (the host's validator ceiling is the larger of the two).
**Puzzle.** No launch crosses it; a braced friend batted by a Mace or Hammer does (the bat always leaves at
35 degrees or steeper, so only the aim left or right can miss). Beat 1: kill or dodge the near goblin
(it attacks anything that moves). Beat 2: the Dagger braces anywhere on the lip, the heavy launches into it
aimed at the far bank. Beat 3: the Dagger lands among two goblins and lies still (a still weapon is
furniture), creeps to the break, lies across it: the bridge comes alive. Beat 4: everyone crosses, the fight
on the far bank, the exit. No pad, no sign; the bridge, the sparks at the break and the door tell the story.

**Test scene** `Assets/Scenes/Dev/BatTest.unity`: this room alone, a bot Dagger standing braced on the near
abutment (auto-brace: a loose weapon that stands up by itself and takes a bat) that you can also possess, a
rack with Maces, Hammers, a Dagger, a Sword and a Banana, no goblins, the doorways bricked up (the MagicDoor
prefab's alcove and Back, M_Brick, with the portal plane off). Rebuilt by hand after every re-save of the
BatRoom prefab (a re-save drops the instance's overrides).

**Decorations.** Rubble blocks in the cistern (1.5 m clear of the walls, the stair and each other), barrels
and crates on the goblins' bank. No pillars (owner, round 27).

## Not yet built from this doc
Narrator hints (signs are gone from these two rooms only for now). Art for the Deco prefabs.
