# VOTING (and the pause state)

The owner's decisions of 2026-10-01 (`docs/PREMISE.md`, last section): V calls a vote, an Among Us style
meeting freezes the whole game, the plurality banishes a player for good and reveals their role. This
file: what travels on the wire, what the host decides, every number, how the PAUSE works and how to
reuse it, what BANISHMENT does, and the tutorial's practice vote.

**Implemented, untested** (round 12, 2026-10-01). Checks made: a clean compile after every script
change (0 errors), every new message encoded and decoded once in the Editor (sizes as listed below), the
Vote snapshot part written and read back through `WorldSim.WritePart / ReadPart` (65 bytes, nothing
left over), the pause arithmetic checked on a sim (a pause from tick 123 to 623 takes 500 ticks out of
game time), `Run.uxml` instantiated in edit mode (all 23 elements the HUD and the vote screen query
present, `VoteView` valid, the bar built with 7 slots), every serialized reference set and read back,
and the edit-mode scene validator on `Boot`, `MainMenu`, `Tutorial`, `Run` and `Dev/FeelBox` (0
problems each). **Nothing was run: no play mode, no test, no socket, no build.**

---

## 1. The idea in one paragraph

Anybody presses **V** (one call per player per run, not in the first 30 s of the timer, not within 45 s
of the last meeting). The host freezes the game for everybody (**PAUSE_BEGIN**) and opens a 25 s
meeting (**VOTE_START**): the vote screen comes up on every machine with a two-column grid of the
players, SKIP VOTE at the bottom and the countdown; voice chat is the discussion. Each player clicks a
name or SKIP and confirms **one final vote**; the host broadcasts it (**VOTE_TALLY**) and everybody
sees the voter's chip land beside the candidate. At the deadline, or as soon as everybody has voted,
the host resolves: the plurality is banished; a tie, or SKIP on top, banishes nobody (**VOTE_END**,
which also reveals the banished player's role). The result stays on screen for 4 s, then the game
runs again (**PAUSE_END**): the timer, the goblins, the movers and the cooldowns resume exactly where
they stopped. A banished player's body breaks and they spectate as a **ghost**: a soul that may pass
teleport doors and touch nothing. Banishing the Mage does **not** end the run; banishing the last
weapon does (the Mage wins).

---

## 2. The wire

`Wire.ProtocolVersion` is **7** (eight new ids and a new snapshot part). Layouts in
`Protocol/Messages/VoteMessages.cs`, enums in `Protocol/VoteEnums.cs`. A new domain, **0x70-0x77**
(0x78-0x7E free).

| Id | Name | Kind | Bytes | Layout after the type byte |
|---|---|---|---|---|
| 0x70 | PAUSE_BEGIN | Event | 6 | `tick u32, reason u8 (PauseReason: 0 None, 1 Vote)` |
| 0x71 | PAUSE_END | Event | 6 | `tick u32, reason u8` |
| 0x72 | VOTE_CALL_REQ | Intent | 2 | `candidate u8` - `VoteTarget.None` (0xFF) for a real vote, `VoteTarget.Dummy` (0xFE) for the tutorial's practice vote |
| 0x73 | VOTE_START | Event | 13 | `tick u32, caller u8, deadlineTick u32 (a sim tick), eligibleMask u8, candidateMask u8, flags u8 (VoteFlags: 1 Practice)` |
| 0x74 | VOTE_CAST_REQ | Intent | 2 | `target u8` - a candidate slot, `VoteTarget.Skip` (0xFD), or `Dummy` in a practice vote |
| 0x75 | VOTE_TALLY | Event | 7 | `tick u32, voter u8, target u8` |
| 0x76 | VOTE_END | Event | 15 | `tick u32, banished u8 (a slot, Dummy, or None), role u8 (LabyrinthRole of the banished), tally u8 x 8 (each slot's final vote: a target, Skip, or None)` |
| 0x77 | VOTE_REFUSED | Reply | 4 | `reason u8 (VoteRefusal), waitTenths u16` - to the asker alone |

`VoteRefusal`: 0 NoRound, 1 Banished, 2 NoCallsLeft, 3 GroupCooldown, 4 TooEarly, 5 AlreadyPaused,
6 NoMeeting, 7 NotEligible, 8 AlreadyVoted, 9 BadTarget, 10 NoTarget. `RoundOutcome` gained
`WeaponsGone = 4`.

`MessageInfo` has all eight. `MessageApplier.Apply` has cases for the five events (the sim keeps all
of them: `PauseState` and `VoteState`). VOTE_REFUSED reaches Game through `NetSession.ReplyReceived`
(`ApplyReply` ignores it, like CURSE_REFUSED).

**Snapshot.** `SnapshotPartKind.Vote = 8`, `End = 9` (SnapshotReceiver sizes its array from `End`),
appended to `SnapshotCodec.Order` and `WorldSim.Hash`. Body: the pause (`paused bool, reason u8,
pauseStartTick u32, pausedTotalTicks u32`: 10 bytes) then the vote (`banishedMask u8, revealedRole u8
x 8, callsUsed u8 x 8, meetingOpen bool, caller u8, deadlineTick u32, eligibleMask u8, candidateMask
u8, flags u8, voteOf u8 x 8, hasResult bool, lastBanished u8, lastRole u8, lastEndTick u32,
lastEndGameTick u32, meetingsHeld u16, lastTally u8 x 8`: 55 bytes). A late joiner lands frozen if a
meeting is on (the vote screen opens from the snapshot, read-only) and knows who is a ghost.

**Secrecy.** VOTE_END is the one event, beside ROUND_RESULT, that names a role - the banished
player's, who is out for good. A living player's role never leaves the host's `LabyrinthRule`; nothing
in the vote state says anything about it (`RevealedRole` is Weapon for everybody not banished and means
nothing). Votes themselves are public by design.

---

## 3. The pause state - and how to reuse it

**What it is.** `Sim/PauseState.cs` (`sim.Pause`): `Paused`, `Reason`, `PauseStartTick` (the
PAUSE_BEGIN header tick) and `PausedTotalTicks` (every finished pause added up). Host owned: only a
host rule emits PAUSE_BEGIN / PAUSE_END (`PauseState.BeginPayload / EndPayload(header, reason)`),
every peer applies them through the normal event path at the same tick, and the snapshot carries the
state. Any phase change resets it.

**Two clocks.** ROOM time never stops: the sim tick, `RoomClock`, the clock pings and a meeting's own
deadline all run on it. GAME time does: `PauseState.GameTick(tick)` = the sim tick minus every paused
tick (the current pause included), and `PausedMsAt(roomMs)` the same in milliseconds.
`SessionRunner.GameMs` = room ms minus the pauses, `SessionRunner.LevelMs` = GameMs since the round
went to Playing, and **`LevelClock` follows LevelMs** - snapping exactly while paused instead of the
usual 50 ms tolerance (and in FixedUpdate too), so nothing scheduled on it creeps. Everything that read
`LevelClock` already pauses for free: `ClockMover`, `Lift`, `Rope`, `CounterweightPair`,
`LightningField`, the practice dummy's hop, the curse expiries (`CurseEffects`). What read room ticks
or `Time` was moved to game time: the **run timer** (RUN_START's `startTick` / `deadlineTick` are now
GAME ticks; `RunRule` checks the deadline against `GameTick(tick)`; `RunHud` counts down from
`SessionRunner.GameMs`), the host's **curse and nudge cooldowns** (`RunRule` / `LabyrinthRule` keep
`GameTick` stamps), and the local cooldown guides on the ability bar (`MageCurse` / `MageNudge` shift
`_readyAt` by the pause's length on PAUSE_END).

**What freezes on every peer, from PAUSE_BEGIN to PAUSE_END** (`Game/PauseGate.cs`, `_Managers/PauseGate`,
polled every frame so a soul spawned mid-pause is frozen too):

| What | How |
|---|---|
| the local player's inputs | `OrbitCamera.InputEnabled = false`, the one overlay flag the settings screen uses: no launch, possess, release, Orb roll, soul flight (`PlayerSoul`), nudge (`MageNudge`), curse (`MageCurse`) or vote call (`VoteCaller`). The settings screen itself may still open (Escape) and close; `SettingsFlow.Close` asks the gate and leaves the input off while paused |
| the local body | `PlayerSoul.SetFrozen(true)`: the body it drives (its weapon, or the soul when free) keeps its velocities in memory, zeroes them and goes **kinematic**; `SetFrozen(false)` gives them back and wakes it. Only the body frozen is thawed, and only while still kinematic: a weapon broken meanwhile (a banishment) is thawed quietly so its respawn finds a dynamic body, and the soul that popped out was never frozen |
| a free soul | the same: kinematic, zero velocity; `PlayerSoul.FixedUpdate` does nothing while frozen |
| remote views | `RemotePlayerView.SetPaused(true)`: no interpolation or extrapolation, the view sits on the last sample (and snaps to a new one, e.g. the soul popping out of a banished weapon); on the thaw the ring is emptied so the next sample snaps |
| POSE streaming | no flag needed: the frozen body sends its resting pose once (Airborne drops, velocity 0), then keepalives; `PoseStreamer` sends at once on either edge |
| goblin brains (host) | `GoblinBrain.NetPaused()`: `agent.isStopped = true`, the FSM is not ticked (no state time, no perception, no strike), and on the thaw every `Time.time` stamp the brain keeps (`_flashUntil`, `_wanderPauseUntil`, `_ignoreCuriousUntil`, `_lastHurtTime`, `_lastAnimateSeen`, `_porterCooldownUntil`, `_lookSince`) is moved forward by the pause's length. Puppets need nothing: their rows stop |
| the host's refusals | HIT_CLAIM, KIT_REQ, BAT_CLAIM, POSSESS_REQ, RELEASE_REQ are dropped while paused (`sim.Pause.Paused` in each validator); the host's own kit publications (`WorldAuthority.SubmitKit`) and magic-door traversals (`RequestMagicDoorTraverse`, both overloads) are refused; `RunRule.CheckEndings` does nothing while paused (so a banishment's `WeaponsGone` fires after the thaw). On PAUSE_END the host re-evaluates every door |
| the pointer | the vote screen frees it on open; on PAUSE_END the gate re-locks it and gives the input back unless the settings screen or the vote screen is still open |

**Not frozen** (known, documented): loose (unheld) weapons keep their local physics (they are at rest
almost always); a broken weapon's respawn tick is a room tick (a weapon may respawn onto the rack during
a pause); the clock pings and TIME_SYNC run on.

**To pause for a new reason:** add a `PauseReason` value, have a host rule
`events.Emit(PauseState.BeginPayload(events.Header(), reason))` and later `EndPayload`, and refuse
VOTE_CALL_REQ meanwhile if you want (VoteRule already refuses while `sim.Pause.Paused`). Nothing else:
every peer's gate, clock and brains read `sim.Pause`.

---

## 4. The rules, and who decides

Everything below is the host: `VoteRule` (`Session/Rules/VoteRule.cs`), an `IHostRule` and the
`IIntentValidator` for VOTE_CALL_REQ and VOTE_CAST_REQ, registered after `RunRule` in
`HostAuthority.CreateDefault()`; it runs only when `GameData.mode` is `Run`. It has no secret table: the
role it reveals is read from `LabyrinthRule.IsMage` on the same host, never copied.

### 4.1 Calling (VOTE_CALL_REQ)

In order, each failure a VOTE_REFUSED to the asker alone: a round open and the asker present
(`NoRound`); not banished (`Banished`); no pause and no meeting running (`AlreadyPaused`); then,
for a real vote: a call left (`voteCallsPerPlayer`, `NoCallsLeft`); the group cooldown since the last
meeting's end in game ticks (`voteGroupCooldown`, `GroupCooldown` + the wait); the run timer running
(`TooEarly`, wait 0: "no votes at all before the timer starts") and `voteNoVoteBeforeSeconds` past its
start (`TooEarly` + the wait). A **practice** call (candidate = Dummy) skips the limits and the timing and
only needs the dummy to exist (`IHostWorld.TryGetPracticeTarget`, `NoTarget`).

Accepted: **PAUSE_BEGIN(Vote)** then **VOTE_START** at the same tick, in that order. `caller` = the
asker; `deadlineTick` = now + `voteMeetingSeconds` in sim ticks (the meeting runs on room time: the game
is paused); `eligibleMask` = every present, non-banished player (fragments included); `candidateMask` =
the same set (the caller may be voted for). Practice: eligible = the caller alone, no candidates, flags
`Practice`. The caller's call is spent on VOTE_START (`VoteState.CallsUsed`).

### 4.2 Casting (VOTE_CAST_REQ)

A meeting open (`NoMeeting`); not banished (`Banished`); listed as a voter (`NotEligible`); not voted
yet (`AlreadyVoted`: **one final vote**, no changing); the target a candidate of this meeting, or SKIP
(`BadTarget`). Accepted: **VOTE_TALLY(voter, target)** to everybody, and if every eligible voter still
present has now voted, the meeting resolves at once.

### 4.3 Resolving

At `deadlineTick`, or when every eligible voter present has voted (a leaver's missing vote never holds
the meeting). Count the votes per candidate; `top` = the largest count. Banish the top candidate when
`top > 0`, no other candidate has `top`, and `skip < top`. Otherwise nobody. Non-voters count for
nothing. **VOTE_END**(banished, role, the eight final votes). If a player was banished and holds a
weapon, **WEAPON_BROKEN**(HostForced) for it right after (the normal break path). Then
`voteResultSeconds` later **PAUSE_END(Vote)**.

### 4.4 The end of a round

`VoteState` resets on the edge into Playing and `PauseState` on every phase change. A round that ends
mid-meeting (the host leaves) just ends; the vote screen closes with the phase. A slot that empties
(PEER_SLOTS) loses its banishment, its calls and its vote, so a newcomer in it starts clean.

---

## 5. Banishment

Host state: `VoteState.BanishedMask` and `RevealedRole[slot]`, set by VOTE_END, snapshotted, cleared
when the slot empties and at round start. Everything else follows it:

- **The body breaks** (WEAPON_BROKEN, HostForced, from the vote rule). The soul pops out where the
  weapon was, during the pause, and holds still until PAUSE_END.
- **A ghost** (`PlayerSoul.IsGhost`, following `WorldAuthority.LocalIsGhost` every frame, so a late
  joiner or a resync lands right): a free soul that flies as before; its collider ignores the
  `SoulBarrier` layer (`Collider.excludeLayers`), the `MagicDoor` sensor watches it like a weapon and
  `WorldAuthority.RequestMagicDoorTraverse(door, soul)` moves it through with its motion turned - a
  locked gate still stops it; it cannot possess (`TryPossess` / `Possess` refuse; the host's
  `PossessValidator` refuses a banished slot too), nudge or curse (`LabyrinthRule` / `RunRule` refuse in
  silence; `MageCurse` / `MageNudge` are off), call or vote (`VoteRule`, and the Vote slot says OUT).
  Goblins ignore souls already; it reports no contacts (it holds no weapon).
- **What others see**: the remote chip reads `NAME` / `BANISHED - THE ARCH MAGE` or `A WEAPON`
  (`RemotePlayerSpawner`). The ghost's own HUD shows a line at the top (`RunHud` `ghost-status`):
  "BANISHED - YOU WERE ... SPECTATE AS A GHOST".
- **The Exit rule** (`RunRule.CheckEndings`) counts only non-Mage, **non-banished** present players; a
  banished weapon is neither needed nor counted. If a banishment leaves no non-Mage standing the round
  ends at once (after the thaw) with `RoundOutcome.WeaponsGone` (SESSION_END CrewLost): "EVERY WEAPON
  WAS BANISHED - THE ARCH MAGE WINS". A round that never had a weapon (a solo Mage) runs on as before.
  A banished player cannot start the run timer by leaving the start room.
- **A banished Mage**: the run goes on; his powers are gone; the bar shows only a disabled Vote slot
  (`RunHud.RefreshBar` hides the mageOnly slots for a ghost). ROUND_RESULT still names him among the
  fragments, and the result banner lists everybody banished with the role each reveal named.

---

## 6. Every tunable

On `Assets/Data/Run.asset` (`RunDef`; `Run_Tutorial.asset` has the same values, read back 45 s - the
practice vote skips the limits anyway). Both assets took the C# defaults without an edit.

| Field | Default | What |
|---|---|---|
| `voteCallsPerPlayer` | **1** | calls per player per run (spent on VOTE_START) |
| `voteGroupCooldown` | **45 s** | after any meeting ends, in game time, before anybody may call |
| `voteNoVoteBeforeSeconds` | **30 s** | after the run timer starts; before it starts, no votes at all |
| `voteMeetingSeconds` | **25 s** | the meeting, in room time; shorter once everybody has voted |
| `voteResultSeconds` | **4 s** | the result on screen, still paused, before PAUSE_END |

`VoteScreen` and `RunHud` carry every string as serialized text; `RemotePlayerSpawner` the two chip tags.
`Gameplay/Vote` = `<Keyboard>/v` + `<Gamepad>/rightShoulder` (the KEYBINDS page lists it as CALL VOTE).

---

## 7. The screen - `Assets/UI/Run.uxml` + `Run.uss` (`vote-root`)

Shared by `Run.unity` and `Tutorial.unity`: it lives in the run HUD's document, so `_UI/RunHud` carries
a second component, `VoteScreen` (`Game/UI/VoteScreen.cs`), and the elements are read by
`Game/UI/VoteView.cs` (queries, events, no decisions - like `SettingsView`). Opened by VOTE_START, or by
`Update` when the sim says a meeting is open and the game is paused (a late joiner's snapshot); closed
by PAUSE_END or a phase change.

| Element | What |
|---|---|
| `vote-root` | the dimmed sheet (display none until opened) |
| `vote-title` | `VOTE CALLED BY <name>` |
| `vote-timer` | the seconds left on the meeting (room time against `deadlineTick`), red at 5 |
| `vote-grid` | two columns of `vote-card` buttons, one per present player: slot colour swatch, callsign, a `YOU` tag, `BANISHED` greyed and unclickable; up to 8 |
| `vote-skip` | SKIP VOTE, with its chips row beside it |
| `vote-confirm` | enabled once a choice is made; after it the choice is locked (`is-selected`), the rest dim (`is-locked`) |
| `vote-chips` | every voter's chip (slot colour, three letters) beside what they voted for, rebuilt whenever `VoteState.Rev` moves |
| `vote-status` | "your vote is in", "you are watching: you have no vote in this meeting" (a ghost, a late joiner), a refusal |
| `vote-result` | `NAME WAS BANISHED` / `they were THE ARCH MAGE` or `A WEAPON`, or `NO ONE WAS BANISHED` / `a tie, or SKIP on top: everybody stays`; `DUMMY WAS BANISHED` in the tutorial |

Pointer: the screen frees it on open; the pause has already turned the input off, so the clicks reach
nothing in the world; PAUSE_END re-locks it (PauseGate). Escape still opens the settings screen on top
(sortingOrder 50 above the run HUD's 1); closing it leaves the cursor free while the vote screen is up.
The gameplay scenes still have no EventSystem (the settings screen's risk 4 applies here too).

---

## 8. The ability bar now

Everybody has the bar (`RunHud.RefreshBar`: shown while Playing). First slot, for all: **V - Call vote**
(`AbilityCooldownSource.Vote`, `mageOnly = false`). A Mage also sees Nudge / Pull and the five curses
(`mageOnly = true`, `AbilityBar.SetMageSlots`); a banished Mage keeps only the Vote slot. The Vote slot
shows the seconds of a running group cooldown or of the "no votes yet" wait (as a cooldown shade), or a
word when V would do nothing: **USED**, **WAIT** (the timer has not started), **PAUSED** (a meeting is
on), **OUT** (banished), **GONE** (the tutorial's dummy was voted out). `VoteCaller` pushes these every
frame (`RunHud.SetCooldown` / `SetAbilityLock`); the host still decides. Both scenes' `RunHud.abilities`
lists were rewritten to the new default (7 entries, the vote first).

---

## 9. The tutorial

`Tutorial.unity`'s practice hall gained `Sign_Vote` on the west wall (local (-10.5, 0, 2), facing east,
sign id 2078) and `DummyRestore` (local (6, 2, 5), a 10 x 4 x 8 trigger box over the entry door's
arrival, on the same layer as `TutorialGate_MageLesson`): a repeatable `TutorialTrigger` that switches
`PracticeDummy` back on. Pressing V in the hall (the HUD awake) sends VOTE_CALL_REQ with candidate
**Dummy**: the host (this player, loopback) freezes the game - the dummy stops hopping, the body holds -
and opens the real vote screen with the dummy as the only candidate (`VoteFlags.Practice`), SKIP VOTE
still offered. Voting the dummy out resolves at once (one voter): `DUMMY WAS BANISHED - they were A
WEAPON`, the dummy's GameObject is switched off (`WorldAuthority.OnVoteEnd`, host), 4 s later the game
thaws. Until the hall is re-entered the Vote slot reads GONE and the host refuses with `NoTarget`. The
practice vote spends no call and ignores the cooldown and the timing (there is no run timer in the
tutorial). The NavMesh was not rebaked: nothing on the World layer moved.

---

## 10. Riskiest untested assumptions

1. **Nothing has been run.** Not solo, not in two tabs.
2. **A kinematic frozen weapon.** `WeaponBody.FixedUpdate` runs on a kinematic body as it does for a
   racked weapon; if Unity warns about velocity writes on kinematic bodies, `AlignBlade` is the place.
3. **The soul popping out of a banished weapon during the pause** is dynamic with zero velocity and no
   gravity; `PlayerSoul.FixedUpdate` is skipped while frozen, so it should hold still.
4. **UI Toolkit `flex-wrap` and percentage widths** for the two-column grid; if the cards stack in one
   column, give `vote-card` a fixed width (320 px) instead of 49 %.
5. **No EventSystem in the gameplay scenes** (as the settings screen).
6. **The host's 3-tick lead**: the host applies PAUSE_BEGIN 150 ms before the clients; its game clock
   stops 150 ms after theirs in room terms, which is the same instant in game terms.
7. **Goblin timer shift** covers the stamps listed in `GoblinBrainNet.NetPaused`; a stamp added later to
   the brain needs a line there.
8. **`Collider.excludeLayers`** on the soul's collider is assumed to beat the layer matrix for the
   SoulBarrier layer (it does in Unity 2022.2+).
9. **The entry trigger's box** is placed by the hall's local numbers (the door at local (8, 0, 9));
   if arrivals land outside it the dummy never comes back until the scene reloads.
10. **The local cooldown guides** (`MageCurse` / `MageNudge` `_readyAt`) shift by the pause on PAUSE_END
    even when no cooldown ran; harmless, but a guide that was already in the past stays in the past.
