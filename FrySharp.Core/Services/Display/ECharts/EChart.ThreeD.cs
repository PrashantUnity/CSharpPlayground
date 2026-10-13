using System.Globalization;
using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

// The 3D charts, drawn by echarts-gl with WebGL, z up: drag to turn them, scroll to zoom, right-drag to move.
public sealed partial class EChart
{
    /// <summary>
    /// The surface z = <paramref name="function"/>(x, y) over the ranges (each −5 to 5 when left out), sampled
    /// <paramref name="resolution"/> times along each axis. Where the function is NaN or ∞ the surface has a hole.
    /// </summary>
    public static EChart Surface(Func<double, double, double> function, (double Min, double Max)? xRange = null, (double Min, double Max)? yRange = null, int resolution = 50)
    {
        ArgumentNullException.ThrowIfNull(function);
        var (x0, x1) = xRange ?? (-5.0, 5.0);
        var (y0, y1) = yRange ?? (-5.0, 5.0);
        var n = Math.Clamp(resolution, 2, 400);
        var rows = new List<List<double?>>(n);
        for (var r = 0; r < n; r++)
        {
            var y = y0 + r * (y1 - y0) / (n - 1);
            var row = new List<double?>(n);
            for (var c = 0; c < n; c++) row.Add(DataReader.Finite(function(x0 + c * (x1 - x0) / (n - 1), y)));
            rows.Add(row);
        }

        return SurfaceChart(rows, (x0, x1), (y0, y1));
    }

    /// <summary>
    /// A surface of heights: a grid (<c>double[,]</c> or rows of numbers), a row for each y and a column for each x, over
    /// the ranges (the column and row numbers when left out). A missing height is a hole.
    /// </summary>
    public static EChart Surface(object heights, (double Min, double Max)? xRange = null, (double Min, double Max)? yRange = null)
    {
        ArgumentNullException.ThrowIfNull(heights);
        if (heights is Func<double, double, double> function) return Surface(function, xRange, yRange);
        var rows = EChartData.Grid(heights, nameof(heights));
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        return SurfaceChart(rows, xRange ?? (0, Math.Max(1, columns - 1)), yRange ?? (0, Math.Max(1, rows.Count - 1)));
    }

    /// <summary>
    /// A surface traced by (x, y, z) = (<paramref name="x"/>(u, v), <paramref name="y"/>(u, v), <paramref name="z"/>(u, v))
    /// as u and v run over their ranges: a sphere, a torus, a Möbius strip, any shape that isn't one height per place.
    /// </summary>
    public static EChart ParametricSurface(
        Func<double, double, double> x,
        Func<double, double, double> y,
        Func<double, double, double> z,
        (double Min, double Max) u,
        (double Min, double Max) v,
        int resolution = 40)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(z);
        var n = Math.Clamp(resolution, 2, 400);
        var data = new JsonArray();
        var (min, max) = (double.MaxValue, double.MinValue);
        // Rows of v, each running along u: the order echarts-gl reads a parametric surface in.
        for (var j = 0; j < n; j++)
        {
            var vj = v.Min + j * (v.Max - v.Min) / (n - 1);
            for (var i = 0; i < n; i++)
            {
                var ui = u.Min + i * (u.Max - u.Min) / (n - 1);
                var pz = DataReader.Finite(z(ui, vj));
                if (pz is { } value) (min, max) = (Math.Min(min, value), Math.Max(max, value));
                data.Add(new JsonArray(Number(x(ui, vj)), Number(y(ui, vj)), Number(pz), ui, vj));
            }
        }

        var chart = ThreeD(EChartType.Surface);
        chart.Option["visualMap"] = ValueMap(min > max ? 0 : min, min > max ? 1 : max, ColorMapPreset.Viridis, horizontal: false, dimension: 2);
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "surface",
            ["parametric"] = true,
            ["dataShape"] = new JsonArray(n, n),
            ["shading"] = "lambert",
            ["wireframe"] = new JsonObject { ["show"] = false },
            ["data"] = data
        });
        return chart;
    }

    /// <summary>
    /// Bars standing on a floor. The data is a grid of heights (<c>double[,]</c> or rows: a row for each y, a column for
    /// each x), or (x, y, height) items: tuples, arrays, or records with x, y and z members. An x or y that is text is a
    /// category; <paramref name="xLabels"/> and <paramref name="yLabels"/> give the categories in order (an x or y that is
    /// a number then picks one by its position).
    /// </summary>
    public static EChart Bar3D(object data, IEnumerable<string>? xLabels = null, IEnumerable<string>? yLabels = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (EChartData.IsGrid(data))
        {
            var rows = EChartData.Grid(data, nameof(data));
            var items = new List<(object? X, object? Y, double? Z)>();
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Count; c++) items.Add((c, r, rows[r][c]));
            }

            var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
            return Bar3DChart(items,
                xLabels ?? Enumerable.Range(0, columns).Select(i => i.ToString(CultureInfo.InvariantCulture)),
                yLabels ?? Enumerable.Range(0, rows.Count).Select(i => i.ToString(CultureInfo.InvariantCulture)));
        }

        return Bar3DChart(EChartData.Triplets(data, nameof(data)), xLabels, yLabels);
    }

    /// <summary>Bars standing on a floor, from records: each at (<paramref name="x"/>, <paramref name="y"/>) as high as <paramref name="z"/>.</summary>
    public static EChart Bar3D<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, Func<T, object?> z, IEnumerable<string>? xLabels = null, IEnumerable<string>? yLabels = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        return Bar3DChart(records.Select(r => (x(r), y(r), DataReader.TryNumber(z(r), out var h) ? h : null)).ToList(), xLabels, yLabels);
    }

    /// <summary>Points in 3D: (x, y, z) tuples, three-number arrays, records with x, y and z, or a name → points map (a series each).</summary>
    public static EChart Scatter3D(object data)
    {
        var spec = Plot3DSpecBuilder.From(data, Plot3DType.Scatter);
        var chart = ThreeD(EChartType.Scatter3D);
        foreach (var series in spec.Series)
        {
            var points = new JsonArray();
            for (var i = 0; i < series.X.Count; i++)
            {
                var value = new JsonArray(Number(series.X[i]), Number(series.Y[i]), Number(series.Z[i]));
                var label = series.Labels?[i];
                points.Add(label == null ? value : (JsonNode)new JsonObject { ["name"] = label, ["value"] = value });
            }

            var item = new JsonObject { ["type"] = "scatter3D", ["symbolSize"] = 8, ["data"] = points };
            if (series.Name != null) item["name"] = series.Name;
            if (series.Color != null) item["itemStyle"] = new JsonObject { ["color"] = series.Color };
            chart.SeriesList().Add(item);
        }

        return chart;
    }

    /// <summary>Points in 3D, from records: each at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).</summary>
    public static EChart Scatter3D<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, Func<T, object?> z)
    {
        ArgumentNullException.ThrowIfNull(records);
        return Scatter3D(records.Select(r => new object?[] { x(r), y(r), z(r) }).ToList());
    }

    /// <summary>A name for the z axis, the one pointing up.</summary>
    public EChart ZLabel(string label) => SetAxis('z', a => a["name"] = label);

    /// <summary>Draws the grid lines over a surface (in <paramref name="color"/>, a CSS colour, when given).</summary>
    public EChart Wireframe(bool show = true, string? color = null)
    {
        var surfaces = Require3DSeries(nameof(Wireframe), "surface");
        foreach (var surface in surfaces)
        {
            var wireframe = new JsonObject { ["show"] = show };
            if (color != null) wireframe["lineStyle"] = new JsonObject { ["color"] = color, ["width"] = 1 };
            surface["wireframe"] = wireframe;
        }

        return this;
    }

    /// <summary>How surfaces and 3D bars are lit: flat colour, one light, or physically based.</summary>
    public EChart Shading(SurfaceShading shading)
    {
        foreach (var series in Require3DSeries(nameof(Shading), "surface", "bar3D"))
        {
            series["shading"] = shading switch { SurfaceShading.Color => "color", SurfaceShading.Realistic => "realistic", _ => "lambert" };
            if (shading == SurfaceShading.Realistic) series["realisticMaterial"] = new JsonObject { ["roughness"] = 0.4, ["metalness"] = 0 };
            else series.Remove("realisticMaterial");
        }

        return this;
    }

    /// <summary>Turns the 3D view by itself (it stops while it is dragged).</summary>
    public EChart AutoRotate(bool rotate = true, double speed = 10)
    {
        var grid = FirstObject("grid3D") ?? throw new InvalidOperationException("AutoRotate turns a 3D chart (surface, 3D bars, 3D points).");
        var view = grid["viewControl"] as JsonObject ?? new JsonObject();
        grid["viewControl"] = view;
        view["autoRotate"] = rotate;
        view["autoRotateSpeed"] = speed;
        return this;
    }

    private List<JsonObject> Require3DSeries(string setting, params string[] types)
    {
        var found = AllSeries().Where(s => types.Contains(TypeOf(s))).ToList();
        return found.Count > 0
            ? found
            : throw new InvalidOperationException($"{setting} is for {string.Join(" and ", types.Select(t => t == "bar3D" ? "3D bars" : t + "s"))}; this chart has none.");
    }

    // The 3D scene: value axes, a box with a perspective camera, a light from one side.
    private static EChart ThreeD(EChartType kind)
    {
        var chart = new EChart(kind);
        chart.Option["tooltip"] = new JsonObject();
        chart.Option["xAxis3D"] = new JsonObject { ["type"] = "value", ["name"] = "x" };
        chart.Option["yAxis3D"] = new JsonObject { ["type"] = "value", ["name"] = "y" };
        chart.Option["zAxis3D"] = new JsonObject { ["type"] = "value", ["name"] = "z" };
        chart.Option["grid3D"] = new JsonObject
        {
            ["boxWidth"] = 100,
            ["boxDepth"] = 100,
            ["boxHeight"] = 70,
            ["viewControl"] = new JsonObject { ["projection"] = "perspective", ["autoRotate"] = false, ["distance"] = 230 },
            ["light"] = new JsonObject
            {
                ["main"] = new JsonObject { ["intensity"] = 1.2, ["shadow"] = false, ["alpha"] = 40, ["beta"] = 30 },
                ["ambient"] = new JsonObject { ["intensity"] = 0.35 }
            }
        };
        return chart;
    }

    // A grid of heights as echarts-gl reads one: rows of y, each running along x, and its shape said outright.
    private static EChart SurfaceChart(List<List<double?>> rows, (double Min, double Max) xRange, (double Min, double Max) yRange)
    {
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        if (rows.Count < 2 || columns < 2) throw new ArgumentException("A surface needs at least 2 rows and 2 columns of heights.");
        var data = new JsonArray();
        var (min, max) = (double.MaxValue, double.MinValue);
        for (var r = 0; r < rows.Count; r++)
        {
            var y = yRange.Min + r * (yRange.Max - yRange.Min) / (rows.Count - 1);
            for (var c = 0; c < columns; c++)
            {
                var x = xRange.Min + c * (xRange.Max - xRange.Min) / (columns - 1);
                var z = c < rows[r].Count ? rows[r][c] : null;
                if (z is { } value) (min, max) = (Math.Min(min, value), Math.Max(max, value));
                data.Add(new JsonArray(Math.Round(x, 6), Math.Round(y, 6), Number(z)));
            }
        }

        var chart = ThreeD(EChartType.Surface);
        chart.Option["visualMap"] = ValueMap(min > max ? 0 : min, min > max ? 1 : max, ColorMapPreset.Viridis, horizontal: false, dimension: 2);
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "surface",
            ["dataShape"] = new JsonArray(rows.Count, columns),
            ["shading"] = "lambert",
            ["wireframe"] = new JsonObject { ["show"] = false },
            ["data"] = data
        });
        return chart;
    }

    private static EChart Bar3DChart(List<(object? X, object? Y, double? Z)> items, IEnumerable<string>? xLabels, IEnumerable<string>? yLabels)
    {
        var xs = new EChartData.Categories(xLabels, items.Select(i => i.X));
        var ys = new EChartData.Categories(yLabels, items.Select(i => i.Y));
        var data = new JsonArray();
        var (min, max) = (double.MaxValue, double.MinValue);
        foreach (var (x, y, z) in items)
        {
            if (z is not { } height) continue;
            data.Add(new JsonArray(xs.Place(x), ys.Place(y), height));
            (min, max) = (Math.Min(min, height), Math.Max(max, height));
        }

        var chart = ThreeD(EChartType.Bar3D);
        chart.Option["xAxis3D"] = xs.Axis("x");
        chart.Option["yAxis3D"] = ys.Axis("y");

        // The floor as long and deep as the categories along it.
        var grid = (JsonObject)chart.Option["grid3D"]!;
        var (columns, rows) = (Math.Max(1, xs.Count), Math.Max(1, ys.Count));
        grid["boxWidth"] = columns >= rows ? 200 : Math.Clamp(200.0 * columns / rows, 60, 200);
        grid["boxDepth"] = columns >= rows ? Math.Clamp(200.0 * rows / columns, 60, 200) : 200;
        chart.Option["visualMap"] = ValueMap(min > max ? 0 : min, min > max ? 1 : max, ColorMapPreset.Viridis, horizontal: false, dimension: 2);
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "bar3D",
            ["shading"] = "lambert",
            ["data"] = data,
            ["label"] = new JsonObject { ["show"] = false },
            ["emphasis"] = new JsonObject { ["label"] = new JsonObject { ["show"] = true } },
            ["itemStyle"] = new JsonObject { ["opacity"] = 0.95 }
        });
        return chart;
    }
}
