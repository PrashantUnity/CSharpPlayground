using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// C# data becomes a spec by the conventions every language follows, and the Display API is the same shape for every
/// visual: data first, then the title, settings left out are the studio's, `configure` last.
/// </summary>
public class VisualBuildersTests
{
    // Charts

    // A null in the middle was skipped, so every value after it moved one place left.
    [Fact]
    public void AMissingNumber_IsAGapInItsPlace()
    {
        var series = Assert.Single(ChartSpecBuilder.From(new double?[] { 1, null, 3, double.NaN, 5 }).Series);

        Assert.Equal([1, null, 3, null, 5], series.Y);
        Assert.Null(series.X);
        Assert.Null(series.Labels);
    }

    [Fact]
    public void Pairs_AreXAndY()
    {
        var series = Assert.Single(ChartSpecBuilder.From(new[] { (1, 2.5), (3, 4.0) }).Series);

        Assert.Equal([1, 3], series.X!);
        Assert.Equal([2.5, 4], series.Y);
    }

    [Fact]
    public void ALabelledMap_IsLabelsAndValues()
    {
        var series = Assert.Single(ChartSpecBuilder.From(new Dictionary<string, int> { ["Chrome"] = 60, ["Safari"] = 25 }).Series);

        Assert.Equal(["Chrome", "Safari"], series.Labels!);
        Assert.Equal([60, 25], series.Y);
    }

    // A name → sequence map came out an empty chart.
    [Fact]
    public void ANameToSequenceMap_IsASeriesEach()
    {
        var spec = ChartSpecBuilder.From(new Dictionary<string, int[]> { ["North"] = [1, 2, 3], ["South"] = [3, 2, 1] });

        Assert.Equal(["North", "South"], spec.Series.Select(s => s.Name));
        Assert.Equal([3, 2, 1], spec.Series[1].Y);
    }

    [Fact]
    public void Records_AreReadByTheirMembersNames()
    {
        var sales = new[] { new { Month = "Jan", Revenue = 120.5, Units = 4 }, new { Month = "Feb", Revenue = 99.25, Units = 3 } };

        var series = Assert.Single(ChartSpecBuilder.From(sales).Series);

        Assert.Equal([120.5, 99.25], series.Y);
        Assert.Null(series.Labels); // Month isn't a place name, so the values sit at their index
        var named = Assert.Single(ChartSpecBuilder.From(new[] { new { Region = "EU", Total = 7 } }).Series);
        Assert.Equal(["EU"], named.Labels!);
        Assert.Equal([7], named.Y);
    }

    [Fact]
    public void Selectors_SayWhereAndWhat()
    {
        var sales = new[] { new { Month = "Jan", Revenue = 120.5 }, new { Month = "Feb", Revenue = 99.25 } };

        var series = Assert.Single(ChartSpecBuilder.From(sales, s => s.Month, s => s.Revenue).Series);

        Assert.Equal(["Jan", "Feb"], series.Labels!);
        Assert.Equal([120.5, 99.25], series.Y);
    }

    [Fact]
    public void OneNumber_IsNotAChart()
    {
        var error = Assert.Throws<ArgumentException>(() => ChartSpecBuilder.From(5));

        Assert.Contains("Int32", error.Message);
    }

    [Fact]
    public void AHistogram_SendsTheSamples_ForTheStudioToCount()
    {
        var spec = ChartSpecBuilder.Histogram(new[] { 1.0, 2, double.NaN, 3 }, bins: 5);

        Assert.Equal(ChartType.Histogram, spec.Kind);
        Assert.Equal(5, spec.Bins);
        Assert.Equal([1, 2, null, 3], Assert.Single(spec.Series).Values!);
    }

    // 3D

    // (int, int, int) tuples gave an empty plot; only (double, double, double) was recognised.
    [Fact]
    public void PointsOfAnyNumbers_ArePlotted()
    {
        var series = Assert.Single(Plot3DSpecBuilder.From(new List<(int, long, float)> { (1, 2, 3), (4, 5, 6.5f) }).Series);

        Assert.Equal([1, 4], series.X);
        Assert.Equal([3, 6.5], series.Z);
    }

    // Properties were read from the first item's type, so a second anonymous type threw; a text coordinate threw too.
    [Fact]
    public void RecordsOfMixedTypes_ArePlotted_AndOneWithoutANumber_IsLeftOut()
    {
        var rows = new object[] { new { X = 1.0, Y = 2.0, Z = 3.0, Label = "a" }, new { X = 4, Y = 5, Z = 6 }, new { X = "left", Y = 2.0, Z = 3.0 } };

        var series = Assert.Single(Plot3DSpecBuilder.From(rows).Series);

        Assert.Equal([1, 4], series.X);
        Assert.Equal(["a", null], series.Labels!);
    }

    [Fact]
    public void AGrid_IsASurface_ARowPerY()
    {
        var surface = Plot3DSpecBuilder.From(new[,] { { 1, 2, 3 }, { 4, 5, 6 } }, Plot3DType.Surface).Surface!;

        Assert.Equal(2, surface.Z.Count);
        Assert.Equal([4, 5, 6], surface.Z[1]);
        Assert.Equal((0.0, 2.0, 0.0, 1.0), (surface.X.Min, surface.X.Max, surface.Y.Min, surface.Y.Max));
    }

    [Fact]
    public void AGraphWithoutPositions_IsLaidOutByTheStudio_AndOneWithPositions_KeepsThem()
    {
        var loose = new Graph3DData();
        loose.AddNode("a");
        loose.AddNode("b");
        var placed = new Graph3DData();
        placed.AddNode("a", x: 1, y: 2, z: 3);

        Assert.All(Plot3DSpecBuilder.From(loose).Graph!.Nodes, n => Assert.Null(n.X));
        Assert.Equal(3, Plot3DSpecBuilder.From(placed).Graph!.Nodes[0].Z);
    }

    [Fact]
    public void AnAdjacencyMap_IsAGraph_WhenAGraphIsAskedFor()
    {
        var graph = Plot3DSpecBuilder.From(new Dictionary<string, string[]> { ["a"] = ["b", "c"], ["b"] = ["c"] }, Plot3DType.Graph3D).Graph!;

        Assert.Equal(["a", "b", "c"], graph.Nodes.Select(n => n.Id));
        Assert.Equal(3, graph.Edges.Count);
    }

    // Visualizers

    private sealed class ListNode(int val, ListNode? next = null)
    {
        public int Val { get; } = val;
        public ListNode? Next { get; } = next;
    }

    private sealed class TreeNode(int val, TreeNode? left = null)
    {
        public int Val { get; } = val;
        public TreeNode? Left { get; } = left;
        public TreeNode? Right => null;
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Visualize_WorksOutTheKind(object data, VisualizerKind kind)
    {
        Assert.Equal(kind, VisualizerSpecBuilder.From(data).Kind);
    }

    public static TheoryData<object, VisualizerKind> Shapes() => new()
    {
        { new[,] { { 1, 0 }, { 0, 1 } }, VisualizerKind.Matrix },
        { new[] { new[] { 1, 0 }, new[] { 0, 1 } }, VisualizerKind.Matrix },
        { new ListNode(1, new ListNode(2)), VisualizerKind.LinkedList },
        { new TreeNode(2, new TreeNode(1)), VisualizerKind.Tree },
        { new Dictionary<int, int[]> { [1] = [2] }, VisualizerKind.Graph },
        { new[] { 3, 1, 2 }, VisualizerKind.ArrayPointers }
    };

    [Fact]
    public void Visualize_SaysWhenItCantTell()
    {
        var error = Assert.Throws<ArgumentException>(() => VisualizerSpecBuilder.From(new { Size = 3 }));

        Assert.Contains("Display.Matrix", error.Message);
    }

    // The API

    [Fact]
    public void Configure_ChangesTheSpecLast_AndSettingsLeftOutStayOut()
    {
        var chart = Emit(() => Display.LineChart(new[] { 1, 2, 3 }, "Sales", configure: s =>
        {
            s.YAxis.Title = "EUR";
            s.Title = "Sales, net";
        }));

        Assert.Equal(("Sales, net", "EUR"), (chart.Title, chart.YAxis.Title));
        Assert.Null(chart.ShowPoints);
        Assert.Null(chart.Width);
    }

    [Fact]
    public void EveryVisual_HasADisplayExtension_ThatHandsTheDataBack()
    {
        var values = new[] { 3, 1, 2 };
        var shown = new List<VisualSpec>();
        using (InteractiveDisplayContext.EnterScope(o => shown.Add(o.Visual!.Spec)))
        {
            Assert.Same(values, values.DisplayLineChart("line"));
            Assert.Same(values, values.DisplayHistogram("histogram"));
            Assert.Same(values, values.DisplayBars("bars"));
            Assert.Same(values, values.DisplayVisualizer("array"));
            new[] { (1, 2, 3) }.DisplayScatter3D("points");
        }

        Assert.Equal(["line", "histogram", "bars", "array", "points"], shown.Select(s => s.Title));
    }

    [Fact]
    public void TheOlderNames_StillWork()
    {
        var shown = new List<VisualSpec>();
        using (InteractiveDisplayContext.EnterScope(o => shown.Add(o.Visual!.Spec)))
        {
#pragma warning disable CS0618
            new[] { 1, 2 }.LineChart("old line");
            new[] { new Point3D(1, 2, 3) }.Dump3D("old 3D");
#pragma warning restore CS0618
        }

        Assert.Equal(["old line", "old 3D"], shown.Select(s => s.Title));
    }

    [Fact]
    public void ARecorder_StartsFromTheKindsData()
    {
        var recorder = Display.Recorder(VisualizerKind.ArrayPointers, new[] { 5, 1, 4 }, "Bubble sort");
        recorder.Step("compare", highlight: [0, 1]);

        Assert.Equal(VisualizerKind.ArrayPointers, recorder.Options.Kind);
        Assert.Equal(2, recorder.Sequence.TotalSteps);
        Assert.Throws<ArgumentException>(() => Display.Recorder(VisualizerKind.LinkedList, new ListNode(1)));
    }

    // Trackers colour the same way (Mark with a state, Paint with a colour), chain, and describe themselves as a spec.
    [Fact]
    public void Trackers_ChainAndShareOneWayToColour()
    {
        var graph = GraphTracker.Create(new Dictionary<int, int[]> { [1] = [2], [2] = [] }, "g")
            .Mark(1, ElementState.Done)
            .Paint(2, "#ff0000")
            .MarkEdge(1, 2, ElementState.Path)
            .Snapshot("coloured");
        var list = LinkedListTracker.Create(new ListNode(1, new ListNode(2)), "l");
        var head = new ListNode(1);
        list.Mark(head, ElementState.Current).Paint(head, "#00ff00").Unmark(head);
        var grid = MatrixTracker.CreateEmpty(2, 2, "m").Mark(0, 0, ElementState.Wall).Paint(1, 1, "#0000ff");

        Assert.Equal(VisualizerKind.Graph, Assert.IsType<VisualizerSpec>(graph.ToVisualSpec()).Kind);
        Assert.Equal(GraphNodeState.Visited, graph.Graph.FindNode("1")!.State);
        Assert.Equal("#ff0000", graph.Graph.FindNode("2")!.Color);
        Assert.Equal((GridCellState.Wall, "#0000ff"), (grid.Grid[0, 0].State, grid.Grid[1, 1].CustomColor));
        Assert.IsType<VisualizerSpec>(list.ToVisualSpec());
    }

    [Fact]
    public void ATracker_IsShownByDisplayShow()
    {
        var tracker = MatrixTracker.CreateEmpty(2, 2, "shown").Visit(0, 0, "start");

        var spec = Emit<VisualSpec>(() => Display.Show(tracker));

        Assert.Equal("shown", spec.Title);
    }

    private static T Emit<T>(Func<Visuals.Interaction.DisplayHandle<T>> display) where T : VisualSpec
    {
        T? spec = null;
        using (InteractiveDisplayContext.EnterScope(o => spec = (T)o.Visual!.Spec))
        {
            display();
        }

        return spec!;
    }
}
