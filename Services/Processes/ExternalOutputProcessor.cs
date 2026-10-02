using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>
/// Processes streaming chunks from external language processes (Java, Python, Node.js, C++, etc.).
/// Parses visual dump protocol lines (<c>__FRY_DISPLAY__ {json}</c> or Fry protocol display messages)
/// and dispatches them to <see cref="RichCellOutput"/> handlers, while forwarding clean stdout/stderr
/// text to the terminal console buffer in real-time (including interactive prompts like <c>input()</c>).
/// A display message may name its output (<c>transient.display_id</c>), so that a later <c>update_display</c> redraws it and
/// <c>subscribe</c> asks for its events (see <see cref="ProgramVisuals"/>).
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
    private readonly ProgramVisuals _visuals;

    /// <param name="visuals">The program's visuals, whose events reach it (<see cref="ExternalVisualSession.Visuals"/>); without
    /// them, its visuals still update in place, but it can't listen to them.</param>
    public ExternalOutputProcessor(Action<string> onConsoleText, Action<RichCellOutput> onRichOutput, Action<string, string>? onShare = null, ProgramVisuals? visuals = null)
    {
        _onConsoleText = onConsoleText ?? throw new ArgumentNullException(nameof(onConsoleText));
        _onRichOutput = onRichOutput ?? throw new ArgumentNullException(nameof(onRichOutput));
        _onShare = onShare;
        _visuals = visuals ?? new ProgramVisuals(sink: null);
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

        // 2. Direct JSON display protocol: {"type":"display", ...}, and the other visual messages
        if (trimmed.StartsWith("{\"type\":", StringComparison.Ordinal) && TryHandleDisplayPayload(trimmed, typed: true))
        {
            return;
        }

        var at = line.IndexOf(DisplayMarker, StringComparison.Ordinal);
        if (at > 0 && TryHandleDisplayPayload(line[(at + DisplayMarker.Length)..].Trim()))
        {
            _onConsoleText(line[..at]);
            return;
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

    // A visual message ({"type": "display" | "update_display" | "subscribe" | "unsubscribe", ...}), or after the marker a
    // message without a type (a display) or a bare MIME bundle. Unless the whole line is typed, anything else is text.
    private bool TryHandleDisplayPayload(string json, bool typed = false)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            var type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            switch (type)
            {
                case "subscribe":
                case "unsubscribe":
                    if (_visuals.Subscription(root, subscribe: type == "subscribe") is { } notice) _onConsoleText(notice);
                    return true;
                case "display" or "update_display" or null when root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object:
                    if (type == null && typed) return false;
                    return Show(_visuals.Display(root, update: type == "update_display"), update: type == "update_display");
                case null when !typed:
                    return Show(MimeOutputMapper.Map(root, default), update: false);
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            // Not valid json; leave to standard console
        }

        return false;
    }

    private bool Show(ProgramVisuals.Shown shown, bool update)
    {
        if (shown.Output != null) _onRichOutput(shown.Output);

        // A plain-text or Markdown display has no rich form: it is text for the console, not a protocol line to show as it is.
        if (shown.Text != null) _onConsoleText(shown.Text);
        return update || shown.Output != null || shown.Text != null;
    }

    private bool Show(KernelOutput mapped, bool update) => Show(new ProgramVisuals.Shown(mapped.Text, mapped.Rich, null), update);

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
