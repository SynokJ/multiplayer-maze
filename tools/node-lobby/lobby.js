#!/usr/bin/env node
'use strict';
/*
 * Minimal Node.js lobby for the multiplayer maze. No npm packages: Node 18+ only.
 *
 * Same HTTP contract as tools/test-lobby/lobby.py, so the WebGL client and the Linux room
 * server work with either one:
 *
 *   POST /api/rooms                    {playerName, roomName?, maxPlayers?} -> 201 {room, host, port, secure, path, token}
 *   GET  /api/rooms                                                         -> 200 {rooms: [...]}
 *   POST /api/rooms/:id/join           {playerName}                         -> 200 {room, host, port, secure, path, token}
 *   POST /internal/rooms/:id/heartbeat {players, state}  (x-internal-key)   -> 204
 *
 * Creating a room starts one room server process on a free local port. nginx forwards
 * /r/<port> to it. A room whose round has started (state "playing") is hidden from the
 * list and refuses joins.
 */
const http = require('http');
const net = require('net');
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const NAME_RE = /^[A-Za-z0-9_ .\-]{1,24}$/;
const ROOM_ID_RE = /^[A-Z0-9]{5}$/;

const START_TIMEOUT_MS = 40000;    // wait this long for a new room to start listening
const HEARTBEAT_TIMEOUT_MS = 25000; // a room that stops reporting is closed
const EMPTY_TIMEOUT_MS = 45000;     // an empty room is closed after this long
const TOKEN_LIFETIME_S = 120;

// ---- settings ----
function parseArgs(argv) {
  const settings = {
    server: null,
    port: 3000,
    publicPort: 80,
    secure: false,
    portMin: 7001,
    portMax: 7099,
    maxRooms: 20,
    level: 0,
    logDir: 'room-logs',
    secret: process.env.ROOM_SECRET || 'dev-secret-dev-secret-dev-secret!',
    key: process.env.LOBBY_INTERNAL_KEY || crypto.randomBytes(16).toString('hex'),
  };

  const numbers = { '--port': 'port', '--public-port': 'publicPort', '--port-min': 'portMin',
    '--port-max': 'portMax', '--max-rooms': 'maxRooms', '--level': 'level' };
  const strings = { '--server': 'server', '--log-dir': 'logDir', '--secret': 'secret', '--key': 'key' };

  for (let i = 0; i < argv.length; i++) {
    const name = argv[i];
    if (name === '--secure') settings.secure = true;
    else if (numbers[name]) settings[numbers[name]] = parseInt(argv[++i], 10);
    else if (strings[name]) settings[strings[name]] = argv[++i];
    else if (name === '--help' || name === '-h') usage(0);
    else usage(1, 'unknown option ' + name);
  }

  if (!settings.server) usage(1, '--server is required');
  return settings;
}

function usage(code, message) {
  if (message) console.error(message + '\n');
  console.error('usage: node lobby.js --server "/opt/maze-server/MazeServer.x86_64" [--port 3000] [--public-port 80]\n' +
    '       [--secure] [--port-min 7001] [--port-max 7099] [--max-rooms 20] [--level 0] [--log-dir room-logs]');
  process.exit(code);
}

const settings = parseArgs(process.argv.slice(2));
const rooms = new Map();

// ---- helpers ----
function makeToken(roomId, name) {
  // Same format as the room server's JoinTokenVerifier: payload.mac, one token per join.
  const payload = JSON.stringify({
    r: roomId,
    n: name,
    j: crypto.randomBytes(8).toString('hex'),
    exp: Math.floor(Date.now() / 1000) + TOKEN_LIFETIME_S,
  });
  const payloadB64 = Buffer.from(payload, 'utf8').toString('base64url');
  const mac = crypto.createHmac('sha256', settings.secret).update(payloadB64).digest('base64url');
  return payloadB64 + '.' + mac;
}

function portOpen(port) {
  return new Promise((resolve) => {
    const socket = net.connect({ host: '127.0.0.1', port });
    const done = (ok) => { socket.destroy(); resolve(ok); };
    socket.setTimeout(300, () => done(false));
    socket.once('connect', () => done(true));
    socket.once('error', () => done(false));
  });
}

async function freePort() {
  const used = new Set([...rooms.values()].map((room) => room.port));
  for (let port = settings.portMin; port <= settings.portMax; port++)
    if (!used.has(port) && !(await portOpen(port))) return port;
  return null;
}

function newRoomId() {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
  for (;;) {
    let id = '';
    for (let i = 0; i < 5; i++) id += chars[crypto.randomInt(chars.length)];
    if (!rooms.has(id)) return id;
  }
}

function publicRoom(room) {
  return { id: room.id, name: room.name, players: room.players, maxPlayers: room.max,
    state: room.playing ? 'playing' : 'ready' };
}

function stopRoom(room) {
  if (!room.exited) {
    room.child.kill('SIGTERM');
    setTimeout(() => { if (!room.exited) room.child.kill('SIGKILL'); }, 5000).unref();
  }
  try { fs.closeSync(room.logFd); } catch (e) { /* already closed */ }
}

function startRoom(id, name, max, port) {
  fs.mkdirSync(settings.logDir, { recursive: true });
  const logFd = fs.openSync(path.join(settings.logDir, 'room-' + id + '.log'), 'a');

  const [command, ...baseArgs] = settings.server.split(/\s+/).filter(Boolean);
  const args = baseArgs.concat(['-batchmode', '-nographics', '-port', String(port), '-roomId', id,
    '-maxPlayers', String(max), '-level', String(settings.level)]);

  const env = Object.assign({}, process.env, {
    ROOM_SECRET: settings.secret,
    LOBBY_URL: 'http://127.0.0.1:' + settings.port,
    LOBBY_INTERNAL_KEY: settings.key,
  });

  const child = spawn(command, args, { env, stdio: ['ignore', logFd, logFd] });
  const now = Date.now();
  const room = { id, name, max, port, child, logFd, players: 0, playing: false,
    exited: false, spawnError: null, lastBeat: now, emptySince: now };

  child.on('exit', () => { room.exited = true; });
  child.on('error', (error) => { room.exited = true; room.spawnError = error; });
  return room;
}

async function waitUntilListening(room) {
  const deadline = Date.now() + START_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (room.exited) return false;
    if (await portOpen(room.port)) return true;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  return false;
}

function reap() {
  const now = Date.now();
  for (const [id, room] of rooms) {
    const dead = room.exited;
    const silent = now - room.lastBeat > HEARTBEAT_TIMEOUT_MS;
    const empty = room.players === 0 && now - room.emptySince > EMPTY_TIMEOUT_MS;
    if (dead || silent || empty) {
      console.log('closing room ' + id + ' (' + (dead ? 'exited' : silent ? 'no heartbeat' : 'empty') + ')');
      stopRoom(room);
      rooms.delete(id);
    }
  }
}

// ---- HTTP ----
function sendJson(res, status, body) {
  const data = body === undefined ? '' : JSON.stringify(body);
  const headers = { 'Content-Length': Buffer.byteLength(data), Connection: 'close' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  res.writeHead(status, headers);
  res.end(data);
}

const fail = (res, status, code) => sendJson(res, status, { error: code });

function readJson(req) {
  return new Promise((resolve) => {
    const chunks = [];
    let size = 0;
    req.on('data', (chunk) => {
      size += chunk.length;
      if (size > 4096) { resolve(null); req.destroy(); return; }
      chunks.push(chunk);
    });
    req.on('end', () => {
      try {
        const body = JSON.parse(Buffer.concat(chunks).toString('utf8'));
        resolve(body && typeof body === 'object' && !Array.isArray(body) ? body : null);
      } catch (e) {
        resolve(null);
      }
    });
    req.on('error', () => resolve(null));
  });
}

function connectInfo(req, room, name) {
  return {
    room: publicRoom(room),
    host: (req.headers.host || 'localhost').split(':')[0],
    port: settings.publicPort,
    secure: settings.secure,
    path: '/r/' + room.port,
    token: makeToken(room.id, name),
  };
}

async function createRoom(req, res) {
  const body = await readJson(req);
  const name = body && body.playerName;
  if (typeof name !== 'string' || !NAME_RE.test(name)) return fail(res, 400, 'invalid_request');

  let max = Number.isInteger(body.maxPlayers) ? body.maxPlayers : 4;
  max = Math.max(1, Math.min(max, 16));
  const roomName = typeof body.roomName === 'string' && body.roomName.trim() ? body.roomName : name + ' room';

  if (rooms.size >= settings.maxRooms) return fail(res, 503, 'no_capacity');

  const port = await freePort();
  if (port === null) return fail(res, 503, 'no_capacity');

  const id = newRoomId();
  const room = startRoom(id, roomName.slice(0, 40), max, port);
  rooms.set(id, room);  // reserved before waiting, so a second request cannot take the same port

  if (!(await waitUntilListening(room))) {
    if (room.spawnError) console.error('cannot start the room server: ' + room.spawnError.message);
    rooms.delete(id);
    stopRoom(room);
    return fail(res, 500, 'room_failed_to_start');
  }

  room.lastBeat = Date.now();
  console.log('room ' + id + ' started on port ' + port);
  sendJson(res, 201, connectInfo(req, room, name));
}

async function joinRoom(req, res, id) {
  const body = await readJson(req);
  const name = body && body.playerName;
  if (typeof name !== 'string' || !NAME_RE.test(name) || !ROOM_ID_RE.test(id)) return fail(res, 400, 'invalid_request');

  const room = rooms.get(id);
  if (!room || room.exited) return fail(res, 404, 'no_such_room');
  if (room.playing) return fail(res, 409, 'room_closed');
  if (room.players >= room.max) return fail(res, 409, 'room_full');

  sendJson(res, 200, connectInfo(req, room, name));
}

async function heartbeat(req, res, id) {
  if (req.headers['x-internal-key'] !== settings.key) return fail(res, 403, 'forbidden');

  const body = (await readJson(req)) || {};
  const room = rooms.get(id);
  if (!room) return fail(res, 404, 'no_such_room');

  if (Number.isInteger(body.players) && body.players >= 0) {
    if (body.players === 0 && room.players > 0) room.emptySince = Date.now();
    room.players = body.players;
  }
  if (body.state === 'lobby' || body.state === 'playing') room.playing = body.state === 'playing';

  room.lastBeat = Date.now();
  sendJson(res, 204);
}

const server = http.createServer((req, res) => {
  const url = req.url.split('?')[0];
  let match;

  const handle = (promise) => promise.catch((error) => {
    console.error(error);
    if (!res.headersSent) fail(res, 500, 'internal_error');
  });

  if (req.method === 'GET' && url === '/api/rooms') {
    // Rooms whose round has started cannot take new players, so they are not listed.
    return sendJson(res, 200, { rooms: [...rooms.values()].filter((room) => !room.playing).map(publicRoom) });
  }
  if (req.method === 'POST' && url === '/api/rooms') return handle(createRoom(req, res));
  if (req.method === 'POST' && (match = url.match(/^\/api\/rooms\/([A-Za-z0-9]+)\/join$/)))
    return handle(joinRoom(req, res, match[1].toUpperCase()));
  if (req.method === 'POST' && (match = url.match(/^\/internal\/rooms\/([A-Za-z0-9]+)\/heartbeat$/)))
    return handle(heartbeat(req, res, match[1].toUpperCase()));

  fail(res, 404, 'not_found');
});

server.on('connection', (socket) => socket.on('error', () => {}));
setInterval(reap, 3000).unref();

function shutdown() {
  for (const room of rooms.values()) if (!room.exited) room.child.kill('SIGTERM');
  process.exit(0);
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);

server.listen(settings.port, '127.0.0.1', () => {
  console.log('node lobby on 127.0.0.1:' + settings.port + ', rooms on ports ' + settings.portMin + '-' + settings.portMax);
});
