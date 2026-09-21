# BACK TOWER — how to test each room

Rooms 6 to 12 plus the two lift stops (a, b), furnished in stage 4 of the tower build.
**Everything here is implemented but untested — nothing in this list has been run.**

How to get there: open `Assets/Scenes/Boot.unity` and press Play (Boot loads `Zone1`). Start in Room 1,
take a weapon off the rack, work through rooms 2-5, and go through the Arena's north doorway — that is
now the magic doorway into Room 6. To jump straight to a room while testing, move
`_Managers/PlayerSpawner`'s spawn or drag the player's soul spawn, or set `_Managers/FloorActivator`
`cullFloors` = false so you can see every floor at once in the Scene view.

Useful knobs while testing: `Assets/Data/MovementTuning.asset` (lift map, stick settings),
`Assets/Data/Weapons/*.asset` (launch speeds), `Assets/Data/Enemies/GoblinPorter.asset` (porter timings),
and each kit instance's own Inspector fields.

---

## Room 6 — Rope Room (floor y 12)
- Arrive as a **blunt** weapon (Mace or Hammer). Confirm you cannot cross the 7 m chasm and cannot cut
  the rope: hitting the cord should bounce you off, not cut it.
- Press Q, fly the soul to `Dagger_06` on the stand at (-3.5, 12, 97.4), press E, then hit the rope at
  speed. The cord should vanish, the cut ends appear, and the deck should fall 5 m in about 1.2 s and
  come to rest flush with the floor, spanning the chasm.
- Walk/launch across the bridge, go out through the far doorway, come back: the bridge must still be
  down (the cut latches).
- Deliberately fall into the pit. You should land on the pit floor (y 9), be able to launch onto
  `Pit_Step` (y 10.5) and then back onto the near floor (y 12) with **any** weapon, including the Hammer.
- Check the held deck is genuinely out of reach before the cut: try to land on it with the Dagger at
  maximum lift. It should be impossible.

## Room 7 — Barred Door (floor y 36)
- Read both signs: the barred door should say "TOWER DOOR / BARRED FROM OUTSIDE", and the sign by the
  magic doorway should point up.
- Try to open the barred door by standing in front of it and by hitting it. It must never open and must
  block you solidly. The three bars should read as bars, not as a doorway.
- Confirm the room is otherwise empty and quick to cross, and that the magic doorway at the wide end
  takes you straight up into Room 8.
- Check the signs are readable from inside the room (this stage moved `Sign.prefab`'s label to the other
  side of its board; every sign in the game is affected, so glance at the older rooms too).

## Room 8 — Pot Room (floor y 48)
- Arrive as a **bladed** weapon. The exit magic doorway should be a solid panel and the HUD prompt should
  tell you the door wants the lever.
- Hit a pot with the blade: it should bounce off and the pot should survive.
- Q / E onto `Mace_08` on the stand at (-4, 48, 97) and smash pots. Four of the five are empty; `Pot_04`
  at (4.5, 48, 100.5) holds the lever.
- Hit the revealed lever at 4 m/s or more. It should flip once, stay on, and the exit door should slide
  open and stay open.
- Go through to Room 9 and come straight back: the return trip must work even before the lever (the
  Room 9 side of the pair is always open), and the door must still be open after the lever.

## Room 9 — Armoury (floor y 60)
- Take damage in Room 8 or 10, come back, stand in the `Anvil_09` trigger at (-5, 60, 99) and hold E for
  2 s out of combat. HP should go to full.
- Possess `Staff_09` on the stand at (-5, 60, 105). Check the HUD name and that it launches at 11 m/s
  (noticeably floatier than the Mace).
- Stand in front of the east doorway (the one to the lift stop). The HUD should say KEY 2 is needed and
  the door must stay shut — key 2 does not exist yet in this build.
- Confirm all three magic doorways (to 8, to 10, to b) are where you expect and that leaving and
  returning does not move the Staff off its slot.

## Room 10 — Carry Room (floor y 72)
- Walk into the south arm and let the porter see you. Then **stop moving** for 2 s. It should walk over,
  pick you up (you rise to its carry socket) and carry you north.
- Watch the gate: it should slide open only while the carrying porter is within 4 m, and shut behind it.
  Confirm you cannot open it yourself, as a weapon or as a free soul.
- Let it set you down on the stand at (-9, 72, 110.8). It should not pick you up again while you sit
  there.
- Move (launch) while in its hands: it should drop you and turn hostile.
- From the far side, climb the four steps (y 73.5 / 75 / 76.5 / 78) and launch over the 7.5 m wall back
  to the south side. Try this with the Hammer, which is the worst case.
- Watch the patrolling goblin north of the gate: it should walk its loop, pause about 1.5 s at each
  point, and only notice you inside its 110 degree cone.
- **Most likely to be wrong:** the porter pathing through its own gate. If it stalls at the wall, the
  NavMesh under (-6.5, 72, 104) is the thing to look at.

## Room 11 — Magnet Room (floor y 84, decks at y 88)
- Climb the three steps onto the entry deck with the heaviest weapon you have. Rises are 1.5 / 1.5 / 1.0 m.
- As a **metal** weapon (`Sword_11` is on the deck), step into the magnet's volume. You should be lifted to
  about y 90.8 and held there, counting as grounded, and be able to launch along the strip. Three hops
  should cross the 12 m gap.
- As the **Staff** (wooden), confirm the magnet ignores you completely and cross on `Stone_1` and
  `Stone_2` instead (2.67 m gaps).
- Fall into the pit on purpose. You should be able to climb `PitStep_1` / `PitStep_2` back onto the entry
  deck only — not out at the far end — and try again.
- Check that a metal weapon standing on either landing is not yanked off it: the magnet volume should
  start at z 96 and end at z 108.

## Room 12 — Beam Climb (floor y 96, exit at y 108)
- Look up: the exit doorway is now 12 m above the floor, with a ledge in front of it.
- Possess `Dagger_12` at (-3.5, 96, 98) and launch at a wooden post. It should stick point-first, hold
  you, and count as grounded so you can launch again.
- Climb post to post around the ring (4.53 m across, about 2 m up each time) to the ledge at y 108 and
  go through the doorway.
- Try the same climb with the Sword: it should be possible to stick but very hard to make the gaps.
  That is intended; the Dagger is the authored answer.
- Try it with a **blunt** weapon: it must not stick at all, so the shaft is a dead end for it (soul-swap
  to the Dagger instead).
- Come back from Room 13: you should arrive on the ledge at y 108, not on the floor.

## Room a — Lift Bottom (floor y 0)
- Come through the magic doorway from Room 2. The new `Landing_A` pad should put you level with the
  parked lift platform without a jump worth thinking about.
- Confirm the lift does **nothing**: it is off until the Room 19 winch (stage 5), so it sits parked at
  its top stop, 192 m up, and the shaft is an empty drop.
- Drop a weapon down the shaft from higher up if you can reach one, and confirm it lands on the shaft
  floor and can get back to Room 2 through the magic doorway (this is the no-soft-lock case).
- Read the sign: it should say the lift is dead until the winch.

## Room b — Lift Stop (floor y 60)
- You cannot reach this room in this build: its only door is the key-2 door off the Armoury, and key 2
  does not exist yet. Inspect it in the Scene view instead.
- Check `Landing_B` (top y 60, z 40.80 to 42.07) lines up with the lift platform's north edge (z 40.7)
  and the existing threshold (z 42.04), so there is a 0.1 m step and not a 1.3 m jump.
- Check the HUD prompt box in front of the door (`KeyDoorPrompt_B`) sits where a weapon would stand.
- When key 2 exists (stage 5), come back and confirm both doors of the pair open together.
