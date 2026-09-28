"""Rich output for the FryPDF notebook's Python kernel: what a value looks like in a cell's output.

A value is shown the way Jupyter would show it, as a MIME bundle, using the first of these it has:
_repr_mimebundle_, a pandas DataFrame/Series (as the studio's own table), _repr_png_ / _repr_jpeg_, _repr_svg_,
_repr_html_, a matplotlib Figure (as a PNG), and otherwise its repr. Also turns values into JSON for #!share.
"""

import base64
import dataclasses
import datetime
import decimal
import io
import json
import math
import pprint
import sys

MAX_TEXT = 100_000
MAX_IMAGE_BYTES = 20 * 1024 * 1024
MAX_ROWS = 1000
MAX_COLUMNS = 100
MAX_CELL_TEXT = 200

TABLE_MIME = "application/vnd.fry.table+json"

_publisher = None


def set_publisher(publisher):
    """The kernel's function that sends a bundle to the studio: publisher(data, metadata)."""
    global _publisher
    _publisher = publisher


def display(*objects, **kwargs):
    """Shows each object in the cell's output, as the last expression of a cell would be."""
    for obj in objects:
        publish_object(obj)


def publish_object(obj):
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


def _text(obj):
    try:
        text = pprint.pformat(obj, width=100, compact=True) if isinstance(obj, (dict, list, tuple, set, frozenset)) else repr(obj)
    except Exception as error:
        text = f"<{type(obj).__name__} (repr failed: {error})>"
    return text if len(text) <= MAX_TEXT else text[:MAX_TEXT] + " …"
