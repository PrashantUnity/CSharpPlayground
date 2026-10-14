using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace FrySharp.Core.Tests;

public class ProcessLauncherTests
{
    [Fact]
    public void AProgramsOutput_IsDelivered_EvenWhenEveryThreadPoolThreadIsBusy()
    {
        if (OperatingSystem.IsWindows()) return; // uses /bin/sh

        // A test run is full of blocked pool threads (tests that wait on tasks): the program's output must not depend on a pool
        // thread being free to read it within the couple of seconds the launcher allows after the program ends.
        ThreadPool.GetMinThreads(out var workers, out _);
        using var release = new ManualResetEventSlim();
        var blockers = Enumerable.Range(0, workers * 3).Select(_ => Task.Run(() => release.Wait())).ToArray();
        try
        {
            var output = new ConcurrentQueue<string>();
            var started = new ProcessLauncher().Start(
                new ProcessStartSpec { FileName = "/bin/sh", Arguments = ["-c", "sleep 1; echo hello-from-a-program"], WorkingDirectory = Path.GetTempPath() },
                output.Enqueue,
                output.Enqueue);

            // (waited for without a pool thread: the point of the test is that none is free)
            Assert.True(SpinWait.SpinUntil(() => started.Completion.IsCompleted, TimeSpan.FromSeconds(90)), "the program did not complete");
            Assert.True(started.Completion.IsCompletedSuccessfully);
            Assert.Contains("hello-from-a-program", string.Concat(output));
        }
        finally
        {
            release.Set();
            SpinWait.SpinUntil(() => blockers.All(b => b.IsCompleted), TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public async Task ACommandsOutput_IsReturned_EvenWhenEveryThreadPoolThreadIsBusy()
    {
        if (OperatingSystem.IsWindows()) return; // uses /bin/sh

        // HostEnvironment.RunAsync runs a compiler or a probe and gives its output a second after the program ends, so its reads must not
        // wait for a free pool thread (a C++ compile whose messages came back empty, with no diagnostics, failed a full test run once).
        // This guards that the output, the error output and the exit code all come back under pool pressure; the race itself depends on
        // how fast the pool recovers, so it is not reproduced on demand.
        ThreadPool.GetMinThreads(out var workers, out _);
        using var release = new ManualResetEventSlim();
        var blockers = Enumerable.Range(0, workers * 3).Select(_ => Task.Run(() => release.Wait())).ToArray();
        try
        {
            var run = Task.Factory.StartNew(
                () => new HostEnvironment().RunAsync("/bin/sh", ["-c", "sleep 1; echo hello-out; echo hello-err 1>&2; exit 3"], TimeSpan.FromSeconds(60)).GetAwaiter().GetResult(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Assert.True(SpinWait.SpinUntil(() => run.IsCompleted, TimeSpan.FromSeconds(90)), "the command did not complete");
            var result = await run; // (already complete: no thread is needed to resume)
            Assert.Equal(3, result.ExitCode);
            Assert.Contains("hello-out", result.StandardOutput);
            Assert.Contains("hello-err", result.StandardError);
        }
        finally
        {
            release.Set();
            SpinWait.SpinUntil(() => blockers.All(b => b.IsCompleted), TimeSpan.FromSeconds(30));
        }
    }
}
