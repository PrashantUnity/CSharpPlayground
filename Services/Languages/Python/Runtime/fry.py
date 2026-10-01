"""The FryPDF studio's display API for Python, the same in a program that is run and in a notebook cell.

    from fry import Display                     # or the module functions: fry.line_chart(...)
    chart = Display.line_chart([3, 1, 4], title="Sales", y_title="EUR")
    chart.on_click(lambda e: print("clicked", e.target))
    chart.update(title="Sales (updated)")
    Display.wait()                              # a program that is run: run callbacks until Stop

Every visual call returns a DisplayHandle: .update(...), .on(event, fn), .on_click/.on_select/.on_step, .off(), .close().
The names, parameters and data conventions are those of every language (docs/visual-protocol.md).
"""

import base64
import copy
import io
import os
import sys

import fry_channel
import fry_specs
from fry_channel import Event, process_events, wait  # noqa: F401  (part of the API)

TABLE_MIME = "application/vnd.fry.table+json"

__all__ = [
    "Display", "DisplayHandle", "Recorder", "CanvasBuilder", "Event", "show", "dump", "display", "table", "html", "markdown", "image", "json",
    "chart", "line_chart", "area_chart", "bar_chart", "scatter_chart", "pie_chart", "donut_chart", "histogram",
    "plot3d", "scatter3d", "trajectory3d", "surface3d", "graph3d", "voxel_bar3d", "plot3d_surface", "voxel_bars",
    "visualize", "matrix", "islands", "tree", "graph", "linked_list", "array", "bars", "board", "canvas", "recorder",
    "process_events", "wait",
]


# ---------------------------------------------------------------------------------------------------------- handles

class DisplayHandle:
    """A shown visual: update it where it is, and hear what the user does to it."""

    _fry_shown = True  # a cell ending with a handle doesn't show it again

    def __init__(self, mime, spec, display_id):
        self.mime = mime
        self.spec = spec
        self.display_id = display_id
        self._closed = False
        self._throttle = fry_channel.Throttle(lambda message: fry_channel.channel().send_in(message[0], message[1]))

    def update(self, spec=None, **options):
        """Redraws the visual: with a new spec (a dict, or a function that changes a copy of the current one), and/or
        the same settings the call that showed it takes (title=..., color=...). Sent at most 30 times a second."""
        if self._closed:
            raise RuntimeError("This visual was closed: it can't be updated.")
        if callable(spec):
            changed = copy.deepcopy(self.spec)
            spec = spec(changed) or changed
        new = copy.deepcopy(spec if spec is not None else self.spec)
        _apply(new, options)
        self.spec = new
        context = fry_channel.channel().context()
        self._throttle.submit(lambda: (context, _message("update_display", self.mime, self.spec, self.display_id)))
        return self

    def on(self, event, callback):
        """Calls callback(event) when the user clicks ("click"), selects ("select") or steps ("step") in the visual."""
        if event not in fry_channel.EVENT_KINDS:
            raise ValueError(f"There is no {event!r} event: use one of {', '.join(fry_channel.EVENT_KINDS)}.")
        if not callable(callback):
            raise TypeError("The callback must be a function taking the event.")
        fry_channel.channel().listen(self.display_id, event, callback)
        return self

    def on_click(self, callback):
        return self.on("click", callback)

    def on_select(self, callback):
        return self.on("select", callback)

    def on_step(self, callback):
        return self.on("step", callback)

    def off(self, event=None):
        """Stops calling the callbacks of event (all of them when left out)."""
        fry_channel.channel().stop_listening(self.display_id, event)
        return self

    def close(self):
        """Sends any waiting update, then stops listening: the visual stays as it is."""
        self._throttle.flush()
        self.off()
        self._closed = True

    def flush(self):
        self._throttle.flush()

    def __repr__(self):
        return f"<{_describe(self.mime, self.spec)}>"


def _message(kind, mime, spec, display_id):
    return {"type": kind, "data": {mime: spec, "text/plain": _describe(mime, spec)}, "metadata": {},
            "transient": {"display_id": display_id}}


def _describe(mime, spec):
    family = {fry_specs.CHART_MIME: "chart", fry_specs.PLOT3D_MIME: "3D plot", fry_specs.VISUALIZER_MIME: "visualizer"}.get(mime, "visual")
    title = spec.get("title")
    return f"{spec.get('kind', '')} {family}".strip() + (f": {title}" if title else "")


# Settings every call takes, and where they go in the spec.
_SETTINGS = {
    "title": ("title",), "subtitle": ("subtitle",), "width": ("width",), "height": ("height",),
    "color": ("color",), "grid": ("grid",), "show_grid": ("grid",), "show_points": ("showPoints",), "show_stats": ("showStats",),
    "x_title": ("xAxis", "title"), "y_title": ("yAxis", "title"), "z_title": ("zAxis", "title"),
    "x_min": ("xAxis", "min"), "x_max": ("xAxis", "max"), "y_min": ("yAxis", "min"), "y_max": ("yAxis", "max"),
    "show_legend": ("legend", "show"), "bins": ("bins",), "color_map": ("colorMap",), "auto_rotate": ("autoRotate",),
    "show_axes": ("showAxes",), "summary": ("summary",), "show_coordinates": ("showCoordinates",),
    "show_values": ("showValues",), "cell_size": ("cellSize",),
}

_COLOR_MAPS = {"viridis": "viridis", "plasma": "plasma", "coolwarm": "coolWarm", "turbo": "turbo", "rainbow": "rainbow", "ocean": "ocean", "fire": "fire"}


def _apply(spec, options):
    configure = options.pop("configure", None)
    for name, value in options.items():
        if value is None:
            continue
        if name not in _SETTINGS:
            raise TypeError(f"There is no setting {name!r}: use one of {', '.join(sorted(_SETTINGS))} or configure=.")
        if name == "color_map":
            value = _COLOR_MAPS.get(str(value).lower().replace("_", ""), value)
        path = _SETTINGS[name]
        target = spec
        for key in path[:-1]:
            target = target.setdefault(key, {})
        target[path[-1]] = value
    if configure is not None:
        configure(spec)


def _show(mime, spec, options):
    """Shows a spec and returns its handle."""
    _apply(spec, options)
    handle = DisplayHandle(mime, spec, fry_channel.new_display_id())
    fry_channel.channel().send(_message("display", mime, spec, handle.display_id))
    return handle


# ------------------------------------------------------------------------------------------------------------ core

def show(obj, title=None, **options):
    """Shows anything: a spec bundle ({mime: spec}, as Copy spec gives), a Recorder, or any value as display does."""
    if isinstance(obj, Recorder):
        return obj.show()
    if isinstance(obj, dict) and len(obj) >= 1 and all(isinstance(k, str) and "/" in k for k in obj):
        for mime in (fry_specs.CHART_MIME, fry_specs.PLOT3D_MIME, fry_specs.VISUALIZER_MIME):
            if mime in obj:
                return _show(mime, copy.deepcopy(obj[mime]), dict(options, title=title))
        _send_bundle(obj)
        return None
    display(obj)
    return None


def display(*objects, **kwargs):
    """Shows each object as a notebook would show it: its richest form (a DataFrame as a table, a figure as an image)."""
    import fry_display
    for obj in objects:
        if getattr(obj, "_fry_shown", False):
            continue
        data, metadata = fry_display.to_mime(obj)
        if data:
            _send_bundle(data, metadata)


def _send_bundle(data, metadata=None):
    fry_channel.channel().send({"type": "display", "data": data, "metadata": metadata or {}})


def dump(obj=None, title=None):
    """Shows a value as a table (a list, dict, DataFrame or object), and returns it, so it can end an expression."""
    table(obj, title=title)
    return obj


def table(obj=None, title=None):
    try:
        _send_bundle({TABLE_MIME: _table(obj, title), "text/plain": repr(obj)})
    except Exception as error:  # a value that can't be a table still shouldn't stop the program
        print(f"[Display] table error: {error}", file=sys.stderr)
    return obj


def html(content):
    _send_bundle({"text/html": str(content), "text/plain": "HTML"})


def markdown(content):
    _send_bundle({"text/markdown": str(content), "text/plain": str(content)})


def json(value, title=None):  # noqa: A001 (the API's name for it)
    import json as _json
    text = _json.dumps(fry_channel.clean(value), indent=2, default=str)
    _send_bundle({"application/json": text, "text/plain": text})


def image(image_or_path, format="PNG"):  # noqa: A002
    """Shows an image: a matplotlib figure, bytes, a file path, or base64 text."""
    if image_or_path is None:
        return
    mime = "image/jpeg" if str(format).upper() in ("JPG", "JPEG") else "image/png"
    if hasattr(image_or_path, "savefig"):
        buffer = io.BytesIO()
        image_or_path.savefig(buffer, format="png", bbox_inches="tight")
        raw, mime = buffer.getvalue(), "image/png"
    elif isinstance(image_or_path, (bytes, bytearray)):
        raw = bytes(image_or_path)
    elif isinstance(image_or_path, str) and os.path.isfile(image_or_path):
        with open(image_or_path, "rb") as f:
            raw = f.read()
        mime = "image/jpeg" if image_or_path.lower().endswith((".jpg", ".jpeg")) else "image/png"
    else:
        text = str(image_or_path).strip()
        _send_bundle({mime: text.split(",", 1)[1] if text.startswith("data:image") else text, "text/plain": "image"})
        return
    _send_bundle({mime: base64.b64encode(raw).decode("ascii"), "text/plain": "image"})


def _cell(value):
    if value is None or isinstance(value, (bool, int, str)):
        return value
    if isinstance(value, float):
        return fry_channel.clean(value)
    return str(value)


def _table(obj, title):
    obj = fry_specs._plain(obj)
    def result(t, columns, rows, numeric=None):
        return {"title": t, "columns": columns, "numeric": numeric or [False] * len(columns), "rows": rows[:1000],
                "totalRows": len(rows), "totalColumns": len(columns)}
    if obj is None:
        return result(title or "null", ["Value"], [["null"]])
    if isinstance(obj, (list, tuple, set)):
        items = list(obj)
        t = title or f"List[{len(items)}]"
        if items and isinstance(items[0], dict):
            keys = list(items[0].keys())
            rows = [[_cell(d.get(k)) for k in keys] for d in items if isinstance(d, dict)]
            return result(t, [str(k) for k in keys], rows, [all(isinstance(r[c], (int, float)) and not isinstance(r[c], bool) for r in rows) for c in range(len(keys))])
        if items and isinstance(items[0], (list, tuple)):
            width = max(len(r) for r in items if isinstance(r, (list, tuple)))
            rows = [[_cell(r[c]) if isinstance(r, (list, tuple)) and c < len(r) else None for c in range(width)] for r in items]
            return result(t, [f"[{c}]" for c in range(width)], rows)
        rows = [[i, _cell(v)] for i, v in enumerate(items)]
        return result(t, ["Index", "Value"], rows, [True, all(isinstance(v, (int, float)) and not isinstance(v, bool) for v in items)])
    if isinstance(obj, dict):
        return result(title or f"Dict[{len(obj)}]", ["Key", "Value"], [[str(k), _cell(v)] for k, v in obj.items()])
    if hasattr(obj, "__dict__"):
        rows = [[str(k), _cell(v)] for k, v in vars(obj).items() if not str(k).startswith("_")]
        return result(title or type(obj).__name__, ["Property", "Value"], rows)
    return result(title or type(obj).__name__, ["Value"], [[_cell(obj)]], [isinstance(obj, (int, float)) and not isinstance(obj, bool)])


# ----------------------------------------------------------------------------------------------------------- charts

def chart(data, title=None, kind="line", x=None, y=None, chart_type=None, chartType=None, **options):
    """A chart of data (kind: line, area, bar, scatter, pie, donut, histogram); x and y pick the fields of records."""
    resolved_kind = chart_type or chartType or kind
    mime, spec = fry_specs.chart_spec(data, resolved_kind, x=x, y=y)
    return _show(mime, spec, dict(options, title=title))


def line_chart(data, title=None, **options):
    return chart(data, title, "line", **options)


def area_chart(data, title=None, **options):
    return chart(data, title, "area", **options)


def bar_chart(data, title=None, **options):
    return chart(data, title, "bar", **options)


def scatter_chart(data, title=None, **options):
    return chart(data, title, "scatter", **options)


def pie_chart(data, title=None, **options):
    return chart(data, title, "pie", **options)


def donut_chart(data, title=None, **options):
    return chart(data, title, "donut", **options)


def histogram(samples, title=None, bins=None, **options):
    mime, spec = fry_specs.histogram_spec(samples, bins)
    return _show(mime, spec, dict(options, title=title))


# --------------------------------------------------------------------------------------------------------------- 3D

def plot3d(data, title=None, kind=None, plot_type=None, plotType=None, **options):
    """A 3D plot, z up (kind: scatter, trajectory, surface, wireframe, graph, voxelBar)."""
    resolved_kind = plot_type or plotType or kind
    mime, spec = fry_specs.plot3d_spec(data, resolved_kind)
    return _show(mime, spec, dict(options, title=title))


def scatter3d(data, title=None, **options):
    return plot3d(data, title, "scatter", **options)


def trajectory3d(data, title=None, **options):
    return plot3d(data, title, "trajectory", **options)


def voxel_bar3d(data, title=None, **options):
    return plot3d(data, title, "voxelBar", **options)


def voxel_bars(data, title=None, **options):
    return voxel_bar3d(data, title=title, **options)


def graph3d(data, title=None, **options):
    return plot3d(data, title, "graph", **options)


def surface3d(data, title=None, x=(-5, 5), y=(-5, 5), resolution=30, wireframe=False, **options):
    """A surface: a grid of heights (a row per y), or a function z = f(x, y) sampled over x and y."""
    if callable(data):
        mime, spec = fry_specs.surface_spec(data, x, y, resolution, wireframe)
    else:
        mime, spec = fry_specs.plot3d_spec(data, "wireframe" if wireframe else "surface")
    return _show(mime, spec, dict(options, title=title))


def plot3d_surface(data, title=None, x=(-5, 5), y=(-5, 5), x_range=None, y_range=None, resolution=30, res=None, colormap=None, color_map=None, wireframe=False, **options):
    """Plots a 3D surface z = f(x, y) over ranges with colormap and camera options."""
    opts = dict(options)
    if colormap or color_map:
        opts["color_map"] = colormap or color_map
    return surface3d(data, title=title, x=x_range or x, y=y_range or y, resolution=res or resolution, wireframe=wireframe, **opts)


# ------------------------------------------------------------------------------------------------------ visualizers

def _visual(built, title, options):
    mime, spec = built
    return _show(mime, spec, dict(options, title=title))


def visualize(data, title=None, **options):
    """A visualizer for whatever data is: a grid, a tree or list node, a graph, or a sequence."""
    return _visual(fry_specs.visualize_spec(data), title, options)


def matrix(grid, title=None, **options):
    return _visual(fry_specs.matrix_spec(grid), title, options)


def islands(grid, title=None, record_steps=True, four_directional=True, **options):
    """A grid of land (1) and water (0): the studio finds its islands and records the search."""
    return _visual(fry_specs.islands_spec(grid, record_steps, four_directional), title, options)


def tree(root, title=None, traversal=None, **options):
    """A tree (node objects or dicts with value and left/right or children, or a level-order list); traversal
    ("preorder", "inorder", "postorder", "levelorder") has the studio record that walk."""
    return _visual(fry_specs.tree_spec(root, traversal), title, options)


def graph(data, title=None, directed=True, **options):
    """A graph: an adjacency dict ({"a": ["b", "c"]}, or [("b", 4)] with weights) or an edge list."""
    return _visual(fry_specs.graph_spec(data, directed), title, options)


def linked_list(head, title=None, detect_cycle=False, **options):
    return _visual(fry_specs.linked_list_spec(head, detect_cycle), title, options)


def array(values, pointers=None, title=None, **options):
    """An array with named pointers into it: array(nums, {"lo": 0, "hi": 4})."""
    return _visual(fry_specs.array_spec(values, pointers), title, options)


def bars(values, title=None, **options):
    return _visual(fry_specs.bars_spec(values), title, options)


def board(grid, title=None, checkerboard=True, **options):
    return _visual(fry_specs.board_spec(grid, checkerboard), title, options)


class CanvasBuilder:
    def __init__(self, title=None, width=600, height=300, background=None, **options):
        self.title = title
        self.width = width
        self.height = height
        self.background = background
        self.options = options
        self.shapes = []

    def add_rect(self, x, y, width, height, label=None, fill=None, stroke=None, corner_radius=None, **opts):
        shape = {"type": "rect", "x": x, "y": y, "width": width, "height": height}
        if label is not None: shape["label"] = str(label)
        if fill is not None: shape["fill"] = fill
        if stroke is not None: shape["stroke"] = stroke
        if corner_radius is not None: shape["cornerRadius"] = corner_radius
        shape.update(opts)
        self.shapes.append(shape)
        return self

    def add_circle(self, cx, cy, radius=20, label=None, fill=None, stroke=None, **opts):
        shape = {"type": "circle", "cx": cx, "cy": cy, "radius": radius}
        if label is not None: shape["label"] = str(label)
        if fill is not None: shape["fill"] = fill
        if stroke is not None: shape["stroke"] = stroke
        shape.update(opts)
        self.shapes.append(shape)
        return self

    def add_arrow(self, x1, y1, x2, y2, label=None, color=None, stroke=None, **opts):
        shape = {"type": "arrow", "x1": x1, "y1": y1, "x2": x2, "y2": y2}
        if label is not None: shape["label"] = str(label)
        s = color or stroke
        if s is not None: shape["stroke"] = s
        shape.update(opts)
        self.shapes.append(shape)
        return self

    def add_line(self, x1, y1, x2, y2, color=None, stroke=None, **opts):
        shape = {"type": "line", "x1": x1, "y1": y1, "x2": x2, "y2": y2}
        s = color or stroke
        if s is not None: shape["stroke"] = s
        shape.update(opts)
        self.shapes.append(shape)
        return self

    def add_text(self, x, y, text="", font_size=14, color=None, **opts):
        shape = {"type": "text", "x": x, "y": y, "text": str(text), "fontSize": font_size}
        if color is not None: shape["color"] = color
        shape.update(opts)
        self.shapes.append(shape)
        return self

    def show(self):
        return _visual(fry_specs.canvas_spec(self.shapes, self.width, self.height, self.background), self.title, self.options)


def canvas(shapes_or_title=None, title=None, width=600, height=300, background=None, **options):
    """Free drawing: shapes as dicts or a CanvasBuilder for method chaining."""
    if isinstance(shapes_or_title, str) or shapes_or_title is None:
        resolved_title = shapes_or_title or title
        return CanvasBuilder(title=resolved_title, width=width, height=height, background=background, **options)
    return _visual(fry_specs.canvas_spec(shapes_or_title, width, height, background), title, options)


class Recorder:
    """An algorithm's steps on a visualizer: record(...) each step, then show(); steps recorded after it is shown
    appear as they come."""

    def __init__(self, kind, state=None, title=None):
        if kind not in fry_specs.STATE_OF_KIND:
            raise ValueError(f"A {kind!r} visualizer can't be recorded: use one of {', '.join(fry_specs.STATE_OF_KIND)}.")
        self.kind = kind
        self.spec = {"kind": kind, "state": fry_specs.STATE_OF_KIND[kind](state if state is not None else []), "steps": []}
        if title is not None:
            self.spec["title"] = title
        self._handle = None

    def step(self, description, state=None, changes=None, highlight=None, pointers=None, notes=None, line=None):
        """One step: a new state (data, as the kind takes it) or changes (as the spec writes them: {"cells": [...]}),
        or neither (the state stays); what it lights up, its pointers ({"i": 2}) and notes ({"sum": 12})."""
        step = {"description": str(description)}
        if state is not None:
            step["state"] = fry_specs.STATE_OF_KIND[self.kind](state)
        if changes is not None:
            step["changes"] = changes
        if highlight is not None:
            items = highlight if isinstance(highlight, list) else [highlight]
            step["highlight"] = [fry_specs.element(h) for h in items]
        if pointers:
            step["pointers"] = fry_specs.pointers(pointers)
        if notes:
            step["notes"] = {str(k): str(v) for k, v in dict(notes).items()}
        step["line"] = line if line is not None else _caller_line()
        self.spec["steps"].append(step)
        if self._handle is not None:
            self._handle.update(self.spec)
        return self

    def show(self, title=None, **options):
        if self._handle is None:
            self._handle = _show(fry_specs.VISUALIZER_MIME, self.spec, dict(options, title=title))
            self.spec = self._handle.spec
        return self._handle

    def _repr_mimebundle_(self, include=None, exclude=None):
        return {fry_specs.VISUALIZER_MIME: self.spec, "text/plain": _describe(fry_specs.VISUALIZER_MIME, self.spec)}


def _caller_line():
    frame = sys._getframe(2)
    return frame.f_lineno if frame is not None else None


def recorder(kind, state=None, title=None):
    """A recorder for an algorithm's steps on state (kind: matrix, arrayPointers, tree, graph, bars, board, canvas)."""
    return Recorder(kind, state, title)


class Display:
    """Every function above, as Display.name(...): the same API as the other languages' Display."""


for _name in __all__:
    if _name not in ("Display", "DisplayHandle", "Recorder", "Event"):
        setattr(Display, _name, staticmethod(globals()[_name]))


def _flush_at_exit():
    # Updates still waiting when the program ends are sent, so its visuals end as it left them.
    import gc
    for obj in gc.get_objects():
        if isinstance(obj, DisplayHandle):
            try:
                obj.flush()
            except Exception:
                pass


import atexit  # noqa: E402
atexit.register(_flush_at_exit)
