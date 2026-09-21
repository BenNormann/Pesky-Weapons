# Pesky Weapons — Game Design Document

> **SUPERSEDED IN PART (2026-09-18).** `docs/SLICE-1.md` is authoritative. The owner rejected:
> any WASD / ground locomotion for weapons (look + jump only; only the Orb rolls), scope zoom,
> and flavour names (grey-box names only). Death / weapon swapping is now a free-flying soul orb
> that possesses weapons. The game opens in a weapon-rack room with a platforming tutorial, then
> a goblin room. Treat everything below as background ideas, not as the spec.

**Version 0.1 — pre-implementation design for the one-shot grey-box slice.**
Unity 6.3 / URP / new Input System / AI Navigation 2.0. WebGL, P2P WebRTC, room codes, no server, 1–4 players (tolerate 8), host-authoritative shared state, owner-authoritative player bodies.

You are a disembodied soul bound into one of the late Arch Mage's weapons. He is dead. His tower is falling apart. The doors still lock. You have no hands, no legs, and one (1) way to move: throw yourself.

---

## 1. Pillars

- **You are the projectile.** Every verb — travel, fight, press a button, open a door — is the same verb: launch your body and hit something with the correct end of yourself.
- **The lock graph is the level.** Rooms exist to teach a verb, test it for a key, or reward curiosity with a modifier. Nothing in the tower is a corridor for its own sake.
- **Mass is a personality.** A warhammer that leaps 3.5 m and a banana that leaps 11.3 m are not balanced against each other; they are balanced *around* each other. Heavy holds the plate, light makes the jump.
- **Soft-lock-proof by construction.** Keys are never consumed, doors latch open forever, plates ratchet, every weapon is re-selectable at a Rack. Four friends who scatter in four directions cannot break the run.
- **Dignity is not a stat.** These weapons are pesky: they tumble, they squeak, they land pommel-first in a soup cauldron. The comedy comes from the physics, not from written jokes.

---

## 2. Movement model

### Controls
| Input | Action |
|---|---|
| Mouse | Orbit camera (pointer-locked), yaw+pitch |
| WASD | **Shuffle** — ground locomotion |
| Space | **Launch** (ground) / **Rebound** (wall window) / **Gust** (Wind Rune) |
| RMB (hold) | Aim assist line / Sniper Scope zoom |
| E | Interact (anvil, rack, key plinth, lever) |
| Q | Ping (see §10) |
| Tab | Party & modifier panel |

### Ground locomotion — the shuffle
Grounded, WASD applies `F = mass × 14 N/kg` in the camera-relative input direction, clamped to **2.4 m/s**, plus a matching torque `mass × 1.2 N·m` so the body flops end-over-end instead of sliding. A sword scrapes; an orb rolls; a ball-and-chain drags its head and flails its handle. The shuffle is deliberately slow — it exists to *aim* the launch, not to travel.

The **movement direction `d̂`** for the launch is the normalised camera-relative input vector, flattened to the XZ plane, sampled over the last 0.10 s. If `|input| < 0.15`, `d̂ = camForwardFlat × 0.30` — a small forward hop, mostly vertical. This is exactly the owner's rule: you launch *in the direction you are moving*, with a little up when grounded.

### Launch impulse
Project gravity is **20 m/s²** (not 9.81 — it makes arcs crisp and readable in a browser). Rigidbody linear damping 0.15, angular damping 0.35.

```
Launch (grounded):
  v  = rb.linearVelocity
  v -= d̂ * max(0, dot(v, d̂)) * 0.80      // shed 80% of forward speed → crisp restart
  v.y = min(v.y, 0)                        // cancel any residual rise
  v += d̂ * S  +  Vector3.up * L            // S, L from WeaponDef
  rb.linearVelocity = v
  rb.AddTorque(randomUnit * S * 0.06 * mass, Impulse)   // the tumble
  wallCharge = 1;  launchCooldown = 0.22 s
```

`S` = launch speed (m/s), `L` = grounded lift (m/s). Two derived numbers drive all level geometry:

- **flat range** `R = S·L / 10` metres
- **apex** `A = L² / 40` metres

Grounded test: any contact whose normal·up ≥ 0.70 within the last 0.12 s (coyote time).

### Air control
Acceleration `H` m/s² (the *handling* stat), applied to the component of input **perpendicular** to current horizontal velocity at full strength, and to the forward component only until horizontal speed reaches `1.15 × S`. You can steer a lot and accelerate barely. An orb (H=16) carves; a warhammer (H=4) is a decision you made 0.6 s ago and must now live with.

### The wall rebound — exactly one, and it must rebound
Requirements, all of them:
1. Airborne, `wallCharge == 1`.
2. Contact with a surface whose normal satisfies `|n.y| < 0.64` (within ~50° of vertical).
3. **Incoming speed into the wall `dot(-v, n) ≥ 3.0 m/s`.** You cannot wall-jump off a wall you are resting against — you have to actually hit it. This is the "has to rebound off the wall" rule, enforced numerically.
4. Within a **0.30 s** window of the contact (0.40 s with Grip Cloth).

```
Rebound:
  r = Reflect(vHorizontal, n)
  d̂ = normalize(r * 0.70 + inputFlat * 0.30)
  if dot(d̂, n) < 0.35: d̂ = normalize(d̂ + n * 0.5)   // never let you hug the wall
  v = d̂ * (S * 0.85) + up * (L * 0.75) + up * max(0, vOld.y) * 0.20
  wallCharge = 0
```

`wallCharge` resets **only on grounding** (normal·up ≥ 0.70). Two facing walls therefore cannot be ladder-climbed: one rebound, then you land. The Wind Rune adds a *separate* `gustCharge` (§5) — a free mid-air launch at 0.80× stats — and that is the only thing that ever changes this rule.

### Orientation and tumble
No animation. The body is a free Rigidbody that genuinely tumbles. Two code-driven assists:
- **Flight bias:** while airborne and `speed > 6 m/s`, apply a torque (gain 1.8, damping 0.8) that eases the weapon's *business axis* toward its velocity vector — a thrown dagger wants to be point-first, a mace wants to be head-first. It is a suggestion, not a constraint; hitting a wall scrambles it.
- **Stick:** blades with `stickChance` embed into ProBuilder surfaces tagged `Wood` when the point/edge hits at ≥ 9 m/s within 25° of the surface normal. You hang there (kinematic anchor) for up to 2.5 s and can launch off it as if grounded — this **resets `wallCharge`**, which is the mechanism behind the wooden-beam platforming in R10. Purely owner-side; zero bytes on the wire.

### Camera
Third-person orbit, 4.0 m boom, 1.1 m above the body, FOV 65. The boom lengthens to 6.0 m when `speed > 14 m/s` and the FOV widens to 78 over 0.25 s. **The camera never inherits the tumble** — it tracks the Rigidbody's centre of mass with critically-damped smoothing (0.08 s), because a spinning camera in a browser is a vomit machine. Spherecast collision, radius 0.3 m.

---

## 3. Attack and damage model

You have no attack button. You hit things by *being thrown at them*.

```
Damage = 10 × dmgMult × speedTerm × angleTerm
  speedTerm = clamp((vClose - 4) / 8, 0, 3.0)      // 0 at 4 m/s, 1.0 at 12, capped at 28
  angleTerm:  point-first  (axis·v ≥ 0.85)         ×1.8   [piercing weapons]
              edge-first   (edge plane leads)      ×1.35  [bladed]
              head-first   (head leads)            ×1.50  [blunt]
              flat / haft / pommel                 ×0.45
```
`vClose` is the closing speed along the contact normal, measured on the attacker's client at the contact tick.

**Worked examples.** Dagger (0.70) at 18 m/s point-first → 22 dmg. Sword (1.00) at 16 m/s edge-first → 20 dmg. Warhammer (1.80) at 13 m/s head-first → 30 dmg. Banana (0.35) at 17 m/s → 5.7 dmg, which is why the banana's actual weapon is the peel.

**Knockback.** `impulse = mass × vClose × 0.8`, capped 40 N·s, along the contact velocity. Host applies it to host-owned enemies as a **stagger**: the agent leaves the NavMesh into a ballistic state for `clamp(impulse/12, 0.15, 1.2)` s, then re-snaps with `NavMesh.SamplePosition`.

**Weapon HP is your HP.** One bar. Durability and health are the same number; the soul does not care, the steel does. Damage sources: enemy hits (flat, per enemy), hazards (brazier 8 HP/s, censer 14 on contact), and **chipping** — landing on `Stone` above **16 m/s** costs `(v − 16) × 2.0` HP. A 9 m drop costs 6 HP. Landing on `Wood`, `Straw` or another player costs nothing.

**Reforge stations.** ProBuilder anvil + glowing cylinder. Hold E for 2.5 s → full HP, clears Bends (temporary −10% stat debuffs from repeated chipping), sets your respawn point, and **resets your `wallCharge` and `gustCharge`**. 20 s per-player cooldown. Unlimited uses. Host owns `lastReforgeTick[playerId]` (uint16) and the `anvilActivated` bit.

**Downed / revive.** At 0 HP your weapon shatters: you become a **Soul Wisp** — 0.35 m sphere, 60% of your `S` and `L`, no collision damage, 45 s timer — and your **Hilt** (a 0.4 m capsule) drops where you fell. Any player, including you as a wisp, can carry the Hilt into a Reforge Anvil's 2 m trigger to reforge you instantly at 100% HP. Otherwise the timer expires and you respawn at the party's last activated anvil at 60% HP. Solo, the wisp timer is 12 s and the respawn is free. Either way the party gains one **Tarnish** stack (+5% enemy HP, max 3, cleared on any boss kill) — drama without a run-ending punishment.

---

## 4. Weapon roster

Gravity 20 m/s². `R = S·L/10` (flat range), `A = L²/40` (apex). All grey-box shapes are Unity primitives or 4–8-face ProBuilder solids with flat URP Lit colours.

| Weapon | Mass kg | Dmg × | S m/s | L m/s | R m | A m | Handling H | HP | Signature trait | Grey-box |
|---|---|---|---|---|---|---|---|---|---|---|
| Rusty Arming Sword | 3.0 | 1.00 | 10.0 | 6.0 | 6.0 | 0.90 | 9 | 100 | Baseline. Sticks in wood (60%) | 1.0×0.12×0.04 box + 0.3 cross-guard box + 0.1 sphere |
| Paring Dagger "Mr Stabby" | 1.2 | 0.70 | 14.0 | 7.0 | 9.8 | 1.23 | 14 | 60 | Point-first hits crit ×1.4 on top of ×1.8 | 0.45 box + 0.15 grip cylinder |
| Duelling Rapier "Sir Whistle" | 2.2 | 1.10 | 12.5 | 6.2 | 7.8 | 0.96 | 13 | 80 | Pierces the first enemy, keeps 70% speed | 1.15 thin cylinder + torus guard |
| Oaken Quarterstaff "The Long Opinion" | 4.5 | 0.90 | 11.0 | 6.5 | 7.2 | 1.06 | 11 | 120 | Lands flat across ≤1.8 m gaps → becomes a kinematic bridge for 6 s | 1.9×0.09 cylinder |
| Bone Wand "Femur Deluxe" | 1.8 | 0.80 | 12.0 | 6.8 | 8.2 | 1.16 | 12 | 70 | Fires a 12-dmg bolt every 2.5 s (RMB, host-validated) | 0.55 capsule + 2 sphere knobs |
| Orb of Mild Magic "Gary" | 2.5 | 0.60 | 9.0 | 6.0 | 5.4 | 0.90 | 16 | 90 | Bounciness 0.75; easiest rebounds; no angle penalty ever | 0.28 sphere |
| Iron Mace "The Persuader" | 8.0 | 1.60 | 7.5 | 5.2 | 3.9 | 0.68 | 5 | 160 | Holds 6 kg plates alone | 0.7 cylinder + 0.22 icosphere head |
| Ball & Chain "Wrecking Gremlin" | 11.0 | 2.00 | 8.0 | 5.4 | 4.3 | 0.73 | 4 | 180 | Two bodies on a ConfigurableJoint; the head lands ~0.25 s after you, hitting twice | 0.5 handle + 1.2 m joint + 0.3 sphere |
| Warhammer "Tectonic Tim" | 9.5 | 1.80 | 7.0 | 5.0 | 3.5 | 0.63 | 4 | 200 | Landing above 12 m/s = 2.5 m shockwave, 12 dmg, staggers | 0.8 cylinder + 0.35×0.25×0.25 head |
| **The Banana "Ser Cavendish"** | 0.9 | 0.35 | 15.0 | 7.5 | 11.3 | 1.41 | 15 | 40 | On landing, drops a **peel** (8 s) that flings anything touching it at +6 m/s in a random horizontal direction. Loses 1 HP per chip. | 5-segment bent ProBuilder prism, yellow |

**Weapon Racks** let any player re-possess any *unlocked* weapon for free, instantly, at any Rack or Anvil. This is the single most important anti-soft-lock device in the game (§6).

---

## 5. Modifiers

**Sockets:** 3 per weapon (Head / Haft / Grip), one modifier each, freely swapped at any Anvil or Rack. **Gate abilities live in a separate Soul Slot that cannot be unequipped, and are granted party-wide to every player including late joiners.** You can never lose an ability you need.

| # | Modifier | Effect (numbers) | Type | How found | Slot |
|---|---|---|---|---|---|
| 1 | **Fire Rune** | Ignite on hit: 3 HP/s for 5 s; spreads to enemies within 1.5 m every 1 s | Optional power | Hidden — behind the R4 oven's breakable wall (Zone 1) | Head |
| 2 | **Wind Rune** | One mid-air **Gust** (double jump) at 0.80×S, 0.80×L; own charge; resets on ground/anvil | **GATE → Zone 2** | Hobnail's reward (Zone 1 boss) | Soul |
| 3 | **Metal Rune** | +25% damage, +1.5 kg mass (changes your plate class — deliberate trade) | Optional | Hidden — R5 breakable wall | Head |
| 4 | **Grip Cloth** | Handling +40%, wall-rebound window 0.30 → 0.40 s | Optional | Clearing the R4 Refectory (one per player) | Grip |
| 5 | **Beeswax** | Landing friction ×0.45 (you *slide*), chipping damage −50%, stick chance −30% | Optional | R8 candle niche, y=2.0 | Haft |
| 6 | **Sniper Scope** (bolted to the blade) | Hold RMB: 3.5× zoom + 20 m dotted arc; first hit within 3 s of a 1.2 s aim does +30%. Un-zoomed it occupies 20% of your screen because it is a brass tube welded across your own blade | Optional | R7 Twin Sconces co-op secret | Head |
| 7 | **Whetstone Rune** | Edge-first angleTerm 1.35 → 1.60 | Optional | Dropped, Crockery Hulk, 30% | Head |
| 8 | **Chain Rune** | Tether + reel to marked iron rings within 12 m | **GATE → Zone 3** | Zone 2 boss | Soul |
| 9 | **Stone Rune** | Press F: +6 kg for 3 s (20 s cooldown). Plate-holding and ground-pounds | **GATE → Zone 4** | Zone 3 boss | Soul |
| 10 | **Feather Charm** | −30% mass for 4 s after each launch, then snaps back mid-air | Optional | Hidden, Zone 2 | Haft |
| 11 | **Squeaky Pommel** | Every landing squeaks; enemies within 8 m turn to the noise for 2 s. An actual taunt, and it is humiliating | Optional | Dropped, Scrap Goblin, 8% | Grip |
| 12 | **Ribbon of Excessive Confidence** | +15% S, −20% max HP | Optional | R2 trough (fall in on purpose) | Grip |
| 13 | **Pickled Egg** | Once: negates lethal damage, restores 40 HP, is consumed, smells for 60 s (enemies aggro from +4 m) | Optional | R6 Larder | Haft |
| 14 | **Bell Charm** | Your impacts stun Bell-kin for 1.0 s | Boss reward | Hobnail (one per player) | Head |
| 15 | **Rubber Ferrule** | Bounciness +0.35. Does **not** grant extra wall charges | Optional | Zone 2 | Haft |
| 16 | **Googly Eyes** | Cosmetic. 12% chance an enemy is Unnerved (1 s hesitate) on first sight of you | Optional | R6 Larder | Grip |

---

## 6. Tower structure

### The five zones

**Zone 1 — The Armoury Floor.** Kitchens, refectory, scriptorium, belfry. New verb: *the launch and the single wall rebound*. Gate ability granted: **Wind Rune**. Boss: **Hobnail, the Bell-Bellied Ogre**. Tone: broken crockery and goblins who have discovered cutlery.

**Zone 2 — The Flooded Undercellar.** Waterlogged storerooms; water is a thick medium (drag 4.0) where light weapons bob and heavy weapons walk the bottom — an explicit mass puzzle. New verb: *double jump*, plus buoyancy routing. Gate granted: **Chain Rune**. Boss: **Mucilage, the Laundry Wraith** (a 6 m wet sheet with a lantern inside).

**Zone 3 — The Scriptorium Stacks.** A vertical library of sliding shelf-platforms and iron rings. New verb: *tether and reel*. Gate granted: **Stone Rune**. Boss: **Quill-Maester Vellum**, a 9 m animated lectern that writes hazards onto the floor.

**Zone 4 — The Orrery Gallery.** Everything is on a clock: rotating rings, counterweights, crushers. New verb: *the 6 kg weight-shift* to ride and stall counterweights. Gate granted: **Master Key of the Spire**. Boss: none (a gauntlet of three mini-bosses).

**Zone 5 — The Spire.** Wind, no floor, the Arch Mage's laboratory. Final boss: **The Arch Mage's Left Hand**, a 7 m gauntlet that is *still trying to finish his last spell*. Reward: the window. The end.

### The two geometry numbers (sequence-break audit)

The worst jumper is Tectonic Tim (`R` 3.5 m, `A` 0.63 m). The best is the banana (`R` 11.3 m, `A` 1.41 m).

- **Mandatory gaps ≤ 3.2 m.** Tim clears with 0.3 m spare plus air control.
- **Mandatory rises ≤ 0.55 m at ≤ 2.0 m horizontal.** Tim's trajectory is at y = +0.55 m on descent at t = 0.337 s, i.e. 2.36 m out. 0.36 m margin.
- **Gate rises ≥ 3.5 m with no wall inside 2.5 m of the approach.** Banana ceiling: apex 1.41 m; a rebound taken at apex adds `(0.75×7.5)²/40 = 0.79 m`; plus half body length 0.15 m → **2.35 m**. Margin 1.15 m.
- **Gate gaps ≥ 16 m, or hazard/void.** Banana ceiling: 11.3 m, +1.6 m from air control at 1.15×S, +12.9 m if it clips an angled wall mid-flight and rebounds forward → call it 14 m worst case. **No angled or convex wall is placed within 6 m of any gate gap.**

**Zone 1 obeys a stronger rule: every progression gate is a solid 0.6 m ProBuilder door or a 4.5 m flat wall. There are zero geometry gates on the critical path.** Geometry gating is reserved for optional secrets (R7's solo route), where a banana skipping it is a reward for picking the banana, not a bug.

### Zone 1 in detail — The Armoury Floor

World axes: **+X east, +Z north, +Y up.** All rooms are axis-aligned. Floor y = 0 unless noted.

| # | Room | Footprint (x, z) | Size / ceiling | Type | Contents | Locked by | Grants |
|---|---|---|---|---|---|---|---|
| R1 | **The Cracked Reliquary** | −8..8, −8..8 | 16×16, h 9 | Hub / reforge | 4 soul plinths (spawn), **Anvil RA-1** at (0,−5), **Rack WR-1** at (0,5), straw pile at (−6,6) | — | Spawn, weapon choice |
| R2 | **The Chipping Hall** | −4..4, 8..32 | 8×24, h 6 | Platforming (tutorial) | 4 m-deep trough z=12..30 with three crossings: gaps of 2.4 m (z=14), 3.0 m (z=20), 3.2 m (z=26, 1.2 m pillar mid-gap). Ramp out of the trough at its south end (z=12→14, y −4→0). **Ribbon of Excessive Confidence** in the trough | — | Teaches launch |
| R3 | **The Rebound Shaft** | −6..4, 32..42 | 10×10, h 16 | Platforming (key) | 12 spiral ledges 1.6×1.6 m, rise 0.55 m, spacing 2.0 m, y 0.60→6.65. Top platform 3×3 at y=7.2. Final 4.6 m crossing with a 0.6 m pillar at 2.3 m — **teaches the rebound**. **BRONZE KEY** on plinth | — | Bronze Key; taught wall rebound |
| R4 | **The Goblin Refectory** | 8..30, −8..8 | 22×16, h 7 | Combat | 6 Scrap Goblins, 2 Lantern Goblins. 4 oak tables 4×1×0.9 (wood — blades stick). **Rack WR-1b** at (10,−6). Great oven (28,6): breakable wall, 120 HP → **FIRE RUNE** | Bronze Door | Grip Cloth ×P; unlatches D-D |
| R5 | **The Plate Gallery** | 12..28, 8..24 | 16×16, h 8 | Puzzle | **Ratchet Plate** 2.4×2.4 at (20,12), threshold 6.0 kg, latches after 1.5 s. Alt solution: 3 **impact levers** on the west wall, ≥12 N·s each within 4 s. Breakable wall (80 HP) at (13,20) → **METAL RUNE**. Crack C-1 at (28,16) → R6 | D-D (opens on R4 clear) | Opens D-E permanently |
| R6 | **The Larder** | 28..36, 12..20 | 8×8, h 4 | Secret | **THE BANANA** (unlocks it at every Rack, party-wide, forever), Pickled Egg, Googly Eyes, 3 Fork Mobs | Breakable crack C-1, 80 HP | The Banana |
| R7 | **Twin Sconces** | 4..12, 24..32 | 8×8, h 6 | Optional / co-op | Two 1.6 m plates at (6,26) and (10,30), 4.0 kg each, must both be held within a 0.4 s window → niche opens at y=3.0. **Solo route:** 5 crumbling tiles 1.2×1.2 at y 1.2/1.9/2.6/2.6/3.0 around the west wall, 2.6 m apart | Nothing (optional) | **SNIPER SCOPE** ×P |
| R8 | **The Scriptorium** | 12..32, 24..44 | 20×20, h 11 | Puzzle / platforming (key) | 4 clock platforms 3×3×0.4 (24 s loop), 2 swinging censers (r=1.0, 5 m arm, 3.2 s period), central island 4×4 at y=6.5 with the **VERDIGRIS KEY**. **BEESWAX** in the candle niche at (14,26,y=2.0) | D-E | Verdigris Key; Beeswax |
| R9 | **The Reforge Undercroft** | 14..26, 44..56 | 12×12, h 6 | Reforge / safe | **Anvil RA-2** at (20,50), **Rack WR-2** at (17,53). Shortcut bar-lift at the west wall | Verdigris Door | Opens shortcut S-1 |
| R10 | **The Crumbling Gantry** | 14..26, 56..76 | 12×20, h 10 | Platforming | 3 m-wide gantry at y=4.0 over a 10 m drop into the Undercroft cellar (stairs back up at the south end). 14 crumbling tiles, 3 censers, 2 clock platforms, 4 wooden beams (stick targets, reset `wallCharge`) | — | — |
| R11 | **The Bell Chamber** | 7..33, 76..102 | 26×26, h 12 | Boss | **Hobnail**. 4 corner ledges 1.8×1.8 at y=3.5. Great bell r=2.0 at (20,89,y=8). Bell Alcove respawn niche at (20,77.5) | Portcullis (one-way in) | **WIND RUNE** ×P, Bell Charm ×P, opens Belfry Stair to Zone 2 |

**Door table (exact world positions, all doors 2.4 m wide × 3.0 m high, 0.6 m thick):**

| Door | Position | Wall | Connects | State |
|---|---|---|---|---|
| D-A | (0, 8) | R1 north | R1 ↔ R2 | Open from the start |
| D-B | (0, 32) | R2 north | R2 ↔ R3 | Open from the start |
| D-Bronze | (8, 0) | R1 east | R1 ↔ R4 | **Bronze Key** |
| D-D | (20, 8) | R4 north | R4 ↔ R5 | Opens on R4 combat clear |
| D-E | (20, 24) | R5 north | R5 ↔ R8 | Opens on Ratchet Plate latch |
| C-1 | (28, 16) | R5 east | R5 ↔ R6 | Breakable, 80 HP |
| D-F | (12, 28) | R8 west | R8 ↔ R7 | Open (R7 is optional) |
| D-Verdigris | (20, 44) | R8 north | R8 ↔ R9 | **Verdigris Key** |
| D-S1 | (14, 50) | R9 west | R9 ↔ shortcut S-1 | Bar-lift, opens from R9 side, then two-way |
| D-G | (20, 56) | R9 north | R9 ↔ R10 | Open arch |
| D-Bell | (20, 76) | R10 north | R10 ↔ R11 | Portcullis: passable inward always (trigger → Bell Alcove); opens both ways on boss death |
| H-1 | (−6, 6) at y=9 | R1 ceiling | R3 top → R1 | One-way drop onto straw (0 damage) |

**Shortcut S-1:** a 3 m-wide corridor (centreline) from (14,50) west to (−10,50), south to (−10,26), east into R2's west wall at (−4,26). It passes 2.5 m clear of R3's west wall (x=−6). Opened from the R9 side. Turns a 4-room trek back to the hub into 12 seconds.

**Layout sanity:** no two room volumes overlap. R3 (−6..4, 32..42) and R7 (4..12, 24..32) meet only at the point (4,32); R7's east wall (x=12, z 24..32) is shared with R8's west wall. The whole zone fits in a 68 × 110 m footprint and lays out on a 2 m grid with no further decisions.

R8's four clock platforms follow the same rule as static geometry: at the synchronised moment of each hop the platform-to-platform gap is ≤ 3.2 m and the rise ≤ 0.55 m, so the warhammer can make the climb to the island on the same schedule as the banana — it simply has less room for error. Platform phases are 0 s, 6 s, 12 s and 18 s on the 24 s loop; the full ascent from floor to island is one loop.

### ASCII map (schematic; the tables above are authoritative)

```
                                   NORTH ^          (schematic; ~1 char = 2 m)

                    +======================================+
                    |    R11   THE BELL CHAMBER   26 x 26  |   z  76..102
                    |   .ledge                     ledge.  |   reward: WIND RUNE x P
                    |               ( BELL )               |            BELL CHARM x P
                    |        [Bell Alcove = respawn]       |
                    +===============[P]====================+   P  = portcullis (20,76)
                                     |                         one-way IN; both ways after kill
                    +----------------+---------------------+
                    |    R10  CRUMBLING GANTRY   12 x 20   |   z  56..76
                    |   ~ ~ tiles ~ ~       ) censer (      |   gantry y=4 over a 10 m drop
                    |   ==beam== ~ ~ tiles ~ ~  ) censer (  |   cellar stairs return to R9
                    +---------------[G]--------------------+   G  = (20,56) open arch
   S-1 <==[S1]======|    R9   REFORGE UNDERCROFT   12 x 12 |   z  44..56
   (14,50)          |      [ANVIL RA-2]     [RACK WR-2]    |
     ||             +---------------[V]--------------------+   V  = VERDIGRIS DOOR (20,44)
     ||  +--------+ |    R8   THE SCRIPTORIUM     20 x 20  |   z  24..44
     ||  | R3     | |   [p]        # ISLAND y=6.5 #    [p] |   ~VERDIGRIS KEY~ on the island
     ||  |REBOUND | |        ) censer (     ) censer (     |   *BEESWAX* in the candle niche
     ||  |SHAFT   | |   [p]                            [p] |   [p] = clock platform, 24 s loop
     ||  |10 x 10 | |                                      |
     ||  |~BRONZE | +----+                                 |
     ||  |  KEY~  | | R7 |--[F]--+                         |   F  = (12,28)
     ||  |    \H1 | |8x8 |       |                         |   R7 TWIN SCONCES -> *SCOPE*
     ||  +--[B]---+ +----+-------+-------------------------+   B  = (0,32)
     ||      |      |    R5   THE PLATE GALLERY   16 x 16  |   z   8..24
     ||  +---+----+ |   [=== RATCHET PLATE ===]            |==C1==>  R6 LARDER 8x8
     ||  | R2      || |levers|        *METAL RUNE*         |         *THE BANANA*
     ||  |CHIPPING | +---------------[D]-------------------+   D  = (20,8), opens on R4 clear
     ||  |HALL 8x24| |    R4  THE GOBLIN REFECTORY 22 x 16 |   z  -8..8
     ||  |. trough | |    g  g  g   [oak tables]   g  g    |   oven wall  -> *FIRE RUNE*
     |+=>+--[A]----+ |    [RACK WR-1b]          [ OVEN ]   |   clear room -> GRIP CLOTH x P
     |      |        +--------------[Br]-------------------+   Br = BRONZE DOOR (8,0)
     |      |                        |
     |  +---+------------------------+------------+
     |  |   R1   THE CRACKED RELIQUARY   16 x 16  |          z  -8..8   A = (0,8) always open
     +->|  [spawn x4] [ANVIL RA-1] [RACK WR-1]    |
        |                            [straw] <----+--- H1 = one-way drop from R3 top (y=9)
        +-----------------------------------------+
```

### Gate graph

```
D-Bronze        <- Bronze Key          (R3 Rebound Shaft, top platform, y=7.2)
D-D             <- clear R4 combat     (R4 Refectory, 8 goblins)
D-E             <- Ratchet Plate latch (R5, 6.0 kg held 1.5 s  OR  3 levers @ >=12 N.s)
C-1 (secret)    <- 80 impact damage    (R5 east wall, any weapon)
R6 Banana       <- enter R6            (party-wide unlock at all Racks, permanent)
D-Verdigris     <- Verdigris Key       (R8 Scriptorium, central island y=6.5)
D-S1 shortcut   <- reach R9            (bar-lift, opens from inside, permanent, two-way)
R7 Sniper Scope <- twin plates 4.0 kg x2 within 0.4 s  OR  solo crumbling-tile route
Zone 2          <- Wind Rune           (Hobnail, R11)
Zone 3          <- Chain Rune          (Zone 2 boss)
Zone 4          <- Stone Rune          (Zone 3 boss)
```

**Critical path:** R1 → R2 → R3 (Bronze Key) → back through R2 → R1 → R4 (combat, Grip Cloth) → R5 (plate) → R8 (Verdigris Key) → R9 (anvil, opens S-1) → R10 → R11 (Hobnail → Wind Rune) → Zone 2.

**Optional loops:** R5 → C-1 → R6 (banana, egg, eyes). R4 oven → Fire Rune. R5 west wall → Metal Rune. R8 candle niche → Beeswax. R8 → R7 → Sniper Scope. R2 trough → Ribbon.

**Shortcut opening behind you:** S-1 (R9 → R2) collapses the return trip once the Verdigris Door is passed.

### Soft-lock proof (1 player and 4 split players)

1. **Keys are never consumed.** Using a key sets `doorOpen[id] = 1` on the host, permanently, for the session, and the key bit stays set. A key can never be "spent on the wrong side".
2. **Doors latch open forever.** Every Zone 1 door, once opened, is a permanently open mesh. Nothing closes behind anyone. The only exception is D-Bell, and it is *passable inward* via a trigger that teleports the arriver to the Bell Alcove.
3. **Every consumable resets.** Crumbling tiles: break 0.55 s after contact, **respawn 6.0 s after breaking**, host-timed. Breakable walls: stay broken. The Ratchet Plate: latches permanently. The impact levers: once all three fire, the latch is set permanently. Nothing on the critical path can be exhausted.
4. **One-way elements, all with a free return.** H-1 (R3 top → R1 straw): R3 is re-climbable from R2 at any time and nothing needed is above it. The R2 trough: a ramp at z=10. The R10 gantry fall: a 10 m drop into the Undercroft cellar with stairs back to R9's floor. D-Bell: inward trigger.
5. **Respawn is always on the unlocked side.** You respawn at the party's last-activated Anvil (RA-1 or RA-2), both of which sit in rooms whose entry doors are permanently open by the time the anvil can be activated. During the boss, respawn is the Bell Alcove *inside* the arena.
6. **Weapon swapping defeats every mass problem.** Racks in R1, R4 and R9 sit on the open side of every gate. A solo banana that meets the 6.0 kg Ratchet Plate has two outs: the three impact levers (banana at 15 m/s = 13.5 N·s > 12 — it qualifies by 1.5 newton-seconds, which is the funniest possible margin), or a 20-second walk back to Rack WR-1b in R4 for the mace.
7. **Four players split in four directions.** Nothing in Zone 1 requires a second player. R7's twin plates are optional *and* have a solo route. Combat gates check "zero enemies alive", not "all players present". If three players are in R8 and one is in R6, no state can regress.
8. **Late joiner.** Receives a snapshot: door bitmask (uint32), key bitmask (uint8), plate/lever/wall bits, enemy roster, party Soul Slot abilities, current anvil. Spawns at the party's anvil (or the Bell Alcove mid-boss) with a Rusty Arming Sword, **all party gate abilities**, 6 s invulnerability, and empty sockets. A late joiner can never lack a traversal ability the party has.

---

## 7. Puzzle and platforming kit

Every element below is authored as a `RoomDef`-referenced prefab with a ScriptableObject config. The networking column is the contract.

| # | Mechanic | Rule | Grey-box | Host owns | Client intent | Pure function of the room clock |
|---|---|---|---|---|---|---|
| 1 | **Mass plate** | Depresses while resting mass ≥ threshold kg (2.4×2.4 m, travel 0.12 m) | ProBuilder slab + 4 posts | `plateId:u8`, `massTenths:u16`, `latched:1 bit` | `PlateContact(plateId, massKg)` at 10 Hz while resting | The 0.12 m visual travel lerps from the state-change tick |
| 2 | **Ratchet plate** | As above but latches permanently after 1.5 s held | + a visible pawl | `latched:1 bit` (never cleared) | same | latch animation from `latchTick` |
| 3 | **Impact lever** | Fires if a hit delivers ≥ N·s; a group fires if all fire within a 4 s window | 0.8 m cylinder on a hinge | `leverBits:u8`, `windowStartTick:u16` | `LeverHit(leverId, impulseNs, tick)`; host rejects `impulse > weapon.mass × weapon.S × 1.25` | lever swing from `hitTick` |
| 4 | **Clock platform** | `pos = A + (B−A) · tri((clock + phase)/period)`, period 24 s | 3×3×0.4 kinematic cube | **Nothing. Zero bytes.** | none | **Entirely.** Every peer computes an identical pose |
| 5 | **Swinging censer** | `angle = amp · sin(2π·clock/3.2 + phase)`, amp 65°, arm 5 m | 1.0 m sphere + cylinder arm | Only the *damage*: host re-evaluates the pure pose at tick T to validate | `HitByHazard(censerId, tick)` | **Pose entirely.** Damage is host-validated against it |
| 6 | **Crumbling tile** | Breaks 0.55 s after first contact (wobble), respawns 6.0 s after breaking | 1.2×1.2×0.2 cube | `tileId:u16`, `breakTick:u16` | `TileTouched(tileId, tick)` | Wobble + fall + respawn are all functions of `breakTick` after the one event |
| 7 | **Keyed door** | Opens on E with the key bit; permanent | 2.4×3.0×0.6 slab, slides up | `doorOpenMask:u32`, `keyMask:u8` | `UseKey(doorId)` inside the 2 m trigger | 0.8 s slide from `openTick` |
| 8 | **Co-op twin plates** | Both ≥ 4.0 kg within a 0.4 s window | two mass plates | `plateA:1, plateB:1, firstPressTick:u16` | two `PlateContact` streams | reward niche opens over 1.0 s from `solveTick` |
| 9 | **Wind column** | Upward force 26 N/kg inside a 3×3×8 m volume, plus +40% air control | translucent box | **Nothing. Zero bytes.** | none | Static; purely local physics on each owner's body |
| 10 | **Breakable wall** | Accumulates damage; shatters at HP; stays broken | 6-piece ProBuilder cluster | `wallId:u8`, `hp:u16` | `HitWall(wallId, impulse, tick)` | shard scatter seeded by `wallId` — identical on all peers |
| 11 | **Sticky wood panel** | Blades embed at ≥9 m/s within 25°; hang 2.5 s; resets `wallCharge` | brown ProBuilder plank | **Nothing** | none (the anchor is your own body) | n/a — owner-authoritative |
| 12 | **Bell tone lock** | Strike 3 bells in the right order within 8 s | 3 spheres on chains | `sequence:u8, seqStartTick:u16` | `BellStruck(bellId, tick)` | bell swing is a damped cosine of `lastStruckTick` |

**The room clock.** The host stamps every packet with `tick` (60 Hz, uint32). Each client keeps a smoothed offset (median of the last 32 samples), corrects at most ±1 tick per 30 ticks, and evaluates clock-driven poses at `localTick`. Four peers agree to well under 2 cm on a 24 s platform loop. No moving platform ever sends a byte.

**Standing on a kinematic platform** is handled owner-side: the platform's per-frame delta is added to your Rigidbody position when you are grounded on it. Shared *dynamic* physics props do not exist anywhere in this game.

---

## 8. Enemies

Simultaneous active cap: **24**, host-enforced; spawners hold back and fill from a pool.

| Enemy | HP | Dmg | Speed | Fun as a flung weapon | Counter-play | Grey-box | Zone |
|---|---|---|---|---|---|---|---|
| **Scrap Goblin** | 45 | 8 | 3.2 | **Ducks 0.25 s after it hears your launch** — you have to lead it or launch from outside 10 m | Launch low and flat, or bait the duck and rebound back | 0.8 m capsule + 2 cube ears, green | 1 |
| **Lantern Goblin** | 60 | 10 (+3/s burn) | 2.6 | Explodes 2.0 m for 20 dmg when killed while burning — a physics chain reaction | Kill it away from you, or *into* a cluster | capsule + 0.25 sphere lamp, orange | 1 |
| **Fork Mob** (spawns as 3) | 18 ea | 5 | 4.5 | The only Zone 1 enemy that intercepts you **mid-flight**; they orbit at 2 m and dive | Land, let them dive, sweep them on the next launch | 3 thin 0.4 m boxes, grey | 1 |
| **Crockery Hulk** (mini) | 180 | 18 | 1.8 | Armoured (×0.25) except the glowing spigot on its back; the spigot only counts at ≥14 m/s | Get behind it, get speed, hit the spigot. Heavy weapons love it | 2.2 m ProBuilder barrel + 2 cylinder arms | 1 |
| **Mopwraith** | 70 | 6 | 2.2 (floats) | Applies **Damp**: −40% fire, +0.2 s launch wind-up. It nerfs *your movement* | Fire Rune burns it off; or kill it first | 1.6 m flattened sphere, grey-blue | 2 |
| **Ember Imp** | 35 | 12 | 5.0 | Fast, leaves 1.5 m fire pools that reshape the arena floor | Ranged (Bone Wand) or bait it onto a banana peel | 0.6 m sphere + cone, red | 2 |
| **Arcane Bookworm** | 120 | 14 | 3.0 | Burrows through shelves and re-emerges — the shelf you are standing on is not safe | Watch the dust puff (0.8 s telegraph), be airborne | 3.5 m segmented cylinder chain | 3 |

**State machines** (host only; identical shape for all types, driven by an `EnemyDef` ScriptableObject):

`Idle` → (player within `aggroRadius` and line of sight) → `Chase` (NavMeshAgent, destination = nearest player's last streamed pose) → (within `attackRadius`) → `Windup` (telegraph seconds, agent stopped, colour flash) → `Attack` (sphere overlap, host applies damage) → `Recover` (cooldown) → `Chase`. Two interrupts: `Stagger` (from knockback, ballistic, 0.15–1.2 s) and `Dead`. Scrap Goblins add a `Duck` state triggered by a host-side `LaunchNoise(pos)` event within 10 m. Crockery Hulks add `Turn` (they pivot at 40°/s, which is the whole fight).

**Enemy networking.** Host owns per enemy: `id:u16`, `type:u8`, `state:3 bits`, `targetPlayer:2 bits`, `pos:3×half`, `yaw:half`, `hp:u16` — 14 bytes. Poses broadcast at **10 Hz**, HP at **3 Hz**, state transitions as **events** with `tick` (so all peers play the same telegraph from the same tick). Client intent: `ClaimHit(enemyId, contactPoint, closingSpeed, businessAxisDot, tick)`. The host validates against its own 250 ms pose history (contact point within 1.5 m of the enemy's interpolated pose at that tick) and against `closingSpeed ≤ 1.30 × weapon.S`, then computes damage itself and broadcasts `EnemyDamaged(id, dmg, newHp, staggerImpulse, tick)`. Clients never decide damage; they only claim geometry.

---

## 9. Bosses

### Zone 1 — Hobnail, the Bell-Bellied Ogre

A 6 m ogre who swallowed the refectory's dinner bell decades ago and has been slightly unhappy ever since. Grey-box: 2.4 m sphere torso, two 1.0 m sphere shoulders, 2× 0.8×2.4 m cylinder legs, 2× 0.6×2.2 m cylinder arms, a 1.2 m **bronze disc** set into the belly at y=2.6. The disc is the fight.

**Arena (R11):** 26×26 m, ceiling 12 m, four 1.8×1.8 m corner ledges at y=3.5 at (11,80), (29,80), (11,98), (29,98). The Great Bell (r 2.0 m) hangs at (20, 89) at y=8. Bell Alcove respawn niche at (20, 77.5). Portcullis at (20,76).

**HP:** `900 + 420 × (players − 1)` → 900 / 1320 / 1740 / 2160.
**Weak point:** the belly disc. Hits at ≥ 12 m/s deal **×4.0** damage and stun for 2.0 s. Below 12 m/s the disc rings and does nothing, which is worse than nothing because it aggros him.

**Phase 1 (100–66%)**
- *Stomp* — 0.9 s telegraph (foot raises, 5 m red ring decal), 22 dmg, knockback 12 m/s radially. Every 6–9 s.
- *Belly Bounce* — every 8.0 s exactly (host, `nextBounceTick`), 0.7 s telegraph (he inflates), flings everything within 7 m at +9 m/s. **Not a threat — a launch pad.** Riding a Belly Bounce is the intended way to reach the corner ledges without the Wind Rune.
- Play pattern: bait the Stomp, ride the Bounce, come down on the disc at 14–18 m/s.

**Phase 2 (66–33%)** — he rings his own belly.
- Adds *Sweep* — 1.1 s telegraph (arm winds back, 180° arc decal), 9 m radius, 26 dmg. Dodge by being airborne or behind him.
- Adds *Clap* — 1.4 s telegraph, a 7 m cylinder at head height (y 1.0–4.0), 30 dmg. Dodge by being on the floor **or** above 4 m. The Clap radius grows +1.0 m per extra player; nothing else scales with party size.
- Summons **2 + P** Scrap Goblins on every Belly Bounce, capped at 6 alive.

**Phase 3 (33–0%)** — he pulls the Great Bell off its chain and wears it as a helmet.
- The belly disc is now **permanently exposed** (×4.0 at any speed above 8 m/s) but he can no longer see, so:
- *Charge* — 2.0 s telegraph (he paws the floor, a 3 m-wide line decal), 14 m/s, 34 dmg. He does not turn. **He hits the arena wall** and is stunned for 3.0 s with the disc facing up. The intended answer is to wall-rebound out of the charge lane — the fight's final exam for the game's only wall jump.
- The dropped chain hangs at (20,89) from y=8 to y=2 as a stick/tether target.

**Reward:** **Wind Rune** (party-wide Soul Slot), **Bell Charm** ×P, portcullis opens both ways, Belfry Stair to Zone 2 unlocks, and Hobnail's bell rolls to the floor and becomes R11's third Reforge Anvil.

**Networking:** host owns `phase:u8`, `hp:u16`, `attackId:u8`, `attackStartTick:u16`, `nextBounceTick:u16`, `pos/yaw`. Every telegraph and hitbox timing is a pure function of `attackStartTick`, so all four clients render the same wind-up from the same instant even under 150 ms event latency. Damage claims go through the same `ClaimHit` path as any enemy, with `businessAxisDot` and the ≥12 m/s disc check evaluated on the host.

### Zone 2 — Mucilage, the Laundry Wraith
A 6 m waterlogged sheet with a lantern where a face should be, in a flooded washroom where the water level is a clock-driven sine (period 30 s, range y 0–4). Heavy weapons fight it on the bottom at half speed; light weapons bob on top and can only strike on the downstroke. It wraps a player (a 6 s grapple) and the others must hit the wrap to free them — the game's first mandatory co-op mechanic, with a solo alternative: the Fire Rune burns the wrap in 3 s. Reward: **Chain Rune**.

### Zone 5 — The Arch Mage's Left Hand
A 7 m disembodied gauntlet, still mid-incantation, in a room with no floor — just 14 clock-driven orrery rings. It fights by *casting the spell it was casting when he died*, over and over, getting slightly further each attempt; the arena visibly changes each time. Finger joints are five separate weak points (180 HP each), and severing all five ends the game. It is also, plainly, trying to pick you up and put you back on the rack.

---

## 10. Co-op rules

- **Scaling.** Enemy HP × `(1 + 0.35 × (P−1))`; spawn counts +1 per extra player (cap 24 active); enemy *damage* never scales, so solo stays tense and four-player stays survivable. Boss HP per §9.
- **Shared vs personal.** Keys, door states, plate latches, broken walls, unlocked weapons and **Soul Slot gate abilities** are **party-shared and permanent**. Socketed modifiers (Fire Rune, Grip Cloth, Scope, etc.) are **personal**, and the host spawns one instance per living player at the source so nobody has to negotiate.
- **Loot.** No competition. There is no currency and no inventory tetris — modifiers are picked up by touch, and the pickup event is host-validated per player.
- **Downed / revive.** Per §3: Soul Wisp 45 s, Hilt carried to any Anvil, otherwise anvil respawn at 60% HP and one Tarnish stack.
- **Dead player's modifiers.** You keep everything. Your soul owns the runes; the steel was only ever a rental. The single exception is the Pickled Egg, which is consumed when it saves you, as eggs are.
- **Respawn points.** Last party-activated Anvil; Bell Alcove during a boss.
- **Late join.** Snapshot per §6.8. Always a Rusty Arming Sword plus every party gate ability.
- **Friendly collisions.** Player-vs-player collision is a **local approximation** — each client simulates the other bodies it sees, so contacts differ by a few centimetres between peers. Therefore: players never damage each other, players *can* bounce off each other (restitution 0.4, capped at 8 m/s so it reads as comedy not catapult), and **no puzzle ever requires precise weapon-on-weapon stacking**. Standing on a teammate is a fun accident, never a solution.
- **Communication.** `Q` = contextual ping (a 1.5 s world-space marker: "here" / "enemy" / "loot" / "I am extremely stuck") broadcast as a 6-byte host event. A permanent party tab (`Tab`) shows each player's weapon icon, HP, modifiers and current room name. Wisps can still ping — being dead should not silence you, it should make you louder.

---

## 11. One-shot grey-box scope

One developer, Unity Editor, primitives + ProBuilder, in order. Everything below R11 is Zone 1 only; Zones 2–5 are folder stubs with a `ZoneDef` asset and nothing else.

**Build order**

1. **ScriptableObject schemas first.** `WeaponDef` (mass, S, L, handling, HP, dmgMult, angle profile, stickChance, bounciness, prefab), `ModifierDef` (slot, stat deltas, hooks, isGate), `EnemyDef` (HP, dmg, speed, radii, telegraph times, prefab), `RoomDef` (bounds, doors, spawn table, clock phase offsets), `ZoneDef`.
2. **PeskyBody.cs** — the owner-authoritative Rigidbody controller: shuffle, launch, grounded test, air control, wall rebound with the 3.0 m/s incoming check, flight bias torque, stick. Tune it in an empty 40×40 m box until it is fun **before anything else is built.** This is the project's make-or-break day.
3. **Orbit camera** with the speed-based boom/FOV and spherecast collision.
4. **Net layer:** WebRTC data channels, room codes, host election, tick clock, `ClaimHit` / `UseKey` / `PlateContact` / `TileTouched` / `LeverHit` / `HitWall` intents, event ring buffer with a 150 ms apply delay, 15 Hz owner pose stream with 100 ms interpolation, snapshot on join. Test with 2 browser tabs on one machine.
5. **Four weapons only:** Rusty Arming Sword, Paring Dagger, Iron Mace, **The Banana**. (Rapier, staff, wand, orb, ball-and-chain, warhammer are authored as `WeaponDef` assets with prefabs but locked at the Rack.)
6. **Damage, HP, chipping, Anvil, Rack, Soul Wisp, Hilt, Tarnish.**
7. **Puzzle kit prefabs 1, 2, 3, 4, 6, 7, 8, 9, 10, 11** from §7. Kit 5 (censer) and 12 (bell lock) can slip.
8. **Zone 1 geometry**, room by room in the order R1, R2, R3, R4, R5, R8, R9, R10, R11, then the optional R6 and R7. Exact footprints and door coordinates are in §6 — no further design decisions are needed to lay it out on a 2 m grid.
9. **NavMesh bake** on R4, R5, R8, R11 floors (agent radius 0.4, height 1.2, step 0.4, slope 30°).
10. **Three enemies:** Scrap Goblin, Lantern Goblin, Crockery Hulk. (Fork Mob is a stretch goal — flying enemies need an off-NavMesh path.)
11. **Five modifiers:** Wind Rune, Fire Rune, Grip Cloth, Metal Rune, Sniper Scope. (The rest are `ModifierDef` assets with no pickup placed.)
12. **Hobnail**, all three phases, with placeholder cube telegraph decals.
13. **Lobby:** room code create/join, weapon select from the Rack, ready check, host-leaves → "the tower ate the host" screen.
14. **HUD (uGUI):** HP bar, 3 socket icons, Soul Slot icon, key icons, party list, ping markers, room name toast.

**Stubbed:** Zones 2–5, the other six weapons, eleven modifiers, four enemy types, bosses 2 and 3, audio beyond four placeholder SFX, any art.

**Acceptance test.** *Two players in two browsers join by room code. One picks the Iron Mace and one picks the Banana. They cross the Chipping Hall, climb the Rebound Shaft using at least one wall rebound, take the Bronze Key, open the Bronze Door, kill eight goblins in the Refectory and each take a Grip Cloth, smash the oven wall for the Fire Rune, solve the Plate Gallery (the mace holds the plate; then they do it again with the banana on the levers to prove both routes), cross the Scriptorium's clock platforms with sub-10 cm divergence between the two clients, take the Verdigris Key, reforge at RA-2, open shortcut S-1 and walk back to R1 in under 15 seconds, cross the Crumbling Gantry, and kill Hobnail. One player dies at least once and is revived by Hilt-to-Anvil. A third player joins mid-boss, spawns in the Bell Alcove with the Wind Rune already in their Soul Slot, and contributes. No desync, no soft-lock, total run under 25 minutes.*

---

## 12. Top risks and how to de-risk each

1. **The core feel might just not be fun.** A tumbling sword you launch is either delightful or nauseating, and no amount of level design saves the wrong answer. *De-risk:* build step 2 first and spend a full day in an empty box. Ship a debug panel that exposes `S`, `L`, `H`, gravity, retain factor and flight-bias gain as live sliders so tuning is minutes, not rebuilds. If it is not fun with one primitive in one box, stop and re-design before any geometry exists.
2. **Launch-only movement makes precision mandatory and frustrating.** *De-risk:* the 0.22 s launch cooldown, the 0.12 s coyote window, generous 1.6 m ledges, sticky wood panels as free re-anchors, and the rule that falling always costs seconds and a few HP but never progress. Every Zone 1 fall has a ramp or stairs.
3. **Clock drift desyncs the "pure function" platforms.** *De-risk:* clamp correction to ±1 tick per 30, seed every clock element from `tick` rather than `Time.time`, and add an F3 overlay showing each peer's offset. Test at 200 ms simulated latency.
4. **WebGL performance with 24 enemies + 4 physics bodies + URP.** *De-risk:* enemies are kinematic NavMesh agents with no Rigidbody except during Stagger; one shared flat-colour material with `MaterialPropertyBlock` tints; fixed timestep 0.02; occlusion by room via `RoomDef` bounds (only the active room and its neighbours simulate). Budget: 60 fps on an M1 Air in Chrome.
5. **Host migration does not exist, so a host disconnect ends the run.** *De-risk:* accepted per the constraints, but mitigated — the host broadcasts a compact "party progress" blob (doors, keys, abilities, anvil) every 10 s, clients persist it to `localStorage`, and a new room can resume Zone 1 at the last anvil. Explicitly a resume, not a migration.
6. **The banana trivialises the level.** *De-risk:* the audit in §6 — the banana's absolute ceiling is 2.35 m of rise and ~14 m of gap, every gate is a door or a 3.5 m+ wall, and no angled geometry sits within 6 m of a gate gap. Add an editor validator script that walks every `RoomDef`'s gate list and fails the build if any gate rise is < 3.5 m or any mandatory gap is > 3.2 m.
7. **Mass-gated puzzles lock out solo light-weapon players.** *De-risk:* every mass puzzle in the game has a second, speed-based solution (levers) and a Rack within one open door. Codified as a rule in `RoomDef`: a room flagged `RequiresMass` must declare an `AltSolutionId` or the validator fails.
8. **Scope creep in the first slice.** *De-risk:* the acceptance test in §11 is the definition of done. Four weapons, three enemies, five modifiers, one boss, eleven rooms. Everything else is a ScriptableObject asset with no prefab, which costs minutes and keeps the data model honest.
