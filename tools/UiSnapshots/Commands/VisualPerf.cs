using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>
/// <c>perf --visuals</c>: what big visuals cost. The display call runs on a worker thread, as a script's does; the view
/// then builds the drawing in the background, so the UI thread only puts the control in place and draws. The budgets:
/// the call within 50 ms, UI work before the first frame within 16 ms, and memory flat while stepping through a long
/// visualizer. A visual its program updates hundreds of times a second redraws at most 30 times a second, and ends on
/// the latest update.
/// </summary>
internal static class VisualPerf
{
    private const double CallBudget = 50;
    private const double UiBudget = 16;

    // Its display call compares each of 500 whole 30 × 30 grids with the one before; agreed on 2026-09-30 as fine at this.
    private const double LongGridCallBudget = 160;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine($"Visuals (budgets: the display call within {CallBudget:F0} ms off the UI thread, {LongGridCallBudget:F0} ms for the 500-step grid; UI work before the first frame within {UiBudget:F0} ms)");
        if (IsDebugBuild())
        {
            Console.WriteLine("  (a Debug build: the budgets are for Release, which runs this several times faster; build with -c Release to check them)");
        }

        var values = Enumerable.Range(0, 100_000).Select(i => Math.Sin(i / 500.0) * 100 + i * 0.001).ToArray();
        Measure("a chart of 100,000 values", CallBudget, () => () => Display.Chart(values, title: "100,000 values"));

        // The tracker's steps are the script's own work; the display call is what turns them into a spec.
        Measure("a 30 × 30 grid visualizer of 500 steps", LongGridCallBudget, () =>
        {
            var tracker = MatrixTracker.CreateEmpty(30, 30, "500 steps");
            for (int i = 0; i < 500; i++) tracker.Visit(i / 30, i % 30, $"step {i}");
            return () => Display.Visualizer(tracker);
        }, stepThrough: true);

        LiveUpdates();
    }

    // A program updating a chart every 3 ms for about a second: how often the view redraws, and whether its last drawing
    // is the last update.
    private static void LiveUpdates()
    {
        const int updates = 300;
        static ChartSpec Chart(int i) => new() { Title = $"Update {i}", Series = { new ChartSeriesSpec { Y = [i, i + 1, i % 7] } } };

        var output = VisualOutput.Create(Chart(0));
        var view = new VisualOutputView { Output = output };
        var window = new Window { Width = 800, Height = 500, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The drawing before any update isn't a redraw.
        var drawn = new HashSet<InteractiveChartControl>(ReferenceEqualityComparer.Instance);
        if (view.GetVisualDescendants().OfType<InteractiveChartControl>().FirstOrDefault() is { } before) drawn.Add(before);
        var redrawnAt = TimeSpan.Zero;
        var clock = Stopwatch.StartNew();
        var program = Task.Run(() =>
        {
            for (int i = 1; i <= updates; i++)
            {
                output.Replace(Chart(i));
                Thread.Sleep(3);
            }
        });

        // The UI thread keeps going as it would, and afterwards long enough for a redraw that was waiting.
        var settled = Stopwatch.StartNew();
        while (!program.IsCompleted || settled.Elapsed < TimeSpan.FromMilliseconds(200))
        {
            if (!program.IsCompleted) settled.Restart();
            Dispatcher.UIThread.Post(() => { }, DispatcherPriority.Background); // a due timer fires when a job runs (no message loop here)
            Dispatcher.UIThread.RunJobs();
            if (view.GetVisualDescendants().OfType<InteractiveChartControl>().FirstOrDefault() is { } chart && drawn.Add(chart)) redrawnAt = clock.Elapsed;
            Thread.Sleep(1);
        }

        var seconds = clock.Elapsed.TotalSeconds - settled.Elapsed.TotalSeconds;
        var last = view.GetVisualDescendants().OfType<InteractiveChartControl>().Single().Options?.Title;
        var redraws = drawn.Count - 1;
        var perSecond = redraws / redrawnAt.TotalSeconds;
        Console.WriteLine($"  a chart updated {updates} times in {seconds:F1} s: redrawn {redraws} times ({perSecond:F0} a second{(perSecond <= 31 ? ", within 30" : ", OVER 30")}), " +
                          $"ending on {(last == $"Update {updates}" ? "the last update" : $"\"{last}\", NOT the last update")}");
        window.Close();
    }

    private static void Measure(string name, double callBudget, Func<Action> prepare, bool stepThrough = false)
    {
        // The first call also loads what every later one reuses (serializers, the JIT), so it is shown on its own.
        var first = Call(prepare());
        var calls = Enumerable.Range(0, 3).Select(_ => Call(prepare())).ToList();
        var output = calls[^1].Output ?? throw new InvalidOperationException($"{name}: nothing was displayed");
        double call = calls.Select(c => c.Milliseconds).Order().ElementAt(1);

        var host = new Border();
        var window = new Window { Width = 1100, Height = 800, Content = new ScrollViewer { Content = host } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The first view of a kind also loads its XAML, once per run; the second is what every later one costs.
        var cold = Show(window, host, output, name);
        var view = new VisualOutputView { Output = output };
        var warm = Show(window, host, output, name, view);

        Console.WriteLine($"  {name}:");
        Console.WriteLine($"    display call {call:F1} ms{Verdict(call, callBudget)} (the first one {first.Milliseconds:F0} ms)");
        Console.WriteLine($"    drawing built in the background in {warm.Background:F0} ms; UI work {warm.Ui:F1} ms{Verdict(warm.Ui, UiBudget)} " +
                          $"(the first view {cold.Ui:F0} ms), then the first frame {warm.Frame:F0} ms; garbage collection paused every thread for {warm.GcPauses:F0} ms of it");

        if (stepThrough) StepThrough(window, view);
        window.Close();
    }

    // Every step drawn in turn, twice: how long a step takes, and whether memory grows with the steps looked at. The first
    // pass also fills what is meant to be kept (caches, keyframes), so flat means the second pass adds nothing.
    private static void StepThrough(Window window, VisualOutputView view)
    {
        var sequence = view.GetVisualDescendants().OfType<InteractiveVisualizerControl>().Single().Options?.Sequence
                       ?? throw new InvalidOperationException("The visualizer has no steps.");
        var memory = new List<long> { PerfSnapshots.SettledMemory() };
        var times = new List<double>();
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < sequence.TotalSteps; i++)
            {
                var clock = Stopwatch.StartNew();
                sequence.SeekStep(i);
                Dispatcher.UIThread.RunJobs();
                using (window.CaptureRenderedFrame()) { }
                times.Add(clock.Elapsed.TotalMilliseconds);
            }

            memory.Add(PerfSnapshots.SettledMemory());
        }

        var sorted = times.Order().ToList();
        double MB(int k) => (memory[k + 1] - memory[k]) / 1048576.0;
        Console.WriteLine($"    stepping through all {sequence.TotalSteps} steps: {sorted[sorted.Count / 2]:F1} ms a step (slowest {sorted[^1]:F0} ms); " +
                          $"memory {MB(0):+0.0;-0.0} MB the first time, {MB(1):+0.0;-0.0} MB the second{(MB(1) <= 2 ? " (flat)" : " (GROWING)")}");
    }

    // One view put on screen: the UI thread's work (attaching, and once the drawing is ready putting the control in
    // place), the background build meanwhile, and the first frame.
    private static (double Ui, double Background, double Frame, double GcPauses) Show(Window window, Border host, VisualOutput output, string name, VisualOutputView? view = null)
    {
        view ??= new VisualOutputView { Output = output };
        var pausedBefore = GC.GetTotalPauseDuration();
        var clock = Stopwatch.StartNew();
        host.Child = view;
        Dispatcher.UIThread.RunJobs();
        double ui = clock.Elapsed.TotalMilliseconds;

        var background = Stopwatch.StartNew();
        while (view.IsPreparing)
        {
            if (background.Elapsed > TimeSpan.FromMinutes(1)) throw new TimeoutException($"{name}: the drawing was never ready");
            var job = Stopwatch.StartNew();
            Dispatcher.UIThread.RunJobs();
            ui += job.Elapsed.TotalMilliseconds;
            Thread.Sleep(1);
        }

        double prepared = background.Elapsed.TotalMilliseconds;
        var placed = Stopwatch.StartNew();
        Dispatcher.UIThread.RunJobs();
        ui += placed.Elapsed.TotalMilliseconds;
        var frame = Stopwatch.StartNew();
        using (window.CaptureRenderedFrame()) { }
        return (ui, prepared, frame.Elapsed.TotalMilliseconds, (GC.GetTotalPauseDuration() - pausedBefore).TotalMilliseconds);
    }

    private static bool IsDebugBuild() =>
        typeof(VisualOutputView).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false)
            .OfType<DebuggableAttribute>().Any(a => a.IsJITOptimizerDisabled);

    private static (double Milliseconds, VisualOutput? Output) Call(Action display)
    {
        VisualOutput? output = null;
        double milliseconds = 0;
        Snapshot.Wait(Task.Run(() =>
        {
            using var scope = InteractiveDisplayContext.EnterScope(o => output ??= o.Visual);
            var clock = Stopwatch.StartNew();
            display();
            milliseconds = clock.Elapsed.TotalMilliseconds;
        }));
        return (milliseconds, output);
    }

    private static string Verdict(double measured, double budget) => measured <= budget ? " (within budget)" : $" (OVER the {budget:F0} ms budget)";
}
