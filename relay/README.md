# Pesky Weapons relay

A tiny WebSocket relay so two browsers that cannot reach each other directly (campus wifi: client
isolation, UDP filtering, blocked signaling relays) can still play. The game tries its normal WebRTC path
first and uses this only for the peers it cannot reach. If this server is down, stopped or unpaid, the game
behaves exactly as without it. Nothing is stored here.

## Deploy on Railway (about five minutes)

1. In your Railway project: **New → GitHub Repo → `BenNormann/Pesky-Weapons`**. In the service's
   **Settings → Source**, set **Root Directory** to `relay` and the branch you want built (`main`, or the
   PR branch while testing). Railway detects Node and runs `npm start`.
2. **Settings → Networking → Generate Domain.** Copy the domain (for example
   `pesky-relay-production.up.railway.app`).
3. **Variables:** add `ALLOWED_ORIGINS` = `https://bennormann.github.io` (comma-separate several).
   Without it the relay accepts any site, which is fine for a quick test and wrong for the published game.
4. Leave App Sleeping off (default). A sleeping relay adds a cold start to the first join.
5. Put the domain in the WebGL template: `Assets/WebGLTemplates/Pesky/index.html`,
   `window.PESKY_RELAY_URL = 'wss://<that domain>'`, then publish. The Build repo picks it up from the
   template; do not edit the Build repo by hand.

`https://<domain>/health` shows the room and peer count.

## Protocol

Connect to `wss://<host>/room/<CODE>?id=<peerId>`. Text frames (server to client only) are JSON:
`{t:'hello', id, peers:[...]}`, `{t:'join', id}`, `{t:'leave', id}`. Binary frames are the game's packets:
client to server `[toLen][to][payload]` (toLen 0 = everyone else), server to client `[fromLen][from][payload]`.
The peer id is the same id the browser uses on its WebRTC path.

Limits: 8 peers per room, 64 KB per frame, 600 messages per second per socket, ping every 20 s.

## Cost

Idle it uses about 128 MB and no CPU: roughly one to three dollars a month of usage on the Hobby plan's
included credit. Game traffic is a few hundred bytes per packet, so egress is cents.
