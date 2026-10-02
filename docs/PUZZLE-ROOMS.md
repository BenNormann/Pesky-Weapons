# FOUR-ROOM PLATFORM RUN

## Party-size baseline

The authored run is tuned for **four players**. Four is the useful centre of the supported 3–8 range: it
creates distinct jobs without making a three-player party impossible. Every room has a simple readable
goal, generous landing surfaces, a latching exit, joke signage, and a `PuzzleRoomProfile` for later scaling.

All four room ids (3–6) are guaranteed in one fixed, continuous order. `RunDef.roomsPerRun` is four:

`Weapon Rack -> Resonance Forge -> Clockwork Canteen -> Magnet Mayhem -> Counterweight Comedy -> Exit`

Clearing the fourth room leads to the existing Exit room; once every non-Mage player reaches its volume,
`RunHud` presents the full-screen **THE WEAPONS ESCAPED** result and reveals the Arch Mage fragment.

## Resonance Forge (room id 3)

**Target:** 3–8 players, optimal 4, expected first-clear time 45–70 seconds, difficulty tier 2.

The south teleport door is sealed by three resonance scales. All three must hold the correct mass class
at the same moment:

| Scale | Valid weapons |
|---|---|
| Light | Banana (0.5), Dagger (1) |
| Medium | Staff (2.5), Sword (3), Orb (4) |
| Heavy | Mace (8), Hammer (14) |

When all three are correct, the lock latches and the exit stays open. This uses the existing
network-authoritative `ScalesLock` and `Door_Scales` pieces; no client decides success.

### Intended play

- Three players bring one weapon from each mass class and park on the marked pans.
- A fourth player acts as runner/spotter: calls the three landings, bumps a weapon that slides off, and
  watches for Mage nudges or curses.
- With three players, everyone must commit a body, then re-possess after the latch.
- With five to eight, spare players can recover bad landings and body-block deliberate sabotage.
- A free soul cannot cross the teleport door, so the team must choose its three classes before entering.

### Hidden-traitor pressure

The puzzle makes quiet sabotage legible but ambiguous. A Mage can nudge an airborne landing, apply Heavy
or Slippery at the wrong moment, or simply bring an incorrect class and argue that the scale is broken.
Because the three indicators are visible from the whole room, honest players can diagnose the state
without the game naming the traitor.

### Future scaling hooks

`PuzzleRoomProfile` records minimum, optimal and maximum party size, solve-time target and difficulty tier.
`RunDef.guaranteedRoomIds` controls featured rooms independently of the random pool. A future selector can
use the profile to choose two active scales for an easy three-player run, add a timed strike for hard mode,
or prefer rooms whose required weapon classes are available on the starting rack.

## Clockwork Canteen (room id 4)

Three out-of-phase moving serving trays form a forgiving upward route to a south balcony. Fixed starter
and recovery platforms mean a missed launch costs time, not a reset. Grounded weapons settle onto and
travel with each tray, while launching immediately restores full momentum. Any weapon can smack the large green
"service bell" lever to latch the exit. Signs advertise "velocity soup" and remind players that falling is
free while dignity costs extra.

**Main verbs:** moving-platform timing, launch arcs, riding together, mid-air Mage nudges.

## Magnet Mayhem (room id 5)

Three ceiling magnets lift metal weapons toward broad landing shelves. Staff, Banana and other non-metal
choices have a deliberately chunky side staircase, so the room never becomes unsolvable because of the
starting rack. Both routes meet at an elevated lever above the exit.

**Main verbs:** weapon tags, magnetic launch correction, route choice, catching a friend after release.

## Counterweight Comedy (room id 6)

The Hammer or Mace parks on one counterweight pan while another player rides the opposite pan upward and
launches onto the goal balcony. Wide loading blocks make boarding easy; the raised button accepts any
weapon impact and latches. The Banana is explicitly described as "trying its best."

**Main verbs:** parked bodies, mass, possession swapping, counterweight lift, cooperative timing.
