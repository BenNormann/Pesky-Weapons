# Backlog

Agreed work not yet built, in rough order. Update when something lands.

## Next round
- Invisibility (leaves a decoy body as an alibi)
- Goblin commands (attack me, call a patrol, mark a friend, wake sleepers) - the map is
  gone, so they need a new way to be given
- Lights out for one room

## Mage powers still unbuilt
- Fun on-screen effect only the Mage sees

## The run (docs/RUN.md)
- Real pool rooms with their own completion conditions (today: 23 blank rooms with a
  placeholder lever seal); the shaped room prefabs and the kit inventory are for this
- What is behind the rack room's RESERVED door
- Curse draft at checkpoints; the shop-window epilogue
- Player death: nothing calls ReportPlayerDown yet; nothing awards legend
- Loose (unheld) weapons are not position-synced after release

## Labyrinth (set aside 2026-09-29, kept in the project)
- Crossing the Exit doorway should end the round (today: gather near it)
- Real 1x2 rooms in the grid (LABYRINTH.md lists what is needed)

## Multiplayer polish
- JOIN_REFUSED so "room full" reaches the joiner
- Signalling relays: the public Nostr relays trystero uses were unusable during the 2026-09-22
  test (the agent had to run a local relay), and peer discovery deadlocked twice when two
  announces crossed (a second JOIN worked). Add more relays to net.js and a JOIN retry.
- TURN relay provider for restrictive networks (hook: window.PESKY_ICE_SERVERS)

## Settings (docs/SETTINGS.md)
- An AudioMixer behind the Music / SFX / Voice sliders (they are stored only; AudioSettings.Apply is
  the hook)
- Keybind conflict warning; tutorial signs and the ability bar's key labels that follow a rebind

## Other
- Remove Unity's AI packages (com.unity.ai.assistant, com.unity.ai.inference / Sentis): they
  produce the repeating NoSubscription console errors and all 364 shader warnings in a web
  build, and nothing uses them.
- Game name (shortlist given 2026-09-22; owner to pick or redirect)
- Tutorial: teach the two-player verbs once a second local player is possible

## Done
- **Settings screen / pause menu and in-level LEAVE**, 2026-09-30: Escape (or losing the pointer
  lock) in the tutorial or a run opens it; RESUME, mouse sensitivity, spike filter, four volume
  sliders (Master works, the rest are a stub), rebindable keys saved in PlayerPrefs, EXIT TO MAIN
  MENU (a client leaves, a host ends the session for everyone after a confirm). Implemented,
  untested. See docs/SETTINGS.md.
- **Mage HUD surviving into the next run** fixed, 2026-09-30: every round start resets each
  peer's role and the host tells every player its role (Weapon or Mage) every round. Implemented,
  untested. See BUILD-LOG round 10.
- **The simplified run** (docs/RUN.md), 2026-09-29: five pool rooms in a row picked per run,
  two-door rooms with a placeholder lever seal on the exit door, a 5:00 timer from the first
  player leaving the rack, the crew wins in the Exit room, the Mage wins on the deadline; the
  labyrinth grid / map / pad / compass / Resurrection Room set aside. Implemented, untested.
- **Curses (Hex)**, 2026-09-29: keys 1-5, Magnetic / Nausea / Slippery / Blindness / Heavy,
  20 s each on one shared 30 s cooldown, 15 m, cast on the player under the crosshair; the
  host validates, only the victim applies and sees it, nothing says who. Implemented,
  untested. See RUN.md sections 4.5 and 5, NETCODE-STATUS round 8.
- **Nudge doubled** to 8 m/s (nudgeImpulse, pullImpulse), 2026-09-29.
- **Mage nudge / pull** (left / right click), 2026-09-22: a small mid-air impulse on another
  player (or the tutorial dummy) in the air, in sight and in range, on a cooldown; the host
  validates, the target's owner applies it, a free soul sees a faint wisp, nothing says who did
  it. Implemented, untested. See NETCODE-STATUS "Feedback round 6" and LABYRINTH section 13.
