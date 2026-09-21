# Pesky Weapons

A friend-slop co-op labyrinth escape for 3-8 players. Everyone is a possessed weapon that
moves only by launching itself, and at least one of you is secretly a fragment of the dead
Arch Mage, steering the group toward his resurrection instead of the exit. Grey-box stage.

Play the latest build: https://bennormann.github.io/Pesky-Weapons-Build/

## Getting set up

1. Clone this repository. (You only need `Pesky-Weapons-Build` if you publish builds.)
2. Install **Unity 6000.3.23f1** through Unity Hub. Add **WebGL Build Support** for web builds
   and Windows Build Support for the two-player test build.
3. Have **git on your PATH**: one package is fetched from a git URL. No git-lfs needed.
4. In Unity Hub: Add > Add project from disk > pick the **`Pesky Weapons Unity`** folder, not
   the repository root.
5. The first open takes a few minutes while Unity rebuilds `Library/` and restores packages.
6. Open `Assets/Scenes/Boot.unity` and press Play.

`NoSubscription` errors in the console come from Unity's AI Assistant package and are harmless.

## Working together

Unity scenes and prefabs merge badly. One person per scene at a time, build rooms as separate
prefabs, work on a branch and merge through pull requests.

## Where to read

| Doc | What it covers |
|---|---|
| `docs/PREMISE.md` | The game |
| `docs/LABYRINTH.md`, `docs/KIT.md` | Building rooms and puzzle pieces |
| `docs/NETCODE-STATUS.md` | Multiplayer |
| `docs/TEST-CHECKLIST.md` | How to test, solo and two players |
| `docs/WEB-BUILD.md` | Web builds and publishing |
