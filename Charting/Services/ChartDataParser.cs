using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

public static class ChartDataParser
{
    public static ChartOptions Parse(object data, string? title = null, string? color = null, string? chartType = null)
    {
        var options = new ChartOptions
        {
            Title = title ?? "Chart",
            PrimaryColor = color ?? ChartPaletteService.DefaultCyan,
            Type = ParseChartType(chartType)
        };

        if (data == null)
        {
            return options;
        }

        if (data is ChartOptions existingOptions)
        {
            if (!string.IsNullOrEmpty(title)) existingOptions.Title = title;
            if (!string.IsNullOrEmpty(color)) existingOptions.PrimaryColor = color;
            if (!string.IsNullOrEmpty(chartType)) existingOptions.Type = ParseChartType(chartType);
            return existingOptions;
        }

        if (data is ChartSeries singleSeries)
        {
            if (!string.IsNullOrEmpty(color)) singleSeries.Color = color;
            options.Series.Add(singleSeries);
            return options;
        }

        if (data is IEnumerable<ChartSeries> multipleSeries)
        {
            options.Series.AddRange(multipleSeries);
            return options;
        }

        // Generic collection parsing
        var series = new ChartSeries
        {
            Name = title ?? "Series 1",
            Color = options.PrimaryColor
        };

        if (data is IDictionary dict)
        {
            int index = 0;
            foreach (DictionaryEntry entry in dict)
            {
                var label = entry.Key?.ToString() ?? $"Item {index}";
                if (TryConvertToDouble(entry.Value, out var y))
                {
                    series.Points.Add(new ChartDataPoint(index, y, label));
                    index++;
                }
            }
        }
        else if (data is IEnumerable enumerable)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                if (item == null) continue;

                if (TryConvertToDouble(item, out var numValue))
                {
                    series.Points.Add(new ChartDataPoint(index, numValue, index.ToString()));
                    index++;
                    continue;
                }

                // Check for KeyValuePair
                var itemType = item.GetType();
                if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    var keyProp = itemType.GetProperty("Key");
                    var valProp = itemType.GetProperty("Value");
                    var k = keyProp?.GetValue(item)?.ToString() ?? index.ToString();
                    var v = valProp?.GetValue(item);
                    if (TryConvertToDouble(v, out var valNum))
                    {
                        series.Points.Add(new ChartDataPoint(index, valNum, k));
                        index++;
                        continue;
                    }
                }

                // Check for tuple (X, Y) or (Label, Value)
                var fields = itemType.GetFields();
                if (fields.Length >= 2 && fields.Any(f => f.Name == "Item1") && fields.Any(f => f.Name == "Item2"))
                {
                    var f1 = itemType.GetField("Item1")?.GetValue(item);
                    var f2 = itemType.GetField("Item2")?.GetValue(item);

                    if (TryConvertToDouble(f1, out var xNum) && TryConvertToDouble(f2, out var yNum))
                    {
                        series.Points.Add(new ChartDataPoint(xNum, yNum));
                        index++;
                        continue;
                    }

                    if (TryConvertToDouble(f2, out var yVal))
                    {
                        var lbl = f1?.ToString() ?? index.ToString();
                        series.Points.Add(new ChartDataPoint(index, yVal, lbl));
                        index++;
                        continue;
                    }
                }

                // Check for object with properties (e.g. Value, Y, Amount, Count, Revenue, Sales)
                var props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var yProp = props.FirstOrDefault(p =>
                    p.Name.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Value", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Amount", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Total", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Score", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Revenue", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Sales", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Price", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Cost", StringComparison.OrdinalIgnoreCase));

                var xProp = props.FirstOrDefault(p =>
                    p.Name.Equals("X", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Key", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Label", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Title", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Category", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Region", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Country", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("Item", StringComparison.OrdinalIgnoreCase));

                if (yProp != null && TryConvertToDouble(yProp.GetValue(item), out var extractedY))
                {
                    var extractedX = (double)index;
                    string? label = null;

                    if (xProp != null)
                    {
                        var xVal = xProp.GetValue(item);
                        if (TryConvertToDouble(xVal, out var xNum))
                        {
                            extractedX = xNum;
                        }
                        label = xVal?.ToString();
                    }

                    series.Points.Add(new ChartDataPoint(extractedX, extractedY, label ?? index.ToString()));
                    index++;
                    continue;
                }

                // Fallback: try first numeric property as Y and first string property as Label
                var firstNumProp = yProp ?? props.FirstOrDefault(p => TryConvertToDouble(p.GetValue(item), out _));
                if (firstNumProp != null && TryConvertToDouble(firstNumProp.GetValue(item), out var firstY))
                {
                    var firstStrProp = xProp ?? props.FirstOrDefault(p => p != firstNumProp && p.PropertyType == typeof(string));
                    var label = firstStrProp?.GetValue(item)?.ToString() ?? index.ToString();
                    series.Points.Add(new ChartDataPoint(index, firstY, label));
                    index++;
                    continue;
                }
            }
        }

        options.Series.Add(series);
        return options;
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

    public static ChartOptions ParseHistogram(
        IEnumerable data,
        int binCount = 10,
        string? title = null,
        string? color = null)
    {
        var rawValues = new List<double>();
        foreach (var item in data)
        {
            if (TryConvertToDouble(item, out var val))
            {
                rawValues.Add(val);
            }
        }

        var options = new ChartOptions
        {
            Title = title ?? "Histogram",
            PrimaryColor = color ?? ChartPaletteService.DefaultCyan,
            Type = ChartType.Bar
        };

        if (rawValues.Count == 0) return options;

        double min = rawValues.Min();
        double max = rawValues.Max();
        if (Math.Abs(max - min) < 1e-6)
        {
            max = min + 1;
        }

        binCount = Math.Max(2, Math.Min(50, binCount));
        double binWidth = (max - min) / binCount;
        int[] counts = new int[binCount];

        foreach (var v in rawValues)
        {
            int bin = (int)((v - min) / binWidth);
            if (bin >= binCount) bin = binCount - 1;
            if (bin < 0) bin = 0;
            counts[bin]++;
        }

        var series = new ChartSeries
        {
            Name = "Frequency",
            Color = options.PrimaryColor
        };

        for (int i = 0; i < binCount; i++)
        {
            double binStart = min + (i * binWidth);
            double binEnd = binStart + binWidth;
            string label = $"{binStart:0.#}-{binEnd:0.#}";
            series.Points.Add(new ChartDataPoint(i, counts[i], label));
        }

        options.Series.Add(series);
        return options;
    }
}
