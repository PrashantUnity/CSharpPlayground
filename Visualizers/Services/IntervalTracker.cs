using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>
/// Draws intervals on one shared timeline, a row each, with the answer building up in a lane underneath. Rows are
/// read from the live list at every step, so sorting that list in place reorders them.
/// </summary>
public sealed class IntervalTracker
{
    public const int MaxRows = 40;

    private const double Padding = 16;
    private const double LabelWidth = 58;
    private const double AxisHeight = 40;
    private const double RowHeight = 24;
    private const double RowGap = 8;
    private const double LaneGap = 20;
    private const double FontSize = 11;

    private readonly IList _source;
    private readonly WatchList _watches = new();
    private List<(double Start, double End)> _previousResult = new();

    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    private IntervalTracker(IList source, string title, int sourceLine, string sourceFile)
    {
        _source = source;
        Sequence = new VisualizerSequence();
        Options = new VisualizerOptions
        {
            Title = title,
            Kind = VisualizerKind.Canvas,
            Sequence = Sequence,
            Summary = $"Intervals: {source.Count}"
        };
        Step("Every interval on one timeline", sourceLine: sourceLine, sourceFile: sourceFile);
    }

    /// <summary>Accepts int[][], List&lt;int[]&gt;, IList&lt;IList&lt;int&gt;&gt;, (int, int) tuples, or objects with Start/End members.</summary>
    public static IntervalTracker Create(
        object intervals,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (intervals is not IList list)
        {
            list = ((IEnumerable)intervals).Cast<object?>().ToList();
        }
        return new IntervalTracker(list, title ?? "Intervals", sourceLine, sourceFile);
    }

    /// <summary>Shows a live heap, list or plain value (<c>Watch(() => rooms)</c>) under the timeline at every later step.</summary>
    public IntervalTracker Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "")
    {
        _watches.Add(collection, name);
        return this;
    }

    /// <summary>
    /// Records the timeline. <paramref name="current"/> is the row being examined; <paramref name="active"/> rows are
    /// in play (overlapping, still running); <paramref name="done"/> rows are finished with; <paramref name="removed"/>
    /// rows were discarded. <paramref name="result"/> is the answer so far, <paramref name="pending"/> an interval
    /// being built (the one being merged or inserted), and <paramref name="marker"/> draws a line across the timeline.
    /// </summary>
    public IntervalTracker Step(
        string description,
        int? current = null,
        IEnumerable<int>? active = null,
        IEnumerable<int>? done = null,
        IEnumerable<int>? removed = null,
        object? result = null,
        object? pending = null,
        double? marker = null,
        string? markerLabel = null,
        string pendingLabel = "new",
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var rows = new List<(double Start, double End)?>();
        foreach (var item in _source)
        {
            if (rows.Count == MaxRows) break;
            rows.Add(TryRead(item, out var s, out var e) ? (s, e) : null);
        }

        var answer = new List<(double Start, double End)>();
        if (result is IEnumerable resultItems)
        {
            foreach (var item in resultItems)
            {
                if (TryRead(item, out var s, out var e)) answer.Add((s, e));
            }
        }

        (double Start, double End)? building = pending != null && TryRead(pending, out var ps, out var pe) ? (ps, pe) : null;

        var scene = BuildScene(rows, answer, result != null, building, pendingLabel, current,
            new HashSet<int>(active ?? Array.Empty<int>()),
            new HashSet<int>(done ?? Array.Empty<int>()),
            new HashSet<int>(removed ?? Array.Empty<int>()),
            marker, markerLabel);

        Options.SceneData ??= scene;
        Sequence.AddStep(new VisualizerStep(Sequence.TotalSteps, description, VisualizerKind.Canvas)
        {
            Snapshot = scene,
            SourceLine = sourceLine,
            SourceFile = sourceFile,
            Watches = _watches.Capture()
        });

        _previousResult = answer;
        return this;
    }

    private VisualizerScene BuildScene(
        List<(double Start, double End)?> rows,
        List<(double Start, double End)> answer,
        bool showResult,
        (double Start, double End)? building,
        string pendingLabel,
        int? current,
        HashSet<int> active,
        HashSet<int> done,
        HashSet<int> removed,
        double? marker,
        string? markerLabel)
    {
        var everything = rows.Where(r => r.HasValue).Select(r => r!.Value).Concat(answer).ToList();
        if (building is { } b) everything.Add(b);
        double min = everything.Count > 0 ? Math.Floor(everything.Min(i => Math.Min(i.Start, i.End))) : 0;
        double max = everything.Count > 0 ? Math.Ceiling(everything.Max(i => Math.Max(i.Start, i.End))) : 10;
        if (marker is { } m)
        {
            min = Math.Min(min, Math.Floor(m));
            max = Math.Max(max, Math.Ceiling(m));
        }
        if (max <= min) max = min + 1;

        double unit = Math.Clamp(720 / (max - min), 12, 64);
        double left = Padding + LabelWidth;
        double X(double value) => left + (value - min) * unit;

        int lanes = rows.Count + (building != null ? 1 : 0);
        double rowsTop = Padding + AxisHeight;
        double resultTop = rowsTop + lanes * (RowHeight + RowGap) + (showResult ? LaneGap : 0);
        double bottom = showResult ? resultTop + RowHeight : rowsTop + Math.Max(0, lanes * (RowHeight + RowGap) - RowGap);

        var scene = new VisualizerScene(X(max) + 60, bottom + Padding);

        // Axis with ticks, plus faint guides so rows can be compared by eye.
        double axisY = Padding + 22;
        double tick = TickStep(unit);
        scene.AddLine(X(min), axisY, X(max), axisY, "#64748b", 1.2);
        for (double t = Math.Ceiling(min / tick) * tick; t <= max + 1e-9; t += tick)
        {
            scene.AddLine(X(t), axisY - 4, X(t), axisY + 4, "#64748b", 1.2);
            scene.AddText(X(t), axisY - 20, Format(t), 10, "#94a3b8", isCentered: true);
            var guide = scene.AddLine(X(t), axisY + 6, X(t), bottom, "#334155", 1.0, isDashed: true);
            guide.Opacity = 0.6;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            double y = rowsTop + i * (RowHeight + RowGap);
            scene.AddText(Padding, y + 5, $"#{i}", FontSize, i == current ? "#fbbf24" : "#94a3b8", isBold: i == current);
            if (rows[i] is not { } interval)
            {
                scene.AddText(left, y + 5, "(not an interval)", FontSize, "#f87171");
                continue;
            }

            var (fill, stroke, text) = i == current ? ("#92400e", "#fbbf24", "#ffffff")
                : removed.Contains(i) ? ("#450a0a", "#f87171", "#fecaca")
                : active.Contains(i) ? ("#115e59", "#2dd4bf", "#ffffff")
                : ("#1e293b", "#64748b", "#e2e8f0");
            string label = removed.Contains(i) ? $"✕ {Describe(interval)}" : Describe(interval);
            var bar = AddBar(scene, X, interval, y, label, fill, stroke, text, $"intervals[{i}] = {Describe(interval)}");
            if (i == current) bar.StrokeThickness = 2.2;
            if (removed.Contains(i)) bar.Opacity = 0.7;
            else if (done.Contains(i) && i != current) bar.Opacity = 0.45;
        }

        if (building is { } pendingInterval)
        {
            double y = rowsTop + rows.Count * (RowHeight + RowGap);
            scene.AddText(Padding, y + 5, pendingLabel, FontSize, "#c084fc", isBold: true);
            var bar = AddBar(scene, X, pendingInterval, y, Describe(pendingInterval), "#3b0764", "#c084fc", "#ffffff", $"{pendingLabel} = {Describe(pendingInterval)}");
            bar.StrokeThickness = 2.0;
        }

        if (showResult)
        {
            scene.AddLine(Padding, resultTop - LaneGap / 2, X(max) + 40, resultTop - LaneGap / 2, "#334155", 1.0);
            scene.AddText(Padding, resultTop + 5, "result", FontSize, "#4ade80", isBold: true);
            for (int k = 0; k < answer.Count; k++)
            {
                bool changed = k >= _previousResult.Count || _previousResult[k] != answer[k];
                var bar = AddBar(scene, X, answer[k], resultTop, Describe(answer[k]), "#14532d", changed ? "#a3e635" : "#4ade80", "#ffffff", $"result[{k}] = {Describe(answer[k])}");
                bar.StrokeThickness = changed ? 2.6 : 1.4;
            }
            if (answer.Count == 0)
            {
                scene.AddText(left, resultTop + 5, "(empty)", FontSize, "#64748b");
            }
        }

        if (marker is { } at)
        {
            var line = scene.AddLine(X(at), axisY + 6, X(at), bottom, "#f472b6", 1.8, isDashed: true);
            line.Opacity = 0.95;
            scene.AddText(X(at), axisY + 7, markerLabel ?? Format(at), 10, "#f472b6", isBold: true, isCentered: true);
        }

        return scene;
    }

    // Short bars keep their label beside them rather than spilling over their neighbours.
    private static SceneRect AddBar(VisualizerScene scene, Func<double, double> x, (double Start, double End) interval, double y, string label, string fill, string stroke, string textColor, string tooltip)
    {
        double from = x(Math.Min(interval.Start, interval.End));
        double width = Math.Max(6, Math.Abs(x(interval.End) - x(interval.Start)));
        bool fits = label.Length * 6.6 + 10 <= width;

        var bar = scene.AddRect(from, y, width, RowHeight, fits ? label : null, fill, stroke, 5);
        bar.Tooltip = tooltip;
        bar.TextColor = textColor;
        bar.FontSize = FontSize;
        if (!fits) scene.AddText(from + width + 6, y + 5, label, FontSize, "#cbd5e1");
        return bar;
    }

    // Ticks at 1, 2, 5, 10, 20, 50 ... units, whichever keeps them at least ~30px apart.
    private static double TickStep(double unit)
    {
        foreach (double step in new[] { 1.0, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 5000, 10000 })
        {
            if (step * unit >= 30) return step;
        }
        return 100000;
    }

    private static string Describe((double Start, double End) interval) => $"[{Format(interval.Start)},{Format(interval.End)}]";

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool TryRead(object? item, out double start, out double end)
    {
        start = end = 0;
        switch (item)
        {
            case null:
                return false;
            case IList list when list.Count >= 2 && IsNumber(list[0]) && IsNumber(list[1]):
                start = ToDouble(list[0]);
                end = ToDouble(list[1]);
                return true;
            case ITuple tuple when tuple.Length >= 2 && IsNumber(tuple[0]) && IsNumber(tuple[1]):
                start = ToDouble(tuple[0]);
                end = ToDouble(tuple[1]);
                return true;
        }

        var from = VisualizerReflectionHelper.GetMemberValue(item, "Start", "start", "From", "from", "Begin", "begin");
        var to = VisualizerReflectionHelper.GetMemberValue(item, "End", "end", "To", "to", "Finish", "finish");
        if (!IsNumber(from) || !IsNumber(to)) return false;
        start = ToDouble(from);
        end = ToDouble(to);
        return true;
    }

    private static bool IsNumber(object? value) => value is int or long or double or float or decimal or short or byte;

    private static double ToDouble(object? value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
}
