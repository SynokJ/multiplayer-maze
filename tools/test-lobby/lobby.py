#!/usr/bin/env python3
"""
Minimal test lobby for the multiplayer maze. Standard library only (Python 3.8+).

It is NOT the production lobby. It speaks the same HTTP contract, so the WebGL client
and the Linux room server work against it unchanged:

  POST /api/rooms                   {playerName, roomName?, maxPlayers?}  -> 201 {room, host, port, secure, path, token}
  GET  /api/rooms                                                         -> 200 {rooms: [...]}
  POST /api/rooms/:id/join          {playerName}                          -> 200 {room, host, port, secure, path, token}
  POST /internal/rooms/:id/heartbeat {players, state}  (header x-internal-key) -> 204
                                    state is "lobby" or "playing"; playing rooms are hidden and closed to joins

Creating a room starts one room server process on a free local port. nginx forwards
/r/<port> to it, so the browser only ever talks to nginx (port --public-port).
"""
import argparse
import atexit
import base64
import hashlib
import hmac
import json
import os
import random
import re
import shlex
import signal
import socket
import string
import subprocess
import sys
import threading
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

NAME_RE = re.compile(r"^[A-Za-z0-9_ .\-]{1,24}$")
ROOM_ID_RE = re.compile(r"^[A-Z0-9]{5}$")

START_TIMEOUT = 40.0      # seconds to wait for a new room to start listening
HEARTBEAT_TIMEOUT = 25.0  # a room that stops reporting is killed
EMPTY_TIMEOUT = 45.0      # an empty room is closed after this long
TOKEN_LIFETIME = 120      # seconds a join token stays valid

lock = threading.Lock()
rooms = {}
settings = None


def b64url(data):
    return base64.urlsafe_b64encode(data).decode("ascii").rstrip("=")


def make_token(room_id, name, secret):
    """Same format as the room server's JoinTokenVerifier: payload.mac, one token per join."""
    payload = json.dumps(
        {"r": room_id, "n": name, "j": uuid.uuid4().hex[:16], "exp": int(time.time()) + TOKEN_LIFETIME},
        separators=(",", ":"),
    )
    payload_b64 = b64url(payload.encode("utf-8"))
    mac = hmac.new(secret.encode("utf-8"), payload_b64.encode("ascii"), hashlib.sha256).digest()
    return payload_b64 + "." + b64url(mac)


def port_open(port):
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=0.3):
            return True
    except OSError:
        return False


def free_port():
    used = {r["port"] for r in rooms.values()}
    for port in range(settings.port_min, settings.port_max + 1):
        if port not in used and not port_open(port):
            return port
    return None


def new_room_id():
    while True:
        room_id = "".join(random.choice(string.ascii_uppercase + string.digits) for _ in range(5))
        if room_id not in rooms:
            return room_id


def public_room(room):
    return {
        "id": room["id"],
        "name": room["name"],
        "players": room["players"],
        "maxPlayers": room["max"],
        "state": "playing" if room["playing"] else "ready",
    }


def stop_room(room):
    proc = room["proc"]
    if proc.poll() is None:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
    try:
        room["log"].close()
    except OSError:
        pass


def start_room(room_id, name, max_players, port):
    log_path = os.path.join(settings.log_dir, "room-%s.log" % room_id)
    log = open(log_path, "wb")

    command = shlex.split(settings.server) + [
        "-batchmode", "-nographics",
        "-port", str(port),
        "-roomId", room_id,
        "-maxPlayers", str(max_players),
        "-level", str(settings.level),
    ]

    env = dict(os.environ)
    env["ROOM_SECRET"] = settings.secret
    env["LOBBY_URL"] = "http://127.0.0.1:%d" % settings.port
    env["LOBBY_INTERNAL_KEY"] = settings.key

    proc = subprocess.Popen(command, env=env, stdout=log, stderr=subprocess.STDOUT)
    room = {
        "id": room_id, "name": name, "max": max_players, "port": port, "proc": proc, "log": log,
        "players": 0, "created": time.time(), "last_beat": time.time(), "empty_since": time.time(),
        "seen_players": False, "playing": False,
    }
    return room


def wait_until_listening(room):
    deadline = time.time() + START_TIMEOUT
    while time.time() < deadline:
        if room["proc"].poll() is not None:
            return False
        if port_open(room["port"]):
            return True
        time.sleep(0.25)
    return False


def reaper():
    while True:
        time.sleep(3)
        now = time.time()
        with lock:
            for room_id in list(rooms):
                room = rooms[room_id]
                dead = room["proc"].poll() is not None
                silent = now - room["last_beat"] > HEARTBEAT_TIMEOUT
                empty = room["players"] == 0 and now - room["empty_since"] > EMPTY_TIMEOUT

                if dead or silent or empty:
                    why = "exited" if dead else ("no heartbeat" if silent else "empty")
                    print("closing room %s (%s)" % (room_id, why), flush=True)
                    stop_room(room)
                    del rooms[room_id]


def shutdown_all():
    for room in list(rooms.values()):
        stop_room(room)


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        print("%s %s" % (self.address_string(), fmt % args), flush=True)

    # ---- helpers ----
    def send_json(self, status, body=None):
        data = b"" if body is None else json.dumps(body).encode("utf-8")
        self.send_response(status)
        if body is not None:
            self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(data)

    def fail(self, status, code):
        self.send_json(status, {"error": code})

    def read_json(self):
        length = int(self.headers.get("Content-Length") or 0)
        if length <= 0 or length > 4096:
            return None
        try:
            body = json.loads(self.rfile.read(length).decode("utf-8"))
        except (ValueError, UnicodeDecodeError):
            return None
        return body if isinstance(body, dict) else None

    def connect_info(self, room, name):
        host = (self.headers.get("Host") or "localhost").split(":")[0]
        return {
            "room": public_room(room),
            "host": host,
            "port": settings.public_port,
            "secure": settings.secure,
            "path": "/r/%d" % room["port"],
            "token": make_token(room["id"], name, settings.secret),
        }

    # ---- routes ----
    def do_GET(self):
        if self.path.split("?")[0] == "/api/rooms":
            # Rooms whose round has started cannot take new players, so they are not listed.
            with lock:
                self.send_json(200, {"rooms": [public_room(r) for r in rooms.values() if not r["playing"]]})
        else:
            self.fail(404, "not_found")

    def do_POST(self):
        path = self.path.split("?")[0]

        if path == "/api/rooms":
            self.create_room()
            return

        match = re.match(r"^/api/rooms/([A-Za-z0-9]+)/join$", path)
        if match:
            self.join_room(match.group(1).upper())
            return

        match = re.match(r"^/internal/rooms/([A-Za-z0-9]+)/heartbeat$", path)
        if match:
            self.heartbeat(match.group(1).upper())
            return

        self.fail(404, "not_found")

    def create_room(self):
        body = self.read_json()
        name = (body or {}).get("playerName")
        if not isinstance(name, str) or not NAME_RE.match(name):
            self.fail(400, "invalid_request")
            return

        max_players = body.get("maxPlayers", 4)
        if not isinstance(max_players, int) or isinstance(max_players, bool):
            max_players = 4
        max_players = max(1, min(max_players, 16))

        room_name = body.get("roomName")
        if not isinstance(room_name, str) or not room_name.strip():
            room_name = name + " room"

        with lock:
            if len(rooms) >= settings.max_rooms:
                self.fail(503, "no_capacity")
                return

            port = free_port()
            if port is None:
                self.fail(503, "no_capacity")
                return

            room_id = new_room_id()
            try:
                room = start_room(room_id, room_name[:40], max_players, port)
            except OSError as error:
                print("cannot start the room server: %s" % error, flush=True)
                self.fail(500, "room_failed_to_start")
                return

            rooms[room_id] = room

        # Wait outside the lock so other requests keep working.
        if not wait_until_listening(room):
            with lock:
                rooms.pop(room_id, None)
                stop_room(room)
            self.fail(500, "room_failed_to_start")
            return

        with lock:
            room["last_beat"] = time.time()
            body = self.connect_info(room, name)

        print("room %s started on port %d" % (room_id, port), flush=True)
        self.send_json(201, body)

    def join_room(self, room_id):
        body = self.read_json()
        name = (body or {}).get("playerName")
        if not isinstance(name, str) or not NAME_RE.match(name) or not ROOM_ID_RE.match(room_id):
            self.fail(400, "invalid_request")
            return

        with lock:
            room = rooms.get(room_id)
            if room is None or room["proc"].poll() is not None:
                self.fail(404, "no_such_room")
                return

            if room["playing"]:
                self.fail(409, "room_closed")
                return

            if room["players"] >= room["max"]:
                self.fail(409, "room_full")
                return

            self.send_json(200, self.connect_info(room, name))

    def heartbeat(self, room_id):
        if self.headers.get("x-internal-key", "") != settings.key:
            self.fail(403, "forbidden")
            return

        body = self.read_json() or {}
        with lock:
            room = rooms.get(room_id)
            if room is None:
                self.fail(404, "no_such_room")
                return

            players = body.get("players")
            if isinstance(players, int) and players >= 0:
                if players == 0 and room["players"] > 0:
                    room["empty_since"] = time.time()
                if players > 0:
                    room["seen_players"] = True
                room["players"] = players

            state = body.get("state")
            if state in ("lobby", "playing"):
                room["playing"] = state == "playing"

            room["last_beat"] = time.time()

        self.send_json(204)


def main():
    global settings

    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--server", required=True, help='room server command, e.g. "/opt/maze-server/MazeServer.x86_64"')
    parser.add_argument("--port", type=int, default=3000, help="lobby HTTP port (nginx proxies /api/ here)")
    parser.add_argument("--public-port", type=int, default=80, help="port the browser uses for the room WebSocket (nginx)")
    parser.add_argument("--secure", action="store_true", help="tell clients to use wss (only with HTTPS)")
    parser.add_argument("--port-min", type=int, default=7001)
    parser.add_argument("--port-max", type=int, default=7099)
    parser.add_argument("--max-rooms", type=int, default=20)
    parser.add_argument("--level", type=int, default=0)
    parser.add_argument("--log-dir", default="room-logs")
    parser.add_argument("--secret", default=os.environ.get("ROOM_SECRET") or "dev-secret-dev-secret-dev-secret!")
    parser.add_argument("--key", default=os.environ.get("LOBBY_INTERNAL_KEY") or uuid.uuid4().hex)
    settings = parser.parse_args()

    os.makedirs(settings.log_dir, exist_ok=True)
    atexit.register(shutdown_all)
    signal.signal(signal.SIGTERM, lambda *_: sys.exit(0))

    threading.Thread(target=reaper, daemon=True).start()

    server = ThreadingHTTPServer(("127.0.0.1", settings.port), Handler)
    print("test lobby on 127.0.0.1:%d, rooms on ports %d-%d" % (settings.port, settings.port_min, settings.port_max), flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
