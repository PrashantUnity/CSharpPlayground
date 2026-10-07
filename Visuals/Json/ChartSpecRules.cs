namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>What a chart spec must satisfy beyond its JSON shape.</summary>
internal static class ChartSpecRules
{
    public const int MaxBins = 50;

    public static void Check(VisualSpecIssues issues, ChartSpec chart)
    {
        if (chart.Bins is { } bins && (bins < 1 || bins > MaxBins)) issues.Add("$.bins", $"must be between 1 and {MaxBins}");
        CheckAxis(issues, "$.xAxis", chart.XAxis);
        CheckAxis(issues, "$.yAxis", chart.YAxis);
        CheckAxis(issues, "$.y2Axis", chart.Y2Axis);
        CheckChartSettings(issues, chart);

        for (var i = 0; i < chart.Series.Count && !issues.IsFull; i++)
        {
            var path = $"$.series[{i}]";
            var series = chart.Series[i];
            if (chart.Kind == ChartType.Histogram) CheckHistogramSeries(issues, path, series);
            else CheckSeries(issues, path, chart, i, series);
        }

        CheckScales(issues, chart);
    }

    internal static void CheckAxis(VisualSpecIssues issues, string path, AxisSpec axis)
    {
        issues.Finite($"{path}.min", axis.Min);
        issues.Finite($"{path}.max", axis.Max);
        issues.Finite($"{path}.suggestedMin", axis.SuggestedMin);
        issues.Finite($"{path}.suggestedMax", axis.SuggestedMax);
        if (axis.Min is { } min && axis.Max is { } max && min >= max) issues.Add(path, "min must be below max");
        if (axis.SuggestedMin is { } sMin && axis.SuggestedMax is { } sMax && sMin >= sMax) issues.Add(path, "suggestedMin must be below suggestedMax");
    }

    // The kind a series is drawn as: its own in a combo, otherwise the chart's.
    internal static ChartType KindOf(ChartSpec chart, ChartSeriesSpec series) => series.Kind ?? (chart.Kind == ChartType.Histogram ? ChartType.Bar : chart.Kind);

    private static bool IsRound(ChartType kind) => kind is ChartType.Pie or ChartType.Donut or ChartType.PolarArea;
    private static bool IsRadial(ChartType kind) => IsRound(kind) || kind == ChartType.Radar;
    private static bool IsLineLike(ChartType kind) => kind is ChartType.Line or ChartType.Area;
    private static bool Stacks(ChartType kind) => kind is ChartType.Bar or ChartType.Line or ChartType.Area;
    private static bool HasMarkers(ChartType kind) => kind is ChartType.Line or ChartType.Area or ChartType.Scatter or ChartType.Bubble or ChartType.Radar;
    private static string Name(ChartType kind) => VisualJsonErrors.JsonName(kind);

    private static void CheckChartSettings(VisualSpecIssues issues, ChartSpec chart)
    {
        var kind = chart.Kind;
        var kinds = chart.Series.Select(s => KindOf(chart, s)).ToList();

        if (chart.Orientation == ChartOrientation.Horizontal && (kinds.Count == 0 || kinds.Any(k => k != ChartType.Bar)))
        {
            issues.Add("$.orientation", "horizontal is for bar charts: every series must be bars");
        }

        if (chart.Stack is ChartStack.Stacked or ChartStack.Percent && !kinds.Any(Stacks))
        {
            issues.Add("$.stack", $"only bars, lines and areas stack; a {Name(kind)} chart doesn't");
        }

        if (chart.StartAngle != null || chart.Sweep != null)
        {
            if (!IsRound(kind)) issues.Add(chart.StartAngle != null ? "$.startAngle" : "$.sweep", $"startAngle and sweep are for pie, donut and polarArea charts, not {Name(kind)}");
            issues.Finite("$.startAngle", chart.StartAngle);
            issues.Finite("$.sweep", chart.Sweep);
            if (chart.Sweep is { } sweep && double.IsFinite(sweep) && (sweep <= 0 || sweep > 360)) issues.Add("$.sweep", "must be above 0 and at most 360 degrees");
        }

        if (chart.Cutout is { } cutout)
        {
            if (kind != ChartType.Donut) issues.Add("$.cutout", $"only a donut has a hole; this chart is a {Name(kind)}");
            else if (!(double.IsFinite(cutout) && cutout >= 0 && cutout <= 0.95)) issues.Add("$.cutout", "must be between 0 and 0.95");
        }

        var usesRight = chart.Series.Any(s => s.Axis == AxisSide.Right);
        if (!chart.Y2Axis.IsEmpty && !usesRight) issues.Add("$.y2Axis", "no series is on the right axis; set a series' axis to right, or leave y2Axis out");

        // Axes only mean something on charts that have them; a radar and a polar area use only the range of their value scale.
        foreach (var (axisPath, axis) in new[] { ("$.xAxis", chart.XAxis), ("$.yAxis", chart.YAxis), ("$.y2Axis", chart.Y2Axis) })
        {
            if (IsRadial(kind) && (axis.Scale != null || axis.Reverse != null)) issues.Add(axisPath, $"a {Name(kind)} chart has no axis to scale or reverse");
            if (kind is ChartType.Pie or ChartType.Donut && (axis.SuggestedMin != null || axis.SuggestedMax != null)) issues.Add(axisPath, $"a {Name(kind)} chart has no axis to suggest a range for");
        }

        if (kind == ChartType.PolarArea && chart.Series.Count > 1) issues.Add("$.series", "a polarArea chart draws one series; use a pie or donut for rings, or a radar for several");
    }

    private static void CheckSeries(VisualSpecIssues issues, string path, ChartSpec chart, int index, ChartSeriesSpec series)
    {
        var kind = KindOf(chart, series);
        var name = Name(kind);
        if (series.Values != null) issues.Add($"{path}.values", "only a histogram counts values; give this series its y instead");

        var count = series.Y.Count;
        if (series.LineWidth is { } lineWidth && !(double.IsFinite(lineWidth) && lineWidth > 0)) issues.Add($"{path}.lineWidth", "must be a positive number of pixels");
        issues.Finite($"{path}.y", series.Y);
        issues.Finite($"{path}.x", series.X);
        issues.SameLength($"{path}.x", series.X, "y", count);
        issues.SameLength($"{path}.labels", series.Labels, "y", count);
        issues.SameLength($"{path}.colors", series.Colors, "y", count);
        issues.SameLength($"{path}.ids", series.Ids, "y", count);

        if (IsRound(kind))
        {
            for (var i = 0; i < count; i++)
            {
                if (series.Y[i] is < 0) issues.Add($"{path}.y[{i}]", "a slice can't be negative");
            }
        }

        if (series.Kind is { } own)
        {
            if (chart.Kind is ChartType.Pie or ChartType.Donut or ChartType.PolarArea or ChartType.Radar or ChartType.Bubble)
            {
                issues.Add($"{path}.kind", $"a {Name(chart.Kind)} chart draws all its series the same way; kind is for mixing line, area, bar and scatter series");
            }
            else if (own is not (ChartType.Line or ChartType.Area or ChartType.Bar or ChartType.Scatter))
            {
                issues.Add($"{path}.kind", "a series in a mixed chart is line, area, bar or scatter");
            }
        }

        string Only(string what) => $"only {what} {(what.EndsWith('s') ? "have" : "has")} this; this series is a {name}";
        void LineOnly(string field, bool present)
        {
            if (present && !IsLineLike(kind)) issues.Add($"{path}.{field}", Only("a line or area"));
        }

        LineOnly("interpolation", series.Interpolation != null);
        LineOnly("tension", series.Tension != null);
        LineOnly("step", series.Step != null);
        LineOnly("fillTo", series.FillTo != null);
        LineOnly("colorSegments", series.ColorSegments != null);
        if (series.Dash != null && !(IsLineLike(kind) || kind == ChartType.Radar)) issues.Add($"{path}.dash", Only("a line, area or radar"));
        if (series.Fill != null && !(IsLineLike(kind) || kind == ChartType.Radar)) issues.Add($"{path}.fill", Only("a line, area or radar"));
        if ((series.PointStyle != null || series.PointRadius != null) && !HasMarkers(kind)) issues.Add($"{path}.{(series.PointStyle != null ? "pointStyle" : "pointRadius")}", Only("a line, area, scatter, bubble or radar"));

        if (series.ColorSegments == true && IsLineLike(kind) && (series.Interpolation is LineInterpolation.Smooth or LineInterpolation.Monotone || series.Step is LineStep.Before or LineStep.After or LineStep.Middle))
        {
            issues.Add($"{path}.colorSegments", "coloured segments are straight lines between values; leave interpolation and step out");
        }

        if (series.Tension is { } tension && !(double.IsFinite(tension) && tension >= 0 && tension <= 1)) issues.Add($"{path}.tension", "must be between 0 and 1");
        if (series.Tension != null && series.Interpolation != LineInterpolation.Smooth && IsLineLike(kind)) issues.Add($"{path}.tension", "only a smooth line has a tension; set interpolation to smooth");
        if (series.Step is { } step && step != LineStep.None && series.Interpolation is LineInterpolation.Smooth or LineInterpolation.Monotone) issues.Add($"{path}.step", "a stepped line is straight between its steps; leave interpolation out");
        if (series.PointRadius is { } radius && !(double.IsFinite(radius) && radius >= 0)) issues.Add($"{path}.pointRadius", "must be a number of pixels, 0 or more");

        if (series.FillTo is { } target && IsLineLike(kind))
        {
            if (target < 0 || target >= chart.Series.Count) issues.Add($"{path}.fillTo", $"there is no series {target}: the chart has {chart.Series.Count}");
            else if (target == index) issues.Add($"{path}.fillTo", "a series can't fill to itself");
            if (series.Fill == false) issues.Add($"{path}.fillTo", "fill is off; turn it on or leave fillTo out");
        }

        if (kind == ChartType.Bubble)
        {
            if (series.Sizes == null) issues.Add($"{path}.sizes", "a bubble chart needs sizes: a radius in pixels for each value");
            else
            {
                issues.SameLength($"{path}.sizes", series.Sizes, "y", count);
                issues.Finite($"{path}.sizes", series.Sizes);
                for (var i = 0; i < series.Sizes.Count && !issues.IsFull; i++)
                {
                    if (series.Sizes[i] is <= 0) issues.Add($"{path}.sizes[{i}]", "a bubble's radius must be above 0");
                }
            }
        }
        else if (series.Sizes != null) issues.Add($"{path}.sizes", $"only a bubble chart has sizes; this series is a {name}");

        if (series.From != null)
        {
            if (kind != ChartType.Bar) issues.Add($"{path}.from", Only("bars"));
            issues.SameLength($"{path}.from", series.From, "y", count);
            issues.Finite($"{path}.from", series.From);
        }

        if (series.CornerRadius is { } corner)
        {
            if (kind != ChartType.Bar) issues.Add($"{path}.cornerRadius", Only("bars"));
            else if (!(double.IsFinite(corner) && corner >= 0)) issues.Add($"{path}.cornerRadius", "must be a number of pixels, 0 or more");
        }

        if (series.Axis == AxisSide.Right && (IsRadial(kind) || chart.Kind is ChartType.Radar or ChartType.Pie or ChartType.Donut or ChartType.PolarArea))
        {
            issues.Add($"{path}.axis", $"a {Name(chart.Kind)} chart has no right axis");
        }

        if (series.Stack != null)
        {
            if (!Stacks(kind)) issues.Add($"{path}.stack", Only("bars, lines and areas"));
            else if (chart.Stack is null or ChartStack.None) issues.Add($"{path}.stack", "the chart isn't stacked: set the chart's stack to stacked or percent");
        }

        if (chart.Stack == ChartStack.Percent && Stacks(kind))
        {
            for (var i = 0; i < count && !issues.IsFull; i++)
            {
                if (series.Y[i] is < 0) issues.Add($"{path}.y[{i}]", "a percent stack can't hold a negative value");
            }
        }
    }

    // Log axes need values above zero, a time axis needs x in milliseconds, and only x can be a time or a category.
    private static void CheckScales(VisualSpecIssues issues, ChartSpec chart)
    {
        if (chart.YAxis.Scale is AxisScale.Time or AxisScale.Category) issues.Add("$.yAxis.scale", "the y axis holds numbers: linear or log");
        if (chart.Y2Axis.Scale is AxisScale.Time or AxisScale.Category) issues.Add("$.y2Axis.scale", "the y axis holds numbers: linear or log");
        if (chart.Kind == ChartType.Histogram && (chart.XAxis.Scale != null || chart.YAxis.Scale == AxisScale.Log)) issues.Add("$.xAxis.scale", "a histogram's bins are on a linear axis");

        for (var i = 0; i < chart.Series.Count && !issues.IsFull; i++)
        {
            var series = chart.Series[i];
            var path = $"$.series[{i}]";
            if (chart.Kind == ChartType.Histogram) continue;

            if (chart.XAxis.Scale == AxisScale.Time && series.X == null) issues.Add($"{path}.x", "a time axis needs x: Unix milliseconds for each value");
            var yScale = series.Axis == AxisSide.Right ? chart.Y2Axis.Scale : chart.YAxis.Scale;
            if (yScale == AxisScale.Log) CheckAbove0(issues, $"{path}.y", series.Y, "a log axis");
            if (chart.XAxis.Scale == AxisScale.Log)
            {
                if (series.X == null) issues.Add($"{path}.x", "a log x axis needs x: values above 0 (the default x starts at 0)");
                else CheckAbove0(issues, $"{path}.x", series.X, "a log axis");
            }
        }
    }

    private static void CheckAbove0(VisualSpecIssues issues, string path, IReadOnlyList<double?> values, string what)
    {
        for (var i = 0; i < values.Count && !issues.IsFull; i++)
        {
            if (values[i] is <= 0) issues.Add($"{path}[{i}]", $"{what} can't show {values[i]:0.##}: values must be above 0 (use null for a missing value)");
        }
    }

    private static void CheckHistogramSeries(VisualSpecIssues issues, string path, ChartSeriesSpec series)
    {
        if (series.Values == null) issues.Add($"{path}.values", "a histogram series needs values: the samples to count");
        if (series.Y.Count > 0) issues.Add($"{path}.y", "a histogram counts its values itself; give the samples as values, not y");
        issues.Finite($"{path}.values", series.Values);

        // A histogram's bars are the counts, drawn one way: nothing that styles a line, a bubble or a combo applies.
        (string Field, bool Set)[] styling =
        [
            ("kind", series.Kind != null), ("axis", series.Axis != null), ("stack", series.Stack != null), ("dash", series.Dash != null),
            ("interpolation", series.Interpolation != null), ("tension", series.Tension != null), ("step", series.Step != null),
            ("fill", series.Fill != null), ("fillTo", series.FillTo != null), ("pointStyle", series.PointStyle != null),
            ("pointRadius", series.PointRadius != null), ("colorSegments", series.ColorSegments != null), ("sizes", series.Sizes != null),
            ("from", series.From != null), ("cornerRadius", series.CornerRadius != null)
        ];
        foreach (var (field, set) in styling)
        {
            if (set) issues.Add($"{path}.{field}", "a histogram draws bars of the counts of its values; this setting is for other charts");
        }
    }
}

/// <summary>What a 3D plot spec must satisfy beyond its JSON shape.</summary>
internal static class Plot3DSpecRules
{
    public static void Check(VisualSpecIssues issues, Plot3DSpec plot)
    {
        ChartSpecRules.CheckAxis(issues, "$.xAxis", plot.XAxis);
        ChartSpecRules.CheckAxis(issues, "$.yAxis", plot.YAxis);
        ChartSpecRules.CheckAxis(issues, "$.zAxis", plot.ZAxis);
        foreach (var (path, axis) in new[] { ("$.xAxis", plot.XAxis), ("$.yAxis", plot.YAxis), ("$.zAxis", plot.ZAxis) })
        {
            if (axis.HasChartOnlySettings) issues.Add(path, "a 3D axis has a title and a range; scale, suggestedMin, suggestedMax and reverse are for 2D charts");
        }

        var kindName = VisualJsonErrors.JsonName(plot.Kind);
        switch (plot.Kind)
        {
            case Plot3DType.Surface or Plot3DType.Wireframe:
                if (plot.Surface == null) issues.Add("$.surface", $"a {kindName} plot needs a surface: its grid of heights");
                else CheckSurface(issues, plot.Surface);
                if (plot.Series.Count > 0) issues.Add("$.series", $"a {kindName} plot draws its surface; series are for scatter, trajectory and voxel-bar plots");
                break;
            case Plot3DType.Graph3D:
                if (plot.Graph == null) issues.Add("$.graph", "a graph plot needs a graph: its nodes and edges");
                else GraphSpecRules.Check(issues, "$.graph", plot.Graph);
                for (var i = 0; plot.Graph != null && i < plot.Graph.Nodes.Count; i++)
                {
                    var node = plot.Graph.Nodes[i];
                    if (node.State != null || node.Pointer != null || node.Note != null || node.Notes != null)
                    {
                        issues.Add($"$.graph.nodes[{i}]", "a 3D graph node has a label, a place, a colour and a size; states, pointers and notes are for graph visualizers");
                    }
                }

                for (var i = 0; plot.Graph != null && i < plot.Graph.Edges.Count; i++)
                {
                    if (plot.Graph.Edges[i].State != null || plot.Graph.Edges[i].Label != null)
                    {
                        issues.Add($"$.graph.edges[{i}]", "a 3D graph edge has a weight, a colour and a direction; states and labels are for graph visualizers");
                    }
                }
                if (plot.Series.Count > 0) issues.Add("$.series", "a graph plot draws its graph; series are for scatter, trajectory and voxel-bar plots");
                break;
            default:
                if (plot.Surface != null) issues.Add("$.surface", $"a {kindName} plot draws series; a surface is for surface and wireframe plots");
                if (plot.Graph != null) issues.Add("$.graph", $"a {kindName} plot draws series; a graph is for graph plots");
                for (var i = 0; i < plot.Series.Count && !issues.IsFull; i++) CheckSeries(issues, $"$.series[{i}]", plot.Series[i]);
                break;
        }
    }

    private static void CheckSeries(VisualSpecIssues issues, string path, Plot3DSeriesSpec series)
    {
        var count = series.X.Count;
        issues.SameLength($"{path}.y", series.Y, "x", count);
        issues.SameLength($"{path}.z", series.Z, "x", count);
        issues.SameLength($"{path}.labels", series.Labels, "x", count);
        issues.SameLength($"{path}.sizes", series.Sizes, "x", count);
        issues.SameLength($"{path}.colors", series.Colors, "x", count);
        issues.Finite($"{path}.x", series.X);
        issues.Finite($"{path}.y", series.Y);
        issues.Finite($"{path}.z", series.Z);
        issues.Finite($"{path}.sizes", series.Sizes);
    }

    private static void CheckSurface(VisualSpecIssues issues, SurfaceSpec surface)
    {
        CheckRange(issues, "$.surface.x", surface.X);
        CheckRange(issues, "$.surface.y", surface.Y);

        var rows = surface.Z;
        if (rows.Count < 2 || rows[0].Count < 2)
        {
            issues.Add("$.surface.z", "a surface needs at least 2 × 2 heights");
            return;
        }

        for (var r = 0; r < rows.Count && !issues.IsFull; r++)
        {
            if (rows[r].Count != rows[0].Count)
            {
                issues.Add($"$.surface.z[{r}]", $"has {rows[r].Count} heights but row 0 has {rows[0].Count}; every row needs one per x");
            }

            issues.Finite($"$.surface.z[{r}]", rows[r]);
        }
    }

    private static void CheckRange(VisualSpecIssues issues, string path, RangeSpec range)
    {
        if (!double.IsFinite(range.Min) || !double.IsFinite(range.Max)) issues.Add(path, "min and max must be finite numbers");
        else if (range.Min >= range.Max) issues.Add(path, "min must be below max");
    }
}

/// <summary>Nodes with unique ids, and edges between nodes that exist: for graph visualizers and 3D graphs alike.</summary>
internal static class GraphSpecRules
{
    public static HashSet<string> Check(VisualSpecIssues issues, string path, GraphSpec graph)
    {
        var ids = issues.UniqueIds($"{path}.nodes", graph.Nodes, node => node.Id, "node");
        for (var i = 0; i < graph.Nodes.Count && !issues.IsFull; i++)
        {
            var node = graph.Nodes[i];
            issues.Finite($"{path}.nodes[{i}].x", node.X);
            issues.Finite($"{path}.nodes[{i}].y", node.Y);
            issues.Finite($"{path}.nodes[{i}].z", node.Z);
            if (node.Size is { } size && !(double.IsFinite(size) && size > 0)) issues.Add($"{path}.nodes[{i}].size", "must be a positive number");
        }

        for (var i = 0; i < graph.Edges.Count && !issues.IsFull; i++)
        {
            var edge = graph.Edges[i];
            if (!ids.Contains(edge.From)) issues.Add($"{path}.edges[{i}].from", $"there is no node \"{edge.From}\"");
            if (!ids.Contains(edge.To)) issues.Add($"{path}.edges[{i}].to", $"there is no node \"{edge.To}\"");
            issues.Finite($"{path}.edges[{i}].weight", edge.Weight);
        }

        return ids;
    }
}
