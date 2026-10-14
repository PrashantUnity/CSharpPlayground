using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

// The 2D charts: the data reads as it does for Charts (numbers, [x, y] pairs, label → number maps, name → sequence maps,
// tuples, and records whose members are found by name), so the same data draws the same in both.
public sealed partial class EChart
{
    // ── Line, area, bar, scatter ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>A line chart of numbers, [x, y] pairs, a label → number map or a name → sequence map (a series each).</summary>
    public static EChart Line(object data, string? name = null) => Cartesian(EChartType.Line, ChartSpecBuilder.From(data), name);

    /// <summary>A line chart of records: each at <paramref name="x"/> (a number, or a label) with the value <paramref name="y"/>.</summary>
    public static EChart Line<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) =>
        Cartesian(EChartType.Line, ChartSpecBuilder.From(records, x, y), name);

    /// <summary>A line chart of several named series: <c>EChart.Line(("Sales", sales), ("Costs", costs))</c>.</summary>
    public static EChart Line(params (string Name, IEnumerable<double> Values)[] series) => Cartesian(EChartType.Line, Several(series), null);

    /// <summary>An area chart: a line with the space under it filled.</summary>
    public static EChart Area(object data, string? name = null) => Cartesian(EChartType.Area, ChartSpecBuilder.From(data), name);

    /// <summary>An area chart of records.</summary>
    public static EChart Area<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) =>
        Cartesian(EChartType.Area, ChartSpecBuilder.From(records, x, y), name);

    /// <summary>An area chart of several named series (piled up with <c>.Stacked()</c>).</summary>
    public static EChart Area(params (string Name, IEnumerable<double> Values)[] series) => Cartesian(EChartType.Area, Several(series), null);

    /// <summary>A bar chart.</summary>
    public static EChart Bar(object data, string? name = null) => Cartesian(EChartType.Bar, ChartSpecBuilder.From(data), name);

    /// <summary>A bar chart of records.</summary>
    public static EChart Bar<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) =>
        Cartesian(EChartType.Bar, ChartSpecBuilder.From(records, x, y), name);

    /// <summary>A bar chart of several named series, side by side (or piled up with <c>.Stacked()</c>).</summary>
    public static EChart Bar(params (string Name, IEnumerable<double> Values)[] series) => Cartesian(EChartType.Bar, Several(series), null);

    /// <summary>A scatter chart of [x, y] pairs.</summary>
    public static EChart Scatter(object data, string? name = null) => Cartesian(EChartType.Scatter, ChartSpecBuilder.From(data), name);

    /// <summary>A scatter chart of records.</summary>
    public static EChart Scatter<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) =>
        Cartesian(EChartType.Scatter, ChartSpecBuilder.From(records, x, y), name);

    /// <summary>
    /// Adds a series to a line, area, bar or scatter chart, drawn as <paramref name="kind"/> (the chart's own kind when
    /// left out): <c>EChart.Bar(revenue, "Revenue").Series("Profit", profit, EChartType.Line)</c>.
    /// </summary>
    public EChart Series(string name, object data, EChartType? kind = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var spec = ChartSpecBuilder.From(data);
        foreach (var series in spec.Series) AddCartesian(spec.Series.Count == 1 ? name : series.Name ?? name, series, CartesianKind(kind));
        return this;
    }

    /// <summary>Adds a series of records to a line, area, bar or scatter chart.</summary>
    public EChart Series<T>(string name, IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, EChartType? kind = null)
    {
        foreach (var series in ChartSpecBuilder.From(records, x, y).Series) AddCartesian(name, series, CartesianKind(kind));
        return this;
    }

    /// <summary>Lays the chart on its side: the categories run down the left, the values along the bottom.</summary>
    public EChart Horizontal()
    {
        RequireCartesian(nameof(Horizontal));
        if (_horizontal) return this;
        _horizontal = true;
        var x = Option["xAxis"];
        var y = Option["yAxis"];
        Option.Remove("xAxis");
        Option.Remove("yAxis");
        Option["xAxis"] = y;
        Option["yAxis"] = x;
        foreach (var series in AllSeries())
        {
            if (series["data"] is not JsonArray points) continue;
            foreach (var point in points.OfType<JsonArray>().Where(p => p.Count == 2).ToList())
            {
                var first = point[0];
                var second = point[1];
                point.Clear();
                point.Add(second);
                point.Add(first);
            }
        }

        return this;
    }

    /// <summary>Piles the series up (bars, lines and areas), each starting where the one under it ends.</summary>
    public EChart Stacked()
    {
        RequireCartesian(nameof(Stacked));
        _stacked = true;
        foreach (var series in AllSeries().Where(s => TypeOf(s) is "line" or "bar")) series["stack"] = "total";
        return this;
    }

    /// <summary>Draws lines and areas as smooth curves through their points.</summary>
    public EChart Smooth(bool smooth = true)
    {
        _smooth = smooth;
        foreach (var series in AllSeries().Where(s => TypeOf(s) == "line")) series["smooth"] = smooth;
        return this;
    }

    /// <summary>Writes each value on the chart (on top of a bar, beside a point, on a slice).</summary>
    public EChart ValueLabels(bool show = true)
    {
        _valueLabels = show;
        foreach (var series in AllSeries()) series["label"] = ValueLabel(TypeOf(series), show);
        return this;
    }

    /// <summary>Zooming: the wheel and a slider under the chart (or only the wheel) pick the part of the categories shown.</summary>
    public EChart Zoom(bool slider = true)
    {
        RequireCartesian(nameof(Zoom));
        var axis = _horizontal ? "yAxisIndex" : "xAxisIndex";
        var zoom = new JsonArray { new JsonObject { ["type"] = "inside", [axis] = 0 } };
        if (slider) zoom.Add(new JsonObject { ["type"] = "slider", [axis] = 0 });
        Option["dataZoom"] = zoom;
        return this;
    }

    /// <summary>A name for the axis along the bottom (or, in 3D, the x axis).</summary>
    public EChart XLabel(string label) => SetAxis('x', a => a["name"] = label);

    /// <summary>A name for the axis up the side (or, in 3D, the y axis).</summary>
    public EChart YLabel(string label) => SetAxis('y', a => a["name"] = label);

    /// <summary>The part of the x axis to show (worked out from the data when left out).</summary>
    public EChart XRange(double min, double max) => SetAxis('x', a => (a["min"], a["max"]) = (min, max));

    /// <summary>The part of the y axis to show (worked out from the data when left out).</summary>
    public EChart YRange(double min, double max) => SetAxis('y', a => (a["min"], a["max"]) = (min, max));

    /// <summary>
    /// Names the places along the chart: the categories of a line, bar or 3D chart, the slices of a pie or funnel, the
    /// spokes of a radar, the columns of a heatmap.
    /// </summary>
    public EChart Labels(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var names = labels.ToList();
        if (AllSeries().FirstOrDefault(s => TypeOf(s) is "pie" or "funnel") is { } slices && slices["data"] is JsonArray items)
        {
            for (var i = 0; i < items.Count && i < names.Count; i++)
            {
                if (items[i] is JsonObject item) item["name"] = names[i];
            }

            return this;
        }

        if (FirstObject("radar")?["indicator"] is JsonArray spokes)
        {
            for (var i = 0; i < spokes.Count && i < names.Count; i++)
            {
                if (spokes[i] is JsonObject spoke) spoke["name"] = names[i];
            }

            return this;
        }

        return SetAxis(Is3D ? 'x' : _horizontal ? 'y' : 'x', a => Categories(a, names));
    }

    /// <summary>Names the rows of a heatmap or the y places of 3D bars.</summary>
    public EChart YLabels(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var names = labels.ToList();
        return SetAxis('y', a => Categories(a, names));
    }

    private static void Categories(JsonObject axis, List<string> names)
    {
        axis["type"] = "category";
        axis["data"] = new JsonArray(names.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray());
    }

    private static EChart Cartesian(EChartType kind, ChartSpec spec, string? name)
    {
        var chart = new EChart(kind);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = kind == EChartType.Scatter ? "item" : "axis" };
        chart.Option["xAxis"] = new JsonObject { ["type"] = "category" };
        chart.Option["yAxis"] = new JsonObject { ["type"] = "value" };
        var single = spec.Series.Count == 1;
        foreach (var series in spec.Series) chart.AddCartesian(single ? name ?? series.Name : series.Name, series, kind);
        return chart;
    }

    private static ChartSpec Several((string Name, IEnumerable<double> Values)[] series) =>
        ChartSpecBuilder.FromSeries(ChartType.Line, series.Select(s => (s.Name, (IEnumerable)s.Values)));

    private EChartType CartesianKind(EChartType? kind)
    {
        RequireCartesian(nameof(Series));
        var chosen = kind ?? (Kind is EChartType.Line or EChartType.Area or EChartType.Bar or EChartType.Scatter ? Kind.Value : EChartType.Line);
        return chosen is EChartType.Line or EChartType.Area or EChartType.Bar or EChartType.Scatter
            ? chosen
            : throw new ArgumentException($"A series added to a chart with axes is a line, an area, bars or points, not {chosen}.", nameof(kind));
    }

    // One series on the axes: by label on a category axis, by number on a value axis, or by position.
    private void AddCartesian(string? name, ChartSeriesSpec spec, EChartType kind)
    {
        var categoryKey = _horizontal ? "yAxis" : "xAxis";
        var placeAxis = FirstObject(categoryKey) ?? new JsonObject { ["type"] = "category" };
        if (FirstObject(categoryKey) == null) Option[categoryKey] = placeAxis;
        var data = new JsonArray();

        if (spec.Labels != null && spec.Labels.Any(l => l != null))
        {
            placeAxis["type"] = "category";
            var names = placeAxis["data"] as JsonArray ?? new JsonArray();
            placeAxis["data"] = names;
            var index = new Dictionary<string, int>();
            for (var i = 0; i < names.Count; i++) index.TryAdd(names[i]?.ToString() ?? string.Empty, i);
            var values = new SortedDictionary<int, double?>();
            for (var i = 0; i < spec.Y.Count; i++)
            {
                var label = spec.Labels[i] ?? (spec.X?[i] is { } x ? x.ToString(CultureInfo.InvariantCulture) : i.ToString(CultureInfo.InvariantCulture));
                if (!index.TryGetValue(label, out var at))
                {
                    at = names.Count;
                    names.Add(JsonValue.Create(label));
                    index[label] = at;
                }

                values[at] = spec.Y[i];
            }

            for (var i = 0; i < names.Count; i++) data.Add(values.TryGetValue(i, out var y) ? Number(y) : null);
        }
        else if (spec.X != null && spec.X.Any(v => v != null) && placeAxis["data"] == null)
        {
            placeAxis["type"] = "value";
            for (var i = 0; i < spec.Y.Count; i++)
            {
                var pair = _horizontal ? new JsonArray(Number(spec.Y[i]), Number(spec.X[i])) : new JsonArray(Number(spec.X[i]), Number(spec.Y[i]));
                data.Add(pair);
            }
        }
        else
        {
            if (placeAxis["type"]?.ToString() == "category" && placeAxis["data"] is not JsonArray)
            {
                placeAxis["data"] = new JsonArray(Enumerable.Range(0, spec.Y.Count).Select(i => (JsonNode?)JsonValue.Create(i.ToString(CultureInfo.InvariantCulture))).ToArray());
            }

            foreach (var y in spec.Y) data.Add(Number(y));
        }

        var series = new JsonObject();
        if (name != null) series["name"] = name;
        series["type"] = kind switch { EChartType.Bar => "bar", EChartType.Scatter => "scatter", _ => "line" };
        if (kind == EChartType.Area) series["areaStyle"] = new JsonObject();
        if (kind is EChartType.Line or EChartType.Area && _smooth) series["smooth"] = true;
        if (kind is not EChartType.Scatter && _stacked) series["stack"] = "total";
        if (_valueLabels) series["label"] = ValueLabel(series["type"]!.ToString(), true);
        if (spec.Color != null) series["itemStyle"] = new JsonObject { ["color"] = spec.Color };
        series["data"] = data;
        SeriesList().Add(series);
    }

    private JsonObject ValueLabel(string? type, bool show)
    {
        var label = new JsonObject { ["show"] = show };
        if (type is "bar" or "line" or "scatter") label["position"] = type == "bar" && _horizontal ? "right" : "top";
        return label;
    }

    // ── Pie, donut, funnel ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A pie chart: each label → number is a slice.</summary>
    public static EChart Pie(object data) => Slices(EChartType.Pie, ChartSpecBuilder.From(data));

    /// <summary>A pie chart of records: each is a slice named by <paramref name="label"/>, as big as <paramref name="value"/>.</summary>
    public static EChart Pie<T>(IEnumerable<T> records, Func<T, object?> label, Func<T, object?> value) => Slices(EChartType.Pie, ChartSpecBuilder.From(records, label, value));

    /// <summary>A donut chart: a pie with a hole.</summary>
    public static EChart Donut(object data) => Slices(EChartType.Donut, ChartSpecBuilder.From(data));

    /// <summary>A donut chart of records.</summary>
    public static EChart Donut<T>(IEnumerable<T> records, Func<T, object?> label, Func<T, object?> value) => Slices(EChartType.Donut, ChartSpecBuilder.From(records, label, value));

    /// <summary>A funnel: each label → number is a stage, widest first.</summary>
    public static EChart Funnel(object data) => Slices(EChartType.Funnel, ChartSpecBuilder.From(data));

    /// <summary>A funnel of records.</summary>
    public static EChart Funnel<T>(IEnumerable<T> records, Func<T, object?> label, Func<T, object?> value) => Slices(EChartType.Funnel, ChartSpecBuilder.From(records, label, value));

    private static EChart Slices(EChartType kind, ChartSpec spec)
    {
        var chart = new EChart(kind);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "item" };
        var source = spec.Series.FirstOrDefault() ?? new ChartSeriesSpec();
        var items = new JsonArray();
        for (var i = 0; i < source.Y.Count; i++)
        {
            var name = source.Labels?[i] ?? (source.X?[i] is { } x ? x.ToString(CultureInfo.InvariantCulture) : (i + 1).ToString(CultureInfo.InvariantCulture));
            items.Add(new JsonObject { ["name"] = name, ["value"] = Number(source.Y[i]) });
        }

        var series = new JsonObject { ["type"] = kind == EChartType.Funnel ? "funnel" : "pie" };
        if (source.Name != null) series["name"] = source.Name;
        switch (kind)
        {
            case EChartType.Pie:
                series["radius"] = "62%";
                break;
            case EChartType.Donut:
                series["radius"] = new JsonArray("40%", "68%");
                series["itemStyle"] = new JsonObject { ["borderRadius"] = 6, ["borderWidth"] = 2, ["borderColor"] = "rgba(0,0,0,0)" };
                break;
            case EChartType.Funnel:
                series["sort"] = "descending";
                series["left"] = "10%";
                series["width"] = "80%";
                series["label"] = new JsonObject { ["show"] = true, ["position"] = "inside" };
                break;
        }

        series["data"] = items;
        chart.SeriesList().Add(series);
        return chart;
    }

    // ── Radar ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A radar chart: a polygon for each series over spokes named by <paramref name="spokes"/> (or by the labels of the
    /// data). Each spoke runs from 0 to <paramref name="max"/>, or to a round number above the largest value on it.
    /// </summary>
    public static EChart Radar(object data, IEnumerable<string>? spokes = null, double? max = null)
    {
        var spec = ChartSpecBuilder.From(data);
        var count = spec.Series.Count == 0 ? 0 : spec.Series.Max(s => s.Y.Count);
        var names = spokes?.ToList() ?? spec.Series.FirstOrDefault(s => s.Labels != null)?.Labels?.Select((l, i) => l ?? $"{i + 1}").ToList()
                    ?? Enumerable.Range(1, count).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

        var indicator = new JsonArray();
        for (var i = 0; i < names.Count; i++)
        {
            var top = max ?? NiceCeiling(spec.Series.Select(s => i < s.Y.Count && s.Y[i] is { } v ? v : 0).DefaultIfEmpty(0).Max());
            indicator.Add(new JsonObject { ["name"] = names[i], ["max"] = top });
        }

        var chart = new EChart(EChartType.Radar);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "item" };
        chart.Option["radar"] = new JsonObject { ["indicator"] = indicator, ["radius"] = "62%" };
        var items = new JsonArray();
        for (var s = 0; s < spec.Series.Count; s++)
        {
            var series = spec.Series[s];
            items.Add(new JsonObject
            {
                ["name"] = series.Name ?? (spec.Series.Count == 1 ? "Values" : $"Series {s + 1}"),
                ["value"] = new JsonArray(series.Y.Select(Number).ToArray())
            });
        }

        chart.SeriesList().Add(new JsonObject { ["type"] = "radar", ["areaStyle"] = new JsonObject { ["opacity"] = 0.2 }, ["data"] = items });
        return chart;
    }

    // A round number at or above the value: 7 → 10, 42 → 50, 0.3 → 0.5.
    private static double NiceCeiling(double value)
    {
        if (value <= 0) return 1;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in new[] { 1, 2, 2.5, 5, 10 })
        {
            if (step * magnitude >= value) return step * magnitude;
        }

        return 10 * magnitude;
    }

    // ── Heatmap ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A heatmap of a grid of numbers (<c>double[,]</c> or rows of numbers): a row for each y, a column for each x, the
    /// first row at the top. A missing number (null, NaN) is an empty cell.
    /// </summary>
    public static EChart Heatmap(object values, IEnumerable<string>? xLabels = null, IEnumerable<string>? yLabels = null)
    {
        var rows = EChartData.Grid(values, nameof(values));
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var chart = new EChart(EChartType.Heatmap);
        chart.Option["tooltip"] = new JsonObject { ["position"] = "top" };
        chart.Option["xAxis"] = new JsonObject { ["type"] = "category", ["splitArea"] = new JsonObject { ["show"] = true }, ["data"] = Names(xLabels, columns) };
        chart.Option["yAxis"] = new JsonObject { ["type"] = "category", ["inverse"] = true, ["splitArea"] = new JsonObject { ["show"] = true }, ["data"] = Names(yLabels, rows.Count) };

        var data = new JsonArray();
        var (min, max) = (double.MaxValue, double.MinValue);
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Count; c++)
            {
                if (rows[r][c] is not { } v) continue;
                data.Add(new JsonArray(c, r, v));
                (min, max) = (Math.Min(min, v), Math.Max(max, v));
            }
        }

        if (data.Count == 0) (min, max) = (0, 1);
        chart.Option["visualMap"] = ValueMap(min, max, ColorMapPreset.Viridis, horizontal: true);
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "heatmap",
            ["data"] = data,
            ["label"] = new JsonObject { ["show"] = rows.Count * columns <= 144 },
            ["emphasis"] = new JsonObject { ["itemStyle"] = new JsonObject { ["shadowBlur"] = 8 } }
        });
        return chart;
    }

    private static JsonArray Names(IEnumerable<string>? labels, int count) =>
        new((labels?.ToList() ?? Enumerable.Range(0, count).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList())
            .Select(n => (JsonNode?)JsonValue.Create(n)).ToArray());

    // A continuous colour scale for the chart's values; horizontal under a 2D chart, upright beside a 3D one.
    private static JsonObject ValueMap(double min, double max, ColorMapPreset preset, bool horizontal, int? dimension = null)
    {
        if (min >= max) (min, max) = (min - 1, max + 1);
        var map = new JsonObject
        {
            ["min"] = min,
            ["max"] = max,
            ["calculable"] = true,
            ["inRange"] = new JsonObject { ["color"] = new JsonArray(Stops(preset).Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()) }
        };
        if (dimension is { } d) map["dimension"] = d;
        if (horizontal)
        {
            map["orient"] = "horizontal";
            map["left"] = "center";
            map["bottom"] = 0;
        }
        else
        {
            map["right"] = 8;
            map["top"] = "middle";
            map["itemHeight"] = 140;
        }

        return map;
    }

    // ── Candlestick ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A candlestick chart of prices: items of [open, close, low, high] (or [date, open, close, low, high]), or records
    /// with open, close, low and high (and a date) members.
    /// </summary>
    public static EChart Candlestick(object data, IEnumerable<string>? dates = null)
    {
        var candles = EChartData.Candles(data, nameof(data));
        var labels = dates?.ToList() ?? candles.Select((c, i) => c.Date ?? (i + 1).ToString(CultureInfo.InvariantCulture)).ToList();
        return CandlestickChart(labels, candles.Select(c => (c.Open, c.Close, c.Low, c.High)).ToList());
    }

    /// <summary>A candlestick chart of records, each a day (or any period) named by <paramref name="date"/>.</summary>
    public static EChart Candlestick<T>(IEnumerable<T> records, Func<T, object?> date, Func<T, double> open, Func<T, double> close, Func<T, double> low, Func<T, double> high)
    {
        ArgumentNullException.ThrowIfNull(records);
        var list = records.ToList();
        return CandlestickChart(list.Select(r => EChartData.DateText(date(r))).ToList(), list.Select(r => (open(r), close(r), low(r), high(r))).ToList());
    }

    private static EChart CandlestickChart(List<string> dates, List<(double Open, double Close, double Low, double High)> candles)
    {
        var chart = new EChart(EChartType.Candlestick);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "axis", ["axisPointer"] = new JsonObject { ["type"] = "cross" } };
        chart.Option["xAxis"] = new JsonObject { ["type"] = "category", ["data"] = new JsonArray(dates.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray()) };
        chart.Option["yAxis"] = new JsonObject { ["type"] = "value", ["scale"] = true };
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "candlestick",
            ["data"] = new JsonArray(candles.Select(c => (JsonNode?)new JsonArray(Number(c.Open), Number(c.Close), Number(c.Low), Number(c.High))).ToArray())
        });
        return chart;
    }

    // ── Gauge ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A dial showing <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>.</summary>
    public static EChart Gauge(double value, double max = 100, string? name = null, double min = 0)
    {
        if (!(max > min)) throw new ArgumentException($"A gauge's max ({max}) must be above its min ({min}).", nameof(max));
        var chart = new EChart(EChartType.Gauge);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "item" };
        var item = new JsonObject { ["value"] = Number(value) };
        if (name != null) item["name"] = name;
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "gauge",
            ["min"] = min,
            ["max"] = max,
            ["progress"] = new JsonObject { ["show"] = true },
            ["detail"] = new JsonObject { ["valueAnimation"] = true, ["formatter"] = "{value}" },
            ["data"] = new JsonArray(item)
        });
        return chart;
    }

    // ── Hierarchies and flows ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A treemap: nested rectangles as big as their values. The tree is a map (name → number, or name → a map of what
    /// is inside), or records with a name, a value and children.
    /// </summary>
    public static EChart Treemap(object tree)
    {
        var chart = new EChart(EChartType.Treemap);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "item" };
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "treemap",
            ["roam"] = false,
            ["label"] = new JsonObject { ["show"] = true },
            ["upperLabel"] = new JsonObject { ["show"] = true, ["height"] = 22 },
            ["data"] = EChartData.Tree(tree, nameof(tree))
        });
        return chart;
    }

    /// <summary>A sunburst: rings of a tree, each part as wide as its value. The tree reads as for <see cref="Treemap"/>.</summary>
    public static EChart Sunburst(object tree)
    {
        var chart = new EChart(EChartType.Sunburst);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "item" };
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "sunburst",
            ["radius"] = new JsonArray(0, "90%"),
            ["label"] = new JsonObject { ["rotate"] = "radial" },
            ["data"] = EChartData.Tree(tree, nameof(tree))
        });
        return chart;
    }

    /// <summary>
    /// A Sankey diagram of flows: (from, to, amount) tuples or records with source/from, target/to and value members.
    /// Flows can't go round in a circle.
    /// </summary>
    public static EChart Sankey(object links)
    {
        var flows = EChartData.Links(links, nameof(links));
        var chart = new EChart(EChartType.Sankey);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "item" };
        chart.SeriesList().Add(new JsonObject
        {
            ["type"] = "sankey",
            ["emphasis"] = new JsonObject { ["focus"] = "adjacency" },
            ["lineStyle"] = new JsonObject { ["color"] = "gradient", ["curveness"] = 0.5 },
            ["data"] = new JsonArray(EChartData.NodeNames(flows).Select(n => (JsonNode?)new JsonObject { ["name"] = n }).ToArray()),
            ["links"] = new JsonArray(flows.Select(f => (JsonNode?)new JsonObject { ["source"] = f.From, ["target"] = f.To, ["value"] = Number(f.Value ?? 1) }).ToArray())
        });
        return chart;
    }

    /// <summary>
    /// A network laid out by forces: an adjacency map (node → the nodes it links to), or links as for <see cref="Sankey"/>.
    /// Nodes with more links are drawn bigger; drag them, scroll to zoom.
    /// </summary>
    public static EChart Graph(object data, bool directed = false)
    {
        var links = EChartData.Links(data, nameof(data));
        var degree = new Dictionary<string, int>();
        foreach (var (from, to, _) in links)
        {
            degree[from] = degree.GetValueOrDefault(from) + 1;
            degree[to] = degree.GetValueOrDefault(to) + 1;
        }

        var chart = new EChart(EChartType.Graph);
        chart.Option["tooltip"] = new JsonObject { ["trigger"] = "item" };
        var series = new JsonObject
        {
            ["type"] = "graph",
            ["layout"] = "force",
            ["roam"] = true,
            ["draggable"] = true,
            ["label"] = new JsonObject { ["show"] = true, ["position"] = "right" },
            ["force"] = new JsonObject { ["repulsion"] = 420, ["gravity"] = 0.06, ["edgeLength"] = new JsonArray(80, 180) },
            ["data"] = new JsonArray(EChartData.NodeNames(links, data).Select(n => (JsonNode?)new JsonObject
            {
                ["name"] = n,
                ["value"] = degree.GetValueOrDefault(n),
                ["symbolSize"] = 16 + 5 * Math.Min(degree.GetValueOrDefault(n), 8)
            }).ToArray()),
            ["links"] = new JsonArray(links.Select(l =>
            {
                var link = new JsonObject { ["source"] = l.From, ["target"] = l.To };
                if (l.Value is { } v) link["value"] = Number(v);
                return (JsonNode?)link;
            }).ToArray())
        };
        if (directed) series["edgeSymbol"] = new JsonArray("none", "arrow");
        chart.SeriesList().Add(series);
        return chart;
    }

    // ── Shared ───────────────────────────────────────────────────────────────────────────────────────────────────

    private bool IsCartesian => FirstObject("xAxis") != null && FirstObject("yAxis") != null && !Is3D;

    private bool Is3D => Option.ContainsKey("grid3D") || Option.ContainsKey("xAxis3D");

    private void RequireCartesian(string setting)
    {
        if (!IsCartesian) throw new InvalidOperationException($"{setting} is for charts with x and y axes (line, area, bar, scatter, candlestick, heatmap); this one has none.");
    }

    // The x, y or z axis (3D when the chart is), changed by setting.
    private EChart SetAxis(char axis, Action<JsonObject> change)
    {
        var key = Is3D ? $"{axis}Axis3D" : $"{axis}Axis";
        var found = FirstObject(key) ?? throw new InvalidOperationException(axis == 'z'
            ? "A z axis is for 3D charts (surface, 3D bars, 3D points)."
            : $"This chart has no {axis} axis to set: axes are for line, area, bar, scatter, candlestick, heatmap and 3D charts.");
        change(found);
        return this;
    }

    /// <summary>A chart of <paramref name="data"/> drawn as <paramref name="kind"/>, the way the factory of that kind reads it.</summary>
    internal static EChart Of(object data, EChartType kind)
    {
        ArgumentNullException.ThrowIfNull(data);
        return kind switch
        {
            EChartType.Line => Line(data),
            EChartType.Area => Area(data),
            EChartType.Bar => Bar(data),
            EChartType.Scatter => Scatter(data),
            EChartType.Pie => Pie(data),
            EChartType.Donut => Donut(data),
            EChartType.Funnel => Funnel(data),
            EChartType.Radar => Radar(data),
            EChartType.Heatmap => Heatmap(data),
            EChartType.Candlestick => Candlestick(data),
            EChartType.Gauge => DataReader.TryPresentNumber(data, out var value)
                ? Gauge(value)
                : throw new ArgumentException($"A gauge shows one number, not a {data.GetType().Name}.", nameof(data)),
            EChartType.Treemap => Treemap(data),
            EChartType.Sunburst => Sunburst(data),
            EChartType.Sankey => Sankey(data),
            EChartType.Graph => Graph(data),
            EChartType.Surface => data is Func<double, double, double> function ? Surface(function) : Surface(data),
            EChartType.Bar3D => Bar3D(data),
            EChartType.Scatter3D => Scatter3D(data),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    /// <summary>A chart of records with x and y: lines, areas, bars, points, pies, donuts and funnels.</summary>
    internal static EChart Of<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, EChartType kind) => kind switch
    {
        EChartType.Line => Line(records, x, y),
        EChartType.Area => Area(records, x, y),
        EChartType.Bar => Bar(records, x, y),
        EChartType.Scatter => Scatter(records, x, y),
        EChartType.Pie => Pie(records, x, y),
        EChartType.Donut => Donut(records, x, y),
        EChartType.Funnel => Funnel(records, x, y),
        _ => throw new ArgumentException($"An x and a y make a line, area, bar, scatter, pie, donut or funnel chart; for {kind} use EChart.{kind}(...).", nameof(kind))
    };

    /// <summary>A chart of records with x, y and z: 3D bars or 3D points.</summary>
    internal static EChart Of<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, Func<T, object?> z, EChartType kind) => kind switch
    {
        EChartType.Bar3D => Bar3D(records, x, y, z),
        EChartType.Scatter3D => Scatter3D(records, x, y, z),
        _ => throw new ArgumentException($"An x, a y and a z make 3D bars or 3D points (EChartType.Bar3D or Scatter3D), not {kind}.", nameof(kind))
    };
}
