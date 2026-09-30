using System.Globalization;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>
/// Turns a chart spec into what the chart control draws: fills in the defaults, bins a histogram, and draws at most
/// <see cref="VisualLimits.MaxChartValues"/> values, saying so when there are more. The same spec gives the same
/// chart whichever language wrote it.
/// </summary>
public static class ChartRenderModelBuilder
{
    public static ChartOptions Build(ChartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var options = new ChartOptions
        {
            Title = spec.Title ?? string.Empty,
            Subtitle = spec.Subtitle ?? string.Empty,
            // A histogram's bins are counted here; what is drawn is bars of the counts.
            Type = spec.Kind == ChartType.Histogram ? ChartType.Bar : spec.Kind,
            PrimaryColor = spec.Color ?? ChartPaletteService.DefaultCyan,
            ShowGrid = spec.Grid ?? spec.Kind is not (ChartType.Pie or ChartType.Donut),
            ShowPoints = spec.ShowPoints ?? spec.Series.All(s => s.Y.Count <= ChartRenderDefaults.MaxValuesWithMarkers),
            ShowStats = spec.ShowStats ?? true,
            ShowLegend = spec.Legend.Show ?? spec.Series.Count > 1,
            Width = spec.Width ?? ChartRenderDefaults.Width,
            Height = spec.Height ?? ChartRenderDefaults.Height,
            XAxisTitle = spec.XAxis.Title,
            YAxisTitle = spec.YAxis.Title,
            XMin = spec.XAxis.Min,
            XMax = spec.XAxis.Max,
            YMin = spec.YAxis.Min,
            YMax = spec.YAxis.Max
        };

        if (spec.Kind == ChartType.Histogram) AddHistogram(options, spec);
        else AddSeries(options, spec);
        return options;
    }

    private static string SeriesColor(ChartSeriesSpec series, int index, ChartOptions options) =>
        series.Color ?? (index == 0 ? options.PrimaryColor : ChartPaletteService.GetSeriesColor(index));

    private static void AddSeries(ChartOptions options, ChartSpec spec)
    {
        var total = spec.Series.Sum(s => s.Y.Count);
        var room = VisualLimits.MaxChartValues;
        for (var i = 0; i < spec.Series.Count; i++)
        {
            var source = spec.Series[i];
            var series = new ChartSeries
            {
                Name = source.Name ?? $"Series {i + 1}",
                Color = SeriesColor(source, i, options),
                StrokeThickness = source.LineWidth ?? ChartRenderDefaults.LineWidth
            };

            var count = Math.Min(source.Y.Count, Math.Max(0, room));
            room -= count;
            for (var j = 0; j < count; j++)
            {
                // Without x, values sit at their index; a missing x or y (null) is a gap, drawn as nothing.
                var x = source.X is { } xs ? xs[j] ?? double.NaN : j;
                series.Points.Add(new ChartDataPoint(x, source.Y[j] ?? double.NaN, source.Labels?[j])
                {
                    CustomColor = source.Colors?[j]
                });
            }

            options.Series.Add(series);
        }

        if (total > VisualLimits.MaxChartValues) options.Notice = Notices.ShowingFirst(VisualLimits.MaxChartValues, total, "values");
    }

    // Every series is counted into the same bins, spread over the range of all of them, so their bars line up.
    private static void AddHistogram(ChartOptions options, ChartSpec spec)
    {
        var samples = spec.Series.Select(s => (s.Values ?? []).Where(v => v is { } d && double.IsFinite(d)).Select(v => v!.Value).ToList()).ToList();
        var all = samples.SelectMany(s => s).ToList();
        var bins = Math.Clamp(spec.Bins ?? ChartRenderDefaults.Bins, 1, ChartSpecRules.MaxBins);
        var min = all.Count > 0 ? all.Min() : 0;
        var max = all.Count > 0 ? all.Max() : 1;
        if (max - min < 1e-9) max = min + 1;
        var width = (max - min) / bins;

        for (var i = 0; i < spec.Series.Count; i++)
        {
            var counts = new int[bins];
            foreach (var value in samples[i])
            {
                counts[Math.Clamp((int)((value - min) / width), 0, bins - 1)]++;
            }

            var series = new ChartSeries
            {
                Name = spec.Series[i].Name ?? "Frequency",
                Color = SeriesColor(spec.Series[i], i, options),
                StrokeThickness = spec.Series[i].LineWidth ?? ChartRenderDefaults.LineWidth
            };
            for (var b = 0; b < bins; b++)
            {
                var from = min + b * width;
                var label = string.Create(CultureInfo.InvariantCulture, $"{from:0.#}-{from + width:0.#}");
                series.Points.Add(new ChartDataPoint(b, counts[b], label));
            }

            options.Series.Add(series);
        }
    }
}
