using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// A run's console output shows in the Terminal exactly once and in order, whether its queued live writes reach the UI
/// thread before or after the run's completion code. (Avalonia resumes the awaiting run at Normal priority, ahead of the
/// writes queued at Default, so in the app the completion usually comes first.)
/// </summary>
public class RunConsoleRelayTests : IDisposable
{
    private readonly string _storageDir = Path.Combine(Path.GetTempPath(), "FryPDF_RunConsoleTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageDir)) Directory.Delete(_storageDir, recursive: true);
        }
        catch { }
    }

    private static int Occurrences(string text, string piece) =>
        (text.Length - text.Replace(piece, string.Empty, StringComparison.Ordinal).Length) / piece.Length;

    // ── The relay on its own: the test decides when queued deliveries run ──────────────

    private static (RunConsoleRelay Relay, StringBuilder Shown, List<Action> Queued) Relay()
    {
        var shown = new StringBuilder();
        var queued = new List<Action>();
        return (new RunConsoleRelay(text => shown.Append(text), queued.Add), shown, queued);
    }

    [Fact]
    public void RunEndingBeforeItsQueuedWrites_ShowsTheOutputOnce()
    {
        var (relay, shown, queued) = Relay();
        relay.Write("1,2,3");
        relay.Write("\n");

        relay.Complete("1,2,3\n");
        Assert.Equal("1,2,3\n", shown.ToString());

        queued.ForEach(deliver => deliver());
        Assert.Equal("1,2,3\n", shown.ToString());
    }

    [Fact]
    public void QueuedWritesArrivingFirst_ShowTheOutputOnce()
    {
        var (relay, shown, queued) = Relay();
        relay.Write("1,2,3\n");

        queued.ForEach(deliver => deliver());
        relay.Complete("1,2,3\n");

        Assert.Equal("1,2,3\n", shown.ToString());
    }

    [Fact]
    public void QueuedDeliveriesInAnyOrder_KeepTheOrderTheScriptWrote()
    {
        var (relay, shown, queued) = Relay();
        relay.Write("a");
        relay.Write("b");
        relay.Write("c");

        queued[2]();
        queued[0]();
        queued[1]();

        Assert.Equal("abc", shown.ToString());
    }

    [Fact]
    public void FinalOutputThatNeverArrivedLive_IsShownOnce()
    {
        var (relay, shown, _) = Relay();

        relay.Complete("only in the final output\n");
        relay.Complete("only in the final output\n");

        Assert.Equal("only in the final output\n", shown.ToString());
    }

    [Fact]
    public void LiveOnlyLines_StayAndTheFinalOutputAddsNothing()
    {
        // The kernel sends NuGet messages and returned values live only; its final output is just what the script wrote.
        var (relay, shown, _) = Relay();
        relay.Write("Restored package Humanizer\n");
        relay.Write("hello\n");
        relay.Write("42\n");

        relay.Complete("hello\n");

        Assert.Equal("Restored package Humanizer\nhello\n42\n", shown.ToString());
    }

    [Fact]
    public void WritesAfterTheRunEnded_StillShow()
    {
        // A task the script started can keep printing after the script itself returned.
        var (relay, shown, queued) = Relay();
        relay.Write("main\n");
        relay.Complete("main\n");

        relay.Write("late\n");
        queued.ForEach(deliver => deliver());

        Assert.Equal("main\nlate\n", shown.ToString());
    }

    [Fact]
    public async Task ConcurrentWritesAndDeliveries_ShowEveryPieceExactlyOnceInEachWritersOrder()
    {
        var shown = new StringBuilder();
        var queued = new ConcurrentQueue<Action>();
        var relay = new RunConsoleRelay(text => shown.Append(text), queued.Enqueue);
        using var writing = new CancellationTokenSource();

        // Deliveries run on their own thread while four writers print.
        var deliverer = Task.Run(() =>
        {
            while (!writing.IsCancellationRequested || !queued.IsEmpty)
            {
                if (queued.TryDequeue(out var deliver)) deliver();
                else Thread.Yield();
            }
        });
        Parallel.For(0, 4, writer =>
        {
            for (int line = 0; line < 500; line++) relay.Write($"[{writer}:{line}]");
        });
        relay.Complete();
        writing.Cancel();
        await deliverer;

        string output = shown.ToString();
        for (int writer = 0; writer < 4; writer++)
        {
            int previous = -1;
            for (int line = 0; line < 500; line++)
            {
                string piece = $"[{writer}:{line}]";
                Assert.Equal(1, Occurrences(output, piece));
                int at = output.IndexOf(piece, StringComparison.Ordinal);
                Assert.True(at > previous, $"{piece} came out of order");
                previous = at;
            }
        }
    }

    // ── Code Studio: every live delivery is held back until after the run, the order that used to duplicate ──

    private (CSharpCodeStudioViewModel Studio, ConcurrentQueue<Action> Held) StudioHoldingLiveOutput(string code, int languageMode = 0)
    {
        var held = new ConcurrentQueue<Action>();
        var script = new ScriptDocumentItem { Title = "Relay test", Code = code };
        var studio = new CSharpCodeStudioViewModel(
            script,
            new LocalScriptStorageService(_storageDir),
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            postToUiThread: held.Enqueue);
        studio.SelectedLanguageModeIndex = languageMode;
        return (studio, held);
    }

    private static void Deliver(ConcurrentQueue<Action> held)
    {
        while (held.TryDequeue(out var deliver)) deliver();
    }

    [Fact]
    public async Task ScriptOutput_ShowsOnce_WhenTheRunFinishesBeforeItsLiveOutputIsDelivered()
    {
        var (studio, held) = StudioHoldingLiveOutput("var xs = new[] { 3, 1, 2 }; Array.Sort(xs); Console.WriteLine(string.Join(\",\", xs));");

        await studio.RunCodeCommand.ExecuteAsync(null);
        Assert.NotEmpty(held); // the live writes were still waiting when the run finished
        var tab = studio.OpenTabs.Single(t => t.Id == studio.Script.Id);
        Assert.Equal(1, Occurrences(studio.ConsoleOutput, "1,2,3"));
        Assert.Equal(1, Occurrences(tab.ConsoleOutput, "1,2,3"));

        Deliver(held);
        Assert.Equal(1, Occurrences(studio.ConsoleOutput, "1,2,3"));
        Assert.Equal(1, Occurrences(tab.ConsoleOutput, "1,2,3"));
    }

    [Fact]
    public async Task ProgramOutput_ShowsOnce_AndBeforeTheFinishedLine()
    {
        const string program = """
            using System;
            public class Program
            {
                public static void Main() => Console.WriteLine("from Main");
            }
            """;
        var (studio, held) = StudioHoldingLiveOutput(program, languageMode: 1);

        await studio.RunCodeCommand.ExecuteAsync(null);
        Deliver(held);

        Assert.Equal(1, Occurrences(studio.ConsoleOutput, "from Main"));
        Assert.True(studio.ConsoleOutput.IndexOf("from Main", StringComparison.Ordinal) <
                    studio.ConsoleOutput.IndexOf("Execution finished", StringComparison.Ordinal),
                    studio.ConsoleOutput);
    }

    [Fact]
    public async Task TestCaseVerdicts_SeeTheWholeOutput_EvenBeforeLiveDeliveriesArrive()
    {
        var (studio, _) = StudioHoldingLiveOutput("Console.WriteLine(\"✅ Case 1 → 42\");");
        var testCase = new TestCaseItem { Name = "Case 1", ExpectedOutput = "42" };

        await studio.RunTestCaseCommand.ExecuteAsync(testCase);

        Assert.True(testCase.Passed);
        Assert.Equal("✅ Case 1 → 42", testCase.ActualOutput);
    }
}
