# SLICE-1: mechanics slice (AUTHORITATIVE)

Owner's corrections of 2026-09-18. Where DESIGN.md or IMPLEMENTATION-PLAN.md disagree with this
file, THIS FILE WINS. Scope: nail the mechanics in the tutorial room plus four more rooms, solo
and offline. Multiplayer (the ATCK netcode port) comes after the owner approves the feel.

## 1. Owner's rules (verbatim intent)
- NO WASD. Look (mouse) and jump (Space) are the ONLY way a weapon moves. One exception: the Orb
  weapon can also roll with WASD.
- Camera is third person, orbits the weapon, and the weapon's rotation never affects it.
- No sniper-scope zoom. (A scope attachment may exist later; it never zooms.)
- Grey-box naming only: Sword, Dagger, Mace, Hammer, Staff, Orb, Banana, Goblin, Key, Door,
  Plate, Anvil, Rune_Metal, Room1_WeaponRoom ... No flavour names anywhere.
- Death: you become a floating soul orb that flies around and possesses another weapon.
  Modifiers are lost. You can also leave your weapon on purpose with a key; if you do it in
  combat the weapon breaks. Its purpose is swapping weapons for puzzles.
- The game starts in a weapon room: every weapon sits on a rack, a short platforming tutorial
  leads out of it, and the next room is "slay a goblin".
- All Unity work goes through the Unity MCP the way an experienced human developer works, and
  the scenes must be set up properly (section 7).

## 2. Movement
- Space = launch. Velocity is SET (not added) to `dir * launchSpeed`, so tuning is direct and
  mass does not change the arc. Launch cooldown 0.25 s. A little launch torque for tumble.
- `dir`: horizontal heading = camera yaw. Elevation comes from camera pitch through a tunable
  linear map: the resting camera (looking ~20 deg down at the weapon) gives the minimum lift
  of 15 deg ("a little up"); tilting the view up raises it to a max of 80 deg. Never below 15
  deg when grounded.
- Allowed only when grounded (a contact with normal.y > 0.6 in the last 0.1 s), OR once per
  airtime as a WALL JUMP while touching a wall (|normal.y| < 0.3 in the last 0.15 s). The wall
  jump must rebound: if the aim points into the wall it is reflected about the wall normal,
  then blended with the normal and the lift so that dot(dir, wallNormal) > 0.3. The counter
  resets on ground contact. (Later the Wind rune adds one air jump.)
- A LineRenderer trajectory preview (about 1 s of ballistic arc) shows where a launch will go.
- Weapon Rigidbody: free rotation, Continuous Dynamic, Interpolate, per-weapon mass, a physics
  material with enough friction and angular drag to settle. Gravity is -20. Falling below
  y = -20 returns the weapon to its last grounded position. No fall damage.
- Orb only: WASD applies camera-relative rolling torque.

## 3. Soul and possession
- The player is a Soul. It is either possessing a weapon or free.
- Free soul: glowing sphere r 0.25, no gravity, collides with World ONLY (doors block it;
  enemies, weapons and plates ignore it; enemies never target it). Hold Space = thrust along the
  full 3D camera forward (accel 18, max 7 m/s); release = drag to a stop. It cannot fight,
  carry, press plates or take pickups.
- E = possess the best free weapon within 2 m (smallest angle to the view). HUD shows a prompt.
- Q = leave the weapon. Out of combat the weapon just drops there, keeps its HP and modifiers,
  and anyone can possess it again. IN COMBAT (damage dealt or taken in the last 5 s, or a goblin
  is targeting you) the weapon BREAKS.
- Weapon HP 0 = the weapon breaks and the soul pops out where it died.
- Modifiers live on the weapon. A weapon that breaks loses them. A broken weapon vanishes
  (small debris puff) and reappears on its home slot, full HP, after 10 s, so the game can
  never run out of weapons.
- Keys are party inventory: never lost.

## 4. Weapons (WeaponDef ScriptableObject; all numbers are starting values)
| Weapon | launchSpeed | mass | damage | HP | Grey-box shape |
|---|---|---|---|---|---|
| Sword | 12 | 3 | 15 | 100 | thin box blade + cross-guard + grip |
| Dagger | 14 | 1 | 9 | 70 | short blade + grip |
| Staff | 11 | 2.5 | 12 | 90 | long cylinder |
| Orb | 10 | 4 | 10 | 90 | sphere (rolls with WASD) |
| Mace | 10 | 8 | 20 | 120 | cylinder + sphere head |
| Hammer | 9 | 14 | 28 | 140 | cylinder + big box head |
| Banana | 13 | 0.5 | 6 | 60 | 3 bent yellow capsules, bouncy |
Ball and chain is stubbed (needs joints).

## 5. Combat
- Impact damage: `damage * smoothstep(3, 12, relativeSpeed)`, 0.35 s per-target hit cooldown,
  knockback on the goblin, hit flash.
- Goblin: green capsule r 0.5 h 1.4, NavMeshAgent speed 3, HP 30, aggro 8 m inside its own room
  only, targets possessed weapons only. Attack: range 1.4 m, 0.5 s telegraph (turns red and
  swells), 15 damage + knockback, 1.2 s cooldown. Simple FSM: Idle, Chase, Windup, Recover, Dead.

## 6. Rooms (laid out along +Z; walls 0.5 m thick; doorways 3 wide x 3.5 high; hallways 4 x 6 x 4)
| Room | Size x,z,h | Contents | Exit opens when |
|---|---|---|---|
| Room1_WeaponRoom | 16,24,10 | soul spawn points, rack with all 7 weapons, tutorial: a 0.8 m step, a 4 m gap (pit has steps out, never a death pit), a 2.5 m exit ledge beside a wall to rebound from, short sign texts | always open (on the ledge) |
| Room2_Goblin | 14,14,6 | 1 goblin | the goblin is dead |
| Room3_KeyClimb | 14,18,14 | clock-driven moving platform over a 6 m pit (steps out), wall-jump climb to a 5 m ledge holding the Key | always open |
| Room4_Plate | 12,12,6 | a second Hammer on a stand (its home slot), Plate with mass threshold 10 that LATCHES the door open | plate latched |
| (hallway) | | Key Door | party has the Key |
| Room5_Arena | 20,20,8 | 3 goblins (wake on entry), Anvil (out of combat, hold E 2 s = full HP), Rune_Metal appears when all 3 are dead (touch = +25% damage on this weapon) | sealed |
| Room6-Room10 | simple boxes | layout shells only: floor, walls, doorways, no gameplay | - |
Every mandatory jump must be checked against the numbers in section 4 (ballistics with g = 20,
then a play-mode test) for every weapon that is required to make it; fix the GEOMETRY, not the
feel numbers. Room3's key ledge may exclude Mace and Hammer (the key is shared); Room1 and all
door thresholds must be passable by all seven.
Moving things are a pure function of a shared clock (`LevelClock.Ms`), never of deltaTime sums.

## 7. Proper Unity setup (non-negotiable)
- Scenes: `Assets/Scenes/Boot.unity` (index 0: bootstrap object only, loads Zone1),
  `Assets/Scenes/Zone1.unity` (index 1), `Assets/Scenes/Dev/FeelBox.unity` (not in the build).
  SampleScene is left alone and is not in the build list.
- Zone1 hierarchy: `_Managers`, `_Cameras`, `_Lighting`, `_UI`, `Environment/<RoomN_Name>/{Geometry,
  Gameplay,Lighting,Spawns}`, `Hallways`, `Shells`. Level geometry is AUTHORED in the scene from
  prefabs and primitives, never generated at runtime. Geometry is static, on the World layer.
  One NavMeshSurface on Environment, baked. One Main Camera, one AudioListener, one directional
  light plus a point light per room, the URP global volume kept.
- Everything repeated is a prefab under `Assets/Prefabs/{Weapons,Player,Enemies,Kit,Rooms}`.
  Data is ScriptableObjects under `Assets/Data`. Materials: a small flat-colour palette under
  `Assets/Materials`. Scripts under `Assets/Scripts/{Data,Game,Editor}` with asmdefs
  `Pesky.Data`, `Pesky.Game`, `Pesky.Editor`, tests in `Assets/Tests` (`Pesky.Tests`).
- References are wired through serialized fields in the Inspector. No `GameObject.Find`, no
  `FindObjectOfType` in gameplay code, no Resources.Load.
- Input: an InputActionAsset under `Assets/Input` (Look, Jump, Possess=E, Release=Q, Roll=WASD,
  Pause=Esc), referenced by serialized fields. Pointer lock on click, Esc frees it.
- Layers: World, Weapon, Soul, Enemy, Trigger, Pickup, Debris, with a collision matrix that
  matches section 3. Every interactive object carries a stable serialized integer id.
- Netcode seam: every shared state change (hit, pickup, possess, release, break, plate, door,
  anvil) goes through one `WorldAuthority` component as request -> validate -> event. Offline it
  applies at once. Views only react to its events. This is what the ATCK port replaces later.
- HUD (UI Toolkit: UXML + USS + PanelSettings): weapon name, HP bar, modifiers, key, context
  prompt, IN COMBAT tag, soul hints.
- After every script change: wait for compile, read the console, fix errors before moving on.
  Save scenes. Never hand-write .unity / .prefab / .asset / .meta files.

## 8. Mouse-look spike filter (feedback round 5)

Chrome under pointer lock occasionally reports a huge single-frame mouse delta for
no reason at all; on the web that used to land as a wild camera jerk. Every mouse
delta now goes through `Assets/Scripts/Game/LookFilter.cs` (ported from ATCK's
LookFilter) inside `OrbitCamera.Update`, the only consumer of the `Look` action
(the soul's flight and the launch aim read the camera's yaw / pitch, never the
raw delta). The tunables live on **`Assets/Data/LookTuning.asset`**
(`Pesky.Data.LookTuning`, referenced by every scene's `OrbitCamera`):

| Field | Default | Meaning |
|---|---|---|
| `filterSpikes` | on | Master switch. |
| `spikeMode` | Drop | `Drop` throws the frame's delta away; `Scale` keeps its direction and shrinks it to `scaleToPixels`. |
| `spikePixels` | 300 | A single frame's delta longer than this many pixels is a spike (18,000 px/s at 60 fps; a hard flick at 20 fps is about 150). Raise it if slow frames make real flicks trip the filter. |
| `scaleToPixels` | 40 | Scale mode only: the length a spike is shrunk to. |
| `dropFirstDeltaAfterLock` | on | The first non-zero delta after the pointer lock is acquired, after the window regains focus, and after the Tab overlay closes (`OrbitCamera.InputEnabled` false -> true) is discarded: it carries the cursor's jump to the centre and whatever the Input System accumulated while nobody read it. |
| `logDrops` | on | Print every dropped / scaled delta (`[pesky] look: ...`) where DebugGate is open. |

Two classic causes were checked and are not present: the delta is a per-frame
pixel count multiplied by `lookSensitivity` only, **never by `deltaTime`** (a
hitch would otherwise become a huge turn), and the first delta after a lock /
overlay change is consumed rather than applied. The debug overlay's `lookDrops`
counter and the console lines show the filter engaging.
