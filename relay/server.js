'use strict';
// Pesky Weapons relay (docs/WEB-BUILD.md, "The relay").
//
// A browser joins  wss://<host>/room/<CODE>?id=<peerId>  and from then on:
//   text frames, server -> client only, JSON control:
//     {t:'hello', id, peers:[...]}   you are in; these peers are already here
//     {t:'join',  id}                a peer arrived
//     {t:'leave', id}                a peer left
//   binary frames, both ways, the game's own packets:
//     client -> server  [toLen:1][to:toLen bytes ascii][payload]   toLen 0 = everyone else in the room
//     server -> client  [fromLen:1][from][payload]
// The room is the code; the peer id is the same id the browser uses on its WebRTC path, so the game sees one
// peer whichever way the packets travel. Nothing is stored; an empty room is forgotten at once.

const http = require('http');
const { WebSocketServer } = require('ws');

const PORT = process.env.PORT || 8787;
const ALLOWED = (process.env.ALLOWED_ORIGINS || '').split(',').map(s => s.trim()).filter(Boolean);
const MAX_ROOM = Number(process.env.MAX_ROOM || 8);
const MAX_FRAME = 64 * 1024;          // the game's packets are a few hundred bytes
const MAX_MSGS_PER_SEC = 600;         // 8 peers at 20 Hz is 160; anything near this is a bug or abuse
const PING_MS = 20000;
const ROOM_RE = /^[A-Z0-9]{3,12}$/;
const ID_RE = /^[A-Za-z0-9_-]{4,64}$/;

const rooms = new Map();              // code -> Map<peerId, ws>

const server = http.createServer((req, res) => {
  if (req.url === '/health' || req.url === '/') {
    let peers = 0;
    for (const r of rooms.values()) peers += r.size;
    res.writeHead(200, { 'content-type': 'text/plain', 'access-control-allow-origin': '*' });
    res.end('pesky relay ok: ' + rooms.size + ' room(s), ' + peers + ' peer(s)\n');
    return;
  }
  res.writeHead(404);
  res.end();
});

const wss = new WebSocketServer({ noServer: true, maxPayload: MAX_FRAME });

server.on('upgrade', (req, socket, head) => {
  const origin = req.headers.origin || '';
  if (ALLOWED.length && !ALLOWED.includes(origin)) { refuse(socket, 403); return; }
  let url;
  try { url = new URL(req.url, 'http://relay'); } catch (e) { refuse(socket, 400); return; }
  const m = url.pathname.match(/^\/room\/([A-Za-z0-9]+)\/?$/);
  const code = m ? m[1].toUpperCase() : '';
  const id = url.searchParams.get('id') || '';
  if (!ROOM_RE.test(code) || !ID_RE.test(id)) { refuse(socket, 400); return; }
  wss.handleUpgrade(req, socket, head, ws => join(ws, code, id));
});

function refuse(socket, status) {
  socket.write('HTTP/1.1 ' + status + ' ' + (status === 403 ? 'Forbidden' : 'Bad Request') + '\r\n\r\n');
  socket.destroy();
}

function join(ws, code, id) {
  let room = rooms.get(code);
  if (!room) { room = new Map(); rooms.set(code, room); }
  if (room.size >= MAX_ROOM && !room.has(id)) { ws.close(4001, 'room full'); return; }
  const old = room.get(id);
  if (old && old !== ws) { room.delete(id); old.close(4002, 'replaced by a newer connection'); }
  room.set(id, ws);
  ws.alive = true;
  ws.count = 0;
  ws.on('pong', () => { ws.alive = true; });

  ws.send(JSON.stringify({ t: 'hello', id, peers: [...room.keys()].filter(p => p !== id) }));
  for (const [pid, p] of room) if (pid !== id && p.readyState === 1) p.send(JSON.stringify({ t: 'join', id }));
  console.log('join', code, id, 'room size', room.size);

  ws.on('message', (data, isBinary) => {
    if (!isBinary) return;                                 // clients never send control frames
    if (++ws.count > MAX_MSGS_PER_SEC) { ws.close(4003, 'too many messages'); return; }
    const buf = Buffer.isBuffer(data) ? data : Buffer.from(data);
    if (buf.length < 1) return;
    const toLen = buf[0];
    if (1 + toLen > buf.length) return;
    const to = toLen ? buf.subarray(1, 1 + toLen).toString('ascii') : '';
    const payload = buf.subarray(1 + toLen);
    const idLen = Buffer.byteLength(id, 'ascii');
    const out = Buffer.allocUnsafe(1 + idLen + payload.length);
    out[0] = idLen;
    out.write(id, 1, 'ascii');
    payload.copy(out, 1 + idLen);
    if (to) {
      const p = room.get(to);
      if (p && p.readyState === 1) p.send(out);
    } else {
      for (const [pid, p] of room) if (pid !== id && p.readyState === 1) p.send(out);
    }
  });

  ws.on('close', () => {
    if (room.get(id) !== ws) return;                       // already replaced
    room.delete(id);
    for (const p of room.values()) if (p.readyState === 1) p.send(JSON.stringify({ t: 'leave', id }));
    if (room.size === 0) rooms.delete(code);
    console.log('leave', code, id, 'room size', room.size);
  });
  ws.on('error', err => console.warn('socket error', code, id, err && err.message));
}

// Keepalive and the per-second message budget.
setInterval(() => {
  for (const room of rooms.values()) {
    for (const ws of room.values()) {
      ws.count = 0;
      if (!ws.alive) { ws.terminate(); continue; }
      ws.alive = false;
      try { ws.ping(); } catch (e) { }
    }
  }
}, PING_MS);
setInterval(() => { for (const room of rooms.values()) for (const ws of room.values()) ws.count = 0; }, 1000);

server.listen(PORT, () => console.log('pesky relay listening on', PORT, 'origins:', ALLOWED.length ? ALLOWED.join(',') : 'any'));
