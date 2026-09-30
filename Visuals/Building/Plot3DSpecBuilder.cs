using System.Collections;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

/// <summary>
/// A 3D plot's spec from C# data, read by the conventions every language follows (docs/visual-protocol.md):
/// <list type="bullet">
/// <item>points: <see cref="Point3D"/>s, (x, y, z) tuples of any numbers (a fourth item is the label), three-number
/// arrays, or records with x, y and z members (and optionally a label, color and size);</item>
/// <item>a name → sequence map, a series each;</item>
/// <item>a surface: a function z = f(x, y), a grid of heights (one row per y), or <see cref="Surface3DData"/>;</item>
/// <item>a graph: <see cref="Graph3DData"/>, or for a graph plot an adjacency map (node → the nodes it links to).</item>
/// </list>
/// A point with a coordinate that is missing or not a number isn't drawn.
/// </summary>
public static class Plot3DSpecBuilder
{
    /// <summary>The members a record's label is read from, in order.</summary>
    public static IReadOnlyList<string> LabelNames { get; } = ["label", "name", "title", "id"];

    /// <summary>A 3D plot of <paramref name="data"/>, drawn as <paramref name="kind"/> (points, or what the data is).</summary>
    /// <exception cref="ArgumentException">The data isn't points, a surface or a graph.</exception>
    public static Plot3DSpec From(object? data, Plot3DType? kind = null)
    {
        switch (data)
        {
            case Plot3DSpec given:
                if (kind is { } asked) given.Kind = asked;
                return given;
            case Plot3DOptions model:
                var converted = Plot3DOptionsConverter.ToSpec(model);
                if (kind is { } modelKind) converted.Kind = modelKind;
                return converted;
            case Surface3DData surface:
                return new Plot3DSpec { Kind = SurfaceKind(kind), Surface = Plot3DOptionsConverter.ToSpec(surface) };
            case Func<double, double, double> function:
                return Surface(function, (-5, 5), (-5, 5), 30, kind);
            case Array { Rank: 2 } grid:
                return new Plot3DSpec { Kind = SurfaceKind(kind), Surface = Grid(Rows(grid)) };
            case IEnumerable rows when kind is Plot3DType.Surface or Plot3DType.Wireframe && data is not string:
                return new Plot3DSpec { Kind = SurfaceKind(kind), Surface = Grid(rows.Cast<object?>().Select(r => r is IEnumerable row and not string ? row.Cast<object?>().ToList() : []).ToList()) };
            case Graph3DData graph:
                return new Plot3DSpec { Kind = Plot3DType.Graph3D, Graph = Graph(graph) };
            case IDictionary adjacency when kind == Plot3DType.Graph3D:
                return new Plot3DSpec { Kind = Plot3DType.Graph3D, Graph = Adjacency(adjacency) };
        }

        var spec = new Plot3DSpec { Kind = kind ?? Plot3DType.Scatter };
        switch (data)
        {
            case null:
                break;
            case IDictionary named when named.Values.Cast<object?>().All(DataReader.IsSequence):
                foreach (var (key, value) in DataReader.Entries(named)) spec.Series.Add(Points((IEnumerable)value!, key.ToString()));
                break;
            case IEnumerable points when data is not string:
                spec.Series.Add(Points(points, null));
                break;
            default:
                throw new ArgumentException($"A 3D plot shows points, a surface or a graph, not a single {data.GetType().Name}.", nameof(data));
        }

        return spec;
    }

    /// <summary>The surface z = <paramref name="function"/>(x, y), sampled <paramref name="resolution"/> times along each axis.</summary>
    public static Plot3DSpec Surface(Func<double, double, double> function, (double Min, double Max) x, (double Min, double Max) y, int resolution = 30, Plot3DType? kind = null) =>
        Surface(function, x, y, resolution, resolution, kind);

    public static Plot3DSpec Surface(Func<double, double, double> function, (double Min, double Max) x, (double Min, double Max) y, int columns, int rows, Plot3DType? kind = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        var surface = Surface3DData.FromFunction(function, x.Min, x.Max, y.Min, y.Max, Math.Max(2, columns), Math.Max(2, rows));
        return new Plot3DSpec { Kind = SurfaceKind(kind), Surface = Plot3DOptionsConverter.ToSpec(surface) };
    }

    private static Plot3DType SurfaceKind(Plot3DType? kind) => kind == Plot3DType.Wireframe ? Plot3DType.Wireframe : Plot3DType.Surface;

    private static List<List<object?>> Rows(Array grid) =>
        Enumerable.Range(0, grid.GetLength(0)).Select(r => Enumerable.Range(0, grid.GetLength(1)).Select(c => grid.GetValue(r, c)).ToList()).ToList();

    // Heights as written: a row per y and a column per x (numpy's order), over the rows' and columns' indices. A height
    // that is missing or not a number is a hole; a short row is padded with holes.
    private static SurfaceSpec Grid(List<List<object?>> rows)
    {
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var spec = new SurfaceSpec { X = new RangeSpec(0, Math.Max(1, columns - 1)), Y = new RangeSpec(0, Math.Max(1, rows.Count - 1)) };
        foreach (var row in rows)
        {
            spec.Z.Add(Enumerable.Range(0, columns).Select(c => c < row.Count && DataReader.TryNumber(row[c], out var z) ? z : null).ToList());
        }

        return spec;
    }

    private static Plot3DSeriesSpec Points(IEnumerable items, string? name)
    {
        var series = new Plot3DSeriesSpec { Name = name };
        var labels = new List<string?>();
        var colors = new List<string?>();
        var sizes = new List<double?>();
        foreach (var item in items)
        {
            if (!TryPoint(item, out var point)) continue;
            series.X.Add(point.X);
            series.Y.Add(point.Y);
            series.Z.Add(point.Z);
            labels.Add(point.Label);
            colors.Add(point.Color);
            sizes.Add(point.Size);
        }

        if (labels.Any(l => l != null)) series.Labels = labels;
        if (colors.Any(c => c != null)) series.Colors = colors;
        if (sizes.Any(s => s != null)) series.Sizes = sizes;
        return series;
    }

    // One point of any of the shapes a point comes in; one missing a coordinate (or not a point at all) isn't drawn.
    private static bool TryPoint(object? item, out (double X, double Y, double Z, string? Label, string? Color, double? Size) point)
    {
        point = default;
        switch (item)
        {
            case null:
                return false;
            case Point3D p:
                point = (p.X, p.Y, p.Z, NullIfEmpty(p.Label), p.CustomColor, p.Size == 1.0 ? null : p.Size);
                return double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
        }

        object?[] items;
        if (DataReader.TryItems(item, out var tuple) && tuple.Length >= 3) items = tuple;
        else if (item is IEnumerable sequence and not string) items = sequence.Cast<object?>().Take(4).ToArray();
        else
        {
            // A record with x, y and z.
            if (!DataReader.TryMember(item, ["x"], out _, out var rx) || !DataReader.TryMember(item, ["y"], out _, out var ry) ||
                !DataReader.TryMember(item, ["z"], out _, out var rz)) return false;
            DataReader.TryMember(item, LabelNames, out _, out var label);
            DataReader.TryMember(item, ["color", "colour"], out _, out var color);
            double? size = DataReader.TryMember(item, ["size"], out _, out var s) && DataReader.TryPresentNumber(s, out var sz) ? sz : null;
            items = [rx, ry, rz, label];
            if (!Coordinates(items, out var x, out var y, out var z)) return false;
            point = (x, y, z, label?.ToString(), color?.ToString(), size);
            return true;
        }

        if (items.Length < 3 || !Coordinates(items, out var px, out var py, out var pz)) return false;
        point = (px, py, pz, items.Length > 3 ? items[3]?.ToString() : null, null, null);
        return true;
    }

    private static bool Coordinates(object?[] items, out double x, out double y, out double z)
    {
        y = z = 0;
        return DataReader.TryPresentNumber(items[0], out x) && DataReader.TryPresentNumber(items[1], out y) && DataReader.TryPresentNumber(items[2], out z);
    }

    // A graph's positions are kept when it has any; one whose nodes are all at the origin is laid out by the studio.
    private static GraphSpec Graph(Graph3DData graph)
    {
        var spec = Plot3DOptionsConverter.ToSpec(graph);
        if (graph.Nodes.All(n => n.X == 0 && n.Y == 0 && n.Z == 0))
        {
            foreach (var node in spec.Nodes) node.X = node.Y = node.Z = null;
        }

        return spec;
    }

    // node → the nodes it links to (a sequence, or a single node).
    private static GraphSpec Adjacency(IDictionary adjacency)
    {
        var spec = new GraphSpec { Directed = true };
        var seen = new HashSet<string>();
        void Node(string id)
        {
            if (seen.Add(id)) spec.Nodes.Add(new GraphNodeSpec { Id = id });
        }

        foreach (var (key, value) in DataReader.Entries(adjacency))
        {
            var from = key.ToString() ?? string.Empty;
            Node(from);
            var targets = value is IEnumerable many and not string ? many.Cast<object?>() : [value];
            foreach (var target in targets.Where(t => t != null))
            {
                var to = target!.ToString() ?? string.Empty;
                Node(to);
                spec.Edges.Add(new GraphEdgeSpec { From = from, To = to });
            }
        }

        return spec;
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
