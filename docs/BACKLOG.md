# Backlog

Agreed work not yet built, in rough order. Update when something lands.

## Next round
- **Mage nudge / pull** (left / right click): a small mid-air impulse on a friend with line of
  sight, short range, short cooldown; leaves a faint wisp only souls can see. Owner approved
  2026-09-22.

## Mage powers still unbuilt
- Invisibility (leaves a decoy body as an alibi)
- Goblin commands via the map (attack me, call a patrol, mark a friend, wake sleepers)
- Lights out for one room
- Hex: short temporary curses
- Fun on-screen effect only the Mage sees

## Labyrinth and rules
- Crossing the Exit doorway should end the round (today: gather near it)
- Player death: nothing calls ReportPlayerDown yet, so respawn-with-group and the legend
  reset never run; nothing awards legend
- Loose (unheld) weapons are not position-synced after release
- Real 1x2 rooms in the grid (LABYRINTH.md lists what is needed)
- Curse draft at checkpoints; the shop-window epilogue
- Real puzzle rooms from the kit inventory and the shaped room prefabs

## Multiplayer polish
- JOIN_REFUSED so "room full" reaches the joiner
- Pause menu and in-level LEAVE
- Signalling relays: the public Nostr relays trystero uses were unusable during the 2026-09-22
  test (the agent had to run a local relay), and peer discovery deadlocked twice when two
  announces crossed (a second JOIN worked). Add more relays to net.js and a JOIN retry.
- TURN relay provider for restrictive networks (hook: window.PESKY_ICE_SERVERS)

## Other
- Game name (shortlist given 2026-09-22; owner to pick or redirect)
- Tutorial: teach the two-player verbs once a second local player is possible
