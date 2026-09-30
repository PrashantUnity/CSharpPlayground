using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public class ScatterChartRenderer : ChartRendererBase
{
    // Past this many points the glow rings only blur the cloud together.
    private const int MaxGlowingPoints = 2000;

    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        var plotArea = GetPlotArea(bounds, options);
        var range = ChartDataRange.Of(options);
        var (minX, maxX, minY, maxY, _) = range;

        DrawGridAndAxes(context, plotArea, range, options.ShowGrid);
        DrawAxisTitles(context, bounds, plotArea, options);

        int seriesIndex = 0;
        foreach (var series in options.Series)
        {
            if (series.Points.Count == 0) continue;

            var seriesColor = string.IsNullOrEmpty(series.Color)
                ? ChartPaletteService.GetSeriesColor(seriesIndex)
                : series.Color;

            var (markerFill, markerStroke, glowStroke) = Marker(ChartPaletteService.ParseColor(seriesColor));

            using (ClipToPlot(context, plotArea))
            {
                // Points on top of each other are drawn once, so a scatter of any size costs at most a marker a cell.
                var drawn = ChartPoints.ForScatter(series.Points, p => (ToCanvasX(p.X, minX, maxX, plotArea), ToCanvasY(p.Y, minY, maxY, plotArea)));
                bool glowing = drawn.Count <= MaxGlowingPoints;
                foreach (var i in drawn)
                {
                    var p = series.Points[i];
                    double px = ToCanvasX(p.X, minX, maxX, plotArea);
                    double py = ToCanvasY(p.Y, minY, maxY, plotArea);

                    // Glow ring + center circle, in the point's own colour when it has one
                    var (fill, stroke, glow) = string.IsNullOrEmpty(p.CustomColor) ? (markerFill, markerStroke, glowStroke) : Marker(ChartPaletteService.ParseColor(p.CustomColor));
                    if (glowing) context.DrawEllipse(null, glow, new Point(px, py), 6, 6);
                    context.DrawEllipse(fill, stroke, new Point(px, py), 4, 4);
                }
            }

            if (seriesIndex == 0)
            {
                DrawXAxisLabels(context, plotArea, series.Points, minX, maxX);
            }

            seriesIndex++;
        }
    }

    private static (IBrush Fill, IPen Stroke, IPen Glow) Marker(Color color) => (
        new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
        new Pen(new SolidColorBrush(color), 1.5),
        new Pen(new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)), 3));

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        var plotArea = GetPlotArea(bounds, options);
        if (!plotArea.Contains(pointerPosition)) return null;

        GetDataBounds(options, out var minX, out var maxX, out var minY, out var maxY);

        ChartHitTestResult? closest = null;
        double minDistance = double.MaxValue;

        foreach (var series in options.Series)
        {
            foreach (var p in series.Points)
            {
                if (!ChartPoints.IsDrawn(p)) continue;
                double px = ToCanvasX(p.X, minX, maxX, plotArea);
                double py = ToCanvasY(p.Y, minY, maxY, plotArea);

                double dx = pointerPosition.X - px;
                double dy = pointerPosition.Y - py;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < minDistance && dist <= 25)
                {
                    minDistance = dist;
                    closest = new ChartHitTestResult(p, series, new Point(px, py));
                }
            }
        }

        return closest;
    }
}
