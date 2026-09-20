using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public class ScatterChartRenderer : ChartRendererBase
{
    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        var plotArea = GetPlotArea(bounds);
        GetDataBounds(options, out var minX, out var maxX, out var minY, out var maxY);

        DrawGridAndAxes(context, plotArea, minX, maxX, minY, maxY, options.ShowGrid);

        int seriesIndex = 0;
        foreach (var series in options.Series)
        {
            if (series.Points.Count == 0) continue;

            var seriesColor = string.IsNullOrEmpty(series.Color)
                ? ChartPaletteService.GetSeriesColor(seriesIndex)
                : series.Color;

            var baseColor = ChartPaletteService.ParseColor(seriesColor);
            var markerFill = new SolidColorBrush(Color.FromArgb(200, baseColor.R, baseColor.G, baseColor.B));
            var markerStroke = new Pen(new SolidColorBrush(baseColor), 1.5);
            var glowStroke = new Pen(new SolidColorBrush(Color.FromArgb(40, baseColor.R, baseColor.G, baseColor.B)), 3);

            foreach (var p in series.Points)
            {
                double px = ToCanvasX(p.X, minX, maxX, plotArea);
                double py = ToCanvasY(p.Y, minY, maxY, plotArea);

                // Glow ring + center circle
                context.DrawEllipse(null, glowStroke, new Point(px, py), 6, 6);
                context.DrawEllipse(markerFill, markerStroke, new Point(px, py), 4, 4);
            }

            if (seriesIndex == 0)
            {
                DrawXAxisLabels(context, plotArea, series.Points, minX, maxX);
            }

            seriesIndex++;
        }
    }

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        var plotArea = GetPlotArea(bounds);
        if (!plotArea.Contains(pointerPosition)) return null;

        GetDataBounds(options, out var minX, out var maxX, out var minY, out var maxY);

        ChartHitTestResult? closest = null;
        double minDistance = double.MaxValue;

        foreach (var series in options.Series)
        {
            foreach (var p in series.Points)
            {
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
