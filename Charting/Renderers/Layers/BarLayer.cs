using System;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;

/// <summary>
/// Draws a bar series: standing or lying, side by side with the other bar series (one lane each, or one for a pile),
/// stacked, floating from a start other than the baseline, with rounded outer corners. A missing value leaves its slot
/// empty; with more bars than pixels each pixel column shows its tallest.
/// </summary>
internal static class BarLayer
{
    // The share of a slot left empty round its bars.
    private const double GroupPadding = 0.2;
    private const double MaxBar = 45;
    private const double DefaultRadius = 3;

    /// <summary>The thickness of one bar along the index axis.</summary>
    public static double LaneLength(CartesianPlot plot) =>
        Math.Max(2, Math.Min(MaxBar, plot.SlotLength * (1 - GroupPadding) / plot.LaneCount));

    /// <summary>Where bar i of a series is, centred in its slot among the lanes; null when it has no value.</summary>
    public static Rect? BarRect(CartesianPlot plot, SeriesLayout layout, int i)
    {
        if (i >= layout.Series.Points.Count || !double.IsFinite(layout.Series.Points[i].Y)) return null;
        var lane = LaneLength(plot);
        var centre = plot.IndexPx(i) + (layout.Lane - (plot.LaneCount - 1) / 2.0) * lane;
        var thickness = Math.Max(1, lane - 2);

        var baseline = plot.Baseline(layout.Side);
        var foot = plot.ValuePx(layout.BaseAt(i, baseline), layout.Side);
        var end = plot.ValuePx(layout.ValueAt(i), layout.Side);
        if (!double.IsFinite(foot) || !double.IsFinite(end)) return null;

        var from = Math.Min(foot, end);
        var length = Math.Max(2, Math.Abs(end - foot));
        return plot.Horizontal
            ? new Rect(from, centre - thickness / 2, length, thickness)
            : new Rect(centre - thickness / 2, from, thickness, length);
    }

    public static void Draw(DrawingContext context, CartesianPlot plot, SeriesLayout layout)
    {
        var series = layout.Series;
        var radius = series.CornerRadius ?? (layout.IsStacked ? 0 : DefaultRadius);
        var baseline = plot.Baseline(layout.Side);

        foreach (var i in ChartPoints.ForBars(series.Points, plot.SlotLength))
        {
            if (BarRect(plot, layout, i) is not { } rect) continue;
            var point = series.Points[i];
            var colorText = !string.IsNullOrEmpty(point.CustomColor)
                ? point.CustomColor
                : (!string.IsNullOrEmpty(series.Color) ? series.Color : ChartPaletteService.GetSeriesColor(layout.Index));
            var brush = new SolidColorBrush(ChartPaletteService.ParseColor(colorText));

            // Outer corners are the ones away from the baseline: the top of a bar that grows up, the right of one that grows right.
            var growsPositive = layout.ValueAt(i) >= layout.BaseAt(i, baseline);
            // A floating bar has no baseline to stand on, so both its ends are rounded.
            var shape = point.From != null && radius > 0
                ? new RoundedRect(rect, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2))
                : Rounded(rect, radius, plot.Horizontal, growsPositive != plot.ValueAxis(layout.Side).Reverse);
            context.DrawRectangle(brush, null, shape);
        }
    }

    // A bar's rectangle with its far end rounded.
    private static RoundedRect Rounded(Rect rect, double radius, bool horizontal, bool farIsAwayFromOrigin)
    {
        var r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        if (r <= 0) return new RoundedRect(rect);
        if (horizontal) return farIsAwayFromOrigin ? new RoundedRect(rect, 0, r, r, 0) : new RoundedRect(rect, r, 0, 0, r);
        return farIsAwayFromOrigin ? new RoundedRect(rect, r, r, 0, 0) : new RoundedRect(rect, 0, 0, r, r);
    }
}
