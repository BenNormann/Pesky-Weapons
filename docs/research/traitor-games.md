# Traitor / social-sabotage research for Pesky Weapons

Research notes, 2026-09-20. Context: 3-8 possessed weapons escaping a 5x5 teleport-door labyrinth; 1-2 hidden Arch Mage fragments; curse draft; personal legend points.

---

## 1. "Haunted Heist" — identification

**Almost certainly: *Haunted Heist* by Autotroph Games (Seattle; Jakob Herlitz + Dominic Giardini), PC/Steam, demo live now, full release 12 Oct 2026. Confidence ~90%.**
It is the only "Haunted Heist" that is a 3D multiplayer sabotage game with an ability kit; everything else sharing the name is a tabletop RPG (Red Mug Games), a GMTK 2025 jam entry, a 2026 comedy film, and a nearby-but-different Steam co-op game *Haunt or Heist*. The 3-8 player count matching ours exactly is a further tell.

- **Loop:** round-based, asymmetric PvP in a **procedurally generated mansion**. Heisters must find gems scattered through the house and carry them back to base before a timer (~10 min) expires. Tricksters haunt the same house and must disrupt or kill them. Teams presumably swap between rounds.
- **Roles:** Heisters get flashlight (battery-limited), a gun, flares, defibrillator (downed teammates can be revived — important: death is not instant removal). Tricksters get night vision plus a cooldown-gated ability kit.
- **Trickster kit:** invisibility, disguises (1:1 impersonation built from *recorded clips of a player's own voice*), voice swapping, muting a player, ambush attacks.
- **Detection/evidence:** proximity voice chat is the primary information channel and the primary attack surface — voice-swap and voice-clone disguises corrupt it. Light/darkness is the other axis: batteries are a resource, so information literally runs out.
- **Praise/complaints:** demo sits ~80% positive over ~110 reviews ("Very Positive"). Coverage praises the voice-based abilities as the standout gag. Not enough review volume yet to extract reliable complaints; treat as unknown.
- **Difference from us:** teams are *known*, not hidden — it is asymmetric PvP, not hidden-traitor. Our Mage is a social-deduction role; theirs is a team you can see.

Sources: https://store.steampowered.com/app/4024440/Haunted_Heist/ · https://store.steampowered.com/app/4320050/Haunted_Heist_Demo/ · https://www.autotrophgames.com/ · https://www.gamespress.com/GAME-ANNOUNCEMENT-HAUNT-YOUR-FRIENDS-IN-VIRAL-HORROR-COMEDY-CO-OP-TITL · https://store.steampowered.com/app/2539870/Haunt_or_Heist/

---

## 2. Among Us — the parts that matter to us

- **Tasks vs sabotage:** crew progress is a shared visible bar; impostors have no tasks but can *fake* them. Sabotage is the impostor's legitimate excuse to be everywhere: O2/reactor force the crew to split and sprint (creating alibis and isolation), lights shrink everyone's vision, comms blinds the information tools, doors trap people. **Sabotage is a movement-control tool, not a damage tool** — that is the key lesson.
- **Kill cooldown** (settings-tunable) paces the round and is the main balance dial.
- **Vents** give impostors a traversal advantage crew cannot match, and being *seen* venting is near-hard proof.
- **Meetings:** emergency button (limited per player) or body report freezes play into discussion + vote; skip is allowed; ejection may or may not confirm the role, which is itself a balance setting.
- **Evidence production:** *visual tasks* (medbay scan, trash, shields, asteroids) are the only hard alibi and can be toggled off; **Admin** shows player counts per room, **Vitals** shows who is dead and when, **Cameras** and **door logs** give timestamped movement. All are flawed on purpose — counts without names, deaths without killers.
- **Roles:** Crew — Scientist (portable vitals), Engineer (limited vent use, which muddies "only impostors vent"), Guardian Angel (dead player who can shield the living), Noisemaker (pings its own corpse location on death), Tracker (tails one player), Detective (investigates for evidence, 2025). Impostor — Shapeshifter (become another player), Phantom (temporary invisibility), Viper (acid kills + dissolves bodies, 2025).
- **Lobby settings** carry the balance across counts: impostor count, kill cooldown, task count/type, vision radii, meeting count and length, confirm-ejects, role probabilities and per-role charges.
- **Why it works on voice:** the mechanics are thin enough that *the argument* is the game; the map exists to generate timelines you can lie about.
- **Known weaknesses:** dead players have almost nothing to do (ghost tasks are widely disliked and players leave or go AFK), external voice chat lets the dead leak information (metagaming — the single biggest problem for a friends-on-Discord game), small lobbies collapse into coin-flips, and griefing/random voting is unpunished.

Sources: https://among-us.fandom.com/wiki/Roles · https://en.wikipedia.org/wiki/Among_Us · https://gamerant.com/among-us-new-roles-explained-noisemaker-tracker-phantom/ · https://steamcommunity.com/app/945360/discussions/0/3202620277337294995/ · https://screenrant.com/best-things-when-dead-among-us/

---

## 3. Genre sweep — loop / fun tool / deniable tool / info leak / lesson

- **Project Winter** (5-8, survival): repair objectives and escape a blizzard. *Fun:* traitor crates, hatchet ambush. *Deniable:* hoarding supplies, "fixing" the wrong thing, leading people to bears. *Info:* proximity + radio channels means whispering is itself evidence. *Lesson:* with **two traitors each is half-strength**, and both going AWOL is a tell — the community complains survivors can simply rush objectives.
- **Dread Hunger** (8): sail the Northwest Passage; thralls stop you. *Fun:* blizzard spell, summoning cannibals, invisible sprint. *Deniable:* poisoned food, gunpowder in the boiler, ramming the ship, being slow to rescue. *Info:* bone daggers and totems are physical incriminating objects. *Lesson:* **let sabotage hide inside normal accidents** — the best tools are indistinguishable from incompetence.
- **Deceit / Deceit 2** (up to 9): tasks interrupted by scheduled blackout/"inbetween" hunts where Terrors transform. *Lesson:* forced chaos phases reset stale arguments, but Deceit 2 reviewed badly (Metacritic 48) — too many systems, unclear reads.
- **Goose Goose Duck** (16): Among Us plus ~70 roles and proximity chat, including neutrals (Dodo wins by being voted out). *Lesson:* role breadth drives retention but overwhelms newcomers; **neutral/selfish win conditions give everyone a reason to lie** — close to our legend points.
- **Lockdown Protocol** (3-8, first-person): tasks under a timer. Dissidents get **no special powers at all** — they must hide items, stall, misinform. *Lesson:* praised as more interesting than a kill button, but many players say sabotage is near-impossible because the map never lets you be alone. **Powerless traitors need privacy to exist.**
- **First Class Trouble** (6): personoids among humans on a liner; killing is *physical* (shoving, strangling, hazards) not a button. *Lesson:* physical murder is gloriously deniable; the community's problem is unpunished RDM — friendly-fire chaos erodes trust in the rules.
- **Unfortunate Spacemen** (up to 16): shapeshifter monster mimics crew and corpses; a later human **Traitor** role hacks tasks/doors/cameras. *Lesson:* a second traitor who is *not* the monster creates real "who helped?" arguments.
- **Secret Neighbor** (6+1): kids gather keys, one is the Neighbor disguised as a kid, abducting people one at a time. *Lesson:* cartoonish, short, no meetings — deduction happens while running.
- **Betrayal at House on the Hill:** explore a tile-built house, then a random "haunt" flips one player traitor mid-game. *Lesson:* the reveal moment is the product; the pre-haunt half is famously aimless and the scenarios are famously unbalanced. **A timed/triggered reveal is exciting; don't let the first half be filler.**
- **The aMAZEing Labyrinth:** every turn you shove a row of tiles, changing everyone's routes to serve your own. *Lesson:* rearranging a maze is fun **because it is public and everyone does it** — the tension is anticipating intent, not hiding the act. Our hidden rearrangement inverts that, so the Mage needs a *visible* cover story for why paths changed.
- **2024-26 notes:** *Among Us 3D* (Schell Games/Innersloth, May 2025, flat + VR, ~74% positive) is the closest thing to "Among Us but 3D" already existing. Steam ran a Social Deduction Fest in mid-2026; new entries skew toward proximity chat and Jackbox-style party framing (*Forest of Deceit*, *DUBIUM*). The genre is crowded but **no one has physics-launch movement**.

Sources: https://en.wikipedia.org/wiki/Project_Winter · https://steamcommunity.com/app/774861/discussions/0/2268068817145972439/ · https://en.wikipedia.org/wiki/Dread_Hunger · https://en.wikipedia.org/wiki/Deceit_2 · https://store.steampowered.com/app/2780980/LOCKDOWN_Protocol/ · https://steamcommunity.com/app/2780980/discussions/0/599654119441961615/ · https://www.metacritic.com/game/first-class-trouble/user-reviews/ · https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/UnfortunateSpacemen · https://goose-goose-duck.fandom.com/wiki/Neutrals · https://en.wikipedia.org/wiki/Betrayal_at_House_on_the_Hill · https://en.wikipedia.org/wiki/Labyrinth_(board_game) · https://en.wikipedia.org/wiki/Among_Us_3D · https://nanogamingnews.com/2026/07/14/steam-social-deduction-fest-2026/

---

## 4. Synthesis for Pesky Weapons

### a. Ranked sabotage/ability ideas for the Mage

1. **Mid-air nudge (telekinesis):** a small, budgeted impulse on a friend already in flight. Reads as your own bad aim. The single best fit for launch-movement — *this is our vent*.
2. **Late-landing drag:** instead of pushing, add drag in the last 0.3 s so a jump lands just short. Even more deniable than a push.
3. **Door re-link (Architect):** swap two teleport destinations. Must remain slow, unobserved and budgeted, and the glyph must update — a lying glyph is unfair; a glyph that quietly *changed* is an argument.
4. **Room rotation:** rotate a room 90°, so the exit is where nobody expects. Cheap, disorienting, blameable on "you got turned around".
5. **Goblin nudge-patrol:** send a patrol through a corridor at a timed moment. The goblins do the sabotage; you were nowhere.
6. **Compass deflection (passive):** the exit compass drifts near a fragment. Detectable only by comparing readings — a social instrument, not a detector.
7. **Curse mis-assignment:** quietly reassign a "good" curse to someone else, or add a hidden fourth pick. Greedy players do this openly, so the Mage hides inside normal greed.
8. **Sticky/greasy surface:** a temporary local physics patch (low friction, high restitution) on one wall the group is about to bank off.
9. **Rack tampering:** swap which body re-forms in which slot, or delay one re-form by a few seconds.
10. **Fake residue:** plant tamper shimmer near an innocent player's last position — frames someone, costs budget.
11. **Anvil/charge skim:** silently consume a shared charge. Pure resource theft, perfectly deniable.
12. **Bat amplification:** make a friendly bat far too strong once, so the *batter* looks like the saboteur.
13. **Soul drag:** slow a flying soul returning to its body by 20% — felt, never seen.
14. **Loop door:** a door that returns you to the room you left, once. Very strong, very memorable, should be the long-cooldown showpiece.
15. **Muted glyph:** one door's glyph goes unreadable for 30 s. Weak alone, great as cover for an actual re-link.

Favour 1, 2, 5, 7 and 12 for first prototype: no new systems, all physical, all funny.

### b. Evidence that argues rather than proves

- Souls see **tamper shimmer** that decays — so the accusation is always "I saw it *and it's gone now*". Someone must leave their body to look, which costs the team.
- The **hub map wall** shows *that* the graph changed, not *who*. Perfect argument fuel.
- **Goblins pulling punches** is a statistical tell across a whole floor, not a single moment.
- **Curse arithmetic:** a public ledger of picks that sometimes doesn't add up.
- **Compass disagreement** between two players standing apart.
- Keep every channel *lossy by design*: counts without names, timestamps without identities, effects without authors. Among Us's Admin map is the model.

### c. Small lobbies, two traitors, traitors who don't know each other

- Below 5 players, hidden roles are coin-flips. Project Winter's lesson: with two traitors, **each one at half strength plus two obvious AWOLs equals a fast loss**. Our co-sign mechanic is the right answer *provided* co-signing is the only path to the big effects; otherwise split the domains so each fragment is individually complete but narrow.
- Blind fragments accidentally banishing each other is excellent comedy and should be preserved, but give them a cheap, low-risk handshake (the out-of-body flicker) so long runs don't end in farce.
- At 3-4 players, consider the PREMISE.md variant: the fragment lives in a **body on the rack**, so the role can move between players and nobody can be pre-cleared.

### d. Meetings without stopping the game

Do not adopt "everyone teleports to a table". Options that suit physics play:
- **Ward circle at the hub, between floors only** — natural downtime already in the loop; discussion happens while drafting curses.
- **Rolling accusation:** a fragment-test at the ward circle that costs a resource, so accusations are expensive rather than free.
- **Proximity-chat argument during play**, with no pause at all (Secret Neighbor / Lockdown Protocol model) — the argument happens while somebody is mid-flight.
- **Asynchronous anonymous vote** that resolves at the next floor transition, so the game never freezes.

### e. Keeping "dead" players alive

We already have the best answer in the design: death is a soul, not elimination — fly home, re-possess, lose legend. Keep it. The genre's biggest sin (Among Us ghost tasks, dead-player AFK) is simply not ours to commit, and *Haunted Heist*'s defibrillator revives point the same way.

### f. Pitfalls most relevant to us

- **Voice-chat metagaming:** friends on Discord will talk out of character. Design so that the Mage's actions are *unprovable* rather than hidden — an information leak matters less when nobody has proof anyway.
- **Griefing under cover of "petty sabotage":** First Class Trouble's RDM problem. Legend points must actively punish pointless destruction, or "everyone sabotages" becomes "nobody cooperates".
- **Traitor being unfun when outnumbered:** Lockdown Protocol's complaint — a saboteur with no privacy cannot act. Our labyrinth must genuinely encourage splitting up, or the Mage never gets a window.
- **Runs that drag:** 25 min is right; the resurrection meter is the pacing valve — make sure it can't stall.
- **Too many systems at once:** Deceit 2's failure. Architect/Master/Hexer is already three subsystems; ship one.

### g. Overlap check — honest

**Too close to something existing:** the hidden-role-with-abilities-in-a-3D-map shell is thoroughly occupied (Among Us 3D, Lockdown Protocol, Haunted Heist, Project Winter). Procedural map + 3-8 players + sabotage kit is, almost beat for beat, Haunted Heist's pitch. "Find the real exit, not the fake one" is close to Deceit's escape phase. A secret traitor who rearranges the map is not new in board games (Betrayal, Labyrinth).

**Genuinely unoccupied:** *nobody has launch-only movement in a social-deduction game.* That single mechanic changes every tool in the genre — a shove becomes a missed jump, a boost becomes a murder weapon, and "I aimed badly" is the most natural alibi ever invented. Pair it with souls (no elimination, plus a second way to see) and a traitor who edits topology, and the combination is ours. The advice: **lead with the physics, not with the traitor.** If two players batting each other around isn't funny, no hidden role will save it — which is exactly what the existing build order already says.
