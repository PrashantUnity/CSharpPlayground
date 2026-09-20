using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

public class ChartDataPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? CustomColor { get; set; }
    public object? Tag { get; set; }

    public ChartDataPoint() { }

    public ChartDataPoint(double y, string? label = null)
    {
        Y = y;
        Label = label ?? string.Empty;
    }

    public ChartDataPoint(double x, double y, string? label = null)
    {
        X = x;
        Y = y;
        Label = label ?? string.Empty;
    }

    public string GetFormattedY()
    {
        if (Math.Abs(Y) >= 1_000_000)
            return $"{Y / 1_000_000.0:0.##}M";
        if (Math.Abs(Y) >= 1_000)
            return $"{Y / 1_000.0:0.##}k";
        if (Math.Abs(Y) < 0.01 && Y != 0)
            return $"{Y:0.####}";
        return Math.Abs(Y - Math.Round(Y)) < 1e-6 ? $"{Y:N0}" : $"{Y:0.##}";
    }
}
