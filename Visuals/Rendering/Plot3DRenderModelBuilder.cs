using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>
/// Turns a 3D plot spec into what the 3D control draws: fills in the defaults, lays out a graph whose nodes have no
/// positions, and keeps within the limits, saying so when it doesn't draw everything.
/// </summary>
public static class Plot3DRenderModelBuilder
{
    public static Plot3DOptions Build(Plot3DSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var options = new Plot3DOptions
        {
            Title = spec.Title ?? Plot3DRenderDefaults.Title,
            Subtitle = spec.Subtitle ?? string.Empty,
            Type = spec.Kind,
            ColorMap = spec.ColorMap ?? ColorMapPreset.Viridis,
            PrimaryColor = spec.Color ?? Plot3DRenderDefaults.Color,
            Width = spec.Width ?? Plot3DRenderDefaults.Width,
            Height = spec.Height ?? Plot3DRenderDefaults.Height,
            ShowAxes = spec.ShowAxes ?? true,
            ShowFloorGrid = spec.ShowGrid ?? true,
            ShowBoundingBox = spec.ShowGrid ?? true,
            AutoRotate = spec.AutoRotate ?? false,
            Wireframe = spec.Kind == Plot3DType.Wireframe,
            XAxisTitle = spec.XAxis.Title,
            YAxisTitle = spec.YAxis.Title,
            ZAxisTitle = spec.ZAxis.Title
        };

        switch (spec.Kind)
        {
            case Plot3DType.Surface or Plot3DType.Wireframe when spec.Surface != null:
                options.Surface = Surface(spec.Surface, options);
                break;
            case Plot3DType.Graph3D when spec.Graph != null:
                options.Graph = Graph(spec.Graph, options);
                break;
            default:
                AddSeries(spec, options);
                break;
        }

        options.RecalculateBounds();
        return options;
    }

    private static void AddSeries(Plot3DSpec spec, Plot3DOptions options)
    {
        var total = spec.Series.Sum(s => s.X.Count);
        var room = VisualLimits.MaxPlot3DPoints;
        for (var i = 0; i < spec.Series.Count; i++)
        {
            var source = spec.Series[i];
            var series = new Series3D { Name = source.Name ?? $"Series {i + 1}", Color = source.Color ?? (i == 0 ? options.PrimaryColor : null) };
            var count = Math.Min(source.X.Count, Math.Max(0, room));
            room -= count;
            for (var j = 0; j < count; j++)
            {
                // A point with a missing coordinate has nowhere to be drawn.
                if (source.X[j] is not { } x || source.Y[j] is not { } y || source.Z[j] is not { } z) continue;
                series.Points.Add(new Point3D(x, y, z, source.Labels?[j], source.Colors?[j], source.Sizes?[j] ?? 1.0));
            }

            options.Series.Add(series);
        }

        if (total > VisualLimits.MaxPlot3DPoints) options.Notice = Notices.ShowingFirst(VisualLimits.MaxPlot3DPoints, total, "points");
    }

    // The spec keeps one row per y; the model indexes heights [x, y]. A grid over the limit is drawn at every n-th height.
    private static Surface3DData Surface(SurfaceSpec spec, Plot3DOptions options)
    {
        var rows = spec.Z.Count;
        var columns = rows > 0 ? spec.Z[0].Count : 0;
        var stride = 1;
        while ((long)((rows + stride - 1) / stride) * ((columns + stride - 1) / stride) > VisualLimits.MaxSurfaceCells) stride++;

        var resX = (columns + stride - 1) / stride;
        var resY = (rows + stride - 1) / stride;
        var heights = new double[resX, resY];
        double minZ = double.MaxValue, maxZ = double.MinValue;
        for (var i = 0; i < resX; i++)
        {
            for (var j = 0; j < resY; j++)
            {
                var z = spec.Z[j * stride][i * stride] ?? double.NaN;
                heights[i, j] = z;
                if (!double.IsFinite(z)) continue;
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);
            }
        }

        if (stride > 1)
        {
            options.Notice = $"showing every {Ordinal(stride)} height ({resX:N0} × {resY:N0} of {columns:N0} × {rows:N0})";
        }

        return new Surface3DData
        {
            ZValues = heights,
            MinX = spec.X.Min,
            MaxX = spec.X.Max,
            MinY = spec.Y.Min,
            MaxY = spec.Y.Max,
            MinZ = minZ <= maxZ ? minZ : 0,
            MaxZ = minZ <= maxZ ? maxZ : 0
        };
    }

    private static string Ordinal(int n) => n switch { 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    private static Graph3DData Graph(GraphSpec spec, Plot3DOptions options)
    {
        var graph = new Graph3DData();
        var nodes = spec.Nodes.Take(VisualLimits.MaxGraphNodes).ToList();
        var ids = new HashSet<string>(nodes.Select(n => n.Id));
        foreach (var node in nodes)
        {
            graph.AddNode(node.Id, node.Label ?? node.Id, node.X ?? 0, node.Y ?? 0, node.Z ?? 0, node.Color, node.Size ?? Plot3DRenderDefaults.NodeRadius);
        }

        foreach (var edge in spec.Edges.Where(e => ids.Contains(e.From) && ids.Contains(e.To)).Take(VisualLimits.MaxGraphEdges))
        {
            graph.AddEdge(edge.From, edge.To, edge.Weight, edge.Color, edge.Directed ?? spec.Directed ?? true);
        }

        if (spec.Nodes.Count > VisualLimits.MaxGraphNodes) options.Notice = Notices.ShowingFirst(VisualLimits.MaxGraphNodes, spec.Nodes.Count, "nodes");
        else if (spec.Edges.Count > VisualLimits.MaxGraphEdges) options.Notice = Notices.ShowingFirst(VisualLimits.MaxGraphEdges, spec.Edges.Count, "edges");

        // Positions the spec gives are kept; the studio lays out a graph whose nodes don't all have one.
        if (nodes.Any(n => n.X == null || n.Y == null || n.Z == null)) ForceDirected3DLayout.ComputeLayout(graph);
        return graph;
    }
}
