using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;

/// <summary>Draws a scatter series (a marker for each value) or a bubble series (a circle as big as its size).</summary>
internal static class PointLayer
{
    // Past this many points the glow rings only blur the cloud together.
    private const int MaxGlowingPoints = 2000;
    public const double DefaultBubble = 6;

    public static void DrawScatter(DrawingContext context, CartesianPlot plot, SeriesLayout layout)
    {
        var series = layout.Series;
        var colorText = string.IsNullOrEmpty(series.Color) ? ChartPaletteService.GetSeriesColor(layout.Index) : series.Color;
        var (markerFill, markerStroke, glowStroke) = Marker(ChartPaletteService.ParseColor(colorText));
        var radius = series.PointRadius ?? 4;

        // Points on top of each other are drawn once, so a scatter of any size costs at most a marker a cell.
        var drawn = ChartPoints.ForScatterAt(series.Points, i => Pixel(plot, layout, i));
        var glowing = drawn.Count <= MaxGlowingPoints && series.PointStyle == PointShape.Circle;
        foreach (var i in drawn)
        {
            var p = series.Points[i];
            var at = plot.At(plot.IndexValue(layout, i), p.Y, layout.Side);

            // Glow ring + marker, in the point's own colour when it has one
            var (fill, stroke, glow) = string.IsNullOrEmpty(p.CustomColor) ? (markerFill, markerStroke, glowStroke) : Marker(ChartPaletteService.ParseColor(p.CustomColor));
            if (glowing) context.DrawEllipse(null, glow, at, radius + 2, radius + 2);
            MarkerShapes.Draw(context, series.PointStyle, at, radius, fill, stroke);
        }
    }

    // Past this many bubbles they are thinned to one in each cell the size of the bubble, so a bubble chart of any size
    // costs about the plot's area over a bubble's, not its number of values.
    private const int MaxExactBubbles = 2000;

    public static void DrawBubbles(DrawingContext context, CartesianPlot plot, SeriesLayout layout)
    {
        var series = layout.Series;
        var colorText = string.IsNullOrEmpty(series.Color) ? ChartPaletteService.GetSeriesColor(layout.Index) : series.Color;
        var drawn = Bubbles(plot, layout);
        var brushes = new Dictionary<string, (IBrush Fill, IPen Edge)>();
        foreach (var i in drawn)
        {
            var p = series.Points[i];
            var key = string.IsNullOrEmpty(p.CustomColor) ? colorText : p.CustomColor;
            if (!brushes.TryGetValue(key, out var look))
            {
                var color = ChartPaletteService.ParseColor(key);
                look = (new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B)), new Pen(new SolidColorBrush(color), 1.5));
                brushes[key] = look;
            }

            var at = plot.At(plot.IndexValue(layout, i), p.Y, layout.Side);
            var radius = Radius(series, i);
            context.DrawEllipse(look.Fill, look.Edge, at, radius, radius);
        }
    }

    // The bubbles drawn, the biggest first so the small ones show on top of them.
    internal static List<int> Bubbles(CartesianPlot plot, SeriesLayout layout)
    {
        var series = layout.Series;
        var all = new List<int>();
        for (var i = 0; i < series.Points.Count; i++)
        {
            if (layout.IsDrawn(i)) all.Add(i);
        }

        if (all.Count > MaxExactBubbles)
        {
            // The biggest of the bubbles in a cell is the one kept; cells grow with the bubble's size class. A bubble with
            // none of it inside the plot (the view is zoomed in) is not drawn at all.
            var kept = new Dictionary<long, int>();
            var area = plot.Area;
            foreach (var i in all)
            {
                var radius = Radius(series, i);
                var at = plot.At(plot.IndexValue(layout, i), series.Points[i].Y, layout.Side);
                if (at.X + radius < area.Left || at.X - radius > area.Right || at.Y + radius < area.Top || at.Y - radius > area.Bottom) continue;

                var (cell, size) = radius <= 8 ? (8, 0) : radius <= 16 ? (16, 1) : radius <= 32 ? (32, 2) : (64, 3);
                var key = (((long)Math.Floor((at.X - area.Left) / cell) + 1024) << 34) | (((long)Math.Floor((at.Y - area.Top) / cell) + 1024) << 2) | (long)size;
                if (!kept.TryGetValue(key, out var held) || Radius(series, held) < radius) kept[key] = i;
            }

            all = [.. kept.Values.Order()];
        }

        all.Sort((a, b) => Radius(series, b).CompareTo(Radius(series, a)) is var c and not 0 ? c : a.CompareTo(b));
        return all;
    }

    public static double Radius(ChartSeries series, int i) => Math.Max(1, series.Points[i].Size ?? series.PointRadius ?? DefaultBubble);

    private static (double X, double Y) Pixel(CartesianPlot plot, SeriesLayout layout, int i)
    {
        var at = plot.At(plot.IndexValue(layout, i), layout.Series.Points[i].Y, layout.Side);
        return (at.X, at.Y);
    }

    private static (IBrush Fill, IPen Stroke, IPen Glow) Marker(Color color) => (
        new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
        new Pen(new SolidColorBrush(color), 1.5),
        new Pen(new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)), 3));
}
