using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

/// <summary>A chart's values summed up for its header: over every series, counting only values that are there.</summary>
internal readonly record struct ChartStatistics(double Min, double Max, double Average, int Count, int Missing)
{
    /// <summary>The statistics of every value of every series; null when the chart has none.</summary>
    public static ChartStatistics? Of(ChartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        double min = double.PositiveInfinity, max = double.NegativeInfinity, sum = 0;
        int count = 0, missing = 0;
        foreach (var series in options.Series)
        {
            foreach (var point in series.Points)
            {
                if (!double.IsFinite(point.Y))
                {
                    missing++;
                    continue;
                }

                min = Math.Min(min, point.Y);
                max = Math.Max(max, point.Y);
                sum += point.Y;
                count++;
            }
        }

        if (count == 0 && missing == 0) return null;
        return count == 0
            ? new ChartStatistics(double.NaN, double.NaN, double.NaN, 0, missing)
            : new ChartStatistics(min, max, sum / count, count, missing);
    }

    /// <summary>"Min: 1 • Max: 9 • Avg: 4.67 • N: 3" (numbers as the axes write them), and how many values are missing when some are.</summary>
    public override string ToString()
    {
        var text = Count == 0 ? "N: 0" : $"Min: {Min:0.##} • Max: {Max:0.##} • Avg: {Average:0.##} • N: {Count}";
        return Missing == 0 ? text : $"{text} • {Missing} missing";
    }
}
