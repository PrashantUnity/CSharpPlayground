using System.Collections.Concurrent;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

/// <summary>
/// The visuals one program shows and the events it listens for on them, read from its messages (<c>display</c>,
/// <c>update_display</c>, <c>subscribe</c>, <c>unsubscribe</c>) the same way whether they come from a notebook kernel or
/// from <c>__FRY_DISPLAY__</c> lines of a program that is run.
/// </summary>
public sealed class ProgramVisuals
{
    private readonly IVisualEventSink? _sink;
    private readonly VisualOutputRegistry _shown = new();
    private readonly ConcurrentDictionary<VisualOutput, byte> _listenedTo = new();

    /// <param name="sink">Where events on its visuals go (to the program); none when the program can't be told about them.</param>
    public ProgramVisuals(IVisualEventSink? sink) => _sink = sink;

    /// <summary>What a display or update message shows.</summary>
    /// <param name="Text">Text for the console (a plain-text or Markdown display).</param>
    /// <param name="Output">An output to add; none when an update redrew the visual where it is.</param>
    /// <param name="DisplayId">The display id the message named, when that id now names one of its visuals.</param>
    public readonly record struct Shown(string? Text, RichCellOutput? Output, string? DisplayId);

    /// <summary>
    /// A display, or an update of one shown before (by its <c>transient.display_id</c>): the new spec redraws it where it
    /// is. An update for an id this program never showed is shown as a new display.
    /// </summary>
    public Shown Display(JsonElement message, bool update)
    {
        var displayId = DisplayIdOf(message);
        var mapped = MimeOutputMapper.Map(Property(message, "data"), Property(message, "metadata"), displayId);
        var text = mapped.Text != null && !(update && mapped.Rich == null) ? mapped.Text : null;
        if (mapped.Rich is not { } rich) return new Shown(text, null, null);

        var shown = update ? _shown.Apply(displayId, rich) : rich;
        if (shown?.Visual == null || displayId == null) return new Shown(text, shown, null);

        _shown.Register(shown);
        return new Shown(text, shown, displayId);
    }

    /// <summary>
    /// A subscribe or unsubscribe message: <c>{display_id, events: ["click", "select", "step"]}</c> (every kind when left
    /// out). Returns what to tell the program's user when it can't be done.
    /// </summary>
    public string? Subscription(JsonElement message, bool subscribe)
    {
        var displayId = Property(message, "display_id") is { ValueKind: JsonValueKind.String } id ? id.GetString() : null;
        if (displayId == null || !_shown.TryGet(displayId, out var visual))
        {
            return subscribe ? $"There is no display \"{displayId}\" to listen to: show it with a display_id first.\n" : null;
        }

        if (_sink == null) return subscribe ? "Events from visuals can't reach this program.\n" : null;

        string[]? kinds = Property(message, "events") is { ValueKind: JsonValueKind.Array } events
            ? events.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToArray()
            : null;
        if (subscribe)
        {
            visual.Subscribe(_sink, kinds);
            _listenedTo[visual] = 0;
        }
        else
        {
            visual.Unsubscribe(_sink, kinds);
        }

        return null;
    }

    /// <summary>The program has ended: its visuals stay, but nothing listens to them any more, and they say so.</summary>
    public void Disconnect()
    {
        foreach (var visual in _listenedTo.Keys)
        {
            if (_sink != null) visual.Unsubscribe(_sink);
            visual.Disconnect();
        }

        _listenedTo.Clear();
    }

    /// <summary>The <c>transient.display_id</c> of a display message.</summary>
    public static string? DisplayIdOf(JsonElement message) =>
        Property(message, "transient") is { ValueKind: JsonValueKind.Object } transient &&
        transient.TryGetProperty("display_id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    private static JsonElement Property(JsonElement message, string name) =>
        message.ValueKind == JsonValueKind.Object && message.TryGetProperty(name, out var value) ? value : default;
}
