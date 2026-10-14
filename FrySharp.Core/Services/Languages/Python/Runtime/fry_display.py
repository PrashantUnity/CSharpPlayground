"""Rich output for the FryPDF notebook's Python kernel: what a value looks like in a cell's output.

A value is shown the way Jupyter would show it, as a MIME bundle, using the first of these it has:
_repr_mimebundle_, a pandas DataFrame/Series (as the studio's own table), _repr_png_ / _repr_jpeg_, _repr_svg_,
_repr_html_, a matplotlib Figure (as a PNG), an object or dict (as the Object Explorer's tree), and otherwise its repr. Also turns values into JSON for #!share.
"""

import base64
import collections
import dataclasses
import datetime
import decimal
import enum
import io
import json
import math
import pathlib
import pprint
import sys
import types
import uuid

MAX_TEXT = 100_000
MAX_IMAGE_BYTES = 20 * 1024 * 1024
MAX_ROWS = 1000
MAX_COLUMNS = 100
MAX_CELL_TEXT = 200

TABLE_MIME = "application/vnd.fry.table+json"
INSPECTOR_MIME = "application/vnd.fry.inspector+json"

# The Object Explorer: levels of objects shown below the first, items of a collection listed, scalars on one line
MAX_INSPECT_DEPTH = 3
MAX_INSPECT_ITEMS = 100
MAX_INSPECT_INLINE = 50

_publisher = None


def set_publisher(publisher):
    """The kernel's function that sends a bundle to the studio: publisher(data, metadata)."""
    global _publisher
    _publisher = publisher


def display(*objects, **kwargs):
    """Shows each object in the cell's output, as the last expression of a cell would be."""
    for obj in objects:
        publish_object(obj)


def __getattr__(name):
    """Fallback export to unify 'from fry_display import Display' with 'from fry import Display'."""
    try:
        import fry
        if hasattr(fry, name):
            return getattr(fry, name)
    except Exception:
        pass
    raise AttributeError(f"module {__name__!r} has no attribute {name!r}")


def publish_object(obj):
    if getattr(obj, "_fry_shown", False): return
    data, metadata = to_mime(obj)
    if data and _publisher is not None:
        _publisher(data, metadata)


def to_mime(obj):
    """(data, metadata) for a value: MIME type → content, the richest first."""
    if isinstance(obj, type):
        return {"text/plain": _text(obj)}, {}

    bundle = _call(obj, "_repr_mimebundle_", include=None, exclude=None)
    if bundle:
        data, metadata = bundle if isinstance(bundle, tuple) else (bundle, {})
        data = _normalize(data)
        if data:
            return data, metadata or {}

    if _is_pandas(obj):
        try:
            return {TABLE_MIME: table(obj), "text/plain": _text(obj)}, {}
        except Exception:
            pass

    if isinstance(obj, (list, tuple)) and len(obj) > 0 and isinstance(obj[0], dict):
        try:
            return {TABLE_MIME: records_table(obj), "text/plain": _text(obj)}, {}
        except Exception:
            pass

    for mime, method in (("image/png", "_repr_png_"), ("image/jpeg", "_repr_jpeg_")):
        image = _call(obj, method)
        if isinstance(image, tuple):
            image = image[0]
        if isinstance(image, (bytes, bytearray)) and 0 < len(image) <= MAX_IMAGE_BYTES:
            return {mime: base64.b64encode(image).decode("ascii"), "text/plain": _text(obj)}, {}

    svg = _call(obj, "_repr_svg_")
    if isinstance(svg, str) and svg:
        return {"image/svg+xml": svg, "text/plain": _text(obj)}, {}

    html = _call(obj, "_repr_html_")
    if isinstance(html, str) and html:
        return {"text/html": html, "text/plain": _text(obj)}, {}

    if _is_figure(obj):
        return figure_bundle(obj, close=True)

    node = inspector(obj)
    if node is not None:
        return {INSPECTOR_MIME: node, "text/plain": _text(obj)}, {}

    return {"text/plain": _text(obj)}, {}


def figure_bundle(figure, close=False):
    """A matplotlib figure as PNG output."""
    buffer = io.BytesIO()
    figure.savefig(buffer, format="png", bbox_inches="tight")
    width, height = figure.get_size_inches() * figure.dpi
    if close:
        # Shown now, so not again when the cell's open figures are shown at its end.
        import matplotlib.pyplot as plt
        plt.close(figure)
    return ({"image/png": base64.b64encode(buffer.getvalue()).decode("ascii")},
            {"image/png": {"width": int(width), "height": int(height)}})


def table(frame):
    """A DataFrame (or Series) as the studio's table: its first rows and columns, the index first."""
    pd = sys.modules["pandas"]
    if isinstance(frame, pd.Series):
        title = f"Series ({len(frame)})"
        frame = frame.to_frame(name=frame.name if frame.name is not None else "value")
    else:
        title = f"DataFrame ({frame.shape[0]} × {frame.shape[1]})"
    shown = frame.iloc[:MAX_ROWS, :MAX_COLUMNS]
    is_numeric = pd.api.types.is_numeric_dtype
    is_bool = pd.api.types.is_bool_dtype
    index_name = shown.index.name if shown.index.name is not None else ""
    columns = [str(index_name)] + [str(c) for c in shown.columns]
    numeric = [bool(is_numeric(shown.index.dtype) and not is_bool(shown.index.dtype))] + [
        bool(is_numeric(dtype) and not is_bool(dtype)) for dtype in shown.dtypes]
    rows = [[_cell(index)] + [_cell(value) for value in row]
            for index, row in zip(shown.index, shown.itertuples(index=False, name=None))]
    return {
        "title": title,
        "columns": columns,
        "numeric": numeric,
        "rows": rows,
        "totalRows": int(frame.shape[0]),
        "totalColumns": int(frame.shape[1]),
    }


def records_table(records, title=None):
    """A list of dicts as the studio's table."""
    cols = list(records[0].keys())
    numeric = []
    for c in cols:
        val = records[0].get(c)
        numeric.append(isinstance(val, (int, float)) and not isinstance(val, bool))
    rows = []
    for r in records[:MAX_ROWS]:
        rows.append([str(r.get(c, "")) if r.get(c) is not None else None for c in cols])
    return {
        "title": title or f"Records[{len(records)}]",
        "columns": cols,
        "numeric": numeric,
        "rows": rows,
        "totalRows": len(records),
        "totalColumns": len(cols),
    }


def to_jsonable(value, depth=0):
    """A value as plain JSON types, for #!share: numpy arrays become lists, DataFrames lists of records."""
    if depth > 32:
        raise TypeError("the value is nested too deeply to share")
    if value is None or isinstance(value, (bool, str)):
        return value
    if isinstance(value, int):
        return value
    if isinstance(value, float):
        return None if math.isnan(value) or math.isinf(value) else value
    numpy = sys.modules.get("numpy")
    if numpy is not None:
        if isinstance(value, numpy.ndarray):
            return to_jsonable(value.tolist(), depth + 1)
        if isinstance(value, numpy.generic):
            return to_jsonable(value.item(), depth + 1)
    pandas = sys.modules.get("pandas")
    if pandas is not None:
        if isinstance(value, pandas.DataFrame):
            return to_jsonable(value.to_dict(orient="records"), depth + 1)
        if isinstance(value, pandas.Series):
            return to_jsonable(value.tolist(), depth + 1)
    if isinstance(value, dict):
        return {str(k): to_jsonable(v, depth + 1) for k, v in value.items()}
    if isinstance(value, (list, tuple, set, frozenset, range)):
        return [to_jsonable(v, depth + 1) for v in value]
    if isinstance(value, (datetime.datetime, datetime.date, datetime.time)):
        return value.isoformat()
    if isinstance(value, decimal.Decimal):
        return float(value)
    if dataclasses.is_dataclass(value) and not isinstance(value, type):
        return to_jsonable(dataclasses.asdict(value), depth + 1)
    if isinstance(value, complex) or callable(value):
        raise TypeError(f"a {type(value).__name__} can't be shared as JSON")
    if hasattr(value, "__dict__"):
        return {k: to_jsonable(v, depth + 1) for k, v in vars(value).items() if not k.startswith("_")}
    raise TypeError(f"a {type(value).__name__} can't be shared as JSON")


def _call(obj, name, **kwargs):
    method = getattr(obj, name, None)
    if not callable(method):
        return None
    try:
        return method(**kwargs) if kwargs else method()
    except Exception:
        return None


def _normalize(data):
    """A _repr_mimebundle_ result with images as base64 text and JSON as text."""
    result = {}
    for mime, content in dict(data).items():
        if content is None:
            continue
        if isinstance(content, (bytes, bytearray)):
            if mime.startswith("image/") and len(content) <= MAX_IMAGE_BYTES:
                result[mime] = base64.b64encode(content).decode("ascii")
        elif isinstance(content, str):
            result[mime] = content
        else:
            try:
                result[mime] = json.dumps(content)
            except (TypeError, ValueError):
                pass
    return result


def _is_pandas(obj):
    module = type(obj).__module__.split(".")[0]
    if module != "pandas" or "pandas" not in sys.modules:
        return False
    pd = sys.modules["pandas"]
    return isinstance(obj, (pd.DataFrame, pd.Series))


def _is_figure(obj):
    module = type(obj).__module__
    return module.startswith("matplotlib.figure") and hasattr(obj, "savefig")


def _cell(value):
    numpy = sys.modules.get("numpy")
    if numpy is not None and isinstance(value, numpy.generic):
        value = value.item()
    if value is None:
        return None
    if isinstance(value, float):
        if math.isnan(value):
            return None
        return value if math.isfinite(value) else str(value)
    if isinstance(value, (bool, int)):
        return value
    pandas = sys.modules.get("pandas")
    if pandas is not None:
        try:
            if pandas.isna(value) is True:
                return None
        except (TypeError, ValueError):
            pass
    text = str(value)
    return text if len(text) <= MAX_CELL_TEXT else text[:MAX_CELL_TEXT - 1] + "…"


def inspector(obj):
    """The Object Explorer's tree for an object, as the studio shows a C# value: {title, properties: [{name, value,
    isString, isNull, child}]}. None for a value that reads best as text: a scalar, a function or class, a short
    list of scalars, or an object with nothing to list."""
    if _is_scalar(obj) or callable(obj) or isinstance(obj, (type, types.ModuleType)):
        return None
    if _inline(obj) is not None:
        return None
    try:
        node = _inspect_node(obj, 0, {id(obj)})
    except Exception:
        return None
    return node if node["properties"] else None


def _is_scalar(value):
    if value is None or isinstance(value, (bool, int, float, complex, str, bytes, bytearray, range,
                                           decimal.Decimal, datetime.date, datetime.time, datetime.timedelta,
                                           enum.Enum, uuid.UUID, pathlib.PurePath)):
        return True
    numpy = sys.modules.get("numpy")
    return numpy is not None and isinstance(value, numpy.generic)


def _scalar_text(value):
    if isinstance(value, float):
        if math.isnan(value):
            return "NaN"
        if math.isinf(value):
            return "∞" if value > 0 else "-∞"
    if isinstance(value, enum.Enum):
        return value.name
    return str(value)


def _inline(value):
    """One line for a list, tuple or set whose items are all scalars (or None), as '[ 1, 2, 3 ]'; else None."""
    if not isinstance(value, (list, tuple, set, frozenset, collections.deque)) or hasattr(value, "_fields"):
        return None
    parts = []
    for index, item in enumerate(value):
        if index > MAX_INSPECT_INLINE:
            parts.append("...")
            break
        if item is None:
            parts.append("<null>")
        elif _is_scalar(item):
            parts.append(_scalar_text(item))
        else:
            return None
    return "[ " + ", ".join(parts) + " ]"


def _inspect_node(obj, depth, path, circular=False):
    node = {"title": type(obj).__name__, "properties": []}
    if circular:
        node["circular"] = True  # a reference back to an object above: its members are listed again, down to the depth limit
    rows = node["properties"]
    for index, (name, value) in enumerate(_members(obj)):
        if index >= MAX_INSPECT_ITEMS:
            rows.append({"name": "...", "value": f"({index}+ items truncated)"})
            break
        rows.append(_inspect_row(name, value, depth + 1, path))
    return node


def _inspect_row(name, value, depth, path):
    row = {"name": name}
    if isinstance(value, _MemberError):
        row["value"] = f"<Error: {value.message}>"
    elif value is None:
        row["value"] = "<null>"
        row["isNull"] = True
    elif _is_scalar(value):
        row["value"] = _scalar_text(value)
        row["isString"] = isinstance(value, str)
    elif callable(value) or isinstance(value, (type, types.ModuleType)):
        row["value"] = _text(value)
    elif (inline := _inline(value)) is not None:
        row["value"] = inline
    elif depth > MAX_INSPECT_DEPTH:
        row["child"] = {"title": type(value).__name__, "properties": []}
    else:
        circular = id(value) in path
        if not circular:
            path.add(id(value))
        try:
            row["child"] = _inspect_node(value, depth, path, circular)
        finally:
            if not circular:
                path.discard(id(value))
    return row


class _MemberError:
    def __init__(self, error):
        self.message = str(error)


def _members(obj):
    """(name, value) pairs an object lists: a dict's items, a collection's items by index, otherwise its public
    attributes (a namedtuple's or dataclass's fields, __dict__, __slots__) and the properties of its class."""
    if isinstance(obj, dict):
        return [(str(key), value) for key, value in obj.items()]
    if isinstance(obj, tuple) and hasattr(obj, "_fields"):
        return list(zip(obj._fields, obj))
    if isinstance(obj, (list, tuple, set, frozenset, collections.deque)):
        return [(f"[{index}]", item) for index, item in enumerate(obj)]

    members = {}
    if dataclasses.is_dataclass(obj) and not isinstance(obj, type):
        for field in dataclasses.fields(obj):
            members[field.name] = getattr(obj, field.name, None)
    for name, value in getattr(obj, "__dict__", {}).items():
        if not name.startswith("_"):
            members.setdefault(name, value)
    for klass in type(obj).__mro__:
        slots = klass.__dict__.get("__slots__", ())
        for name in ((slots,) if isinstance(slots, str) else slots):
            if not name.startswith("_") and name not in members and hasattr(obj, name):
                members[name] = getattr(obj, name)
        for name, attribute in klass.__dict__.items():
            if isinstance(attribute, property) and not name.startswith("_") and name not in members:
                try:
                    members[name] = getattr(obj, name)
                except Exception as error:
                    members[name] = _MemberError(error)
    return list(members.items())


def _text(obj):
    try:
        text = pprint.pformat(obj, width=100, compact=True) if isinstance(obj, (dict, list, tuple, set, frozenset)) else repr(obj)
    except Exception as error:
        text = f"<{type(obj).__name__} (repr failed: {error})>"
    return text if len(text) <= MAX_TEXT else text[:MAX_TEXT] + " …"
