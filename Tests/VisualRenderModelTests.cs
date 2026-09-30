using System.Diagnostics;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// Specs as other languages write them (the fixtures) drawn by the studio: defaults filled in, a gap a gap, the work the
/// studio does itself (islands, a traversal, a cycle), and thousands of steps without holding thousands of copies.
/// </summary>
public class VisualRenderModelTests
{
    private static T Fixture<T>(string file) where T : VisualSpec => (T)VisualJsonTests.ReadFixture(file);

    [Theory]
    [MemberData(nameof(VisualJsonTests.Fixtures), MemberType = typeof(VisualJsonTests))]
    public void EveryFixture_IsDrawnWithNothingLeftOut(string file)
    {
        string? notice = VisualJsonTests.ReadFixture(file) switch
        {
            ChartSpec chart => ChartRenderModelBuilder.Build(chart).Notice,
            Plot3DSpec plot => Plot3DRenderModelBuilder.Build(plot).Notice,
            VisualizerSpec visualizer => VisualizerRenderModelBuilder.Build(visualizer).Notice,
            _ => throw new InvalidOperationException(file)
        };

        Assert.Null(notice);
    }

    [Fact]
    public void AChart_FillsInWhatItsSpecLeavesOut_AndAMissingValueIsAGap()
    {
        var chart = ChartRenderModelBuilder.Build(Fixture<ChartSpec>("chart-line.json"));

        Assert.True(chart.ShowLegend);
        Assert.Equal(("Month", "USD", 0.0), (chart.XAxisTitle, chart.YAxisTitle, chart.YMin));
        var storeA = chart.Series[0];
        Assert.Equal(new double[] { 0, 1, 2, 3 }, storeA.Points.Select(p => p.X));           // x is the index when left out
        Assert.True(double.IsNaN(storeA.Points[2].Y));                                       // null is a gap
        Assert.Equal("#4ec9b0", storeA.Color);                                               // the chart's colour for the first series
        Assert.Equal("#ff0000", chart.Series[1].Points[2].CustomColor);
        Assert.Equal(ChartType.Line, chart.Type);
    }

    [Fact]
    public void AHistogram_IsCountedByTheStudio()
    {
        var chart = ChartRenderModelBuilder.Build(Fixture<ChartSpec>("chart-histogram.json"));

        var bins = Assert.Single(chart.Series).Points;
        Assert.Equal(8, bins.Count);
        Assert.Equal(9, bins.Sum(b => b.Y)); // the null sample isn't counted
        Assert.Equal(ChartType.Bar, chart.Type); // what is drawn: bars of the counts
        Assert.StartsWith("11-", bins[0].Label);
    }

    [Fact]
    public void ASurface_KeepsItsHoles()
    {
        var plot = Plot3DRenderModelBuilder.Build(Fixture<Plot3DSpec>("plot3d-surface.json"));

        var surface = plot.Surface!;
        Assert.Equal((5, 5), (surface.ResolutionX, surface.ResolutionY));
        Assert.True(double.IsNaN(surface.ZValues[2, 2]));
        Assert.Equal((-1.0, 1.0, -2.0, 2.0), (surface.MinX, surface.MaxX, surface.MinY, surface.MaxY));
        Assert.Equal((0.0, 2.0), (surface.MinZ, surface.MaxZ)); // the hole isn't a height
        Assert.False(plot.ShowFloorGrid);
    }

    [Fact]
    public void AGraphWithoutPositions_IsLaidOutByTheStudio()
    {
        var plot = Plot3DRenderModelBuilder.Build(Fixture<Plot3DSpec>("plot3d-graph.json"));

        var nodes = plot.Graph!.Nodes;
        Assert.True(nodes.Select(n => (n.X, n.Y, n.Z)).Distinct().Count() == nodes.Count, "every node has a place of its own");
        Assert.False(plot.Graph.Edges[0].IsDirected);
        Assert.True(plot.Graph.Edges[1].IsDirected);
    }

    [Fact]
    public void AGrid_ReadsWhatItsValuesSay_AndItsStepsChangeIt()
    {
        var visualizer = VisualizerRenderModelBuilder.Build(Fixture<VisualizerSpec>("visualizer-matrix.json"));

        var grid = visualizer.MatrixData!;
        Assert.Equal(CellKind.Water, grid[0, 0].Kind);   // 0 is water
        Assert.Equal(CellKind.Land, grid[1, 0].Kind);    // 1 is land
        Assert.Equal(CellKind.Wall, grid[0, 2].Kind);    // the cell says it's a wall, over its value
        Assert.Equal(CellKind.Start, grid[2, 0].Kind);   // "S"

        var steps = visualizer.Sequence!.Steps;
        Assert.Equal(3, steps.Count);
        Assert.Equal(GridCellState.Visited, ((GridMatrixData)steps[1].Snapshot!)[1, 0].State);
        Assert.Contains((1, 0), steps[1].ActiveCells);
        Assert.Equal("2", steps[1].AuxiliaryInfo["queue size"]);
        Assert.Equal(7, steps[1].SourceLine);

        // The last step draws a whole new state: the visited cell is plain again, the path cell isn't.
        var last = (GridMatrixData)steps[2].Snapshot!;
        Assert.Equal(GridCellState.Default, last[1, 0].State);
        Assert.Equal(GridCellState.Path, last[2, 2].State);
        Assert.Contains("me", last[2, 2].SubLabel);
    }

    [Fact]
    public void Islands_AreFoundByTheStudio()
    {
        var visualizer = VisualizerRenderModelBuilder.Build(Fixture<VisualizerSpec>("visualizer-islands.json"));

        Assert.Equal(2, visualizer.MatrixData!.Islands.Count);
        Assert.Equal("Number of islands", visualizer.Title);
        Assert.Null(visualizer.Sequence); // recordSteps: false
    }

    [Fact]
    public void ATraversal_IsRecordedByTheStudio()
    {
        var visualizer = VisualizerRenderModelBuilder.Build(Fixture<VisualizerSpec>("visualizer-trie.json"));

        Assert.NotNull(visualizer.Sequence);
        Assert.True(visualizer.Sequence!.TotalSteps >= 4);
        Assert.Equal(TreeNodeState.Matched, visualizer.TreeData!.Children[0].Children[0].State);
    }

    [Fact]
    public void AListsCycle_IsFoundAndMarkedByTheStudio()
    {
        var visualizer = VisualizerRenderModelBuilder.Build(Fixture<VisualizerSpec>("visualizer-linked-list.json"));

        var list = visualizer.LinkedListData!;
        Assert.True(list.HasCycle);
        Assert.Equal(1, list.CycleTargetIndex);
        Assert.True(list.Nodes[1].IsCycleTarget);
        Assert.Equal("Cycle", visualizer.Title);

        var pointers = ((LinkedListData)visualizer.Sequence!.Steps[0].Snapshot!).Pointers;
        Assert.Equal(new[] { ("slow", 0), ("fast", 0) }, pointers.Select(p => (p.Name, p.Index)));
    }

    [Fact]
    public void AnArraysStep_BringsItsOwnPointersAndHighlights()
    {
        var visualizer = VisualizerRenderModelBuilder.Build(Fixture<VisualizerSpec>("visualizer-array.json"));

        var step = visualizer.Sequence!.Steps[0];
        var pointers = Assert.IsType<List<PointerMarkerData>>(step.CustomData);
        Assert.Equal(new[] { ("i", 1), ("j", 7) }, pointers.Select(p => (p.Name, p.Index)));
        Assert.Equal(new[] { 1, 7 }, step.ActiveCells.Select(c => c.Col));
        Assert.Equal("true", visualizer.ArrayData!.Items[4].DisplayValue);
        Assert.Equal("null", visualizer.ArrayData.Items[6].DisplayValue);
    }

    // 5,000 steps, each changing one of 400 items: building is immediate, any step can be looked at, and every one
    // matches applying the changes one by one.
    [Fact]
    public void ThousandsOfSteps_AreBuiltOnlyWhenLookedAt_AndMatchPlayingThemInOrder()
    {
        const int items = 400, steps = VisualLimits.MaxSteps;
        var spec = new VisualizerSpec
        {
            Kind = VisualizerKind.ArrayPointers,
            State = { Array = new ArrayState { Values = Enumerable.Range(0, items).Select(i => (ScalarValue)i).ToList() } }
        };
        for (var s = 0; s < steps; s++)
        {
            spec.Steps.Add(new VisualizerStepSpec { Changes = new VisualizerChangesSpec { Items = [new ItemSpec { Index = s % items, Value = s }] } });
        }

        var clock = Stopwatch.StartNew();
        var visualizer = VisualizerRenderModelBuilder.Build(spec);
        Assert.True(clock.ElapsedMilliseconds < 1_000, $"building took {clock.ElapsedMilliseconds} ms");

        foreach (var index in new[] { steps - 1, 17, 2_500, 0, 4_096, steps - 1 })
        {
            var array = (ArrayPointerData)visualizer.Sequence!.Steps[index].Snapshot!;
            for (var i = 0; i < items; i++)
            {
                // The last step at or before `index` that set item i, else its starting value.
                var lastSet = index - ((index - i) % items + items) % items;
                var expected = lastSet >= 0 && lastSet <= index ? lastSet : i;
                Assert.Equal(expected.ToString(), array.Items[i].DisplayValue);
            }
        }
    }
}
