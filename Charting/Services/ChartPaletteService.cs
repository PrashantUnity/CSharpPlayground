using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

public static class ChartPaletteService
{
    public static readonly string DefaultCyan = "#4ec9b0";
    public static readonly string AccentAmber = "#f59e0b";
    public static readonly string AccentIndigo = "#6366f1";
    public static readonly string AccentEmerald = "#10b981";
    public static readonly string AccentRose = "#f43f5e";
    public static readonly string AccentSky = "#0ea5e9";
    public static readonly string AccentPurple = "#a855f7";

    public static readonly IReadOnlyList<string> ExpressivePalette = new[]
    {
        "#4ec9b0", // Cyan / Teal
        "#60a5fa", // Blue
        "#f59e0b", // Amber
        "#f43f5e", // Rose
        "#10b981", // Emerald
        "#a855f7", // Purple
        "#38bdf8", // Sky
        "#fb923c"  // Orange
    };

    public static IBrush GetBrush(string? colorHexOrName, double opacity = 1.0)
    {
        var color = ParseColor(colorHexOrName);
        if (opacity < 1.0)
        {
            color = Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B);
        }
        return new SolidColorBrush(color);
    }

    public static Color ParseColor(string? colorHexOrName)
    {
        if (string.IsNullOrWhiteSpace(colorHexOrName))
            return Color.Parse(DefaultCyan);

        try
        {
            if (colorHexOrName.StartsWith("#"))
                return Color.Parse(colorHexOrName);

            return Color.Parse(colorHexOrName);
        }
        catch
        {
            return Color.Parse(DefaultCyan);
        }
    }

    public static string GetSeriesColor(int seriesIndex)
    {
        return ExpressivePalette[seriesIndex % ExpressivePalette.Count];
    }
}
