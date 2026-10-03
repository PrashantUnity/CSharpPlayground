using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Everything the C# side draws today, turned into a spec, sent through JSON and drawn from it, draws exactly the same:
/// charts from every kind of data, every 3D kind, every visualizer kind, and every Blind 75 solution's visualizers.
/// </summary>
public class VisualSpecEquivalenceTests
{
    private static readonly Dictionary<string, Func<ChartOptions>> ChartCases = new()
    {
        ["numbers"] = () => ChartDataParser.Parse(new[] { 1, 4, 9, 16 }, "Squares", "#ff0000", "Bar"),
        ["dictionary"] = () => ChartDataParser.Parse(new Dictionary<string, double> { ["Chrome"] = 60, ["Safari"] = 25, ["Other"] = 15 }, chartType: "Pie"),
        ["xy tuples"] = () => ChartDataParser.Parse(new[] { (1.0, 2.0), (3.0, 5.5), (4.0, -1.0) }, chartType: "Scatter"),
        ["labelled tuples"] = () => ChartDataParser.Parse(new[] { ("Jan", 12), ("Feb", 15), ("Mar", 9) }),
        ["records"] = () => ChartDataParser.Parse(new[] { new { Month = "Jan", Revenue = 120.5 }, new { Month = "Feb", Revenue = 99.25 } }, chartType: "Area"),
        ["histogram"] = () => ChartDataParser.ParseHistogram(new[] { 12.0, 15, 11, 40, 42, 13, 14, 55, 16 }, 8, "Latency", "#6366f1"),
        ["not a number"] = () => ChartDataParser.Parse(new[] { 1.0, double.NaN, 3.0 }),
        ["hand built"] = () => new ChartOptions
        {
            Title = "Two stores",
            Subtitle = "2026",
            Type = ChartType.Line,
            ShowLegend = true,
            ShowGrid = false,
            Width = 700,
            Height = 320,
            XAxisTitle = "Month",
            YMin = 0,
            Series =
            {
                new ChartSeries { Name = "A", Color = "#22c55e", StrokeThickness = 3, Points = { new ChartDataPoint(0, 1, "Jan") { CustomColor = "#ff0000" }, new ChartDataPoint(1, 2, "Feb") } },
                new ChartSeries { Name = "B", Points = { new ChartDataPoint(0, 5), new ChartDataPoint(2, 7) } }
            }
        },
        ["settings"] = () =>
        {
            var chart = ChartDataParser.Parse(new[] { 3, 1, 2 }, "Emitted", "#0ea5e9", "Line");
            (chart.Width, chart.Height, chart.ShowGrid, chart.ShowPoints, chart.ShowStats) = (640, 300, false, false, false);
            return chart;
        }
    };

    public static TheoryData<string> ChartCaseNames() => new(ChartCases.Keys);

    [Theory]
    [MemberData(nameof(ChartCaseNames))]
    public void AChart_DrawsTheSameFromItsSpec(string name)
    {
        var original = ChartCases[name]();
        var spec = ChartOptionsConverter.ToSpec(original);

        Assert.Empty(VisualSpecValidator.Validate(spec));
        var expected = VisualFingerprints.Of(original);
        Assert.Equal(expected, VisualFingerprints.Of(ChartRenderModelBuilder.Build(spec)));
        Assert.Equal(expected, VisualFingerprints.Of(ChartRenderModelBuilder.Build(VisualJson.Clone(spec))));
    }

    private static readonly Dictionary<string, Func<Plot3DOptions>> Plot3DCases = new()
    {
        ["points"] = () => Plot3DDataParser.Parse(new[] { new Point3D(1, 2, 3, "a", "#ff0000", 2), new Point3D(-1, 0, 4), new Point3D(2, 2, 2, "c") }, "Points"),
        ["tuples"] = () => Plot3DDataParser.Parse(new List<(double, double, double)> { (1, 2, 3), (4, 5, 6) }),
        ["surface"] = () => Plot(new Plot3DOptions { Title = "Saddle", Type = Plot3DType.Surface, ColorMap = ColorMapPreset.Plasma, Surface = Surface3DData.FromFunction((x, y) => x * y, -2, 2, -1, 1, 10, 12) }),
        ["wireframe"] = () => Plot(new Plot3DOptions { Title = "Waves", Type = Plot3DType.Wireframe, Wireframe = true, Surface = Surface3DData.FromFunction((x, y) => Math.Sin(x) + Math.Cos(y), -3, 3, -3, 3, 15, 15) }),
        ["graph"] = () =>
        {
            var graph = Network();
            ForceDirected3DLayout.ComputeLayout(graph);
            return Plot(new Plot3DOptions { Title = "Network", Type = Plot3DType.Graph3D, PrimaryColor = "#f59e0b", Graph = graph });
        },
        ["trajectory"] = () => Plot(new Plot3DOptions
        {
            Title = "Spiral", Type = Plot3DType.Trajectory, ColorMap = ColorMapPreset.Turbo, AutoRotate = true,
            Series = { new Series3D { Points = Enumerable.Range(0, 50).Select(i => new Point3D(Math.Cos(i / 5.0), Math.Sin(i / 5.0), i / 10.0)).ToList() } }
        }),
        ["voxel bars"] = () => Plot(new Plot3DOptions
        {
            Title = "Bars", Type = Plot3DType.VoxelBar, PrimaryColor = "#10b981",
            Series = { new Series3D { Points = { new Point3D(0, 0, 3), new Point3D(1, 0, 5), new Point3D(0, 1, 2) } } }
        })
    };

    private static Plot3DOptions Plot(Plot3DOptions options)
    {
        options.RecalculateBounds();
        return options;
    }

    private static Graph3DData Network()
    {
        var graph = new Graph3DData();
        graph.AddNode("a", "Alpha", color: "#ff0000");
        graph.AddNode("b");
        graph.AddNode("c", radius: 12);
        graph.AddEdge("a", "b", 2.5);
        graph.AddEdge("b", "c", color: "#00ff00", isDirected: true);
        return graph;
    }

    public static TheoryData<string> Plot3DCaseNames() => new(Plot3DCases.Keys);

    [Theory]
    [MemberData(nameof(Plot3DCaseNames))]
    public void A3DPlot_DrawsTheSameFromItsSpec(string name)
    {
        var original = Plot3DCases[name]();
        var spec = Plot3DOptionsConverter.ToSpec(original);

        Assert.Empty(VisualSpecValidator.Validate(spec));
        var expected = VisualFingerprints.Of(original);
        Assert.Equal(expected, VisualFingerprints.Of(Plot3DRenderModelBuilder.Build(spec)));
        Assert.Equal(expected, VisualFingerprints.Of(Plot3DRenderModelBuilder.Build(VisualJson.Clone(spec))));
    }

    // The one deliberate difference: a missing height is a hole. It used to count as 0 in the height range, so a surface
    // from 1 to 4 with a hole ranged from 0 and was drawn squashed.
    [Fact]
    public void AHoleInASurface_StaysAHole_AndDoesntStretchTheHeightRange()
    {
        var original = Plot3DDataParser.Parse(Surface3DData.FromGrid(new[,] { { 1, double.NaN }, { 3, 4 } }));

        var redrawn = Plot3DRenderModelBuilder.Build(VisualJson.Clone(Plot3DOptionsConverter.ToSpec(original)));

        Assert.True(double.IsNaN(redrawn.Surface!.ZValues[0, 1]));
        Assert.Equal((1.0, 4.0), (redrawn.Surface.MinZ, redrawn.Surface.MaxZ));
    }

    private sealed class ListNode(int val, ListNode? next = null)
    {
        public int Val { get; } = val;
        public ListNode? Next { get; set; } = next;
    }

    private sealed class TreeNode(int val, TreeNode? left = null, TreeNode? right = null)
    {
        public int Val { get; } = val;
        public TreeNode? Left { get; } = left;
        public TreeNode? Right { get; } = right;
    }

    private static ListNode CycleList()
    {
        var third = new ListNode(3);
        var head = new ListNode(1, new ListNode(2, third));
        third.Next = head.Next;
        return head;
    }

    private static object[,] Maze() => new object[,] { { 'S', 0, 1 }, { "#", 1, 0 }, { 0, 0, 'T' } };
    private static char[,] Sea() => new[,] { { '1', '1', '0' }, { '0', '1', '0' }, { '0', '0', '1' } };
    private static TreeNode Bst() => new(4, new TreeNode(2, new TreeNode(1), new TreeNode(3)), new TreeNode(6));
    private static Dictionary<int, List<int>> Edges() => new() { [1] = [2, 3], [2] = [3], [3] = [] };

    private static BoardVisualizerData Queens()
    {
        var board = new BoardVisualizerData(4, 4, checkerboard: true);
        board[0, 1].Value = "Q";
        board[1, 3].Value = "Q";
        board[1, 3].IsConflict = true;
        board[2, 0].IsActive = true;
        board[3, 2].FillColor = "#ff0000";
        board[3, 2].SubLabel = "dp";
        return board;
    }

    private static VisualizerScene Scene()
    {
        var scene = new VisualizerScene(420, 220);
        scene.AddRect(10, 10, 80, 40, "box", cornerRadius: 6);
        scene.AddCircle(150, 40, 18, "c");
        scene.AddLine(0, 100, 400, 100, isDashed: true);
        scene.AddArrow(90, 30, 130, 40, "to");
        scene.AddText(200, 150, "Hello, 世界", 14, isBold: true, isCentered: true);
        return scene;
    }

    // Each kind as the C# side modelled it (what Display.* built before it made specs), and the Display call today.
    private static readonly Dictionary<string, (Func<VisualizerOptions> Model, Action Display)> VisualizerCases = new()
    {
        ["matrix"] = (() =>
        {
            var grid = MatrixDataParser.Parse(Maze());
            grid.ShowCoordinates = false;
            return new VisualizerOptions { Title = "Maze", Kind = VisualizerKind.Matrix, MatrixData = grid, ShowCoordinates = false, CellSize = grid.CellSize };
        }, () => Display.Matrix(Maze(), "Maze", showCoordinates: false)),
        ["islands"] = (() =>
        {
            var grid = MatrixDataParser.Parse(Sea());
            var (islands, steps) = IslandDetectorService.DetectAndGenerateSteps(grid, recordSteps: true, fourDirectional: true);
            return new VisualizerOptions { Title = $"Number of Islands ({islands.Count} Found)", Kind = VisualizerKind.Islands, MatrixData = grid, Sequence = steps };
        }, () => Display.Islands(Sea())),
        ["tree traversal"] = (() =>
        {
            var tree = TreeDataParser.Parse(Bst())!;
            return new VisualizerOptions { Title = "In order", Kind = VisualizerKind.Tree, TreeData = tree, Sequence = TreeDataParser.GenerateTraversalSteps(tree, "inorder") };
        }, () => Display.Tree(Bst(), "In order", "inorder")),
        ["graph"] = (() => new VisualizerOptions { Title = "Edges", Kind = VisualizerKind.Graph, GraphData = GraphDataParser.Parse(Edges(), isDirected: false) },
            () => Display.Graph(Edges(), "Edges", isDirected: false)),
        ["linked list cycle"] = (() =>
        {
            var list = CycleList();
            var data = LinkedListDataParser.Parse(list);
            return new VisualizerOptions { Title = "Linked List (Cycle Detected)", Kind = VisualizerKind.LinkedList, LinkedListData = data, Sequence = LinkedListDataParser.GenerateCycleDetectionSteps(list) };
        }, () => Display.LinkedList(CycleList(), recordCycleSteps: true)),
        ["array"] = (() => new VisualizerOptions { Title = "Two pointers", Kind = VisualizerKind.ArrayPointers, ArrayData = ArrayPointerDataParser.Parse(new[] { 1, 3, 5, 7, 9 }, new { lo = 0, hi = 4 }) },
            () => Display.Array(new[] { 1, 3, 5, 7, 9 }, new { lo = 0, hi = 4 }, "Two pointers")),
        ["bars"] = (() => new VisualizerOptions { Title = "Unsorted", Kind = VisualizerKind.Bars, BarData = new BarChartVisualizerData(new[] { 5.5, 1, 4, 2 }) },
            () => Display.Bars(new[] { 5.5, 1, 4, 2 }, "Unsorted")),
        ["board"] = (() => new VisualizerOptions { Title = "Queens", Kind = VisualizerKind.Board, BoardData = Queens() },
            () => Display.Board(Queens(), "Queens")),
        ["canvas"] = (() => new VisualizerOptions { Title = "Scene", Kind = VisualizerKind.Canvas, SceneData = Scene() },
            () => Display.Canvas(Scene(), "Scene"))
    };

    public static TheoryData<string> VisualizerCaseNames() => new(VisualizerCases.Keys);

    [Theory]
    [MemberData(nameof(VisualizerCaseNames))]
    public void AVisualizer_DrawsTheSameFromItsSpec(string name)
    {
        AssertRedrawsTheSame(VisualizerCases[name].Model());
    }

    // Display.* now builds the spec from the data (and asks the studio for islands, walks and cycle searches, as every
    // language does): the picture is the one the C# model drew.
    [Theory]
    [MemberData(nameof(VisualizerCaseNames))]
    public void AVisualizerDisplayCall_DrawsWhatItsModelDrew(string name)
    {
        var (model, display) = VisualizerCases[name];
        var spec = Assert.IsType<VisualizerSpec>(Displayed(display).Spec);

        Assert.Equal(VisualFingerprints.Of(model()), VisualFingerprints.Of(VisualizerRenderModelBuilder.Build(spec)));
    }

    public static TheoryData<int> Blind75Problems() => new(Blind75CatalogService.GetAllProblems().Select(p => p.Number));

    // The shipped solutions use every tracker (matrix, tree, graph, list, recursion, interval, trie) and the recorder.
    [Theory]
    [MemberData(nameof(Blind75Problems))]
    public async Task EveryBlind75Visualizer_DrawsTheSameFromItsSpec(int number)
    {
        var script = Blind75CatalogService.ConvertToScript(Blind75CatalogService.GetProblemByNumber(number)!);
        var outputs = new List<RichCellOutput>();
        using var origins = VisualOriginCapture.Begin();
        var result = await new NotebookExecutionKernel().ExecuteCellAsync(script.Code, onRichOutput: o => { lock (outputs) outputs.Add(o); });

        Assert.True(result.Success, result.ErrorMessage);
        var visualizers = outputs.Where(o => o.Kind == CellOutputKind.Visualizer).ToList();
        Assert.NotEmpty(visualizers);
        foreach (var output in visualizers)
        {
            AssertRedrawsTheSame(origins.OriginOf<VisualizerOptions>(output.Visual) ?? throw new InvalidOperationException("A visualizer was shown without the model it came from."));
        }
    }

    // Drawn from the spec itself, as the studio draws a C# visual, and from its JSON, as another program's arrives.
    private static void AssertRedrawsTheSame(VisualizerOptions original)
    {
        var expected = VisualFingerprints.Of(original);
        var spec = VisualizerOptionsConverter.ToSpec(original);

        Assert.Empty(VisualSpecValidator.Validate(spec));
        foreach (var (how, drawn) in new[] { ("its spec", spec), ("its spec's JSON", VisualJson.Clone(spec)) })
        {
            var actual = VisualFingerprints.Of(VisualizerRenderModelBuilder.Build(drawn));
            if (expected == actual) continue;

            var (e, a) = (expected.Split('\n'), actual.Split('\n'));
            var line = Enumerable.Range(0, Math.Min(e.Length, a.Length)).FirstOrDefault(i => e[i] != a[i], Math.Min(e.Length, a.Length));
            Assert.Fail($"'{original.Title}' ({original.Kind}) draws differently from {how} at line {line}:\nexpected: {e.ElementAtOrDefault(line)}\nactual:   {a.ElementAtOrDefault(line)}");
        }
    }

    private static VisualOutput Displayed(Action display)
    {
        RichCellOutput? output = null;
        using (InteractiveDisplayContext.EnterScope(o => output ??= o))
        {
            display();
        }

        return output?.Visual ?? throw new InvalidOperationException(output?.Text ?? "Nothing was displayed.");
    }
}
