using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

/// <summary>Chart colours as Avalonia brushes, and series colours from the active desktop theme (<see cref="ChartPalette"/> otherwise).</summary>
public static class ChartPaletteService
{
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
            return Color.Parse(ChartPalette.DefaultCyan);

        try
        {
            if (colorHexOrName.StartsWith("#"))
                return Color.Parse(colorHexOrName);

            return Color.Parse(colorHexOrName);
        }
        catch
        {
            return Color.Parse(ChartPalette.DefaultCyan);
        }
    }

    /// <summary>
    /// The colour of series <paramref name="seriesIndex"/> (0-based): the active theme's <c>ChartSeries{n}Brush</c> when it
    /// has chart colours (generated themes do, chosen to stay distinct with colour-vision deficiencies), else the default
    /// expressive palette.
    /// </summary>
    public static string GetSeriesColor(int seriesIndex)
    {
        if (seriesIndex < 0) seriesIndex = 0;
        var engine = PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ThemeEngine;
        if (PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ThemeTokens.TryGet(engine, $"ChartSeries{seriesIndex % ThemeSeriesCount + 1}Brush", out var themed))
        {
            return themed;
        }

        return ChartPalette.DefaultSeriesColor(seriesIndex);
    }

    private const int ThemeSeriesCount = 8;
}
