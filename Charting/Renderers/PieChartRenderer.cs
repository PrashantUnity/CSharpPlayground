using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public class PieChartRenderer : ChartRendererBase
{
    private readonly bool _isDonut;

    public PieChartRenderer(bool isDonut = false)
    {
        _isDonut = isDonut;
    }

    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        var points = options.Series.SelectMany(s => s.Points).Where(p => p.Y > 0).ToList();
        if (points.Count == 0) return;

        double total = points.Sum(p => p.Y);
        if (total <= 0) return;

        // Reserve space for legend on right
        double legendWidth = Math.Min(160, bounds.Width * 0.35);
        var chartRect = new Rect(bounds.Left + 10, bounds.Top + 10, Math.Max(50, bounds.Width - legendWidth - 20), Math.Max(50, bounds.Height - 20));

        var center = new Point(chartRect.Center.X, chartRect.Center.Y);
        double outerRadius = Math.Min(chartRect.Width, chartRect.Height) * 0.44;
        double innerRadius = (_isDonut || options.Type == ChartType.Donut) ? outerRadius * 0.55 : 0;

        double currentAngle = -Math.PI / 2; // Start at 12 o'clock

        var slicePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 20, 24, 33)), 1.5);

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            double sliceAngle = (p.Y / total) * 2 * Math.PI;
            double nextAngle = currentAngle + sliceAngle;

            var colorStr = !string.IsNullOrEmpty(p.CustomColor) ? p.CustomColor : ChartPaletteService.GetSeriesColor(i);
            var color = ChartPaletteService.ParseColor(colorStr);
            var brush = new SolidColorBrush(color);

            var sliceGeometry = CreateSliceGeometry(center, innerRadius, outerRadius, currentAngle, nextAngle);
            context.DrawGeometry(brush, slicePen, sliceGeometry);

            currentAngle = nextAngle;
        }

        // Draw Legend on the right
        double legendX = chartRect.Right + 15;
        double legendY = bounds.Top + 20;

        for (int i = 0; i < Math.Min(points.Count, 10); i++)
        {
            var p = points[i];
            var colorStr = !string.IsNullOrEmpty(p.CustomColor) ? p.CustomColor : ChartPaletteService.GetSeriesColor(i);
            var color = ChartPaletteService.ParseColor(colorStr);
            var brush = new SolidColorBrush(color);

            double pct = (p.Y / total) * 100.0;
            var label = string.IsNullOrEmpty(p.Label) ? $"Item {i + 1}" : p.Label;
            if (label.Length > 12) label = label.Substring(0, 10) + "..";
            var text = $"{label} ({pct:0.0}%)";

            // Legend color pill
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(legendX, legendY + 2, 10, 10), 2, 2));

            // Legend text
            var ft = CreateFormattedText(text, 10, TextBrush);
            context.DrawText(ft, new Point(legendX + 16, legendY));

            legendY += 18;
            if (legendY > bounds.Bottom - 18) break;
        }
    }

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        var points = options.Series.SelectMany(s => s.Points).Where(p => p.Y > 0).ToList();
        if (points.Count == 0) return null;

        double total = points.Sum(p => p.Y);
        if (total <= 0) return null;

        double legendWidth = Math.Min(160, bounds.Width * 0.35);
        var chartRect = new Rect(bounds.Left + 10, bounds.Top + 10, Math.Max(50, bounds.Width - legendWidth - 20), Math.Max(50, bounds.Height - 20));

        var center = new Point(chartRect.Center.X, chartRect.Center.Y);
        double outerRadius = Math.Min(chartRect.Width, chartRect.Height) * 0.44;
        double innerRadius = (_isDonut || options.Type == ChartType.Donut) ? outerRadius * 0.55 : 0;

        double dx = pointerPosition.X - center.X;
        double dy = pointerPosition.Y - center.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < innerRadius || distance > outerRadius) return null;

        // Angle from center in radians (-PI to PI, starting from positive X-axis)
        double angle = Math.Atan2(dy, dx);
        // Normalize angle to start at 12 o'clock (-PI/2) and range [0, 2*PI)
        double normalizedAngle = angle + (Math.PI / 2.0);
        if (normalizedAngle < 0) normalizedAngle += 2 * Math.PI;

        double accumulatedAngle = 0;
        var parentSeries = options.Series.FirstOrDefault() ?? new ChartSeries();

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            double sliceAngle = (p.Y / total) * 2 * Math.PI;

            if (normalizedAngle >= accumulatedAngle && normalizedAngle < accumulatedAngle + sliceAngle)
            {
                double midAngle = (accumulatedAngle + sliceAngle / 2.0) - (Math.PI / 2.0);
                double markerDist = (innerRadius + outerRadius) / 2.0;
                var markerPos = new Point(center.X + Math.Cos(midAngle) * markerDist, center.Y + Math.Sin(midAngle) * markerDist);

                return new ChartHitTestResult(p, parentSeries, markerPos);
            }

            accumulatedAngle += sliceAngle;
        }

        return null;
    }

    private StreamGeometry CreateSliceGeometry(Point center, double innerR, double outerR, double startAngle, double endAngle)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        int segments = Math.Max(6, (int)(Math.Abs(endAngle - startAngle) / (Math.PI / 36.0)));

        // Outer arc start
        double xOuterStart = center.X + Math.Cos(startAngle) * outerR;
        double yOuterStart = center.Y + Math.Sin(startAngle) * outerR;

        ctx.BeginFigure(new Point(xOuterStart, yOuterStart), true);

        // Follow outer arc
        for (int step = 1; step <= segments; step++)
        {
            double a = startAngle + (endAngle - startAngle) * (step / (double)segments);
            ctx.LineTo(new Point(center.X + Math.Cos(a) * outerR, center.Y + Math.Sin(a) * outerR));
        }

        if (innerR > 1e-3)
        {
            // Connect to inner arc end
            ctx.LineTo(new Point(center.X + Math.Cos(endAngle) * innerR, center.Y + Math.Sin(endAngle) * innerR));

            // Follow inner arc back to start
            for (int step = segments - 1; step >= 0; step--)
            {
                double a = startAngle + (endAngle - startAngle) * (step / (double)segments);
                ctx.LineTo(new Point(center.X + Math.Cos(a) * innerR, center.Y + Math.Sin(a) * innerR));
            }
        }
        else
        {
            // Connect to center
            ctx.LineTo(center);
        }

        ctx.EndFigure(true);
        return geometry;
    }
}
