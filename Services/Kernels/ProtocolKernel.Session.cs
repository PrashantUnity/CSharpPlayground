using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

// The kernel program's side of a ProtocolKernel: one cell's run, and one run of the program with its pipes.
public sealed partial class ProtocolKernel
{
    /// <summary>One cell's run: where its output goes, and what its error said.</summary>
    private sealed class Execution(string id, KernelExecutionRequest request, CancellationToken token)
    {
        private const int MaxKeptOutput = 1024 * 1024;
        private readonly StringBuilder _output = new();

        public string Id { get; } = id;
        public KernelExecutionRequest Request { get; } = request;
        public CancellationToken Token { get; } = token;
        public volatile bool Finished;
        public volatile bool InterruptRequested;
        public string? ErrorSummary;
        public string? MissingDependency;
        public string? MissingName;

        public string Output
        {
            get
            {
                lock (_output) return _output.ToString();
            }
        }

        public void Write(string? text)
        {
            if (string.IsNullOrEmpty(text)) return;
            lock (_output)
            {
                if (_output.Length < MaxKeptOutput) _output.Append(text);
            }

            Request.OnConsole?.Invoke(text);
        }

        public void Show(RichCellOutput output) => Request.OnRichOutput?.Invoke(output);
    }

    /// <summary>One run of the kernel program: its pipes, the requests waiting for replies, and its end.</summary>
    private sealed class Session(ProtocolKernel owner)
    {
        private const int StderrTailLength = 4000;

        private readonly StringBuilder _partialLine = new();
        private readonly StringBuilder _stderrTail = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
        private readonly TaskCompletionSource<JsonElement> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IManagedProcess? _process;
        private volatile Execution? _current;
        private volatile Execution? _last;
        private volatile bool _dead;
        private volatile bool _shuttingDown;
        private KernelStoppedException? _stopped;

        public Task<JsonElement> Ready => _ready.Task;

        public bool IsAlive => !_dead && _process is { HasExited: false };

        public int? ProcessId => _process?.Id;

        public void Start(ProcessStartSpec spec, IProcessLauncher processes)
        {
            _process = processes.Start(spec, OnStandardOutput, OnStandardError);
            _ = _process.Completion.ContinueWith(t => OnExited(t.IsCompletedSuccessfully ? t.Result : -1), TaskScheduler.Default);
        }

        public void Begin(Execution execution)
        {
            _current = execution;
            _last = execution;
        }

        public void End(Execution execution)
        {
            execution.Finished = true;
            if (ReferenceEquals(_current, execution)) _current = null;
        }

        public Task<JsonElement> Expect(string id)
        {
            var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = reply;
            if (_dead) reply.TrySetException(_stopped ?? new KernelStoppedException($"The {owner.DisplayName} kernel isn't running."));
            return reply.Task;
        }

        public Task SendAsync(object message) =>
            _process?.WriteInputAsync(JsonSerializer.Serialize(message) + "\n") ?? Task.CompletedTask;

        public void Kill() => _process?.Kill();

        public void Shutdown()
        {
            _shuttingDown = true;
            if (_process == null) return;
            // Closing its input is what ends the program (it exits on end of input, with what it started); the kill
            // makes sure.
            _process.CloseInput();
            _process.Kill();
        }

        private void OnStandardOutput(string chunk)
        {
            lock (_partialLine)
            {
                var start = 0;
                while (true)
                {
                    var newline = chunk.IndexOf('\n', start);
                    if (newline < 0)
                    {
                        _partialLine.Append(chunk, start, chunk.Length - start);
                        return;
                    }

                    string line;
                    if (_partialLine.Length > 0)
                    {
                        _partialLine.Append(chunk, start, newline - start);
                        line = _partialLine.ToString();
                        _partialLine.Clear();
                    }
                    else
                    {
                        line = chunk.Substring(start, newline - start);
                    }

                    Handle(line.TrimEnd('\r'));
                    start = newline + 1;
                }
            }
        }

        private void OnStandardError(string chunk)
        {
            lock (_stderrTail)
            {
                _stderrTail.Append(chunk);
                if (_stderrTail.Length > StderrTailLength * 2) _stderrTail.Remove(0, _stderrTail.Length - StderrTailLength);
            }

            // What C code and child processes print, and anything the program writes before it's ready.
            (_current ?? _last)?.Write(chunk);
        }

        private void Handle(string line)
        {
            if (line.Length == 0) return;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                (_current ?? _last)?.Write(line + "\n"); // not a protocol message: show it rather than lose it
                return;
            }

            using (document)
            {
                var message = document.RootElement;
                var type = Text(message, "type");
                var target = _current ?? _last;
                switch (type)
                {
                    case "ready":
                        _ready.TrySetResult(message.Clone());
                        break;
                    case "stream":
                        target?.Write(Text(message, "text"));
                        break;
                    case "display":
                        var output = MimeOutputMapper.Map(
                            message.TryGetProperty("data", out var data) ? data : default,
                            message.TryGetProperty("metadata", out var metadata) ? metadata : default);
                        if (output.Text != null) target?.Write(output.Text);
                        if (output.Rich != null) target?.Show(output.Rich);
                        break;
                    case "error":
                        if (target != null)
                        {
                            var name = Text(message, "ename");
                            var value = Text(message, "evalue");
                            target.ErrorSummary = value.Length > 0 ? $"{name}: {value}" : name;
                            target.MissingDependency = Text(message, "missingModule") is { Length: > 0 } module ? module : null;
                            target.MissingName = Text(message, "missingName") is { Length: > 0 } missing ? missing : null;
                            target.Write(Text(message, "traceback"));
                        }
                        break;
                    case "input_request":
                        _ = AnswerAsync(target, Text(message, "prompt"), message.TryGetProperty("password", out var password) && password.ValueKind == JsonValueKind.True);
                        break;
                    case "reply":
                        if (_pending.TryRemove(Text(message, "id"), out var reply)) reply.TrySetResult(message.Clone());
                        break;
                }
            }
        }

        // input(): the question goes to whoever runs the cell (a notebook shows an input box); no one to ask is end of input.
        private async Task AnswerAsync(Execution? target, string prompt, bool password)
        {
            string? value = null;
            try
            {
                if (target?.Request.OnInputRequest is { } ask) value = await ask(prompt, password, target.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
            {
                value = null;
            }

            try
            {
                await SendAsync(new { type = "input_reply", value });
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
            }

            // As a terminal shows it: the prompt, then what was typed.
            target?.Write(prompt + (password ? string.Empty : value ?? string.Empty) + "\n");
        }

        private void OnExited(int exitCode)
        {
            _dead = true;
            string tail;
            lock (_stderrTail) tail = _stderrTail.ToString().Trim();
            if (tail.Length > StderrTailLength) tail = tail[^StderrTailLength..];

            _stopped = new KernelStoppedException(
                $"The {owner.DisplayName} kernel stopped (exit code {exitCode})." +
                (tail.Length > 0 ? $" Its last output:\n{tail}" : string.Empty) +
                "\nRun the cell again to start a fresh kernel; the variables from before are gone.");
            _ready.TrySetException(_stopped);
            foreach (var id in _pending.Keys.ToList())
            {
                if (_pending.TryRemove(id, out var reply)) reply.TrySetException(_stopped);
            }

            owner.OnSessionEnded(this, unexpected: !_shuttingDown);
        }
    }
}
