using System.Collections;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// How one series of a chart is drawn, one setting at a time: <c>.Series("Costs", costs, s =&gt; s.Dashed().Step().OnRightAxis())</c>.
/// Each call sets a field of the series' spec, and says in its summary which kinds of series it means anything to.
/// </summary>
public sealed class SeriesOptions
{
    internal SeriesOptions(ChartSeriesSpec spec) => Spec = spec;

    /// <summary>The series' spec, for what the settings don't reach.</summary>
    public ChartSeriesSpec Spec { get; }

    /// <summary>Draws this series as another kind than the chart's, to mix them: line, area, bar or scatter.</summary>
    public SeriesOptions Kind(ChartType kind) => Set(s => s.Kind = kind);

    /// <summary>Measures this series on the second value axis, up the right side.</summary>
    public SeriesOptions OnRightAxis() => Set(s => s.Axis = AxisSide.Right);

    /// <summary>The series' colour, a CSS colour such as <c>"#4ec9b0"</c>.</summary>
    public SeriesOptions Color(string color) => Set(s => s.Color = color);

    /// <summary>A colour for each value (a bar, a point or a slice); a null keeps the series colour.</summary>
    public SeriesOptions Colors(params string?[] colors) => Set(s => s.Colors = [.. colors]);

    /// <summary>A line's width in pixels.</summary>
    public SeriesOptions LineWidth(double pixels) => Set(s => s.LineWidth = pixels);

    /// <summary>A line or area drawn with dashes.</summary>
    public SeriesOptions Dashed() => Set(s => s.Dash = LineDash.Dashed);

    /// <summary>A line or area drawn with dots.</summary>
    public SeriesOptions Dotted() => Set(s => s.Dash = LineDash.Dotted);

    /// <summary>A line or area drawn as a curve through the values; <paramref name="tension"/> (0 to 1) is how round.</summary>
    public SeriesOptions Smooth(double tension = 0.4) => Set(s => (s.Interpolation, s.Tension) = (LineInterpolation.Smooth, tension));

    /// <summary>A curve that never rises or falls between two values that don't.</summary>
    public SeriesOptions Monotone() => Set(s => s.Interpolation = LineInterpolation.Monotone);

    /// <summary>A stepped line: <see cref="LineStep.After"/> jumps straight after a value, <see cref="LineStep.Before"/> holds it until the next x, <see cref="LineStep.Middle"/> jumps halfway.</summary>
    public SeriesOptions Step(LineStep step = LineStep.After) => Set(s => s.Step = step);

    /// <summary>Fills a line down to the baseline (an area is filled already), or not.</summary>
    public SeriesOptions Fill(bool on = true) => Set(s => s.Fill = on);

    /// <summary>Fills the space between this line and the series at index <paramref name="series"/> (counting from 0).</summary>
    public SeriesOptions FillTo(int series) => Set(s => (s.Fill, s.FillTo) = (true, series));

    /// <summary>The shape (and radius) of the markers of a line, scatter or radar series.</summary>
    public SeriesOptions Points(PointShape shape = PointShape.Circle, double? radius = null) => Set(s => (s.PointStyle, s.PointRadius) = (shape, radius ?? s.PointRadius));

    /// <summary>The colours of the values colour the line segment that ends at each, not only its marker.</summary>
    public SeriesOptions ColorSegments(bool on = true) => Set(s => s.ColorSegments = on);

    /// <summary>The pile this series stacks in, when the chart is stacked: series with the same name stack together.</summary>
    public SeriesOptions StackGroup(string name) => Set(s => s.Stack = name);

    /// <summary>Floating bars: each bar runs from the value in <paramref name="from"/> to its own value.</summary>
    public SeriesOptions Floating(IEnumerable<double> from) => Set(s => s.From = [.. from.Select(v => (double?)v)]);

    /// <summary>Rounds the outer corners of bars, by this many pixels.</summary>
    public SeriesOptions Rounded(double radius = 6) => Set(s => s.CornerRadius = radius);

    /// <summary>A bubble chart's radius in pixels for each value.</summary>
    public SeriesOptions Sizes(IEnumerable<double> sizes) => Set(s => s.Sizes = [.. sizes.Select(v => (double?)v)]);

    private SeriesOptions Set(Action<ChartSeriesSpec> change)
    {
        change(Spec);
        return this;
    }
}
