using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>The pieces the Blind 75 problems are built from: Judge, value watches, array/bar steps, interval and trie trackers.</summary>
public class LearningToolkitTests
{
    private sealed class ListNode
    {
        public int val;
        public ListNode? next;
        public ListNode(int val, ListNode? next = null) { this.val = val; this.next = next; }
    }

    private sealed class TreeNode
    {
        public int val;
        public TreeNode? left, right;
        public TreeNode(int val, TreeNode? left = null, TreeNode? right = null) { this.val = val; this.left = left; this.right = right; }
    }

    private sealed class GraphNode
    {
        public int val;
        public List<GraphNode> neighbors = new();
        public GraphNode(int val) => this.val = val;
    }

    private sealed class DictionaryTrieNode
    {
        public Dictionary<char, DictionaryTrieNode> Children { get; } = new();
        public bool IsEnd { get; set; }
    }

    private sealed class ArrayTrieNode
    {
        public ArrayTrieNode?[] next = new ArrayTrieNode?[26];
        public string? word;
    }

    private static string CaptureConsole(Action action)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try { action(); }
        finally { Console.SetOut(original); }
        return writer.ToString();
    }

    [Fact]
    public void JudgeFormat_PrintsValuesTheWayLeetCodeDoes()
    {
        Assert.Equal("[0,1]", Judge.Format(new[] { 0, 1 }));
        Assert.Equal("[[1,6],[8,10]]", Judge.Format(new[] { new[] { 1, 6 }, new[] { 8, 10 } }));
        Assert.Equal("true", Judge.Format(true));
        Assert.Equal("\"bab\"", Judge.Format("bab"));
        Assert.Equal("2.5", Judge.Format(2.5));
        Assert.Equal("null", Judge.Format(null));
        Assert.Equal("[null,false]", Judge.Format(new object?[] { null, false }));
        Assert.Equal("\"a<b & c\"", Judge.Format("a<b & c"));
    }

    [Fact]
    public void JudgeFormat_SerializesListsTreesAndGraphs()
    {
        Assert.Equal("[1,2,3]", Judge.Format(new ListNode(1, new ListNode(2, new ListNode(3)))));

        var tree = new TreeNode(3, new TreeNode(9), new TreeNode(20, new TreeNode(15), new TreeNode(7)));
        Assert.Equal("[3,9,20,null,null,15,7]", Judge.Format(tree));

        // An empty list, tree or graph is [] when the declared type is a node type.
        Assert.Equal("[]", Judge.Format(null, typeof(ListNode)));
        Assert.Equal("[]", Judge.Format(null, typeof(TreeNode)));
        Assert.Equal("[]", Judge.Format(null, typeof(GraphNode)));
        Assert.Equal("null", Judge.Format(null, typeof(string)));

        var one = new GraphNode(1);
        var two = new GraphNode(2);
        one.neighbors.Add(two);
        two.neighbors.Add(one);
        Assert.Equal("[[2],[1]]", Judge.Format(one));

        var cycle = new ListNode(1, new ListNode(2));
        cycle.next!.next = cycle;
        Assert.Equal("[1,2]", Judge.Format(cycle));
    }

    [Fact]
    public void JudgeCanonical_IgnoresSpacingNumberFormAndOptionalOrder()
    {
        Assert.Equal(Judge.Canonical("[0,1]", false), Judge.Canonical("[ 0, 1 ]", false));
        Assert.Equal(Judge.Canonical("2", false), Judge.Canonical("2.0", false));
        Assert.NotEqual(Judge.Canonical("[1,0]", false), Judge.Canonical("[0,1]", false));
        Assert.Equal(Judge.Canonical("[[3,2],[1]]", true), Judge.Canonical("[[1],[2,3]]", true));
        Assert.Equal(Judge.Canonical("[10,9]", true), Judge.Canonical("[9,10]", true));
        Assert.Equal("not json", Judge.Canonical(" not json ", false));
    }

    [Fact]
    public void JudgeCase_PrintsOneVerdictLinePerCase()
    {
        var judge = new Judge();
        string output = CaptureConsole(() =>
        {
            judge.Case("Example 1", () => new[] { 0, 1 }, "[0,1]");
            judge.Case("Example 2", () => new[] { 2, 1 }, "[1,2]", anyOrder: true);
            judge.Case("Wrong", () => 3, "4");
            judge.Case("Throws", () => Array.Empty<int>()[0], "0");
            judge.Summary();
        });

        Assert.Equal(2, judge.Passed);
        Assert.Equal(2, judge.Failed);
        Assert.Contains("✅ Example 1 → [0,1]", output);
        Assert.Contains("❌ Wrong → got 3 · expected 4", output);
        Assert.Contains("❌ Throws → threw IndexOutOfRangeException", output);
        Assert.Contains("🏁 2/4 passed, 2 failed", output);

        Assert.True(Judge.Verdict(output, "Example 1"));
        Assert.True(Judge.Verdict(output, "Example 2"));
        Assert.False(Judge.Verdict(output, "Wrong"));
        Assert.Null(Judge.Verdict(output, "Example"));
    }

    [Fact]
    public void JudgeVerdict_UsesTheLastLineForACase()
    {
        const string output = "❌ Example 1 → got 1 · expected 2\n✅ Example 1 → 2\n";
        Assert.True(Judge.Verdict(output, "Example 1"));
        Assert.Equal("✅ Example 1 → 2", Judge.LineFor(output, "Example 1"));
    }

    [Fact]
    public void JudgeAgree_ReportsTheFirstDisagreement()
    {
        var judge = new Judge();
        string output = CaptureConsole(() =>
        {
            judge.Agree("Doubles", random => random.Next(0, 10), x => x * 2, x => x + x);
            judge.Agree("Broken", random => random.Next(0, 10), x => x * 2, x => x + 1);
        });

        Assert.Contains("✅ Doubles → 200 random inputs agree with the reference", output);
        Assert.Contains("❌ Broken → input", output);
        Assert.Equal(1, judge.Passed);
        Assert.Equal(1, judge.Failed);
    }

    [Fact]
    public void ValueWatch_ReadsTheVariableAgainAtEveryStep()
    {
        int best = 1;
        var recorder = VisualizerRecorder.CreateArray(new[] { 4, 5, 6 }, "watch");
        recorder.Watch(() => best);
        recorder.Step("first");
        best = 7;
        recorder.Step("second");

        var steps = recorder.Sequence.Steps;
        Assert.Equal("best", steps[1].Watches.Single().Name);
        Assert.Equal("1", steps[1].Watches.Single().Items.Single());
        Assert.Equal("7", steps[2].Watches.Single().Items.Single());
    }

    [Fact]
    public void WatchRows_PutValuesOnOneRowAndLightUpChanges()
    {
        int left = 0, right = 5;
        var seen = new HashSet<int>();
        var recorder = VisualizerRecorder.CreateArray(new[] { 1, 2, 3, 4, 5, 6 }, "rows");
        recorder.Watch(() => left);
        recorder.Watch(() => right);
        recorder.Watch(seen);
        recorder.Step("start");
        left = 1;
        seen.Add(3);
        recorder.Step("move");

        recorder.Sequence.SeekStep(2);
        var rows = VisualizerPlaybackControl.BuildWatchRows(recorder.Sequence);

        Assert.Equal("values", rows[0].Name);
        Assert.Equal(new[] { "left = 1", "right = 5" }, rows[0].Cells.Select(c => c.Text));
        Assert.Equal(new[] { true, false }, rows[0].Cells.Select(c => c.IsAdded));
        Assert.Equal("seen", rows[1].Name);
    }

    [Fact]
    public void ArrayRecorder_AcceptsStringsAndHighlightsRanges()
    {
        var recorder = VisualizerRecorder.CreateArray("abca", "window");
        recorder.Step("window 1..2", pointers: new { left = 1, right = 2 }, highlight: Enumerable.Range(1, 2));

        var step = recorder.Sequence.Steps[^1];
        var cells = ((ArrayPointerData)step.Snapshot!).Items.Select(i => i.DisplayValue);
        Assert.Equal(new[] { "a", "b", "c", "a" }, cells);
        Assert.Equal(new[] { (0, 1), (0, 2) }, step.ActiveCells);
        Assert.Equal(new[] { "left", "right" }, ((List<PointerMarkerData>)step.CustomData!).Select(p => p.Name));
    }

    [Fact]
    public void CellValues_ReadNaturally()
    {
        Assert.Equal("∞", VisualizerValueFormatter.Format(int.MaxValue));
        Assert.Equal("true", VisualizerValueFormatter.Format(true));
        Assert.Equal("[1,2]", VisualizerValueFormatter.Format(new List<int> { 1, 2 }));
        Assert.Equal("[]", VisualizerValueFormatter.Format(new List<int>()));
        Assert.Equal("[2,3]", VisualizerValueFormatter.Format((2, 3)));
    }

    [Fact]
    public void BarRecorder_LabelsPointersAndShadesTheBlockBetweenThem()
    {
        var heights = new[] { 1, 8, 6, 2 };
        var recorder = VisualizerRecorder.CreateBars(heights, "water");
        recorder.Step("walls", pointers: new { left = 1, right = 2, far = 99 }, shade: new BarShade(1, 2, 6, "area 6"));
        heights[3] = 9;
        recorder.Step("grew");

        var walls = (BarChartVisualizerData)recorder.Sequence.Steps[1].Snapshot!;
        Assert.Equal("left", walls.Items[1].PointerLabel);
        Assert.Equal("right", walls.Items[2].PointerLabel);
        Assert.True(walls.Items[1].IsActive && walls.Items[2].IsActive && !walls.Items[0].IsActive);
        Assert.Equal(new BarShade(1, 2, 6, "area 6"), walls.Shade);

        // A later plain step clears the pointers and re-reads the live array.
        var grew = (BarChartVisualizerData)recorder.Sequence.Steps[2].Snapshot!;
        Assert.All(grew.Items, item => Assert.Null(item.PointerLabel));
        Assert.Equal(9, grew.Items[3].Value);
        Assert.Null(grew.Shade);
    }

    [Fact]
    public void IntervalTracker_DrawsRowsFromTheLiveListAndOutlinesNewResults()
    {
        var intervals = new[] { new[] { 8, 10 }, new[] { 1, 3 } };
        var tracker = IntervalTracker.Create(intervals, "merge");
        Array.Sort(intervals, (a, b) => a[0].CompareTo(b[0]));
        var merged = new List<int[]> { new[] { 1, 3 } };
        tracker.Step("first group", current: 0, result: merged, marker: 3, markerLabel: "end 3");

        Assert.Equal(VisualizerKind.Canvas, tracker.Options.Kind);
        Assert.Equal("Intervals: 2", tracker.Options.GetSummaryText());

        var scene = (VisualizerScene)tracker.Sequence.Steps[^1].Snapshot!;
        var tips = scene.Shapes.OfType<SceneRect>().Select(r => r.Tooltip).ToList();
        Assert.Equal(new[] { "intervals[0] = [1,3]", "intervals[1] = [8,10]", "result[0] = [1,3]" }, tips);
        Assert.Equal("#a3e635", scene.Shapes.OfType<SceneRect>().Last().Stroke);
        Assert.Contains(scene.Shapes.OfType<SceneText>(), t => t.Text == "end 3");
    }

    [Fact]
    public void IntervalTracker_ReadsTuplesAndStartEndObjects()
    {
        var tuples = IntervalTracker.Create(new List<(int, int)> { (1, 4), (2, 5) });
        var objects = IntervalTracker.Create(new[] { new { Start = 0, End = 30 } });

        Assert.Equal(2, ((VisualizerScene)tuples.Sequence.Steps[0].Snapshot!).Shapes.OfType<SceneRect>().Count());
        Assert.Equal("intervals[0] = [0,30]", ((VisualizerScene)objects.Sequence.Steps[0].Snapshot!).Shapes.OfType<SceneRect>().Single().Tooltip);
    }

    [Fact]
    public void TrieTracker_FindsDictionaryChildrenAndWordEnds()
    {
        var root = new DictionaryTrieNode();
        var tracker = TrieTracker.Create(root, "trie");
        var node = root;
        foreach (char c in "ab")
        {
            node.Children[c] = new DictionaryTrieNode();
            node = node.Children[c];
        }
        node.IsEnd = true;
        tracker.Step("inserted ab", path: "ab");
        tracker.Step("look for ax", path: "ax");

        var inserted = tracker.Sequence.Steps[1];
        var tree = (TreeNodeData)inserted.Snapshot!;
        Assert.Equal("root", tree.DisplayValue);
        var b = tree.Children.Single().Children.Single();
        Assert.Equal("ab", b.SubLabel);
        Assert.Equal(TreeNodeState.Current, b.State);
        Assert.Equal(new[] { b.Id }, inserted.ActiveNodeIds);

        // Both new letters are new since the empty trie.
        var changed = StepChanges.TreeNodes((TreeNodeData)tracker.Sequence.Steps[0].Snapshot!, tree);
        Assert.Contains(b.Id, changed);

        var missing = tracker.Sequence.Steps[2];
        Assert.Equal("a", missing.AuxiliaryInfo["path"]);
        Assert.Equal("'x'", missing.AuxiliaryInfo["missing"]);
    }

    [Fact]
    public void TrieTracker_FindsArrayChildrenAndAStringWordMarker()
    {
        var root = new ArrayTrieNode();
        root.next['c' - 'a'] = new ArrayTrieNode { word = "c" };
        var tracker = TrieTracker.Create(root);
        tracker.Step("one word");

        var tree = (TreeNodeData)tracker.Sequence.Steps[^1].Snapshot!;
        var c = tree.Children.Single();
        Assert.Equal("c", c.DisplayValue);
        Assert.Equal(TreeNodeState.Matched, c.State);
    }

    [Fact]
    public void ComputeFit_NeverZoomsPastTheCap()
    {
        Rect? tiny(double zoom, int? step) => new Rect(20, 20, 50 * zoom, 30 * zoom);
        var canvas = new Size(632, 332);

        var open = VisualizerViewport.ComputeFit(tiny, canvas, new int?[] { null }, maxZoom: 1.0)!.Value;
        var button = VisualizerViewport.ComputeFit(tiny, canvas, new int?[] { null })!.Value;

        Assert.Equal(1.0, open.Zoom);
        Assert.Equal(VisualizerViewport.MaxFitZoom, button.Zoom);
    }
}
