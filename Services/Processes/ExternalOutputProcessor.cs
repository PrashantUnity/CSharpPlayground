using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>
/// Processes streaming chunks from external language processes (Java, Python, Node.js, C++, etc.).
/// Parses visual dump protocol lines (<c>__FRY_DISPLAY__ {json}</c> or Fry protocol display messages)
/// and dispatches them to <see cref="RichCellOutput"/> handlers, while forwarding clean stdout/stderr
/// text to the terminal console buffer in real-time (including interactive prompts like <c>input()</c>).
/// </summary>
public sealed class ExternalOutputProcessor
{
    public const string DisplayMarker = "__FRY_DISPLAY__";
    private readonly StringBuilder _partialLine = new();
    private readonly Action<string> _onConsoleText;
    private readonly Action<RichCellOutput> _onRichOutput;

    public ExternalOutputProcessor(Action<string> onConsoleText, Action<RichCellOutput> onRichOutput)
    {
        _onConsoleText = onConsoleText ?? throw new ArgumentNullException(nameof(onConsoleText));
        _onRichOutput = onRichOutput ?? throw new ArgumentNullException(nameof(onRichOutput));
    }

    /// <summary>Processes a stream chunk, forwarding terminal output in real time and buffering protocol lines.</summary>
    public void ProcessChunk(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;

        lock (_partialLine)
        {
            var start = 0;
            while (start < chunk.Length)
            {
                var newline = chunk.IndexOf('\n', start);
                if (newline < 0)
                {
                    // No newline in remaining chunk
                    var remaining = chunk.Substring(start);
                    _partialLine.Append(remaining);

                    // If the buffered line cannot possibly be a protocol message, flush it to console immediately
                    // so interactive prompts (e.g. Python input('Name? ') or C/Java prompts) display without delay.
                    if (!CouldBeProtocolPrefix(_partialLine))
                    {
                        _onConsoleText(_partialLine.ToString());
                        _partialLine.Clear();
                    }
                    return;
                }

                // Newline found: process the complete line
                _partialLine.Append(chunk, start, newline - start);
                var fullLine = _partialLine.ToString();
                _partialLine.Clear();

                ProcessLine(fullLine.TrimEnd('\r'));
                start = newline + 1;
            }
        }
    }

    /// <summary>Flushes any remaining buffered text when execution ends.</summary>
    public void Flush()
    {
        lock (_partialLine)
        {
            if (_partialLine.Length > 0)
            {
                ProcessLine(_partialLine.ToString().TrimEnd('\r'), isTrailing: true);
                _partialLine.Clear();
            }
        }
    }

    private static bool CouldBeProtocolPrefix(StringBuilder sb)
    {
        if (sb.Length == 0) return true;
        var first = sb[0];
        if (first != '_' && first != '{') return false;

        // Check if prefix of DisplayMarker ("__FRY_DISPLAY__")
        if (first == '_')
        {
            var len = Math.Min(sb.Length, DisplayMarker.Length);
            for (var i = 0; i < len; i++)
            {
                if (sb[i] != DisplayMarker[i]) return false;
            }
            return true;
        }

        // Check if prefix of {"type":"display"
        const string jsonPrefix = "{\"type\":";
        var jLen = Math.Min(sb.Length, jsonPrefix.Length);
        for (var i = 0; i < jLen; i++)
        {
            if (sb[i] != jsonPrefix[i]) return false;
        }
        return true;
    }

    private void ProcessLine(string line, bool isTrailing = false)
    {
        var trimmed = line.Trim();

        // 1. Explicit Fry display marker: __FRY_DISPLAY__ {json}
        if (trimmed.StartsWith(DisplayMarker, StringComparison.Ordinal))
        {
            var jsonPayload = trimmed.Substring(DisplayMarker.Length).Trim();
            if (TryHandleDisplayPayload(jsonPayload))
            {
                return; // Handled as visual dump; do not emit raw marker to console!
            }
        }

        // 2. Direct JSON display protocol: {"type":"display", ...}
        if (trimmed.StartsWith("{\"type\":", StringComparison.Ordinal) &&
            (trimmed.Contains("\"display\"") || trimmed.Contains("'display'")))
        {
            if (TryHandleDisplayPayload(trimmed))
            {
                return;
            }
        }

        // Regular console text
        _onConsoleText(isTrailing ? line : line + "\n");
    }

    private bool TryHandleDisplayPayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            JsonElement data = default;
            JsonElement metadata = default;

            if (root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
            {
                data = d;
                if (root.TryGetProperty("metadata", out var m) && m.ValueKind == JsonValueKind.Object)
                {
                    metadata = m;
                }
            }
            else
            {
                data = root;
            }

            var mapped = MimeOutputMapper.Map(data, metadata);
            if (mapped.Rich != null)
            {
                _onRichOutput(mapped.Rich);
                return true;
            }
        }
        catch (JsonException)
        {
            // Not valid json; leave to standard console
        }
        catch
        {
            // Defensive ignore
        }

        return false;
    }
}
