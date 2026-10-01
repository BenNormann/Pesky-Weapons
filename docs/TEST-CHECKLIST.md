# TEST CHECKLIST

Everything in `Pesky Weapons Unity` was written and never run: seven stages of
netcode, a menu, a labyrinth and a tutorial, all "implemented, untested". No
agent has entered play mode, opened a socket or clicked a button. This file is
the order to find that out in, from the things everything else stands on to the
things that only matter once the rest works.

Two rules for reading it:

* **Ignore** the repeating console line `Error reason is 'NoSubscription' …
  generators.ai.unity.com`. That is Unity's own AI package and has nothing to do
  with this project.
* `[tcp]` lines come from the transport (sockets). `[net]` lines come from the
  session (the handshake and the protocol). A failure that prints neither is a
  scene or a script problem, not a network one.

---

## 0. How to run it solo

1. Open `Assets/Scenes/Boot.unity` and press Play. Boot loads `MainMenu`.
2. Type a callsign, then:
   * **TUTORIAL** - loads `Tutorial` on an offline loopback session (no sockets
     at all). This is the fastest way to check that movement, souls, goblins,
     kit, magic doors, the compass, the map and the Mage powers work.
   * **HOST** - opens a room on `localhost:7777` and shows the room page. START
     loads `Labyrinth`.
3. Click in the Game view to lock the pointer; Esc frees it.

Opening `Tutorial.unity` or `Labyrinth.unity` straight from the Editor also
works: `SessionRunner` starts its own offline session, and because the menu did
not start it, a finished run stays in the level instead of loading the menu.

**Build scenes are exactly `Boot`, `MainMenu`, `Tutorial`, `Labyrinth`.**
`Zone1`, `Bridge`, `MainTower` and `Keep` are still in the project and still
open in the Editor, but they are not in the build any more.

## 0b. How to run two players

Two Editors cannot exist for one project, so the pairing is **the Editor hosts
and a Windows build joins**.

**Making the build** (Unity 6 Build Profiles):

1. `File > Build Profiles…` (Ctrl+Shift+B).
2. Pick **Windows** in the platform list. If there is no profile yet, select the
   Windows platform and it uses the shared **Scene List**, which is already
   Boot / MainMenu / Tutorial / Labyrinth - check that list in the profile
   window before building.
3. Tick **Development Build** (it makes `Player.log` chattier; nothing in the
   game depends on it).
4. **Build**, and put it somewhere outside `Assets/`, e.g.
   `C:\Users\benos\Code\Pesky Weapons\Builds\Win\`.

**Running the pair:**

1. Editor: Play from `Boot`, callsign, **HOST**. The room page shows this
   machine's LAN address and port; the console prints
   `[tcp] hosting on port 7777 …`. Allow the Windows Firewall prompt the first
   time - declining it leaves the host saying "hosting" while nobody can reach
   it.
2. Build: run it windowed from PowerShell so both windows are visible:
   `& "…\Pesky.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720`
3. In the build's menu type `localhost:7777` in the code field and press
   **JOIN**. On two machines, type the LAN address the host's room page shows
   (`192.168.1.5:7777`); both machines must allow the port.
4. The build's log is
   `%USERPROFILE%\AppData\LocalLow\<company>\<product>\Player.log`. Keep it open
   in a tail (`Get-Content -Wait`) - half of the checks below are read there.

Useful facts: the host is always slot 0; up to 8 players; a peer that goes
silent for 10 s is dropped; recompiling a script while playing closes the
sockets on purpose.

---

## 1. The ordered checks

Work down the list. Each one assumes everything above it passed; a failure high
up makes everything below it meaningless.

### 1.1 Connect, room page, start  *(the foundation - nothing else works without it)*

* [ ] The build reaches the menu and the Editor reaches the menu.
* [ ] Editor HOST: room page appears, one crew row (yours, amber), the address
      is printed on it and in the console.
* [ ] Build JOIN `localhost:7777`: Editor console prints `[tcp] peer joined: p1`,
      the build's log prints `[tcp] connected as p1; host is host, 0 other
      peer(s)` and then `[net]` join lines.
* [ ] Both room pages now show **two** crew rows with the same names.
* [ ] START (host only, enabled from one player) → **both** peers load
      `Labyrinth` from their own `PhaseChanged`. Nobody is told to load it.

If the client connects but never shows a crew row, it is the handshake, not the
socket: look for a protocol-version mismatch (`JOIN_REFUSED` does not exist, so
a mismatch is a silent hang on the joiner and a 12 s timeout in the menu).

### 1.2 Seeing each other move  *(POSE, the stream everything else is judged against)*

* [ ] Each player sees the other's weapon in the right room, moving.
* [ ] Launch (Space): the remote copy follows about 100 ms behind, smoothly, and
      tumbles the same way (rotation is streamed).
* [ ] Standing still: the remote copy stops dead rather than drifting.
* [ ] Go through a magic door: the remote copy **snaps**, it does not fly across
      the room (the Teleport flag).
* [ ] Press Q: the other player sees a soul orb, not a weapon, where you are.

### 1.3 Possess, release, break, respawn  *(the host's answer, not yours)*

* [ ] Possess (E) a free weapon: both peers agree instantly on who holds it.
* [ ] Two players press E on the same weapon at the same moment: exactly one
      gets it; the other gets nothing at all (silence is the refusal).
* [ ] Release (Q) out of combat: the weapon drops and both peers see it in the
      same place. Loose weapons are **not** synchronised afterwards - they may
      drift apart; that is by design.
* [ ] Release in combat (within 5 s of a hit, or with a hostile goblin on you):
      the weapon **breaks** on both peers.
* [ ] A broken weapon respawns on its rack ~10 s later, on both peers.
* [ ] A player leaves while holding a weapon: the weapon goes free for everybody.

### 1.4 Goblins, and a client hitting one  *(the host simulates, the client claims)*

* [ ] Goblins ignore a still weapon and turn hostile when they see one move -
      for **both** players, not just the host.
* [ ] The client hits a goblin: hp falls on both screens, the shove happens, the
      goblin dies on both.
* [ ] The client's own weapon takes damage from a goblin hit and is shoved.
* [ ] A goblin dying looks the same on both (a puppet goblin may show "dead"
      about 150 ms before the hit flash - harmless).
* [ ] Nothing at all happens when a client hits a goblin it is nowhere near
      (the host's 6 m sanity check).

### 1.5 Magic doors across clients

* [ ] Both players go through the same magic door and end up in the same room,
      keeping their speed.
* [ ] The one who did not travel sees the other arrive instantly, not slide.
* [ ] Doors with a gate (key, plate, lever, cleared room) open for both peers
      at the same time.

### 1.6 Batting a friend  *(friend ballistics v0)*

* [ ] Launch your weapon into another player's weapon fast (> 4 m/s of relative
      speed): they get shoved.
* [ ] Watch for a **double shove** - once from local physics, once when the
      host's BAT_EVENT lands ~150 ms later. If it feels wrong, that is
      `GameData.batRestitution` / `batMaxSpeed`, or drop one of the two.
* [ ] A gentle nudge does nothing.

### 1.7 The labyrinth: doorways and wrapping

* [ ] Every room has four doorways, each with a glyph and a truthful name over it.
* [ ] Walking through a doorway puts you in the room its glyph named.
* [ ] Leaving the east edge of the map arrives on the west edge (the grid wraps).
* [ ] The one doorway in the whole labyrinth that does **not** wrap is the Exit,
      in the good-end room: it leads nowhere and is lit by a ring on the floor.
* [ ] Both players see the same room names over the same doorways.

### 1.8 The map

* [ ] Hold **Tab**: the map appears; north is up, your cell says YOU, visited
      cells are marked, the start and both ends are marked, the exit side reads
      `EXIT N/E/S/W`.
* [ ] Release Tab: it closes. A weapon (non-Mage) keeps looking around while the
      map is open; the cursor is not freed for them.
* [ ] The glyph on a tile matches the glyph over the doorway that leads there.

### 1.9 The Mage's swap

* [ ] A Mage's map has the mage bar under it; a weapon's map has **nothing** -
      no bar, no chips, nothing to pick.
* [ ] Drag a room tile onto an orthogonal neighbour: a ghost follows the pointer,
      only legal drops light up, an illegal one snaps back.
* [ ] A legal swap: both players see the doorway glyphs change, and walking
      through that doorway now arrives somewhere else.
* [ ] The start room and the two end rooms cannot be dragged at all.
* [ ] A second swap inside the cooldown does nothing - no message, no refusal.
* [ ] A non-Mage cannot make one happen by any means (there is no button to try).

### 1.10 The compass, and bending it

* [ ] The compass (top left) points at a **doorway of the room you are in** and
      turns with the camera.
* [ ] Following it repeatedly reaches the Exit.
* [ ] Standing in the target room the needle spins and says "you are there".
* [ ] A Mage clicks a crew chip, then a tile (or BAD END): that player's compass
      starts pointing the wrong way, **with nothing at all on their screen to say
      it was bent**.
* [ ] UN-BEND puts it back.
* [ ] The bend limit: with too few non-Mages nothing can be bent (strictly fewer
      than half). With 4+ crew, bending a second one should be refused in silence
      until the first is released.

### 1.11 Both ways a round ends

* [ ] **Escape**: the whole crew (minus the Mages) stands in the lit ring at the
      exit doorway → "THE WEAPONS ESCAPED", the fragments are named, everybody
      goes back to the room page with "the run is over".
* [ ] **The Mage's win**: get a majority of the crew into the Resurrection Room
      at once → "THE ARCH MAGE WINS" on every screen.
* [ ] After either, the host's room page still works and START runs another
      round with a **different** layout.

### 1.12 Leaving, and the host leaving

* [ ] A client presses LEAVE (or closes the build): the host sees the crew row
      go, the weapon that player held goes free.
* [ ] The **host** leaves: every client lands on the title page with "the host
      left the room" rather than freezing.
* [ ] Closing the Editor's play mode mid-session leaves no socket behind (the
      next Play still hosts fine).

### 1.13 Late join

* [ ] Start a run with one player, then join with the second **while the round is
      running**: the joiner should skip the room page and load the labyrinth
      straight from the menu, with the right layout, the right weapons and the
      right goblins.
* [ ] The late joiner's map shows the same table as everybody else.

---

## 2. The tutorial, solo  *(new, and the only thing that can be tested alone)*

MainMenu → TUTORIAL. It is `Tutorial.unity`: rooms 1-5 of the old Zone1 (the
weapon room, the goblin room, the key climb, the plate room, the arena) and then
a small practice labyrinth.

* [ ] Room 1: possess a weapon, launch through the five rooms as before. The
      towers are gone; nothing dead-ends where a tower door used to be.
* [ ] The arena's far doorway now leads to the practice labyrinth's Entry Hall.
* [ ] The moment you arrive, the labyrinth HUD wakes up: compass top left, and
      about 1.5 s later "YOU ARE A FRAGMENT OF THE ARCH MAGE".
* [ ] Three signs in the Entry Hall explain the Mage, the compass and the map.
* [ ] press Tab (toggle): a 3x3 map with nine rooms, the mage bar under it, and one crew
      chip called **DUMMY**.
* [ ] Drag two neighbouring rooms to swap them, then walk through a doorway and
      check the glyph told the truth about where you came out.
* [ ] Click DUMMY, then a tile: the chip marks itself bent and the line under the
      bar says where the dummy's compass now points. Nothing is sent anywhere -
      it is a stand-in, not a player.
* [ ] Find the Resurrection Room (its sign, its red ring) and the Gate Hall.
      Standing in the Resurrection Room does **not** end the tutorial.
* [ ] Stand in the lit ring at the Gate Hall's exit doorway → back to the main
      menu, title page, "the run is over".

---

## 3. The riskiest untested assumptions, gathered

Collected from `NETCODE-STATUS.md` (S2.6, S3.6, S4.7, S5.5, S6.5, S7.6). These
are the places most likely to be wrong, roughly in the order they would bite.

**The session and the sockets**

1. **The synchronous offline round trip.** Everything solo depends on
   `Send(intent) → validator → Emit → EventApplied → scene change` happening
   inside the `Request*` call. If pressing E does nothing at all, that is the
   first thing to read.
2. **Nothing has ever opened a socket.** A mistake in the length prefix shows up
   as an immediate disconnect (`[tcp] bad frame length`) or a silent drop.
3. **A client has no local slot for its first frames.** `PlayerSpawner` picks a
   spawn point from the slot in `Start`, and on a client that can still be
   `NoSlot` - the most likely thing to look wrong on the joining side.
4. **`JOIN_REFUSED` does not exist**: a full room or a version mismatch is a 12 s
   silence on the joiner.
5. **Firewall.** The first host in the Editor pops a Windows dialog; declining it
   is indistinguishable from a broken transport.

**The game on top of it**

6. **No damage without POSE or GameData.** `HitValidator` drops every claim if
   `GameData` is missing, and rejects one whose streamed pose is more than 6 m
   from the goblin.
7. **Kinematic remote weapons in PhysX**: trigger volumes and collisions against
   a driven copy are assumed to work; the double shove in 1.6 lives here.
8. **Host perception of remote players** rides a flag in the pose stream, so
   "playing dead" near a goblin may behave slightly differently for a client.
9. **Loose weapons are per-peer physics** and will drift; possession snaps them.
10. **`LevelClock` re-base**: a hitch longer than Unity's maximum delta time
    re-bases the shared clock and every scheduled mover jumps. Watch riders on
    lifts.
11. **Re-entrant host events**: a kit change that causes another kit change
    queues the inner one first. Every handler is meant to be idempotent.

**The labyrinth**

12. **`CollectPlayerCells` is the round loop's only eyesight**: a player above a
    roof or between two rooms is in no cell and counts for nothing - not for the
    endings, not as a respawn anchor.
13. **Win checks run at 5 Hz off streamed poses**, so "everybody at the exit" is
    decided slightly late.
14. **A room swap mid-flight**: a swap that lands between a door sensor's two
    samples sends the traveller somewhere the client had not drawn yet.
15. **100 magic doors each run a sensor scan every physics step** in the
    labyrinth, and 101 realtime lights are in the scene. Never measured.
16. **The compass needle's screen angle** is one expression; if it is mirrored,
    negate it.
17. **The Mage's drag uses pointer capture**, and the drop cell is found by
    hit-testing tile rectangles.
18. **Nothing has ever called `ReportPlayerDown`**, so death, the respawn anchor
    and the legend reset have never run at all.

**The tutorial (stage 7)**

19. **`TutorialTrigger` only sees weapons** (souls are on a layer that never
    touches triggers), so arriving in the Entry Hall as a soul would not wake the
    HUD. Arriving through the door as a weapon is the expected path.
20. **The tutorial runs on its own data asset** (`GameData_Tutorial`, whose
    labyrinth is `Labyrinth_Tutorial`, 3x3). If the real `GameData` changes,
    that copy does not follow.
21. **The tutorial player is always the Mage** (one player, one fragment), so the
    escape ending cannot fire there: the lit ring at the exit ends the tutorial
    through `TutorialTrigger`, not through the labyrinth's own rule.

---

## 4. Feedback round 2 — the five things that changed (2026-09-20)

All **implemented, untested**. `Wire.ProtocolVersion` is now **4**, so an older
build cannot join a newer one: rebuild both sides before pairing two instances.

### 4.1 Door labels off, room numbers on the floor

- Walk into any labyrinth room. There should be **no text above any doorway** —
  no glyph, no destination name, nothing.
- In the middle of the floor there should be **one large number lying flat**: the
  room's id. It must read the right way round from the camera above (not
  mirrored), with the **top of the digits pointing at the north doorway**.
- Cross a doorway and check the new room shows a **different** number, and that
  it matches the tile's glyph on the Mage's map for the cell you are in.
- Read the **room-name sign** on the south wall from inside the room. It must be
  visible and read correctly. *(The sign fix is project-wide: check a couple of
  tutorial signs in rooms 1-5 too. If any sign now faces the wrong way, revert
  `Assets/Prefabs/Kit/Signs_And_Lights/Sign.prefab` > `Label` to yaw 180 and tell me.)*
- To get the labels back for a moment: `Assets/Data/Labyrinth.asset` >
  `showDoorLabels` on. They should come back **reading the right way round**.

### 4.2 The shared scratch pad

Solo first, then with two instances.

- As a **weapon**, press Tab (toggle). You should see **only the pad** — a blank dark
  canvas — with **no map, no grid, no room names, nothing about the labyrinth**.
- Left mouse draws in **your slot colour**. Right mouse erases. The ERASER and
  PEN buttons do the same thing; THIN / MEDIUM / THICK change the width.
- The cursor should be **free** while Tab is held and **re-lock** when you let
  go, for a weapon as well as a Mage.
- Draw one very long unbroken squiggle (more than about 64 points) and check it
  has **no gap** in it where it was split.
- As the **Mage**, press Tab (toggle): there should be two top tabs, **MAP** and **PAD**.
  MAP is the old grid with drag-to-swap and the bend chips; PAD is the same pad.
  Switching tabs must not lose what is drawn.
- **Two instances**: draw on one, and it must appear on the other within a frame
  or two, in the drawer's colour. Erase on one, and the rub-out must appear on
  the other too (it is a stroke in the canvas colour, not a delete).
- Draw a lot very fast on a client. Some strokes may be **silently refused** by
  the host's rate cap: they fade off your own screen after about 3 seconds and
  never reach anybody else. That is expected, not a bug.
- **CLEAR** must only be visible on the **host**. Pressing it wipes the pad for
  everyone.
- **Late join**: draw, then join with a second instance. The newcomer should see
  the existing drawing (it rides the snapshot).
- **New round**: finish a round, start another. The pad must be **blank**.

### 4.3 The compass

- The HUD compass is a small ring with a **green spike from the middle**, and
  nothing else — no needle, no N/E/S/W letters, no room name.
- Turn on the spot: the spike must stay pointing at the same doorway **on
  screen**.
- Walk into the room the spike points at: the spike should **spin**.
- As a **Mage**, there should be **two** spikes: green toward the true Exit path
  and **red** toward the Resurrection Room. Walk into the Resurrection Room and
  the red spike alone should spin.
- **Bend a crew member's compass** (two instances). Their spike must still be
  **green** and simply point the wrong way. Nothing on their screen may hint that
  it was bent.

### 4.4 The swap cooldown

- `Assets/Data/Labyrinth.asset` > `swapCooldownSeconds` should read **60**.
- As the Mage, swap two rooms, then try again at once: **nothing happens** (the
  host refuses in silence). The SWAP ring on the map counts down from 60.
- The tutorial's practice grid uses `Labyrinth_Tutorial.asset` at **15 s**, so
  the lesson is not a wait.

### 4.5 The tutorial hole

- In the Goblin Room (Room 2), walk to the **east wall**. The opening that used
  to be there — where the lift shaft beside the Goblin Room was deleted — is now
  **solid wall**. There must be no way to launch out of the level there.
- Walk the whole tutorial and try to leave every room by every wall. Every
  opening should either have a door in it or be walled.
- `Pesky > Validate Open Scenes` now checks this: it reports any `..._Doorway`
  wall segment with no door within 2.5 m and no floor 3 m out on one side. It
  reads **0 problems** on Boot, MainMenu, Tutorial and Labyrinth.

## 5. Feedback round 3 (implemented, untested)

### 5.1 Signs read from the room

- In the **Tutorial**, walk up to `THE GATE HALL…`, `THE RESURRECTION ROOM…`,
  `THE COMPASS…`, `IN A REAL RUN…` and `HOLD TAB…`, and to the two control
  signs in the first room. Each must read **left to right, facing you**, from the
  open side of the room — not blank, not mirrored, not readable only from the
  1.5 m gap behind it. Walk behind each one: the board must hide the text.
- Every `Room N` / `CELL x` sign in the Labyrinth should be unchanged and correct.
- `Pesky > Validate Open Scenes` reads **0 problems**; a sign turned to face a
  near wall now makes it report `Sign '…' faces the wrong way`.

### 5.2 A soul cannot use a teleport door

- As a **free soul** (Q), fly at a doorway from the front: you must be **stopped
  by the opening**, not teleported and not able to slip into the alcove or the
  void behind the frame. A HUD line says to possess a weapon first.
- Possess a weapon (E) and launch through the same doorway: it must work
  **exactly as before** — same speed, same turn, same purple flash.
- Throw a **loose** weapon through: it must still travel. Goblins must still
  refuse to path into the alcove, and the trajectory preview must still end at
  the plane.
- Walk the whole tutorial, the respawn and the practice labyrinth: nothing may
  require a soul to pass a doorway.

### 5.3 The camera stays inside the room

- As a soul, fly **straight up into the ceiling** of a labyrinth room and look
  around: the camera must stay in the room — no seeing through the ceiling, no
  grey void, no near-plane clipping into the slab. Repeat against a wall, in a
  corner, in the **Round**, **Octagon**, **L-shape**, **Long Gallery** and
  **Tall Shaft** rooms, and as a **possessed weapon**.
- Nothing should pop: the camera may snap **in** but must ease back **out**.
- **Round 4 - look straight up from a surface.** Possess a weapon and let it come
  to **rest on the floor**, then pitch the view **fully up** and sweep the yaw all
  the way round: no black void along the bottom of the screen, no seeing under the
  floor. Repeat lying on the **rack plinth beside / under the rails**, **against a
  wall**, and **in a corner**; then as a **soul pressed into the ceiling** looking
  fully down and fully up. Under a rail / at the ceiling the camera should stay
  about 1 m back with the target low (or high) in frame rather than land on it.

### 5.4 The Mage's compass bend actually lands

- In the **Tutorial**, press Tab (toggle), click the practice chip, then a room: the chip
  must show `DUMMY > ROOM n` and the map must say so — never nothing at all.
- Try a second bend while the ring is counting: the hint must say **why**
  (recharging), not fall silent. Bend past the limit: it must say
  `ALREADY BENT n OF max`.
- With **3 players** (1 Mage, 2 crew) a bend must now land — this is the case
  that silently failed. With 5 crew the limit must still be 2.

## 6. Feedback round 5 (web performance, input spikes, the compass bend)

### 6.1 Numbers before feelings

- Open the web build with `?debug=1`, press **F3**: the overlay must appear
  (fps, fixed/frame, role, peers, rtt, in/out, poseIn, cell, compass). The
  browser console must carry a `[pesky] dbg ...` line every 5 s.
- Alone in the Labyrinth the frame rate must be far above the round-4 build's
  (about 30 fps uncapped on the test machine); only the room you stand in is
  drawn (walk through a doorway: the new room is there at once, nothing pops
  late). Loose weapons left in another room must still be there when you return.
- Two tabs / two machines: `peers=1` on both, `poseIn` about 20 Hz while the
  other player moves and about 1 Hz while still, `rtt` a few ms on one machine.
- Hide the **host** tab for ten seconds: the client's `simLag` climbs and its
  remote view freezes; when the host tab returns the client must catch up within
  a second or two and never stay frozen. Hide a **client** tab instead: the host
  is unaffected, the client catches up on return.

### 6.2 The spike filter

- Under pointer lock, flick the mouse as hard as you can, alt-tab away and back,
  open and close the Tab map: the view must never jerk, and each of those must
  add to `lookDrops` on the overlay (with `[pesky] look: ...` in the console).
- `Assets/Data/LookTuning.asset`: turn `filterSpikes` off and repeat to compare;
  try `Scale` mode.

### 6.3 The bend, for real, in two tabs

- Start a room with two players. The role reveal names the Mage; on the Mage's
  page hold **Tab**, click the other player's chip, then **BAD END**. The Mage's
  console must show `map: bend requested ...`; the **host's** console `host:
  bend ... accepted: COMPASS_TARGETS(BadEnd) sent to slot n`; the bent player's
  console `reply: COMPASS_TARGETS - this compass now points at BadEnd`, and its
  overlay `target=BadEnd`. The bent player's green spike must now point along
  the path to the Resurrection Room and **spin inside it**; **TRUE** on the chip
  must send it back (`target=GoodEnd`).
- Do it once with the Mage as host and once with the Mage as client.

### 6.4 The tutorial dummy

- In the practice labyrinth a grey figure (**DUMMY**) stands in the Entry Hall
  with a disc and a glowing spike over its head. Its spike must point at the
  doorway a truthful compass would take. press Tab (toggle), click **DUMMY**, then **BAD
  END**: the spike must swing to the doorway toward the Resurrection Room; pick a
  room instead: it points that way; pick the Entry Hall itself: it spins.

## 7. Feedback round 6: the Mage's nudge / pull (implemented, untested)

Protocol version 5: two copies of the game must both be this build.

### 7.1 Solo, in the tutorial

- [ ] In the practice labyrinth's Entry Hall the **DUMMY** now hops about 2 m
      every 3 s and lands back on its spot. A new sign on the east side explains
      the power.
- [ ] Look at the dummy while it is in the air: a small purple diamond spins
      over it. On the ground: no diamond.
- [ ] **Left click** while the diamond shows: the dummy is pushed away from you
      (along your view), then its next hop brings it back. **Right click**
      (after the cooldown): it is pulled toward you.
- [ ] Click while it stands on the floor: a quiet line under the middle of the
      screen, `only while they are in the air`. Walk more than 12 m away:
      `too far`. Click again at once after a nudge: `recharging n s`.
- [ ] Hold **Tab**: the MAP tab's mage row has a third ring, **NUDGE**, counting
      down 8 s after a nudge. With the overlay open, left / right clicks draw on
      the pad or drag on the map and **never** nudge. Nothing about the nudge is
      visible with the overlay closed except the diamond and the refusal line.
- [ ] Before the Mage room (rooms 1-5) clicking does nothing and shows nothing.
- [ ] With the pointer unlocked (Esc), the first left click only locks the
      pointer; it must not nudge.

### 7.2 Two players (Mage + one weapon; then Mage + two weapons)

- [ ] Start a room; the role reveal says who is the Mage (use `?debug=1` on both
      pages, F3 shows `role=`).
- [ ] The weapon launches; while it is in the air the Mage aims at it: diamond
      over it; **left click**: the weapon's flight bends away from the Mage's
      view, like a bad jump. **Right click** (after 8 s): it is drawn toward the
      Mage. The weapon's player sees only his own body lurch.
- [ ] Refusals, each on the Mage's screen only and each with a `[pesky] reply:
      NUDGE_REFUSED - ...` line (and a `host: nudge ... refused: ...` line on the
      host): target **on the ground** (`NotAirborne`), **more than 12 m** away
      (`OutOfRange`), **behind a wall** (`NoLineOfSight`; the local marker should
      already be gone), **on cooldown** (`Cooldown`, with the time left).
- [ ] A **soul**: let a third player (or the weapon's player, after Q) fly as a
      soul in the same room: when the Mage nudges somebody, the soul sees a faint
      pale streak at the target for about a second. A weapon sees no streak.
- [ ] **Nothing identifies the Mage**: on the weapon's and the soul's pages, no
      text, no marker, no sound, no console line (without `?debug=1`) says who
      did it; the only `NUDGE_EVENT` traffic is the same for everyone.
- [ ] The weapon's clicks (left and right, overlay shut) do nothing and send
      nothing: with `?debug=1` the host prints no `nudge` line for them, and the
      weapon's `out` counter on F3 does not move per click.
- [ ] Mage as host and Mage as client: both must work.
- [ ] (6+ players, two fragments) one fragment's nudge is visibly weaker (0.6);
      both nudging the same body within a second: the second lands at full
      strength (`(co-signed, full strength)` on the host's debug line).

## 8. Run mode (round 8, 2026-09-29; implemented, untested)

Protocol version **6**: two copies of the game must both be this build. START now loads
`Run`, not `Labyrinth`; the room page line reads `5 rooms, 5:00   for n weapons`. Sections
1.7-1.11 (the labyrinth, the map, the swap, the compass, the labyrinth endings) no longer
apply: that mode is set aside (`docs/RUN.md` section 9 says how to bring it back). The full
design and every number: `docs/RUN.md`.

### 8.1 Solo, in the tutorial

- [ ] Rooms 1-5 are unchanged. Room 6 is now **one room**, the practice hall: no doorways,
      four sealed walls, the signs (see section 9.2 for the round-9 layout), the hopping
      **DUMMY**, and a small lit gold **ring** in the far south-west corner with an `EXIT` sign.
- [ ] Entering the hall wakes the HUD: after about 1.5 s **YOU ARE A FRAGMENT OF THE ARCH
      MAGE**. No compass, no Tab overlay (Tab does nothing), **no timer** (the tutorial has no
      run).
- [ ] The **ability bar** appears at the bottom centre (section 9.1). Nothing by the
      crosshair. Before the hall (rooms 1-5) the bar must not be there.
- [ ] Nudge / pull on the dummy as in section 7.1, but stronger (8 m/s, was 4): the dummy
      flies noticeably farther. After a nudge the **Nudge / Pull** slot counts down 8 s.
- [ ] Look at the dummy (in the air or on the floor, both fine) and press **1**: its label
      changes from `DUMMY` to `MAGNETIC 20` and counts down to 0, then back to `DUMMY`. The
      five curse slots count down **6 s** (the tutorial's shorter cooldown; 30 s in a real run).
- [ ] **2**, **3**, **4**, **5** in turn (after each cooldown): `NAUSEA`, `SLIPPERY`,
      `BLINDNESS`, `HEAVY` on the label. A second key while one is on **replaces** it.
- [ ] Press a key at once after another: quiet line `recharging n s`. Walk more than 15 m
      from the dummy: `too far`. Look at nothing and press 1-5: nothing at all happens.
- [ ] `?debug=1` (browser) or the Editor console: `curse: Magnetic requested on the practice
      dummy` -> `host: curse Magnetic on the practice dummy from slot 0 accepted: CURSE_EVENT
      for 20.0 s`.
- [ ] Launch into the lit ring: the tutorial ends and the menu returns, as before.

### 8.2 Two players: the run

- [ ] Host + join, START: both load **Run** and spawn as souls in the rack room. Roles: one
      reveal says WEAPON, the other FRAGMENT (F3 `role=`). Timer at the top reads a dim
      **5:00** once the layout has arrived (about a second; host console: `host: run layout
      7 3 24 1 12 (pool of 23)` with different numbers per round).
- [ ] The rack room has **two doorways**: north is shut by a solid panel with a **RESERVED**
      sign beside it (nothing opens it); south glows. East and west are plain wall.
- [ ] Possess a weapon and launch through the south door: you come out of the **north**
      doorway of a pool room whose floor number is the first id of the layout. The timer on
      **both** screens starts counting down at that moment (host console: `host: run timer
      started at tick ...`).
- [ ] The pool room has a north doorway (back to the rack: go through it and you are back at
      the rack's south door - **two-way**) and a south doorway shut by a **panel**, with a
      **lever** on a post beside it (at +4, -9 of the room).
- [ ] Hit the lever at 4 m/s or more with any weapon: the handle swings, turns green, the
      south panel slides into the wall, the plane glows. On the **other** player's screen the
      same happens (KIT_STATE Lever, then Door). A client hitting it must work too (KIT_REQ ->
      host).
- [ ] Through the open south door: the next room of the layout, its own lever, and so on
      for **five** rooms. Room k's north doorway always leads back to room k - 1's south
      doorway.
- [ ] Room 5's south door leads into the **Gate Hall**: its north doorway is where you arrive,
      its south doorway is lit gold (arch + floor ring). Walking into that doorway does
      nothing (it leads nowhere); the win is the room.
- [ ] **Exit win**: every NON-Mage player's weapon inside the Gate Hall at once. With two
      players that is the weapon player alone in the hall; the Mage may still be in room 3:
      the banner **THE WEAPONS ESCAPED** and `the arch mage was: <name>`, then the menu.
- [ ] **Timer loss**: start a new run, leave the rack, wait. Under 30 s the timer turns
      **red**. At 0:00: **TIME IS UP - THE ARCH MAGE WINS** and the Mage's name, on both
      screens, then the menu. Being inside the Gate Hall with one non-Mage still outside at
      0:00 must lose.
- [ ] **Late join**: a third player joining mid-run sees the same timer and the same layout
      (his doors lead to the same rooms), and a lever that was already struck is already
      green with its door open.

### 8.3 Two players: the curses (Mage + one weapon)

- [ ] The Mage looks at the weapon's body within 15 m and presses **1**: on the **weapon's**
      screen `CURSED: MAGNETIC   20 s` counting down; nothing on the Mage's screen but the
      ability bar's five curse slots starting their 30 s. Drop a loose weapon nearby: the victim's launches curve
      toward it (the trajectory preview already shows the bend).
- [ ] **2 NAUSEA**: the victim's view rolls and yaws slowly; launches wander off the aim.
- [ ] **3 SLIPPERY**: the victim's weapon slides on landing and will not settle (P_Slick);
      after 20 s it grips again. Possessing another weapon mid-curse moves the curse with you.
- [ ] **4 BLINDNESS**: the victim's screen goes almost black except a small clear circle in
      the middle; gone after 20 s.
- [ ] **5 HEAVY**: the victim's launches are half speed (the preview arc is short).
- [ ] **Nobody but the victim sees anything**: on the Mage's page no marker, no line, no
      effect; on a third player's page nothing at all. No console line names the caster
      without `?debug=1`.
- [ ] Refusals on the Mage's screen only: target farther than 15 m (`too far`), a **free
      soul** (`nothing to curse there`), a key inside the 30 s (`recharging n s`). Each with a
      `reply: CURSE_REFUSED - ...` line under `?debug=1` and a `host: curse ... refused` line
      on the host.
- [ ] The weapon player presses 1-5: nothing happens, nothing is sent (F3 `out` counter does
      not move per key; the host prints no `curse` line).
- [ ] Mage as host and Mage as client: both must work.
- [ ] **Nudge** in the run: as section 7.2, but 8 m/s. The nudge target marker and the
      ability bar are the Mage's only extra UI; a weapon has no bar.

## 9. Round 9: ability bar + tutorial ring (2026-09-29; implemented, untested)

### 9.1 The Mage's ability bar

- [ ] **Nothing by the crosshair**: the old `N` / `C` rings right of the screen centre are
      gone for everybody, Mage or not.
- [ ] **Mage only**: in the tutorial hall (you are the Mage) and as the Mage in a run, a row
      of six square slots sits centred at the bottom of the screen: first `LMB / RMB
      Nudge / Pull`, then (a little apart) `1 Magnetic`, `2 Nausea`, `3 Slippery`,
      `4 Blindness`, `5 Heavy`. Each slot: a flat grey tile with a glyph (N/P, M N S B H), the
      key in amber in the top-left corner, the name underneath.
- [ ] **A weapon sees no bar**: the second player (not the Mage) in a two-player run has
      nothing at the bottom centre; the weapon HUD (bottom-left panel, bottom-right hint) is
      unchanged and does not overlap the bar on the Mage's screen.
- [ ] **Cooldown sweep**: cast any curse: the pressed slot **pulses** (grows a little, gold
      border, about 0.3 s), then **all five** curse slots darken; the dark sheet shrinks
      upward as the cooldown runs out (the tile refills from the bottom) and each shows the
      whole seconds left (6 in the tutorial, 30 in a run) in amber. At 0 they are bright again.
      The Nudge / Pull slot is not darkened by a curse.
- [ ] Nudge or pull (left or right click on the airborne dummy): the **Nudge / Pull** slot
      alone pulses, darkens with the same sweep and counts down 8 s; the curse slots are
      untouched. A nudge refused locally (not airborne, out of sight) does not darken it.
- [ ] A refused curse (`too far`, `nothing to curse there`) does not darken the bar; a
      refusal for `recharging` keeps the countdown honest.
- [ ] The bar disappears with the result banner at the end of a run.
- [ ] **Scale**: at 1920 x 1080 and at 1280 x 720 (browser window or Game view) the bar keeps
      its proportions (the panel scales with the screen width), the key and name text stay
      legible, and `LMB / RMB` fits inside its slot's corner.

### 9.2 The tutorial's exit ring

- [ ] Entering the practice hall (through the door from room 5, north-east corner) you see,
      in walking order: `Sign_Mage` then `Sign_Run` then `Sign_Nudge` on the **east wall** on
      your left, the hopping **dummy** in front of them, `Sign_Curse` on the south wall, then
      the **EXIT** sign and, in the **far south-west corner**, a **small** lit gold ring
      (2 m radius, gold bar floating over it) turned toward you. The `ENTRY HALL` sign is now
      on the north wall beside the door you came in by.
- [ ] You can reach and read every sign and practise nudge / pull and all five curses on the
      dummy without the tutorial ending. Standing at the old spot (about 5 m south-west of the
      hall's centre) and at the hall's respawn point does **not** end it.
- [ ] Launch into the ring: the tutorial ends and the menu returns. Landing a metre outside
      its painted edge must not end it (the trigger is a 3.6 m square box, 3.5 m tall, turned
      with the ring: its sides sit just inside the 4 m circle, its four corners poke about
      0.5 m past the edge).

## 10. Round 10: settings screen + role reset (2026-09-30; implemented, untested)

Details in `docs/SETTINGS.md`; the bug's cause in `docs/BUILD-LOG.md` round 10.

### 10.1 The Mage HUD no longer survives a round (two players, then three)

- [ ] Host + one client. Play a run to its end (or let the timer run out: set `Run.asset`
      `timerSeconds` low for the test). Note who was the Mage (the result banner names them).
- [ ] Back on the room page the host presses START again, **several times over several runs**.
      Each run: exactly the players the host drew see "YOU ARE A FRAGMENT ..." and the ability bar;
      **everybody else sees "YOU ARE A WEAPON" and no bar**, whatever they were last run. Check
      the case the owner hit: Mage last run, weapon this run -> no bar, keys 1-5 and clicks do
      nothing, no nudge marker floats over an airborne player. And the reverse: weapon last run, Mage this run -> the bar.
- [ ] Same with the **host** as last run's Mage (the host's own role resets too).
- [ ] The reveal appears about 1.5 s into the run, not later than ~4.5 s, and says the right
      thing the first time (it no longer guesses before the role has arrived).
- [ ] With the debug overlay on (`?debug=1` or the Editor), the console of every player prints
      one `reply: ROLE_ASSIGN - this peer is a Weapon` or `... Mage` per run (weapons used to
      print nothing).
- [ ] **Late joiner**: start a run with two players, then join a third mid-run. The third sees
      "YOU ARE A WEAPON", no bar, and its console prints the ROLE_ASSIGN Weapon line.
- [ ] Second run's timer: on the room page -> START, the timer is hidden until the new rooms are
      known, then shows 5:00 dim; it never flashes last run's red 0:00. The start room's south
      door leads into this run's first room, not last run's.
- [ ] Tutorial after a run (same machine): you are still the Mage in the practice hall.

### 10.2 Opening and closing the settings screen (solo tutorial is enough, then a run)

- [ ] In the tutorial, click to lock the pointer, then **Escape**: the dimmed sheet with the
      PAUSED panel appears, the cursor is free, the camera stops following the mouse.
- [ ] While it is open: Space does not launch, E does not possess, Q does not leave the weapon,
      WASD does not fly the soul or roll the Orb, left / right click and 1-5 do nothing (as the
      Mage in the practice hall, aim at the dummy first). The world keeps running behind it.
- [ ] **RESUME** closes it and the pointer locks again (in a browser it may take one more
      click). The view does **not** jump on the first mouse movement after resuming.
- [ ] Escape again (open -> Escape) closes it the same way.
- [ ] A click on the dark sheet **outside** the panel closes it too, and that click does not
      nudge anything (as the Mage, aim at the airborne dummy, open, click outside the panel).
- [ ] **Alt-tab** away while playing (pointer locked for more than half a second): coming back,
      the settings screen is open. Clicking out of the browser page does the same. In the
      browser, pressing Escape once while locked opens the screen and does **not** immediately
      close it again.
- [ ] Right after the level loads (before you ever clicked to lock), nothing opens by itself.
- [ ] MainMenu: Escape does nothing new; the room page and title page work as before.

### 10.3 The settings themselves

- [ ] **Sensitivity**: drag to 0.20x - the view turns very slowly; to 3.00x - fast. The value
      label follows the slider. It applies while you drag (look after resuming). Quit and start
      again (or reload the page): the value is kept.
- [ ] **Audio**: MASTER 0 silences the game (if anything makes sound), 100 restores; MUSIC, SFX,
      VOICE move and show their numbers but change nothing (the caption says so). All four are
      kept after a restart.

### 10.4 Keybinds

- [ ] KEYBINDS shows 16 rows: LAUNCH / SOUL UP (SPACE), POSSESS (E), LEAVE WEAPON (Q), MOVE
      FORWARD / BACK / LEFT / RIGHT (W S A D), SOUL DOWN (SHIFT), MAP (TAB), MAGE: NUDGE (LMB),
      MAGE: PULL (RMB), MAGE: CURSE 1-5 (1-5). No LOOK, no PAUSE / ESC row.
- [ ] Click POSSESS's key: it turns amber and reads PRESS A KEY; press F: it reads F with an
      amber border, the status line says "saved". Resume: F possesses, E does nothing.
- [ ] Rebind MAGE: NUDGE to a key or to the middle mouse button; it works in the practice hall.
- [ ] While a key reads PRESS A KEY, **Escape** keeps the old key ("kept the old key") and does
      **not** close the settings screen.
- [ ] Restart the game (or reload the page): the rebinds are still there, in the tutorial and in
      a run. **RESET ALL** puts every key back and the amber borders go.
- [ ] BACK returns to the PAUSED page.

### 10.5 EXIT TO MAIN MENU

- [ ] Tutorial (offline): EXIT goes straight to the title page, no question.
- [ ] **Client** in a two-player run: EXIT -> the client lands on the title page with "you left
      the room"; the host's run goes on and the client's weapon / soul disappears for the host.
- [ ] **Host** with a client in the room: EXIT shows the confirm page ("YOU ARE THE HOST.
      LEAVING ENDS THE SESSION FOR EVERYBODY ..."). CANCEL returns to the PAUSED page. END THE
      SESSION AND EXIT: the host lands on the title page with "you ended the session for
      everyone"; the client lands on the title page with "the host left the room".
- [ ] After any EXIT, HOST / JOIN / TUTORIAL from the title page work normally.

## 11. Round 11: settings polish (2026-09-30; implemented, untested)

- [ ] The MOUSE card has only SENSITIVITY and the KEYBINDS button: **no SPIKE FILTER row**.
      KEYBINDS has a clear gap above it (about the gap between two cards), not flush under the
      slider row.
- [ ] The spike filter is still on: a very hard flick under pointer lock still adds to
      `lookDrops` on the debug overlay, as in section 6.2.
- [ ] KEYBINDS page: the scrollbar on the right is **thin and dark** (dark track, grey rounded
      thumb, lighter on hover), with **no arrow buttons**, and sits a little apart from the key
      buttons without covering them. No horizontal bar.
- [ ] The mouse wheel scrolls the list; dragging the thumb scrolls it; scrolled to the end, the
      last row (MAGE: CURSE 5) is fully visible with its bottom line.

## 12. Round 12: voting + the pause (2026-10-01; implemented, untested)

Design, rules and risks: `docs/VOTING.md`. Everything below is untested.

### 12.1 Solo, in the tutorial
- [ ] In the practice hall the ability bar shows a **V - Call vote** slot FIRST (left of Nudge / Pull),
      the curses after it. Before the hall (HUD asleep) V does nothing.
- [ ] Read `Sign_Vote` on the west wall. Press **V**: everything freezes (the dummy stops hopping, mid-air
      if it was up; your weapon stops dead; no launch, no E / Q, WASD does nothing), the cursor appears,
      and the vote screen says `VOTE CALLED BY <your name>`, a 25 s countdown, one card `DUMMY`, SKIP
      VOTE, CONFIRM greyed.
- [ ] Click DUMMY (amber border), CONFIRM: your chip (your colour, your first three letters) appears on
      the card at once, the screen says "your vote is in", and the result follows immediately (you are
      the only voter): `DUMMY WAS BANISHED` / `they were A WEAPON`.
- [ ] About 4 s later the screen closes, the pointer re-locks, the dummy is gone, the weapon moves again
      (a weapon frozen mid-air carries on along its arc). The Vote slot reads **GONE**; V shows a quiet
      line.
- [ ] Escape during the meeting opens the settings screen on top; RESUME leaves the cursor free and the
      vote screen up; nothing in the world reacts to clicks.
- [ ] Leave the hall through the entry door and come back: the dummy is back and hopping; V works again.
      SKIP VOTE then CONFIRM: `NO ONE WAS BANISHED`, the dummy stays.
- [ ] Curse the dummy (1-5), then call a vote at once: the curse's countdown over its head STOPS during
      the meeting and resumes after.
- [ ] KEYBINDS lists CALL VOTE (V); rebinding it works.

### 12.2 Two players (Editor hosts, a build joins; one is the Mage)
- [ ] Both bars show the Vote slot; the Mage's shows its powers after it. In the start room, before
      anybody leaves, the slot reads **WAIT** and V says "no votes before the timer starts".
- [ ] Leave the start room (the timer starts): for 30 s the slot shows a cooldown shade counting down;
      after it the slot is clear. On the host and on the client alike.
- [ ] The weapon presses V while the Mage is in a launch: **both** screens freeze at once (the Mage's
      weapon hangs in the air on both machines, no drift, no sliding), the **timer stops** on both, the
      vote screen opens on both with both names, `YOU` on your own card, the caller named in the header.
- [ ] Each votes the other: the chips land on both screens as each vote comes in; the second vote
      resolves the meeting at once (no waiting for the countdown). A 1-1 tie: `NO ONE WAS BANISHED`.
- [ ] Nobody votes: the countdown runs to 0 and resolves to nobody. Only one votes for the other: that
      one is banished (plurality 1-0).
- [ ] After the result the game thaws on both; the timer resumes from where it stopped (compare the
      clock before and after: the difference is the meeting plus 4 s, not 0).
- [ ] The caller's slot now reads **USED**; the other's shows the 45 s group cooldown counting down, then
      clears. A second call by the same player: refused with "your vote call is used up".
- [ ] Banish the WEAPON (the Mage votes it, it skips): its weapon breaks on both machines, its soul pops
      out, the result says `they were A WEAPON`, its chip on the Mage's screen reads `BANISHED - A
      WEAPON`, its own HUD shows the purple BANISHED line, its bar has only a greyed Vote slot (OUT).
      Then: the run ends right after the thaw with `EVERY WEAPON WAS BANISHED - THE ARCH MAGE WINS` (it
      was the only weapon). With three players (two weapons) the run goes on instead.
- [ ] The ghost flies **through a teleport door** (the soul passes the opening and comes out of the
      twin, the camera turns with it) but not through a locked exit door; E on a weapon does nothing; it
      cannot call or vote in the next meeting (the screen says it is watching).
- [ ] Banish the MAGE: `they were THE ARCH MAGE`; the run does NOT end; the Mage's bar loses its powers;
      1-5 and clicks do nothing; the weapon(s) still have to reach the Exit before the timer; the result
      banner at the end lists `banished: <name> (the mage)`.
- [ ] Exit rule: with three players, banish one weapon; the remaining weapon alone in the Exit room wins.
- [ ] Goblins: start a meeting with a hostile goblin mid-chase; it stops dead (host and client), its
      strike does not land during the pause, and it resumes the chase afterwards.
- [ ] The nudge / curse cooldowns on the Mage's bar do not run down during the pause (compare before /
      after), and the host does not refuse a curse that the bar says is ready right after the thaw.
- [ ] Late join during a meeting (three players): the joiner lands frozen with the vote screen open,
      read-only ("you are watching"), and thaws with everybody.
- [ ] A client leaves mid-meeting: the meeting resolves without its vote (the others' votes decide, or
      the countdown runs out).
