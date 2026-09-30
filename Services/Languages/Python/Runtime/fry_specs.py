"""Specs for the studio's charts, 3D plots and visualizers, from Python data (docs/visual-protocol.md).

The data conventions are those of every language (the C# builders in Visuals/Building are the reference):

* charts: numbers (x is the index; None, NaN and infinity are gaps), [x, y] pairs, a label -> number dict, a
  name -> sequence dict (a series each), or records (dicts, dataclasses, objects) whose value is their first field
  named in VALUE_NAMES (else their first number) and whose place is their first field named in PLACE_NAMES;
* 3D: (x, y, z) triples (a fourth item is the label), records with x, y and z, a name -> points dict, a grid of heights
  (a row per y) or a function z = f(x, y), and for a graph an adjacency dict;
* visualizers: a 2D sequence (a matrix), a sequence with pointers (an array), node objects or dicts with
  value/val/data and left/right or children (a tree) or next (a list), an adjacency dict or edge list (a graph).

Nothing here draws or sends anything: each function returns (mime, spec) with the spec as plain JSON data.
"""

import math
import numbers

CHART_MIME = "application/vnd.fry.chart.v1+json"
PLOT3D_MIME = "application/vnd.fry.plot3d.v1+json"
VISUALIZER_MIME = "application/vnd.fry.visualizer.v1+json"

VALUE_NAMES = ("y", "value", "amount", "count", "total", "score", "revenue", "sales", "price", "cost")
PLACE_NAMES = ("x", "key", "label", "name", "title", "category", "region", "country", "item")
LABEL_NAMES = ("label", "name", "title", "id")
NODE_VALUE_NAMES = ("val", "Val", "value", "Value", "data", "Data", "key", "Key")

CHART_KINDS = ("line", "area", "bar", "scatter", "pie", "donut", "histogram")
PLOT3D_KINDS = ("scatter", "surface", "wireframe", "trajectory", "graph", "voxelBar")
TRAVERSALS = {"preorder": "preorder", "inorder": "inorder", "postorder": "postorder", "levelorder": "levelOrder", "bfs": "levelOrder"}


# ------------------------------------------------------------------------------------------------------ reading data

def number(value):
    """A value as a number, or None: a gap for None, NaN, infinity and anything that isn't a number."""
    if value is None or isinstance(value, bool):
        return None
    item = getattr(value, "item", None)  # a numpy scalar
    if callable(item) and not isinstance(value, numbers.Number):
        try:
            value = item()
        except (TypeError, ValueError):
            return None
    if isinstance(value, numbers.Real):
        value = float(value) if not isinstance(value, int) else value
        return value if isinstance(value, int) or math.isfinite(value) else None
    return None


def _plain(data):
    tolist = getattr(data, "tolist", None)  # numpy arrays, pandas Series
    if callable(tolist) and not isinstance(data, (list, tuple, dict, str)):
        return tolist()
    to_dict = getattr(data, "to_dict", None)  # a pandas DataFrame: its records
    if callable(to_dict) and hasattr(data, "columns"):
        return to_dict(orient="records")
    return data


def is_sequence(value):
    value = _plain(value)
    return isinstance(value, (list, tuple, range)) or (
        hasattr(value, "__iter__") and not isinstance(value, (str, bytes, dict)) and not _is_record(value))


def _is_record(value):
    return isinstance(value, dict) or hasattr(value, "_asdict") or hasattr(value, "__dict__") or hasattr(value, "__slots__")


def members(record):
    """A record's fields in order: a dict's items, a namedtuple's or dataclass's fields, an object's attributes."""
    if isinstance(record, dict):
        return list(record.items())
    if hasattr(record, "_asdict"):
        return list(record._asdict().items())
    if hasattr(record, "__dataclass_fields__"):
        return [(name, getattr(record, name)) for name in record.__dataclass_fields__]
    if hasattr(record, "__dict__"):
        return [(k, v) for k, v in vars(record).items() if not k.startswith("_")]
    return [(name, getattr(record, name)) for name in getattr(record, "__slots__", ()) if hasattr(record, name)]


def member(record, names):
    """(name, value) of the record's first field named in names, or (None, None)."""
    fields = dict(members(record))
    for name in names:
        if name in fields:
            return name, fields[name]
    return None, None


def _as_list(data):
    data = _plain(data)
    return list(data) if data is not None else []


# ----------------------------------------------------------------------------------------------------------- charts

def _place(value):
    if value is None:
        return None, None
    n = number(value)
    return (n, None) if n is not None and not isinstance(value, str) else (None, str(value))


def _read(item):
    n = number(item)
    if n is not None:
        return None, None, n
    if item is None or isinstance(item, (float, bool)):
        return None, None, None
    item = _plain(item)
    if isinstance(item, (list, tuple)) and not hasattr(item, "_asdict"):
        if len(item) != 2:
            return None, None, None
        x, label = _place(item[0])
        return x, label, number(item[1])
    if _is_record(item):
        place_name, place_value = member(item, PLACE_NAMES)
        _, named = member(item, VALUE_NAMES)
        y = number(named)
        if y is None:
            for name, value in members(item):
                if name != place_name and number(value) is not None:
                    y = number(value)
                    break
        x, label = _place(place_value) if place_name is not None else (None, None)
        return x, label, y
    return None, None, None


def _series(items, name=None):
    series = {}
    if name is not None:
        series["name"] = str(name)
    xs, labels, ys = [], [], []
    for x, label, y in items:
        xs.append(x)
        labels.append(label)
        ys.append(y)
    if any(x is not None for x in xs):
        series["x"] = xs
    series["y"] = ys
    if any(label is not None for label in labels):
        series["labels"] = labels
    return series


def chart_spec(data, kind="line", x=None, y=None):
    """A chart of data; x and y (field names or functions) say where each record goes and its value."""
    if kind not in CHART_KINDS:
        raise ValueError(f"There is no {kind!r} chart: use one of {', '.join(CHART_KINDS)}.")
    if kind == "histogram":
        return histogram_spec(data)
    spec = {"kind": kind, "series": []}
    data = _plain(data)
    if y is not None:
        get_y = y if callable(y) else (lambda r, name=y: dict(members(r)).get(name))
        get_x = None if x is None else x if callable(x) else (lambda r, name=x: dict(members(r)).get(name))
        spec["series"].append(_series((*_place(get_x(r) if get_x else None), number(get_y(r))) for r in _as_list(data)))
    elif data is None:
        pass
    elif isinstance(data, dict):
        entries = list(data.items())
        if entries and all(is_sequence(v) for _, v in entries):
            spec["series"] = [_series((_read(i) for i in _as_list(v)), k) for k, v in entries]
        else:
            spec["series"].append(_series((None, str(k), number(v)) for k, v in entries))
    elif is_sequence(data):
        spec["series"].append(_series(_read(i) for i in _as_list(data)))
    else:
        raise TypeError(f"A chart shows a sequence of values or a dict of them, not a single {type(data).__name__}.")
    return CHART_MIME, spec


def histogram_spec(samples, bins=None):
    """A histogram: the studio counts the samples into bins bars (10 when left out)."""
    spec = {"kind": "histogram"}
    if bins is not None:
        spec["bins"] = int(bins)
    samples = _plain(samples)
    if samples is None:
        spec["series"] = []
    elif isinstance(samples, dict) and all(is_sequence(v) for v in samples.values()):
        spec["series"] = [{"name": str(k), "values": [v for v in map(number, _as_list(s)) if v is not None]} for k, s in samples.items()]
    elif is_sequence(samples):
        spec["series"] = [{"values": [v for v in map(number, _as_list(samples)) if v is not None]}]
    else:
        raise TypeError(f"A histogram counts a sequence of numbers, not a single {type(samples).__name__}.")
    return CHART_MIME, spec


# --------------------------------------------------------------------------------------------------------------- 3D

def _point(item):
    item = _plain(item)
    if isinstance(item, (list, tuple)) and not hasattr(item, "_asdict"):
        if len(item) < 3:
            return None
        coordinates = [number(v) for v in item[:3]]
        label = str(item[3]) if len(item) > 3 and item[3] is not None else None
        return None if None in coordinates else (*coordinates, label, None, None)
    if item is None or not _is_record(item):
        return None
    fields = dict(members(item))
    if not all(k in fields for k in ("x", "y", "z")):
        return None
    coordinates = [number(fields[k]) for k in ("x", "y", "z")]
    if None in coordinates:
        return None
    _, label = member(item, LABEL_NAMES)
    _, color = member(item, ("color", "colour"))
    return (*coordinates, None if label is None else str(label), None if color is None else str(color), number(fields.get("size")))


def _points(items, name=None):
    series = {}
    if name is not None:
        series["name"] = str(name)
    columns = {"x": [], "y": [], "z": []}
    labels, colors, sizes = [], [], []
    for item in _as_list(items):
        point = _point(item)
        if point is None:
            continue
        for key, value in zip("xyz", point[:3]):
            columns[key].append(value)
        labels.append(point[3])
        colors.append(point[4])
        sizes.append(point[5])
    series.update(columns)
    if any(v is not None for v in labels):
        series["labels"] = labels
    if any(v is not None for v in sizes):
        series["sizes"] = sizes
    if any(v is not None for v in colors):
        series["colors"] = colors
    return series


def _grid_surface(rows):
    rows = [_as_list(r) if is_sequence(r) else [] for r in _as_list(rows)]
    columns = max((len(r) for r in rows), default=0)
    return {
        "x": {"min": 0, "max": max(1, columns - 1)},
        "y": {"min": 0, "max": max(1, len(rows) - 1)},
        "z": [[number(r[c]) if c < len(r) else None for c in range(columns)] for r in rows],
    }


def surface_spec(function, x=(-5, 5), y=(-5, 5), resolution=30, wireframe=False):
    """The surface z = function(x, y), sampled resolution times along each axis (a row per y)."""
    n = max(2, int(resolution))

    def at(lo, hi, i):
        return lo + (hi - lo) * i / (n - 1)

    z = []
    for r in range(n):
        yy = at(y[0], y[1], r)
        row = []
        for c in range(n):
            try:
                row.append(number(function(at(x[0], x[1], c), yy)))
            except (ArithmeticError, ValueError):
                row.append(None)
        z.append(row)
    return PLOT3D_MIME, {"kind": "wireframe" if wireframe else "surface",
                         "surface": {"x": {"min": x[0], "max": x[1]}, "y": {"min": y[0], "max": y[1]}, "z": z}}


def _adjacency(adjacency, directed=True):
    nodes, edges, seen = [], [], set()

    def node(name):
        if name not in seen:
            seen.add(name)
            nodes.append({"id": name})

    for key, targets in adjacency.items():
        source = str(key)
        node(source)
        many = _as_list(targets) if is_sequence(targets) else [targets]
        for target in many:
            if target is None:
                continue
            weight = None
            if isinstance(target, (list, tuple)) and len(target) == 2:  # (neighbour, weight)
                target, weight = target
            node(str(target))
            edge = {"from": source, "to": str(target)}
            if number(weight) is not None:
                edge["weight"] = number(weight)
            edges.append(edge)
    return {"directed": bool(directed), "nodes": nodes, "edges": edges}


def _edge_list(edges, directed=True):
    adjacency = {}
    for edge in _as_list(edges):
        edge = _as_list(edge)
        if len(edge) < 2:
            continue
        adjacency.setdefault(edge[0], [])
        adjacency.setdefault(edge[1], [])
        adjacency[edge[0]].append((edge[1], edge[2]) if len(edge) > 2 else edge[1])
    return _adjacency(adjacency, directed)


def plot3d_spec(data, kind=None):
    """A 3D plot of points, a surface or a graph (kind: scatter, surface, wireframe, trajectory, graph, voxelBar)."""
    if kind is not None and kind not in PLOT3D_KINDS:
        raise ValueError(f"There is no {kind!r} 3D plot: use one of {', '.join(PLOT3D_KINDS)}.")
    data = _plain(data)
    if callable(data):
        return surface_spec(data, wireframe=kind == "wireframe")
    if kind in ("surface", "wireframe"):
        return PLOT3D_MIME, {"kind": kind, "surface": _grid_surface(data)}
    if kind == "graph":
        graph = data if isinstance(data, dict) and "nodes" in data else _adjacency(data) if isinstance(data, dict) else _edge_list(data)
        return PLOT3D_MIME, {"kind": "graph", "graph": graph}
    spec = {"kind": kind or "scatter", "series": []}
    if data is None:
        pass
    elif isinstance(data, dict) and all(is_sequence(v) for v in data.values()):
        spec["series"] = [_points(v, k) for k, v in data.items()]
    elif is_sequence(data):
        spec["series"].append(_points(data))
    else:
        raise TypeError(f"A 3D plot shows points, a surface or a graph, not a single {type(data).__name__}.")
    return PLOT3D_MIME, spec


# ------------------------------------------------------------------------------------------------------ visualizers

def _scalar(value):
    value = _plain(value)
    if value is None or isinstance(value, (bool, str)):
        return value
    n = number(value)
    return n if n is not None else str(value)


def _text(value):
    return "null" if value is None else str(_scalar(value)) if not isinstance(value, bool) else str(value)


def visualizer(kind, state, **fields):
    spec = {"kind": kind, "state": state}
    spec.update({k: v for k, v in fields.items() if v is not None})
    return VISUALIZER_MIME, spec


def grid_state(grid):
    rows = [[_scalar(v) for v in _as_list(row)] for row in _as_list(grid)]
    return {"grid": {"values": rows}}


def matrix_spec(grid, show_coordinates=None, show_values=None, cell_size=None):
    return visualizer("matrix", grid_state(grid), showCoordinates=show_coordinates, showValues=show_values, cellSize=cell_size)


def islands_spec(grid, record_steps=True, four_directional=True):
    islands = {}
    if not record_steps:
        islands["recordSteps"] = False
    if not four_directional:
        islands["fourDirectional"] = False
    return visualizer("islands", grid_state(grid), islands=islands or None)


def board_spec(grid, checkerboard=True):
    state = grid_state(grid)
    state["grid"]["checkerboard"] = bool(checkerboard)
    return visualizer("board", state)


def element(ref):
    """An element as specs name it: an index (3), a node id ("a"), a cell ((r, c)) or an edge (("a", "b"))."""
    if isinstance(ref, (tuple, list)) and len(ref) == 2:
        if all(isinstance(v, int) and not isinstance(v, bool) for v in ref):
            return [ref[0], ref[1]]
        return {"from": str(ref[0]), "to": str(ref[1])}
    if isinstance(ref, int) and not isinstance(ref, bool):
        return ref
    return None if ref is None else str(ref)


def pointers(named):
    """{name: element} as the spec's pointers, in order."""
    if not named:
        return None
    return [{"name": str(name), "at": element(at)} for name, at in dict(named).items()]


def array_spec(values, pointer_positions=None):
    values = list(values) if isinstance(values, str) else _as_list(values)
    return visualizer("arrayPointers", {"array": {"values": [_scalar(v) for v in values]}}, pointers=pointers(pointer_positions))


def bars_state(values):
    return {"bars": {"values": [number(v) for v in _as_list(values)]}}


def bars_spec(values):
    return visualizer("bars", bars_state(values))


def _node_value(node):
    if isinstance(node, dict):
        for name in NODE_VALUE_NAMES:
            if name in node:
                return node[name]
        return None
    for name in NODE_VALUE_NAMES:
        if hasattr(node, name):
            return getattr(node, name)
    return node


def _field(node, *names):
    for name in names:
        if isinstance(node, dict):
            if name in node:
                return node[name]
        elif hasattr(node, name):
            return getattr(node, name)
    return None


def _level_order(values):
    values = [None if v is None or str(v).strip().lower() in ("null", "#", "") else _text(v) for v in values]
    if not values or values[0] is None:
        return {"nodes": []}
    counter = [1]
    root = {"id": "node_1", "value": values[0]}
    order = [root]  # nodes in the order they are listed: depth first, as the reference lists them
    queue, i, children = [root], 1, {}
    while queue and i < len(values):
        current = queue.pop(0)
        for side in ("left", "right"):
            if i < len(values):
                if values[i] is not None:
                    counter[0] += 1
                    child = {"id": f"node_{counter[0]}", "value": values[i]}
                    current[side] = child["id"]
                    children.setdefault(current["id"], []).append(child)
                    queue.append(child)
                i += 1

    listed = []

    def walk(node):
        listed.append(node)
        for child in children.get(node["id"], []):
            walk(child)

    walk(root)
    return {"root": "node_1", "nodes": listed}


def tree_state(root):
    root = _plain(root)
    if root is None:
        return {"tree": {"nodes": []}}
    if isinstance(root, str):
        text = root.strip()
        if text.startswith("[") and text.endswith("]"):
            text = text[1:-1]
        return {"tree": _level_order([t.strip().strip("\"'") for t in text.split(",")] if text.strip() else [])}
    if isinstance(root, (list, tuple)):
        return {"tree": _level_order(list(root))}
    nodes, counter, ancestors = [], [0], set()

    def parse(node):
        if node is None or id(node) in ancestors:
            return None
        ancestors.add(id(node))
        try:
            counter[0] += 1
            spec = {"id": f"node_{counter[0]}", "value": _text(_node_value(node))}
            nodes.append(spec)
            left, right = _field(node, "left", "Left"), _field(node, "right", "Right")
            if left is not None or right is not None:
                for side, child in (("left", left), ("right", right)):
                    child_id = parse(child)
                    if child_id is not None:
                        spec[side] = child_id
            else:
                kids = [k for k in (parse(c) for c in _as_list(_field(node, "children", "Children", "nodes", "Nodes") or [])) if k]
                if kids:
                    spec["children"] = kids
            return spec["id"]
        finally:
            ancestors.discard(id(node))

    parse(root)
    return {"tree": {"root": "node_1", "nodes": nodes}}


def tree_spec(root, traversal=None):
    walk = None
    if traversal:
        key = str(traversal).lower().replace("-", "").replace("_", "").replace(" ", "")
        if key not in TRAVERSALS:
            raise ValueError(f"There is no {traversal!r} walk of a tree: use preorder, inorder, postorder or levelorder.")
        walk = TRAVERSALS[key]
    return visualizer("tree", tree_state(root), traversal=walk)


def graph_state(graph, directed=True):
    graph = _plain(graph)
    if isinstance(graph, dict) and "nodes" in graph and "edges" in graph:
        return {"graph": graph}
    return {"graph": _adjacency(graph, directed) if isinstance(graph, dict) else _edge_list(graph, directed)}


def graph_spec(graph, directed=True):
    return visualizer("graph", graph_state(graph, directed))


def linked_list_state(head, limit=500):
    nodes, seen, node = [], {}, head
    while node is not None and len(nodes) < limit:
        if id(node) in seen:  # a cycle: the last node points back
            nodes[-1]["next"] = seen[id(node)]
            break
        node_id = f"n{len(nodes)}"
        seen[id(node)] = node_id
        if nodes:
            nodes[-1]["next"] = node_id
        nodes.append({"id": node_id, "value": _text(_node_value(node))})
        node = _field(node, "next", "Next")
    if node is not None and len(nodes) >= limit and id(node) not in seen:
        nodes[-1]["truncated"] = True
    return {"linkedList": {"nodes": nodes}}


def linked_list_spec(head, detect_cycle=False):
    return visualizer("linkedList", linked_list_state(head), detectCycle=True if detect_cycle else None)


def canvas_spec(shapes, width=600, height=300, background=None):
    state = {"width": width, "height": height, "shapes": [dict(s) for s in _as_list(shapes)]}
    if background is not None:
        state["background"] = background
    return visualizer("canvas", {"canvas": state})


def visualize_spec(data):
    """A visualizer for whatever data is: a grid, a tree or list node, a graph, or a sequence (an array)."""
    data = _plain(data)
    if data is None:
        raise ValueError("There is nothing to visualize (None).")
    if isinstance(data, str):
        return array_spec(data)
    if isinstance(data, dict):
        if any(k in data for k in ("left", "right", "children")):
            return tree_spec(data)
        if "next" in data:
            return linked_list_spec(data)
        return graph_spec(data)
    if is_sequence(data):
        items = _as_list(data)
        return matrix_spec(items) if items and all(is_sequence(r) for r in items) else array_spec(items)
    if _field(data, "next", "Next") is not None or hasattr(data, "next"):
        return linked_list_spec(data)
    if any(hasattr(data, k) for k in ("left", "right", "children", "Left", "Right", "Children")):
        return tree_spec(data)
    raise TypeError(f"A {type(data).__name__} doesn't look like a grid, tree, list, graph or array; "
                    "use matrix, tree, linked_list, graph or array to say which.")


STATE_OF_KIND = {
    "matrix": grid_state, "islands": grid_state, "board": grid_state, "arrayPointers": lambda v: {"array": {"values": [_scalar(x) for x in _as_list(v)]}},
    "tree": tree_state, "graph": graph_state, "linkedList": linked_list_state, "bars": bars_state,
    "canvas": lambda shapes: {"canvas": {"shapes": [dict(s) for s in _as_list(shapes)]}},
}
