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
    public LegendSpec Legend { get; set; } = new();

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
