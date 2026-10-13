using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

/// <summary>
/// The older way to turn data into a chart's model. It reads data exactly as <see cref="ChartSpecBuilder"/> does (the
/// conventions every language follows) and gives the model the chart control draws from that spec.
/// </summary>
public static class ChartDataParser
{
    public static ChartOptions Parse(object data, string? title = null, string? color = null, string? chartType = null)
    {
        // A model given is changed in place, as it always was.
        if (data is ChartOptions existingOptions)
        {
            if (!string.IsNullOrEmpty(title)) existingOptions.Title = title;
            if (!string.IsNullOrEmpty(color)) existingOptions.PrimaryColor = color;
            if (!string.IsNullOrEmpty(chartType)) existingOptions.Type = ParseChartType(chartType);
            return existingOptions;
        }

        var spec = ChartSpecBuilder.From(data, string.IsNullOrWhiteSpace(chartType) ? null : ParseChartType(chartType));
        spec.Title = title ?? spec.Title ?? "Chart";
        if (!string.IsNullOrEmpty(color)) spec.Color = color;
        return ChartRenderModelBuilder.Build(spec);
    }

    public static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;
        if (value == null) return false;

        switch (value)
        {
            case int i: result = i; return true;
            case double d: result = double.IsNaN(d) || double.IsInfinity(d) ? 0 : d; return true;
            case float f: result = float.IsNaN(f) || float.IsInfinity(f) ? 0 : f; return true;
            case decimal m: result = (double)m; return true;
            case long l: result = l; return true;
            case short s: result = s; return true;
            case byte b: result = b; return true;
            case uint u: result = u; return true;
            case ulong ul: result = ul; return true;
            case ushort us: result = us; return true;
            case sbyte sb: result = sb; return true;
            case string str when double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                try
                {
                    result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return !double.IsNaN(result) && !double.IsInfinity(result);
                }
                catch
                {
                    return false;
                }
        }
    }

    public static ChartType ParseChartType(string? chartType)
    {
        if (string.IsNullOrWhiteSpace(chartType))
            return ChartType.Line;

        if (Enum.TryParse<ChartType>(chartType, true, out var parsed))
            return parsed;

        var lower = chartType.Trim().ToLowerInvariant();
        if (lower.Contains("bar") || lower.Contains("col")) return ChartType.Bar;
        if (lower.Contains("area")) return ChartType.Area;
        if (lower.Contains("scatter") || lower.Contains("dot") || lower.Contains("point")) return ChartType.Scatter;
        if (lower.Contains("donut")) return ChartType.Donut;
        if (lower.Contains("pie")) return ChartType.Pie;
        if (lower.Contains("hist")) return ChartType.Histogram;

        return ChartType.Line;
    }

    /// <summary>The samples counted into <paramref name="binCount"/> bars, as <see cref="ChartSpecBuilder.Histogram"/> does.</summary>
    public static ChartOptions ParseHistogram(
        IEnumerable data,
        int binCount = 10,
        string? title = null,
        string? color = null)
    {
        var spec = ChartSpecBuilder.Histogram(data, binCount);
        spec.Title = title ?? "Histogram";
        if (!string.IsNullOrEmpty(color)) spec.Color = color;
        return ChartRenderModelBuilder.Build(spec);
    }
}
