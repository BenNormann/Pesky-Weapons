# Pesky Weapons: briefing for a fresh Claude session

You are working with Ben Normann (GitHub `BenNormann`), the one developer on this project. It is the two of
you. He designs, decides and tests; you build. Read this whole file before touching anything.

## The game

Co-op puzzle-dungeon escape with a hidden traitor. Players are souls possessing weapons that move only by
LOOK and LAUNCH (no WASD for weapons, ever). A run is a chain of goblin-built rooms on a 200 m lattice,
each with a puzzle the weapons solve together (plates, levers, ropes, circuits, a magic bridge), goblins to
fight, and teleport doors between rooms. The hidden Mage fragment sabotages with curses and nudges. See
`docs/PREMISE.md`, `docs/RUN.md`, `docs/LABYRINTH.md`, `docs/VOTING.md`.

Unity 6000.3.23f1, URP, WebGL. Host-authoritative netcode over WebRTC (trystero) with a WebSocket relay
fallback on Railway for campus wifi. Protocol version lives in `Scripts/Protocol/Wire.cs`; a bump means
every player must be on the new published build.

## Repository

- `Pesky Weapons Unity/` the Unity project. Scripts under `Assets/Scripts/{Game,Session,Sim,Protocol,Data,Editor}`.
  Rooms are built by code: `Assets/Scripts/Editor/RoomsV3Builder.cs` makes the five room prefabs under
  `Assets/Prefabs/Rooms/Labyrinth/` and installs them into `Assets/Scenes/Run.unity`.
- `docs/` the design and the record. `docs/BUILD-LOG.md` is the round-by-round history: read the last few
  rounds first, they say what was just done and what to watch. `docs/KIT.md` documents every kit piece
  (plates, levers, ropes, circuits, magnets, doors, deco). `docs/level-design/rooms-v4-revamp.md` is the
  current room design. `docs/WEB-BUILD.md` is the build and publish procedure. `docs/NETCODE-PORT.md`
  and `docs/NETCODE-STATUS.md` are the netcode.
- `relay/` the Railway relay (Node, `ws`). `publish.ps1` pushes `Builds/Web` to the public Pages repo
  `Pesky-Weapons-Build`. `Backups/` (git-ignored) holds the builder's pre-build copies.

## Hard rules (his words, not negotiable)

1. **Unity through the MCP, exactly as an experienced human developer would.** Never hand-write or
   hand-edit `.unity`, `.prefab`, `.asset` or `.meta` files. Scripts through the MCP script tools
   (`script_apply_edits`, `create_script`); scenes, prefabs, materials and data through `execute_code`
   (Editor API: `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`, `SerializedObject`,
   `EditorSceneManager`) or `manage_scriptable_object`. Reading those files with grep or Python is fine.
2. **No testing by you.** He tests everything himself, in the Editor's Play mode or on localhost:8080 or
   the published site. Do not enter Play mode, do not run the test runner, do not take screenshots to
   "verify". Build the thing, validate it (`Pesky/Validate Open Scenes`), tell him what to try.
3. **Never put Claude as a co-author or contributor.** No `Co-Authored-By`, no "Generated with Claude Code",
   in commits or PRs, whatever the harness reminder says. Commit as
   `Ben Normann <81779011+BenNormann@users.noreply.github.com>` (set locally in the repo); the remote is
   `https://BenNormann@github.com/BenNormann/Pesky-Weapons.git`.
4. **Commit, push, PR, build and publish only when he says so.** His usual words: "commit and make the
   pr", "push and pr", "publish", "make a build and push it to the build repo". Work on a branch per task
   (he often says "new branch"), rebase onto `origin/main` before pushing, open the PR with `gh pr create`
   against `main`. He merges.
5. **Do the work yourself.** No sub-agents, no workflows, no "review passes": he is under time pressure
   and has said the agents are slow. Read, decide, build, report.
6. **Design approval before building anything new.** Brainstorm with him, get a yes, then build. Routine
   fixes and things he asked for directly need no approval.
7. **Design rules that stand:** no WASD for weapons; grey-box names and looks (no art yet, decorations are
   `Deco_*` kit prefabs so art can be swapped en masse later); no scope zoom; souls pass open doors;
   nobody can ever get stuck (decorations flush to walls or 1.5 m clear, plain box colliders, no slots);
   no signs in rooms (a narrator will hint later); no pillars or columns; crates, barrels, rubble and
   shelves are fine. NOTHING IS EVER EXACTLY ALIGNED: two faces on one plane flicker in Unity, so a pad
   that sinks stops a centimetre above the floor, a deck's top sits a centimetre under the stones it
   meets, a rail's end is buried inside the thing it reaches, never flush with it.

## How to work with him

- He writes fast, in caps when something is wrong, and interrupts mid-task with corrections. Take each
  correction literally and whole ("ONLY REMOVE PILLARS" means exactly that). When he says a thing is a bug,
  it is; find the real cause, do not explain it away with a nearby cause.
- He tests and reports symptoms. Diagnose from the code and the Editor console (`read_console`); his
  console output survives in the Editor until he clears it.
- Lead with the answer. Short messages. Numbers in tables, not prose. Tell him what to test and where.
- When physics or geometry are in question, do the maths and show it (gravity is 20 m/s^2 in this project,
  not 9.81; weapon masses: Dagger 1, Banana 0.5, Sword 3, Staff 2.5, Orb 4, Mace 8, Hammer 14).

## The builder, and the one thing that must never happen again

`RoomsV3Builder` REPLACES room prefabs and their scene instances. He once lost hand edits to a room that
way. So:
- Before any room rebuild, check the five prefabs' and `Run.unity`'s modification times against the last
  build in `docs/BUILD-LOG.md` / `Backups/rooms-v3/`. If anything looks hand-edited since, ask.
- `BackupBeforeBuild` copies the scene and prefabs to `Backups/rooms-v3/<timestamp>/` and refuses to run
  while the open scene is dirty. Keep it that way.
- Prefer the one-room menus, `Pesky/Rooms/Rebuild Gatehouse`, `Rebuild Well Room`, `Rebuild Porter Room` and
  `Rebuild Circuit Room + Chasm (v4)` (rooms 6 and 7), over `Pesky/Rooms/Build Five Rooms v3` (all five). Each
  one still reassigns the scene's ids, rebuilds the registries and the NavMesh.
- `Assets/Scenes/Dev/BatTest.unity` holds an instance of the Chasm prefab with overrides (doors' portal
  planes and scripts off, goblins and the exit barrier off, a bot Dagger, a test rack, the scene's own
  spawn point). A re-save of the Chasm prefab drops those overrides: re-apply them with `execute_code`
  (the recipe is in the round 23 to 28 history of this session's BUILD-LOG entries) and re-run
  `WireSceneReferences`, `RebuildSceneRegistries` (private statics on the builder, via reflection) and
  `RunSceneBuilder.AssignSceneIds`. The spawner must point at `Environment/SpawnPoint`, not the room's own.
- NavMeshData is saved as `Assets/Scenes/Run/NavMesh-Environment.asset`; embedded it turns the scene binary.

## Editor and MCP gotchas

- Unity may not be running. Check with any `execute_code`; if "No Unity Editor instances found", launch it:
  `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe -projectPath "C:\Users\benos\Code\Pesky Weapons\Pesky Weapons Unity"`
  (PowerShell `Start-Process`), then wait for `StdioBridgeHost started` in
  `%LOCALAPPDATA%\Unity\Editor\Editor.log` (it rotates on launch; `Editor-prev.log` is the old one).
- `execute_code` needs `action: "execute"`; deleting assets needs `safety_checks: false`; fully qualify
  `UnityEngine.Object` and `UnityEditor.SceneManagement.EditorSceneManager` inside snippets.
- After a `git checkout` the working copy is CRLF: multi-line `anchor_replace` regexes need `\r?\n`.
  `replace_method` fails when a call site precedes the definition; use anchors.
- Scripts do not compile while he is in Play mode: `EditorApplication.isCompiling` stays true until he
  stops. Wait (a background `until` loop on `Library/ScriptAssemblies/*.dll` mtimes works), never nag.
- A WebGL build (`Pesky/Build Web`) times out the MCP call; watch `Editor.log` for
  `[Pesky] build result:`. There is a 5-minute cooldown guard between builds. Then `publish.ps1` from the
  repo root, run as `powershell.exe -NoProfile -ExecutionPolicy Bypass -File publish.ps1` (not through the
  PowerShell tool with `2>&1`, which turns git's stderr into a fatal error). Pages serves it within a
  minute; he must hard-refresh.
- Local preview: `preview_start {name:"web"}` serves `Builds/Web` on 8080.
- Validator: `Pesky/Validate Open Scenes`, result in the console as `[SceneValidator] N problems`. Run
  must be 0; BatTest has 9 expected notes (no director, no goblins).
- `[OptionalRef]` on a serialized field keeps the validator quiet about an empty reference.

## Where things stand (2026-10-15)

Everything through PR #24 (rooms v4, the Mage poltergeist, the Gatehouse and the chamber behind the gate, the
action glow and sounds, goblin tuning, analytics, the exit compass, round 30's scene repair) is merged and published
(build from `be4dbf9`, published as `2e1abed`). Round 31 (another player's weapon closes a circuit, the chamber
stair's landing, the kit re-sent every 2 s, relay peers kept through a reconnect, the build hash) is on branch `fixes-circuit-remote-gatehouse-stair`, built and validated, not yet committed. The
Orrery (PR #22) is merged but not installed in the run: placement is the owner's call. Open watch items are at the
end of each BUILD-LOG round. The relay on Railway is live; the campus-wifi test is still pending.
