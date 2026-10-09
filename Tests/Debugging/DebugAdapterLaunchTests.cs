using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.Debugging;

/// <summary>
/// How the Go (Delve) and C# (netcoredbg) providers start their debug adapters and drive them. The adapters here are stand-ins that
/// behave as each real one is documented to: Delve is a TCP server that refuses setBreakpoints and configurationDone until it
/// has been asked to launch; netcoredbg talks over its standard streams and answers launch at once.
/// </summary>
public class DebugAdapterLaunchTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_DapLaunch_" + Guid.NewGuid().ToString("N"));

    public DebugAdapterLaunchTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static DebugLaunchContext Context(string sourcePath, string code, Action<string>? live = null, params int[] breakpointLines) => new(
        ScriptId: "adapter-launch",
        SourceFilePath: sourcePath,
        SourceCode: code,
        Breakpoints: breakpointLines.Select(line => new BreakpointItem { LineNumber = line, IsEnabled = true }).ToList(),
        Toolchain: null,
        OnLiveOutput: live,
        CancellationToken: CancellationToken.None);

    // ---- Go: Delve ----

    // What `dlv dap` does: it answers initialize; setBreakpoints and configurationDone are errors until launch has been handled;
    // launch is followed by the "initialized" event and its own answer.
    private static Func<MockDapServer, string, int, JsonElement, Task> DelveLike(Action<JsonElement>? launchArguments = null)
    {
        var launched = false;
        return async (server, command, seq, root) =>
        {
            switch (command)
            {
                case "launch":
                    launched = true;
                    launchArguments?.Invoke(root.GetProperty("arguments").Clone());
                    await server.EventAsync("initialized");
                    await server.RespondAsync(seq, command);
                    break;
                case "setBreakpoints" or "configurationDone" when !launched:
                    await server.RespondAsync(seq, command, success: false, message: "No debug session started");
                    break;
                case "setBreakpoints":
                    await server.RespondAsync(seq, command, body: new { breakpoints = new[] { new { verified = true, line = 3 } } });
                    break;
                default:
                    await server.RespondAsync(seq, command);
                    break;
            }
        };
    }

    private (GoDebuggerProvider Provider, FakeProcessLauncher Launcher, string Source) GoProvider(
        Func<ProcessStartSpec, FakeProcess, Task> dlv)
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddGo("/opt/homebrew/bin/go", "1.22.4");
        host.AddFile("/opt/homebrew/bin/dlv");
        host.OnCommand = (file, args) => args.Count > 0 && args[0] == "version" && file.EndsWith("dlv", StringComparison.Ordinal)
            ? new CommandResult(0, "Delve Debugger\nVersion: 1.22.1\n", string.Empty, false)
            : args.Count > 0 && args[0] == "build" ? new CommandResult(0, string.Empty, string.Empty, false) : null;

        var launcher = new FakeProcessLauncher { Behavior = dlv };
        var toolchain = new GoToolchainProvider(host, launcher, new ToolchainSettingsStore(Path.Combine(_dir, "tc.json")), _dir);
        var source = Path.Combine(_dir, "main.go");
        File.WriteAllText(source, "package main\n\nfunc main() {\n\tprintln(\"hi\")\n}\n");
        return (new GoDebuggerProvider(toolchain, launcher, host), launcher, source);
    }

    private static int ListenPort(ProcessStartSpec spec) =>
        int.Parse(spec.Arguments.Single(a => a.StartsWith("--listen=127.0.0.1:", StringComparison.Ordinal)).Split(':')[^1]);

    [Fact]
    public async Task Delve_IsToldWhereToListen_AndDrivenInTheOrderItNeeds()
    {
        var launchArguments = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockDapServer? server = null;
        var (provider, launcher, source) = GoProvider(async (spec, process) =>
        {
            var port = ListenPort(spec);
            var (accepted, listener) = MockDapServer.ListenOnce(port, DelveLike(args => launchArguments.TrySetResult(args)));
            using (listener)
            {
                process.Write($"DAP server listening at: 127.0.0.1:{port}\n");
                server = await accepted;
                await using (server) await Task.Delay(Timeout.Infinite, process.KilledToken);
            }
        });
        var output = new List<string>();

        await using var session = await provider.LaunchAsync(Context(source, File.ReadAllText(source), output.Add, breakpointLines: 4)).WaitAsync(Patience);

        Assert.IsType<DapDebugSession>(session);
        var dlvStart = launcher.Started.Single(s => s.FileName.Replace('\\', '/') == "/opt/homebrew/bin/dlv");
        Assert.Equal("dap", dlvStart.Arguments[0]);
        Assert.Equal($"--listen=127.0.0.1:{ListenPort(dlvStart)}", dlvStart.Arguments[1]);
        Assert.Equal(["initialize", "launch", "setBreakpoints", "configurationDone"], server!.Requests);

        var launch = await launchArguments.Task.WaitAsync(Patience);
        Assert.Equal("exec", launch.GetProperty("mode").GetString());
        Assert.False(launch.GetProperty("stopOnEntry").GetBoolean());
        Assert.True(Path.IsPathRooted(launch.GetProperty("program").GetString()));
        Assert.Equal(_dir, launch.GetProperty("cwd").GetString());

        var breakpoints = server.RequestBodies.Single(r => r.GetProperty("command").GetString() == "setBreakpoints").GetProperty("arguments");
        Assert.Equal(source, breakpoints.GetProperty("source").GetProperty("path").GetString());
        Assert.DoesNotContain(output, text => text.Contains("DAP server listening", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Delve_TheBannerIsHidden_ButItsOtherOutputIsKept()
    {
        var shown = new List<string>();
        var filtered = GoDebuggerProvider.WithoutListeningBanner(shown.Add)!;

        filtered("DAP server listening at: 127.0.0.1:53211\n");
        filtered("could not launch process: fork/exec /x: permission denied\n");
        filtered("DAP server listening at: 127.0.0.1:5\r\nnext line\n");

        Assert.Equal(["could not launch process: fork/exec /x: permission denied\n", "next line\n"], shown);
        Assert.Null(GoDebuggerProvider.WithoutListeningBanner(null));
    }

    [Fact]
    public async Task Delve_ALaunchItRefuses_IsAnErrorAtOnce_AndItsProcessIsStopped()
    {
        FakeProcess? dlvProcess = null;
        var (provider, _, source) = GoProvider(async (spec, process) =>
        {
            dlvProcess = process;
            var (accepted, listener) = MockDapServer.ListenOnce(ListenPort(spec), async (server, command, seq, _) =>
            {
                if (command == "launch") await server.RespondAsync(seq, command, success: false, message: "can't find /x: no such file");
                else await server.RespondAsync(seq, command);
            });
            using (listener)
            {
                await using var server = await accepted;
                await Task.Delay(Timeout.Infinite, process.KilledToken);
            }
        });

        var ex = await Assert.ThrowsAsync<DapException>(() => provider.LaunchAsync(Context(source, File.ReadAllText(source))).WaitAsync(Patience));

        Assert.Contains("no such file", ex.Message);
        await WaitUntil(() => dlvProcess is { WasKilled: true });
    }

    [Fact]
    public async Task Delve_ThatExitsAtOnce_IsReportedWithItsExitCode_NotAsATimeout()
    {
        var (provider, _, source) = GoProvider((_, process) =>
        {
            process.WriteError("listen tcp 127.0.0.1:1: bind: address already in use\n");
            process.Exit(1);
            return Task.CompletedTask;
        });

        // Whether it goes before the debugger connects or after (another program may hold the port), it is the same failure.
        var ex = await Assert.ThrowsAsync<DebugAdapterExitedException>(() => provider.LaunchAsync(Context(source, File.ReadAllText(source))).WaitAsync(Patience));

        Assert.Equal(1, ex.ExitCode);
        Assert.Contains("exit code 1", ex.Message);
    }

    [Fact]
    public async Task Delve_WhosePortAnotherProgramHolds_IsReportedAsExited_NotAsADroppedConnection()
    {
        // The studio reaches whatever holds the port (here a program that never speaks DAP) before Delve gives up on it.
        TcpListener? other = null;
        TcpClient? reached = null;
        var (provider, _, source) = GoProvider(async (spec, process) =>
        {
            other = new TcpListener(IPAddress.Loopback, ListenPort(spec));
            other.Start();
            reached = await other.AcceptTcpClientAsync();
            process.WriteError("listen tcp 127.0.0.1: bind: address already in use\n");
            process.Exit(1);
        });

        try
        {
            var ex = await Assert.ThrowsAsync<DebugAdapterExitedException>(() => provider.LaunchAsync(Context(source, File.ReadAllText(source))).WaitAsync(Patience));

            Assert.Equal(1, ex.ExitCode);
        }
        finally
        {
            reached?.Dispose();
            other?.Stop();
        }
    }

    // ---- C#: netcoredbg ----

    // What netcoredbg does: "initialized" follows initialize; launch is answered at once; nothing depends on the order.
    private static Func<MockDapServer, string, int, JsonElement, Task> NetCoreDbgLike(bool refuseLaunch = false) => async (server, command, seq, _) =>
    {
        if (command == "launch" && refuseLaunch)
        {
            await server.RespondAsync(seq, command, success: false, message: "The program 'dotnet' could not be started.");
            return;
        }

        await server.RespondAsync(seq, command, body: command == "setBreakpoints" ? new { breakpoints = new[] { new { verified = true, line = 2 } } } : null);
        if (command == "initialize") await server.EventAsync("initialized");
    };

    private CSharpDebuggerProvider CSharpProvider(FakeProcessLauncher launcher)
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddFile("/opt/homebrew/bin/netcoredbg");
        host.AddFile("/usr/bin/netcoredbg");
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();
        return new CSharpDebuggerProvider(compiler, new ScriptDebuggerService(compiler, engine), engine, launcher, host, cacheDirectory: Path.Combine(_dir, "cs"));
    }

    private const string CSharpScript = "var x = 40;\nConsole.WriteLine(x + 2);\n";

    [Fact]
    public async Task NetCoreDbg_IsDrivenInTheOrderTheSpecificationGives_AndBreakpointsUseTheScriptsSourceName()
    {
        MockDapServer? adapter = null;
        var launcher = new FakeProcessLauncher
        {
            Behavior = (spec, process) => FakeDapAdapter.RunOnStandardStreamsAsync(process, NetCoreDbgLike(), server => adapter = server)
        };

        // The document is called "Notes.cs" but the build records its source as script.cs: a breakpoint sent under the document's
        // name matches nothing, and the program would run straight through.
        await using var session = await CSharpProvider(launcher).LaunchAsync(Context("Notes.cs", CSharpScript, breakpointLines: 2)).WaitAsync(Patience);

        var dap = Assert.IsType<DapDebugSession>(session);
        Assert.Equal(["initialize", "launch", "setBreakpoints", "configurationDone"], adapter!.Requests);

        var launch = adapter.RequestBodies.Single(r => r.GetProperty("command").GetString() == "launch").GetProperty("arguments");
        Assert.Equal("dotnet", launch.GetProperty("program").GetString());
        Assert.EndsWith("script.dll", launch.GetProperty("args")[0].GetString());
        Assert.False(launch.GetProperty("stopAtEntry").GetBoolean());

        static string BreakpointPath(JsonElement request) => request.GetProperty("arguments").GetProperty("source").GetProperty("path").GetString()!;
        Assert.Equal("script.cs", BreakpointPath(adapter.RequestBodies.Single(r => r.GetProperty("command").GetString() == "setBreakpoints")));

        await dap.SetBreakpointsAsync("Notes.cs", [new BreakpointItem { LineNumber = 1, IsEnabled = true }]).WaitAsync(Patience);
        var live = adapter.RequestBodies.Where(r => r.GetProperty("command").GetString() == "setBreakpoints").Last();
        Assert.Equal("script.cs", BreakpointPath(live));
    }

    [Fact]
    public async Task NetCoreDbg_ThatRefusesToLaunch_FallsBackToTheInProcessDebugger_AndSaysWhy()
    {
        var launcher = new FakeProcessLauncher
        {
            Behavior = (spec, process) => FakeDapAdapter.RunOnStandardStreamsAsync(process, NetCoreDbgLike(refuseLaunch: true))
        };
        var output = new List<string>();

        await using var session = await CSharpProvider(launcher).LaunchAsync(Context("Notes.cs", CSharpScript, output.Add)).WaitAsync(Patience);

        Assert.IsType<InProcessRoslynDebugSession>(session);
        Assert.Contains(output, text => text.Contains("could not be started", StringComparison.Ordinal) && text.Contains("Falling back", StringComparison.Ordinal));
    }

    // ---- the shared start-up ----

    private static DapClient ClientOf(MockDapServer server)
    {
        var client = server.CreateClient();
        return client;
    }

    [Fact]
    public async Task AnAdapterThatNeverAnswersInitialize_FailsWithATimeout_NotAHang()
    {
        await using var server = new MockDapServer { OnRequest = (_, _, _, _) => Task.CompletedTask };

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            DapAdapterManager.StartSessionAsync("go", ClientOf(server), process: null, Context("/x/main.go", "x"), launch: null, DapHandshake.Standard,
                CancellationToken.None, initializeTimeout: TimeSpan.FromMilliseconds(300)).WaitAsync(Patience));

        Assert.Contains("initialize", ex.Message);
        Assert.Contains("Debug Adapter Protocol", ex.Message);
    }

    [Fact]
    public async Task AnAdapterThatIsStoppedByTheUser_WhileItStarts_IsACancellation_NotATimeout()
    {
        await using var server = new MockDapServer { OnRequest = (_, _, _, _) => Task.CompletedTask };
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DapAdapterManager.StartSessionAsync("go", ClientOf(server), process: null, Context("/x/main.go", "x"), launch: null, DapHandshake.Standard,
                cts.Token, initializeTimeout: TimeSpan.FromSeconds(30)).WaitAsync(Patience));
    }

    [Fact]
    public async Task AnAdapterThatDiesWhileTheSessionStarts_IsReportedAtOnce()
    {
        await using var server = new MockDapServer { OnRequest = (_, _, _, _) => Task.CompletedTask };
        var process = new FakeProcess(1, _ => { }, _ => { });
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            process.Exit(3);
        });

        var ex = await Assert.ThrowsAsync<DebugAdapterExitedException>(() =>
            DapAdapterManager.StartSessionAsync("go", ClientOf(server), process, Context("/x/main.go", "x"), launch: null, DapHandshake.Standard,
                CancellationToken.None, initializeTimeout: TimeSpan.FromSeconds(30)).WaitAsync(Patience));

        Assert.Equal(3, ex.ExitCode);
        Assert.Contains("exit code 3", ex.Message);
    }

    [Fact]
    public async Task AnAdapterOnItsStandardStreams_ThatExits_EndsTheSession()
    {
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launcher = new FakeProcessLauncher
        {
            Behavior = async (spec, process) =>
            {
                await FakeDapAdapter.RunOnStandardStreamsAsync(process, NetCoreDbgLike(), ends: exit.Task);
                process.Exit(0);
            }
        };
        var manager = new DapAdapterManager(launcher, new FakeHostEnvironment());

        await using var session = await manager.LaunchStdioAdapterAsync(
            "csharp", new ProcessStartSpec { FileName = "adapter" }, Context("/x/main.cs", "x"),
            postHandshake: c => c.SendRequestAsync("launch", new { }), ct: CancellationToken.None, handshake: DapHandshake.Standard).WaitAsync(Patience);
        Assert.NotEqual(DebugSessionState.Terminated, session.State);

        exit.SetResult();

        await WaitUntil(() => session.State == DebugSessionState.Terminated);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(20);
        }
    }
}
