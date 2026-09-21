# Pesky Weapons: premise (brainstorm draft, 2026-09-19)

Status: DRAFT from the owner + Claude brainstorm. Nothing here is built to this design yet.
It supersedes DESIGN.md, IMPLEMENTATION-PLAN.md's content plan, level-design/castle-layout.html
and level-design/back-tower-rooms.html as the direction. SLICE-1.md still describes the
movement, soul and goblin mechanics that exist and are kept.

## Pitch
A run-based labyrinth escape for possessed weapons. You move only by launching yourself, you
can leave your body and take another, you need your friends to get anywhere, every floor ends
in a curse draft, everyone wants to be the legend, and at least one of you is a fragment of the
Arch Mage, quietly re-routing his own tower.

## Pillars
- You PLAY it: every role is active, nobody waits to be carried.
- The other players are the content (friend-slop): voice chat, trust, deniable betrayal.
- Only-as-a-weapon verbs: launch-as-movement, soul swap, parked bodies, being an object to goblins.
- Physically separate challenge rooms in a labyrinth that is different every run.
- Grey-box, basic names. Weapons: look + jump only (the Orb rolls; the soul flies).

## A run (about 25 minutes)
1. HUB = the Armoury. Pick bodies from the rack (rack contents depend on party size).
2. A floor = a set of challenge-room wings off the hub, linked by magic doors whose graph is
   shuffled per run. Find and break the floor's seal, find the way up.
3. Back at the hub between floors: ward circle (test a soul or vote to banish), curse draft,
   anvil, map wall, recall bell.
4. Every weapon break feeds the resurrection meter.
5. End: the weapons break the last seal and escape, OR the meter fills, the fragments reunite
   and the Arch Mage hunts everyone to the exit.
6. Epilogue: escaped weapons sit in a shop window; a hero picks the most legendary one.

## The labyrinth
- Rooms are handcrafted modules (non-square, big, may hold several challenges). Doors are magic
  doorways that keep your speed; each shows a glyph of where it leads, and glyphs never lie.
- Every wing has a return door to the hub, so "go back to the starting room" is always possible.
- Room modules declare tags: REQUIRES (bladed / blunt / heavy / metal / wooden, minimum players)
  and BETTER WITH. The generator only picks rooms the run's rack can solve. Seal anchors, enemy
  counts and simultaneity scale with party size.

## Bodies and souls
- Rack by party size: 1-2 players Sword + Mace; 3-4 add Dagger, Hammer, Staff; 5-8 add Shield,
  Banana and duplicates. Always a spare or two. Weapons are NOT provided inside rooms.
- A broken body re-forms on the hub rack after 10 s; the soul flies home. Bodies are never lost,
  so the team can never be stuck without a weapon type. The cost of dying is time, exposure and
  the meter.
- Recall bell (hub): every unpossessed body shatters and re-forms on the rack. Small meter cost.
  Abusable. A banish vote ejects a player from a body they are hogging.

## Co-operation verbs (boosting is only one of them)
Friend ballistics (bat a friend, brace for a super-launch, Staff catapult, Shield trampoline,
blade footholds, rescue bat) - parked bodies as fixtures - bait and freeze / ambush - class
locks (tags) - soul scouting (souls see hidden glyphs and tamper residue; someone guards the
empty body) - simultaneous seal anchors - body courier - shared scarcity (rack, anvil charges).

## The Mage (always at least one)
- The Arch Mage's soul is FRAGMENTED: more players = more fragments, powers split by domain.
  Architect: re-link two doors, seal, loop (only while unobserved, near the door, lying still
  ~3 s, cooldown, budget per floor; the host refuses changes that would cut off the seal or the
  way up). Master: goblin commands (attack me for cover, call a patrol, mark a friend, wake
  sleepers). Hexer: curse meddling (hidden extra pick, reassign, worsen). One Mage alone holds
  all three with smaller budgets.
- Evidence per domain: tamper shimmer visible only to souls + the hub map wall showing what
  changed; goblins that pull their punches on him and porters that never carry him; curse
  assignments that do not add up.
- Fragments do not know each other; two fragment souls out of body near each other see a
  flicker. They can banish each other by mistake.
- Goblins are his staff: they fake hostility toward him. After the reveal they serve him openly.
- Goal: weapon breaks fill the meter. Full meter + fragments together at the ward circle =
  resurrection: one player becomes the Arch Mage (hunter), other fragments become lieutenants.
  A fragment banished at that moment removes its domain from the endgame.

## Curses (run-long modifiers on the existing rune system)
Good for you, bad for your friends; pick one of three at each ward circle, some assignable to
others. Examples: Magnetic, Bell, Exploding Landings, Echo, Swap Sneeze, Goblin Magnet, Glass,
Leash. Cleanse or trade at anvils. Greed and sabotage look the same.

## Legend
Everyone earns legend (kills, saves, trick shots, not breaking). Nobody is eliminated. Petty
sabotage by everybody is the Mage's camouflage.

## Kept from the current build
Launch movement and arc preview, soul + possession, racks and home slots, goblins (curious /
hostile / patrol / sleeper / porter / shield boss), magic doors (become the labyrinth engine),
room shape builder (rooms as modules), rune / modifier system (curses), stick-in-wood, anvil,
WorldAuthority seam, several kit pieces as room dressing.
Dropped: the 45-room linear tower campaign and contraption puzzles as the core.

## Build order (multiplayer first: none of this can be judged solo)
1. Port the ATCK netcode (never yet verified between two real browsers: prove that first).
2. Two players in the FeelBox batting each other. If that is not fun, stop and rethink.
3. Hub + shuffled-door labyrinth from ~8 existing rooms, room tags, rack by party size.
4. Curses, legend, epilogue.
5. The Mage layer last: it is rules on top (roles, powers, evidence, meter, ward votes, hunt).

## Owner decisions, 2026-09-20 (these override anything above that conflicts)
- The labyrinth is a 5x5 GRID for now, of square and long-rectangle rooms. It is not a physical
  grid: only how the teleport doors are connected (a host-owned table: cell -> room + rotation).
  Must be easy to expand. Nothing physically moves when the maze is rearranged.
- The Mage rearranges rooms through a WINDOW he opens. It must be hard to do and on a cooldown.
- TWO END ROOMS: a shared EXIT the group moves toward, and a RESURRECTION ROOM that is the
  Mage's goal. His job is to guide the group to the wrong one. (This replaces the hub-and-meter
  structure above unless kept deliberately.)
- The other weapons need some sense of direction, but not too much: compass, or compass +
  distance (undecided).
- At most TWO fragments. Both have the same standard abilities, REDUCED unless they choose the
  same action: one initiates, the other gets an approve pop-up that spends their action too.
- Death: respawn with the group, losing legend (all of it? undecided).
- Splitting up is allowed: puzzles push against it and being alone creates suspicion.
- Idea to explore: the Mage uses telekinesis to fling / nudge friends in mid-air.
- Research requested: Haunted Heist, Among Us, sabotage games -> docs/research/traitor-games.md.
Claude's proposals awaiting the owner's answer: fragments deflect nearby compasses toward the
Resurrection Room (compass shows direction only); the Mage's power to rearrange comes only from
weapon breaks; legend is banked at anvils and death wipes only unbanked legend; rooms may be
moved with players inside; Nudge (deniable) versus joint-cast Fling (obvious).

## Owner decisions, later on 2026-09-20 (override anything above that conflicts)
- Compass deflection: YES, but only ever for a MINORITY of the group (strictly fewer than half
  of the non-Mage players). It is an active Mage power chosen through his HUD, not proximity.
- The Mage gets NO extra power from deaths. Later: a fun on-screen visual effect only he sees.
- Death: respawn with the group and LOSE ALL legend.
- The Resurrection Room firing = INSTANT MAGE WIN for now (simple grey box; no hunt phase yet).
- Mage powers (owner's list): invisibility; mid-air nudge / pull on left / right click; goblin
  commands via HUD; room re-arranging via HUD (swap NEIGHBOURS only, with a cooldown); giving
  curses or turning off a room's lights via HUD; changing teammates' compass directions via HUD.
- No resurrection meter. Two fragments max, same abilities, reduced unless co-signed.
Claude's proposals awaiting the owner: evidence per power is always partial (wisp seen only by
souls, truthful glyph changes + rumble inside moved rooms, goblin tells, decoy that ignores
bumps); invisibility leaves a decoy body (alibi); start, Exit and Resurrection rooms cannot be
swapped and the host refuses swaps that cut a room off from the Exit; the Mage HUD is never
visible by default: everyone has the same hold-to-open map, the Mage's copy is clickable, and
the mouse buttons do something harmless for non-Mages so clicking is no tell; grey-box power
order: Nudge/Pull, Room swap, Compass bend, Lights out, then goblin commands, Hex, Invisibility.
Research notes: docs/research/traitor-games.md (Haunted Heist is asymmetric PvP with known
teams and a very similar pitch; our unoccupied ground is launch-only movement: "I aimed badly"
is the alibi; keep legend from rewarding pointless friend-breaking).

## Open questions
- Player counts: hidden roles are thin at 3 and meaningless at 2. Variant to consider: the
  fragment hides in a BODY on the rack and whoever possesses it holds its powers.
- Hunt phase: what form does the resurrected Arch Mage take (hands and first person, or a big
  floating lich), and how long should the hunt last?
- How much combat versus traversal versus challenge rooms per floor?
- Does the tall-tower / bridge / keep fiction stay as the skin of the labyrinth?
