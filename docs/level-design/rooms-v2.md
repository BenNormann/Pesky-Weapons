# Rooms v2: five puzzle rooms for the run (text design, 2026-10-01)

Status: DESIGN ONLY, nothing built. Supersedes the four rooms in `docs/PUZZLE-ROOMS.md` for the
MVP run. The game as it stands is `docs/PREMISE.md` (2026-09-29 SIMPLIFICATION, 2026-10-01
VOTING) plus the kit in `docs/KIT.md` and the run in `docs/RUN.md`.

Conventions. Numbers are from `KIT.md` and the brief: g = 20; launch 15-80 deg; range at 45 deg
Dagger 9.8 m, Banana 8.5, Sword 7.2, Staff 6.1, Mace 5.0, Hammer 4.1; apex at 80 deg Dagger 4.75 m,
Sword 3.5, Mace 2.4, Hammer 2.0. A launch is about 1.5 s. "A" and "B" are the two players of the
canonical pair; the canonical pair is **Sword + Mace** and every room is solvable by it. Entry is the
north doorway and exit the south doorway (RUN.md section 8), but the exit's position inside the room
varies. "Solo" is the one-player path. Every room's solo path uses the **courier launch**: in the start
room launch a spare body at the first door and press Q in the air; the empty body sails through (loose
weapons are travellers, `MagicDoor`), you take a second body and follow; a soul can fly anywhere
inside a room, so a couriered body is never out of reach. It is slow (every room then needs one or two
soul round trips) and it is how one creative player gets through.

Rule used everywhere: **every pit, trench and chasm floor is left by a slope of 20 deg or less**, never
steps, because a Heavy-cursed Mace (5 m/s) only clears 0.6 m and must still be able to hop out.

---

## Room 1: GATE ROOM

**Shape.** Long gallery, 56 x 18 m, 9 m high, long axis north-south. Entry at the north end, exit at
the south end at the top of a ramp. Everything is visible down the length of the hall from the door.

**Premise.** A hall barred by one portcullis and one trench. Players learn that a parked body is a
weight, that a gate held from one side can be pinned from the other, and that a trench is nothing to
fear. Twist: the "key" to the first gate is a friend sitting still, and the "key" to the exit is both of
you standing in the hole together.

**Beats.**
1. HOLD (about 15 s). Visible from the entry: a square plate P1 (8 kg, non-latching) in front of a
   shut gate G1, a chain from the plate up to the gate. Nothing happens until something heavy sits on
   P1. The Mace (8) lands on it, G1 slides up and stays up only while the Mace stays. The Sword
   launches through. Kit: hold plate (new tunable), `Door`. Litmus: a human carrying the Mace could not
   do this because the Mace is not heavy enough to leave behind and walk off; here the body IS the
   weight and the player simply waits inside it.
2. PIN (about 15 s). Through G1 the Sword sees lever L1 on the wall with the other end of G1's chain
   running to it. Hitting L1 (any impact at 4 m/s) latches G1 open. The Mace leaves P1 and follows.
   Kit: `ImpactLever` (latching), `Door` with two conditions (plate held OR lever on, new). Litmus: a
   human could pull a lever with a hand; a weapon can only throw itself at it, which makes "hit the
   lever" a launch you can miss and that a friend can nudge.
3. WEIGH IN (about 30 s). The hall is cut by a trench 8 m wide, 3 m deep, with 18 deg ramps down both
   faces. On its floor is a big plate P2 (10 kg, latching) and the ramp beyond climbs to the exit door
   `Door_Plate`. Sword (3) alone does nothing, Mace (8) alone does nothing, both together (11) latch it
   and the exit opens for good. Kit: `Plate`, `Door_Plate`. Litmus: a human cannot weigh himself with
   a sword to make up a shortfall; two possessed bodies are two weights and the sum is the puzzle.

**Interlock.** The holder cannot be the goer (beat 1); the exit needs both masses at once (beat 3).
Asymmetric information: only the player beyond G1 sees L1 and the chain, so the holder is told
"there's a lever, I'm pinning it, you can get off" by voice.

**Solo.** Courier the Mace through, enter as the Sword. Possess the Mace, sit on P1, Q, fly through
the open gate as a soul, possess the Sword on the near side, launch through G1, hit L1, Q, fly back
to the Mace, bring it down into the trench onto P2, Q, bring the Sword down too (11 kg), exit as the
Sword; the Mace stays behind. About 2.5 min.

**Scaling 3-4.** P1 still takes one heavy; P2 needs 10 kg so any two bodies of the party suffice and
the Mage cannot stall it by staying out of the trench. With four players the trench is a meeting
point: everyone drops in, the latch fires, everyone ramps out. Add a second trench plate P2b (10 kg)
behind a second portcullis for four players if the room feels thin.

**Completion and state.** Exit opens when P2 is latched. Host bytes: P1 pressed (1 bit, live, not
latched), L1 on (1 bit), G1 open (1 bit), P2 latched (1 bit), exit open (1 bit).

**Soft-lock and reset.** A body that breaks re-forms at the rack one door away (about 30 s round
trip). The Mace parked on P1 and abandoned is still a weight, which is fine. No heavy in the party:
P1 cannot be held, and the hall says so at a glance; back to the rack for the Mace or Hammer (one
door). A Hammer alone (14) latches P2 but cannot pass G1 alone; the solo courier rule covers it. A
shutting G1 is a sliding panel that shoves a body out of the opening, never crushes.

**Mage moments.** (1) The holder steps off P1 while the friend is in the opening: "I thought you were
through." (2) Nudge the friend's launch at G1 into the jamb. (3) Heavy curse on the friend as he
drops into the trench: 20 s of hopping up the ramp at half speed.

**Kit.** Existing: `Plate`, `Door_Plate`, `ImpactLever`, `Door`. New: `Plate.latching` false (a hold
plate reports pressed / released, host family, no latch); `Door` with a second, OR-ed `DoorCondition`
(PlateHeld OR LeverOn), host state unchanged (one open flag).

**Loadout.** Needs one body of 8 kg or more. Easier with a Hammer (holds P1 from any landing) and a
Dagger (clears the trench in one hop).

---

## Room 2: WELL ROOM

**Shape.** Tall shaft, 8-sided, 24 m across, 20 m high. Entry at the floor, north. Exit door on the
south wall **12 m up**, on a ring balcony 4 m wide that runs round the shaft at y 12. A lift shaft in
the centre, parked at its top stop (the lift plugs the balcony's middle when off).

**Premise.** The way out is upstairs and there is a lift, but the lift is off and its winch is at the
top. Blades climb; blunts ride; the exit needs the rider. Twist: a sword climbs by stabbing itself
into the wall, and a mace is a passenger that nobody can leave behind.

**Beats.**
1. CLIMB (about 25 s). From the entry you see six wooden posts (`WoodBlock`, 0.9 x 6 x 0.9) in a
   spiral up the wall, each 3.6 m from the last and 1.6 m higher, each backed by the wall, and above
   them the balcony. A blade launches point-first into a post, sticks, counts as grounded, launches
   again. Sword reach at +1.6 m is 5.4 m (needs 3.6), Dagger 7.5. Mace and Hammer bounce off wood.
   Kit: `WoodSurface`. Litmus: a human could not climb by being stabbed into the posts; the weapon's
   own point is the piton.
2. WINCH (about 20 s). On the balcony the climber lands beside the winch (`Winch`, latching) and
   hits it: the lift wakes and starts its cycle (dwell 4 s, 12 m at 3 m/s). The heavy waiting below
   boards at the bottom stop and rides up. Kit: `Winch`, `Lift`. Litmus: a human would push the lift's
   button; the weapon must throw itself at the winch, and the rider must land square on a slick
   platform and sit still or slide off.
3. WEIGH (about 15 s). Beside the exit door on the balcony is plate P (8 kg, latching). The climber
   (3 kg) stands on it and nothing happens. The heavy steps off the lift onto it and the exit latches
   open. Kit: `Plate`, `Door_Plate`. Litmus: a human carrying a mace weighs more than 8 kg anyway;
   here only the body counts, so the blade that got up first is useless at the door.

**Interlock.** Symmetric: the blade cannot open the exit, the heavy cannot start the lift. Asymmetric
information: only the climber sees the winch and the plate from the balcony; the rider below sees
only the lift stop and must be told when to board (the lift dwells 4 s per stop).

**Solo.** Courier the Mace through. Climb as the Sword, hit the winch, Q, fly down as a soul, possess
the Mace, board the lift, ride up, sit on P, exit as the Mace. About 2 min and the first real
soul swap of the run.

**Scaling 3-4.** Two blades race the posts (a post takes two stuck blades side by side). All heavies
ride together (the platform is 3.2 m). For four players put a second winch halfway up on a side
ledge reachable only from the lift's middle stop (a Lift with three stops) so the ride itself has a
job.

**Completion and state.** Exit opens when P latches. Host bytes: winch on (1 bit), lift on (1 bit) +
start clock ms (u32), P latched (1 bit), exit open (1 bit).

**Soft-lock and reset.** A climber nudged off a post falls to the flat floor (no pit). A broken body
re-forms at the rack two doors away (about 60 s). If the blade player leaves his body stuck in a post
and flies off as a soul the body is still a foothold. No blade in the party: the posts are mute and
the lift stays off; back to the rack. No heavy: the plate never fires; a Staff (2.5) + Sword (3) +
Orb (4) party sums 9.5 and must fetch a heavy. The lift's off state parks it at the top, never
mid-shaft.

**Mage moments.** (1) Nudge the climber between posts: a fall to the floor and a 20 s re-climb,
"gust of wind". (2) Heavy curse on the climber: at 6 m/s the Sword cannot reach the next post at all
for 20 s and everybody can see him failing, which is funny and suspicious. (3) Slippery on the rider
as the lift leaves the floor.

**Kit.** Existing: `WoodBlock`, `Winch`, `Lift`, `Plate`, `Door_Plate`, elevated doorway (KIT.md
"Moving an authored doorway up a wall"). New: none.

**Loadout.** Needs one blade and one body of 8 kg or more. Easier with a Dagger (climbs in four
hops) and a Hammer (sits still on the plate).

---
## Room 3: PORTER ROOM

**Shape.** L-shape, arms 50 x 18 m (north arm, entry at its north end) and 36 x 18 m (east arm),
10 m high. A 5 m partition with a `PorterGate` in it closes the inner corner between the arms (a 1 m
lip on its north face defeats wall-jumps over it); the exit door is at the far end of the east arm.
Two chains come over the partition's top from the east arm and END in the north arm: one at a side
door in the partition, one at a goblin pen built into the north arm's wall.

**Premise.** Goblins do not know you are alive. A porter goblin tidies still weapons through its gate
and sets them on a stand on the far side; the gate shuts behind it. Players must play dead, get
carried, and then open a proper door for the rest from the far side, hitting the right one of two
levers that only the near side can tell apart. Twist: your friend is luggage, and the one who can see
the answer is the one who cannot act on it.

**Beats.**
1. SNEAK (about 20 s). The north arm has one patrol goblin on a loop (pause 1 s at each end) and,
   near the corner, the porter standing on its noticing ground beside its shut gate. A weapon seen
   moving inside the patrol's 110 deg cone turns it hostile; lying still 5 s makes it lose interest. A
   and B move only behind its back, in two or three hops, to the porter's ground. Kit: `Goblin` +
   `PatrolRoute`, view cone. Litmus: a human could not stop being a person; a weapon that lies still
   is furniture, and a goblin forgets furniture.
2. RIDE (about 20 s). A lies still for 2 s within 8 m of the porter; it fetches A, carries A through
   the gate (which opens only for a carrying porter) and sets A on the stand in the east arm. While A
   is carried, B follows the two chains to their ends in the north arm: one to the side door, one to
   the pen. On the far side A sees two levers, L_DOOR and L_PEN, with two identical chains climbing
   the partition, and cannot tell which is which.
   Kit: `GoblinPorter`, `PorterGate`, `HammerStand` (drop point). Litmus: a human cannot be picked up
   and carried through a door by the guard; a still weapon can.
3. LEVER (about 20 s). B says which lever. A launches from the stand into L_DOOR (4 m): the side door
   latches open and the exit door opens with it. B hops through the side door; everybody launches down
   the east arm to the exit. L_PEN instead opens the pen into the north arm, where everyone but the
   rider is waiting, and lets out goblins (one per player): a 20-30 s mess, not a dead end, L_DOOR
   still works. Kit: `ImpactLever` x 2, `Door_Lever` x 2, `Goblin`
   sleepers in the pen. Litmus: a human could read both chains and pull the right lever; a weapon
   has to be told, and throw itself, and the teller may be lying.

**Interlock.** The rider cannot see the answer, the reader cannot cross. Both could ride in turn (the
porter's cooldown is 5 s) but it costs 20 s per rider and a party in a hurry trusts the call. The
asymmetric information is the chains: their ends are on one side and their levers on the other, no
sign needed.

**Solo.** The courier is not needed. Read the chains first, sneak, lie still, ride, hit L_DOOR, exit.
Slow because a solo player is always in the patrol's cone with nobody to time the loop, and a hostile
patrol means lying still for 5 s each time.

**Scaling 3-4.** Patrols = players minus one (max three), on overlapping loops so somebody is always
looking. Pen goblins = players. Everyone but the rider waits in the north arm being quiet, which is
exactly where a Mage's "accidental" launch in front of the patrol is deniable. For four players the
porter has a second stand beyond a second gate (two rides in sequence, two side doors).

**Completion and state.** Exit opens when L_DOOR is on. Host bytes: porter carry (weapon id, u16),
L_DOOR on (1 bit), L_PEN on (1 bit), side door open (1 bit), pen door open (1 bit), exit open (1
bit), goblin HP (existing).

**Soft-lock and reset.** Turning animate in the porter's hands drops you and angers it (a fight, not
a lock). A body broken by goblins re-forms at the rack three doors away (about 90 s): the costliest
mistake in the run, which is why the room is about not fighting. A weapon parked on the stand and
abandoned stays there; the porter never re-fetches a weapon on its stand. If every goblin dies the
porter is dead too: the side door's lever is also reachable by a blade climbing two wooden posts at
the partition (a slow fallback), so a cleared room still opens. Goblins walk through shut gates
(KIT.md caveat): keep the pen out of the porter's route.

**Mage moments.** (1) The reader says "the left one" and it is the pen: "I swear it looked like the
door chain." (2) The rider "mis-launches" into L_PEN: "I slipped off the stand." (3) Blindness on the
rider just before the lever hop, or Nausea on a sneaker in the cone.

**Kit.** Existing: `GoblinPorter`, `PorterGate`, `PatrolRoute`, `Goblin`, `GoblinSleeper` (in the
pen), `HammerStand`, `ImpactLever`, `Door_Lever`, `WoodBlock`. New: none. The partition is open
above (a soul may fly over it to look; no weapon clears 5 m plus the lip).

**Loadout.** Any weapons. Easier with a Dagger or Banana (short, quick hops in the cone's shadow);
the Hammer is a slow sneaker.

---

## Room 4: LIGHTNING ROOM

**Shape.** Ring, outer 56 m across, 8-sided core 18 m across, ring 19 m wide, open roof over the
ring, roofed core. Entry on the ring's north side. The core's only opening is on its SOUTH face,
180 deg round the ring from the entry; the exit door is inside the core.

**Premise.** The open ring is under a storm: every 6 s a bolt hits the highest metal weapon in it,
after a 1 s glow. A lightning rod stands by the entry; a metal body parked on it draws every bolt
harmlessly. Someone must cross half the ring to the core, kill the storm from inside, and then a metal
body must lie across two contacts to open the exit. Twist: your friend is the lightning rod, and the
door's "key" is a sword used as a wire.

**Beats.**
1. ROD (about 15 s). A bolt lands every 6 s somewhere in the ring; the glow telegraphs it. By the
   entry: a 2 m plinth with a slope and a stand on top, a rod. The Mace climbs the slope and sits on
   the rod. From then on every bolt hits the Mace for 0 damage and nothing else in the ring is struck.
   Kit: `LightningField` + rod anchor (new). Litmus: a human cannot volunteer as a grounded conductor;
   a parked metal body is one.
2. CROSS (about 25 s). The Sword hops the half ring (about 60 m, 10 hops) to the core's south door.
   The Mace's player may Q and fly ahead as a soul to look into the core (souls pass the ring and the
   core's open doorway) and call out what is inside; the body on the rod keeps working. Kit: ring
   floor, `WoodBlock` wind-breaks every 10 m as wall-backed landings. Litmus: a human runs; a weapon
   hops in 1.5 s arcs with a bolt every 6 s, and the soul scouting ahead is a thing only a weapon can
   do.
3. QUENCH (about 10 s). In the core: lever L_QUENCH. Hit it and the field switches off (latched), the
   ring goes quiet, the rod is free. Kit: `ImpactLever` -> lightning off (new wiring). Litmus: as
   beat 2.
4. CONDUCT (about 25 s). Beside the exit door: two posts 1 m apart, a gap between them. A metal
   weapon resting across both for 1 s closes the circuit and the door latches open. The Sword lies
   down across them. The Mace leaves the rod, crosses the dead ring, both go through. Kit:
   `ContactPair` (new), `Door` on ContactsClosed. Litmus: a human cannot be the wire between two
   terminals; a sword is a bar of metal.

**Interlock.** With two metal bodies: the rod cannot cross, the crosser cannot be struck safely
without the rod. With a wooden Staff in the party the Staff crosses and quenches untouched, but the
contacts still need a metal body, so the metal still has to come. Asymmetric information: the
crosser sees the core; the rod's soul sees the glow pattern from above and the core first. "It needs
YOU in here, it wants metal on the posts" is the sentence the room is built to make someone say.

**Solo.** Courier the Mace. Park it on the rod, Q, possess the Sword, cross, quench, lie on the
contacts, exit as the Sword. About 2 min. A solo Staff + Sword works the same with no rod.

**Scaling 3-4.** One rod covers the one field. For four players split the ring into two fields
(north half, south half) with the schedule 3 s out of phase and a rod for each: two bodies parked,
two crossing; a Staff makes one rod redundant. Pen nothing; the storm is the enemy.

**Completion and state.** Exit opens when the contacts latch. Host bytes: lightning on (1 bit),
L_QUENCH on (1 bit), contacts latched (1 bit), exit open (1 bit). The strike schedule is the clock.

**Soft-lock and reset.** A crosser struck three times breaks (Sword 100 HP, 40 per bolt) and re-forms
at the rack four doors away (about 2 min): the brute-force crossing is possible but a gamble. A body
left on the rod is left; its player continues as a soul and must be re-bodied by someone bringing a
spare (rare) or stays a scout until the end. No metal body in the party (Staff + Banana): the
contacts cannot close; back to the rack, and the room says so from the door (the posts are
unmistakably a gap in a wire). The ring has no pits.

**Mage moments.** (1) Magnetic curse on the crosser while the rod is within 20 m: his first three
launches curve back toward the parked Mace, "the storm's pulling me". (2) The rod player gets off the
rod "to come help" while the friend is mid-ring. (3) Blindness on the crosser so the 1 s glow is in
the dark. A rod refusal is the most visible stall in the run: everyone can see the empty rod.

**Kit.** Existing: `LightningField`, `ImpactLever`, `HammerStand`, `WoodBlock`, `Door`. New:
`LightningField.on` (host flag, `RequestSetLightning(id, on)`, latched off by a lever wired like the
Winch; 1 bit); `LightningField.rodAnchor` (a weapon resting within 1 m of it is always the one struck,
for 0 damage; no host state); `ContactPair` (two posts, trigger between, latches when a metal weapon
rests across both for `holdSeconds` 1; host family, 1 bit) and `DoorCondition.Mode.ContactsClosed`.

**Loadout.** Needs one metal body. Easier with a Staff (crosses untouched) or a Hammer (140 HP: a
brute-force crossing survives three bolts).

---
## Room 5: BAT ROOM (finale, holds the EXIT)

**Shape.** Round, 12-sided, 60 m across, 24 m high. Entry on the north rim at floor level. A chasm
14 m wide and 6 m deep crosses the room east-west through the middle; its north face is an 18 deg
ramp, its south face is sheer. South of the chasm a 26 m deep apron, wall-backed, and in the south
wall the EXIT doorway: a `Door` that is shut, with a lit 8 x 8 m exit volume behind it (the run's
Exit room is this alcove; `RunDirector.exitRoom` points here).

**Premise.** No launch crosses the chasm; a friend can. The finale uses the one verb the run has
been saving: a heavy weapon bats a braced friend across, and later up. Twist: your body is the ball,
your friend is the bat, and the exit opens the moment the last of you lands where only a thrown
weapon can land.

**Beats.**
1. BAT (about 20 s). From the entry everyone sees the chasm, the apron beyond, and on the south dock
   a winch with a chain running to a ferry platform parked at the south dock. The Sword braces on
   the launch pad (a marked 4 x 4 slab at the chasm's north lip); the Mace launches into it from
   behind at 45 deg; the Sword flies about 20 m and lands on the apron (any landing from 14 to 26 m
   counts). Kit: friend ballistics (existing, needs numbers), pad marker. Litmus: a human could not
   be hit across a canyon by a mace and enjoy it; a possessed weapon braced for the hit takes the
   momentum and flies.
2. FERRY (about 25 s). The Sword hits the dock winch; the ferry (a horizontal lift, new mode) starts
   shuttling north dock, south dock, north dock, dwelling 4 s at each. The Mace boards at the north
   dock and rides across. Kit: `Winch`, `Lift` horizontal. Litmus: the rider is cargo on a slick deck
   and the switch is a launch target on the far side; nothing here is a hand on a lever.
3. UP-BAT (about 20 s). Above the exit door, 7.5 m up on a bracket, lever L_EXIT; three wooden
   posts climb the wall beside it from a 2.5 m ledge (the slow way, blades only). The fast way: the
   Sword braces on the dais pad, the Mace bats it at 80 deg; it rises about 9 m, hits L_EXIT on the
   way, and lands on the dais. Kit: `ImpactLever`, `WoodBlock`. Litmus: a human cannot be thrown
   straight up at a switch 7.5 m above his head by a friend with a mace.
4. OPEN (about 10 s). Beside the exit door, plate P_DOCK (3 kg, latching) on the south dock: any
   body that rode or landed stands on it. L_EXIT on AND P_DOCK latched: the exit door slides open,
   the alcove lights, and everyone who is not the Mage goes in. The run ends on the host's exit
   count. Kit: `Door` with two AND-ed conditions, `ExitZone`. Litmus: the last step is a gathering
   of bodies in a doorway that opened because bodies were thrown at things.

**Interlock.** The heavy cannot cross without the ferry; the ferry's winch is on the far side (or 4 m
up the sheer south face of the chasm, see Solo); the light cannot cross fast without the heavy's bat.
Beat 3 is the same pair the other way up. Asymmetric information: the batter sees the arc preview
and the braced friend sees nothing but the far wall, so the count ("three, two, one") is spoken, and
the one who lands sees the dock and the lever first.

**Solo.** Courier not needed. Drop into the chasm (the north ramp is the way back out), stick a
blade into the post 4 m up the south face, launch into the winch from there (the winch sits in the
south face at y -2, reachable from above by anyone and from below by a blade on its post), ramp
back out, board the ferry, climb the three posts to L_EXIT, stand on P_DOCK, walk in. About 2 min.
A solo blunt cannot reach the winch from below and must go back for a blade.

**Scaling 3-4.** Each light is batted in turn (the batter stays until last and rides the ferry); two
heavies bat two lights at once from two pads. For four players a second lever L_EXIT_B on the east
wall at 7.5 m, both needed (two up-bats or two climbs). The exit volume is 8 x 8 m: four bodies fit.

**Completion and state.** Exit door opens when L_EXIT (and L_EXIT_B at 4) are on AND P_DOCK is
latched; the run ends when every non-banished non-Mage is inside the alcove. Host bytes: winch on
(1 bit), ferry on (1 bit) + start clock ms (u32), L_EXIT on (1 bit each), P_DOCK latched (1 bit),
exit door open (1 bit). Exit occupancy is measured by the host, not stored.

**Soft-lock and reset.** A bat that misses drops the friend into the chasm: ramp out, 20 s. A body
broken here re-forms at the rack five doors away (about 2.5 min): the finale must not kill, so the
chasm is 6 m deep with no fall damage and no goblins. The ferry off-state parks it at the south dock,
never mid-chasm. A heavy that refuses to bat is a visible stall and the slow winch route exists. No
heavy in the party: the chasm post + winch route and the post climb to the lever still work, slowly;
no blade and no heavy (Staff + Banana) cannot reach the winch at all and must go back.

**Mage moments.** (1) The batter whiffs: "I slipped off the pad." (2) The Mage nudges the batted
friend mid-flight (he is airborne for over 2 s, 12 m LOS from the north lip) into the chasm: this is
the cleanest "sorry" in the game. (3) Heavy curse on the batter: a half-speed Mace bats a friend
exactly halfway across. (4) Blindness on the rider as the ferry leaves. The finale is also where the
timer is lowest and a vote costs 25 s nobody has.

**Kit.** Existing: `Winch`, `Lift`, `ImpactLever`, `WoodBlock`, `Plate`, `Door`, `ExitZone`. New:
`Lift` horizontal mode (use the stops' full position, off = the far stop; host state as today);
friend-ballistics tuning (`batSpeedBraced` 20 m/s along the batter's launch direction while the
target holds the launch key, unbraced a nudge's worth; no host state, it is a velocity change on the
target like a nudge); `Door` with two AND-ed conditions (the OR-door of Room 1 with a mode switch).

**Loadout.** Needs one heavy and one light (Sword, Dagger or Banana). The Banana is the best ball
(light, bouncy, 8.5 m of its own range), the Hammer the best bat.

---
## OVERVIEW

**The arc.** Room 1 teaches the handshake with no enemies and no height: hold, pin, weigh. Room 2
turns the handshake vertical and makes the two roles unlike each other (climber, rider). Room 3 adds
the game's strangest truth, that goblins think you are furniture, and splits the information so one
player must trust another. Room 4 puts the two metal bodies into two roles (rod, crosser) and ends on
the body-as-wire. Room 5 spends the saved verb, batting, twice, and the exit door opening is the last
latch of the run. Rooms 1, 2 and 5 are mass-and-mechanism rooms; 3 and 4 are "you are an object"
rooms; the alternation keeps the run from feeling like five plates.

**Verbs in order.** 1 PARK and HOLD (a body is a weight, pin from the far side). 2 STICK and RIDE (a
blade is a piton, a blunt is cargo, the soul swap). 3 PLAY DEAD (goblins carry still weapons; one
side sees, the other acts). 4 BE THE ROD and THE WIRE (metal draws bolts, metal closes circuits, the
soul scouts). 5 BAT and BRACE (a friend is a projectile, across and then up).

**Start rack (7 slots).** Sword, Sword, Dagger, Mace, Hammer, Staff, Banana. The canonical pair Sword
+ Mace clears every room; the second Sword is the spare for a broken blade; the Dagger is the fast
climber and the best sneaker; the Hammer is the plate-and-rod body and the strongest bat but climbs
nothing; the Staff is Room 4's shortcut; the Banana is the ball. Nothing in a room hands out a
weapon, and a party that brings two blunts learns it in Room 2 one door from the rack, not in Room 5.

**Time budget (two competent players, no Mage).**

| Room | Beats | Pair | Solo | Door transit |
|---|---|---|---|---|
| 1 Gate | hold, pin, weigh | 60-75 s | ~150 s | 5 s |
| 2 Well | climb, winch, weigh | 60-75 s | ~120 s | 5 s |
| 3 Porter | sneak, ride, lever | 60-80 s | ~120 s | 5 s |
| 4 Lightning | rod, cross, quench, conduct | 75-95 s | ~120 s | 5 s |
| 5 Bat | bat, ferry, up-bat, open | 70-90 s | ~120 s | - |
| Total | | **5:45-7:00** | ~10:30 | +20 s |

The run's 5:00 timer (`RunDef.timerSeconds`) cannot hold five 60-120 s rooms. Recommend **8:00** for
the MVP (the brief's own arithmetic: five rooms at 90 s is 7:30 before the Mage does anything), then
tune down once measured. A four-player party is not faster per room (more bodies to move) but has
more hands for the Mage to meddle with, so 8:00 is the number for every party size.

**Rules every room obeys.** Grey-box names (Gate, Well, Porter, Lightning, Bat). Every beat visible
from the door or found within 20 s; chains and wires show cause and effect without a sign. No
precision: landings are 4 m slabs or wider, posts are wall-backed, trenches have 18 deg ramps. No
shared physics props: the only things that move are bodies, kit on the host, and clock-driven
movers. Nothing on a schedule but the clock (storm, lift, ferry). Host state per room is under 8
bytes plus one u32 clock stamp. Every solution latches except the two deliberate hold states (the
hold plate, the porter gate), and each of those has a latching way round it on the far side.

## RESEARCH TAKEAWAYS

1. Nicholson's facility survey (Peeking Behind the Locked Door): most rooms are linear or path-based
   rather than open, path-based rooms keep a group of four or five busy, and "I had nothing to do" is
   the common complaint. Used as: each room is linear in its gate but its beats run in parallel for two
   roles (the holder watches the gate, the rod's soul scouts, the batter counts down), and 3-4 scaling
   adds parallel jobs (second winch, second field, second lever), never a longer queue.
2. Nicholson's Ask Why: every element must belong to the room's logic. Used as: every mechanism is an
   expression of "weapons are objects" (plates weigh them, goblins carry them, bolts hit metal, a sword
   is a wire). No decoration pretends to be a puzzle.
3. Red herrings frustrate and make players distrust the room (Nicholson; Codex rule 13). Used as: the
   only wrong lever in the run (Room 3's pen) has its chain in plain sight; the trap is a readable
   penalty, and the misdirection comes from a player, not the room.
4. The "aha" must be discovered, not read. Used as: signs are placeholders only; each room's twist is
   physical (the chain runs to the other side; the lift is off and the winch is up there; the porter
   picks up the still one; the empty rod; no launch crosses but a friend does).
5. Codex rules 4, 9 and 10: self-validating answers, no permanent failure, clear feedback. Used as:
   every latch has a visible state (gate slides and stays, pan sinks, field goes quiet, ferry starts),
   the HUD door prompt is the only hint text, ramps lead out of every pit, bodies re-form, the pen is
   recoverable, the ferry and lift park at a dock and never mid-way.
6. Codex rule 7, under five minutes per puzzle, and the survey's note that long gaps without
   progress kill morale. Used as: 60-120 s rooms, three or four 20-25 s beats, and a failure cost (rack
   round trip) that grows with depth so the early rooms are forgiving.
7. Portal 2 co-op: a chamber went back to the designer if a tester finished it with one portal pair,
   and the ping tool plus countdown made simultaneous actions possible. Used as: the fast path of
   every room needs two bodies, the solo path is a slow courier-and-swap path by design, and the
   bat and the lift boarding are spoken countdowns on voice.
8. Keep Talking / We Were Here: information held by one player and action held by the other forces
   talk, and talk is where a hidden traitor lives. Used as: Room 3's chains, Room 2's lift timing,
   Room 4's soul scout and "it wants metal", Room 5's arc preview that only the batter sees. The Mage's
   best weapon in these rooms is a confident wrong sentence.
9. Hazelight (It Takes Two, Split Fiction): one new mechanic per level, never recycled, saved for the
   end where it matters. Used as: one verb per room, batting held back to the finale.
10. Codex rule 12, the first draft is always too hard. The numbers most likely wrong: bat speed and
    brace window, Room 2 post spacing for the Sword, the storm period against a brute-force crossing,
    the porter's 2 s stillness under a hostile patrol.
