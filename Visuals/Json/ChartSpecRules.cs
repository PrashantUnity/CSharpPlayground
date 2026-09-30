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

        for (var i = 0; i < chart.Series.Count && !issues.IsFull; i++)
        {
            var path = $"$.series[{i}]";
            var series = chart.Series[i];
            if (chart.Kind == ChartType.Histogram) CheckHistogramSeries(issues, path, series);
            else CheckSeries(issues, path, series, chart.Kind);
        }
    }

    internal static void CheckAxis(VisualSpecIssues issues, string path, AxisSpec axis)
    {
        issues.Finite($"{path}.min", axis.Min);
        issues.Finite($"{path}.max", axis.Max);
        if (axis.Min is { } min && axis.Max is { } max && min >= max) issues.Add(path, "min must be below max");
    }

    private static void CheckSeries(VisualSpecIssues issues, string path, ChartSeriesSpec series, ChartType kind)
    {
        if (series.Values != null) issues.Add($"{path}.values", "only a histogram counts values; give this series its y instead");

        var count = series.Y.Count;
        if (series.LineWidth is { } lineWidth && !(double.IsFinite(lineWidth) && lineWidth > 0)) issues.Add($"{path}.lineWidth", "must be a positive number of pixels");
        issues.Finite($"{path}.y", series.Y);
        issues.Finite($"{path}.x", series.X);
        issues.SameLength($"{path}.x", series.X, "y", count);
        issues.SameLength($"{path}.labels", series.Labels, "y", count);
        issues.SameLength($"{path}.colors", series.Colors, "y", count);
        issues.SameLength($"{path}.ids", series.Ids, "y", count);

        if (kind is ChartType.Pie or ChartType.Donut)
        {
            for (var i = 0; i < count; i++)
            {
                if (series.Y[i] is < 0) issues.Add($"{path}.y[{i}]", "a slice can't be negative");
            }
        }
    }

    private static void CheckHistogramSeries(VisualSpecIssues issues, string path, ChartSeriesSpec series)
    {
        if (series.Values == null) issues.Add($"{path}.values", "a histogram series needs values: the samples to count");
        if (series.Y.Count > 0) issues.Add($"{path}.y", "a histogram counts its values itself; give the samples as values, not y");
        issues.Finite($"{path}.values", series.Values);
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
