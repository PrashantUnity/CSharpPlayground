using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// A C# visual can change after it is shown, and tell its code what the user does to it: callbacks run when the kernel is
/// free (never beside a running cell), or inside a cell that asks, and print into the cell that asked for them.
/// </summary>
public class VisualEventsTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private sealed class Cell
    {
        public readonly ConcurrentQueue<string> Console = new();
        public readonly ConcurrentQueue<RichCellOutput> Outputs = new();
        public string Text => string.Concat(Console);
        public VisualOutput Visual => Outputs.Select(o => o.Visual).First(v => v != null)!;
    }

    // Off the caller's thread, as the notebook and Code Studio run a cell (the code runs inline until its first await).
    private static Task<KernelExecutionResult> Run(NotebookExecutionKernel kernel, string code, Cell cell, CancellationToken ct = default) =>
        Task.Run(() => kernel.ExecuteCellAsync(code, cell.Console.Enqueue, cell.Outputs.Enqueue, ct));

    // The visual is shown before the code on the next line listens to it.
    private static Task Listening(Cell cell) =>
        WaitUntil(() => cell.Outputs.Any(o => o.Visual != null) && cell.Visual.HasSubscriber(VisualEventKinds.Click));

    private static async Task WaitUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > Patience) throw new TimeoutException("It never happened.");
            await Task.Delay(10);
        }
    }

    private static VisualEvent ClickOn(int index) => VisualEvent.Click(new VisualEventTarget { Series = 0, Index = index });

    [Fact]
    public async Task AClickCallback_RunsWhenTheKernelIsFree_AndPrintsInTheCellThatAskedForIt()
    {
        var kernel = new NotebookExecutionKernel();
        var chartCell = new Cell();
        var laterCell = new Cell();
        await Run(kernel, """
            var chart = Display.LineChart(new[] { 1, 2, 3 }, "Click me");
            chart.OnClick(e => Console.WriteLine($"clicked {e.Target!.Index}"));
            """, chartCell);
        await Run(kernel, "Console.WriteLine(\"later\");", laterCell);

        chartCell.Visual.Raise(ClickOn(1));

        await WaitUntil(() => chartCell.Text.Contains("clicked 1"));
        Assert.DoesNotContain("clicked", laterCell.Text);
    }

    [Fact]
    public async Task ACallback_WaitsForTheRunningCellToEnd()
    {
        var kernel = new NotebookExecutionKernel();
        var chartCell = new Cell();
        await Run(kernel, "var order = new System.Collections.Concurrent.ConcurrentQueue<string>(); Display.Chart(new[] { 1, 2 }).OnClick(_ => order.Enqueue(\"callback\"));", chartCell);

        var busyCell = new Cell();
        var busy = Run(kernel, "Console.Write(\"started;\"); System.Threading.Thread.Sleep(400); order.Enqueue(\"cell\");", busyCell);
        await WaitUntil(() => busyCell.Text.Contains("started")); // the cell is running and holding the lock now
        chartCell.Visual.Raise(ClickOn(0));
        await busy;

        var seen = string.Empty;
        await WaitUntilAsync(async () =>
        {
            var check = new Cell();
            await Run(kernel, "Console.Write(string.Join(\",\", order));", check);
            seen = check.Text;
            return seen.Contains("callback");
        });
        Assert.Equal("cell,callback", seen);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!await condition())
        {
            if (clock.Elapsed > Patience) throw new TimeoutException("It never happened.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task ProcessEvents_RunsTheCallbacks_InsideTheCell()
    {
        var kernel = new NotebookExecutionKernel();
        var cell = new Cell();
        var running = Run(kernel, """
            var clicks = 0;
            Display.BarChart(new[] { 3, 1, 2 }, "Bars").OnClick(e => clicks++);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clicks == 0 && clock.Elapsed < TimeSpan.FromSeconds(15)) { Display.ProcessEvents(); System.Threading.Thread.Sleep(10); }
            Console.WriteLine($"clicks: {clicks}");
            """, cell);

        await Listening(cell);
        cell.Visual.Raise(ClickOn(2));
        var result = await running.WaitAsync(Patience);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("clicks: 1", cell.Text);
    }

    [Fact]
    public async Task Wait_RunsCallbacksUntilStop()
    {
        var kernel = new NotebookExecutionKernel();
        var cell = new Cell();
        using var stop = new CancellationTokenSource();
        var waiting = Run(kernel, """
            Display.Chart(new[] { 1, 2, 3 }).OnClick(e => Console.WriteLine($"clicked {e.Target!.Index}"));
            Display.Wait();
            """, cell, stop.Token);

        try
        {
            await Listening(cell);
            cell.Visual.Raise(ClickOn(2));
            await WaitUntil(() => cell.Text.Contains("clicked 2"));
        }
        finally
        {
            stop.Cancel(); // Stop, even when the test fails: Wait would otherwise wait for ever
        }

        var result = await waiting.WaitAsync(Patience);

        Assert.True(result.WasCancelled || result.Success);
    }

    [Fact]
    public async Task AFailingCallback_SaysSoInItsCell()
    {
        var kernel = new NotebookExecutionKernel();
        var cell = new Cell();
        await Run(kernel, "Display.Chart(new[] { 1, 2 }).OnClick(_ => throw new InvalidOperationException(\"no\"));", cell);

        cell.Visual.Raise(ClickOn(0));

        await WaitUntil(() => cell.Text.Contains("click callback failed") && cell.Text.Contains("no"));
    }

    [Fact]
    public async Task AReset_DropsTheCallbacks()
    {
        var kernel = new NotebookExecutionKernel();
        var cell = new Cell();
        await Run(kernel, "Display.Chart(new[] { 1, 2 }).OnClick(_ => Console.WriteLine(\"still here\"));", cell);

        kernel.HardReset();
        cell.Visual.Raise(ClickOn(0));
        await Task.Delay(300);

        Assert.DoesNotContain("still here", cell.Text);
    }

    // A handle redraws its visual where it is; a change that can't be drawn leaves it as it was.
    [Fact]
    public async Task Update_RedrawsInPlace_AndABadChangeLeavesItAsItWas()
    {
        var kernel = new NotebookExecutionKernel();
        var cell = new Cell();
        await Run(kernel, "var chart = Display.LineChart(new[] { 1, 2, 3 }, \"Before\");", cell);

        var update = await Run(kernel, "chart.Update(s => { s.Title = \"After\"; s.Series[0].Y.Add(4); });", new Cell());
        var bad = new Cell();
        var broken = await Run(kernel, "chart.Update(s => s.Series[0].X = new() { 1 });", bad);

        Assert.True(update.Success, update.ErrorMessage);
        var spec = Assert.IsType<ChartSpec>(cell.Visual.Spec);
        Assert.Equal(("After", 4), (spec.Title, spec.Series[0].Y.Count));
        Assert.False(broken.Success);
        Assert.Contains("$.series[0].x", bad.Text);
        Assert.Null(spec.Series[0].X);
    }

    // What was clicked, as the spec names it.
    [Fact]
    public void AClickedValue_IsNamedBySeriesIndexAndItsId()
    {
        var spec = new ChartSpec { Series = { new ChartSeriesSpec { Y = [1, 2, 3], Ids = ["a", "b", "c"], Labels = ["x", "y", "z"] } } };
        var drawn = ChartRenderModelBuilder.Build(spec);
        var point = drawn.Series[0].Points[1];

        var target = VisualEventTargets.Of(new ChartHitTestResult(point, drawn.Series[0], new Point()), drawn, spec);

        Assert.Equal((0, 1, "b", "y", 2.0), (target.Series, target.Index, target.Id, target.Label, target.Y));
    }

    [Fact]
    public void AClickedCellItemOrNode_IsNamedAsTheSpecNamesIt()
    {
        var grid = new VisualizerOptions { Kind = VisualizerKind.Matrix };
        var array = new VisualizerOptions { Kind = VisualizerKind.ArrayPointers };
        var list = new VisualizerOptions { Kind = VisualizerKind.LinkedList, LinkedListData = new LinkedListData { Nodes = { new LinkedListNodeData(0, "1", 1) { Id = "head" }, new LinkedListNodeData(1, "2", null) } } };

        Assert.Equal((2, 3), VisualEventTargets.Of(new VisualizerHitTestResult { Row = 2, Col = 3 }, grid)!.Cell);
        Assert.Equal(4, VisualEventTargets.Of(new VisualizerHitTestResult { Row = 0, Col = 4 }, array)!.Item);
        Assert.Equal("head", VisualEventTargets.Of(new VisualizerHitTestResult { NodeId = "node_0" }, list)!.Node);
        Assert.Equal("n1", VisualEventTargets.Of(new VisualizerHitTestResult { NodeId = "node_1" }, list)!.Node);
    }

    [Fact]
    public void AnEvent_IsTheJsonEveryLanguageReads()
    {
        var click = VisualEvent.Click(new VisualEventTarget { Cell = (1, 2) }, ["ctrl"]).ToJson();
        var select = VisualEvent.Select([new VisualEventTarget { Edge = ("a", "b") }, new VisualEventTarget { Node = "c" }]).ToJson();

        Assert.Equal("""{"event":"click","target":{"cell":[1,2]},"modifiers":["ctrl"]}""", click);
        Assert.Equal("""{"event":"select","targets":[{"edge":["a","b"]},{"node":"c"}]}""", select);
        Assert.Equal("""{"event":"step","index":4}""", VisualEvent.Step(4).ToJson());
    }
}
