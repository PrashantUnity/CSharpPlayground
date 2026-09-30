using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Debugging;

/// <summary>
/// The requests that start a debug session, in the order each adapter needs. lldb-dap (C++ and Rust) follows the DAP
/// specification: it answers <c>launch</c> only after <c>configurationDone</c>, so <c>configurationDone</c> can't come first.
/// </summary>
public class DapHandshakeTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static DebugLaunchContext Context() => new(
        ScriptId: "handshake",
        SourceFilePath: "/work/main.rs",
        SourceCode: "fn main() {}",
        Breakpoints: [new BreakpointItem { LineNumber = 5, IsEnabled = true }],
        Toolchain: null,
        OnLiveOutput: null,
        CancellationToken: CancellationToken.None);

    // What lldb-dap does: initialize answers; launch is held; "initialized" is sent; configurationDone answers and releases launch.
    private static Func<MockDapServer, string, int, JsonElement, Task> LldbDapLike(TaskCompletionSource<int>? launchSeq = null)
    {
        var heldLaunch = 0;
        return async (server, command, seq, _) =>
        {
            switch (command)
            {
                case "launch":
                    heldLaunch = seq;
                    await server.EventAsync("initialized");
                    break;
                case "configurationDone":
                    await server.RespondAsync(seq, command);
                    if (heldLaunch != 0) await server.RespondAsync(heldLaunch, "launch");
                    break;
                default:
                    await server.RespondAsync(seq, command, body: command == "setBreakpoints" ? new { breakpoints = new[] { new { verified = true, line = 5 } } } : null);
                    break;
            }
        };
    }

    private static Task Launch(DapClient client) => client.SendRequestAsync("launch", new { program = "/x" });

    [Fact]
    public async Task TheStandardOrder_LaunchesFirst_ThenBreakpoints_ThenConfigurationDone()
    {
        await using var server = new MockDapServer { OnRequest = LldbDapLike() };
        await using var client = server.CreateClient();
        client.Start();

        await DapAdapterManager.PerformHandshakeAsync(client, Context(), Launch, DapHandshake.Standard, initializedTimeout: null, CancellationToken.None)
            .WaitAsync(Patience);

        Assert.Equal(["initialize", "launch", "setBreakpoints", "configurationDone"], server.Requests);
    }

    [Fact]
    public async Task TheLegacyOrder_IsKeptForTheAdaptersWrittenAgainstIt()
    {
        await using var server = new MockDapServer();
        await using var client = server.CreateClient();
        client.Start();

        await DapAdapterManager.PerformHandshakeAsync(client, Context(), Launch, DapHandshake.Legacy, initializedTimeout: null, CancellationToken.None)
            .WaitAsync(Patience);

        Assert.Equal(["initialize", "setBreakpoints", "configurationDone", "launch"], server.Requests);
    }

    [Fact]
    public async Task TheStandardOrder_WorksWithAnAdapterThatAnswersLaunchFirst_AndSaysInitializedAfterwards()
    {
        await using var server = new MockDapServer
        {
            OnRequest = async (s, command, seq, _) =>
            {
                await s.RespondAsync(seq, command);
                if (command == "launch") await s.EventAsync("initialized");
            }
        };
        await using var client = server.CreateClient();
        client.Start();

        await DapAdapterManager.PerformHandshakeAsync(client, Context(), Launch, DapHandshake.Standard, initializedTimeout: null, CancellationToken.None)
            .WaitAsync(Patience);

        Assert.Equal(["initialize", "launch", "setBreakpoints", "configurationDone"], server.Requests);
    }

    [Fact]
    public async Task TheStandardOrder_WorksWithAnAdapterThatSaysInitializedBeforeItIsAskedToLaunch()
    {
        await using var server = new MockDapServer
        {
            OnRequest = async (s, command, seq, _) =>
            {
                await s.RespondAsync(seq, command);
                if (command == "initialize") await s.EventAsync("initialized");
            }
        };
        await using var client = server.CreateClient();
        client.Start();

        await DapAdapterManager.PerformHandshakeAsync(client, Context(), Launch, DapHandshake.Standard, initializedTimeout: null, CancellationToken.None)
            .WaitAsync(Patience);

        Assert.Equal(["initialize", "launch", "setBreakpoints", "configurationDone"], server.Requests);
    }

    [Fact]
    public async Task TheStandardOrder_CarriesOnWithoutAnInitializedEvent_AfterTheTimeout()
    {
        await using var server = new MockDapServer();
        await using var client = server.CreateClient();
        client.Start();

        await DapAdapterManager.PerformHandshakeAsync(client, Context(), Launch, DapHandshake.Standard, TimeSpan.FromMilliseconds(200), CancellationToken.None)
            .WaitAsync(Patience);

        Assert.Equal(["initialize", "launch", "setBreakpoints", "configurationDone"], server.Requests);
    }

    [Fact]
    public async Task ALaunchTheAdapterRefuses_IsAnErrorAtOnce_NotAHang()
    {
        await using var server = new MockDapServer
        {
            OnRequest = (s, command, seq, _) => command == "launch"
                ? s.RespondAsync(seq, command, success: false, message: "program not found")
                : s.RespondAsync(seq, command)
        };
        await using var client = server.CreateClient();
        client.Start();

        async Task RefusedLaunch(DapClient c)
        {
            var response = await c.SendRequestAsync("launch", new { program = "/nope" });
            if (!response.Success) throw new DapException(response.Message!, "launch", response);
        }

        var ex = await Assert.ThrowsAsync<DapException>(() =>
            DapAdapterManager.PerformHandshakeAsync(client, Context(), RefusedLaunch, DapHandshake.Standard, initializedTimeout: null, CancellationToken.None).WaitAsync(Patience));

        Assert.Equal("program not found", ex.Message);
        Assert.DoesNotContain("configurationDone", server.Requests);
    }

    [Fact]
    public async Task AConfigurationDoneTheAdapterRefuses_IsAnError()
    {
        await using var server = new MockDapServer
        {
            OnRequest = (s, command, seq, _) => command == "configurationDone"
                ? s.RespondAsync(seq, command, success: false, message: "Expected process to be stopped.")
                : s.RespondAsync(seq, command)
        };
        await using var client = server.CreateClient();
        client.Start();

        var ex = await Assert.ThrowsAsync<DapException>(() =>
            DapAdapterManager.PerformHandshakeAsync(client, Context(), Launch, DapHandshake.Standard, TimeSpan.FromMilliseconds(100), CancellationToken.None).WaitAsync(Patience));

        Assert.Contains("Expected process to be stopped", ex.Message);
    }

    [Fact]
    public async Task TheSession_HearsAnEventThatFollowsConfigurationDoneAtOnce()
    {
        // A breakpoint hit (or the end of a short program) right after configurationDone used to be lost: nothing was listening yet.
        await using var server = new MockDapServer
        {
            OnRequest = async (s, command, seq, root) =>
            {
                await s.RespondAsync(seq, command);
                if (command == "configurationDone")
                {
                    await s.EventAsync("exited", new { exitCode = 101 });
                    await s.EventAsync("terminated");
                }
            }
        };
        var client = server.CreateClient();

        await using var session = await DapAdapterManager.StartSessionAsync("rust", client, process: null, Context(), Launch, DapHandshake.Legacy, CancellationToken.None)
            .WaitAsync(Patience);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>();
        session.Terminated += args => terminated.TrySetResult(args);
        var deadline = DateTime.UtcNow + Patience;
        while (session.State != DebugSessionState.Terminated && DateTime.UtcNow < deadline) await Task.Delay(20);

        Assert.Equal(DebugSessionState.Terminated, session.State);
    }

    [Fact]
    public async Task AFailedLaunch_DisposesTheSession()
    {
        await using var server = new MockDapServer();
        var client = server.CreateClient();

        static Task Fails(DapClient _) => Task.FromException(new InvalidOperationException("launch failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DapAdapterManager.StartSessionAsync("rust", client, process: null, Context(), Fails, DapHandshake.Standard, CancellationToken.None).WaitAsync(Patience));

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendRequestAsync("anything"));
    }

    [Fact]
    public async Task ASubscriberThatArrivesAfterTheProgramEnded_IsToldHowItEnded()
    {
        await using var server = new MockDapServer
        {
            OnRequest = async (s, command, seq, root) =>
            {
                await s.RespondAsync(seq, command);
                if (command == "configurationDone")
                {
                    await s.EventAsync("output", new { category = "stdout", output = "hello before anyone listened\n" });
                    await s.EventAsync("exited", new { exitCode = 101 });
                    await s.EventAsync("terminated");
                }
            }
        };
        var client = server.CreateClient();

        await using var session = await DapAdapterManager.StartSessionAsync("rust", client, process: null, Context(), Launch, DapHandshake.Legacy, CancellationToken.None)
            .WaitAsync(Patience);
        var deadline = DateTime.UtcNow + Patience;
        while (session.State != DebugSessionState.Terminated && DateTime.UtcNow < deadline) await Task.Delay(20);

        // Only now does anything subscribe.
        DebugTerminatedEventArgs? ended = null;
        var output = new List<string>();
        session.Terminated += args => ended = args;
        session.OutputReceived += text => output.Add(text);

        Assert.NotNull(ended);
        Assert.Equal(101, ended!.ExitCode);
        Assert.Equal(["hello before anyone listened\n"], output);
    }

    [Theory]
    [InlineData(3, 0, 3)]        // the adapter says 0 for every program; the program's own report wins
    [InlineData(null, 101, 101)] // nothing reported (a crash): the adapter's code stands
    public async Task TheProgramsOwnExitCode_WinsOverTheAdaptersWhenThereIsOne(int? reported, int adapterSays, int expected)
    {
        await using var server = new MockDapServer
        {
            OnRequest = async (s, command, seq, root) =>
            {
                await s.RespondAsync(seq, command);
                if (command == "continue")
                {
                    await s.EventAsync("exited", new { exitCode = adapterSays });
                    await s.EventAsync("terminated");
                }
            }
        };
        var client = server.CreateClient();
        await using var session = (DapDebugSession)await DapAdapterManager.StartSessionAsync("csharp", client, process: null, Context(), null, DapHandshake.Legacy, CancellationToken.None)
            .WaitAsync(Patience);
        session.ExitCodeOverride = () => reported;
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Terminated += args => terminated.TrySetResult(args);

        await session.ContinueAsync();

        Assert.Equal(expected, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }

    [Fact]
    public async Task ASubscriberThatArrivesWhileTheProgramIsPaused_IsToldWhereItStopped()
    {
        await using var server = new MockDapServer
        {
            OnRequest = async (s, command, seq, root) =>
            {
                switch (command)
                {
                    case "stackTrace":
                        await s.RespondAsync(seq, command, body: new { stackFrames = new[] { new { id = 1, name = "main", line = 5, column = 1, source = new { path = "/work/main.rs" } } } });
                        break;
                    case "scopes":
                        await s.RespondAsync(seq, command, body: new { scopes = new[] { new { name = "Locals", variablesReference = 7, expensive = false } } });
                        break;
                    case "variables":
                        await s.RespondAsync(seq, command, body: new { variables = new[] { new { name = "i", value = "7", type = "int", variablesReference = 0 } } });
                        break;
                    default:
                        await s.RespondAsync(seq, command);
                        if (command == "configurationDone") await s.EventAsync("stopped", new { reason = "breakpoint", threadId = 1 });
                        break;
                }
            }
        };
        var client = server.CreateClient();

        await using var session = await DapAdapterManager.StartSessionAsync("rust", client, process: null, Context(), Launch, DapHandshake.Legacy, CancellationToken.None)
            .WaitAsync(Patience);
        var deadline = DateTime.UtcNow + Patience;
        while (session.State != DebugSessionState.Paused && DateTime.UtcNow < deadline) await Task.Delay(20);
        while (session.PausedLine != 5 && DateTime.UtcNow < deadline) await Task.Delay(20);

        DebugPausedEventArgs? paused = null;
        session.Paused += args => paused = args;
        while (paused == null && DateTime.UtcNow < deadline) await Task.Delay(10);

        Assert.NotNull(paused);
        Assert.Equal(5, paused!.LineNumber);
        Assert.Equal("i", Assert.Single(paused.Locals).Name);
        Assert.Equal("7", paused.Locals[0].ValueDisplay);
    }

    [Fact]
    public async Task ContinueThatIsAnsweredAfterTheProgramEnded_DoesNotBringTheSessionBackToLife()
    {
        await using var server = new MockDapServer
        {
            OnRequest = async (s, command, seq, root) =>
            {
                if (command == "continue")
                {
                    await s.EventAsync("exited", new { exitCode = 0 });
                    await s.EventAsync("terminated");
                    await Task.Delay(100);     // the program is over before "continue" is answered
                }

                await s.RespondAsync(seq, command);
            }
        };
        var client = server.CreateClient();
        await using var session = await DapAdapterManager.StartSessionAsync("rust", client, process: null, Context(), null, DapHandshake.Legacy, CancellationToken.None)
            .WaitAsync(Patience);
        var resumed = 0;
        session.Resumed += () => Interlocked.Increment(ref resumed);

        await session.ContinueAsync();

        Assert.Equal(DebugSessionState.Terminated, session.State);
        Assert.Equal(0, resumed);
    }

    private static MockDapServer ServerWithScopes(object[] scopes, Func<int, object[]> variablesOf) => new()
    {
        OnRequest = async (s, command, seq, root) =>
        {
            switch (command)
            {
                case "scopes":
                    await s.RespondAsync(seq, command, body: new { scopes });
                    break;
                case "variables":
                    await s.RespondAsync(seq, command, body: new { variables = variablesOf(root.GetProperty("arguments").GetProperty("variablesReference").GetInt32()) });
                    break;
                default:
                    await s.RespondAsync(seq, command);
                    break;
            }
        }
    };

    [Theory]
    [InlineData("Registers", "registers")]
    [InlineData("Registers", null)]
    [InlineData("General Purpose Registers", null)]
    public async Task TheVariablesPanel_ShowsTheProgramsLocals_NotTheCpuRegistersAnAdapterAlsoOffers(string registerScope, string? hint)
    {
        // lldb-dap answers a stopped C++ or Rust program with its locals and one scope of register groups: those are for reading
        // machine code, and listed as variables they bury the program's own.
        await using var server = ServerWithScopes(
            [
                new { name = "Locals", variablesReference = 1, expensive = false },
                new { name = registerScope, variablesReference = 2, expensive = false, presentationHint = hint }
            ],
            reference => reference == 1
                ? [new { name = "total", value = "6", type = "u32", variablesReference = 0 }]
                : [new { name = "General Purpose Registers", value = "{0}", type = "", variablesReference = 3 }]);
        var client = server.CreateClient();
        client.Start();
        await using var session = new DapDebugSession("rust", client);

        var variables = await session.GetVariablesAsync(0).WaitAsync(Patience);

        Assert.Equal(["total"], variables.Select(v => v.Name));
        Assert.Equal(["scopes", "variables"], server.Requests.Where(r => r is "scopes" or "variables").Distinct().ToList());
        Assert.Equal(1, server.Requests.Count(r => r == "variables"));
    }

    [Fact]
    public async Task TheVariablesPanel_StillShowsEveryOtherScope()
    {
        await using var server = ServerWithScopes(
            [
                new { name = "Arguments", variablesReference = 1, expensive = false },
                new { name = "Locals", variablesReference = 2, expensive = false }
            ],
            reference => reference == 1
                ? [new { name = "n", value = "21", type = "i32", variablesReference = 0 }]
                : [new { name = "result", value = "42", type = "i32", variablesReference = 0 }]);
        var client = server.CreateClient();
        client.Start();
        await using var session = new DapDebugSession("go", client);

        var variables = await session.GetVariablesAsync(0).WaitAsync(Patience);

        Assert.Equal(["n", "result"], variables.Select(v => v.Name));
    }
}
