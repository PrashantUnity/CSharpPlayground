using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

public class Point3D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? CustomColor { get; set; }
    public double Size { get; set; } = 1.0;
    public object? Tag { get; set; }

    public Point3D() { }

    public Point3D(double x, double y, double z, string? label = null, string? color = null, double size = 1.0)
    {
        X = x;
        Y = y;
        Z = z;
        Label = label ?? string.Empty;
        CustomColor = color;
        Size = size;
    }

    public string GetFormattedCoordinates()
    {
        return $"({FormatValue(X)}, {FormatValue(Y)}, {FormatValue(Z)})";
    }

    private static string FormatValue(double val)
    {
        if (Math.Abs(val) >= 1_000_000) return $"{val / 1_000_000.0:0.##}M";
        if (Math.Abs(val) >= 1_000) return $"{val / 1_000.0:0.##}k";
        if (Math.Abs(val) < 0.01 && val != 0) return $"{val:0.####}";
        return Math.Abs(val - Math.Round(val)) < 1e-6 ? $"{val:N0}" : $"{val:0.##}";
    }
}
