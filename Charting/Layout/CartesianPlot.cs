using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;

/// <summary>
/// A cartesian chart laid out: the series it draws (visible ones, each with its kind, axis, pile and bar lane), the axes
/// that map values to pixels, and the plot area. Line, area, bar, scatter and bubble charts, and any mix of them, share it.
/// The index axis (x) is a row of slots when any series is bars or the axis is a category axis, otherwise numbers; a
/// horizontal bar chart turns the plot so that the index axis runs down the left side and the values run along the bottom.
/// </summary>
internal sealed class CartesianPlot
{
    // About this many pixels between ticks along the numeric index axis.
    private const double IndexTickSpacing = 90;

    private CartesianPlot(ChartOptions options, Rect area, bool horizontal, IReadOnlyList<SeriesLayout> series, AxisMap index, AxisMap left, AxisMap? right, int lanes)
    {
        Options = options;
        Area = area;
        Horizontal = horizontal;
        Series = series;
        Index = index;
        Left = left;
        Right = right;
        LaneCount = lanes;
    }

    public ChartOptions Options { get; }
    public Rect Area { get; }
    public bool Horizontal { get; }

    /// <summary>The series that are drawn (hidden and empty ones left out), in order.</summary>
    public IReadOnlyList<SeriesLayout> Series { get; }

    public AxisMap Index { get; }
    public AxisMap Left { get; }
    public AxisMap? Right { get; }
    public int LaneCount { get; }

    public bool IsCategory => Index.IsCategory;

    public AxisMap ValueAxis(AxisSide side) => side == AxisSide.Right && Right != null ? Right : Left;

    /// <summary>Where value <paramref name="i"/> of a series sits along the index axis: its place in the series, or its x.</summary>
    public double IndexValue(SeriesLayout series, int i) => IsCategory ? i : series.Series.Points[i].X;

    public double IndexPx(double index)
    {
        var unit = Index.Unit(index);
        return Horizontal ? Area.Top + unit * Area.Height : Area.Left + unit * Area.Width;
    }

    public double ValuePx(double value, AxisSide side)
    {
        var unit = ValueAxis(side).Unit(value);
        return Horizontal ? Area.Left + unit * Area.Width : Area.Bottom - unit * Area.Height;
    }

    public Point At(double index, double value, AxisSide side) =>
        Horizontal ? new Point(ValuePx(value, side), IndexPx(index)) : new Point(IndexPx(index), ValuePx(value, side));

    /// <summary>Where bars grow from and areas are filled to: zero, kept inside the axis.</summary>
    public double Baseline(AxisSide side)
    {
        var axis = ValueAxis(side);
        return axis is LogAxis ? axis.Min : Math.Clamp(0, Math.Min(axis.Min, axis.Max), Math.Max(axis.Min, axis.Max));
    }

    /// <summary>The pixels one category slot takes along the index axis (0 when the index axis is numbers).</summary>
    public double SlotLength => Index is CategoryAxis c ? (Horizontal ? Area.Height : Area.Width) / c.Count : 0;

    public static CartesianPlot Create(ChartOptions options, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(options);
        var chartKind = options.Type == ChartType.Histogram ? ChartType.Bar : options.Type;
        var layouts = new List<SeriesLayout>();
        for (var i = 0; i < options.Series.Count; i++)
        {
            var series = options.Series[i];
            if (series.Points.Count == 0 || options.HiddenSeries?.Contains(i) == true) continue;
            layouts.Add(new SeriesLayout { Series = series, Index = i, Kind = series.Kind ?? chartKind });
        }

        ChartStacking.Apply(options.Stack, layouts);
        var lanes = AssignLanes(options.Stack, layouts);
        var horizontal = options.Orientation == ChartOrientation.Horizontal && layouts.Count > 0 && layouts.All(l => l.IsBar);
        var hasRight = layouts.Any(l => l.Side == AxisSide.Right);
        var area = PlotArea(bounds, options, horizontal, hasRight);

        var index = BuildIndexAxis(options, layouts, area, horizontal);
        var left = BuildValueAxis(options.YAxis, layouts, AxisSide.Left, options.Stack, area);
        var right = hasRight ? BuildValueAxis(options.Y2Axis ?? new ChartAxisOptions(), layouts, AxisSide.Right, options.Stack, area) : null;
        return new CartesianPlot(options, area, horizontal, layouts, index, left, right, lanes);
    }

    // Where the data is drawn: inside room for tick labels, and for axis titles when there are any.
    private static Rect PlotArea(Rect bounds, ChartOptions options, bool horizontal, bool hasRight)
    {
        const double titleSize = 16;
        var leftTitle = horizontal ? options.XAxis.Title : options.YAxis.Title;
        var bottomTitle = horizontal ? options.YAxis.Title : options.XAxis.Title;
        var left = (horizontal ? 75 : 55) + (HasText(leftTitle) ? titleSize : 0);
        var right = hasRight ? 55 + (HasText(options.Y2Axis?.Title) ? titleSize : 0) : 20;
        var top = 20;
        var bottom = 30 + (HasText(bottomTitle) ? titleSize : 0);
        return new Rect(
            bounds.Left + left,
            bounds.Top + top,
            Math.Max(10, bounds.Width - left - right),
            Math.Max(10, bounds.Height - top - bottom));
    }

    private static bool HasText(string? text) => !string.IsNullOrWhiteSpace(text);

    // Bars side by side in each slot: one lane for each bar series, or for each pile of stacked ones.
    private static int AssignLanes(ChartStack stack, IReadOnlyList<SeriesLayout> layouts)
    {
        var lanes = new Dictionary<(AxisSide, string), int>();
        var next = 0;
        foreach (var layout in layouts.Where(l => l.IsBar))
        {
            if (stack == ChartStack.None)
            {
                layout.Lane = next++;
                continue;
            }

            var key = (layout.Side, layout.Series.StackGroup ?? string.Empty);
            if (!lanes.TryGetValue(key, out var lane)) lanes[key] = lane = next++;
            layout.Lane = lane;
        }

        return Math.Max(1, next);
    }

    private static AxisMap BuildIndexAxis(ChartOptions options, IReadOnlyList<SeriesLayout> layouts, Rect area, bool horizontal)
    {
        var axis = options.XAxis;
        if (layouts.Any(l => l.IsBar) || axis.Scale == AxisScale.Category)
        {
            var count = layouts.Count == 0 ? 1 : layouts.Max(l => l.Series.Points.Count);
            var points = layouts.Count == 0 ? [] : layouts[0].Series.Points;
            return new CategoryAxis(count, new PlaceNames(points, count), axis.Reverse);
        }

        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        var whole = true;
        foreach (var layout in layouts)
        {
            foreach (var point in layout.Series.Points)
            {
                if (!ChartPoints.IsDrawn(point)) continue;
                if (axis.Scale == AxisScale.Log && point.X <= 0) continue;
                min = Math.Min(min, point.X);
                max = Math.Max(max, point.X);
                if (point.X != Math.Floor(point.X)) whole = false;
            }
        }

        if (axis.Scale == AxisScale.Log)
        {
            var (from, to) = LogAxis.Decades(axis.Min ?? min, axis.Max ?? max);
            return new LogAxis(from, to, axis.Reverse);
        }

        var (lo, hi) = ChartDataRange.XAxis(min, max, axis.Min, axis.Max, axis.SuggestedMin, axis.SuggestedMax);
        if (axis.Scale == AxisScale.Time)
        {
            if (hi - lo < 2) (lo, hi) = (lo - 12 * 3_600_000.0, hi + 12 * 3_600_000.0);
            return new TimeAxis(lo, hi, axis.Reverse);
        }

        var step = ChartTicks.Step(hi - lo, Math.Max(2, (int)((horizontal ? area.Height : area.Width) / IndexTickSpacing)));
        if (whole) step = Math.Max(step, 1);
        (lo, hi) = Padded(lo, hi, axis, BubbleReach(layouts), area.Width);
        return new LinearAxis(lo, hi, step, axis.Reverse);
    }

    // The names of the places along a category axis, written when asked for (an axis shows about ten of a hundred thousand).
    private sealed class PlaceNames(IReadOnlyList<ChartDataPoint> points, int count) : IReadOnlyList<string>
    {
        public int Count => count;

        public string this[int i] => i >= points.Count ? string.Empty : string.IsNullOrEmpty(points[i].Label) ? $"{points[i].X:0.##}" : points[i].Label;

        public IEnumerator<string> GetEnumerator()
        {
            for (var i = 0; i < count; i++) yield return this[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // The biggest circle a bubble chart draws, in pixels (0 for any other chart).
    private static double BubbleReach(IReadOnlyList<SeriesLayout> layouts)
    {
        var reach = 0.0;
        foreach (var layout in layouts.Where(l => l.Kind == ChartType.Bubble))
        {
            for (var i = 0; i < layout.Series.Points.Count; i++) reach = Math.Max(reach, layout.Series.Points[i].Size ?? layout.Series.PointRadius ?? PointLayer.DefaultBubble);
        }

        return reach;
    }

    // Room at the ends of an axis (not where the chart fixes it) for the biggest circle to fit inside the plot.
    private static (double Min, double Max) Padded(double lo, double hi, ChartAxisOptions axis, double reach, double lengthPx)
    {
        if (reach <= 0 || lengthPx <= 4 * reach) return (lo, hi);
        var perPixel = (hi - lo) / (lengthPx - 2 * reach);
        return (axis.Min is null ? lo - reach * perPixel : lo, axis.Max is null ? hi + reach * perPixel : hi);
    }

    private static void Include(double value, bool log, ref double min, ref double max)
    {
        if (!double.IsFinite(value) || log && value <= 0) return;
        if (value < min) min = value;
        if (value > max) max = value;
    }

    private static AxisMap BuildValueAxis(ChartAxisOptions axis, IReadOnlyList<SeriesLayout> layouts, AxisSide side, ChartStack stack, Rect area)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        var zero = false;
        var log = axis.Scale == AxisScale.Log;
        foreach (var layout in layouts.Where(l => l.Side == side))
        {
            zero |= layout.IsBar;
            var points = layout.Series.Points;
            for (var i = 0; i < points.Count; i++)
            {
                if (!layout.IsDrawn(i)) continue;
                var top = layout.ValueAt(i);
                Include(top, log, ref min, ref max);
                if (layout.IsBar) Include(layout.BaseAt(i, 0), log, ref min, ref max);
                else if (layout.IsStacked) Include(layout.BaseAt(i, top), log, ref min, ref max);
            }
        }

        if (axis.Scale == AxisScale.Log)
        {
            var (from, to) = LogAxis.Decades(axis.Min ?? min, axis.Max ?? max);
            return new LogAxis(from, to, axis.Reverse);
        }

        // A percent stack ends at 100 (the axis stops there, not a step past it) unless the chart fixes it.
        var percent = stack == ChartStack.Percent && layouts.Any(l => l.Side == side && l.IsStacked);
        var (lo, hi, step) = ChartDataRange.ValueAxis(min, max, axis.Min, axis.Max ?? (percent ? 100 : null), zero, axis.SuggestedMin, axis.SuggestedMax);
        (lo, hi) = Padded(lo, hi, axis, BubbleReach(layouts), area.Height);
        return new LinearAxis(lo, hi, step, axis.Reverse);
    }
}
