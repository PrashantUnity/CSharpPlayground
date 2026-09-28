"""The Python kernel of FryPDF's C# Code Studio notebooks.

Started by the studio with the user's own Python (so every installed package works), it runs notebook cells one
after another in one namespace, as Jupyter's IPython kernel does, and talks to the studio over its stdin/stdout pipes
in the Fry kernel protocol: one JSON object per line (see docs/kernel-protocol.md in the plugin's repository).

Only the standard library is used. What cells print, and anything C code or child processes write, reaches the studio
as output; the protocol keeps pipes of its own, so nothing a cell does can garble it.
"""

import ast
import asyncio
import builtins
import faulthandler
import importlib
import importlib.util
import inspect
import io
import json
import keyword
import linecache
import os
import queue
import re
import reprlib
import signal
import site
import subprocess
import sys
import threading
import time
import traceback
import types

KERNEL_DIR = os.path.dirname(os.path.abspath(__file__))
KERNEL_FILE = os.path.abspath(__file__)
KERNEL_PID = os.getpid()

# The kernel's own helper, imported before sys.path points at the notebook's folder (a user's display.py can't shadow it).
import fry_display  # noqa: E402

_INTERRUPTED = object()
_HIDDEN_NAMES = {"display", "__fry__", "In", "Out", "exit", "quit", "get_ipython"}


class _State:
    def __init__(self):
        self.current_id = None
        self.busy = False
        self.execution_count = 0
        self.cell_names = set()


_state = _State()
_END_OF_INPUT = object()
_EXIT_GRACE_SECONDS = 2.0
_write_lock = threading.Lock()
_requests = queue.Queue()
_input_replies = queue.Queue()
_proto_in = None
_proto_out = None
_proto_fds = ()
_main_thread_id = threading.get_ident()
_CAN_MASK_SIGNALS = hasattr(signal, "pthread_sigmask")


# ---------------------------------------------------------------------------------------------------------- protocol

def send(message):
    """Writes one protocol message: a line of JSON, escaped to ASCII, never interleaved with another."""
    line = json.dumps(message, ensure_ascii=True, default=str) + "\n"
    with _write_lock:
        if _CAN_MASK_SIGNALS and threading.get_ident() == _main_thread_id:
            # An interrupt landing mid-write would leave half a line in the pipe: it waits until the line is out.
            previous = signal.pthread_sigmask(signal.SIG_BLOCK, {signal.SIGINT})
            try:
                _proto_out.write(line)
                _proto_out.flush()
            finally:
                signal.pthread_sigmask(signal.SIG_SETMASK, previous)
        else:
            _proto_out.write(line)
            _proto_out.flush()


def _setup_channels():
    """Keeps the pipes the studio started the kernel with for the protocol, and gives fds 0 and 1 to everything else:
    fd 1 (C printf, child processes) goes where fd 2 goes, which the studio shows as output; fd 0 reads nothing."""
    global _proto_in, _proto_out, _proto_fds
    out_fd = os.dup(1)
    in_fd = os.dup(0)
    os.dup2(2, 1)
    devnull = os.open(os.devnull, os.O_RDONLY)
    os.dup2(devnull, 0)
    os.close(devnull)
    if sys.platform == "win32":
        _point_windows_std_handles_at_the_new_fds()
    _proto_fds = (out_fd, in_fd)
    _proto_out = os.fdopen(out_fd, "w", encoding="ascii", newline="\n")
    _proto_in = os.fdopen(in_fd, "r", encoding="utf-8", errors="replace", newline=None)


def _point_windows_std_handles_at_the_new_fds():
    # Child processes inherit Windows' standard handles, not C's fds: without this, subprocess.run() output (and
    # input) would still use the protocol's pipes.
    try:
        import ctypes
        import msvcrt
        kernel32 = ctypes.windll.kernel32
        kernel32.SetStdHandle(-11, msvcrt.get_osfhandle(1))  # STD_OUTPUT_HANDLE
        kernel32.SetStdHandle(-10, msvcrt.get_osfhandle(0))  # STD_INPUT_HANDLE
    except Exception:
        pass


def _after_fork_in_child():
    # A forked child (multiprocessing on Linux) mustn't speak the protocol: its prints go to fd 1, i.e. output.
    for fd in _proto_fds:
        try:
            os.close(fd)
        except OSError:
            pass
    sys.stdout = open(1, "w", closefd=False, buffering=1, encoding="utf-8", errors="replace")
    sys.stderr = open(2, "w", closefd=False, buffering=1, encoding="utf-8", errors="replace")


def _reader():
    """The protocol's input, on its own thread: interrupts and input replies act at once, the rest waits its turn."""
    try:
        for line in _proto_in:
            line = line.strip()
            if not line:
                continue
            try:
                message = json.loads(line)
            except ValueError:
                continue
            kind = message.get("type")
            if kind == "interrupt":
                _interrupt()
            elif kind == "input_reply":
                _input_replies.put(message.get("value"))
            elif kind == "shutdown":
                _exit_now()
            else:
                _requests.put(message)
    except Exception:
        pass
    # End of input: no more requests will come. Those already here are answered (input piped in by hand works), then
    # the kernel and what it started go too. A cell still busy after a moment (the studio went away mid-run) doesn't
    # keep it alive.
    _requests.put(_END_OF_INPUT)
    timer = threading.Timer(_EXIT_GRACE_SECONDS, _exit_now)
    timer.daemon = True
    timer.start()


def _interrupt():
    if not _state.busy:
        return
    _input_replies.put(_INTERRUPTED)
    if hasattr(signal, "pthread_kill"):
        # Wakes the main thread from a blocking call (time.sleep, a socket read) too.
        signal.pthread_kill(_main_thread_id, signal.SIGINT)
    else:
        # Windows: a real SIGINT runs Python's handler, which also sets the event time.sleep waits on;
        # _thread.interrupt_main() only schedules the handler, so a sleeping cell wouldn't notice.
        signal.raise_signal(signal.SIGINT)


def _exit_now():
    for stream in (_stdout, _stderr):
        try:
            stream.flush()
        except Exception:
            pass
    if hasattr(os, "killpg"):
        try:
            os.killpg(os.getpgrp(), signal.SIGKILL)
        except OSError:
            pass
    os._exit(0)


# ------------------------------------------------------------------------------------------------------------ output

class FryStream(io.TextIOBase):
    """sys.stdout / sys.stderr: text is batched (a line, 8 KB, or 50 ms) into protocol "stream" messages. One object
    for the kernel's life, since logging handlers, warnings and progress bars keep a reference to it."""

    def __init__(self, name):
        super().__init__()
        self.name = name
        self._parts = []
        self._size = 0
        self._lock = threading.RLock()

    encoding = "utf-8"
    errors = "replace"

    def writable(self):
        return True

    def readable(self):
        return False

    def isatty(self):
        return False

    def fileno(self):
        # Code that writes to the fd directly lands where the studio reads output.
        return 2

    @property
    def closed(self):
        return False

    @property
    def buffer(self):
        return _BytesToText(self)

    def reconfigure(self, *args, **kwargs):
        pass

    def write(self, text):
        if not isinstance(text, str):
            raise TypeError(f"write() argument must be str, not {type(text).__name__}")
        if not text:
            return 0
        if os.getpid() != KERNEL_PID:
            os.write(2, text.encode("utf-8", "replace"))
            return len(text)
        with self._lock:
            self._parts.append(text)
            self._size += len(text)
            if "\n" in text or self._size >= 8192:
                self.flush()
        return len(text)

    def flush(self):
        with self._lock:
            if not self._parts:
                return
            text = "".join(self._parts)
            self._parts.clear()
            self._size = 0
        send({"type": "stream", "id": _state.current_id, "name": self.name, "text": text})


class _BytesToText:
    """sys.stdout.buffer: bytes written to it are decoded as UTF-8 onto the text stream."""

    def __init__(self, stream):
        self._stream = stream

    def write(self, data):
        self._stream.write(bytes(data).decode("utf-8", "replace"))
        return len(data)

    def flush(self):
        self._stream.flush()

    def isatty(self):
        return False

    def fileno(self):
        return 2


class FryStdin(io.TextIOBase):
    """sys.stdin: each line is asked of the user through the studio (like input())."""

    encoding = "utf-8"

    def readable(self):
        return True

    def isatty(self):
        return False

    def fileno(self):
        raise io.UnsupportedOperation("the notebook's stdin has no file descriptor")

    def readline(self, size=-1):
        try:
            return _ask("") + "\n"
        except EOFError:
            return ""

    def read(self, size=-1):
        return self.readline()


def _ask(prompt, password=False):
    _stdout.flush()
    _stderr.flush()
    send({"type": "input_request", "id": _state.current_id, "prompt": str(prompt), "password": bool(password)})
    while True:
        try:
            value = _input_replies.get(timeout=0.1)
        except queue.Empty:
            continue
        if value is _INTERRUPTED:
            raise KeyboardInterrupt
        if value is None:
            raise EOFError("EOF when reading a line")
        return str(value)


def _input(prompt=""):
    return _ask(prompt)


def _getpass(prompt="Password: ", stream=None):
    return _ask(prompt, password=True)


def _publish(data, metadata):
    _stdout.flush()
    _stderr.flush()
    send({"type": "display", "id": _state.current_id, "data": data, "metadata": metadata or {}})


def _flusher():
    while True:
        time.sleep(0.05)
        try:
            _stdout.flush()
            _stderr.flush()
        except Exception:
            pass


_stdout = FryStream("stdout")
_stderr = FryStream("stderr")


# ------------------------------------------------------------------------------------------------------- matplotlib

class _MatplotlibBackendHook:
    """Points matplotlib at the inline backend (fry_matplotlib: figures become PNG output) the first time it's
    imported. Done in this process only, not with MPLBACKEND, so a Python the cell starts keeps its usual backend."""

    def find_spec(self, name, path, target=None):
        if name != "matplotlib":
            return None
        sys.meta_path.remove(self)
        spec = importlib.util.find_spec(name)
        if spec is None or spec.loader is None:
            return spec
        original = spec.loader.exec_module

        def exec_module(module):
            original(module)
            try:
                module.rcParams["backend"] = "module://fry_matplotlib"
            except Exception:
                pass

        spec.loader.exec_module = exec_module
        return spec


def _flush_figures():
    if "matplotlib.pyplot" not in sys.modules:
        return
    backend = sys.modules.get("fry_matplotlib")
    if backend is not None:
        try:
            backend.flush_figures()
        except Exception as error:
            _stderr.write(f"Couldn't draw a figure: {error}\n")


# ---------------------------------------------------------------------------------------------------------- running

class _FryHelpers:
    """`__fry__`: what a cell's !command and %magic lines become."""

    def shell(self, command):
        process = subprocess.Popen(command, shell=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                   stdin=subprocess.DEVNULL, cwd=os.getcwd())
        for chunk in iter(lambda: process.stdout.read1(4096) if hasattr(process.stdout, "read1") else process.stdout.read(4096), b""):
            _stdout.write(chunk.decode("utf-8", "replace"))
        return process.wait()

    def pip(self, arguments):
        # A %pip below the top of a cell (the studio runs the ones at the top itself, choosing where to install).
        return self.shell(f'"{sys.executable}" -m pip {arguments} --disable-pip-version-check')

    def magic(self, line):
        name = line.split()[0]
        raise RuntimeError(f"{name} isn't supported here: the studio understands !shell commands and %pip.")


def _transform_magics(source):
    """IPython's !command and %magic lines, as Python. Only tried when the cell doesn't parse as it is: no Python
    statement starts with ! or %."""
    changed = False
    lines = []
    for line in source.splitlines(True):
        stripped = line.lstrip()
        indent = line[:len(line) - len(stripped)]
        ending = "\n" if line.endswith("\n") else ""
        body = stripped.rstrip("\r\n")
        if body.startswith("!"):
            lines.append(f"{indent}__fry__.shell({body[1:]!r}){ending}")
            changed = True
        elif body.startswith("%pip"):
            lines.append(f"{indent}__fry__.pip({body[4:].strip()!r}){ending}")
            changed = True
        elif body.startswith("%matplotlib"):
            lines.append(f"{indent}pass{ending}")
            changed = True
        elif body.startswith("%"):
            lines.append(f"{indent}__fry__.magic({body!r}){ending}")
            changed = True
        else:
            lines.append(line)
    return "".join(lines) if changed else None


def _ends_with_semicolon(source):
    for line in reversed(source.splitlines()):
        code = line.split("#", 1)[0].strip()
        if code:
            return code.endswith(";")
    return False


def _run_code(code, namespace):
    result = eval(code, namespace)
    if inspect.iscoroutine(result):
        task = _loop.create_task(result)
        try:
            return _loop.run_until_complete(task)
        except KeyboardInterrupt:
            task.cancel()
            try:
                _loop.run_until_complete(task)
            except BaseException:
                pass
            raise
    return result


def _run_cell(source, label):
    _state.execution_count += 1
    # Tracebacks name the code as the notebook shows the cell ("<Cell [3]>"). Each run gets a name of its own, so a
    # traceback through a function defined in an earlier run still shows that run's source.
    filename = f"<Cell {label}>"
    if filename in _state.cell_names:
        filename = f"<Cell {label} #{_state.execution_count}>"
    _state.cell_names.add(filename)
    try:
        tree = ast.parse(source, filename=filename, mode="exec")
    except SyntaxError:
        transformed = _transform_magics(source)
        if transformed is None:
            linecache.cache[filename] = (len(source), None, source.splitlines(True), filename)
            raise
        source = transformed
        tree = ast.parse(source, filename=filename, mode="exec")
    linecache.cache[filename] = (len(source), None, source.splitlines(True), filename)

    flags = ast.PyCF_ALLOW_TOP_LEVEL_AWAIT
    last = None
    if tree.body and isinstance(tree.body[-1], ast.Expr) and not _ends_with_semicolon(source):
        last = tree.body.pop()
    if tree.body:
        _run_code(compile(ast.Module(body=tree.body, type_ignores=[]), filename, "exec", flags=flags), _namespace)
    if last is not None:
        value = _run_code(compile(ast.Expression(body=last.value), filename, "eval", flags=flags), _namespace)
        if value is not None:
            _namespace["_"] = value
            fry_display.publish_object(value)


def _report_error(error_type, error, tb):
    # The kernel's own frames aren't the user's business.
    while tb is not None and os.path.abspath(tb.tb_frame.f_code.co_filename) == KERNEL_FILE:
        tb = tb.tb_next
    text = "".join(traceback.format_exception(error_type, error, tb))
    line = None
    for frame in traceback.extract_tb(tb):
        if frame.filename.startswith("<Cell "):
            line = frame.lineno
    if isinstance(error, SyntaxError) and (error.filename or "").startswith("<Cell "):
        line = error.lineno
    missing_module = None
    if isinstance(error, ModuleNotFoundError) and error.name:
        missing_module = error.name.split(".")[0]
    missing_name = None
    if isinstance(error, NameError):
        missing_name = getattr(error, "name", None)
        if not missing_name:
            match = re.search(r"name '([^']+)' is not defined", str(error))
            missing_name = match.group(1) if match else None
    send({
        "type": "error",
        "id": _state.current_id,
        "ename": error_type.__name__,
        "evalue": str(error),
        "traceback": text,
        "line": line,
        "missingModule": missing_module,
        "missingName": missing_name,
    })


def _execute(message):
    _state.current_id = message.get("id")
    _state.busy = True
    status = "ok"
    try:
        importlib.invalidate_caches()
        _run_cell(message.get("code") or "", message.get("cell") or f"[{_state.execution_count + 1}]")
    except KeyboardInterrupt:
        status = "interrupted"
    except SystemExit as error:
        # exit() in a notebook ends the cell, not the kernel.
        status = "error"
        send({"type": "error", "id": _state.current_id, "ename": "SystemExit", "evalue": str(error.code),
              "traceback": f"SystemExit: {error.code}\n", "line": None})
    except BaseException:
        status = "error"
        _report_error(*sys.exc_info())
    finally:
        try:
            _flush_figures()
            _stdout.flush()
            _stderr.flush()
        except KeyboardInterrupt:
            status = "interrupted"
        _state.busy = False
        while not _input_replies.empty():
            try:
                _input_replies.get_nowait()
            except queue.Empty:
                break
        send({"type": "reply", "id": message.get("id"), "status": status, "executionCount": _state.execution_count})


# ------------------------------------------------------------------------------------------------ variables, sharing

_short = reprlib.Repr()
_short.maxstring = 80
_short.maxother = 80
_short.maxlist = 8
_short.maxdict = 6


def _describe(value):
    module = type(value).__module__.split(".")[0]
    if module in ("numpy", "pandas") and hasattr(value, "shape"):
        return f"{type(value).__name__} {tuple(value.shape)}"
    try:
        return _short.repr(value)
    except Exception:
        return f"<{type(value).__name__}>"


def _kind(value):
    if isinstance(value, type):
        return "Class"
    if isinstance(value, (types.FunctionType, types.BuiltinFunctionType, types.MethodType)):
        return "Function"
    if isinstance(value, (bool, int, float, complex, str, bytes)) or value is None:
        return "Primitive"
    if isinstance(value, (list, tuple, dict, set, frozenset, range)) or type(value).__module__.split(".")[0] in ("numpy", "pandas"):
        return "Collection"
    return "Object"


def _variables():
    found = []
    for name, value in list(_namespace.items()):
        if name.startswith("_") or name in _HIDDEN_NAMES or isinstance(value, types.ModuleType):
            continue
        found.append({"name": name, "type": type(value).__name__, "value": _describe(value), "kind": _kind(value)})
        if len(found) >= 200:
            break
    return found


def _handle_request(message):
    kind = message.get("type")
    request_id = message.get("id")
    try:
        if kind == "execute":
            _execute(message)
            return
        if kind == "variables":
            send({"type": "reply", "id": request_id, "status": "ok", "variables": _variables()})
        elif kind == "get_value":
            name = message.get("name", "")
            if name not in _namespace:
                raise KeyError(f"Python has no variable named '{name}'. Run the cell that defines it first.")
            payload = json.dumps(fry_display.to_jsonable(_namespace[name]), allow_nan=False)
            send({"type": "reply", "id": request_id, "status": "ok", "json": payload})
        elif kind == "set_value":
            name = message.get("name", "")
            if not name.isidentifier() or keyword.iskeyword(name):
                raise ValueError(f"'{name}' can't be a Python variable name.")
            _namespace[name] = json.loads(message.get("json") or "null")
            send({"type": "reply", "id": request_id, "status": "ok"})
        elif kind == "add_search_path":
            site.addsitedir(message.get("path", ""))
            importlib.invalidate_caches()
            send({"type": "reply", "id": request_id, "status": "ok"})
        else:
            send({"type": "reply", "id": request_id, "status": "error", "message": f"Unknown request '{kind}'."})
    except KeyboardInterrupt:
        send({"type": "reply", "id": request_id, "status": "interrupted"})
    except Exception as error:
        message_text = error.args[0] if isinstance(error, KeyError) and error.args else str(error)
        send({"type": "reply", "id": request_id, "status": "error", "message": message_text})


# ---------------------------------------------------------------------------------------------------------- startup

def main():
    global _namespace, _loop
    arguments = sys.argv[1:]
    cwd = arguments[arguments.index("--cwd") + 1] if "--cwd" in arguments else os.getcwd()

    signal.signal(signal.SIGINT, signal.default_int_handler)
    if hasattr(os, "setsid"):
        try:
            os.setsid()  # its own process group, so it can take what cells start down with it
        except OSError:
            pass

    _setup_channels()
    if hasattr(os, "register_at_fork"):
        os.register_at_fork(after_in_child=_after_fork_in_child)
    faulthandler.enable(file=sys.__stderr__)

    # A __main__ with no __file__: multiprocessing's spawn doesn't try to run the kernel again in each child.
    main_module = types.ModuleType("__main__")
    main_module.__dict__.update({"__builtins__": builtins, "__spec__": None, "__loader__": None, "__package__": None})
    sys.modules["__main__"] = main_module
    _namespace = main_module.__dict__
    _namespace["display"] = fry_display.display
    _namespace["__fry__"] = _FryHelpers()

    _loop = asyncio.new_event_loop()
    asyncio.set_event_loop(_loop)

    sys.stdout = _stdout
    sys.stderr = _stderr
    sys.stdin = FryStdin()
    builtins.input = _input
    try:
        import getpass
        getpass.getpass = _getpass
    except Exception:
        pass
    fry_display.set_publisher(_publish)

    # The notebook's folder first, as `python script.py` would have the script's; the kernel's own folder last, for
    # fry_matplotlib.
    sys.argv = [""]
    sys.path = [p for p in sys.path if os.path.abspath(p or ".") != KERNEL_DIR]
    sys.path.insert(0, cwd)
    sys.path.append(KERNEL_DIR)
    try:
        os.chdir(cwd)
    except OSError:
        pass
    if "matplotlib" in sys.modules:
        try:
            sys.modules["matplotlib"].rcParams["backend"] = "module://fry_matplotlib"
        except Exception:
            pass
    else:
        sys.meta_path.insert(0, _MatplotlibBackendHook())

    threading.Thread(target=_reader, name="fry-protocol-reader", daemon=True).start()
    threading.Thread(target=_flusher, name="fry-output-flusher", daemon=True).start()
    send({"type": "ready", "language": "python", "version": "%d.%d.%d" % sys.version_info[:3], "executable": sys.executable})

    while True:
        try:
            message = _requests.get()
            if message is _END_OF_INPUT:
                _exit_now()
            _handle_request(message)
        except KeyboardInterrupt:
            # An interrupt that arrived just after a cell finished.
            continue


_namespace = {}
_loop = None

if __name__ == "__main__":
    main()
