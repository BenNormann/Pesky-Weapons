---
description: Validate the open Unity scenes (ids, references, layers, kit wiring, coplanar faces / z-fighting), then rebase onto origin/main, push the branch and open the PR. Refuses to ship on any validator problem.
---

Ship the current branch, but only if the Unity scene validator is clean. Do every step yourself, in order,
through the MCP for Unity instance that belongs to THIS session's checkout (never another chat's Editor:
pick the instance whose `project_path` in `~/.unity-mcp/unity-mcp-status-<hash>.json` is this checkout and
`set_active_instance` to it). Stop and report at the first failure.

1. **Compile check.** `refresh_unity` (force, compile) and wait until the status file says `ready`. Read the
   console filtered on `error CS`; any compile error stops the ship.
2. **Scene check.** Make sure `Assets/Scenes/Run.unity` is the open scene (open it if not; the scene must
   not be dirty). Run the menu item `Pesky/Validate Open Scenes` and read the console entry starting with
   `[SceneValidator]`. It must say `0 problems.` Every problem it lists is a real defect: unassigned ids,
   empty references, wrong layers, non-static geometry, kit pieces wired wrong, doorway openings blocked,
   and **coplanar faces** (two cube faces in the same plane, same normal, overlapping: they z-fight and
   flicker; doors are also checked in their OPEN pose). Fix the cause in the builder or the prefab
   (inset the piece, give crossing pieces different thicknesses, bury the overlap), rebuild, re-run. Do not
   ship with problems and do not weaken the validator to pass.
3. **Clean tree.** Drop Unity's whitespace-only rewrites (`git checkout --` on any file whose diff is empty
   under `--ignore-all-space --ignore-blank-lines`, typically `ProjectSettings/*.asset` and the NavMesh
   `.meta`). Commit everything that remains with a message in the project's voice (what changed and why, the
   round number, the docs/BUILD-LOG.md entry), as Ben Normann, with no Claude attribution of any kind.
4. **Rebase.** `git fetch origin` and `git rebase origin/main`. Resolve conflicts honestly: generated room
   prefabs and `Run.unity` take main's side and are rebuilt by the room builder, then re-validated (back to
   step 2); hand-edited kit prefabs are re-applied through the Editor; docs keep both sides.
5. **Ship.** `git push -u origin <branch>` and `gh pr create --base main` with a title and body that say what
   the round built and what the owner should try first. No Claude attribution in the PR either.
6. Report: the validator line, the commit hash, the PR link, and anything the owner must test by hand.
