using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public class BarChartRenderer : ChartRendererBase
{
    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        var plotArea = GetPlotArea(bounds, options);

        // Bars grow from zero, so zero is always on the axis: above bars of negative values too.
        var range = ChartDataRange.Of(options, zeroBaseline: true);
        var (minX, maxX, minY, maxY, _) = range;

        DrawGridAndAxes(context, plotArea, range, options.ShowGrid);
        DrawAxisTitles(context, bounds, plotArea, options);

        var allSeries = options.Series.Where(s => s.Points.Count > 0).ToList();
        if (allSeries.Count == 0) return;

        int categoryCount = allSeries.Max(s => s.Points.Count);
        if (categoryCount == 0) return;

        double slotWidth = plotArea.Width / categoryCount;
        double groupPadding = slotWidth * 0.2;
        double usableGroupWidth = slotWidth - groupPadding;
        double singleBarWidth = Math.Max(2, Math.Min(45, usableGroupWidth / allSeries.Count));

        double zeroY = ToCanvasY(Math.Clamp(0, minY, maxY), minY, maxY, plotArea);
        using (ClipToPlot(context, plotArea))
        {
            for (int seriesIdx = 0; seriesIdx < allSeries.Count; seriesIdx++)
            {
                // A missing value leaves its place empty; with more bars than pixels, a column shows its tallest.
                var series = allSeries[seriesIdx];
                foreach (var catIdx in ChartPoints.ForBars(series.Points, slotWidth))
                {
                    var point = series.Points[catIdx];
                    var colorStr = !string.IsNullOrEmpty(point.CustomColor)
                        ? point.CustomColor
                        : (!string.IsNullOrEmpty(series.Color) ? series.Color : ChartPaletteService.GetSeriesColor(seriesIdx));

                    var baseColor = ChartPaletteService.ParseColor(colorStr);
                    var barBrush = new SolidColorBrush(baseColor);

                    double slotLeft = plotArea.Left + catIdx * slotWidth + (groupPadding / 2.0);
                    double barX = slotLeft + (seriesIdx * singleBarWidth);
                    double targetY = ToCanvasY(point.Y, minY, maxY, plotArea);

                    double barTop = Math.Min(zeroY, targetY);
                    double barHeight = Math.Max(2, Math.Abs(targetY - zeroY));

                    var barRect = new Rect(barX, barTop, Math.Max(1, singleBarWidth - 2), barHeight);
                    var rounded = new RoundedRect(barRect, 3, 3, 0, 0);
                    context.DrawRectangle(barBrush, null, rounded);
                }
            }
        }

        // Draw X-axis labels using first series points
        var primarySeries = allSeries[0];
        int step = Math.Max(1, (int)Math.Ceiling(primarySeries.Points.Count / 10.0));
        for (int i = 0; i < primarySeries.Points.Count; i += step)
        {
            var p = primarySeries.Points[i];
            double slotCenter = plotArea.Left + i * slotWidth + (slotWidth / 2.0);
            var label = string.IsNullOrEmpty(p.Label) ? $"{p.X:0.##}" : p.Label;
            if (label.Length > 12) label = label.Substring(0, 10) + "..";

            var ft = CreateFormattedText(label, 9.5, TextBrush);
            context.DrawText(ft, new Point(slotCenter - ft.Width / 2, plotArea.Bottom + 6));
        }
    }

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        var plotArea = GetPlotArea(bounds, options);
        if (!plotArea.Contains(pointerPosition)) return null;

        var allSeries = options.Series.Where(s => s.Points.Count > 0).ToList();
        if (allSeries.Count == 0) return null;

        int categoryCount = allSeries.Max(s => s.Points.Count);
        if (categoryCount == 0) return null;

        double slotWidth = plotArea.Width / categoryCount;
        int targetCat = (int)((pointerPosition.X - plotArea.Left) / slotWidth);
        if (targetCat < 0 || targetCat >= categoryCount) return null;

        GetDataBounds(options, out _, out _, out var minY, out var maxY, zeroBaseline: true);

        double groupPadding = slotWidth * 0.2;
        double usableGroupWidth = slotWidth - groupPadding;
        double singleBarWidth = Math.Max(2, Math.Min(45, usableGroupWidth / allSeries.Count));
        double slotLeft = plotArea.Left + targetCat * slotWidth + (groupPadding / 2.0);

        for (int sIdx = 0; sIdx < allSeries.Count; sIdx++)
        {
            var series = allSeries[sIdx];
            if (targetCat >= series.Points.Count) continue;

            double barX = slotLeft + (sIdx * singleBarWidth);
            if (pointerPosition.X >= barX && pointerPosition.X <= barX + singleBarWidth)
            {
                var point = series.Points[targetCat];
                if (!double.IsFinite(point.Y)) return null; // a gap has no bar to point at
                double targetY = ToCanvasY(point.Y, minY, maxY, plotArea);
                return new ChartHitTestResult(point, series, new Point(barX + singleBarWidth / 2, targetY));
            }
        }

        // Fallback: Return primary series point in this category
        if (targetCat >= allSeries[0].Points.Count || !double.IsFinite(allSeries[0].Points[targetCat].Y)) return null;
        var fallbackPoint = allSeries[0].Points[targetCat];
        double fallbackY = ToCanvasY(fallbackPoint.Y, minY, maxY, plotArea);
        return new ChartHitTestResult(fallbackPoint, allSeries[0], new Point(slotLeft + singleBarWidth / 2, fallbackY));
    }
}
