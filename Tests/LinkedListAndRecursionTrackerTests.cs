using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class LinkedListAndRecursionTrackerTests
{
    private sealed class ListNode
    {
        public int val;
        public ListNode? next;
        public ListNode(int v, ListNode? n = null) { val = v; next = n; }
    }

    private static LinkedListData ListAt(LinkedListTracker tracker, int step) =>
        Assert.IsType<LinkedListData>(tracker.Sequence.Steps[step < 0 ? tracker.Sequence.TotalSteps + step : step].Snapshot);

    private static IEnumerable<TreeNodeData> All(TreeNodeData node) => new[] { node }.Concat(node.Children.SelectMany(All));

    [Fact]
    public void LinkedListTracker_Reversal_KeepsNodesInPlaceAndFlipsTheArrows()
    {
        var head = new ListNode(1, new ListNode(2, new ListNode(3)));
        var tracker = LinkedListTracker.Create(head, "reverse");

        ListNode? prev = null, curr = head;
        while (curr != null)
        {
            var next = curr.next;
            curr.next = prev;
            prev = curr;
            curr = next;
            tracker.Step("advance", new { prev, curr });
        }

        var start = ListAt(tracker, 0);
        Assert.Equal(new int?[] { 1, 2, null }, start.Nodes.Select(n => n.NextIndex));
        Assert.Equal(("head", 0), (start.Pointers.Single().Name, start.Pointers.Single().Index));

        var done = ListAt(tracker, -1);
        Assert.Equal(new[] { "1", "2", "3" }, done.Nodes.Select(n => n.DisplayValue));
        Assert.Equal(new int?[] { null, 0, 1 }, done.Nodes.Select(n => n.NextIndex));
        Assert.Equal(2, done.Pointers.Single(p => p.Name == "prev").Index);
        Assert.Equal(-1, done.Pointers.Single(p => p.Name == "curr").Index);
    }

    [Fact]
    public void LinkedListTracker_PicksUpNewNodesFromLinksAndFromVariables()
    {
        var a = new ListNode(1, new ListNode(3));
        var b = new ListNode(2, new ListNode(4));
        var tracker = LinkedListTracker.Create(a, "merge");
        tracker.Step("second list", new { a, b, count = 0 });

        var both = ListAt(tracker, -1);
        Assert.Equal(new[] { "1", "3", "2", "4" }, both.Nodes.Select(n => n.DisplayValue));
        Assert.Equal(new[] { 2 }, both.ChainStarts);
        Assert.Equal("0", tracker.Sequence.Steps[^1].AuxiliaryInfo["count"]);

        a.next = new ListNode(5, a.next);
        tracker.Step("insert 5 after 1");

        var inserted = ListAt(tracker, -1);
        Assert.Equal("5", inserted.Nodes[4].DisplayValue);
        Assert.Equal(4, inserted.Nodes[0].NextIndex);
        Assert.Equal(1, inserted.Nodes[4].NextIndex);
        Assert.Equal(new[] { 2 }, inserted.ChainStarts);
    }

    [Fact]
    public void LinkedListTracker_SurvivesCyclesAndCapsLongLists()
    {
        var a = new ListNode(1);
        a.next = new ListNode(2, a);
        Assert.Equal(new int?[] { 1, 0 }, ListAt(LinkedListTracker.Create(a), 0).Nodes.Select(n => n.NextIndex));

        ListNode? longHead = null;
        for (int i = 0; i < 60; i++) longHead = new ListNode(i, longHead);
        var capped = ListAt(LinkedListTracker.Create(longHead), 0);
        Assert.Equal(LinkedListTracker.MaxNodes, capped.Nodes.Count);
        Assert.True(capped.Nodes[^1].ContinuesBeyondView);
    }

    [Fact]
    public void LinkedListTracker_PointerBagWithANextProperty_IsNotMistakenForANode()
    {
        var head = new ListNode(1, new ListNode(2));
        var tracker = LinkedListTracker.Create(head);
        var next = head.next;

        tracker.Step("save next", new { curr = head, next });

        var data = ListAt(tracker, -1);
        Assert.Equal(2, data.Nodes.Count);
        Assert.Equal(new[] { ("curr", 0), ("next", 1) }, data.Pointers.Select(p => (p.Name, p.Index)));
    }

    [Fact]
    public void LinkedListTracker_SingleNodeArgument_IsNamedAfterTheVariable()
    {
        var head = new ListNode(1, new ListNode(2));
        var tracker = LinkedListTracker.Create(head);
        var runner = head.next;

        tracker.Step("runner moved", runner);

        var pointer = ListAt(tracker, -1).Pointers.Single();
        Assert.Equal(("runner", 1), (pointer.Name, pointer.Index));
    }

    [Fact]
    public void StepChanges_LinkedListNodes_FlagsRewiredValueChangedAndNewNodes()
    {
        var before = new LinkedListData { Nodes = { new(0, "1", 1), new(1, "2", null) } };
        var after = new LinkedListData { Nodes = { new(0, "1", null), new(1, "2", 0), new(2, "3", null) } };

        Assert.Equal(new[] { 0, 1, 2 }, StepChanges.LinkedListNodes(before, after).OrderBy(i => i));
        Assert.Empty(StepChanges.LinkedListNodes(before, before.Clone()));
    }

    [Fact]
    public void CycleDetectionSteps_LabelSlowAndFastPointers()
    {
        var n1 = new ListNode(1);
        var n2 = new ListNode(2);
        var n3 = new ListNode(3);
        n1.next = n2;
        n2.next = n3;
        n3.next = n2;

        var sequence = LinkedListDataParser.GenerateCycleDetectionSteps(n1);

        var start = Assert.IsType<LinkedListData>(sequence.Steps[0].Snapshot);
        Assert.All(start.Pointers, p => Assert.Equal(0, p.Index));
        var meeting = Assert.IsType<LinkedListData>(sequence.Steps[^1].Snapshot);
        Assert.Equal(meeting.Pointers.Single(p => p.Name == "slow").Index, meeting.Pointers.Single(p => p.Name == "fast").Index);
    }

    [Fact]
    public void LinkedListRenderer_HitTest_AccountsForTheNullPointerBox()
    {
        var tracker = LinkedListTracker.Create(new ListNode(1, new ListNode(2)));
        tracker.Step("prev is null", new { prev = (ListNode?)null });
        tracker.Sequence.SeekStep(1);
        var renderer = new LinkedListRenderer();
        var bounds = new Rect(0, 0, 600, 300);

        // null box 155-199, node 0 at 225-287, node 1 at 313-375 (row centre y = 150)
        Assert.Null(renderer.HitTest(new Point(177, 150), bounds, tracker.Options));
        Assert.Equal("node_0", renderer.HitTest(new Point(256, 150), bounds, tracker.Options)?.NodeId);
        Assert.Equal("node_1", renderer.HitTest(new Point(344, 150), bounds, tracker.Options)?.NodeId);
    }

    [Fact]
    public void RecursionTracker_NaiveFibonacci_DrawsEveryCallAndTheCallStack()
    {
        var calls = RecursionTracker.Create("fib");
        long Fib(int n)
        {
            using var call = calls.Enter($"fib({n})");
            if (n < 2) return call.Return(n);
            return call.Return(Fib(n - 1) + Fib(n - 2));
        }

        Assert.Equal(3, Fib(4));
        Assert.Equal(9, calls.CallCount);

        var final = Assert.IsType<TreeNodeData>(calls.Sequence.Steps[^1].Snapshot);
        var top = final.Children.Single();
        Assert.Equal(("fib(4)", "= 3"), (top.DisplayValue, top.SubLabel));
        Assert.All(All(final).Skip(1), n => Assert.Equal(TreeNodeState.Visited, n.State));

        // When the first leaf runs, every caller on the way down is waiting on the stack
        var firstLeaf = calls.Sequence.Steps.First(s => s.Description == "Call fib(1)");
        Assert.Equal("4", firstLeaf.AuxiliaryInfo["Depth"]);
        var node = Assert.IsType<TreeNodeData>(firstLeaf.Snapshot);
        var states = new List<TreeNodeState>();
        while (true)
        {
            states.Add(node.State);
            if (node.Children.Count == 0) break;
            node = node.Children[0];
        }
        Assert.Equal(new[] { TreeNodeState.Candidate, TreeNodeState.Candidate, TreeNodeState.Candidate, TreeNodeState.Candidate, TreeNodeState.Current }, states);
    }

    [Fact]
    public void RecursionTracker_MemoHitsEndBranchesAndAreCounted()
    {
        var calls = RecursionTracker.Create();
        var memo = new Dictionary<int, long>();
        calls.Watch(memo);
        long Fib(int n)
        {
            using var call = calls.Enter($"fib({n})");
            if (memo.TryGetValue(n, out var cached)) return call.Memo(cached);
            if (n < 2) return call.Return(n);
            return call.Return(memo[n] = Fib(n - 1) + Fib(n - 2));
        }

        Assert.Equal(5, Fib(5));
        Assert.Equal(9, calls.CallCount);

        var last = calls.Sequence.Steps[^1];
        Assert.Equal("2", last.AuxiliaryInfo["Memo hits"]);
        Assert.Equal(2, All(Assert.IsType<TreeNodeData>(last.Snapshot)).Count(n => n.State == TreeNodeState.Matched));
        Assert.Equal("memo", last.Watches.Single().Name);
    }

    [Fact]
    public void RecursionTracker_StopsDrawingAtTheLimitButLetsTheRecursionFinish()
    {
        var calls = RecursionTracker.Create();
        long Fib(int n)
        {
            using var call = calls.Enter(n);
            return n < 2 ? call.Return(n) : call.Return(Fib(n - 1) + Fib(n - 2));
        }

        Assert.Equal(6765, Fib(20));
        Assert.Equal(RecursionTracker.MaxCalls, calls.CallCount);
        Assert.Contains("Stopped drawing", calls.Sequence.Steps[^1].Description);
    }

    [Fact]
    public void RecursionTracker_PrunedBranchesAndExceptionsCloseTheirCalls()
    {
        var calls = RecursionTracker.Create();
        void Search(int depth)
        {
            using var call = calls.Enter(depth);
            if (depth == 2)
            {
                call.Prune("too deep");
                return;
            }
            Search(depth + 1);
        }
        Search(0);
        Assert.Contains(All(Assert.IsType<TreeNodeData>(calls.Sequence.Steps[^1].Snapshot)), n => n.DisplayValue == "2" && n.State == TreeNodeState.Pruned);

        var failing = RecursionTracker.Create();
        void Boom(int depth)
        {
            using var call = failing.Enter(depth);
            if (depth == 3) throw new InvalidOperationException("boom");
            Boom(depth + 1);
        }
        Assert.Throws<InvalidOperationException>(() => Boom(0));
        var unwound = failing.Sequence.Steps[^1];
        Assert.All(All(Assert.IsType<TreeNodeData>(unwound.Snapshot)).Skip(1), n => Assert.Equal(TreeNodeState.Visited, n.State));
        Assert.Equal("0", unwound.AuxiliaryInfo["Depth"]);
    }

    [Fact]
    public void TreeLayout_NaryTrees_OnlyLeavesTakeHorizontalSpace()
    {
        var chain = new TreeNodeData("a", "a");
        var tail = chain;
        for (int i = 0; i < 4; i++)
        {
            var child = new TreeNodeData($"n{i}", $"n{i}") { Parent = tail };
            tail.Children.Add(child);
            tail = child;
        }

        var (width, _) = PdfEditorApp.Plugins.CSharpEditor.Visualizers.Layouts.TreeLayoutEngine.ComputeLayout(chain);
        Assert.Equal(200, width);
        Assert.Equal(chain.X, tail.X);

        var fan = new TreeNodeData("p", "p");
        foreach (var name in new[] { "x", "y", "z" }) fan.Children.Add(new TreeNodeData(name, name) { Parent = fan });
        PdfEditorApp.Plugins.CSharpEditor.Visualizers.Layouts.TreeLayoutEngine.ComputeLayout(fan);
        Assert.Equal(fan.Children[1].X, fan.X);
    }

    [Fact]
    public void TreeRenderer_DeepCurrentCall_IsScrolledIntoView()
    {
        var calls = RecursionTracker.Create();
        void Dive(int depth)
        {
            using var call = calls.Enter($"d{depth}");
            if (depth < 6) Dive(depth + 1);
        }
        Dive(1);
        calls.Sequence.SeekStep(calls.Sequence.Steps.FindIndex(s => s.Description == "Call d6"));

        // d6 sits 438px down the layout; the view slides it to 300 - (22 + 24) = 254 on a 300px canvas
        var hit = new TreeRenderer().HitTest(new Point(154, 254), new Rect(0, 0, 400, 300), calls.Options);
        Assert.Equal("Node [d6]", hit?.Title);
    }

    [Fact]
    public async Task Templates_ReverseListAndFibonacciTree_RunAndRecordSteps()
    {
        var reverse = await RunTemplateAsync("leetcode_206_reverse_linked_list");
        var done = Assert.IsType<LinkedListData>(reverse.Steps[^1].Snapshot);
        Assert.Equal(new int?[] { null, 0, 1, 2 }, done.Nodes.Select(n => n.NextIndex));
        Assert.Equal(3, done.Pointers.Single(p => p.Name == "head").Index);

        var fibonacci = await RunTemplateAsync("recursion_tree_fibonacci_memo");
        Assert.Equal("2", fibonacci.Steps[^1].AuxiliaryInfo["Memo hits"]);
    }

    private static async Task<VisualizerSequence> RunTemplateAsync(string templateId)
    {
        var template = CodeTemplateLibrary.GetTemplates().Single(t => t.Id == templateId);
        RichCellOutput? output = null;
        var result = await new NotebookExecutionKernel().ExecuteCellAsync(template.InitialCode, onRichOutput: r => output = r);
        Assert.True(result.Success, result.ErrorMessage);
        return output!.VisualizerOptions!.Sequence!;
    }
}
