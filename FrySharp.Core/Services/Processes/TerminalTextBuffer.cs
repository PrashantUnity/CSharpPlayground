using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>
/// What a terminal would show for a program's output: a carriage return goes back to the start of the line (progress
/// bars redraw in place), a backspace steps back one character, color codes are dropped, and only the most recent
/// output is kept once there's a lot of it.
/// </summary>
public sealed class TerminalTextBuffer
{
    public const int DefaultMaxLength = 256 * 1024;
    public const string TrimmedMarker = "… (earlier output trimmed) …\n";

    private enum Escape
    {
        None,
        Start,
        ControlSequence,
        OperatingSystemCommand,
        OperatingSystemCommandEnd
    }

    private readonly int _maxLength;
    private readonly StringBuilder _finishedLines = new();
    private readonly StringBuilder _line = new();
    private int _cursor;
    private bool _pendingCarriageReturn;
    private bool _trimmed;
    private Escape _escape;

    public TerminalTextBuffer(int maxLength = DefaultMaxLength)
    {
        _maxLength = Math.Max(1024, maxLength);
    }

    /// <summary>The text as it stands now.</summary>
    public string Text => (_trimmed ? TrimmedMarker : string.Empty) + _finishedLines + _line;

    public bool IsEmpty => !_trimmed && _finishedLines.Length == 0 && _line.Length == 0;

    public void Append(string? chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;

        foreach (var c in chunk)
        {
            if (_escape != Escape.None)
            {
                ReadEscape(c);
                continue;
            }

            if (_pendingCarriageReturn)
            {
                // Decided only now, because "\r\n" can arrive split across two chunks: a line end, not a redraw.
                _pendingCarriageReturn = false;
                if (c == '\n')
                {
                    EndLine();
                    continue;
                }

                _cursor = 0;
            }

            switch (c)
            {
                case '\r':
                    _pendingCarriageReturn = true;
                    break;
                case '\n':
                    EndLine();
                    break;
                case '\b':
                    if (_cursor > 0) _cursor--;
                    break;
                case '\u001b':
                    _escape = Escape.Start;
                    break;
                case '\a':
                    break;
                default:
                    Put(c);
                    break;
            }
        }

        TrimIfLong();
    }

    private void Put(char c)
    {
        if (_cursor < _line.Length) _line[_cursor] = c;
        else _line.Append(c);
        _cursor++;
    }

    private void EndLine()
    {
        _finishedLines.Append(_line).Append('\n');
        _line.Clear();
        _cursor = 0;
    }

    private void ReadEscape(char c)
    {
        switch (_escape)
        {
            case Escape.Start:
                _escape = c switch
                {
                    '[' => Escape.ControlSequence,
                    ']' => Escape.OperatingSystemCommand,
                    _ => Escape.None
                };
                break;
            case Escape.ControlSequence:
                // Parameters until a final byte in @..~. "ESC[K" (clear to end of line) is how many progress bars
                // redraw a shorter line, so it's honored; colors and cursor moves are dropped.
                if (c >= '@' && c <= '~')
                {
                    if (c == 'K' && _cursor < _line.Length) _line.Length = _cursor;
                    _escape = Escape.None;
                }
                break;
            case Escape.OperatingSystemCommand:
                if (c == '\a') _escape = Escape.None;
                else if (c == '\u001b') _escape = Escape.OperatingSystemCommandEnd;
                break;
            case Escape.OperatingSystemCommandEnd:
                _escape = Escape.None;
                break;
        }
    }

    private void TrimIfLong()
    {
        if (_finishedLines.Length + _line.Length <= _maxLength) return;

        // Keep the newest three quarters, starting at a line boundary.
        var excess = _finishedLines.Length + _line.Length - _maxLength * 3 / 4;
        if (excess <= 0) return;
        var cut = Math.Min(excess, _finishedLines.Length);
        var text = _finishedLines.ToString();
        var lineEnd = text.IndexOf('\n', Math.Max(0, cut - 1));
        cut = lineEnd < 0 ? text.Length : lineEnd + 1;
        _finishedLines.Remove(0, cut);
        _trimmed = true;

        if (_line.Length > _maxLength)
        {
            _line.Remove(0, _line.Length - _maxLength / 2);
            _cursor = Math.Min(_cursor, _line.Length);
        }
    }
}
