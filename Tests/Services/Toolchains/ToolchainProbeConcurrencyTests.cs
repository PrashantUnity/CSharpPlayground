using System.Diagnostics;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// A version probe starts a process, and starting one can take a while (forking a big process waits for every other
/// start). Callers that ask for the same probe meanwhile must wait for it without holding their thread: a thread held
/// per caller starved the thread pool, and with it the whole studio (and the test run, which then took minutes longer).
/// </summary>
public sealed class ToolchainProbeConcurrencyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("probe-concurrency-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task AskingForAToolchain_WhileItsProbeIsStarting_DoesNotHoldTheCallersThread()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddFile("/opt/homebrew/bin/go");
        host.OnCommand = (_, args) =>
        {
            if (args.Count == 0 || args[0] != "version") return null;
            Thread.Sleep(600); // a process start that takes its time, on the thread that asked for it
            return new CommandResult(0, "go version go1.22.4 darwin/arm64\n", string.Empty, false);
        };
        var provider = new GoToolchainProvider(host, new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_dir, "tc.json")), _dir);

        var first = Task.Run(() => provider.ListAsync(new ToolchainQuery()));
        await Task.Delay(100);
        var clock = Stopwatch.StartNew();
        var second = provider.ListAsync(new ToolchainQuery());
        var held = clock.Elapsed;

        Assert.True(held < TimeSpan.FromMilliseconds(250), $"the caller's thread was held {held.TotalMilliseconds:0} ms waiting for another caller's probe");
        foreach (var list in await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10)))
        {
            Assert.Contains(list, t => t.Version == new Version(1, 22, 4));
        }
    }
}
