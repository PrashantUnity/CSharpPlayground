using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

/// <summary>The older 3D model (<see cref="Plot3DOptions"/>) as a spec, losing nothing the plot shows.</summary>
public static class Plot3DOptionsConverter
{
    public static Plot3DSpec ToSpec(Plot3DOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var spec = new Plot3DSpec
        {
            Title = options.Title,
            Subtitle = string.IsNullOrEmpty(options.Subtitle) ? null : options.Subtitle,
            Kind = options.Type is Plot3DType.Surface or Plot3DType.Wireframe
                ? (options.Wireframe ? Plot3DType.Wireframe : Plot3DType.Surface)
                : options.Type,
            ColorMap = options.ColorMap,
            Color = options.PrimaryColor,
            AutoRotate = options.AutoRotate,
            ShowAxes = options.ShowAxes,
            ShowGrid = options.ShowFloorGrid,
            Width = options.Width == Plot3DRenderDefaults.Width ? null : options.Width,
            Height = options.Height == Plot3DRenderDefaults.Height ? null : options.Height,
            XAxis = { Title = options.XAxisTitle },
            YAxis = { Title = options.YAxisTitle },
            ZAxis = { Title = options.ZAxisTitle }
        };

        foreach (var series in options.Series)
        {
            var points = series.Points;
            spec.Series.Add(new Plot3DSeriesSpec
            {
                Name = series.Name,
                Color = series.Color,
                // A coordinate that isn't a number (NaN) is missing, which JSON writes as null.
                X = points.Select(p => Finite(p.X)).ToList(),
                Y = points.Select(p => Finite(p.Y)).ToList(),
                Z = points.Select(p => Finite(p.Z)).ToList(),
                Labels = points.Any(p => !string.IsNullOrEmpty(p.Label)) ? points.Select(p => string.IsNullOrEmpty(p.Label) ? null : p.Label).ToList() : null,
                Sizes = points.Any(p => p.Size != 1.0) ? points.Select(p => (double?)p.Size).ToList() : null,
                Colors = points.Any(p => p.CustomColor != null) ? points.Select(p => p.CustomColor).ToList() : null
            });
        }

        if (options.Surface is { } surface) spec.Surface = ToSpec(surface);
        if (options.Graph is { } graph) spec.Graph = ToSpec(graph);
        return spec;
    }

    // The model indexes heights [x, y]; the spec keeps one row per y, numpy's meshgrid order.
    public static SurfaceSpec ToSpec(Surface3DData surface)
    {
        var spec = new SurfaceSpec { X = new RangeSpec(surface.MinX, surface.MaxX), Y = new RangeSpec(surface.MinY, surface.MaxY) };
        for (var j = 0; j < surface.ResolutionY; j++)
        {
            var row = new List<double?>(surface.ResolutionX);
            for (var i = 0; i < surface.ResolutionX; i++)
            {
                var z = surface.ZValues[i, j];
                row.Add(Finite(z));
            }

            spec.Z.Add(row);
        }

        return spec;
    }

    public static GraphSpec ToSpec(Graph3DData graph)
    {
        // Directed is the spec's default, so a graph says "not directed" once and only an edge that differs says more.
        var directed = graph.Edges.Count == 0 || graph.Edges.All(e => e.IsDirected);
        return new GraphSpec
        {
            Directed = directed,
            Nodes = graph.Nodes.Select(n => new GraphNodeSpec
            {
                Id = n.Id,
                Label = n.Label == n.Id ? null : n.Label,
                X = Finite(n.X),
                Y = Finite(n.Y),
                Z = Finite(n.Z),
                Color = n.Color,
                Size = n.Radius == Plot3DRenderDefaults.NodeRadius ? null : n.Radius
            }).ToList(),
            Edges = graph.Edges.Select(e => new GraphEdgeSpec
            {
                From = e.FromId,
                To = e.ToId,
                Weight = e.Weight is { } weight ? Finite(weight) : null,
                Color = e.Color,
                Directed = e.IsDirected == directed ? null : e.IsDirected
            }).ToList()
        };
    }

    private static double? Finite(double value) => double.IsFinite(value) ? value : null;
}

/// <summary>What a 3D plot looks like where its spec says nothing.</summary>
public static class Plot3DRenderDefaults
{
    public const string Title = "3D Visualization";
    public const string Color = "#4ec9b0";
    public const double Width = 640;
    public const double Height = 360;
    public const double NodeRadius = 8.0;
}
