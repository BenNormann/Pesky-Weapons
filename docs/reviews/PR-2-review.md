# Review: PR #2 "Add fixed four-room puzzle run and polished banana"

**Verdict: request changes.** The C# is small and sound: no wire change, a host-only picker, and it respects the pause. The blockers are content and design. The Banana build deleted a Mace from the start rack. The run overrides one of your decisions. Two of the four rooms are solo platforming courses. Names break the grey-box rule throughout.

Paths are under `Pesky Weapons Unity/Assets/`.

## What the run is now
`Run.asset` sets `roomsPerRun` to 4 and `guaranteedRoomIds` to [3, 4, 5, 6]. `RunLayoutPicker` places the guaranteed ids first and fills any remaining slots at random. Whatever the seed, every run is Start, Resonance Forge, Clockwork Canteen, Magnet Mayhem, Counterweight Comedy, Exit. The random pool still exists (19 blank lever rooms in `RunDirector.pool`), but nothing is drawn from it unless `roomsPerRun` is above 4.

## Must fix
1. **Mace_7 is gone from the start rack.** `BananaWeaponBuilder.InstallOnRack` (Scripts/Editor/BananaWeaponBuilder.cs:132-138) deletes whatever weapon homes on `Slot_7`, and that was `Mace_7`. The rack in `LabyrinthRoom_Start.prefab` (Run and Labyrinth) is now 2 Swords, Dagger, Mace, Staff, Hammer and Banana. Counterweight needs the Mace (item 5), so the run now depends on a single Mace. Restore it. Whether the Banana goes on the rack, and in which slot, is your call.
2. **Your decisions are overridden without sign-off.** PREMISE (2026-09-29) says five random rooms. This PR makes it four fixed rooms. Canteen and Magnet Mayhem are movement courses (the new doc calls this a "PLATFORM RUN"), the contraption/platformer direction you rejected, and one weapon can solve either alone. None of the designs came to you before they were built.
3. **Grey-box names.** Flavour names appear in:
   - the `Labyrinth.asset` labels, which show on door labels
   - the RoomVolume names
   - object names such as `FriendshipElevator` and `DefinitelyNotAFridgeMagnet`
   - the joke signs
   - the banner `THE WEAPONS ESCAPED (SOMEHOW)` (Game/UI/RunHud.cs:51)
4. **Resonance Forge with three players.** It is always room 1 and needs three weapon bodies on the pans at once. With 3 players (1 Mage, 2 weapons), the Mage can refuse and stall until time runs out, and banishing him doesn't help. The only way around it is to launch a weapon at the doorway and release it mid-air (loose weapons do pass MagicDoors, MagicDoor.cs:7-15). That relies on loose-weapon physics, which is not synced between machines. Gate the room by party size or accept the risk explicitly.

## Should fix
5. **The Counterweight sign describes an order that fails.** "PARK A HAMMER OR MACE... A FRIEND RIDES THE OTHER PAN UP" doesn't work:
   - The first weapon on a level pan always sinks it (deadzone is `< 0.5`, CounterweightPair.cs:52-53; even the Banana weighs 0.5), so the empty pan rises to 6 m, out of reach.
   - Only near-simultaneous boarding of both pans works.
   - The Hammer can't board a 3 m pan from the 1.3 m steps (+1.7 m against a 1.57 m budget), so it is Mace only.
   - A Dagger or Banana can probably skip the room alone: land on a level pan and launch to the 5.4 m balcony before the pan sinks.
6. **The rooms are fully unpacked, not variants** of `LabyrinthRoom_TwoDoor` (`PrefabUnpackMode.Completely`, RunPuzzleRoomsBuilder.cs:154, ResonanceForgeRoomBuilder.cs:56, whose summary still says "variant"). The eight MagicDoors and the walls are copies that no future prefab fix reaches, which contradicts RUN.md section 8. This is most of the +36k lines.
7. **The Canteen trays clip.** Over the 210 s cycle:
   - Tray_B is inside MiddleStep 59% of the time.
   - Tray_C is inside GoalBalcony 58% of the time.
   - Tray_B and Tray_C overlap 17% of the time.

   The 3.6 m rider box is wider than the 3 m tray, so weapons on the balcony get dragged by a tray passing under them (RunPuzzleRoomsBuilder.cs:76-78, 206-207).
8. **Magnet Mayhem:** WoodRoute_C (top 3.5) sits under MagnetLanding_C (underside 3.875), so about 4 m² of the last non-metal step has 0.375 m headroom. Metal weapons just walk under Magnet_C and step onto the balcony.
9. **The three exit levers** float 0.35 m above their balconies.
10. **The signs face away.** Text is on the board's -Z face, so `Instructions` and `JokeSign` read from the south. Players enter from the north and see blank backs.
11. **The `Pesky/Rooms/Build Resonance Forge` menu** resets `guaranteedRoomIds` to [3] (ResonanceForgeRoomBuilder.cs:215-224). Running it later silently makes the run Forge plus 3 random rooms. Delete it.
12. **`Labyrinth.unity` churn.** The Banana menu re-saves this set-aside scene and renumbers every scene id in it. Revert the file.
13. **The ClockMover brake defaults to 18** (ClockMover.cs:39), so the Tutorial's mover changes too, and `CarryRiders` now runs an OverlapBox every step even when the mover is still (:90). Is that intended?
14. **"Q LEAVES A BODY IN PLACE"** points players at weapon bodies whose physics isn't synced. The host latches on its own physics, but each peer colours the pans from its own reading (ScalesLock.cs:107). Tell players to stay possessed on the pans instead.
15. **Stale text.**
    - RUN.md (sections 1, 4.1, 5, 6.2 and 8) and PREMISE are not updated.
    - The Tutorial's `Sign_Run` still says "FIVE ROOMS" (Tutorial.unity:3929).
    - The RunSceneBuilder.cs:280 comment says the builder is "safe to rerun". It isn't: `BuildRunScene` recopies Labyrinth and would drop PauseGate, VoteCaller and Settings.

## Nits
- `weaponSub` hard-codes "four rooms" (RunHud.cs:48).
- `PuzzleRoomProfile` has no reader.
- The three builders duplicate their helpers.
- The room shells are still square 24 x 24.
- The PR description is empty.

## The Banana
Not just visuals:
- **Hitbox:** 3 capsules (radius 0.06, about 0.7 m long) become 5 (radius 0.12, about 1.2 m long), about twice as thick and 1.7 times as long. Inertia and centre of mass shift with it.
- **Unchanged:** mass 0.5, launch 13, and `P_Bouncy` (re-applied by `ApplyDef`).
- **Rack:** it takes a Mace's slot.

## Netcode and PR #1
- The RUN_LAYOUT format is unchanged. The picker runs on the host only (RunRule.cs:120) and gives the same result for the same seed, so no protocol bump is needed and late joiners still get the layout from the snapshot.
- New movement runs on LevelClock, so the vote pause freezes it. Nothing new reads Time.time or RoomClock.
- The exit rule, banishment, role reset and AbilityBar are untouched.
- PR #1 is already merged. This branch sits on that merge and fast-forwards (GitHub: MERGEABLE), and PauseGate, VoteCaller, VoteScreen and SettingsFlow survive in `Run.unity`. Until the fixes land, nobody else should edit `Run.unity` or `LabyrinthRoom_Start.prefab`: scene YAML conflicts can't be merged by hand.

## Hygiene and tests
- **Hygiene:** the `Run.unity` diff is deliberate (rooms 3-6 replaced, ids renumbered by the deterministic id pass, the Banana swap). The new kit is registered and wired. No missing scripts, `.meta` files or GUIDs.
- **Picker tests:** these should compile, and they test real behaviour (order, dedupe, same result for the same seed).
- **`ResonanceForgeTests`:** these mostly pin authored numbers (player counts 3/4/8, at least 10 renderers) and will break on any tuning. Their door-wiring checks are the useful part. I have not run any of the tests.

## Good
- The picker is small, pure and tested.
- The rooms reuse host-authoritative kit, with no new shared state.
- No weapons or enemies in rooms, and every exit latches.
- The builds are repeatable editor automation, not hand-written YAML.

## Needs play to confirm
- How the trays feel.
- The Counterweight boarding window and the Dagger/Banana skip.
- Ferrying a loose weapon through a door.
- The pans with remote players.
- Whether the signs are readable.
