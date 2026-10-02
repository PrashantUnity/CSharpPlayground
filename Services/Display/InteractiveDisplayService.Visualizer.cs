using System.Collections;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

// Data-structure visualizers: one helper per kind, plus Visualize (which works out the kind) and the recorder and
// trackers that play an algorithm step by step.
public static partial class Display
{
    /// <summary>
    /// A visualizer for whatever <paramref name="data"/> is: a 2D grid (a matrix), a node with next (a linked list) or with
    /// left, right or children (a tree), a map of adjacency lists (a graph), a sequence (an array), or a spec, recorder or
    /// tracker.
    /// </summary>
    public static DisplayHandle<VisualizerSpec> Visualize(object data, string? title = null, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.From(data), title, configure);

    public static DisplayHandle<VisualizerSpec> Visualizer(VisualizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Show(VisualizerOptionsConverter.ToSpec(options), configure: null, origin: options);
    }

    // Typed, so a MatrixTracker (which converts to its grid) never picks the wrong overload.
    public static DisplayHandle<VisualizerSpec> Visualizer(VisualizerRecorder recorder) => Visualizer(recorder.Options);
    public static DisplayHandle<VisualizerSpec> Visualizer(MatrixTracker tracker) => Visualizer(tracker.Options);
    public static DisplayHandle<VisualizerSpec> Visualizer(TreeTracker tracker) => Visualizer(tracker.Options);
    public static DisplayHandle<VisualizerSpec> Visualizer(GraphTracker tracker) => Visualizer(tracker.Options);
    public static DisplayHandle<VisualizerSpec> Visualizer(LinkedListTracker tracker) => Visualizer(tracker.Options);
    public static DisplayHandle<VisualizerSpec> Visualizer(RecursionTracker tracker) => Visualizer(tracker.Options);
    public static DisplayHandle<VisualizerSpec> Visualizer(IntervalTracker tracker) => Visualizer(tracker.Options);
    public static DisplayHandle<VisualizerSpec> Visualizer(TrieTracker tracker) => Visualizer(tracker.Options);

    /// <summary>
    /// A recorder for an algorithm on <paramref name="state"/>: record a step with <c>Step(description, …)</c> each time
    /// something happens, then show it with <c>Display.Visualizer(recorder)</c>. Kinds: matrix, arrayPointers, tree,
    /// graph, bars, board, canvas (a list is recorded by <see cref="LinkedListTracker"/>, islands by <see cref="Islands"/>).
    /// </summary>
    public static VisualizerRecorder Recorder(
        VisualizerKind kind,
        object? state = null,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "") => kind switch
    {
        VisualizerKind.Matrix => VisualizerRecorder.CreateMatrix(MatrixDataParser.Parse(Required(state, kind)), title, sourceLine, sourceFile),
        VisualizerKind.ArrayPointers => VisualizerRecorder.CreateArray(Required(state, kind) as IEnumerable ?? throw NotA("sequence", kind), title, sourceLine, sourceFile),
        VisualizerKind.Tree => VisualizerRecorder.CreateTree(Required(state, kind), title, sourceLine, sourceFile),
        VisualizerKind.Graph => VisualizerRecorder.CreateGraph(Required(state, kind), title, sourceLine: sourceLine, sourceFile: sourceFile),
        VisualizerKind.Bars => Required(state, kind) switch
        {
            BarChartVisualizerData bars => VisualizerRecorder.CreateBars(bars, title, sourceLine, sourceFile),
            IEnumerable<int> ints => VisualizerRecorder.CreateBars(ints, title, sourceLine, sourceFile),
            IEnumerable<double> doubles => VisualizerRecorder.CreateBars(doubles, title, sourceLine, sourceFile),
            _ => throw NotA("sequence of numbers", kind)
        },
        VisualizerKind.Board => VisualizerRecorder.CreateBoard(Required(state, kind) as BoardVisualizerData ?? throw NotA(nameof(BoardVisualizerData), kind), title, sourceLine, sourceFile),
        VisualizerKind.Canvas => VisualizerRecorder.CreateCanvas(title, sourceLine: sourceLine, sourceFile: sourceFile),
        VisualizerKind.LinkedList => throw new ArgumentException("A linked list is recorded by LinkedListTracker.Create(head), which follows its next pointers as they change.", nameof(kind)),
        _ => throw new ArgumentException("Islands record themselves: Display.Islands(grid) finds them and records the search.", nameof(kind))
    };

    /// <summary>A grid of land and water (1/0, '1'/'0'); the studio finds its islands and records the search.</summary>
    public static DisplayHandle<VisualizerSpec> Islands(object grid, string? title = null, bool recordSteps = true, bool fourDirectional = true, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.Islands(grid, recordSteps, fourDirectional), title, configure);

    /// <summary>A 2D array or jagged rows; values say what a cell is (1 land, 0 water, "S" start, "T" target, "#" wall).</summary>
    public static DisplayHandle<VisualizerSpec> Matrix(object grid, string? title = null, bool? showCoordinates = null, bool? showValues = null, double? cellSize = null, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.Matrix(grid, showCoordinates, showValues, cellSize), title, configure);

    /// <summary>A tree of node objects (value, left/right or children); <paramref name="recordTraversal"/> (preorder, inorder, postorder, levelorder) records that walk.</summary>
    public static DisplayHandle<VisualizerSpec> Tree(object root, string? title = null, string? recordTraversal = null, Action<VisualizerSpec>? configure = null)
    {
        if (root is TreeTracker tracker) return ShowVisualizer(VisualizerOptionsConverter.ToSpec(tracker.Options), title, configure);
        return ShowVisualizer(VisualizerSpecBuilder.Tree(root, VisualizerSpecBuilder.TraversalNamed(recordTraversal)), title, configure);
    }

    /// <summary>A graph: an adjacency map (node → neighbours) or a list of edges.</summary>
    public static DisplayHandle<VisualizerSpec> Graph(object graph, string? title = null, bool isDirected = true, Action<VisualizerSpec>? configure = null, bool? directed = null)
    {
        var actuallyDirected = directed ?? isDirected;
        if (graph is GraphTracker tracker) return ShowVisualizer(VisualizerOptionsConverter.ToSpec(tracker.Options), title, configure);
        return ShowVisualizer(VisualizerSpecBuilder.Graph(graph, actuallyDirected), title, configure);
    }

    /// <summary>A linked list from its head (nodes with next); <paramref name="recordCycleSteps"/> records Floyd's cycle search.</summary>
    public static DisplayHandle<VisualizerSpec> LinkedList(object head, string? title = null, bool recordCycleSteps = false, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.LinkedList(head, recordCycleSteps), title, configure);

    /// <summary>An array, list or string, with pointers into it: <c>new { lo = 0, hi = 4 }</c> or a name → index map.</summary>
    public static DisplayHandle<VisualizerSpec> Array(object array, object? pointers = null, string? title = null, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.Array(array, pointers), title, configure);

    /// <summary>Numbers as bars, as a sorting algorithm moves them.</summary>
    public static DisplayHandle<VisualizerSpec> Bars(object values, string? title = null, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.Bars(values), title, configure);

    public static DisplayHandle<VisualizerSpec> Board(BoardVisualizerData boardData, string? title = null, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.Board(boardData), title, configure);

    public static DisplayHandle<VisualizerSpec> Canvas(VisualizerScene scene, string? title = null, Action<VisualizerSpec>? configure = null) =>
        ShowVisualizer(VisualizerSpecBuilder.Canvas(scene), title, configure);

    /// <summary>A drawing of shapes: <paramref name="draw"/> adds rectangles, circles, lines, arrows and text to the scene.</summary>
    public static DisplayHandle<VisualizerSpec> Canvas(Action<VisualizerScene> draw, string? title = null, double width = 600, double height = 300, Action<VisualizerSpec>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(draw);
        var scene = new VisualizerScene(width, height);
        draw(scene);
        return Canvas(scene, title, configure);
    }

    private static DisplayHandle<VisualizerSpec> ShowVisualizer(VisualizerSpec spec, string? title, Action<VisualizerSpec>? configure)
    {
        Set(spec, title, null, null, null);
        return Show(spec, configure);
    }

    private static object Required(object? state, VisualizerKind kind) =>
        state ?? throw new ArgumentException($"A {kind} recorder needs the data it starts from.", nameof(state));

    private static ArgumentException NotA(string what, VisualizerKind kind) =>
        new($"A {kind} recorder starts from a {what}.", "state");
}
