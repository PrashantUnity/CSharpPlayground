using System.Collections.Concurrent;
using System.Threading.Channels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// Starts pretend programs: each one runs <see cref="Behavior"/>, which writes output, reads input and exits as the test
/// says. Nothing real is started.
/// </summary>
public sealed class FakeProcessLauncher : IProcessLauncher
{
    private int _nextId = 1000;

    public ConcurrentQueue<ProcessStartSpec> Started { get; } = new();

    /// <summary>What every started program does; by default it exits with 0 at once.</summary>
    public Func<ProcessStartSpec, FakeProcess, Task> Behavior { get; set; } = (_, p) =>
    {
        p.Exit(0);
        return Task.CompletedTask;
    };

    /// <summary>Makes a start fail, as for a program that isn't installed.</summary>
    public Func<ProcessStartSpec, bool> FailToStart { get; set; } = _ => false;

    public IManagedProcess Start(ProcessStartSpec spec, Action<string> onStandardOutput, Action<string> onStandardError)
    {
        if (FailToStart(spec)) throw new ProcessStartException($"Couldn't start {spec.FileName}: not found");
        Started.Enqueue(spec);
        var process = new FakeProcess(Interlocked.Increment(ref _nextId), onStandardOutput, onStandardError);
        _ = Task.Run(async () =>
        {
            try
            {
                await Behavior(spec, process);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                process.WriteError($"fake process failed: {ex.Message}\n");
                process.Exit(99);
            }
        });
        return process;
    }
}

public sealed class FakeProcess : IManagedProcess
{
    private readonly Action<string> _stdout;
    private readonly Action<string> _stderr;
    private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<string> _input = Channel.CreateUnbounded<string>();
    private readonly System.Text.StringBuilder _pendingInput = new();
    private readonly CancellationTokenSource _killed = new();

    public FakeProcess(int id, Action<string> stdout, Action<string> stderr)
    {
        Id = id;
        _stdout = stdout;
        _stderr = stderr;
    }

    public int Id { get; }
    public bool HasExited => _completion.Task.IsCompleted;
    public Task<int> Completion => _completion.Task;
    public bool WasKilled { get; private set; }
    public bool InputClosed { get; private set; }

    /// <summary>Cancelled when the process is killed, so a behavior waiting on something stops.</summary>
    public CancellationToken KilledToken => _killed.Token;

    public void Write(string text)
    {
        if (!HasExited) _stdout(text);
    }

    public void WriteError(string text)
    {
        if (!HasExited) _stderr(text);
    }

    /// <summary>The next line typed into the program, without its line ending; null at end of input or when killed.</summary>
    public async Task<string?> ReadLineAsync()
    {
        while (true)
        {
            var text = _pendingInput.ToString();
            var newline = text.IndexOf('\n');
            if (newline >= 0)
            {
                _pendingInput.Remove(0, newline + 1);
                return text[..newline].TrimEnd('\r');
            }

            try
            {
                if (!await _input.Reader.WaitToReadAsync(_killed.Token)) return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            while (_input.Reader.TryRead(out var chunk)) _pendingInput.Append(chunk);
        }
    }

    public void Exit(int code) => _completion.TrySetResult(code);

    public Task WriteInputAsync(string text, CancellationToken ct = default)
    {
        if (!InputClosed) _input.Writer.TryWrite(text);
        return Task.CompletedTask;
    }

    public void CloseInput()
    {
        InputClosed = true;
        _input.Writer.TryComplete();
    }

    public void Kill()
    {
        if (HasExited) return;
        WasKilled = true;
        _killed.Cancel();
        _completion.TrySetResult(-9);
    }

    public void Dispose()
    {
    }
}
