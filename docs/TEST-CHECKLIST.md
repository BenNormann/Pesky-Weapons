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
* [ ] Hold Tab: a 3x3 map with nine rooms, the mage bar under it, and one crew
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

- As a **weapon**, hold Tab. You should see **only the pad** — a blank dark
  canvas — with **no map, no grid, no room names, nothing about the labyrinth**.
- Left mouse draws in **your slot colour**. Right mouse erases. The ERASER and
  PEN buttons do the same thing; THIN / MEDIUM / THICK change the width.
- The cursor should be **free** while Tab is held and **re-lock** when you let
  go, for a weapon as well as a Mage.
- Draw one very long unbroken squiggle (more than about 64 points) and check it
  has **no gap** in it where it was split.
- As the **Mage**, hold Tab: there should be two top tabs, **MAP** and **PAD**.
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
