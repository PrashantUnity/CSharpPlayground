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

    /// <summary>
    /// <c>__FRY_SHARE__ {"name":"nums","json":"[1,2,3]"}</c>: a value the program offers to other kernels' cells (<c>#!share</c>),
    /// as JSON text. It is handed to <c>onShare</c> and isn't shown as output.
    /// </summary>
    public const string ShareMarker = "__FRY_SHARE__";

    private readonly StringBuilder _partialLine = new();
    private readonly Action<string> _onConsoleText;
    private readonly Action<RichCellOutput> _onRichOutput;
    private readonly Action<string, string>? _onShare;

    public ExternalOutputProcessor(Action<string> onConsoleText, Action<RichCellOutput> onRichOutput, Action<string, string>? onShare = null)
    {
        _onConsoleText = onConsoleText ?? throw new ArgumentNullException(nameof(onConsoleText));
        _onRichOutput = onRichOutput ?? throw new ArgumentNullException(nameof(onRichOutput));
        _onShare = onShare;
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

        // Check if prefix of DisplayMarker ("__FRY_DISPLAY__") or ShareMarker ("__FRY_SHARE__")
        if (first == '_')
        {
            return IsPrefixOf(sb, DisplayMarker) || IsPrefixOf(sb, ShareMarker);
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

    private static bool IsPrefixOf(StringBuilder sb, string marker)
    {
        var len = Math.Min(sb.Length, marker.Length);
        for (var i = 0; i < len; i++)
        {
            if (sb[i] != marker[i]) return false;
        }

        return true;
    }

    private void ProcessLine(string line, bool isTrailing = false)
    {
        var trimmed = line.Trim();

        // 0. Filter out launcher runtime diagnostic banners that are not part of user code
        if (IsRuntimeNoise(trimmed))
        {
            return;
        }

        // 0b. A value the program shares with other kernels
        if (_onShare != null && trimmed.StartsWith(ShareMarker, StringComparison.Ordinal) && TryHandleSharePayload(trimmed[ShareMarker.Length..].Trim()))
        {
            return;
        }

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

    private bool TryHandleSharePayload(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String &&
                root.TryGetProperty("json", out var value) && value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(name.GetString()))
            {
                _onShare!(name.GetString()!, value.GetString()!);
                return true;
            }
        }
        catch (JsonException)
        {
            // Not a share message: it's ordinary output.
        }

        return false;
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

            // A plain-text or Markdown display has no rich form: it is text for the console, not a protocol line to show as it is.
            if (mapped.Text != null)
            {
                _onConsoleText(mapped.Text);
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

    /// <summary>Checks whether a line is a runtime launcher diagnostic banner (e.g. JVM Picked up JAVA_TOOL_OPTIONS).</summary>
    public static bool IsRuntimeNoise(string trimmed)
    {
        if (string.IsNullOrWhiteSpace(trimmed)) return false;

        // JVM launcher diagnostic notices when JAVA_TOOL_OPTIONS or _JAVA_OPTIONS are present
        if (trimmed.StartsWith("Picked up JAVA_TOOL_OPTIONS:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Picked up _JAVA_OPTIONS:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Picked up JAVA_OPTIONS:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
