# Porter Room v5: the receiving hall (2026-10-14; APPROVED and BUILT in round 29d)

Built as written, with these numbers: the counter 1 m tall and 1 m thick (so the crate stacks sit on it
without overhang), its top 8 m in front of the grate; the dock 0.5 m up (a goblin riser, so it needs no
step) over x -8..-2, clear of the entry doorway, from the counter to the north wall; three stacks and a fourth
in the store, the stand in the far west corner, the porter starting beside the bars east of the maze; the
inspectors start behind the counter. The counter and the dock are `MLedge` blocks (the blue-grey ledge
stone), the furniture the usual Deco pieces.

Owner: give the Porter Room a visual overhaul, make it look better; plan first. Same rules as every room:
grey-box, no textures, decorations only where they are obstacles (block sight or movement), nothing a weapon
can wedge in, no signs, no pillars. Already done this round without a plan because they were direct asks: the
partition is 1.2 m thick and the grate rises INSIDE it with only its bottom edge showing; crate stacks,
barrels and a shelf in the hall, the porter's stores in the guard room; the porter's refusal line goes to the
game chat.

## Why the room exists

The goblins' RECEIVING HALL: everything that comes into the inner labyrinth is handed over here. Petitioners
wait in the hall, inspectors (the two goblins) check the goods at a counter, and anything small enough goes
through the hatch in the thick wall to the porter, who shelves it in the store behind. The hall is a public
room; the store is not. That is the whole puzzle, told by the furniture: you are the goods.

## What changes (three things, all obstacles)

1. **The counter.** A waist-high stone counter (1 m tall, 0.6 m thick) runs across the hall 8 m in front of
   the grate, from the west wall to 3 m short of the east wall: the inspectors' side and the petitioners'
   side. Weapons hop it (1 m is nothing) but goblins path round its open end, so the fight has a shape: the
   two goblins start BEHIND the counter and come round the east end. Crate stacks on the counter top (flush
   on it, two stacks) break the sight line from the entry to the grate, so the grate is found, not shown.
2. **The receiving dock.** The hall's west half, from the entry to the counter, is a raised stone dock 0.6 m
   up with a goblin step at its end (flush to the west wall; the ramp-free kind, a weapon hops it): the
   petitioners' queue. Barrels along the dock's edge (flush to the drop) and the cart at its head: a corridor
   of obstacles that funnels the way to the counter and gives the Dagger cover to creep along while the
   goblins hunt whatever moved last. Nothing a weapon can fall behind: the dock's drop is a plain step.
3. **The store.** Behind the grate: shelves flush along both walls, a crate maze (three stacks, 1.5 m apart,
   1.5 m from the walls) between the bars and the porter's stand, so the stand is round a corner and the
   Dagger's carry has a route through goods instead of across an empty room. The lever stays on the back of
   the partition beside the grate (its wire to the wheel is already there); the stand sits in the far corner
   behind the maze.

Not changing: the room's size (16 x 50 x 11), the partition, the grate and its bell, the porter and its
stand, the exit (room cleared), the two goblins' count. The goblins' routes: the inspectors start behind the
counter; `GoblinBrain` needs no change (the NavMesh bakes round the counter and the dock).

## Numbers to settle

- Counter 8 m in front of the grate (z -7), 1 m tall; or 1.5 m so only a launch clears it.
- Dock 0.6 m up over the west half (x -8..0, z 5..25); or the whole width with a gap at the counter end.
- Three crate stacks in the store, or two and a shelf island.
