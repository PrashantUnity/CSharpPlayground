using System.IO;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Provides zero-configuration runtime support for Python scripts in C# Code Studio,
/// injecting <c>dump()</c>, <c>display()</c>, and <c>Display</c> into Python scripts via
/// <c>sitecustomize.py</c> on <c>PYTHONPATH</c>.
/// </summary>
public static class PythonDisplayRuntime
{
    public static async Task<string> EnsureRuntimeFilesAsync(IHostEnvironment host, CancellationToken ct = default)
    {
        var runtimeDir = Path.Combine(Path.GetTempPath(), "FryStudio", "python_runtime");
        try
        {
            Directory.CreateDirectory(runtimeDir);

            var siteCustomizePath = Path.Combine(runtimeDir, "sitecustomize.py");
            var fryPath = Path.Combine(runtimeDir, "fry.py");

            var siteCustomizeCode = """
                # Injected by FryPDF C# Code Studio for rich interactive display
                try:
                    import builtins
                    import fry
                    builtins.dump = fry.dump
                    builtins.display = fry.display
                    builtins.Display = fry.Display
                except Exception:
                    pass
                """;

            var fryCode = """
                import sys
                import json
                import base64
                import os
                import io

                DISPLAY_MARKER = "__FRY_DISPLAY__"
                TABLE_MIME = "application/vnd.fry.table+json"

                def dump(obj=None, title=None):
                    return Display.dump(obj, title=title)

                def display(*objects, **kwargs):
                    for obj in objects:
                        Display.dump(obj)

                class Display:
                    @staticmethod
                    def dump(obj=None, title=None):
                        return Display.table(obj, title=title)

                    @staticmethod
                    def table(obj=None, title=None):
                        try:
                            bundle = _to_table_bundle(obj, title=title)
                            _emit(bundle)
                        except Exception as e:
                            sys.stderr.write(f"[Display] table error: {e}\n")
                        return obj

                    @staticmethod
                    def show(obj=None, title=None):
                        return Display.dump(obj, title=title)

                    @staticmethod
                    def html(html_content):
                        _emit({"text/html": str(html_content)})

                    @staticmethod
                    def image(image_or_path, format="PNG"):
                        if image_or_path is None:
                            return
                        # Matplotlib figure
                        if hasattr(image_or_path, "savefig"):
                            buf = io.BytesIO()
                            image_or_path.savefig(buf, format="png", bbox_inches="tight")
                            b64 = base64.b64encode(buf.getvalue()).decode("ascii")
                            _emit({"image/png": b64})
                            return
                        # Bytes
                        if isinstance(image_or_path, (bytes, bytearray)):
                            b64 = base64.b64encode(image_or_path).decode("ascii")
                            mime = "image/jpeg" if format.upper() == "JPEG" else "image/png"
                            _emit({mime: b64})
                            return
                        # File path
                        if isinstance(image_or_path, str) and os.path.isfile(image_or_path):
                            with open(image_or_path, "rb") as f:
                                b64 = base64.b64encode(f.read()).decode("ascii")
                            mime = "image/jpeg" if image_or_path.lower().endswith((".jpg", ".jpeg")) else "image/png"
                            _emit({mime: b64})
                            return
                        # Raw base64 string
                        if isinstance(image_or_path, str):
                            raw = image_or_path.strip()
                            if raw.startswith("data:image"):
                                comma = raw.find(",")
                                if comma >= 0:
                                    raw = raw[comma + 1:]
                            _emit({"image/png": raw})

                def _emit(data_bundle):
                    msg = json.dumps({"type": "display", "data": data_bundle, "metadata": {}})
                    sys.stdout.write(f"\n{DISPLAY_MARKER} {msg}\n")
                    sys.stdout.flush()

                def _to_table_bundle(obj, title=None):
                    if obj is None:
                        t = title if title is not None else "null"
                        return {TABLE_MIME: {"title": t, "columns": ["Value"], "numeric": [False], "rows": [["null"]], "totalRows": 1, "totalColumns": 1}}

                    # Pandas DataFrame / Series
                    if hasattr(obj, "to_dict") and hasattr(obj, "columns"):
                        try:
                            cols = [str(c) for c in obj.columns]
                            rows = []
                            for _, r in obj.iterrows():
                                rows.append([_cell(r[c]) for c in obj.columns])
                            t = title if title is not None else f"DataFrame [{len(rows)} rows × {len(cols)} cols]"
                            numeric = [True if str(obj[c].dtype).startswith(("int", "float")) else False for c in obj.columns]
                            return {TABLE_MIME: {"title": t, "columns": cols, "numeric": numeric, "rows": rows, "totalRows": len(rows), "totalColumns": len(cols)}}
                        except Exception:
                            pass

                    # List / Tuple / Set
                    if isinstance(obj, (list, tuple, set)):
                        lst = list(obj)
                        t = title if title is not None else f"List[{len(lst)}]"
                        if len(lst) == 0:
                            return {TABLE_MIME: {"title": t, "columns": ["Value"], "numeric": [False], "rows": [], "totalRows": 0, "totalColumns": 1}}

                        first = lst[0]
                        # List of dicts
                        if isinstance(first, dict):
                            keys = list(first.keys())
                            cols = [str(k) for k in keys]
                            rows = [[_cell(d.get(k)) for k in keys] for d in lst if isinstance(d, dict)]
                            numeric = [all(isinstance(r[c], (int, float)) for r in rows) for c in range(len(cols))]
                            return {TABLE_MIME: {"title": t, "columns": cols, "numeric": numeric, "rows": rows, "totalRows": len(rows), "totalColumns": len(cols)}}

                        # 2D List
                        if isinstance(first, (list, tuple)):
                            max_c = max(len(r) for r in lst if isinstance(r, (list, tuple)))
                            cols = [f"[{c}]" for c in range(max_c)]
                            rows = []
                            for r in lst:
                                r_list = list(r) if isinstance(r, (list, tuple)) else [r]
                                row = [_cell(r_list[c]) if c < len(r_list) else None for c in range(max_c)]
                                rows.append(row)
                            return {TABLE_MIME: {"title": t, "columns": cols, "numeric": [False]*max_c, "rows": rows, "totalRows": len(rows), "totalColumns": max_c}}

                        # 1D List of scalars or objects
                        rows = [[i, _cell(v)] for i, v in enumerate(lst)]
                        is_num = all(isinstance(v, (int, float)) for v in lst)
                        return {TABLE_MIME: {"title": t, "columns": ["Index", "Value"], "numeric": [True, is_num], "rows": rows, "totalRows": len(rows), "totalColumns": 2}}

                    # Dict
                    if isinstance(obj, dict):
                        t = title if title is not None else f"Dict[{len(obj)}]"
                        rows = [[str(k), _cell(v)] for k, v in obj.items()]
                        return {TABLE_MIME: {"title": t, "columns": ["Key", "Value"], "numeric": [False, False], "rows": rows, "totalRows": len(rows), "totalColumns": 2}}

                    # Dataclass or object with __dict__
                    if hasattr(obj, "__dict__"):
                        d = obj.__dict__
                        t = title if title is not None else type(obj).__name__
                        rows = [[str(k), _cell(v)] for k, v in d.items() if not str(k).startswith("_")]
                        return {TABLE_MIME: {"title": t, "columns": ["Property", "Value"], "numeric": [False, False], "rows": rows, "totalRows": len(rows), "totalColumns": 2}}

                    # Scalar
                    t = title if title is not None else type(obj).__name__
                    return {TABLE_MIME: {"title": t, "columns": ["Value"], "numeric": [isinstance(obj, (int, float))], "rows": [[_cell(obj)]], "totalRows": 1, "totalColumns": 1}}

                def _cell(val):
                    if val is None:
                        return None
                    if isinstance(val, (int, float, bool, str)):
                        return val
                    return str(val)
                """;

            await File.WriteAllTextAsync(siteCustomizePath, siteCustomizeCode, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(fryPath, fryCode, ct).ConfigureAwait(false);
        }
        catch
        {
            // Defensive fallback
        }

        return runtimeDir;
    }
}
