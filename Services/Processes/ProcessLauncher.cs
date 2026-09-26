using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>A program to start: what, with which arguments, where, and with which environment changes.</summary>
public sealed record ProcessStartSpec
{
    private static readonly IReadOnlyDictionary<string, string?> NoEnvironmentChanges = new Dictionary<string, string?>();

    public required string FileName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public string? WorkingDirectory { get; init; }

    /// <summary>Variables to set on top of the studio's own environment; a null value removes one.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = NoEnvironmentChanges;

    /// <summary>How the command reads in a terminal, for messages.</summary>
    public string CommandLine => string.Join(" ", new[] { FileName }.Concat(Arguments).Select(Quote));

    private static string Quote(string part) =>
        part.Length > 0 && part.IndexOfAny([' ', '\t', '"', '\'']) < 0 ? part : "\"" + part.Replace("\"", "\\\"") + "\"";
}

/// <summary>A started program: its input, its end, and a way to stop it with everything it started.</summary>
public interface IManagedProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }

    /// <summary>The exit code, once the program has ended and all its output has been delivered.</summary>
    Task<int> Completion { get; }

    Task WriteInputAsync(string text, CancellationToken ct = default);

    /// <summary>Ends the program's input, as Ctrl+D does in a terminal.</summary>
    void CloseInput();

    /// <summary>Ends the program and every process it started.</summary>
    void Kill();
}

/// <summary>Starts programs with their output streamed as text. Tests substitute a fake one.</summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Starts <paramref name="spec"/>. Output arrives on background threads as UTF-8 text chunks, split wherever the
    /// program happened to flush (not necessarily at line ends). Throws <see cref="ProcessStartException"/> when the
    /// program can't be started.
    /// </summary>
    IManagedProcess Start(ProcessStartSpec spec, Action<string> onStandardOutput, Action<string> onStandardError);
}

public sealed class ProcessStartException : Exception
{
    public ProcessStartException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

public sealed class ProcessLauncher : IProcessLauncher
{
    public IManagedProcess Start(ProcessStartSpec spec, Action<string> onStandardOutput, Action<string> onStandardError)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Encoding.UTF8 would write a byte-order mark first, and the program's first input() would begin with it.
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            WorkingDirectory = spec.WorkingDirectory ?? string.Empty
        };
        foreach (var argument in spec.Arguments) startInfo.ArgumentList.Add(argument);
        foreach (var (name, value) in spec.Environment)
        {
            if (value == null) startInfo.Environment.Remove(name);
            else startInfo.Environment[name] = value;
        }

        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new ProcessStartException($"Couldn't start {spec.FileName}.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            process.Dispose();
            throw new ProcessStartException($"Couldn't start {spec.FileName}: {ex.Message}", ex);
        }

        if (OperatingSystem.IsWindows()) WindowsJobObject.TryAssign(process);
        return new ManagedProcess(process, onStandardOutput, onStandardError);
    }

    private sealed class ManagedProcess : IManagedProcess
    {
        // After the program exits its output normally ends at once; a grandchild still holding the pipe would keep the
        // readers waiting forever, so they get this long.
        private static readonly TimeSpan OutputDrainLimit = TimeSpan.FromSeconds(2);

        private readonly Process _process;
        private readonly Task _stdout;
        private readonly Task _stderr;
        private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _inputLock = new(1, 1);
        private int _disposed;

        public ManagedProcess(Process process, Action<string> onStandardOutput, Action<string> onStandardError)
        {
            _process = process;
            Id = process.Id;
            ProcessRegistry.Add(this);
            _stdout = Pump(process.StandardOutput.BaseStream, onStandardOutput);
            _stderr = Pump(process.StandardError.BaseStream, onStandardError);
            _ = FinishAsync();
        }

        public int Id { get; }

        public bool HasExited => _completion.Task.IsCompleted;

        public Task<int> Completion => _completion.Task;

        public async Task WriteInputAsync(string text, CancellationToken ct = default)
        {
            if (HasExited) return;
            await _inputLock.WaitAsync(ct);
            try
            {
                var input = _process.StandardInput;
                await input.WriteAsync(text.AsMemory(), ct);
                await input.FlushAsync(ct);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The program ended or closed its input: nothing to write to.
            }
            finally
            {
                _inputLock.Release();
            }
        }

        public void CloseInput()
        {
            try
            {
                _process.StandardInput.Close();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        public void Kill()
        {
            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                // Already gone.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            Kill();
            _ = _completion.Task.ContinueWith(_ => _process.Dispose(), TaskScheduler.Default);
        }

        private static Task Pump(Stream stream, Action<string> sink) => Task.Run(async () =>
        {
            // Decoding the raw bytes keeps a character split across two reads intact and delivers text as soon as it's
            // written, without waiting for the end of a line (a prompt printed with end="", a progress bar).
            var decoder = new UTF8Encoding(false).GetDecoder();
            var bytes = new byte[8192];
            var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            try
            {
                int read;
                while ((read = await stream.ReadAsync(bytes)) > 0)
                {
                    var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
                    if (count > 0) Deliver(sink, new string(chars, 0, count));
                }

                var rest = decoder.GetChars(Array.Empty<byte>(), 0, 0, chars, 0, flush: true);
                if (rest > 0) Deliver(sink, new string(chars, 0, rest));
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The pipe closed under the reader: the process was killed or disposed.
            }
        });

        private static void Deliver(Action<string> sink, string text)
        {
            try
            {
                sink(text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Process output handler failed: {ex.Message}");
            }
        }

        private async Task FinishAsync()
        {
            try
            {
                await _process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
            }

            await Task.WhenAny(Task.WhenAll(_stdout, _stderr), Task.Delay(OutputDrainLimit));

            int exitCode;
            try
            {
                exitCode = _process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                exitCode = -1;
            }

            ProcessRegistry.Remove(this);
            _completion.TrySetResult(exitCode);
        }
    }
}
