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
            Legend = { Show = options.ShowLegend ? true : null },
            Width = options.Width == ChartRenderDefaults.Width ? null : options.Width,
            Height = options.Height == ChartRenderDefaults.Height ? null : options.Height,
            XAxis = { Title = options.XAxisTitle, Min = options.XMin, Max = options.XMax },
            YAxis = { Title = options.YAxisTitle, Min = options.YMin, Max = options.YMax }
        };

        foreach (var series in options.Series)
        {
            spec.Series.Add(ToSpec(series));
        }

        return spec;
    }

    public static ChartSeriesSpec ToSpec(ChartSeries series)
    {
        var points = series.Points;
        return new ChartSeriesSpec
        {
            Name = series.Name,
            Color = series.Color,
            LineWidth = series.StrokeThickness == ChartRenderDefaults.LineWidth ? null : series.StrokeThickness,
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
    public const int Bins = 10;

    /// <summary>When a spec doesn't say whether to mark each value, a series of up to this many is marked.</summary>
    public const int MaxValuesWithMarkers = 40;
}
