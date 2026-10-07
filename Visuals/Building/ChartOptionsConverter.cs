using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

/// <summary>The older chart model (<see cref="ChartOptions"/>) as a spec, losing nothing the chart shows.</summary>
public static class ChartOptionsConverter
{
    public static ChartSpec ToSpec(ChartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var spec = new ChartSpec
        {
            Title = NullIfEmpty(options.Title),
            Subtitle = NullIfEmpty(options.Subtitle),
            // A model's histogram is already counted into its bars.
            Kind = options.Type == ChartType.Histogram ? ChartType.Bar : options.Type,
            Color = options.PrimaryColor,
            Grid = options.ShowGrid,
            ShowPoints = options.ShowPoints,
            ShowStats = options.ShowStats,
            // Settings the model only has by default are left out, so the studio's defaults apply as for any language.
            Legend = { Show = options.ShowLegend ? true : null, Position = options.LegendPosition == LegendPosition.Bottom ? null : options.LegendPosition },
            Orientation = options.Orientation == ChartOrientation.Vertical ? null : options.Orientation,
            Stack = options.Stack == ChartStack.None ? null : options.Stack,
            StartAngle = options.StartAngle == 0 ? null : options.StartAngle,
            Sweep = options.Sweep == 360 ? null : options.Sweep,
            Cutout = options.Type == ChartType.Donut && options.Cutout != ChartRenderDefaults.DonutCutout ? options.Cutout : null,
            Width = options.Width == ChartRenderDefaults.Width ? null : options.Width,
            Height = options.Height == ChartRenderDefaults.Height ? null : options.Height,
            XAxis = ToSpec(options.XAxis),
            YAxis = ToSpec(options.YAxis),
            Y2Axis = options.Y2Axis == null ? new AxisSpec() : ToSpec(options.Y2Axis)
        };

        foreach (var series in options.Series)
        {
            spec.Series.Add(ToSpec(series));
        }

        return spec;
    }

    private static AxisSpec ToSpec(ChartAxisOptions axis) => new()
    {
        Title = axis.Title,
        Min = axis.Min,
        Max = axis.Max,
        SuggestedMin = axis.SuggestedMin,
        SuggestedMax = axis.SuggestedMax,
        Scale = axis.Scale == AxisScale.Linear ? null : axis.Scale,
        Reverse = axis.Reverse ? true : null
    };

    public static ChartSeriesSpec ToSpec(ChartSeries series)
    {
        var points = series.Points;
        return new ChartSeriesSpec
        {
            Name = series.Name,
            Color = series.Color,
            LineWidth = series.StrokeThickness == ChartRenderDefaults.LineWidth ? null : series.StrokeThickness,
            Kind = series.Kind,
            Axis = series.Axis == AxisSide.Left ? null : series.Axis,
            Stack = series.StackGroup,
            Dash = series.Dash == LineDash.Solid ? null : series.Dash,
            Interpolation = series.Interpolation == LineInterpolation.Linear ? null : series.Interpolation,
            Tension = series.Interpolation == LineInterpolation.Smooth && series.Tension != ChartRenderDefaults.Tension ? series.Tension : null,
            Step = series.Step == LineStep.None ? null : series.Step,
            Fill = series.Fill,
            FillTo = series.FillTo,
            PointStyle = series.PointStyle == PointShape.Circle ? null : series.PointStyle,
            PointRadius = series.PointRadius,
            ColorSegments = series.ColorSegments ? true : null,
            CornerRadius = series.CornerRadius,
            Sizes = points.Any(p => p.Size != null) ? points.Select(p => p.Size).ToList() : null,
            From = points.Any(p => p.From != null) ? points.Select(p => p.From).ToList() : null,
            Y = points.Select(p => Finite(p.Y)).ToList(),

            // Columns that say nothing beyond the default are left out: x when it is just the index, and so on.
            X = points.Where((p, i) => p.X != i).Any() ? points.Select(p => Finite(p.X)).ToList() : null,
            Labels = points.Any(p => !string.IsNullOrEmpty(p.Label)) ? points.Select(p => NullIfEmpty(p.Label)).ToList() : null,
            Colors = points.Any(p => p.CustomColor != null) ? points.Select(p => p.CustomColor).ToList() : null
        };
    }

    private static double? Finite(double value) => double.IsFinite(value) ? value : null;

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}

/// <summary>What a chart looks like where its spec says nothing.</summary>
public static class ChartRenderDefaults
{
    public const double Width = 560;

    /// <summary>The drawing's height; the header and legend come on top of it.</summary>
    public const double Height = 280;

    public const double LineWidth = 2.0;

    /// <summary>How round a smooth line is when its spec doesn't say.</summary>
    public const double Tension = 0.4;

    /// <summary>The share of a donut's radius left empty in the middle when its spec doesn't say.</summary>
    public const double DonutCutout = 0.55;
    public const int Bins = 10;

    /// <summary>When a spec doesn't say whether to mark each value, a series of up to this many is marked.</summary>
    public const int MaxValuesWithMarkers = 40;
}
