# Rooms v3: the five rooms revised to the owner's 2026-10-02 notes (text + floor plans)

Status: DESIGN ONLY, nothing built. Supersedes `rooms-v2.md`. Floor plans: `rooms-v3-plans.html`
(same folder). Sources: `docs/PREMISE.md` (2026-09-29 simplification, 2026-10-01 voting, 2026-10-02
THE FIVE ROOMS, which overrides v2 wherever they differ), `docs/KIT.md`, `docs/RUN.md` section 8.

## Conventions and numbers used everywhere

- g = 20, lift 15-80 deg, every launch travels forward. Range = v^2 sin(2a) / g, apex = (v sin a)^2 / 2g,
  reach at a rise h = (v / g) sqrt(v^2 - 2gh). Launch speeds: Dagger 14, Banana 13, Sword 12, Staff 11,
  Orb 10, Mace 10, Hammer 9. Masses: Banana 0.5, Dagger 1, Staff 2.5, Sword 3, Orb 4, Mace 8, Hammer 14.
- Range at 45 deg: Dagger 9.8 m, Banana 8.45, Sword 7.2, Staff 6.05, Orb / Mace 5.0, Hammer 4.05.
  Apex at 80 deg: Dagger 4.75, Banana 4.10, Sword 3.49, Staff 2.93, Orb / Mace 2.42, Hammer 1.96.
- Hopping up a slope of angle t: best lift is 45 + t/2 and the hop covers 2 v^2 cos(a) sin(a - t) /
  (g cos^2 t) along the slope. On 20 deg: Sword 5.4 m, Mace 3.7, Hammer 3.0, Heavy-cursed Mace (5 m/s)
  0.93 per hop. Every ramp in the run is 20 deg or less.
- A blade sticks in wood at 6 m/s or more, point first: a Sword still has 6 m/s at +2.7 m, a Dagger at
  +4.0 m. A rope is cut at 5 m/s or more: a Sword cuts up to +2.98 m above its launch.
- A wall jump must REBOUND (dot with the wall normal above 0.3), so it climbs at most 4.4 m more
  (Dagger at 72 deg) while moving away from the wall: nothing can wall-jump onto the top of the wall it
  jumped from. The only risk is a ledge beside a wall within about 2 m of the jump, so every balcony
  end in these rooms has a 2 m parapet and the Porter Room's partition is 9.5 m. A Dagger's theoretical
  ceiling from a flat floor is 9.2 m.
- Mandatory jumps carry a 20 percent margin for the weakest weapon that must make them; every pit floor
  leaves by a 20 deg slope or a level corridor; no mandatory landing higher than the weapon's apex less
  20 percent (Hammer 1.57 m, Mace 1.94, Sword 2.79, Dagger 3.80).
- SOULS PASS OPEN DOORS (closed and locked doors still block). So the one-player path of every room is
  the FETCH: release (Q) out of combat, fly back through the open doors to the rack at 7 m/s, bring
  the other body forward. A fetch from room n costs about 25 n seconds (10 s of soul flight per room
  each way plus 12-15 s of hopping per room with a Mace). The v2 "courier launch" is gone.
- Canonical pair A = Sword, B = Mace; every room is solvable by it. Entry is the north doorway, exit the
  south doorway (RUN.md section 8). A launch is about 1.5 s. Grey-box names only.
- Grey-box READABILITY (owner, global): every functional object is built from primitives that say what
  it is. Each room ends with a readability table.

---

## Room 1: ARMORY GATE

**Shape.** Long gallery, 56 x 18 m, 9 m high, long axis north-south. Entry in the north wall, exit in
the south wall at the top of the trench's far ramp. Everything is visible down the hall from the door.
A cross-wall at z 12 (full height) holds the portcullis G1 in a 3 x 3.5 opening.

**Premise.** A hall barred by one portcullis and one trench. A parked body is a weight; a gate held
from one side can be pinned from the other; a trench is nothing to fear. Twist: the key to the first
gate is a friend sitting still, and the key to the exit is both of you standing in the hole together.

**Beats.**
1. HOLD (about 15 s). Visible from the entry: hold plate P1 (3 x 3 m, 8 kg, NOT latching) beside the
   approach to G1, with a chain from the plate's edge up the cross-wall, over a pulley wheel at the
   lintel and down to the portcullis's top. The Mace (8) lands on P1; G1 slides up and stays up only
   while the Mace stays. The Sword launches through. Kit: hold plate (`Plate.latching` false), `Door`
   as a portcullis. Litmus: a human cannot leave himself behind as a weight; here the body is the weight
   and the player waits inside it.
2. PIN (about 15 s). Through G1 the Sword sees lever L1 on the cross-wall's far face, 1.5 m up, with
   G1's second chain running from the gate's top along the wall to it. Any impact at 4 m/s flips L1
   and latches G1 open. The Mace leaves P1 and follows. Kit: `ImpactLever` (latching), `Door` with two
   OR-ed conditions (PlateHeld OR LeverOn). Litmus: a human pulls a lever by hand; a weapon throws itself
   at it, which is a launch you can miss and a friend can nudge.
3. WEIGH IN (about 30 s). From z 22 to z 46 the hall is cut by a trench 24 m long, 3 m deep, full width:
   a 9 m ramp down (18.4 deg), a 6 m flat floor holding plate P2 (3 x 3, 10 kg, latching), a 9 m ramp up
   to the exit. Sword (3) alone nothing, Mace (8) alone nothing, both (11) latch it; the exit opens for
   good. Kit: `Plate`, `Door_Plate`. Litmus: a human cannot weigh himself with a sword to make up the
   shortfall; two possessed bodies are two weights and the sum is the puzzle.

**Interlock.** The holder cannot be the goer (beat 1); the exit needs both masses at once (beat 3).
Asymmetric information: only the player beyond G1 sees L1 and its chain, so the holder is told "there is
a lever, I am pinning it, you can get off" by voice (or sees the gate stay up when the friend leaves P1).

**One-player path (fetch).** Enter as the Mace, sit on P1, Q (the body stays, the gate stays up), fly
through the open gate and back through the entry door to the rack, possess the Sword, hop in, through
G1, hit L1, Q, fly to the Mace, bring it down onto P2, Q, fly to the Sword, bring it onto P2 (11 kg),
exit as the Sword. About 2:20. With a Hammer (14): hold, fetch the Sword, pin, fetch the Hammer, P2
alone latches, exit as the Hammer, about 2:00.

**Scaling 3-4.** P1 takes one heavy; P2 at 10 kg is any two bodies of the party, so the Mage cannot
stall it by staying out. Four players: a second portcullis G2 across the trench's far ramp with its own
hold plate P1b on the trench floor (a second hold-and-pin, the holder now in the hole).

**Completion and state.** Exit opens when P2 latches. Host bytes: P1 pressed (1 bit, live), L1 on
(1 bit), G1 open (1 bit), P2 latched (1 bit), exit open (1 bit).

**Soft-lock and reset.** A body broken here re-forms at the rack one door away (about 30 s). A Mace
parked on P1 and abandoned is still a weight. No heavy in the party: P1 cannot be held and the hall says
so at a glance; fetch the Mace or Hammer (one door). A shutting G1 shoves a body out of the opening,
never crushes. The trench ramps are 18.4 deg: a Heavy-cursed Mace still climbs them (0.95 m a hop).

**Mage moments.** (1) The holder steps off P1 while the friend is in the opening. (2) Nudge the friend's
launch at G1 into the jamb. (3) Heavy curse on the friend as he drops into the trench: 20 s of hopping
up the ramp at a quarter of the distance per hop.

**Kit.** Existing: `Plate`, `Door_Plate`, `ImpactLever`, `Door`, `DoorPrompt`. New: `Plate.latching`
false (reports pressed / released, host family, no latch); `Door` with a second OR-ed `DoorCondition`
(PlateHeld OR LeverOn, host state unchanged: one open flag); `Door_Portcullis` prefab variant (the
panel is a bar lattice and `openOffset` is (0, 3.6, 0), nothing else changes).

**Loadout.** Needs one body of 8 kg or more. Easier with a Hammer (latches P2 alone) and a Dagger.

**Readability (grey-box primitives).**

| Object | Built from | Reads as |
|---|---|---|
| Portcullis G1 | one box collider 3 x 3.5 x 0.3 behind a lattice of 7 vertical bars (0.15 sq) and 3 horizontal bars; slides UP 3.6 m into a slot in the cross-wall | a gate of bars that lifts |
| Chain P1 to G1 | a run of 0.3 m box links alternating 90 deg, from the plate's edge up the wall to a pulley wheel (0.8 m cylinder, axis horizontal, on a bracket at the lintel) and down to the gate's top | a chain over a pulley lifts the gate |
| Chain G1 to L1 | same links from the gate's top along the far face of the wall to the lever | the lever holds the gate |
| Hold plate P1 / plate P2 | 3 x 3 x 0.2 slab sunk flush in a 0.1 m recess, green while pressed / latched | a pressure plate |
| Lever L1 | 0.3 m post with a 1 m handle that swings -35 to +35 deg, red off / green on | a lever |
| Trench | the floor itself drops: two 9 m ramps and a flat floor, no railing | a ditch with sloped sides |
| Exit door | the standard `Door` panel with a `DoorPrompt` ("PLATE 2  NEEDED") | a locked door |

---

## Room 2: WELL ROOM

**As built (2026-10-02 revision; owner: shrink the lift a quarter, no clipping, no gaps).** Platform
**19.5 x 19.5 m** (was 26), balconies at **7 m** (was 9), swing = asin(7 / 19.5) = **21.0 deg** so the dropped
north edge lands on the floor; north balcony 8 m deep (the hop from the platform's north edge to it is 5.7 m:
Sword 7.2, Dagger 9.8, Mace 5.0 cannot); frames 8 m tall; no end parapets, the octagon's corner walls are the
balcony ends. The octagon is now a true regular one: corner walls centred 20 m from the room centre along the
diagonals (x, z = +-14.14), 16.57 m long plus 1.4 m buried in the straight walls, so nothing clips and nothing
gaps. The text below keeps the original numbers.

**Shape.** Octagon, 40 m flat to flat, 14 m high: an elevator shaft. Entry in the north flat at floor
level. Exit in the south flat **9 m up**, on the SOUTH BALCONY (6 m deep, the full flat, 2 m parapets
at its ends). A second balcony, the NORTH BALCONY (4 m deep, 2 m end parapets), sits above the entry
at 9 m with nothing on it but the rope's cleat. The PLATFORM, a 26 x 26 m square slab, hangs level at
9 m, its south edge 0.8 m from the south balcony's inner edge, so at rest it plugs the shaft like a
parked car. Four wooden GUIDE FRAMES stand against its four edge midpoints, one per side: two 0.9 m
uprights 3.6 m apart (centre to centre) joined by a crossbar at 10 m. Above the platform's north and
south edge midpoints hang two PULLEY WHEELS on a ceiling beam: the north wheel carries a ROPE, the
south wheel a CHAIN. Each runs from a yoke at the platform's edge up over its wheel and down at a slant
to a cleat on the wall above its balcony, 1 m over the balcony floor.

**Premise.** The way out is upstairs and the elevator is stuck at the top. Nobody switches it on: a
blade climbs the guide frame, cuts the rope that holds the entry-side edge, and the platform swings
down into a 26 m ramp from the entry floor to the exit balcony. Blades climb; blunts ride the ramp; the
exit needs the heavy. Twist: a sword climbs by stabbing itself into the posts, and the elevator is
"fixed" by breaking it.

**Beats.**
1. CLIMB (about 15 s). From the entry the blade sees the north frame 6.4 m in front of it, and the
   north balcony 2.4 m behind the frame. It launches into the inner face of one upright at +2 m and
   sticks, launches across to the other upright at +4, back at +6, across at +8, and from +8 onto the
   balcony at 9 m (2.4 m across, +1 up). Per hop: 3.6 m across, +2 m up. Sword reach at +2 m is
   4.8 m (1.33x the 3.6 needed), arrival speed 8 m/s (1.33x the 6 needed to stick) and the arc
   arrives near its apex, almost level, so it is point-first. Dagger reach 7.5 m. Mace and Hammer bounce
   off wood. Kit: `WoodBlock` x 3 per frame. Litmus: a human cannot climb by being stabbed into the
   posts; the weapon's own point is the piton.
2. CUT (about 5 s). On the north balcony the rope slants down from the wheel to its cleat on the wall,
   1 to 2.7 m above the balcony floor. The blade launches into it at low lift (12 m/s against the 5
   needed) and cuts it: the north edge falls, the platform swings about its south edge (the chain holds
   that edge for ever) and lands as a 20.3 deg ramp, foot at the floor 8.6 m south of the entry door,
   top flush with the south balcony. 1.2 s fall. Kit: `Rope` + `RopeSegment` (the whole rope, yoke to wheel to cleat, is cuttable), `DropRamp` (Swing, 20.3 deg, 26 m deck).
   Litmus: a human would look for the lift's button; the weapon can only cut, and only a blade cuts
   (a Mace thrown at the rope bounces off, KIT rule).
3. RAMP AND WEIGH (about 25 s). The heavy hops up the ramp (Mace 3.7 m a hop, 7 hops; Hammer 3.0,
   9 hops), crosses the 0.8 m gap onto the south balcony and sits on plate P (3 x 3, 8 kg, latching)
   beside the exit door; the exit latches open. The blade drops off the north balcony (no fall damage)
   and takes the ramp too (Sword 5.4 m a hop). Kit: `Plate`, `Door_Plate`, elevated doorway. Litmus: a
   human carrying a mace weighs more than 8 kg anyway; here only the body counts, so the blade that got
   up first is useless at the door.

**Interlock.** Symmetric: the blade cannot press the plate, the heavy cannot climb or cut. Asymmetric
information: only the climber sees the cleat and that the rope is a rope and the chain a chain; the
heavy below sees the platform's underside, the slanting cords and the frames, and must stand clear of
where the edge lands ("I'm cutting, get off the north side").

**One-player path (fetch).** Enter as the Sword, climb, cut, drop to the floor, Q, fly back through the
open doors to the rack (two rooms, about 50 s), bring the Mace forward (about 30 s), up the ramp, sit on
P, exit as the Mace. About 1:50. The Sword stays in the Well Room; the run does not need it again
unless Room 4 is reached with only one metal body (then a second fetch).

**Scaling 3-4.** Two blades climb two frames (the north and the east or west; a frame takes two stuck
blades, one per upright face). All heavies take the ramp. Four players: the exit plate wants 16 kg
(two heavies, or a Hammer + anything) so the second heavy has a job; the ramp is 26 m wide, nobody
queues.

**Completion and state.** Exit opens when P latches. Host bytes: rope cut (1 bit) + cut clock ms (u32,
the ramp's pose is a pure function of it), P latched (1 bit), exit open (1 bit).

**Soft-lock and reset.** A climber nudged off an upright falls to the flat floor (no pit). A broken
body re-forms at the rack two doors away (about 60 s). A blade stuck in an upright and abandoned by
its soul is still a foothold. No blade in the party: the frames are mute and the rope stays; fetch a
blade. No heavy: the plate never fires (Staff 2.5 + Sword 3 + Orb 4 = 9.5, fetch a Mace). The chain
cannot be cut, so there is no wrong rope and the ramp can never fall flat. A body standing where the
north edge lands is shoved, never crushed (DropRamp rule). The parapets (2 m) keep a Dagger's 9.2 m
wall-jump ceiling off the balconies; the frames are the only climb.

**Mage moments.** (1) Nudge the climber between uprights: a fall to the floor and a 15 s re-climb,
"gust of wind". (2) Heavy curse on the climber: at 6 m/s the Sword's reach at +2 m is 1.2 m, it cannot
make a single hop for 20 s and everybody watches. (3) The heavy "waits for the ramp" under the falling
edge and gets shoved across the floor. (4) Heavy curse on the heavy on the ramp: 0.93 m a hop, 28 hops.

**Kit.** Existing: `WoodBlock`, `Rope`, `DropRamp` (Swing mode; hinge = the platform's south edge at
y 9, deck 26 m along -Z, `swingDegrees` 20.3, `fallSeconds` 1.2), `Plate`, `Door_Plate`, elevated
doorway (KIT.md "Moving an authored doorway up a wall"). New: none. Geometry only: the chain, the
wheels, the beam, the cleats, the balconies, the frames (`WoodBlock` scaled).

**Loadout.** Needs one blade and one body of 8 kg or more. Easier with a Dagger (climbs in four
hops) and a Hammer (sits still on the plate).

**Readability (grey-box primitives).**

| Object | Built from | Reads as |
|---|---|---|
| Platform | 26 x 26 x 0.4 slab, dark planks tint, a 0.5 m wooden yoke beam along the north and south edges with a ring at its middle | an elevator car |
| Guide frame (x4) | two 0.9 x 0.9 x 10 wooden uprights 3.6 m apart touching the platform's edge, a 0.5 x 0.5 x 4.5 crossbar on top | the rails the car runs on, and a ladder-shaped thing a blade can zigzag up |
| Pulley wheel (x2) | 1.2 m cylinder, axis horizontal E-W, 0.3 thick, on a 0.5 sq ceiling beam that spans the shaft N-S at 13.5 m | a wheel with a cord over it |
| Rope (north) | 0.12 m tan cylinder: vertical from the yoke ring to the wheel, slanting from the wheel to the cleat; after the cut two frayed stubs (the kit's cut ends) | a rope |
| Chain (south) | 0.3 m dark grey box links alternating 90 deg, same path on the south side, to an iron ring | a chain: not cuttable |
| Cleat (north) | a 0.6 m horizontal bar on two 0.2 m brackets on the wall 1 m above the balcony, the rope's end wound round it | where the rope is tied off |
| Balconies | 0.5 m thick slabs, 2 m parapet walls at the ends only (the inner edge is open) | a landing at the top of the shaft |
| Plate P / exit door | as Room 1 | a plate beside a locked door |

---

## Room 3: PORTER ROOM

**As built (2026-10-02 revision, owner's version).** Simpler and meaner than the text below, which is kept for
the record. An OPEN HALL (16 x 35) with TWO ordinary goblins to kill; behind them the partition with a big
GRATE DOOR (bars on the hall side; a `PorterGate` that nobody opens but lever L_DOOR inside). Bang the bars
(any weapon hitting the grate at 3 m/s or more) and the porter comes over from the OTHER side and stands at
the bars for 8 s. A Dagger (or Banana: 1 kg or less) lying still within 3.5 m of the bars it reaches through,
pulls to its side and carries to the stand inside; anything heavier gets "a shame I can only fit a dagger
through the bars" over its head. The Dagger then launches into L_DOOR (3 m away) and the grate lifts for good;
the exit opens when the hall is cleared (both goblins dead). Dropped: the patrol, the pen, the sleepers, L_PEN
and the two shield guards. The porter cannot leave its room while the grate is shut (the panel carves the
NavMesh).

**Shape.** A hall 40 x 16 m (north-south, entry in the north wall) ending in a PARTITION 9.5 m high
under an 11 m roof (a 1.5 m slot above it: chains and souls pass, no weapon does). Beyond the
partition the CONTROL ROOM, 12 x 10 m, with the exit door in its south wall. The only way through the
partition is the CONTROL DOOR at its centre: a 3 x 3.5 slab that is a `PorterGate` (slides up only for
a carrying porter) and also lifts for good when lever L_DOOR inside is on. In the hall's east wall at
z 24-28 a PEN, a 4 x 4 alcove behind a barred gate, two sleeping goblins inside. Two chains come over
the partition's top from the control room and END in the hall: the west one drops to a wheel at the
control door's jamb and across the lintel to the door's top; the east one runs along the partition's
hall face to the east wall and north to the pen gate's top.

**Premise.** Goblins think you are furniture. The porter who keeps the control room tidy is the only
thing that can pass its door, and it only carries what lies still and what it can lift. A light weapon
gets carried in, lifts the door from the inside, and a heavy one comes in to kill the two armoured
guards; the exit opens when the control room is cleared. Twist: your friend is luggage, the one who can
see which lever is which is outside, and the one who can win the fight cannot get in alone.

**Beats.**
1. SNEAK AND READ (about 20 s). One patrol goblin walks a rectangle in the hall (pause 1 s at each
   corner, 110 deg cone). A weapon seen moving turns it hostile; lying still 5 s makes it forget. A and
   B move only behind its back toward the control door. On the way B reads the two chains: "left chain
   is the door, right chain goes to the pen". Kit: `Goblin` + `PatrolRoute`. Litmus: a human cannot
   stop being a person; a still weapon is furniture and a goblin forgets furniture.
2. LURE AND RIDE (about 20 s). The porter waits inside at its home spot. A weapon LANDING (impact 4
   m/s or more) within 15 m of the control door is the noise that brings it out: it walks out through
   its door (which it opens for itself) to its noticing ground 6 m into the hall and looks round for 8 s.
   A (Sword, 3 kg) lies still 2 s within 8 m: fetched, carried through the door, set on the stand
   inside; the porter walks back to its home facing the door. B (Mace, 8 kg) lying still is tugged at
   and left ("too heavy", `porterMaxMass` 4): the porter never carries a heavy. B stays out of the
   porter's cone. Kit: `GoblinPorter` (+ new lure, max mass, flee), `PorterGate`, `HammerStand`.
   Litmus: a human cannot be picked up and carried through a door by the guard; a still weapon can.
3. LEVER (about 10 s). From the stand A sees two levers flanking the door on the inside, L_DOOR and
   L_PEN, identical, each with a chain climbing the partition. B says which. A launches into L_DOOR
   (4 m): the control door lifts and latches. The two guards see A move and chase; A hops back out
   into the hall (guards never leave their room). L_PEN instead opens the pen into the hall where B is
   waiting: two goblins on B, a 20-30 s mess, not a dead end. Kit: `ImpactLever` x 2, `Door_Lever`
   (pen), `PorterGate.latchLever`. Litmus: a human reads both chains and pulls the right one; a weapon
   must be told, and throw itself, and the teller may be lying.
4. FIGHT (about 15 s). B hops in. The two GUARDS carry a shield plate: only a weapon of 8 kg or more
   touches them (`shieldMinMass` 8, shield 20 HP, body 30 HP), blades and the Staff bounce off for 0.
   Mace: three landings per guard (shield, body, body), six launches, about 10 s, taking two or three
   hits of 15 (120 HP). The control room's volume clears and the exit door opens. The porter never
   fights: seeing an animate weapon it runs to its home and cowers, so it is not part of the clear. Kit:
   `GoblinBoss` role with a small def (`GoblinGuard.asset`), `RoomVolume`, `Door_RoomCleared`. Litmus:
   a human with a sword gets round a shield; a sword thrown at a shield plate just clatters off.

**Interlock.** The porter carries only bodies of 4 kg or less; the guards are hurt only by bodies of
8 kg or more; the door is lifted only from the inside. So the light must ride and the heavy must fight,
and neither can do the other's half. Asymmetric information: the chains' ends are in the hall and
their levers are in the control room, so the rider is told which lever by the reader, who may be the
Mage. The fight is the second asymmetry: A inside sees where the guards stand; B at the door hears
"now, they're both on the left".

**One-player path (fetch).** Sneak, land near the door, lie still, ride, hit L_DOOR (read the chains
first from the hall; a soul can also fly through the slot over the partition to look), hop out, wait
5 s out of combat, Q, fly back three rooms to the rack (about 75 s), bring the Mace (about 45 s), kill
the guards, exit. About 3:00, the slowest solo in the run, because it is the one room built around two
different bodies at once.

**Scaling 3-4.** Patrols = players minus one (max three) on overlapping loops. Guards = players minus
one (max three). Pen goblins = players. One rider is enough; everyone else waits quietly in the hall,
which is where a Mage's "accidental" launch in front of the patrol is deniable. Four players: a second
stand and a second lever pair in the control room (both L_DOOR levers needed: two rides).

**Completion and state.** Exit opens when the control room's volume is cleared (both guards dead). Host
bytes: porter carry (weapon id, u16, existing), L_DOOR on (1 bit), L_PEN on (1 bit), control door open
(1 bit, PorterGate), pen open (1 bit), guard HP x 2 (existing enemy state), exit open (1 bit).

**Soft-lock and reset.** Turning animate in the porter's hands drops you where you are and the porter
flees (the kit's "angers it" becomes "scares it"); a body dropped in the doorway is shoved out by the
shutting door. A body broken by goblins re-forms at the rack three doors away (about 90 s): the costliest
mistake in the run. A weapon parked on the stand stays there (the porter never re-fetches it). Only
lights in the party (Sword + Dagger): nobody can hurt the guards; fetch a Mace. Only heavies (Mace +
Hammer): the porter tugs and leaves both, which the hall shows at once; fetch a Sword. A DEAD PORTER
(players may kill it) is the one true dead end in the run: no weapon reaches the 9.5 m slot and the
door only opens from inside. Two guards against it: the porter flees rather than fights, so killing it
takes deliberate chasing; and it is the only enemy in the run that RE-FORMS, at its home spot, 20 s
after dying (`respawnSeconds`, new, host clock), so the dead end lasts 20 s. Goblins walk through shut
gates (KIT caveat): the pen is off the porter's route and the guards' room volume keeps them inside.

**Mage moments.** (1) The reader says "the left one" and it is the pen. (2) The rider "mis-launches"
into L_PEN: "I slipped off the stand". (3) The heavy Mage "cannot find the door" while A is being
chased inside: A hops out and nothing is lost but time. (4) Blindness on the rider just before the
lever hop; Nausea on a sneaker in the patrol's cone. (5) The Mage kills the porter "in self-defence"
and everyone waits 20 s.

**Kit.** Existing: `GoblinPorter`, `PorterGate`, `PatrolRoute`, `Goblin`, `GoblinSleeper` (pen),
`GoblinBoss` role, `HammerStand`, `ImpactLever`, `Door_Lever`, `RoomVolume`, `Door_RoomCleared`. New:
`GoblinPorter.lureRange` 15 m (an impact of `wakeImpactSpeed` 4 m/s within this range of its
`listenPoint` sends an idle porter out to its noticing ground for `porterLookSeconds` 8; no host state,
the host's porter decides and everyone sees it walk); `GoblinPorter.porterMaxMass` 4 (heavier still
weapons are tugged for 1 s and left; no state); `GoblinPorter.flees` true (an animate weapon in its cone
makes it drop its load and run to its home point instead of attacking; no state);
`GoblinPorter.respawnSeconds` 20 (host clock stamp, u32, the only respawning enemy);
`PorterGate.latchLever` (an `ImpactLever` reference: while the lever is on the gate stays open; state
is the lever's bit); `GoblinGuard.asset` (an `EnemyDef` for the `ShieldBoss` role at body scale 1.0:
`shieldMaxHp` 20, `shieldMinMass` 8, body 30 HP, attack 15, `bladedDamageBonus` 1; no code).

**Loadout.** Needs one body of 4 kg or less and one of 8 kg or more. Easier with a Dagger (quick hops
in the cone's shadow) and a Hammer (two landings per guard).

**Readability (grey-box primitives).**

| Object | Built from | Reads as |
|---|---|---|
| Control door | 3 x 3.5 x 0.3 slab in a frame, a chain from its top centre up to a wheel on the lintel and over the partition; slides UP 3.6 m | a heavy door lifted by its chain |
| Partition | 9.5 m wall, roof at 11 m, the 1.5 m slot open; two chains visibly go over its top | a wall you cannot get over, that the chains do |
| Lever L_DOOR / L_PEN | as Room 1's lever, both red, both with a chain going straight up the wall | two identical levers |
| Pen | 4 x 4 alcove, a bar lattice gate (Door_Lever variant of the portcullis) with 0.4 m gaps you can see the sleepers through | a cage with goblins in it |
| Guards | the boss prefab at scale 1.0 with its 1.2 x 1.2 shield plate on the front and the shield bar above the body bar | a goblin with a shield |
| Porter | the porter prefab (its carry socket above the head) standing at its home spot facing the door | a goblin that carries things |
| Stand | `HammerStand` (a 1 m post with a cradle) 6 m inside the door | where carried weapons are put |
| Noticing ground | a 3 m disc of lighter floor 6 m north of the control door | where the porter looks |

---

## Room 4: CIRCUIT ROOM

**Shape.** Rectangle, 24 x 30 m (east-west by north-south), 8 m high. Entry in the north wall, exit
in the south wall 4 m east of centre. In the south-west corner a LEDGE 2.5 m high, 10 x 8 m. By the
entry on the west side the SOURCE: a glowing cube on a 1 m plinth. From its base a WIRE, a 0.3 m rail
on the floor, runs west to the wall, south along the west wall to GAP 1 on the floor at z 14, on south
to the ledge, up its north face, across the ledge's top to GAP 2 at z 26, down the ledge's east face
and east along the south wall to the TERMINAL beside the exit door. A gap is two 0.4 m pads 0.5 m
apart. The wire glows as far as the current reaches: from the door you see it lit up to gap 1 and dark
after it, and both gaps, and the ledge, and the dark terminal at the door.

**Premise.** Power from the source opens the door, and the wire to the door is broken in two places.
A body is a wire: a metal weapon lying across a gap closes it; the door needs both gaps closed at the
same moment. Twist: your friends are the missing pieces of the circuit, and the Staff, who can go
anywhere, is no use at all.

**Beats.**
1. LOW GAP (about 15 s). The Mace hops to gap 1 (12 m from the entry) and lies across its two pads
   (any metal weapon at rest touching both pads closes the gap: Dagger, Sword, Mace, Hammer; the
   Staff and Banana do nothing, the segment stays dark). The wire lights from gap 1 to gap 2 and no
   further. Kit: `CircuitGap` (new). Litmus: a human cannot be the wire between two terminals; a mace is
   a bar of metal.
2. HIGH GAP (about 20 s). The Sword hops onto the ledge (2.5 m: Sword apex 3.49, 1.4x; Dagger 4.75;
   a Mace at 2.42 and a Hammer at 1.96 cannot) and lies across gap 2. The whole wire lights to the
   terminal; after 1 s with both gaps closed the door's bolt pulls and the exit LATCHES open. Both get
   up and go. Kit: `CircuitGap`, `Circuit` + `Door` on CircuitClosed (new). Litmus: a human carrying
   both weapons would lay them down and walk away; here each wire is a player holding still, and the
   room is the sentence "hold it... hold it... it's on".

**Interlock.** Two metal bodies at rest at the same time, one of them on a 2.5 m ledge only a blade
(or a Dagger) reaches, so with the canonical pair the roles are fixed: Mace low, Sword high. A Staff or
Banana in the party changes nothing (not metal). Asymmetric information: small on purpose (the owner
wants this room overt). What is left: the high player cannot see gap 1 from the ledge's far side and
the low player cannot see the terminal; "mine's lit, is yours?" and the Mage saying "it's on, get up"
a second early so the latch never fires.

**One-player path (fetch).** Lie on gap 1 as the Mace, Q (a released metal body still closes the
gap), fly back four rooms for the Sword (about 100 s), hop it in, onto the ledge, lie on gap 2: latch,
exit as the Sword. About 2:30. A solo player who kept both metal bodies moving forward through rooms
1-3 (the two-body shuffle) does it in 40 s.

**Scaling 3-4.** Two gaps for 2-3 players (never more simultaneous bodies than non-Mage players, so
the Mage cannot stall by refusing). Four players: a third gap on a 1.5 m shelf on the east wall
(Hammer reaches 1.57) so three bodies are needed and the fourth is free; a parked body counts, so a
banished or refusing player's body is still a wire once someone soul-swaps into it.

**Completion and state.** Exit latches when every gap has been closed together for 1 s. Host bytes:
gap 1 closed (1 bit, live), gap 2 closed (1 bit, live), exit open (1 bit, latched). The wire glow is a
view of those bits.

**Soft-lock and reset.** No pits, no enemies. A body broken elsewhere re-forms at the rack four doors
away (about 2 min) but nothing here breaks bodies. No metal body in the party (Staff + Banana): the
gaps cannot close and the room says so from the door; fetch. Two blunts (Mace + Hammer): nobody reaches
the ledge; fetch a blade. One metal body only: the fetch above. A body left lying on a gap is left; the
latch has already fired, so nothing is lost.

**Mage moments.** (1) "Lie" on a gap with one pad short, so the segment stays dark: "it's not taking".
(2) Get up at 0.9 s. (3) Magnetic curse on the Sword while the Mace lies on gap 1: its hops to the ledge
curve back toward the Mace, "the current's pulling me". (4) Nudge the Sword's 2.5 m hop off the
ledge's edge. All of them cost seconds, none of them cost bodies.

**Kit.** Existing: `Door`, `DoorPrompt` ("CIRCUIT OPEN"), `Ledge`, `WoodBlock` (none needed). New:
`CircuitGap` (two pads with one trigger over both; closed while a METAL weapon slower than `restSpeed`
1.5 overlaps both pad contacts; host family, 1 bit live, no latch); `Circuit` (an ordered list of gaps
plus a wire view: segment i glows while gaps 0..i-1 are closed; no state of its own);
`DoorCondition.Mode.CircuitClosed` (all gaps closed for `holdSeconds` 1; the door's open flag latches
as every door does). This is v2's `ContactPair` split into a live gap and a latching door.

**Loadout.** Needs two metal bodies, one of them a Sword or Dagger. Easier with a Dagger (the ledge is
a trivial hop) and a Hammer (lies still).

**Readability (grey-box primitives).**

| Object | Built from | Reads as |
|---|---|---|
| Source | 1 x 1 x 1 cube, emissive cyan, on a 1 x 1 x 1 plinth; the wire leaves the plinth's base | a magical power source |
| Wire | a 0.3 x 0.1 rail on the floor, up walls as a vertical strip, mitred at corners; cyan emissive where live, dark grey where dead | a wire carrying power as far as it glows |
| Gap | the rail ends in a 0.4 x 0.4 x 0.3 pad, 0.5 m of bare floor, another pad; pads glow when the gap is closed | a break in the wire that something metal must bridge |
| Ledge | a 10 x 8 x 2.5 block, the wire climbing its north face and crossing its top | a step up the wire takes |
| Terminal | a 0.6 x 0.6 x 1 block beside the exit door with a 0.3 m glowing bolt line into the door frame | where the wire ends, at the door's lock |
| Exit door | standard `Door`, `DoorPrompt` "CIRCUIT OPEN" / "CIRCUIT CLOSED" | a locked door with a lock the wire feeds |

---

## Room 5: BAT ROOM (finale, holds the EXIT)

**Shape.** Rectangle, 24 x 52 m, 24 m high. Entry in the north wall at floor level (y 0). An entry
floor 8 m deep, then STEPS: four risers of 1.5 m with 3 m treads (z 8 to 20) up to the DAIS at y 6
(z 20 to 26, 21 m wide), the north bank of the CHASM. The chasm is 14 m wide (z 26 to 40), floor at
y 0, full width; its south face is sheer, 6 m. Beyond it the SOUTH BANK at y 6 (z 40 to 52, 12 m
deep) with the EXIT doorway in its south wall and the lit 8 x 8 exit volume behind it (the run's Exit
room is this alcove; `RunDirector.exitRoom` points here). Along the west wall a 3 m CORRIDOR at y 0
runs from the entry floor past the steps and under nothing to the chasm floor: the way back out of the
chasm, level the whole way. On the south bank's lip, in the west half (x -8 to -2), the DRAWBRIDGE
stands upright: a hinged slab 14.5 m long and 6 m wide, hinge at the lip. Beside it, 4 m onto the
bank, lever L_BRIDGE on a post. On the dais's east half the BRACE PAD, a marked 3 x 3 slab at the lip.
In the chasm's south-east corner a wooden GUIDE FRAME (two uprights 3.6 m apart, 1 m out from the
face, 7 m tall) for the one-player path.

**Premise.** No launch crosses the chasm; a friend can. The finale spends the one verb the run has been
saving: a heavy bats a braced friend across, the friend drops the drawbridge, everyone walks over. Twist:
your body is the ball, your friend is the bat, and the run ends on a bridge that fell because a sword
was thrown at a lever.

**Beats.**
1. STEPS (about 10 s). Everyone hops up four 1.5 m risers to the dais (Hammer apex 1.96, 1.31x; a
   Heavy-cursed Hammer at 0.49 waits out its 20 s on a tread). From the dais the whole room is
   readable: the chasm, the upright bridge on the far lip, the lever beside it, the exit beyond. Kit:
   `Step`. Litmus: none, this is the walk to the window.
2. BAT (about 15 s). The Sword braces on the pad (holds the launch key, 1 m from the lip); the Mace
   launches into it from 1.5 m behind at 45 deg. The Sword leaves at 20 m/s along the Mace's launch
   direction: 20 m at 45 deg, 18.8 m at 35 or 55, against the 14 m needed (1.43x), and lands on the
   south bank (any landing from 14 to 26 m counts; the bank is 12 m deep). The Mace STOPS DEAD at the
   hit (it gave its momentum away) and drops onto the pad. A miss (Mage nudge mid-air, a 15 deg bat)
   drops the friend 6 m into the chasm, no damage, west corridor, steps, again. Kit: friend ballistics
   (existing, numbers new), pad marker. Litmus: a human could not be hit across a canyon by a mace and
   enjoy it; a possessed weapon braced for the hit takes the momentum and flies.
3. LEVER (about 5 s). The Sword launches into L_BRIDGE (4 m/s): it latches on, the bridge's catch lets
   go and the slab swings down 90 deg (2 s) to land flat on the dais's west half, lip to lip; the exit
   door slides open with it. Kit: `ImpactLever` (latching), `DropRamp` (Swing 90, released by a lever),
   `Door_Lever`. Litmus: a human pulls the lever; a weapon throws itself at it and the Mage can nudge
   that throw.
4. CROSS (about 15 s). The Mace hops over the bridge (14.5 m, three hops) and everyone who is not the
   Mage goes into the exit alcove. The run ends on the host's exit count. Kit: `ExitZone`. Litmus: the
   last step is a walk over a bridge that fell because bodies were thrown at things.

**Interlock.** The light cannot cross without the heavy's bat; the heavy cannot cross without the
bridge; the lever is on the far side. Asymmetric information: the batter sees the bat preview (a
second arc, the friend's, drawn while a braced friend is in front of it); the braced friend sees only
the far wall, so the count ("three, two, one") is spoken, and the one who lands sees the lever first.

**One-player path.** No heavy, no bat. Drop into the chasm (walk the west corridor back if you miss),
stick a blade into the guide frame's uprights at +2, +4 (the frame is 7 m, the bank 6), and from +4
launch up and over the lip onto the south bank (+2 m over 1 m: Sword apex 3.49, 1.7x), hit L_BRIDGE,
walk in. About 1:00. A solo blunt cannot climb and fetches a blade (five rooms back, about 2 min: the
most expensive fetch in the run, and the rack says so from the start: bring a blade).

**Scaling 3-4.** Each light is batted in turn (the batter stays until last and crosses on the bridge);
two heavies bat two lights at once from two pads (the dais's east half holds two). Four players: a
second lever L_BRIDGE_B on the bank's east side, both needed (two bats or a bat and a climb). The exit
volume is 8 x 8: four bodies fit.

**Completion and state.** Exit door opens when L_BRIDGE is on (and L_BRIDGE_B at four); the run ends
when every non-banished non-Mage is inside the alcove. Host bytes: L_BRIDGE on (1 bit each) + on
clock ms (u32, the bridge's pose is a function of it), exit door open (1 bit). Exit occupancy is
measured by the host, not stored.

**Soft-lock and reset.** A bat that misses drops the friend into the chasm: corridor and steps, 20 s.
A body broken here re-forms at the rack five doors away (about 2.5 min): the finale must not kill, so
the chasm is 6 m deep with no fall damage and no goblins. The bridge's off state is upright, never
half-down. A heavy that refuses to bat is a visible stall and the frame climb exists. No heavy in the
party: the frame climb; no blade and no heavy (Staff + Banana) cannot reach the lever at all and
fetches. The dais is 21 m wide with the corridor beside it, so a body nudged off the dais's west edge
lands in the corridor (y 0, 6 m drop, no damage).

**Mage moments.** (1) The batter whiffs: "I slipped off the pad" (bat at 15 deg: 10 m, into the
chasm). (2) The Mage nudges the batted friend mid-flight (airborne 2.8 s, within 12 m of the north lip
for the first second): the cleanest "sorry" in the game. (3) Heavy curse on the batter: at 5 m/s the
bat still fires at 20 m/s (the bat speed is a rule, not the batter's speed), so this one does nothing,
on purpose: the finale's bat must not be curse-able into the chasm by a Mage who is not the batter.
(4) Blindness on the braced friend so it cannot see where it lands. The finale is where the timer is
lowest and a vote costs 25 s nobody has (the timer pauses, but the Mage's cooldowns do not need to).

**Kit.** Existing: `Step`, `ImpactLever`, `DropRamp`, `Door_Lever`, `ExitZone`, `WoodBlock`. New:
friend-ballistics numbers (`batSpeedBraced` 20 m/s along the batter's launch direction while the
target holds the launch key and is lighter than the batter, unbraced a nudge's worth; `batterStops`
true: the batter's velocity is zeroed at the hit; no host state, it is a velocity change on the target
like a nudge, host-validated the same way); bat preview (local view: the friend's arc drawn for the
batter while a braced friend is within 2.5 m in front); `DropRamp.releaseLever` (an `ImpactLever` as
the release, beside the existing `rope`; the pose is a function of the lever's on clock ms, which the
lever does not keep today: add `ImpactLever.OnMs`, u32, host).

**Loadout.** Needs one heavy and one light (Sword, Dagger or Banana). The Banana is the best ball
(0.5 kg, bouncy), the Hammer the best bat (nothing changes in the numbers, but it looks right).

**Readability (grey-box primitives).**

| Object | Built from | Reads as |
|---|---|---|
| Steps | four 1.5 x 3 x 21 blocks | stairs |
| Chasm | the floor is absent between z 26 and 40 from wall to wall; the dais and bank are 6 m blocks with sheer faces | a gorge between two banks |
| Drawbridge | a 6 x 14.5 x 0.4 slab, two 0.4 m hinge cylinders at its foot on the bank's lip, standing upright; a 0.3 m catch block at its top on a bracket; swings down 90 deg to lie lip to lip | a raised drawbridge |
| Lever L_BRIDGE | the standard lever on a 1 m post, 4 m onto the bank, a short chain from its base to the catch | the lever that releases the bridge |
| Brace pad | a 3 x 3 x 0.1 slab of lighter floor at the dais lip, a 0.5 m arrow block on it pointing south | stand here, face that way |
| Guide frame | two 0.9 x 0.9 x 7 wooden uprights 3.6 m apart, 1 m from the south face, no crossbar | something a blade can zigzag up |
| West corridor | 3 m of floor at y 0 beside the steps and the dais, open to the chasm floor | the way out of the gorge |
| Exit | the standard doorway with the lit 8 x 8 alcove and the `ExitZone` ring | the way out |

---

## OVERVIEW

**The arc.** Room 1 teaches the handshake with no enemies and no height: hold, pin, weigh. Room 2
turns it vertical and makes the two roles unlike each other (climber and cutter, rider and weight) and
ends on the run's one big mechanical event, a 26 m platform swinging down. Room 3 adds the game's
strangest truth, that goblins think you are furniture, splits the information so one player must
trust another, and holds the run's only fight. Room 4 is the breather: overt, quiet, no enemies, two
bodies lying still as wire. Room 5 spends the saved verb, batting, and ends on a drawbridge. Rooms 1, 2
and 5 are mass-and-mechanism rooms; 3 and 4 are "you are an object" rooms; the alternation keeps the
run from feeling like five plates.

**Verbs in order.** 1 PARK and HOLD (a body is a weight, pin from the far side). 2 STICK and CUT (a
blade is a piton and a knife, a blunt is cargo, the first fetch). 3 PLAY DEAD and FIGHT (goblins carry
still weapons; one side sees, the other acts; only mass beats a shield). 4 BE THE WIRE (metal closes
circuits, hold still together). 5 BAT and BRACE (a friend is a projectile; the exit is a bridge).

**Start rack (7 slots).** Sword, Sword, Dagger, Mace, Hammer, Staff, Banana. The canonical pair Sword
+ Mace clears every room; the second Sword is the spare blade and Room 4's second metal body for a
solo; the Dagger climbs fastest and sneaks best; the Hammer is the plate body and the best bat but
climbs nothing and cannot ride the porter; the Staff and the Banana are there to be the wrong answer in
Room 4 and the best ball in Room 5. Nothing in a room hands out a weapon; a party of two blunts learns
it in Room 2 one door from the rack, a party of two lights in Room 3 three doors away (and the hall
shows it the moment the porter tugs at nothing).

**Time budget (two competent players, no Mage).**

| Room | Beats | Pair | Solo (fetch) | Door transit |
|---|---|---|---|---|
| 1 Armory Gate | hold, pin, weigh | 60-75 s | ~140 s | 5 s |
| 2 Well | climb, cut, ramp and weigh | 50-70 s | ~110 s | 5 s |
| 3 Porter | sneak and read, lure and ride, lever, fight | 65-90 s | ~180 s | 5 s |
| 4 Circuit | low gap, high gap | 40-60 s | ~150 s | 5 s |
| 5 Bat | steps, bat, lever, cross | 45-65 s | ~60 s (a blade) | - |
| Total | | **4:20-6:00** (+20 s doors = **4:40-6:20**) | ~10:40 | +20 s |

The 5:00 timer (`RunDef.timerSeconds`) fails the upper bound before the Mage has done anything. A Mage
adds roughly a quarter again (a nudge-fall is 15-20 s, a wrong lever 20-30 s, a curse 20 s) and a vote
pauses the clock, so a pair with a working Mage lands around 6:00-7:30. Recommend **7:00** as the
design's number and **8:00 for the first playtest**, then come down. The rooms were trimmed per the
owner's notes (no up-bat, no ferry, no storm crossing), which is why this is a minute under v2.

**Rules every room obeys.** Grey-box names (Armory Gate, Well, Porter, Circuit, Bat). Every beat
visible from the door or found within 20 s; chains and wires show cause and effect without a sign. No
precision: landings are 3 m slabs or wider, posts come in pairs, ramps are 20 deg or less. No shared
physics props: the only things that move are bodies, kit on the host, and clock-driven movers
(the platform and the bridge are pure functions of a cut / lever clock stamp). Host state per room is
under 8 bytes plus one u32 clock stamp. Every solution latches except the two deliberate hold states
(the hold plate, the porter door), and each has a latching way round it on the far side. A room never
needs more SIMULTANEOUS bodies than the party has non-Mage players, and a released body counts
wherever a body counts (plates, gaps), so a refusing Mage is a stall, never a lock. The only enemies
that must die are Room 3's two guards; the only enemy that respawns is the porter.

## WHAT CHANGED FROM V2 AND WHY

1. **Gate Room is ARMORY GATE.** Rename only; the portcullis gets bars and a vertical slide and the
   chains get links and a pulley wheel so they read (owner: grey-box must be identifiable).
2. **Well Room: cut, do not switch.** The winch and the cycling lift are gone. The lift is a 26 m
   square slab hanging level at the top of a 40 m octagonal shaft on a rope and a chain over two
   wheels, with wooden guide frames at its four edge midpoints. A blade climbs the north frame and cuts
   the rope on the north balcony; the slab swings about its chained south edge into a 20.3 deg ramp
   (the kit's `DropRamp`, which already exists and already follows a `Rope`). Why a rope AND a chain
   rather than four ropes: a rigid slab on three taut ropes cannot sag, and four separately cuttable
   ropes give the Mage a second cut that drops the slab flat and strands the heavy; one rope and one
   chain leaves exactly one thing to cut and no wrong answer. Why frames of two uprights rather than
   single posts: a blade cannot re-stick on the face it just launched from (the launch rebounds), so a
   climb needs two faces to zigzag between; a pair 3.6 m apart is also what an elevator guide looks
   like. Why 40 m across and 9 m up: a 20 deg ramp needs a deck 2.9x its drop, and 9 m is above
   every wall-jump ceiling but the Dagger's theoretical 9.2, which the 2 m end parapets cover. The
   interlock (blade climbs and cuts, heavy weighs) is v2's exactly.
3. **Porter Room: the fight.** The porter is the only thing that passes the control door; it is lured
   out by the noise of a landing, carries only light bodies, never fights, and respawns. Inside: two
   shield guards only mass hurts, so the light rider lifts the door and the heavy comes in to clear the
   room. The two-chain / two-lever asymmetry is kept because the owner's "open the door from the inside"
   is exactly where it lives, and the wrong lever still opens the pen. The partition rose to 9.5 m with
   a slot (chains and souls pass) because a 5 m wall with a lip did not hold against a corner wall-jump
   on paper.
4. **Lightning Room is CIRCUIT ROOM.** No storm, no rod, no quench. A source, a glowing wire, two gaps
   and a ledge; two metal bodies lie across the gaps at once; the Staff and the Banana do nothing. The
   ledge (2.5 m) is what makes the roles fixed with the canonical pair (Sword up, Mace down). v2's
   `ContactPair` became a live `CircuitGap` plus a latching door condition, so the gaps cannot be closed
   one after the other with the same body.
5. **Bat Room: chasm, steps, bat, lever, drawbridge.** The ferry, the winch and the up-bat are gone.
   Entry at floor level with steps up to a 6 m dais (the owner's "steps up the side you enter from");
   the heavy bats the light 20 m over a 14 m chasm; the light hits the lever; the drawbridge (a 14.5 m
   hinged slab standing upright, swung down 90 deg by the existing `DropRamp`) lands lip to lip and the
   exit opens with the lever. The batter now stops dead at the hit, otherwise its own arc carried it into
   the chasm for any pad position that let the friend reach the far bank. The one-player path is a
   wooden guide frame in the chasm's far face.
6. **Souls pass open doors.** Every one-player path is now a FETCH (release, fly back, bring the other
   body); the mid-air courier launch is gone from every room and the solo times are honest flight and
   hop times. The fetch cost grows with depth (about 25 s per room each way), which is why the rack
   wants a blade and a heavy from the start.
7. **Readability.** Every functional object now has a line saying which primitives make it readable:
   bars for a portcullis, a wheel with a cord for a pulley, links for a chain, a glowing rail with a
   visible break for wiring, a hinged slab for a drawbridge, two uprights for a climbable frame.
8. **Time.** 4:40-6:20 for a pair against v2's 5:45-7:00. Timer 7:00 (8:00 for the first test).

## RESEARCH TAKEAWAYS (carried from v2, still the basis)

Path-based rooms with parallel jobs for two roles (Nicholson); every element belongs to "weapons are
objects"; no red herrings (the one wrong lever has its chain in plain sight); the aha is discovered in
the geometry, not read; self-validating latches with visible state; under five minutes a room with a
failure cost that grows with depth; the fast path of every room needs two bodies (Portal 2 co-op);
information held by one player and action by the other is where a traitor lives (Keep Talking); one new
verb per room, batting saved for the end (Hazelight); the first draft is always too hard, so the
numbers most likely wrong are: bat speed and brace window, the Sword's 3.6 m / +2 m post hop and its
near-level arrival, the porter's lure range against the patrol, and whether a Mace settles on a 20 deg
slab.
