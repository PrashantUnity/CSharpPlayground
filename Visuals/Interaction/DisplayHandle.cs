using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

/// <summary>
/// A visual that was shown: <c>var chart = Display.LineChart(sales);</c>. It names the output and holds the spec it was
/// drawn from. When the spec couldn't be drawn, an error was shown instead and <see cref="IsShown"/> is false.
/// </summary>
public class DisplayHandle
{
    internal DisplayHandle(VisualOutput? output) => Output = output;

    internal VisualOutput? Output { get; }

    /// <summary>The output's id; null when nothing was shown.</summary>
    public string? DisplayId => Output?.DisplayId;

    public bool IsShown => Output != null;

    /// <summary>What is drawn. Read it, don't change it: <see cref="Update(VisualSpec)"/> changes the visual.</summary>
    public VisualSpec? Spec => Output?.Spec;

    /// <summary>
    /// Redraws the visual from <paramref name="spec"/>, where it is: every view of it redraws (a burst of updates is drawn
    /// once, with the last) and a notebook saves the new one. A spec that can't be drawn changes nothing and throws.
    /// </summary>
    /// <exception cref="VisualSpecException">The spec isn't valid; the message says where.</exception>
    /// <exception cref="InvalidOperationException">Nothing was shown, or the spec is another kind of visual.</exception>
    public void Update(VisualSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var output = Output ?? throw new InvalidOperationException("Nothing was shown, so there is nothing to update.");
        if (spec.Family != output.Family)
        {
            throw new InvalidOperationException($"This is a {output.Family} visual; a {spec.Family} spec can't update it.");
        }

        VisualSpecValidator.EnsureValid(spec);
        output.Replace(spec);
    }

    // The callbacks this handle added, so Off takes away only its own.
    private readonly List<(IVisualEventSink Sink, string Kind)> _callbacks = [];

    /// <summary>
    /// Runs <paramref name="callback"/> for each <paramref name="kind"/> of event on the visual (click, select, step). It
    /// runs when the kernel is free, or inside running code that calls <c>Display.ProcessEvents()</c> or
    /// <c>Display.Wait()</c>; what it prints or shows goes to the cell that asked for it.
    /// </summary>
    public DisplayHandle On(string kind, Action<VisualEvent> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!VisualEventKinds.IsKnown(kind))
        {
            throw new ArgumentException($"There is no \"{kind}\" event: use {string.Join(", ", VisualEventKinds.All)}.", nameof(kind));
        }

        var output = Output ?? throw new InvalidOperationException("Nothing was shown, so there is nothing to listen to.");
        var sink = new CallbackSink(VisualEventLoop.CurrentOrOwn(), callback);
        lock (_callbacks) _callbacks.Add((sink, kind));
        output.Subscribe(sink, [kind]);
        return this;
    }

    /// <summary>Runs <paramref name="callback"/> when an element is clicked: <c>e.Target</c> is what was clicked.</summary>
    public DisplayHandle OnClick(Action<VisualEvent> callback) => On(VisualEventKinds.Click, callback);

    /// <summary>Runs <paramref name="callback"/> when the elements picked with Ctrl- or Shift-click change: <c>e.Targets</c>.</summary>
    public DisplayHandle OnSelect(Action<VisualEvent> callback) => On(VisualEventKinds.Select, callback);

    /// <summary>Runs <paramref name="callback"/> when a step visualizer moves to a step: <c>e.Index</c>.</summary>
    public DisplayHandle OnStep(Action<VisualEvent> callback) => On(VisualEventKinds.Step, callback);

    /// <summary>Stops the callbacks this handle added, for one kind of event or all.</summary>
    public DisplayHandle Off(string? kind = null)
    {
        (IVisualEventSink Sink, string Kind)[] removed;
        lock (_callbacks)
        {
            removed = _callbacks.Where(c => kind == null || c.Kind == kind).ToArray();
            _callbacks.RemoveAll(c => kind == null || c.Kind == kind);
        }

        foreach (var (sink, _) in removed) Output?.Unsubscribe(sink);
        return this;
    }

    public override string ToString() =>
        Output is { } output ? $"{output.Family} {output.DisplayId}: {output.Spec.Title ?? "untitled"}" : "(not shown)";
}

/// <summary>A shown chart, 3D plot or visualizer, with its spec typed.</summary>
public sealed class DisplayHandle<TSpec> : DisplayHandle where TSpec : VisualSpec
{
    internal DisplayHandle(VisualOutput? output) : base(output) { }

    /// <inheritdoc cref="DisplayHandle.Spec"/>
    public new TSpec? Spec => Output?.Spec as TSpec;

    /// <summary>
    /// Changes the visual: <paramref name="change"/> gets a copy of the spec shown, and what it makes of it is drawn,
    /// e.g. <c>chart.Update(s => s.Series[0].Y.Add(42))</c>.
    /// </summary>
    /// <exception cref="VisualSpecException">The changed spec isn't valid; the visual stays as it was.</exception>
    public void Update(Action<TSpec> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var shown = Spec ?? throw new InvalidOperationException("Nothing was shown, so there is nothing to update.");
        var next = VisualJson.Clone(shown);
        change(next);
        Update(next);
    }
}
