using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class VisualizerWatchAndDiffTests
{
    private sealed class ListNode
    {
        public int val;
        public ListNode(int v) => val = v;
    }

    private sealed class ExplodingCollection : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("boom");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void WatchCapture_ShowsCollectionsInTheOrderItemsComeOut()
    {
        var queue = new Queue<int>(new[] { 1, 2, 3 });
        var stack = new Stack<string>();
        stack.Push("a");
        stack.Push("b");
        var minHeap = new PriorityQueue<string, int>();
        minHeap.Enqueue("far", 9);
        minHeap.Enqueue("near", 1);
        var maxHeap = new PriorityQueue<string, int>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
        maxHeap.Enqueue("small", 1);
        maxHeap.Enqueue("big", 9);

        Assert.Equal("Queue: 1 | 2 | 3", Describe(WatchCapture.Capture("q", queue)));
        Assert.Equal("Stack: b | a", Describe(WatchCapture.Capture("s", stack)));
        Assert.Equal("PriorityQueue: near (1) | far (9)", Describe(WatchCapture.Capture("pq", minHeap)));
        Assert.Equal(new[] { "big (9)", "small (1)" }, WatchCapture.Capture("pq", maxHeap).Items);
    }

    [Fact]
    public void WatchCapture_DescribesMapsSetsNodesAndTuplesReadably()
    {
        var dist = new Dictionary<string, double> { ["A"] = 0, ["B"] = double.PositiveInfinity };
        var visited = new HashSet<(int, int)> { (0, 1) };
        var nodes = new List<ListNode> { new(7), new(8) };
        var levels = new List<List<int>> { new() { 3 }, new() { 9, 20 } };

        Assert.Equal("Map: A: 0 | B: ∞", Describe(WatchCapture.Capture("dist", dist)));
        Assert.Equal("Set: (0, 1)", Describe(WatchCapture.Capture("visited", visited)));
        Assert.Equal(new[] { "7", "8" }, WatchCapture.Capture("nodes", nodes).Items);
        Assert.Equal(new[] { "[3]", "[9, 20]" }, WatchCapture.Capture("levels", levels).Items);
        Assert.Equal("Value: 42", Describe(WatchCapture.Capture("count", 42)));
    }

    [Fact]
    public void FormatScalarValue_FormatsInfinitiesAndScalarsConsistentlyAcrossPlatforms()
    {
        Assert.Equal("∞", ObjectInspectorBuilder.FormatScalarValue(double.PositiveInfinity));
        Assert.Equal("-∞", ObjectInspectorBuilder.FormatScalarValue(double.NegativeInfinity));
        Assert.Equal("NaN", ObjectInspectorBuilder.FormatScalarValue(double.NaN));
        Assert.Equal("∞", ObjectInspectorBuilder.FormatScalarValue(float.PositiveInfinity));
        Assert.Equal("-∞", ObjectInspectorBuilder.FormatScalarValue(float.NegativeInfinity));
        Assert.Equal("NaN", ObjectInspectorBuilder.FormatScalarValue(float.NaN));
    }

    [Fact]
    public void WatchCapture_CapsLongCollectionsAndSurvivesThrowingEnumerators()
    {
        var big = WatchCapture.Capture("big", Enumerable.Range(0, 100).ToList());
        Assert.Equal(WatchCapture.MaxItems, big.Items.Count);
        Assert.Equal(100, big.Count);

        var broken = WatchCapture.Capture("broken", new ExplodingCollection());
        Assert.Equal(new[] { "(InvalidOperationException)" }, broken.Items);
    }

    [Fact]
    public void Watch_SnapshotsTheLiveCollectionAtEveryLaterStepUnderItsVariableName()
    {
        var recorder = VisualizerRecorder.CreateArray(ArrayPointerDataParser.Parse(new[] { 1, 2 }));
        var queue = new Queue<int>();
        recorder.Watch(queue);

        queue.Enqueue(5);
        recorder.Step("enqueue 5");
        queue.Enqueue(6);
        queue.Dequeue();
        recorder.Step("enqueue 6, dequeue 5");

        Assert.Empty(recorder.Sequence.Steps[0].Watches);
        Assert.Equal("queue", recorder.Sequence.Steps[1].Watches.Single().Name);
        Assert.Equal(new[] { "5" }, recorder.Sequence.Steps[1].Watches.Single().Items);
        Assert.Equal(new[] { "6" }, recorder.Sequence.Steps[2].Watches.Single().Items);

        recorder.Watch(new Stack<int>(), "queue");
        recorder.Step("replaced");
        Assert.Equal(WatchKind.Stack, recorder.Sequence.Steps[3].Watches.Single().Kind);
    }

    [Fact]
    public void Trackers_WatchCollectionsToo()
    {
        var tree = TreeTracker.Create("[1, 2, 3]");
        var stack = new Stack<string>(new[] { "1" });
        tree.Watch(stack);
        tree.Visit("1");

        var graph = GraphTracker.Create("[[0,1]]");
        var frontier = new Queue<string>(new[] { "0" });
        graph.Watch(frontier);
        graph.Visit("0");

        var grid = MatrixTracker.CreateEmpty(2, 2);
        var seen = new HashSet<(int, int)> { (0, 0) };
        grid.Watch(seen).Visit(0, 0);

        Assert.Equal("stack", tree.Sequence.Steps[^1].Watches.Single().Name);
        Assert.Equal("frontier", graph.Sequence.Steps[^1].Watches.Single().Name);
        Assert.Equal("seen", grid.Sequence.Steps[^1].Watches.Single().Name);
    }

    [Fact]
    public void StepChanges_ReportChangedValuesButNotTraversalState()
    {
        var before = ArrayPointerDataParser.Parse(new[] { 1, 2, 3 });
        var after = ArrayPointerDataParser.Parse(new[] { 3, 2, 1 });
        Assert.Equal(new[] { 0, 2 }, StepChanges.ArrayItems(before, after).OrderBy(i => i));

        var barsBefore = new BarChartVisualizerData(new[] { 5, 1 });
        var barsAfter = new BarChartVisualizerData(new[] { 1, 5 });
        Assert.Equal(2, StepChanges.Bars(barsBefore, barsAfter).Count);

        var gridBefore = MatrixDataParser.Parse(new[,] { { 0, 0 }, { 0, 0 } });
        var gridAfter = gridBefore.Clone();
        gridAfter[1, 1].SubLabel = "dp=4";
        gridAfter[0, 0].State = GridCellState.Visited;
        Assert.Equal(new[] { (1, 1) }, StepChanges.MatrixCells(gridBefore, gridAfter));

        var graphBefore = GraphDataParser.Parse("[[0,1]]");
        var graphAfter = graphBefore.Clone();
        graphAfter.FindNode("1")!.SubLabel = "d=4";
        graphAfter.FindNode("0")!.State = GraphNodeState.Visited;
        Assert.Equal(new[] { "1" }, StepChanges.GraphNodes(graphBefore, graphAfter));
    }

    [Fact]
    public void StepChanges_TreeNodes_FlagsNewNodesAndSwappedChildren()
    {
        var before = TreeDataParser.ParseLeetCodeString("[1, 2, 3]")!;
        var swapped = before.Clone();
        swapped.SwapChildren();
        var grown = before.Clone();
        grown.Left!.Left = new TreeNodeData("4", "node_new") { Parent = grown.Left };
        grown.Left.Children.Add(grown.Left.Left);

        Assert.Equal(new[] { "2", "3" }, StepChanges.TreeNodes(before, swapped).Select(id => swapped.FindNode(id)!.DisplayValue).OrderBy(v => v));
        Assert.Equal(new[] { "node_new" }, StepChanges.TreeNodes(before, grown));
        Assert.Empty(StepChanges.TreeNodes(null, before));
    }

    [Fact]
    public void StepChanges_AddedWatchItems_MarksOnlyWhatJustArrived()
    {
        var before = new StepWatch("queue", WatchKind.Queue, new[] { "a", "b" }, 2);
        var after = new StepWatch("queue", WatchKind.Queue, new[] { "b", "c", "c" }, 3);
        var previousHadOneC = new StepWatch("queue", WatchKind.Queue, new[] { "c" }, 1);

        Assert.Equal(new[] { false, true, true }, StepChanges.AddedWatchItems(before, after));
        Assert.Equal(new[] { false, false, true }, StepChanges.AddedWatchItems(new StepWatch("queue", WatchKind.Queue, new[] { "b", "c" }, 2), after));
        Assert.Equal(new[] { true, false, true }, StepChanges.AddedWatchItems(previousHadOneC, new StepWatch("queue", WatchKind.Queue, new[] { "b", "c", "c" }, 3)));
    }

    [Fact]
    public void WatchRows_ShowQueueEndsOverflowAndEmptyCollections()
    {
        var sequence = new VisualizerSequence(new[]
        {
            new VisualizerStep(0, "start") { Watches = { new StepWatch("queue", WatchKind.Queue, new[] { "a" }, 1) } },
            new VisualizerStep(1, "grow")
            {
                Watches =
                {
                    new StepWatch("queue", WatchKind.Queue, new[] { "a", "b" }, 30),
                    new StepWatch("stack", WatchKind.Stack, Array.Empty<string>(), 0)
                }
            }
        });
        sequence.SeekStep(1);

        var rows = VisualizerPlaybackControl.BuildWatchRows(sequence);

        Assert.Equal(new[] { "front", "a", "b", "+28 more", "back" }, rows[0].Cells.Select(c => c.Text));
        Assert.Equal(new[] { false, false, true, false, false }, rows[0].Cells.Select(c => c.IsAdded));
        Assert.Equal("Queue • 30 items", rows[0].Summary);
        Assert.Equal(new[] { "empty" }, rows[1].Cells.Select(c => c.Text));
        Assert.True(rows[1].Cells[0].IsMarker);
    }

    [Fact]
    public async Task Templates_DijkstraAndCourseSchedule_ShowTheirQueuesAtEachStep()
    {
        var dijkstra = await RunTemplateAsync("dijkstra_shortest_path");
        var pqStrips = dijkstra.Steps.SelectMany(s => s.Watches).Where(w => w.Name == "pq").ToList();
        Assert.NotEmpty(pqStrips);
        Assert.Contains(pqStrips, w => w.Items.Contains("2 (2)"));

        var schedule = await RunTemplateAsync("leetcode_207_course_schedule");
        var finalWatches = schedule.Steps[^1].Watches;
        Assert.Equal(new[] { "0", "1", "2", "3", "4" }, finalWatches.Single(w => w.Name == "order").Items);
        Assert.Equal(0, finalWatches.Single(w => w.Name == "queue").Count);
    }

    private static async Task<VisualizerSequence> RunTemplateAsync(string templateId)
    {
        var template = CodeTemplateLibrary.GetTemplates().Single(t => t.Id == templateId);
        RichCellOutput? output = null;
        var result = await new NotebookExecutionKernel().ExecuteCellAsync(template.InitialCode, onRichOutput: r => output = r);
        Assert.True(result.Success, result.ErrorMessage);
        return output!.VisualizerOptions!.Sequence!;
    }

    private static string Describe(StepWatch watch) => $"{watch.Kind}: {string.Join(" | ", watch.Items)}";
}
