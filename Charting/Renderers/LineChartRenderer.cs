using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public class LineChartRenderer : ChartRendererBase
{
    private readonly bool _isAreaMode;

    public LineChartRenderer(bool isAreaMode = false)
    {
        _isAreaMode = isAreaMode;
    }

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
            var strokeBrush = new SolidColorBrush(baseColor);
            var strokePen = new Pen(strokeBrush, series.StrokeThickness > 0 ? series.StrokeThickness : 2.5);

            // Area Gradient Fill
            if (_isAreaMode || options.Type == ChartType.Area)
            {
                var areaGeometry = new StreamGeometry();
                using (var ctx = areaGeometry.Open())
                {
                    double firstX = ToCanvasX(series.Points[0].X, minX, maxX, plotArea);
                    double firstY = ToCanvasY(series.Points[0].Y, minY, maxY, plotArea);
                    double zeroBaselineY = ToCanvasY(Math.Max(minY, 0), minY, maxY, plotArea);

                    ctx.BeginFigure(new Point(firstX, zeroBaselineY), true);
                    ctx.LineTo(new Point(firstX, firstY));

                    for (int i = 1; i < series.Points.Count; i++)
                    {
                        double px = ToCanvasX(series.Points[i].X, minX, maxX, plotArea);
                        double py = ToCanvasY(series.Points[i].Y, minY, maxY, plotArea);
                        ctx.LineTo(new Point(px, py));
                    }

                    double lastX = ToCanvasX(series.Points[^1].X, minX, maxX, plotArea);
                    ctx.LineTo(new Point(lastX, zeroBaselineY));
                    ctx.EndFigure(true);
                }

                var gradientBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(Color.FromArgb(90, baseColor.R, baseColor.G, baseColor.B), 0.0),
                        new GradientStop(Color.FromArgb(10, baseColor.R, baseColor.G, baseColor.B), 1.0)
                    }
                };
                context.DrawGeometry(gradientBrush, null, areaGeometry);
            }

            // Line Curve
            var lineGeometry = new StreamGeometry();
            using (var ctx = lineGeometry.Open())
            {
                double firstX = ToCanvasX(series.Points[0].X, minX, maxX, plotArea);
                double firstY = ToCanvasY(series.Points[0].Y, minY, maxY, plotArea);

                ctx.BeginFigure(new Point(firstX, firstY), false);
                for (int i = 1; i < series.Points.Count; i++)
                {
                    double px = ToCanvasX(series.Points[i].X, minX, maxX, plotArea);
                    double py = ToCanvasY(series.Points[i].Y, minY, maxY, plotArea);
                    ctx.LineTo(new Point(px, py));
                }
                ctx.EndFigure(false);
            }
            context.DrawGeometry(null, strokePen, lineGeometry);

            // Point markers
            if (options.ShowPoints || series.Points.Count <= 40)
            {
                var pointFill = new SolidColorBrush(Color.FromArgb(255, 15, 20, 28));
                var pointStroke = new Pen(strokeBrush, 2.0);

                foreach (var p in series.Points)
                {
                    double px = ToCanvasX(p.X, minX, maxX, plotArea);
                    double py = ToCanvasY(p.Y, minY, maxY, plotArea);
                    context.DrawEllipse(pointFill, pointStroke, new Point(px, py), 3.5, 3.5);
                }
            }

            // Draw X-axis labels for primary series
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

                // Priority to close distance, or closest along X within horizontal threshold
                if (dist < minDistance && (dist <= 40 || Math.Abs(dx) <= 35))
                {
                    minDistance = dist;
                    closest = new ChartHitTestResult(p, series, new Point(px, py));
                }
            }
        }

        return closest;
    }
}
