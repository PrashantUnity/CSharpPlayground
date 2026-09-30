using System;
using System.Collections.Generic;
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

            var baseColor = ChartPaletteService.ParseColor(seriesColor);
            var strokeBrush = new SolidColorBrush(baseColor);
            var strokePen = new Pen(strokeBrush, series.StrokeThickness > 0 ? series.StrokeThickness : 2.5);
            // A missing value is a gap: the area and the line stop at it and start again after it. A run with more values
            // than the plot has pixel columns is drawn through the few that give the same picture.
            var runs = ChartPoints.Runs(series.Points).ToList();
            var drawn = runs.Select(r => ChartPoints.ForLine(series.Points, r.Start, r.Length, plotArea.Width, x => ToCanvasX(x, minX, maxX, plotArea))).ToList();

            using (ClipToPlot(context, plotArea))
            {
                if (_isAreaMode || options.Type == ChartType.Area)
                {
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
                    double zeroBaselineY = ToCanvasY(Math.Max(minY, 0), minY, maxY, plotArea);
                    foreach (var indices in drawn)
                    {
                        context.DrawGeometry(gradientBrush, null, AreaGeometry(series, indices, zeroBaselineY, minX, maxX, minY, maxY, plotArea));
                    }
                }

                foreach (var indices in drawn.Where(d => d.Count > 1))
                {
                    context.DrawGeometry(null, strokePen, LineGeometry(series, indices, minX, maxX, minY, maxY, plotArea));
                }

                // Markers where asked for and there is room for them (values packed tighter than a marker would only
                // thicken the line), and always for a value between two gaps, which has no line to show it: a marker
                // where there is room, a dot where there isn't. A value with its own colour is marked in it.
                bool roomForMarkers = series.Points.Count * ChartPoints.MarkerSpacing <= plotArea.Width;
                if (roomForMarkers)
                {
                    var pointFill = new SolidColorBrush(Color.FromArgb(255, 15, 20, 28));
                    var pointStroke = new Pen(strokeBrush, 2.0);
                    foreach (var (start, length) in runs)
                    {
                        if (length > 1 && !options.ShowPoints) continue;
                        for (int i = start; i < start + length; i++)
                        {
                            var p = series.Points[i];
                            var stroke = string.IsNullOrEmpty(p.CustomColor) ? pointStroke : new Pen(new SolidColorBrush(ChartPaletteService.ParseColor(p.CustomColor)), 2.0);
                            context.DrawEllipse(pointFill, stroke, new Point(ToCanvasX(p.X, minX, maxX, plotArea), ToCanvasY(p.Y, minY, maxY, plotArea)), 3.5, 3.5);
                        }
                    }
                }
                else
                {
                    var isolated = runs.Where(r => r.Length == 1).Select(r => series.Points[r.Start]).ToList();
                    foreach (var i in ChartPoints.ForScatter(isolated, p => (ToCanvasX(p.X, minX, maxX, plotArea), ToCanvasY(p.Y, minY, maxY, plotArea))))
                    {
                        var p = isolated[i];
                        context.DrawEllipse(strokeBrush, null, new Point(ToCanvasX(p.X, minX, maxX, plotArea), ToCanvasY(p.Y, minY, maxY, plotArea)), 1.5, 1.5);
                    }
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

    // One run's line, through the points given (in order).
    private StreamGeometry LineGeometry(ChartSeries series, IReadOnlyList<int> indices, double minX, double maxX, double minY, double maxY, Rect plotArea)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        for (int k = 0; k < indices.Count; k++)
        {
            var p = series.Points[indices[k]];
            var point = new Point(ToCanvasX(p.X, minX, maxX, plotArea), ToCanvasY(p.Y, minY, maxY, plotArea));
            if (k == 0) ctx.BeginFigure(point, false);
            else ctx.LineTo(point);
        }

        ctx.EndFigure(false);
        return geometry;
    }

    // The area under one run of values, down to the baseline.
    private StreamGeometry AreaGeometry(ChartSeries series, IReadOnlyList<int> indices, double baselineY, double minX, double maxX, double minY, double maxY, Rect plotArea)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(ToCanvasX(series.Points[indices[0]].X, minX, maxX, plotArea), baselineY), true);
        foreach (var i in indices)
        {
            ctx.LineTo(new Point(ToCanvasX(series.Points[i].X, minX, maxX, plotArea), ToCanvasY(series.Points[i].Y, minY, maxY, plotArea)));
        }

        ctx.LineTo(new Point(ToCanvasX(series.Points[indices[^1]].X, minX, maxX, plotArea), baselineY));
        ctx.EndFigure(true);
        return geometry;
    }

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
