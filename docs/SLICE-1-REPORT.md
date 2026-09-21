# Pesky Weapons — Slice 1 report

## How to play
Open `Assets/Scenes/Boot.unity` and press Play; Boot loads `Zone1`. Click in the Game view to lock the
pointer (Esc frees it). You start as a free soul in Room 1 by a rack of seven weapons: possess one and
hop it through five rooms to the sealed door.

## Controls
| Input | Free soul | Possessing a weapon |
|---|---|---|
| Mouse | look / fly direction | aim — camera pitch sets the launch lift (pitch +20 = 15 deg, -35 = 80 deg) |
| Space | thrust | launch (0.25 s cooldown; one wall jump per airtime) |
| E | possess the weapon under the prompt | hold 2 s at the Anvil, out of combat, for full HP |
| Q | — | release (in combat this breaks the weapon; it respawns on its rack after 10 s) |
| WASD | — | roll — Orb only, nothing else reads it |
| Esc | frees the pointer. The Pause action is bound but no handler listens to it yet |

The HUD shows weapon name, HP, modifiers, the key tag, IN COMBAT, the context prompt and soul hints.

## Tuning knobs (Inspector only, no code change)
| Asset | Fields |
|---|---|
| `Assets/Data/Weapons/*.asset` | `launchSpeed`, `launchTorque`, `mass`, `linearDrag`, `angularDrag`, `physicsMaterial`, `damage`, `maxHp`, `canRoll`, `rollTorque`. After changing mass/drag/material run menu **Pesky / Weapons / Apply Defs To Prefabs** |
| `Assets/Data/MovementTuning.asset` | lift map (`minLiftDeg` 15, `maxLiftDeg` 80, `restingCameraPitchDeg` 20, `maxLiftCameraPitchDeg` -35), `launchCooldown` 0.25, `compensateIntegrator`, ground/wall masks and graces, `wallJumpsPerAirtime` 1, `killY` -20, preview settings, `gravity` 20 (preview only) |
| `Assets/Prefabs/Player/PlayerSoul.prefab` | `thrustAccel` 18, `maxSpeed` 7, `releaseDrag` 4, `possessRange` 2, `combatSeconds` 5, `popOutLift` 0.5 |
| `Main Camera` in **both** scenes (`OrbitCamera`, scene override) | `lookSensitivity` 2, `pivotOffset` (0,1,0), `distance` 5, `minPitch` -35, `maxPitch` 75, `restingPitch` 20, `followSmoothTime` 0.08, `collisionRadius` 0.25 |
| `Assets/Input/PeskyControls.inputactions` | mouse sensitivity = the `scaleVector2` 0.06 processor on Look / `<Mouse>/delta` |
| `Assets/Prefabs/Weapons/*.prefab` (`WeaponBody`) | `respawnDelay` 10, `maxAngularVelocity` 25 |
| `Assets/Data/Enemies/Goblin.asset`, `Assets/Data/Modifiers/Rune_Metal.asset` | goblin HP/speed/damage/windup; rune damage multiplier 1.25 |
| Kit prefabs under `Assets/Prefabs/Kit` | `Plate` threshold 10 / restSpeed 1.5, `Anvil` chargeSeconds 2, `Door` openOffset / slideSeconds 0.8, `ClockMover` points + period |

## Stubbed or rough
Anvil's 2 s hold was never machine-verified (the gate behind it was) — try it by hand. Plate "resting"
is a speed test, not a contact test. Knockback uses `NavMeshAgent.Move`, so goblins never ragdoll. No
debris puff — a broken weapon just vanishes. Ball and chain is stubbed per spec. Rooms 6-10 are empty
shells; hallways and shells have no ceilings, deliberately. The rune hides only at runtime, so it shows
in the Scene view. After a geometry change, re-bake `Environment`'s NavMeshSurface and run
**Pesky / Validate Open Scenes**.
