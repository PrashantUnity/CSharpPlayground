using System.Text;

namespace FrySharp.Sidecar.Services;

/// <summary>
/// A run's output on its way to the client. Text is sent in batches: when 8 KB is pending,
/// or 50 ms after the first character, so a program printing 100,000 lines sends a few hundred messages, not 100,000.
/// </summary>
public sealed class RunOutput : IDisposable
{
    public const int FlushChars = 8 * 1024;
    public static readonly TimeSpan FlushDelay = TimeSpan.FromMilliseconds(50);
    public const int MaxTranscriptChars = 1024 * 1024;
    public const string TrimmedMarker = "… (earlier output trimmed) …\r\n";

    private readonly Action<string> _send;
    private readonly Lock _gate = new();
    private readonly StringBuilder _pending = new();
    private readonly StringBuilder _transcript = new();
    private readonly Timer _timer;
    private bool _timerArmed;
    private bool _trimmed;

    public RunOutput(Action<string> send)
    {
        _send = send;
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var flushNow = false;
        lock (_gate)
        {
            _pending.Append(text);
            AppendTranscript(text);
            if (_pending.Length >= FlushChars)
            {
                flushNow = true;
            }
            else if (!_timerArmed)
            {
                _timerArmed = true;
                _timer.Change(FlushDelay, Timeout.InfiniteTimeSpan);
            }
        }

        if (flushNow) Flush();
    }

    public void Flush()
    {
        string batch;
        lock (_gate)
        {
            _timerArmed = false;
            if (_pending.Length == 0) return;
            batch = _pending.ToString();
            _pending.Clear();
            _send(batch);
        }
    }

    public string Transcript
    {
        get
        {
            lock (_gate) return (_trimmed ? TrimmedMarker : string.Empty) + _transcript;
        }
    }

    private void AppendTranscript(string text)
    {
        _transcript.Append(text);
        if (_transcript.Length <= MaxTranscriptChars) return;

        var cut = _transcript.Length - MaxTranscriptChars * 3 / 4;
        for (var i = cut; i < _transcript.Length && i < cut + 4096; i++)
        {
            if (_transcript[i] == '\n')
            {
                cut = i + 1;
                break;
            }
        }
        _transcript.Remove(0, cut);
        _trimmed = true;
    }

    public void Dispose()
    {
        Flush();
        _timer.Dispose();
    }
}
