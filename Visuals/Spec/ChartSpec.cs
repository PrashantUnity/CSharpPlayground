namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>
/// A 2D chart (<c>application/vnd.fry.chart.v1+json</c>): one or more series drawn as lines, areas, bars, points or
/// slices. Series are columns of values, as in a Plotly trace; a missing value (null) is a gap.
/// </summary>
public sealed class ChartSpec : VisualSpec
{
    public ChartSpec() : base(VisualFamily.Chart) { }

    public ChartType Kind { get; set; } = ChartType.Line;

    public AxisSpec XAxis { get; set; } = new();
    public AxisSpec YAxis { get; set; } = new();

    /// <summary>A second value axis, up the right side, for the series whose <c>axis</c> is right (they are measured on it, not on <c>yAxis</c>).</summary>
    public AxisSpec Y2Axis { get; set; } = new();

    public LegendSpec Legend { get; set; } = new();

    /// <summary>Which way bars run (default: vertical); only when every series is bars.</summary>
    public ChartOrientation? Orientation { get; set; }

    /// <summary>
    /// How series on the same axis are piled up (default: none). A series' own <c>stack</c> names the pile it joins, so
    /// bars can be stacked in groups. Bars, lines and areas stack; the rest don't.
    /// </summary>
    public ChartStack? Stack { get; set; }

    /// <summary>Where a pie, donut or polar area starts, in degrees clockwise from the top (default: 0).</summary>
    public double? StartAngle { get; set; }

    /// <summary>How far round a pie, donut or polar area goes, in degrees (default: 360; 180 makes a half-circle gauge).</summary>
    public double? Sweep { get; set; }

    /// <summary>The share of a donut's radius left empty in the middle, from 0 to 0.95 (default: 0.55).</summary>
    public double? Cutout { get; set; }

    /// <summary>Grid lines behind the data (default: on, except for pie and donut charts).</summary>
    public bool? Grid { get; set; }

    /// <summary>A dot on every value of a line or area, where there is room for them (default: when no series has more than 40 values).</summary>
    public bool? ShowPoints { get; set; }

    /// <summary>The min, max, average and count summary in the header (default: on).</summary>
    public bool? ShowStats { get; set; }

    /// <summary>How many bars a histogram groups its values into (default: 10, at most 50).</summary>
    public int? Bins { get; set; }

    /// <summary>The colour of the first series; the others follow the studio's palette (a CSS colour, e.g. <c>#4ec9b0</c>).</summary>
    public string? Color { get; set; }

    public List<ChartSeriesSpec> Series { get; set; } = [];
}

/// <summary>
/// One series of a chart, as columns. <see cref="Y"/> holds the values; <see cref="X"/> their positions (the index when
/// left out); <see cref="Labels"/> a name for each value (categories on the x axis, slice names in a pie). Every column
/// given must be as long as <see cref="Y"/>. A histogram's series has <see cref="Values"/> instead: the samples to count.
/// </summary>
public sealed class ChartSeriesSpec
{
    /// <summary>Names the series in click events.</summary>
    public string? Id { get; set; }

    public string? Name { get; set; }
    public string? Color { get; set; }

    /// <summary>The line's width in pixels (default: 2).</summary>
    public double? LineWidth { get; set; }

    /// <summary>How this series is drawn when the chart mixes kinds (a combo): line, area, bar or scatter. Default: the chart's kind.</summary>
    public ChartType? Kind { get; set; }

    /// <summary>Which value axis this series is measured on (default: left).</summary>
    public AxisSide? Axis { get; set; }

    /// <summary>The pile this series stacks in; series that share a name stack together. Default: one pile for all.</summary>
    public string? Stack { get; set; }

    /// <summary>A line or area only: solid, dashed or dotted (default: solid).</summary>
    public LineDash? Dash { get; set; }

    /// <summary>A line or area only: straight segments, or a curve through the values (default: linear).</summary>
    public LineInterpolation? Interpolation { get; set; }

    /// <summary>A smooth line only: how round the curve is, from 0 (straight) to 1 (default: 0.4).</summary>
    public double? Tension { get; set; }

    /// <summary>A line or area only: where it changes level between values, for a stepped line (default: none).</summary>
    public LineStep? Step { get; set; }

    /// <summary>A line only: fill down to the baseline. Default: on for an area, off for a line.</summary>
    public bool? Fill { get; set; }

    /// <summary>A line or area only: fill the space between this series and the series at this index.</summary>
    public int? FillTo { get; set; }

    /// <summary>A line, area, scatter or bubble: the shape of each marker (default: circle).</summary>
    public PointShape? PointStyle { get; set; }

    /// <summary>The marker radius in pixels (default: 3.5 on a line, 4 on a scatter).</summary>
    public double? PointRadius { get; set; }

    /// <summary>A line only: <c>colors</c> colour the segment that ends at each value instead of only its marker.</summary>
    public bool? ColorSegments { get; set; }

    /// <summary>A bubble chart: each circle's radius in pixels.</summary>
    public List<double?>? Sizes { get; set; }

    /// <summary>A bar chart: where each bar starts (default: the baseline), for floating bars from <c>from</c> to <c>y</c>.</summary>
    public List<double?>? From { get; set; }

    /// <summary>A bar only: how round its outer corners are, in pixels.</summary>
    public double? CornerRadius { get; set; }

    public List<double?>? X { get; set; }
    public List<double?> Y { get; set; } = [];
    public List<string?>? Labels { get; set; }

    /// <summary>A colour for each value (a bar, point or slice); null keeps the series colour.</summary>
    public List<string?>? Colors { get; set; }

    /// <summary>Names each value in click events.</summary>
    public List<string?>? Ids { get; set; }

    /// <summary>A histogram's samples: the studio counts them into bins.</summary>
    public List<double?>? Values { get; set; }
}
