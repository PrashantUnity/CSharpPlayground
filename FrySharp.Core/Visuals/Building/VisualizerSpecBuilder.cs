using System.Collections;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

/// <summary>
/// A visualizer's spec from C# data, read by the conventions every language follows (docs/visual-protocol.md): a 2D
/// array or jagged rows (a matrix), a sequence with named pointers (an array), node objects with value, left/right or
/// children (a tree) or next (a list), an adjacency map or edge list (a graph). What the studio works out for any
/// language (the islands and their search, a traversal, a list's cycle) is asked for in the spec, not recorded here.
/// </summary>
public static class VisualizerSpecBuilder
{
    public static VisualizerSpec Matrix(object grid, bool? showCoordinates = null, bool? showValues = null, double? cellSize = null)
    {
        var data = MatrixDataParser.Parse(grid, cellSize ?? VisualizerRenderDefaults.CellSize);
        if (showCoordinates is { } coordinates) data.ShowCoordinates = coordinates;
        if (showValues is { } values) data.ShowValues = values;
        return ToSpec(new VisualizerOptions { Kind = VisualizerKind.Matrix, MatrixData = data, CellSize = data.CellSize });
    }

    /// <summary>A grid of land and water whose islands the studio finds, recording the search unless told not to.</summary>
    public static VisualizerSpec Islands(object grid, bool recordSteps = true, bool fourDirectional = true)
    {
        var spec = ToSpec(new VisualizerOptions { Kind = VisualizerKind.Islands, MatrixData = MatrixDataParser.Parse(grid) });
        spec.Islands = new IslandsOptionsSpec
        {
            RecordSteps = recordSteps ? null : false,
            FourDirectional = fourDirectional ? null : false
        };
        return spec;
    }

    /// <summary>A tree; with <paramref name="traversal"/>, the studio records that walk of it.</summary>
    public static VisualizerSpec Tree(object? root, TreeTraversal? traversal = null)
    {
        var spec = ToSpec(new VisualizerOptions { Kind = VisualizerKind.Tree, TreeData = TreeDataParser.Parse(root) });
        spec.Traversal = traversal;
        return spec;
    }

    /// <summary>A walk's name as the older API wrote it ("inorder", "level-order"…), or null for none.</summary>
    public static TreeTraversal? TraversalNamed(string? name) =>
        (name ?? string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant() switch
        {
            "" => null,
            "preorder" => TreeTraversal.Preorder,
            "inorder" => TreeTraversal.Inorder,
            "postorder" => TreeTraversal.Postorder,
            "levelorder" or "bfs" => TreeTraversal.LevelOrder,
            _ => throw new ArgumentException($"There is no \"{name}\" walk of a tree: use preorder, inorder, postorder or levelorder.", nameof(name))
        };

    public static VisualizerSpec Graph(object? graph, bool directed = true) =>
        ToSpec(new VisualizerOptions { Kind = VisualizerKind.Graph, GraphData = GraphDataParser.Parse(graph, directed) });

    /// <summary>A linked list; with <paramref name="detectCycle"/>, the studio records Floyd's tortoise and hare on it.</summary>
    public static VisualizerSpec LinkedList(object? head, bool detectCycle = false)
    {
        var spec = ToSpec(new VisualizerOptions { Kind = VisualizerKind.LinkedList, LinkedListData = LinkedListDataParser.Parse(head) });
        if (detectCycle) spec.DetectCycle = true;
        return spec;
    }

    /// <summary>An array (or list, or string) with pointers into it: <c>new { lo = 0, hi = 4 }</c> or a name → index map.</summary>
    public static VisualizerSpec Array(object values, object? pointers = null) =>
        ToSpec(new VisualizerOptions { Kind = VisualizerKind.ArrayPointers, ArrayData = ArrayPointerDataParser.Parse(values, pointers) });

    public static VisualizerSpec Bars(object values) =>
        ToSpec(new VisualizerOptions { Kind = VisualizerKind.Bars, BarData = BarData(values) });

    public static VisualizerSpec Board(BoardVisualizerData board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var spec = ToSpec(new VisualizerOptions { Kind = VisualizerKind.Board, BoardData = board });
        spec.Title ??= board.Title;
        return spec;
    }

    public static VisualizerSpec Canvas(VisualizerScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return ToSpec(new VisualizerOptions { Kind = VisualizerKind.Canvas, SceneData = scene });
    }

    /// <summary>
    /// A visualizer for whatever the data is: a spec, a tracker or recorder, a 2D grid (a matrix), a node object with next
    /// (a list) or with left, right or children (a tree), a map of adjacency lists (a graph), or a sequence (an array).
    /// </summary>
    /// <exception cref="ArgumentException">The data looks like none of them.</exception>
    public static VisualizerSpec From(object? data)
    {
        switch (data)
        {
            case VisualizerSpec spec: return spec;
            case IVisualSource source when source.ToVisualSpec() is VisualizerSpec described: return described;
            case VisualizerOptions options: return ToSpec(options);
            case VisualizerRecorder recorder: return ToSpec(recorder.Options);
            case BoardVisualizerData board: return Board(board);
            case VisualizerScene scene: return Canvas(scene);
            case BarChartVisualizerData bars: return ToSpec(new VisualizerOptions { Kind = VisualizerKind.Bars, BarData = bars });
            case System.Array { Rank: 2 }: return Matrix(data);
            case IDictionary: return Graph(data);
            case string text: return Array(text);
            case IEnumerable sequence when IsGrid(sequence): return Matrix(data);
            case IEnumerable: return Array(data);
            case null: throw new ArgumentException("There is nothing to visualize (null).", nameof(data));
        }

        if (DataReader.TryMember(data, ["next"], out _, out _)) return LinkedList(data);
        if (DataReader.TryMember(data, ["left", "right", "children"], out _, out _)) return Tree(data);
        throw new ArgumentException($"A {data.GetType().Name} doesn't look like a grid, tree, list, graph or array; use Display.Matrix, Tree, LinkedList, Graph or Array to say which.", nameof(data));
    }

    private static bool IsGrid(IEnumerable rows)
    {
        var any = false;
        foreach (var row in rows)
        {
            if (row is not IEnumerable || row is string) return false;
            any = true;
        }

        return any;
    }

    private static BarChartVisualizerData BarData(object values) => values switch
    {
        BarChartVisualizerData bars => bars,
        IEnumerable<int> ints => new BarChartVisualizerData(ints),
        IEnumerable<double> doubles => new BarChartVisualizerData(doubles),
        IEnumerable sequence when values is not string => new BarChartVisualizerData(sequence.Cast<object?>().Select(v => DataReader.TryPresentNumber(v, out var n) ? n : 0)),
        _ => throw new ArgumentException($"Bars show a sequence of numbers, not a {values.GetType().Name}.", nameof(values))
    };

    private static VisualizerSpec ToSpec(VisualizerOptions options) => VisualizerOptionsConverter.ToSpec(options);
}
