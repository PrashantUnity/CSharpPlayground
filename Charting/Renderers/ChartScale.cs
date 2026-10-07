using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

/// <summary>
/// The values a chart's axes span: the chart's own range where it fixes one (<see cref="ChartOptions.XMin"/> and the
/// rest), otherwise the range of its values. The value axis runs from tick to tick, a round step apart (<see
/// cref="StepY"/>); x keeps to the data. A missing value (NaN) is a gap, so it never counts.
/// </summary>
internal readonly record struct ChartDataRange(double MinX, double MaxX, double MinY, double MaxY, double StepY)
{
    /// <summary>The value axis is split into at most this many steps.</summary>
    public const int ValueSteps = 7;

    private const double Epsilon = 1e-6;

    // A value this close to the end of the axis (in steps) would sit on the frame, so the axis goes a step further.
    private const double Room = 0.05;

    /// <param name="zeroBaseline">Bars grow from zero, so zero is always in their range.</param>
    public static ChartDataRange Of(ChartOptions options, bool zeroBaseline = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        foreach (var series in options.Series)
        {
            foreach (var point in series.Points)
            {
                if (!ChartPoints.IsDrawn(point)) continue;
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        var (fromX, toX) = XAxis(minX, maxX, options.XAxis.Min, options.XAxis.Max, options.XAxis.SuggestedMin, options.XAxis.SuggestedMax);
        var (fromY, toY, step) = ValueAxis(minY, maxY, options.YAxis.Min, options.YAxis.Max, zeroBaseline, options.YAxis.SuggestedMin, options.YAxis.SuggestedMax);
        return new ChartDataRange(fromX, toX, fromY, toY, step);
    }

    // Along x, the data as it is (one either side of a single x), unless the chart fixes it.
    internal static (double Min, double Max) XAxis(double dataMin, double dataMax, double? fixedMin, double? fixedMax, double? suggestedMin = null, double? suggestedMax = null)
    {
        if (dataMin > dataMax) (dataMin, dataMax) = (0, 1); // nothing to draw
        if (suggestedMin is { } sMin && double.IsFinite(sMin)) dataMin = Math.Min(dataMin, sMin);
        if (suggestedMax is { } sMax && double.IsFinite(sMax)) dataMax = Math.Max(dataMax, sMax);
        double min = dataMin, max = dataMax;
        if (max - min < Epsilon) (min, max) = (min - 1, max + 1);
        if (fixedMin is { } from && double.IsFinite(from)) min = from;
        if (fixedMax is { } to && double.IsFinite(to)) max = to;
        if (max <= min) max = min + 1; // a range fixed on one side past the data on the other
        return (min, max);
    }

    // The values from tick to tick: all positive start from zero, and an end the chart doesn't fix is a round step past
    // the data.
    internal static (double Min, double Max, double Step) ValueAxis(double dataMin, double dataMax, double? fixedMin, double? fixedMax, bool zeroBaseline, double? suggestedMin = null, double? suggestedMax = null)
    {
        if (dataMin > dataMax) (dataMin, dataMax) = (0, 1); // nothing to draw
        if (suggestedMin is { } sMin && double.IsFinite(sMin)) dataMin = Math.Min(dataMin, sMin);
        if (suggestedMax is { } sMax && double.IsFinite(sMax)) dataMax = Math.Max(dataMax, sMax);
        if (zeroBaseline)
        {
            dataMin = Math.Min(dataMin, 0);
            dataMax = Math.Max(dataMax, 0);
        }

        double min = dataMin, max = dataMax;
        if (max - min < Epsilon)
        {
            // One value: 0 to 1 for zero, otherwise a fifth of it either side, whichever its sign.
            if (Math.Abs(min) < Epsilon) (min, max) = (0, 1);
            else (min, max) = (min - Math.Abs(min) * 0.2, max + Math.Abs(max) * 0.2);
        }
        else if (min >= 0)
        {
            min = 0;
        }

        bool fromFixed = fixedMin is { } from && double.IsFinite(from);
        bool toFixed = fixedMax is { } to && double.IsFinite(to);
        if (fromFixed) min = fixedMin!.Value;
        if (toFixed) max = fixedMax!.Value;
        if (max <= min) max = min + 1; // a range fixed on one side past the data on the other

        // Zero at an end is the baseline (bars of negative values hang from it), so the axis stops there.
        var step = ChartTicks.Step(max - min, ValueSteps);
        if (!toFixed && max != 0)
        {
            var end = Math.Ceiling(max / step - Epsilon) * step;
            max = end - max < step * Room ? end + step : end;
        }

        if (!fromFixed && min != 0)
        {
            var end = Math.Floor(min / step + Epsilon) * step;
            min = min - end < step * Room ? end - step : end;
        }

        return (min, max, step);
    }
}

/// <summary>Where an axis's ticks go: round numbers a round step apart (1, 2, 2.5 or 5 times a power of ten).</summary>
internal static class ChartTicks
{
    private static readonly double[] Multiples = [1, 2, 2.5, 5, 10];

    /// <summary>The smallest round step that splits <paramref name="span"/> into at most <paramref name="steps"/> parts.</summary>
    public static double Step(double span, int steps)
    {
        if (!(span > 0) || !double.IsFinite(span)) return 1;
        var raw = span / Math.Max(1, steps);
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var multiple in Multiples)
        {
            if (multiple * power >= raw * (1 - 1e-9)) return multiple * power;
        }

        return 10 * power;
    }

    /// <summary>The multiples of <paramref name="step"/> from <paramref name="min"/> to <paramref name="max"/>, ends included.</summary>
    public static IEnumerable<double> Between(double min, double max, double step)
    {
        if (!(step > 0) || !double.IsFinite(min) || !double.IsFinite(max)) yield break;
        var first = Math.Ceiling(min / step - 1e-9);
        var last = Math.Floor(max / step + 1e-9);
        for (var k = first; k <= last; k++)
        {
            var tick = k * step;
            yield return Math.Abs(tick) < step * 1e-9 ? 0 : tick; // no "-0"
        }
    }
}

/// <summary>
/// Which of a chart's points are drawn, the unbroken runs a line is drawn through, and, for a series with more values
/// than the plot has pixels, the few of them that draw the same picture.
/// </summary>
internal static class ChartPoints
{
    /// <summary>Marks are drawn on a line only when its values are at least this many pixels apart on average.</summary>
    public const double MarkerSpacing = 4;

    /// <summary>Scatter points closer than this (in pixels, across and down) are drawn as one.</summary>
    public const double ScatterCell = 2;

    /// <summary>A point with a missing (NaN) or infinite coordinate is a gap: nothing is drawn for it.</summary>
    public static bool IsDrawn(ChartDataPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    /// <summary>The runs of consecutive drawn points (start index and length): a line breaks at every gap.</summary>
    public static IEnumerable<(int Start, int Length)> Runs(IReadOnlyList<ChartDataPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        int start = -1;
        for (int i = 0; i < points.Count; i++)
        {
            if (IsDrawn(points[i]))
            {
                if (start < 0) start = i;
                continue;
            }

            if (start >= 0) yield return (start, i - start);
            start = -1;
        }

        if (start >= 0) yield return (start, points.Count - start);
    }

    /// <summary>
    /// The points of a run that draw its line exactly at this width: in each pixel column, the first, lowest, highest and
    /// last value, in order (M4 aggregation), so at most four a column however many values there are. A run with no more
    /// values than columns comes back whole, and so does one whose x goes back on itself (it has no columns).
    /// </summary>
    /// <param name="column">The pixel column an x falls in.</param>
    public static IReadOnlyList<int> ForLine(IReadOnlyList<ChartDataPoint> points, int start, int length, double columns, Func<double, double> column)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(column);
        return ForLineAt(start, length, columns, i => column(points[i].X), i => points[i].Y, Increasing(points, start, length));
    }

    /// <summary>
    /// <see cref="ForLine"/> for values placed by index: <paramref name="columnOf"/> is the pixel column value i falls in,
    /// <paramref name="valueOf"/> the value drawn for it (a stacked top, say), and <paramref name="increasing"/> says
    /// whether the columns never go back (otherwise the run comes back whole).
    /// </summary>
    public static IReadOnlyList<int> ForLineAt(int start, int length, double columns, Func<int, double> columnOf, Func<int, double> valueOf, bool increasing)
    {
        ArgumentNullException.ThrowIfNull(columnOf);
        ArgumentNullException.ThrowIfNull(valueOf);
        var all = new List<int>(Math.Min(length, 4096));
        if (length <= columns || !increasing)
        {
            for (int i = start; i < start + length; i++) all.Add(i);
            return all;
        }

        long current = long.MinValue;
        int first = -1, low = -1, high = -1, last = -1;
        for (int i = start; i < start + length; i++)
        {
            long at = (long)Math.Floor(columnOf(i));
            if (at != current)
            {
                AddColumn(all, first, low, high, last);
                current = at;
                first = low = high = last = i;
                continue;
            }

            if (valueOf(i) < valueOf(low)) low = i;
            if (valueOf(i) > valueOf(high)) high = i;
            last = i;
        }

        AddColumn(all, first, low, high, last);
        return all;
    }

    /// <summary>The points of a series a scatter plot draws: the first in each small cell of the plot, in order.</summary>
    /// <param name="pixel">Where a point is drawn.</param>
    public static IReadOnlyList<int> ForScatter(IReadOnlyList<ChartDataPoint> points, Func<ChartDataPoint, (double X, double Y)> pixel)
    {
        ArgumentNullException.ThrowIfNull(pixel);
        return ForScatterAt(points, i => pixel(points[i]));
    }

    /// <summary><see cref="ForScatter"/> with the pixel looked up by the point's index.</summary>
    public static IReadOnlyList<int> ForScatterAt(IReadOnlyList<ChartDataPoint> points, Func<int, (double X, double Y)> pixelOf)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(pixelOf);
        var drawn = new List<int>();
        var taken = new HashSet<(long, long)>();
        for (int i = 0; i < points.Count; i++)
        {
            if (!IsDrawn(points[i])) continue;
            var (x, y) = pixelOf(i);
            if (taken.Add(((long)Math.Floor(x / ScatterCell), (long)Math.Floor(y / ScatterCell)))) drawn.Add(i);
        }

        return drawn;
    }

    /// <summary>
    /// The bars of a series a bar chart draws: every value there is, or, with more bars than pixels, the tallest in each
    /// pixel column (the others would be drawn behind it).
    /// </summary>
    /// <param name="slotWidth">The pixels each bar's slot takes.</param>
    public static IReadOnlyList<int> ForBars(IReadOnlyList<ChartDataPoint> points, double slotWidth)
    {
        ArgumentNullException.ThrowIfNull(points);
        var drawn = new List<int>();
        long column = long.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            if (!double.IsFinite(points[i].Y)) continue;
            long at = slotWidth >= 1 ? i : (long)Math.Floor(i * slotWidth);
            if (at != column)
            {
                drawn.Add(i);
                column = at;
            }
            else if (Math.Abs(points[i].Y) > Math.Abs(points[drawn[^1]].Y))
            {
                drawn[^1] = i;
            }
        }

        return drawn;
    }

    private static bool Increasing(IReadOnlyList<ChartDataPoint> points, int start, int length)
    {
        for (int i = start + 1; i < start + length; i++)
        {
            if (points[i].X < points[i - 1].X) return false;
        }

        return true;
    }

    // A column's four points in the order they come, each once.
    private static void AddColumn(List<int> into, int first, int low, int high, int last)
    {
        if (first < 0) return;
        Span<int> four = [first, low, high, last];
        four.Sort();
        for (int k = 0; k < four.Length; k++)
        {
            if (k == 0 || four[k] != four[k - 1]) into.Add(four[k]);
        }
    }
}
