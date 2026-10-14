"""How Python code talks to the FryPDF studio about what it shows: display and update messages out, events on its
visuals back in, and the callbacks those events run.

Two channels, one for each way a Python program runs:

* in a notebook, the kernel (fry_kernel.py) sends messages on its protocol pipe and reads events on it too; callbacks
  run when the kernel is idle, or when the running cell asks (process_events, wait);
* a program that is run owns its stdout and stdin, so it writes each message on a line after __FRY_DISPLAY__ and hears
  about events over a loopback socket, whose address and token the studio gives it (FRY_EVENTS, FRY_EVENTS_TOKEN); it
  connects the first time its code listens to a visual. Callbacks run only when it asks.

The messages are those of docs/kernel-protocol.md and docs/visual-protocol.md.
"""

import json
import math
import os
import queue
import socket
import sys
import threading
import time
import uuid

DISPLAY_MARKER = "__FRY_DISPLAY__"
EVENT_KINDS = ("click", "select", "step")

# A live visual is redrawn at most this often; the latest update wins.
MIN_UPDATE_INTERVAL = 1 / 30


def clean(value):
    """A value as JSON can carry it: NaN and infinity are null (a gap), numpy's scalars and arrays plain Python."""
    if isinstance(value, float):
        return value if math.isfinite(value) else None
    if value is None or isinstance(value, (bool, int, str)):
        return value
    if isinstance(value, dict):
        return {str(k): clean(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [clean(v) for v in value]
    tolist = getattr(value, "tolist", None)  # numpy arrays and scalars
    if callable(tolist):
        return clean(tolist())
    return value


def to_json(message):
    """One message as a line of JSON: ASCII only (so a line never breaks inside a character), and no NaN."""
    return json.dumps(clean(message), ensure_ascii=True, allow_nan=False, separators=(",", ":"), default=str)


def new_display_id():
    return uuid.uuid4().hex[:12]


class Channel:
    """Where messages go and events come from. A subclass says how; callbacks and their queue are the same for both."""

    def __init__(self):
        self._events = queue.Queue()
        self._callbacks = {}  # display id -> {kind: [callback]}
        self._lock = threading.RLock()

    # ---- out

    def send(self, message):
        raise NotImplementedError

    def context(self):
        """What a message sent now belongs to (a notebook cell), to send later with the same (a throttled update)."""
        return None

    def send_in(self, context, message):
        self.send(message)

    # ---- in

    def listen(self, display_id, kind, callback):
        with self._lock:
            kinds = self._callbacks.setdefault(display_id, {})
            first_of_kind = kind not in kinds
            kinds.setdefault(kind, []).append(callback)
        if first_of_kind:
            self.send({"type": "subscribe", "display_id": display_id, "events": [kind]})
        self.connect()

    def stop_listening(self, display_id, kind=None):
        with self._lock:
            kinds = self._callbacks.get(display_id)
            if not kinds:
                return
            gone = list(kinds) if kind is None else [kind] if kind in kinds else []
            for k in gone:
                del kinds[k]
            if not kinds:
                del self._callbacks[display_id]
        if gone:
            self.send({"type": "unsubscribe", "display_id": display_id, "events": gone})

    def connect(self):
        """Makes sure events can come in (a program that is run connects to the studio now)."""

    def post(self, message):
        """An event message from the studio: {type: "event", id, display_id, event: {...}}."""
        self._events.put(message)

    def run_pending(self):
        """Runs the callbacks of every event that has come, here and now; how many events there were."""
        count = 0
        while True:
            try:
                message = self._events.get_nowait()
            except queue.Empty:
                return count
            self.dispatch(message)
            count += 1

    def wait(self, timeout=None):
        """Runs callbacks as their events come, until Stop (KeyboardInterrupt) or the time is up."""
        self.connect()
        deadline = None if timeout is None else time.monotonic() + timeout
        while True:
            left = None if deadline is None else deadline - time.monotonic()
            if left is not None and left <= 0:
                return
            try:
                # Short waits, so Ctrl+C (Stop) is noticed at once on every platform.
                message = self._events.get(timeout=0.2 if left is None else min(0.2, left))
            except queue.Empty:
                continue
            self.dispatch(message)

    def dispatch(self, message):
        event = message.get("event") or {}
        with self._lock:
            callbacks = list(self._callbacks.get(message.get("display_id"), {}).get(event.get("event"), []))
        for callback in callbacks:
            self.run_callback(message, callback, Event(event))

    def run_callback(self, message, callback, event):
        try:
            callback(event)
        except KeyboardInterrupt:
            raise
        except Exception as error:  # a failing callback says so; the program goes on
            print(f"⚠️ The {event.kind} callback failed: {type(error).__name__}: {error}", file=sys.stderr)


class Event:
    """What the user did to a visual: event.kind ("click", "select", "step"), and what it was done to, named as the spec
    names it: event.target ({"series": 0, "index": 2, ...}, {"node": "a"}, {"cell": [1, 2]}, {"item": 3}), event.targets
    for a selection, event.index for a step, event.modifiers (["ctrl", "shift", ...]). event["target"] works too."""

    def __init__(self, payload):
        self._payload = dict(payload)
        self.kind = self._payload.get("event")
        self.target = self._payload.get("target")
        self.targets = self._payload.get("targets")
        self.index = self._payload.get("index")
        self.modifiers = self._payload.get("modifiers", [])

    def __getitem__(self, key):
        return self._payload[key]

    def get(self, key, default=None):
        return self._payload.get(key, default)

    def __repr__(self):
        return f"Event({self._payload!r})"


class LineChannel(Channel):
    """A program that is run: messages on stdout after the marker; events over the studio's loopback socket."""

    def __init__(self, stream=None, environment=None):
        super().__init__()
        self._stream = stream
        environment = os.environ if environment is None else environment
        self._address = environment.get("FRY_EVENTS")
        self._token = environment.get("FRY_EVENTS_TOKEN")
        self._socket = None
        self._write_lock = threading.Lock()

    def send(self, message):
        stream = self._stream or sys.stdout
        line = f"{DISPLAY_MARKER} {to_json(message)}\n"
        with self._write_lock:
            stream.write(line)
            stream.flush()

    def connect(self):
        with self._lock:
            if self._socket is not None or not self._address or not self._token:
                return
            host, _, port = self._address.rpartition(":")
            try:
                connection = socket.create_connection((host, int(port)), timeout=5)
                connection.settimeout(None)
                connection.sendall((json.dumps({"type": "hello", "token": self._token}) + "\n").encode("ascii"))
            except (OSError, ValueError) as error:
                print(f"⚠️ Events from the studio can't reach this program: {error}", file=sys.stderr)
                self._address = None  # don't try again for every callback
                return
            self._socket = connection
        threading.Thread(target=self._read, args=(connection,), name="fry-events", daemon=True).start()

    def _read(self, connection):
        try:
            with connection.makefile("r", encoding="utf-8", newline="\n") as lines:
                for line in lines:
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        message = json.loads(line)
                    except ValueError:
                        continue
                    if message.get("type") == "event":
                        self.post(message)
        except OSError:
            pass  # the studio closed it: the program's visuals are disconnected


class KernelChannel(Channel):
    """A notebook cell: the kernel's own send, and the running cell's id on every message (the studio routes by it)."""

    def __init__(self, send, current_id, run_as):
        super().__init__()
        self._send = send
        self._current_id = current_id
        self._run_as = run_as

    def send(self, message):
        self.send_in(self.context(), message)

    def context(self):
        return self._current_id()

    def send_in(self, context, message):
        self._send(dict(message, id=context))

    def run_callback(self, message, callback, event):
        # Its output belongs to the cell that showed the visual: the event's id routes there.
        self._run_as(message.get("id"), lambda: Channel.run_callback(self, message, callback, event))


_channel = None
_channel_lock = threading.Lock()


def channel():
    """The channel of this process: the kernel's once it has set one (use), otherwise stdout's."""
    global _channel
    with _channel_lock:
        if _channel is None:
            _channel = LineChannel()
        return _channel


def use(new_channel):
    """Makes new_channel the one every display uses (the kernel calls this at startup)."""
    global _channel
    with _channel_lock:
        _channel = new_channel


class Throttle:
    """Sends a handle's updates at most MIN_UPDATE_INTERVAL apart, the latest winning; one that has to wait is sent by
    a timer, so a program that stops updating still gets its last one drawn."""

    def __init__(self, send):
        self._send = send
        self._lock = threading.Lock()
        self._last = 0.0
        self._pending = None
        self._timer = None

    def submit(self, message_factory):
        with self._lock:
            now = time.monotonic()
            wait = self._last + MIN_UPDATE_INTERVAL - now
            if wait <= 0 and self._timer is None:
                self._last = now
                self._pending = None
                send_now = message_factory
            else:
                self._pending = message_factory
                send_now = None
                if self._timer is None:
                    self._timer = threading.Timer(max(wait, 0.001), self._flush_later)
                    self._timer.daemon = True
                    self._timer.start()
        if send_now is not None:
            self._send(send_now())

    def flush(self):
        """Sends a waiting update now (before the program ends)."""
        with self._lock:
            pending, self._pending = self._pending, None
            if self._timer is not None:
                self._timer.cancel()
                self._timer = None
            if pending is not None:
                self._last = time.monotonic()
        if pending is not None:
            self._send(pending())

    def _flush_later(self):
        with self._lock:
            self._timer = None
        self.flush()


def process_events():
    """Runs the callbacks whose events have come, here and now, and says how many events there were."""
    return channel().run_pending()


def wait(timeout=None):
    """Waits for events on this program's visuals and runs their callbacks as they come, until Stop (or timeout
    seconds): for a program that has shown an interactive visual and has nothing else to do."""
    channel().wait(timeout)
