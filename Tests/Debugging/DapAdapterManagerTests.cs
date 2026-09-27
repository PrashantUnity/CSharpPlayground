using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Debugging;

public class DapAdapterManagerTests
{
    [Fact]
    public void BreakpointItem_DefaultIsVerified_IsTrue()
    {
        var bp = new BreakpointItem { LineNumber = 10, IsEnabled = true };
        Assert.True(bp.IsVerified);

        bp.IsVerified = false;
        Assert.False(bp.IsVerified);
    }

    [Fact]
    public void BreakpointMargin_StoresVerifiedAndUnverifiedStates()
    {
        var margin = new BreakpointMargin();
        var bp1 = new BreakpointItem { LineNumber = 5, IsEnabled = true, IsVerified = true };
        var bp2 = new BreakpointItem { LineNumber = 15, IsEnabled = true, IsVerified = false };
        var bp3 = new BreakpointItem { LineNumber = 25, IsEnabled = false, IsVerified = true };

        margin.SetBreakpoints([bp1, bp2, bp3]);

        Assert.True(margin.HasBreakpoint(5));
        Assert.True(margin.HasBreakpoint(15));
        Assert.True(margin.HasBreakpoint(25));
        Assert.False(margin.HasBreakpoint(35));
    }

    [Fact]
    public async Task DapAdapterManager_RegistersAndResolvesAdapters()
    {
        var launcher = new FakeProcessLauncher();
        var host = new FakeHostEnvironment();
        var manager = new DapAdapterManager(launcher, host);

        var toolchainSettings = new ToolchainSettingsStore(Path.Combine(Path.GetTempPath(), $"toolchains_{Guid.NewGuid():N}.json"));
        var javaToolchain = new JavaToolchainProvider(host, launcher, toolchainSettings, Path.GetTempPath());
        var javaProvider = new JavaDebuggerProvider(javaToolchain, launcher, host, manager);

        Assert.True(manager.HasAdapter(LanguageIds.Java));
        Assert.NotNull(manager.GetAdapter(LanguageIds.Java));

        var resolution = await manager.ResolveDebuggerAsync(LanguageIds.Java, null);
        Assert.Contains("java-debug", resolution.DebuggerName);
    }

    [Fact]
    public async Task DapDebugSession_UpdatesBreakpointVerified_FromSetBreakpointsResponse()
    {
        var clientPipe = new Pipe();
        var serverPipe = new Pipe();

        var clientIn = serverPipe.Reader.AsStream();
        var clientOut = clientPipe.Writer.AsStream();

        var serverIn = clientPipe.Reader.AsStream();
        var serverOut = serverPipe.Writer.AsStream();

        var dapClient = new DapClient(clientIn, clientOut);
        dapClient.Start();

        var session = new DapDebugSession("test", dapClient);

        // Mock server answering setBreakpoints with verified=true for line 10, verified=false for line 20
        var serverTask = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            int read = await serverIn.ReadAsync(buffer);
            var text = Encoding.UTF8.GetString(buffer, 0, read);

            // Read header & request
            if (text.Contains("\"setBreakpoints\""))
            {
                var responseJson = JsonSerializer.Serialize(new
                {
                    seq = 1,
                    type = "response",
                    request_seq = 1,
                    command = "setBreakpoints",
                    success = true,
                    body = new
                    {
                        breakpoints = new[]
                        {
                            new { verified = true, line = 10 },
                            new { verified = false, line = 20 }
                        }
                    }
                });

                var bodyBytes = Encoding.UTF8.GetBytes(responseJson);
                var headerBytes = Encoding.ASCII.GetBytes($"Content-Length: {bodyBytes.Length}\r\n\r\n");

                await serverOut.WriteAsync(headerBytes);
                await serverOut.WriteAsync(bodyBytes);
                await serverOut.FlushAsync();
            }
        });

        var bps = new List<BreakpointItem>
        {
            new() { LineNumber = 10, IsEnabled = true, IsVerified = false },
            new() { LineNumber = 20, IsEnabled = true, IsVerified = true }
        };

        var verifiedEvents = new List<(int Line, bool Verified)>();
        session.BreakpointVerifiedChanged += (line, verified) => verifiedEvents.Add((line, verified));

        await session.SetBreakpointsAsync("test.cs", bps);
        await serverTask;

        Assert.True(bps[0].IsVerified);
        Assert.False(bps[1].IsVerified);
        Assert.Equal(2, verifiedEvents.Count);
        Assert.Contains(verifiedEvents, e => e.Line == 10 && e.Verified);
        Assert.Contains(verifiedEvents, e => e.Line == 20 && !e.Verified);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task DapDebugSession_HandlesBreakpointEvent_Dynamically()
    {
        var clientPipe = new Pipe();
        var serverPipe = new Pipe();

        var clientIn = serverPipe.Reader.AsStream();
        var clientOut = clientPipe.Writer.AsStream();
        var serverOut = serverPipe.Writer.AsStream();

        var dapClient = new DapClient(clientIn, clientOut);
        dapClient.Start();

        var session = new DapDebugSession("test", dapClient);

        var eventTcs = new TaskCompletionSource<(int Line, bool Verified)>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.BreakpointVerifiedChanged += (line, verified) => eventTcs.TrySetResult((line, verified));

        // Emit dynamic DAP event: breakpoint
        var evtJson = JsonSerializer.Serialize(new
        {
            seq = 5,
            type = "event",
            @event = "breakpoint",
            body = new
            {
                reason = "changed",
                breakpoint = new { verified = true, line = 42 }
            }
        });

        var bodyBytes = Encoding.UTF8.GetBytes(evtJson);
        var headerBytes = Encoding.ASCII.GetBytes($"Content-Length: {bodyBytes.Length}\r\n\r\n");

        await serverOut.WriteAsync(headerBytes);
        await serverOut.WriteAsync(bodyBytes);
        await serverOut.FlushAsync();

        var result = await eventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(42, result.Line);
        Assert.True(result.Verified);

        await session.DisposeAsync();
    }
}
