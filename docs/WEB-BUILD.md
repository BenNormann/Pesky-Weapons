# WEB-BUILD

How Pesky Weapons becomes a web page other people can play, and how the desktop
test build is made. Everything here is driven from
`Pesky Weapons Unity/Assets/Scripts/Editor/BuildTools.cs` (ported from ATCK's
`Editor/BuildTools.cs`) plus `publish.ps1` at the repository root.

---

## 1. Build it

### From the Editor

| Menu item | What it does |
|---|---|
| **Pesky > Apply Player Settings** | Writes every Web setting listed in section 3. Safe to run any time; it changes nothing else. |
| **Pesky > Build Web** | Applies those settings and builds **every enabled scene in the build settings** into `Builds/Web` at the repository root. |
| **Pesky > Build Windows (two-player test)** | A development Windows x64 player at `Builds/Windows/PeskyWeapons.exe` (section 6). |

The build settings list is the single source of truth for what ships: today
`Boot`, `MainMenu`, `Tutorial`, `Labyrinth`, in that order. Add a scene in
**File > Build Profiles** and the next build includes it.

Two things to know about the menu items:

- **They will not build on the wrong platform.** If the active build target is
  not the one the item needs, it switches the target, logs
  `active build target is now <X>; run the menu item again to build`, and stops.
  Switching reimports every asset and reloads the C# domain, which would pull
  the ground out from under a build started in the same call — so the first run
  switches and the second run builds. On a cold switch the reimport takes a few
  minutes.
- **Build Web refuses to run twice in five minutes**, and refuses while another
  build is running. An automation channel that times out can replay a menu item,
  and an IL2CPP WebGL build is 10–25 minutes of CPU.

A WebGL build prints `[Pesky] build result: Succeeded, size N MB, …` in the
console and opens `Builds/Web` in Explorer when it finishes. It warns past a
60 MB budget.

### From the command line

```
"C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" ^
  -batchmode -projectPath "C:\Users\benos\Code\Pesky Weapons\Pesky Weapons Unity" ^
  -buildTarget WebGL ^
  -executeMethod Pesky.Editor.BuildTools.BuildFromCommandLine ^
  -logFile "%CD%\build.log"
```

`BuildFromCommandLine` exits the Editor with **0** on success and **1** on
failure, so CI fails properly. The Editor must be closed first — Unity allows
only one process per project. This is the same entry point a
`game-ci/unity-builder` workflow would call as `buildMethod`.

---

## 2. Publish it to GitHub Pages

```powershell
cd "C:\Users\benos\Code\Pesky Weapons"
.\publish.ps1
```

`publish.ps1` (do not edit it) clones or reuses
`..\Pesky-Weapons-Build`, wipes everything except `.git`, copies
`Builds\Web\*` in, writes `.nojekyll`, commits and pushes `main`. It refuses to
touch a folder whose `origin` is not `Pesky-Weapons-Build`, and it stops with a
clear message if `Builds\Web\index.html` does not exist.

### One-time GitHub setup

1. Create an **empty public** repository on GitHub named **`Pesky-Weapons-Build`**
   (no README, no .gitignore, no licence — the first publish force-fills it).
2. Run `.\publish.ps1` once. It clones the empty repository and pushes the build.
3. On GitHub: **Settings > Pages > Build and deployment > Deploy from a branch >
   Branch: `main` > Folder: `/ (root)` > Save**.
4. The game is then served at

   **https://bennormann.github.io/Pesky-Weapons-Build/**

Pages takes about a minute to go live after each push.

### Every time after that

- **Everyone must be on the same build.** The join handshake compares
  `Wire.ProtocolVersion`; a mismatch is refused. Publish after *any* change that
  touches the netcode, and tell everyone to reload before a session.
- **Hard-refresh after publishing** (Ctrl+F5, or Ctrl+Shift+R). The Unity loader,
  the `.wasm` and the `.data` are aggressively cached by the browser and by
  Unity's own IndexedDB data cache. `index.html` cache-busts `net.js` and
  `keys.js` with a `?v=<timestamp>` query on every load, but the Unity payload
  itself is only re-fetched on a hard refresh.
- GitHub Pages is a static host: it **cannot set `Content-Encoding`**, which is
  why the build ships Brotli **with the decompression fallback** (section 3).
  Without that fallback the page loads a blank canvas and the console shows a
  compressed-file error.

---

## 3. The Web player settings, and why each one

`BuildTools.ApplyPlayerSettings()` writes all of these, so nobody has to
remember them and nothing drifts:

| Setting | Value | Why |
|---|---|---|
| Template | `PROJECT:Pesky` | `Assets/WebGLTemplates/Pesky` — our `index.html`, `net.js`, `trystero.min.js`, `keys.js`. |
| Compression | **Brotli + decompression fallback** | Pages cannot set `Content-Encoding`; the fallback embeds a JS decompressor. |
| Run In Background | **on** | Without it the tab pauses when unfocused and every connection dies the moment a player alt-tabs to Discord. |
| Graphics APIs | `OpenGLES3` only, defaults off | WebGL 2 only. WebGPU is experimental and off. |
| Threads | off | No COOP/COEP headers on Pages means no `SharedArrayBuffer`. |
| Exceptions | `FullWithStacktrace` | A browser bug report is worthless without a stack trace. |
| Engine code stripping | on | Size. |
| Managed stripping | **Low** + `Assets/link.xml` | Size, without cutting types that are only reached by name. |
| IL2CPP code generation | `OptimizeSize` | Claws back most of the wasm that full stack traces cost. |
| Data caching | on | Returning players skip the `.data` download. |
| Memory | 64 MB initial, 2048 MB max, geometric growth | Eight tumbling rigidbodies and a labyrinth need room to grow; asking for it up front fails on weaker machines. |
| Company / product | `Ben Normann` / `Pesky Weapons` | Also the `AppData\LocalLow\<company>\<product>\Player.log` path for desktop builds. |

**Colour space stays Linear.** ATCK forces Gamma; Pesky does not, because the
project is authored Linear (URP) and WebGL 2 supports linear colour space.
`ApplyPlayerSettings` deliberately does not touch `PlayerSettings.colorSpace`.
If a browser build ever looks washed out or too dark compared with the Editor,
that is the first setting to suspect.

### `Assets/link.xml`

Ported from ATCK's, trimmed to the assemblies this project actually has
(ATCK's `UnityEngine.TextRenderingModule` and `UnityEngine.InputModule` entries
are gone — neither package is in `Packages/manifest.json`) and extended with
`UnityEngine.AIModule` (goblin `NavMeshAgent`s) and
`UnityEngine.UIElementsModule` (the UI Toolkit menu, room page, map and HUD).
It preserves types that stripping cannot see a reference to. The symptom of
getting it wrong is `Can't add component because class 'X' doesn't exist!` in
the browser console and an invisible or collider-less world.

---

## 4. Platform conditionals (why the Editor still works on the WebGL target)

Once the active build target is WebGL, **`UNITY_WEBGL` is defined inside the
Editor too**. Every browser-only path is therefore guarded with
`#if UNITY_WEBGL && !UNITY_EDITOR`, and the desktop transport with the exact
complement `#if !UNITY_WEBGL || UNITY_EDITOR`:

| File | Guard | Effect |
|---|---|---|
| `Transport/WebRtcTransport.cs` | `UNITY_WEBGL && !UNITY_EDITOR` (usings, all nine `AHNet_*` `DllImport`s, every call site) | In the Editor the class still compiles and `Connect` logs "WebGL-only" instead of calling into a `__Internal` library that is not there. |
| `Transport/TcpTransport.cs`, `TcpLink.cs` | `!UNITY_WEBGL \|\| UNITY_EDITOR` (whole file) | Sockets and threads are compiled out of a real WebGL player entirely; they stay available in the Editor on the WebGL target. |
| `Session/TransportFactory.cs` | `UNITY_WEBGL && !UNITY_EDITOR` (`ForPlatform`, `DefaultPort`, `HostAddresses`) | A WebGL player can only ever get `WebRtcTransport`; the Editor and standalone get `TcpTransport`. This is the only code that names a concrete transport. |
| `Game/UI/RoomCodes.cs` | `UNITY_WEBGL && !UNITY_EDITOR` (`IsAddressBased`) | The exact complement of the factory's branch, so the room code shown is always the code that transport understands: a six-character trystero code in the browser, `host:port` in the Editor and on desktop. |
| `Game/UI/Clipboard.cs` | `UNITY_WEBGL && !UNITY_EDITOR` | `AHNet_CopyClipboard` in the browser, `GUIUtility.systemCopyBuffer` everywhere else. |
| `Game/UI/MenuFlow.cs` | runtime `Application.platform != RuntimePlatform.WebGLPlayer` | Hides QUIT in the browser (a tab cannot quit itself). Runtime, not compile-time, on purpose. |

So with the Editor sitting on the WebGL target, pressing Play still uses TCP or
loopback exactly as before; only a real browser build takes the WebRTC path.

---

## 5. The browser bridge, and TURN for restrictive networks

Three names have to match across C#, the plugin and the page JS, and a mismatch
is a *silent* failure:

| Name | Where |
|---|---|
| GameObject `NetBridge` | `WebRtcTransport.GameObjectName`, and `SendMessage('NetBridge', …)` in `net.js` |
| `window.AH_Net` | defined by `net.js`, called by `Assets/Plugins/WebGL/AHNet.jslib` |
| `window.AH_UnityInstance` | set by `index.html` after `createUnityInstance`, read by `net.js` to flush its queue |

Every `AHNet_*` `DllImport` in C# has a matching export in `AHNet.jslib`
(`Start`, `Leave`, `Broadcast`, `SendTo`, `GetSelfId`, `UnityReady`,
`CopyClipboard`, `VoiceSetMode`, `VoiceSetPeerVolume`, `VoiceSetThreshold`).
Adding a `DllImport` without adding the export breaks the *link* step of the
build, not runtime.

The app id and room prefix are Pesky's own: `pesky-weapons-9d4kv2` and `PESKY-`.

### TURN hook

Browser-to-browser works on ordinary home networks through STUN alone. Campus
wifi, corporate networks, some mobile hotspots and symmetric NAT need a **TURN
relay**. The hook is live:

```html
<!-- Assets/WebGLTemplates/Pesky/index.html -->
window.PESKY_ICE_SERVERS = [
  { urls: ['stun:stun.l.google.com:19302', 'stun:stun1.l.google.com:19302'] },
  { urls: 'turn:relay.example.com:3478', username: 'u', credential: 'p' }   // add this
];
```

`net.js` reads `window.PESKY_ICE_SERVERS` in its `iceServers()` and falls back
to the hard-coded STUN list when the page does not define it. (ATCK defined the
global but never read it; that dead hook was made live during the port.)

You can edit `index.html` **in the published repository** and push, without
rebuilding Unity — it is a plain file at the site root. Keep the same change in
`Assets/WebGLTemplates/Pesky/index.html` or the next build will overwrite it.
Never commit a TURN credential to a public repository you care about; free TURN
credentials are usually short-lived and rotated.

---

## 6. The Windows two-player test build

**Pesky > Build Windows (two-player test)** produces a *development* Windows x64
player at `Builds/Windows/PeskyWeapons.exe`: windowed 1280×720, resizable, Run
In Background on, Mono scripting backend (a minute to build instead of twenty).
It builds the same scenes as the web build.

It switches the active build target to Windows and leaves it there — run it
once to switch, once more to build. **Pesky > Build Web** switches back to
WebGL the same way.

The pairing to run is in `docs/TEST-CHECKLIST.md`: the Editor hosts, the Windows
build joins `localhost:7777`. Windows Firewall asks on the first listen; allow
it on private networks. The desktop transport is LAN-only — no NAT traversal, no
encryption — so the browser build is the one for playing with people elsewhere.

---

## 7. What a finished web build contains

`Builds/Web/` after a successful build — the first real one was **2026-09-21,
16.2 MiB (16,964,942 bytes)** on disk:

```
index.html                     the template, Unity's loader macros filled in
net.js                  37 KB  trystero bridge (window.AH_Net)
keys.js                  7 KB  browser shortcut swallowing + Keyboard Lock
trystero.min.js         59 KB  vendored, no CDN
.nojekyll                      without it Pages silently drops Build/
Build/Web.loader.js    115 KB
Build/Web.data.unityweb 6.4 MB scenes and assets
Build/Web.wasm.unityweb 9.5 MB the engine and the game, IL2CPP -> wasm
Build/Web.framework.js.unityweb 77 KB
```

The build files are named after the output folder (`Web.*`) and carry the
`.unityweb` extension, which is what Brotli **with** the decompression fallback
produces — the sign that the Pages-compatible path is really in use. Without the
fallback they would be `.br` and Pages would serve them unusable.

There is no `TemplateData/` folder: the Pesky template has no images or CSS of
its own, so Unity writes none. Nothing in `index.html` references it.

All script paths in `index.html` are **relative** (`net.js`, `trystero.min.js`,
`Build/…`), which is what makes the game work from a sub-path such as
`/Pesky-Weapons-Build/`. Never change them to absolute `/…` paths.

---

## 8. Two things that will bite

**"Build was canceled." on the very first build of a fresh clone.** The
`com.unity.test-framework.performance` package (pulled in indirectly, it is not
in `manifest.json`) creates `Assets/Resources/PerformanceTestRunInfo.json` and
`PerformanceTestRunSettings.json` from its `OnPreprocessBuild` hook. Creating
new assets *during* a build changes the asset database mid-build and Unity
aborts with a bare `Build was canceled.` and no explanation. The files now exist
and are committed, so it does not recur — but if you ever delete them, the next
build fails once and the one after it succeeds.

**The AI packages pay rent in wasm.** `com.unity.ai.inference` compiles a large
set of "Sentis" pixel shaders into the player (they are the only warnings the
build produces: `integer modulus may be much slower…`), and
`com.unity.ai.assistant`, `com.unity.multiplayer.center`, `com.unity.timeline`
and `com.unity.visualscripting` are all in `manifest.json` without the game
using any of them. Removing the ones that are not needed is the cheapest way to
get the download below 16 MB if that ever matters.

---

## 9. Debugging a web build: the URL flags, the F3 overlay and driving a page by hand

Added in feedback round 5. Everything here is gated by
`Assets/Scripts/Game/DebugGate.cs` (ported from ATCK): it is **on in the Editor
and in development builds, and on a release page only when the URL carries
`?debug=1`**. A page without the flag builds none of it - the overlay component
disables itself in `Awake`, `DebugGate.Log` prints nothing, and the Session
assembly's `NetDebug` lines stay silent.

| Flag | Effect |
|---|---|
| `?debug=1` | Opens the gate: F3 overlay, `[pesky]` console lines (look-filter drops, map requests, host verdicts, reply deliveries). Read through `Assets/Plugins/WebGL/PeskyPlatform.jslib`'s `Pesky_QueryFlag`. |
| `?debug=1&nolock=1` | Pointer-lock bypass for browsers that refuse pointer lock (the desktop app's browser pane). Every "is the pointer locked" gate (`OrbitCamera.PointerLocked` -> `DebugGate.PointerLocked`) answers yes, the lock is **never requested** (`OrbitCamera.LockPointer` returns at once), and mouse look reads the same `Look` deltas. Off in the Editor, off without `?debug=1`. |
| `?debug=1&overlay=1` | The overlay is shown from the first frame instead of waiting for F3. |

**The overlay** (`Assets/Scripts/Game/DebugOverlay.cs`, a `DebugOverlay` object
under `_UI` in Tutorial, Labyrinth and Dev/FeelBox; a code-built UI Toolkit
label on the shared `UiPanelSettings`, sorting order 100). **F3** toggles it
(`keys.js` already swallows the browser's own F3). It shows, refreshed once a
second:

```
fps=31.2 fixed/frame=1.87(max 3)
role=host transport=WebRtc slot=0 peers=1 rtt=- simLag=0t phase=Playing
in=21/s,1.3KB/s out=44/s,2.9KB/s poseIn=20Hz
cell=12 target=GoodEnd role=Weapon compass=N lookDrops=0 lock=yes
```

- `fps` is a one-second average of `Update` calls; `fixed/frame` the physics
  steps per rendered frame (max in the window).
- `rtt` is the last `CLOCK_PING` round trip (`RoomClock.LastRttMs`); the host
  shows `-`. `simLag` is `RoomClock.Tick - WorldSim.Tick`: how many ticks the sim
  is behind the room clock (it climbs while a tab is stalled and the catch-up cap
  of 20 ticks per frame works it off).
- `in` / `out` are transport messages and payload bytes per second, counted at
  the Session boundary (`NetStats`: FRAMEs, raw POSE, pings, pongs; not the base64
  and WebRTC framing the browser adds). `poseIn` is the rate of raw POSE (0x01)
  arriving from other peers.
- `cell` / `target` / `role` / `compass` come from the local sim: the compass
  target kind (GoodEnd, BadEnd, Cell#n), this peer's role, and the doorway the
  green spike names (N/E/S/W or `spin`). `red=` appears on a Mage's machine.
- `lookDrops` / `lookScaled` are the spike filter's counters, `lock` the pointer
  lock as gameplay sees it, and `TAB-HIDDEN` appears while `document.hidden`.

The same numbers go to the browser console every 5 s as one line, `[pesky] dbg
...`, so a console dump (or an agent's `read_console_messages`) tells the story
without a screenshot. Other `[pesky]` lines under the gate: `look: dropped ...`,
`map: bend requested ...`, `map: refused locally - ...`, `host: bend ... accepted /
refused: <why>`, `host: swap ... accepted / refused`, `reply: ROLE_ASSIGN ...`,
`reply: COMPASS_TARGETS ...`. Note that on a `?debug=1` page **the host's own
console names who it bent and why it refused** - the wire stays silent, the
page does not.

### Driving a page by hand (an embedded browser or an automation harness)

- Keys reach Unity only as `KeyboardEvent`s on the focused `#unity-canvas` with
  `key`, `code` **and** `keyCode` set (a synthetic event without `code` /
  `keyCode` is ignored for Backspace, arrows and friends; printable characters
  need a `keypress` with `charCode` as well). Mouse buttons must be
  `PointerEvent`s (`pointerdown` / `pointerup` beside `mousedown` / `mouseup`).
- A hidden or occluded page gets **no `requestAnimationFrame` at all**, which
  stops Unity's main loop dead (Unity WebGL is rAF-driven unless
  `Application.targetFrameRate` is set). For a headless measurement, polyfill it
  before or after load - `window.requestAnimationFrame = cb =>
  setTimeout(() => cb(performance.now()), 0)` - and read the *uncapped* frame
  rate from the overlay; it then reflects CPU frame cost, not vsync.
- Signalling goes through public Nostr relays that come and go. For a local
  two-tab test with no relay dependence, run any NIP-01 relay on localhost and,
  **before the first `HOST` / `JOIN` click** (trystero fixes its relay list on
  the page's first `joinRoom`), replace `window.trystero` with a wrapper whose
  `joinRoom` merges `relayConfig: { urls: ['ws://127.0.0.1:8091'], redundancy: 1 }`
  into the config. A 90-line Python relay (`websockets`) was enough for round 5.

### What a hidden host tab does to everyone

A background tab in Chrome gets no rAF, so a **hidden host stops ticking**: its
`WorldSim` freezes, POSE stops, clients' `simLag` climbs and their remote views
freeze; when the tab returns, `NetSession.AdvanceHost` catches up at most 20
ticks per frame and, past 5 s behind, jumps the clock and re-snapshots everyone
(`ResnapshotAll`). Clients never desync permanently - they wait or re-request a
snapshot (`RESYNC_REQ` after 5 s behind) - but nothing can make a hidden tab
simulate. `PlayerSettings.runInBackground` only keeps the game alive on
**focus loss** (alt-tab to Discord with the tab still visible), not on
visibility loss. Play with **the host's tab visible** - a second window, a
second monitor, or simply the host not tabbing away; if the host must be in a
background tab, every client will see the world stall until it comes back.
