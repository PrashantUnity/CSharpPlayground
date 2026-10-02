using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using Xunit;

namespace CSharpEditorPlugin.Tests.Debugging;

public class DapClientTests
{
    private static (Stream ClientIn, Stream ClientOut, Stream ServerIn, Stream ServerOut) CreateDuplexStreams()
    {
        var pipe1 = new Pipe();
        var pipe2 = new Pipe();

        // Server writes to pipe1, client reads from pipe1
        // Client writes to pipe2, server reads from pipe2
        var clientIn = pipe1.Reader.AsStream();
        var serverOut = pipe1.Writer.AsStream();

        var clientOut = pipe2.Writer.AsStream();
        var serverIn = pipe2.Reader.AsStream();

        return (clientIn, clientOut, serverIn, serverOut);
    }

    private static async Task WriteDapMessageAsync(Stream stream, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {bytes.Length}\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private static async Task<string> ReadDapMessageAsync(Stream stream)
    {
        var headerBuilder = new StringBuilder();
        var singleByte = new byte[1];

        // Read header until \r\n\r\n
        while (true)
        {
            var read = await stream.ReadAsync(singleByte, 0, 1);
            if (read == 0) throw new EndOfStreamException();
            headerBuilder.Append((char)singleByte[0]);
            if (headerBuilder.ToString().EndsWith("\r\n\r\n")) break;
        }

        var header = headerBuilder.ToString();
        int len = 0;
        foreach (var line in header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                len = int.Parse(line.Substring("Content-Length:".Length).Trim());
            }
        }

        var body = new byte[len];
        int totalRead = 0;
        while (totalRead < len)
        {
            var read = await stream.ReadAsync(body, totalRead, len - totalRead);
            if (read == 0) throw new EndOfStreamException();
            totalRead += read;
        }

        return Encoding.UTF8.GetString(body);
    }

    [Fact]
    public async Task DapClient_CanSendRequest_AndReceiveResponse()
    {
        var (clientIn, clientOut, serverIn, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        client.Start();

        var serverTask = Task.Run(async () =>
        {
            var rawRequest = await ReadDapMessageAsync(serverIn);
            using var doc = JsonDocument.Parse(rawRequest);
            var seq = doc.RootElement.GetProperty("seq").GetInt32();
            var cmd = doc.RootElement.GetProperty("command").GetString();

            var resp = new
            {
                seq = 100,
                type = "response",
                request_seq = seq,
                success = true,
                command = cmd,
                body = new { supportsConfigurationDoneRequest = true }
            };
            await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
        });

        var response = await client.SendRequestAsync("initialize", new { clientID = "test" });
        await serverTask;

        Assert.True(response.Success);
        Assert.Equal("initialize", response.Command);
        Assert.NotNull(response.Body);
        Assert.True(response.Body.Value.GetProperty("supportsConfigurationDoneRequest").GetBoolean());
    }

    [Fact]
    public async Task DapClient_DispatchesEventsCorrectly()
    {
        var (clientIn, clientOut, serverIn, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);

        var eventTcs = new TaskCompletionSource<DapEvent>();
        client.EventReceived += evt =>
        {
            if (evt.Event == "output") eventTcs.TrySetResult(evt);
            return Task.CompletedTask;
        };

        client.Start();

        var evtJson = new
        {
            seq = 200,
            type = "event",
            @event = "output",
            body = new { output = "Hello from debugger!\n" }
        };

        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(evtJson));

        var received = await eventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("output", received.Event);
        Assert.NotNull(received.Body);
        Assert.Equal("Hello from debugger!\n", received.Body.Value.GetProperty("output").GetString());
    }

    [Fact]
    public async Task DapDebugSession_HandlesFullBreakpointsAndPauseLifecycle()
    {
        var (clientIn, clientOut, serverIn, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        client.Start();

        await using var session = new DapDebugSession("csharp", client);

        var pausedTcs = new TaskCompletionSource<DebugPausedEventArgs>();
        session.Paused += args => pausedTcs.TrySetResult(args);

        // Server mock loop
        var serverTask = Task.Run(async () =>
        {
            while (true)
            {
                string raw;
                try
                {
                    raw = await ReadDapMessageAsync(serverIn);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                var seq = root.GetProperty("seq").GetInt32();
                var cmd = root.GetProperty("command").GetString();

                if (cmd == "setBreakpoints")
                {
                    var resp = new
                    {
                        seq = 1,
                        type = "response",
                        request_seq = seq,
                        success = true,
                        command = cmd,
                        body = new
                        {
                            breakpoints = new[] { new { id = 1, verified = true, line = 10 } }
                        }
                    };
                    await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));

                    // Trigger stopped event!
                    var stopEvt = new
                    {
                        seq = 2,
                        type = "event",
                        @event = "stopped",
                        body = new { reason = "breakpoint", threadId = 1 }
                    };
                    await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(stopEvt));
                }
                else if (cmd == "stackTrace")
                {
                    var resp = new
                    {
                        seq = 3,
                        type = "response",
                        request_seq = seq,
                        success = true,
                        command = cmd,
                        body = new
                        {
                            stackFrames = new[]
                            {
                                new
                                {
                                    id = 100,
                                    name = "Main()",
                                    line = 10,
                                    column = 5,
                                    source = new { name = "script.cs", path = "/tmp/script.cs" }
                                }
                            }
                        }
                    };
                    await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
                }
                else if (cmd == "scopes")
                {
                    var resp = new
                    {
                        seq = 4,
                        type = "response",
                        request_seq = seq,
                        success = true,
                        command = cmd,
                        body = new
                        {
                            scopes = new[]
                            {
                                new { name = "Locals", variablesReference = 500, expensive = false }
                            }
                        }
                    };
                    await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
                }
                else if (cmd == "variables")
                {
                    var varRef = root.GetProperty("arguments").GetProperty("variablesReference").GetInt32();
                    if (varRef == 500)
                    {
                        var resp = new
                        {
                            seq = 5,
                            type = "response",
                            request_seq = seq,
                            success = true,
                            command = cmd,
                            body = new
                            {
                                variables = new[]
                                {
                                    new { name = "x", value = "42", type = "int", variablesReference = 0 },
                                    new { name = "person", value = "Person", type = "Person", variablesReference = 600 }
                                }
                            }
                        };
                        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
                    }
                    else if (varRef == 600)
                    {
                        var resp = new
                        {
                            seq = 6,
                            type = "response",
                            request_seq = seq,
                            success = true,
                            command = cmd,
                            body = new
                            {
                                variables = new[]
                                {
                                    new { name = "Name", value = "\"Alice\"", type = "string", variablesReference = 0 }
                                }
                            }
                        };
                        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
                    }
                }
                else if (cmd == "evaluate")
                {
                    var resp = new
                    {
                        seq = 7,
                        type = "response",
                        request_seq = seq,
                        success = true,
                        command = cmd,
                        body = new { result = "84", type = "int", variablesReference = 0 }
                    };
                    await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
                }
                else if (cmd == "continue")
                {
                    var resp = new
                    {
                        seq = 8,
                        type = "response",
                        request_seq = seq,
                        success = true,
                        command = cmd
                    };
                    await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
                }
                else if (cmd == "disconnect")
                {
                    var resp = new
                    {
                        seq = 9,
                        type = "response",
                        request_seq = seq,
                        success = true,
                        command = cmd
                    };
                    await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(resp));
                    break;
                }
            }
        });

        // 1. Set breakpoints
        var bp = new BreakpointItem { LineNumber = 10, IsEnabled = true };
        await session.SetBreakpointsAsync("/tmp/script.cs", new[] { bp });

        // 2. Wait for paused event
        var pausedArgs = await pausedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(10, pausedArgs.LineNumber);
        Assert.Equal("script.cs", pausedArgs.SourceFilePath);
        Assert.Equal("breakpoint", pausedArgs.Reason);
        Assert.Single(pausedArgs.CallStack);
        Assert.Equal("Main()", pausedArgs.CallStack[0].MethodName);

        // 3. Verify locals
        Assert.Equal(2, pausedArgs.Locals.Count);
        Assert.Equal("x", pausedArgs.Locals[0].Name);
        Assert.Equal("42", pausedArgs.Locals[0].ValueDisplay);

        var personVar = pausedArgs.Locals[1];
        Assert.Equal("person", personVar.Name);

        // 4. Test expanding child variables
        var children = await session.GetVariableChildrenAsync(personVar);
        Assert.Single(children);
        Assert.Equal("Name", children[0].Name);
        Assert.Equal("\"Alice\"", children[0].ValueDisplay);

        // 5. Test Evaluate
        var evalResult = await session.EvaluateAsync("x * 2", 100, EvaluationContext.Watch);
        Assert.True(evalResult.Success);
        Assert.Equal("84", evalResult.Value);

        // 6. Test Continue
        await session.ContinueAsync();
        Assert.Equal(DebugSessionState.Running, session.State);

        // 7. Test Stop
        await session.StopAsync();
        Assert.Equal(DebugSessionState.Terminated, session.State);

        await serverTask;
    }

    [Theory]
    [InlineData(101, 101)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public async Task DapDebugSession_ReportsHowTheProgramExited(int sent, int expected)
    {
        var (clientIn, clientOut, _, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        client.Start();
        await using var session = new DapDebugSession("rust", client);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>();
        session.Terminated += args => terminated.TrySetResult(args);

        // The adapter says how the program ended, then that debugging is over (the order the DAP specification gives).
        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 1, type = "event", @event = "exited", body = new { exitCode = sent } }));
        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 2, type = "event", @event = "terminated", body = new { } }));

        var args = await terminated.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(expected, args.ExitCode);
        Assert.False(args.WasCancelled);
        Assert.Equal(DebugSessionState.Terminated, session.State);
    }

    [Fact]
    public async Task DapDebugSession_ATerminatedEventAloneCarriesNoExitCode_SoItIsZero()
    {
        var (clientIn, clientOut, _, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        client.Start();
        await using var session = new DapDebugSession("rust", client);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>();
        session.Terminated += args => terminated.TrySetResult(args);

        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 1, type = "event", @event = "terminated", body = new { } }));

        Assert.Equal(0, (await terminated.Task.WaitAsync(TimeSpan.FromSeconds(5))).ExitCode);
    }

    [Fact]
    public async Task DapDebugSession_AnExitedEventWithoutACode_IsZero_AndTheLaterTerminatedEventChangesNothing()
    {
        var (clientIn, clientOut, _, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        client.Start();
        await using var session = new DapDebugSession("rust", client);
        var seen = new List<DebugTerminatedEventArgs>();
        var first = new TaskCompletionSource<bool>();
        session.Terminated += args => { seen.Add(args); first.TrySetResult(true); };

        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 1, type = "event", @event = "exited", body = new { } }));
        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 2, type = "event", @event = "terminated", body = new { } }));
        await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        var only = Assert.Single(seen);
        Assert.Equal(0, only.ExitCode);
    }

    [Fact]
    public async Task DapClient_HandlesEventsInTheOrderTheyArrive_EvenWhenAHandlerIsSlow()
    {
        var (clientIn, clientOut, _, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        var seen = new List<int>();
        var done = new TaskCompletionSource<bool>();
        client.EventReceived += async evt =>
        {
            var number = evt.Body!.Value.GetProperty("n").GetInt32();
            if (number % 7 == 0) await Task.Delay(15);   // a slow one must not let later ones overtake it
            lock (seen) seen.Add(number);
            if (number == 49) done.TrySetResult(true);
        };
        client.Start();

        for (var n = 0; n < 50; n++)
        {
            await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = n + 1, type = "event", @event = "output", body = new { n } }));
        }

        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(Enumerable.Range(0, 50), seen);
    }

    [Fact]
    public async Task DapClient_ReportsTheDisconnectOnlyAfterEveryEarlierEventIsHandled()
    {
        var (clientIn, clientOut, _, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        var log = new List<string>();
        var disconnected = new TaskCompletionSource<bool>();
        client.EventReceived += async evt =>
        {
            await Task.Delay(50);
            lock (log) log.Add(evt.Event);
        };
        client.Disconnected += () =>
        {
            lock (log) log.Add("disconnected");
            disconnected.TrySetResult(true);
        };
        client.Start();

        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 1, type = "event", @event = "exited", body = new { exitCode = 3 } }));
        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 2, type = "event", @event = "terminated", body = new { } }));
        serverOut.Dispose(); // the adapter goes away

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(["exited", "terminated", "disconnected"], log);
    }

    [Fact]
    public async Task DapDebugSession_AnAdapterThatExitsAndThenDisconnects_KeepsTheProgramsExitCode()
    {
        var (clientIn, clientOut, _, serverOut) = CreateDuplexStreams();
        await using var client = new DapClient(clientIn, clientOut);
        client.Start();
        await using var session = new DapDebugSession("rust", client);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>();
        session.Terminated += args => terminated.TrySetResult(args);

        // lldb-dap says the program exited, then quits: the disconnect (-1) must not replace the exit code.
        await WriteDapMessageAsync(serverOut, JsonSerializer.Serialize(new { seq = 1, type = "event", @event = "exited", body = new { exitCode = 101 } }));
        serverOut.Dispose();

        Assert.Equal(101, (await terminated.Task.WaitAsync(TimeSpan.FromSeconds(5))).ExitCode);
    }
}
