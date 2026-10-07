using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// A chart being put together; start one with <see cref="Charts"/>. Each setting is optional: what is left out is the
/// studio's default, the same in every language.
/// </summary>
public sealed class ChartBuilder : VisualBuilder<ChartBuilder, ChartSpec>
{
    internal ChartBuilder(ChartSpec spec) : base(spec) { }

    /// <summary>A name for the axis along the bottom.</summary>
    public ChartBuilder XLabel(string label) => Edit(s => s.XAxis.Title = label);

    /// <summary>A name for the axis up the side.</summary>
    public ChartBuilder YLabel(string label) => Edit(s => s.YAxis.Title = label);

    /// <summary>The part of the x axis to show (worked out from the data when left out).</summary>
    public ChartBuilder XRange(double min, double max) => Edit(s => (s.XAxis.Min, s.XAxis.Max) = (min, max));

    /// <summary>The part of the y axis to show (worked out from the data when left out).</summary>
    public ChartBuilder YRange(double min, double max) => Edit(s => (s.YAxis.Min, s.YAxis.Max) = (min, max));

    /// <summary>The colour of the first series, a CSS colour such as <c>"#4ec9b0"</c>; the others follow the studio's palette.</summary>
    public ChartBuilder Color(string color) => Edit(s => s.Color = color);

    /// <summary>Shows or hides the series legend (it shows by itself when there is more than one series).</summary>
    public ChartBuilder Legend(bool show = true) => Edit(s => s.Legend.Show = show);

    /// <summary>Shows or hides the grid lines behind the data.</summary>
    public ChartBuilder Grid(bool show = true) => Edit(s => s.Grid = show);

    /// <summary>Shows or hides a dot on every value of a line or area.</summary>
    public ChartBuilder Points(bool show = true) => Edit(s => s.ShowPoints = show);

    /// <summary>Shows or hides the min, max, average and count summary.</summary>
    public ChartBuilder Stats(bool show = true) => Edit(s => s.ShowStats = show);

    /// <summary>How many bars a histogram counts its samples into.</summary>
    public ChartBuilder Bins(int bins) => Edit(s => s.Bins = bins);

    /// <summary>Draws the chart as another kind.</summary>
    public ChartBuilder As(ChartType kind) => Edit(s => s.Kind = kind);

    /// <summary>
    /// Adds a series: numbers, [x, y] pairs, a label → number map or records, like the data a chart starts from. A map of
    /// names to sequences adds a series for each.
    /// </summary>
    public ChartBuilder Series(string name, object data, string? color = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var added = ChartSpecBuilder.From(data, Spec.Kind).Series;
        if (added.Count == 1)
        {
            added[0].Name = name;
            if (color != null) added[0].Color = color;
        }

        return Edit(s => s.Series.AddRange(added));
    }

    /// <summary>Adds a series from records: each at <paramref name="x"/> (a number, or a label) with the value <paramref name="y"/>.</summary>
    public ChartBuilder Series<T>(string name, IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? color = null)
    {
        var added = ChartSpecBuilder.From(records, x, y, Spec.Kind).Series;
        foreach (var series in added)
        {
            series.Name = name;
            if (color != null) series.Color = color;
        }

        return Edit(s => s.Series.AddRange(added));
    }

    /// <summary>Adds a series and says how it is drawn: <c>.Series("Costs", costs, s =&gt; s.Dashed().OnRightAxis())</c>.</summary>
    public ChartBuilder Series(string name, object data, Action<SeriesOptions> style)
    {
        ArgumentNullException.ThrowIfNull(style);
        Series(name, data);
        return Style(style);
    }

    /// <summary>Says how the series added last is drawn (the first one, when none was added).</summary>
    public ChartBuilder Style(Action<SeriesOptions> style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return Edit(s =>
        {
            if (s.Series.Count == 0) throw new InvalidOperationException("There is no series to style yet: give the chart its data first.");
            style(new SeriesOptions(s.Series[^1]));
        });
    }

    /// <summary>Piles series up (bars, lines and areas), each starting where the one under it ends; <paramref name="percent"/> scales every place to 100%.</summary>
    public ChartBuilder Stacked(bool percent = false) => Edit(s => s.Stack = percent ? ChartStack.Percent : ChartStack.Stacked);

    /// <summary>Lays bars along the y axis, the categories running down the left side.</summary>
    public ChartBuilder Horizontal() => Edit(s => s.Orientation = ChartOrientation.Horizontal);

    /// <summary>A second value axis up the right side, for the series set <c>OnRightAxis()</c>.</summary>
    public ChartBuilder RightAxis(string? label = null, double? min = null, double? max = null) => Edit(s =>
    {
        s.Y2Axis.Title = label ?? s.Y2Axis.Title;
        s.Y2Axis.Min = min ?? s.Y2Axis.Min;
        s.Y2Axis.Max = max ?? s.Y2Axis.Max;
    });

    /// <summary>A logarithmic value axis: a power of ten for each step (every value must be above 0).</summary>
    public ChartBuilder LogY() => Edit(s => s.YAxis.Scale = AxisScale.Log);

    /// <summary>A logarithmic x axis (every x must be above 0).</summary>
    public ChartBuilder LogX() => Edit(s => s.XAxis.Scale = AxisScale.Log);

    /// <summary>A time x axis: the x of each series is Unix milliseconds (<c>new DateTimeOffset(date).ToUnixTimeMilliseconds()</c>), and ticks fall on calendar units.</summary>
    public ChartBuilder TimeX() => Edit(s => s.XAxis.Scale = AxisScale.Time);

    /// <summary>One slot for each value on the x axis, named by its label.</summary>
    public ChartBuilder CategoryX() => Edit(s => s.XAxis.Scale = AxisScale.Category);

    /// <summary>How far the value axis goes at least, when the data doesn't reach it; a fixed <c>YRange</c> overrides it.</summary>
    public ChartBuilder SuggestedY(double min, double max) => Edit(s => (s.YAxis.SuggestedMin, s.YAxis.SuggestedMax) = (min, max));

    /// <summary>Runs the x axis from its largest end.</summary>
    public ChartBuilder ReverseX() => Edit(s => s.XAxis.Reverse = true);

    /// <summary>Runs the value axis from its largest end (downwards).</summary>
    public ChartBuilder ReverseY() => Edit(s => s.YAxis.Reverse = true);

    /// <summary>Which side of the chart the legend is on, and shows it.</summary>
    public ChartBuilder LegendAt(LegendPosition position) => Edit(s => (s.Legend.Show, s.Legend.Position) = (true, position));

    /// <summary>A pie, donut or polar area's start (degrees clockwise from the top) and how far round it goes (180 is a half circle).</summary>
    public ChartBuilder Angles(double start, double sweep) => Edit(s => (s.StartAngle, s.Sweep) = (start, sweep));

    /// <summary>A donut's hole, from 0 to 0.95 of its radius.</summary>
    public ChartBuilder Cutout(double share) => Edit(s => s.Cutout = share);

    /// <summary>Names the x positions (or the spokes of a radar, or the slices of a pie): a label for each value of every series that has that many.</summary>
    public ChartBuilder Labels(IEnumerable<string> labels) => Edit(s =>
    {
        var names = labels.Select(l => (string?)l).ToList();
        foreach (var series in s.Series.Where(x => x.Y.Count == names.Count)) series.Labels = names;
    });

    /// <summary>A colour for each value of the first series (a bar, a point or a slice); a null keeps the series colour.</summary>
    public ChartBuilder Colors(params string?[] colors) => Edit(s =>
    {
        if (s.Series.Count == 0) throw new InvalidOperationException("There is no series to colour yet: give the chart its data first.");
        s.Series[0].Colors = [.. colors];
    });
}
