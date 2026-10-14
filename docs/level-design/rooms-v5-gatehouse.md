# Rooms v5: THE GATEHOUSE (redraft 2, 2026-10-14; APPROVED and BUILT in round 29)

Built as written below with the owner's edits: no textures, grey-box; decorations only where they block sight
or movement (the braziers, banners and empty rack are out; crates, barrels, a cart and a shelf stay); both
the stair and the timber post lead to the chamber; the chamber is 6 x 6 (the passage's width); the portcullis
is built by the builder from the Door prefab (no separate prefab); `Deco_Cart` is the one new Deco piece.

Owner's brief: the hallway gate room (room 3, `RoomsV3Builder.ArmoryGate`) is too simple and needs
decoration; propose a design as interesting as the Circuit Room. Owner's notes on draft 1: no moat (the trench
read as nothing); every lever and pressure plate in the game is to be CONNECTED TO A CIRCUIT LINE so cause and
effect read without a sign; decorate BOTH sides of the gate; the gate bigger and thicker, the wall thicker
than the gate; everything is stone brick (texture to come); new puzzle-piece prefabs are fine if they belong
in the room. Nothing below is built until the owner says yes. Grey-box, no signs, no pillars, decorations
flush or 1.5 m clear with plain box colliders, nobody can ever get stuck.

## One rule for the whole game: a switch is a wire

A pressure plate, a lever or a circuit break is a SWITCH on a magic line. Its conduit (the same `M_MagicFlow`
rails as the Circuit Room) runs visibly from the switch to the thing it drives and is LIVE while the switch is
on: a held plate lights its line, a latched plate keeps it lit, a lever on keeps it lit, and the door, gate
or bridge at the far end moves when its line is live. Where two lines meet (a gate held by a plate OR pinned
by a lever) they meet in a JUNCTION on the wall, and the line out of it is live when either line in is.
A player who follows a wire always finds what it does. This round builds the piece and wires the Gatehouse;
the Well Room's exit plate, the Porter Room's bell and grate lever and the Chasm already-wired break get their
lines in the same pass (the builder does it; no hand work).

Kit (new):
- **`PowerLine`**: a view component listing a `source` (a PressurePlate, an ImpactLever, a Door or a Circuit)
  and its rails; drives the rails' `_Live` like `Circuit` does. No state of its own.
- **`PowerJunction`**: a wall box with N inputs (PowerLines) and one output line, OR / AND; also a view.
- **`Portcullis.prefab`**: a big lattice gate (6 m wide, 7 m tall, 1 m thick, stone-brick frame, iron bars)
  on a `Door` that slides UP, with a WINCH DRUM and a COUNTERWEIGHT above it (the weight drops as the gate
  rises: a second `Door` on the same condition). Replaces the 3 x 3.5 door-with-bars.
- Deco: `Deco_Brazier` (a fire bowl, flush to a wall), `Deco_Banner` (flat on a wall), `Deco_Cart` (a goblin
  hand cart, 1.5 m clear), `Deco_RackEmpty` (an empty weapon rack, flush). Grey boxes for art later.

## The Gatehouse: why it exists

The inner gate of the labyrinth: a stone-brick gate wall across the hall with the portcullis in its only
passage, a winch chamber inside the wall above the gate, the petitioners' yard in front and the guards' yard
behind. The goblins closed the gate. Its winch is driven by magic from the generator's network, and the
goblins' own convenience is the puzzle: a HOLD PLATE in the front yard raises the gate while something heavy
stands on it (so one porter could bring a cart through), and a PIN LEVER in the winch chamber locks the
winch with the gate up (so the guards did not have to stand on the plate all day). Both are wired, in plain
view, to the junction on the gate wall.

## Shape: 24 x 60 x 12 m hall, entry north, exit south, floor level both

1. **The front yard** (north, 22 m). Where you wait to be let in. The HOLD PLATE (8 kg: Mace or Hammer) is
   set into the floor off to the west, half hidden behind a crate stack and a cart, with its line running
   along the floor and up the gate wall to the junction. Decoration: braziers either side of the entry, a
   banner on each flanking wall, barrels, the cart, an empty rack. ONE sleeping goblin (`GoblinSleeper`) by
   the east wall: a loud landing wakes it.
2. **The gate wall**: 8 m thick, full height, stone brick. In its middle the GATE PASSAGE, 6 m wide and 7 m
   tall, with the PORTCULLIS (1 m thick) at its north end: closed it fills the opening; open it rises into
   the wall. Over the passage, inside the wall, the WINCH CHAMBER: 8 x 6 m, 4 m high, with the drum and the
   counterweight visible through a wide window onto the front yard, and the PIN LEVER on the drum. Reached by
   a goblin stair (0.5 m risers) that climbs the gate wall's north face on the east side, flush to the wall,
   with a parapet at the top: any weapon hops up it. (A timber post beside the stair lets a blade climb
   straight up instead, if the owner wants the climb taught here; optional.)
   On the north face, beside the passage, the JUNCTION: two lines in (plate, lever), one line out running up
   to the winch. In the passage's ceiling the gate's line is visible: power is the gate.
3. **The back yard** (south, 30 m). The guards' side: a watch platform (mezzanine, 4 m up, along the east
   wall, goblin stair) with a brazier on it, a long shelf of goblin gear against the west wall, crate stacks
   that hide the exit's switch from the passage. TWO goblins: one on the watch platform, one patrolling the
   yard (`PatrolRoute`), both attacking anything that moves. The EXIT LINE: the exit door's supply wire comes
   down the south wall from the WEIGH PLATE (a latching 10 kg plate: a Hammer, or a Mace with a Sword or an
   Orb on it) set into the floor in the back yard's south-west corner behind the crates; latch it and the
   line lights to the exit door.

## The puzzle: three beats, two roles, two fights

- Beat 1, the yard: the heavy sits on the hold plate; its line lights, the junction lights, the winch line
  lights, the counterweight drops and the gate rises. The landing wakes the sleeper and the fight is in the
  yard while the heavy must stay still on the plate.
- Beat 2, the chamber: somebody else hops up the stair into the winch chamber and hits the pin lever; the
  lever's line lights, the junction stays live from either side, the gate stays up for good and the heavy may
  leave the plate. (Interlock: whoever holds the plate cannot pin the lever; it takes two bodies.)
- Beat 3, the back yard: through the passage, the two guards, the weigh plate behind the crates; its line
  lights to the exit door and the exit opens. A weapon that dies is back on its rack in the start room and
  comes through the open gate.
- The Mage's plays: nudge the heavy off the hold plate before the lever is pinned (the gate drops: the line
  dies, the counterweight rises, whoever is in the passage is shut on one side; the passage is 8 m, nobody is
  crushed); Nausea the one on the stair; Blind the heavy on the weigh plate hunt.

## Numbers to settle with the owner

- Hall 24 x 60 x 12; gate wall 8 m thick; portcullis 6 x 7 x 1; winch chamber 8 x 6 x 4 at 8 m.
- Hold plate 8 kg, weigh plate 10 kg (or 12 so it always takes two).
- One sleeper in front, a watcher and a patroller behind.
- The stair for everyone, or the timber post for blades only, or both.
