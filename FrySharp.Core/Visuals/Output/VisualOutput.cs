using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

/// <summary>
/// A chart, 3D plot or visualizer in a cell's output or the Results deck: its spec, what a notebook saves of it, and
/// (for a live one) the display id that updates it. Views build their own drawing from <see cref="Spec"/>, on the UI
/// thread, when they are shown; nothing here is a control.
/// </summary>
public sealed partial class VisualOutput : ObservableObject
{
    /// <summary>The largest visual a notebook saves; a bigger one is redrawn by running its cell again.</summary>
    public const int MaxSavedBytes = 4 * 1024 * 1024;

    [ObservableProperty]
    private VisualSpec _spec;

    // The spec as the notebook saves it, worked out once, when it is first wanted.
    private Lazy<JsonElement?> _saved;

    private VisualOutput(VisualSpec spec, string displayId, Lazy<JsonElement?> saved)
    {
        _spec = spec;
        DisplayId = displayId;
        _saved = saved;
    }

    /// <summary>Names the visual for updates (and, from other programs, for their events).</summary>
    public string DisplayId { get; }

    public VisualFamily Family => Spec.Family;

    public string MimeType => VisualMimeTypes.For(Family);

    /// <summary>
    /// The spec as the notebook saves it; null when it is over <see cref="MaxSavedBytes"/>. It is worked out the first
    /// time it is wanted: a notebook asks for it in the background as soon as it shows the visual (<see
    /// cref="PrepareSavedJson"/>), so neither the script that showed it nor saving the notebook waits for it, and a
    /// visual that is never saved (Code Studio's) is never written out.
    /// </summary>
    public JsonElement? SavedJson => Volatile.Read(ref _saved).Value;

    /// <summary>Works out <see cref="SavedJson"/> now, on this thread (not the UI thread: a big spec takes a while).</summary>
    public void PrepareSavedJson() => _ = SavedJson;

    /// <summary>A visual for a spec. The spec must be valid (<see cref="VisualSpecValidator"/>) and isn't changed after.</summary>
    public static VisualOutput Create(VisualSpec spec, string? displayId = null) =>
        new(spec, displayId ?? NewDisplayId(), SavedWhenWanted(spec));

    /// <summary>A visual read from JSON, whose element (<paramref name="byteLength"/> bytes of UTF-8) is kept as its saved form rather than written out again.</summary>
    public static VisualOutput FromJson(VisualSpec spec, JsonElement json, int byteLength, string? displayId = null)
    {
        // Copied now: the element belongs to a message whose document is let go of.
        JsonElement? saved = byteLength <= MaxSavedBytes ? json.Clone() : null;
        return new VisualOutput(spec, displayId ?? NewDisplayId(), new Lazy<JsonElement?>(() => saved));
    }

    /// <summary>A new spec for the same visual (an update from its program).</summary>
    public void Replace(VisualSpec spec)
    {
        Volatile.Write(ref _saved, SavedWhenWanted(spec));
        Spec = spec;
    }

    // Who wants to hear about clicks, picks and steps on this visual, and which of them.
    private readonly Lock _gate = new();
    private readonly List<(IVisualEventSink Sink, HashSet<string> Kinds)> _subscribers = [];

    /// <summary>Whether anyone listens for <paramref name="kind"/> events; views don't work out events no one wants.</summary>
    public bool HasSubscriber(string kind)
    {
        lock (_gate) return _subscribers.Exists(s => s.Kinds.Contains(kind));
    }

    /// <summary>Starts telling <paramref name="sink"/> about the <paramref name="kinds"/> of event (all of them when none are named).</summary>
    public void Subscribe(IVisualEventSink sink, IEnumerable<string>? kinds = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var wanted = new HashSet<string>(kinds?.Where(VisualEventKinds.IsKnown) ?? VisualEventKinds.All, StringComparer.Ordinal);
        if (wanted.Count == 0) wanted.UnionWith(VisualEventKinds.All);
        lock (_gate)
        {
            var index = _subscribers.FindIndex(s => ReferenceEquals(s.Sink, sink));
            if (index >= 0) _subscribers[index].Kinds.UnionWith(wanted);
            else _subscribers.Add((sink, wanted));
        }

        OnPropertyChanged(nameof(IsInteractive));
    }

    /// <summary>Stops telling <paramref name="sink"/> about the <paramref name="kinds"/> of event (all of them when none are named).</summary>
    public void Unsubscribe(IVisualEventSink sink, IEnumerable<string>? kinds = null)
    {
        lock (_gate)
        {
            var index = _subscribers.FindIndex(s => ReferenceEquals(s.Sink, sink));
            if (index < 0) return;
            if (kinds == null) _subscribers.RemoveAt(index);
            else
            {
                _subscribers[index].Kinds.ExceptWith(kinds);
                if (_subscribers[index].Kinds.Count == 0) _subscribers.RemoveAt(index);
            }
        }

        OnPropertyChanged(nameof(IsInteractive));
    }

    /// <summary>Whether a program listens to this visual (its view says so, and lets go of it when the program ends).</summary>
    public bool IsInteractive
    {
        get
        {
            lock (_gate) return _subscribers.Count > 0;
        }
    }

    /// <summary>
    /// The program that listened to this visual has ended: it is still drawn, but clicks no longer reach any code (its
    /// view says so).
    /// </summary>
    [ObservableProperty]
    private bool _isDisconnected;

    public void Disconnect() => IsDisconnected = true;

    /// <summary>Tells every subscriber to the event's kind; from the UI thread, so a sink hands the work on.</summary>
    public void Raise(VisualEvent visualEvent)
    {
        ArgumentNullException.ThrowIfNull(visualEvent);
        IVisualEventSink[] sinks;
        lock (_gate) sinks = _subscribers.Where(s => s.Kinds.Contains(visualEvent.Kind)).Select(s => s.Sink).ToArray();
        foreach (var sink in sinks) sink.Deliver(DisplayId, visualEvent);
    }

    private static Lazy<JsonElement?> SavedWhenWanted(VisualSpec spec) =>
        new(() => Saved(VisualJson.SerializeToUtf8Bytes(spec)), LazyThreadSafetyMode.ExecutionAndPublication);

    private static JsonElement? Saved(byte[] utf8) => utf8.Length <= MaxSavedBytes ? JsonDocument.Parse(utf8).RootElement.Clone() : null;

    private static string NewDisplayId() => Guid.NewGuid().ToString("N")[..12];
}
