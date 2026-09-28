using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>
/// An interactive <see cref="TextReader"/> that pipes user input from the studio UI into
/// an executing program (e.g. via <see cref="Console.In"/>), unblocking <see cref="Console.ReadLine()"/>
/// calls as lines arrive, and completing cleanly on execution cancellation.
/// </summary>
public sealed class InteractiveStdinReader : TextReader
{
    private readonly CancellationToken _cancellationToken;
    private readonly BlockingCollection<string> _lineQueue = new();
    private readonly StringBuilder _charBuffer = new();
    private readonly object _stateLock = new();
    private int _waitingReaders;
    private bool _isCompleted;

    public InteractiveStdinReader(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
    }

    /// <summary>True when code is actively waiting on input from this reader.</summary>
    public bool IsWaitingForInput
    {
        get
        {
            lock (_stateLock)
            {
                return _waitingReaders > 0;
            }
        }
    }

    public event Action? WaitingStarted;
    public event Action? WaitingEnded;

    /// <summary>Feeds a line of input from the user/UI into the reader queue.</summary>
    public void PostInput(string? line)
    {
        if (_isCompleted) return;
        _lineQueue.Add(line ?? string.Empty);
    }

    /// <summary>Signals that no more input will be provided, unblocking any waiting readers with EOF (null).</summary>
    public void Complete()
    {
        lock (_stateLock)
        {
            if (_isCompleted) return;
            _isCompleted = true;
        }

        _lineQueue.CompleteAdding();
    }

    public override string? ReadLine()
    {
        lock (_stateLock)
        {
            if (_charBuffer.Length > 0)
            {
                var full = _charBuffer.ToString();
                _charBuffer.Clear();
                var nl = full.IndexOf('\n');
                if (nl >= 0)
                {
                    var linePart = full[..nl].TrimEnd('\r');
                    if (nl + 1 < full.Length)
                    {
                        _charBuffer.Append(full[(nl + 1)..]);
                    }
                    return linePart;
                }
                return full;
            }
        }

        NotifyWaiting(true);
        try
        {
            return _lineQueue.Take(_cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Collection completed
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            NotifyWaiting(false);
        }
    }

    public override Task<string?> ReadLineAsync()
    {
        return Task.Run(() => ReadLine(), _cancellationToken);
    }

    public override int Read()
    {
        lock (_stateLock)
        {
            if (_charBuffer.Length > 0)
            {
                var c = _charBuffer[0];
                _charBuffer.Remove(0, 1);
                return c;
            }
        }

        var line = ReadLine();
        if (line == null) return -1;

        lock (_stateLock)
        {
            _charBuffer.Append(line).Append('\n');
            var c = _charBuffer[0];
            _charBuffer.Remove(0, 1);
            return c;
        }
    }

    public override int Read(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (index < 0 || count < 0 || index + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0) return 0;

        int read = 0;
        while (read < count)
        {
            var next = Read();
            if (next == -1) break;
            buffer[index + read] = (char)next;
            read++;
            if (Peek() == -1 && read > 0) break;
        }

        return read;
    }

    public override int Peek()
    {
        lock (_stateLock)
        {
            return _charBuffer.Length > 0 ? _charBuffer[0] : -1;
        }
    }

    private void NotifyWaiting(bool isWaiting)
    {
        lock (_stateLock)
        {
            if (isWaiting)
            {
                _waitingReaders++;
            }
            else if (_waitingReaders > 0)
            {
                _waitingReaders--;
            }
        }

        if (isWaiting) WaitingStarted?.Invoke();
        else WaitingEnded?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Complete();
            _lineQueue.Dispose();
        }
        base.Dispose(disposing);
    }
}
