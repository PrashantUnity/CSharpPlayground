using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public abstract class ChartRendererBase : IChartRenderer
{
    protected static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
    protected static readonly IPen AxisPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1);
    protected static readonly IBrush TextBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
    protected static readonly Typeface DefaultTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

    public abstract void Render(DrawingContext context, Rect bounds, ChartOptions options);
    public abstract ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options);

    protected Rect GetPlotArea(Rect bounds)
    {
        double leftPadding = 55;
        double rightPadding = 20;
        double topPadding = 20;
        double bottomPadding = 30;

        return new Rect(
            bounds.Left + leftPadding,
            bounds.Top + topPadding,
            Math.Max(10, bounds.Width - leftPadding - rightPadding),
            Math.Max(10, bounds.Height - topPadding - bottomPadding));
    }

    protected void GetDataBounds(
        ChartOptions options,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY)
    {
        minX = 0;
        maxX = 1;
        minY = 0;
        maxY = 1;

        var allPoints = options.Series.SelectMany(s => s.Points).ToList();
        if (allPoints.Count == 0) return;

        minX = allPoints.Min(p => p.X);
        maxX = allPoints.Max(p => p.X);
        minY = allPoints.Min(p => p.Y);
        maxY = allPoints.Max(p => p.Y);

        if (Math.Abs(maxX - minX) < 1e-6)
        {
            minX -= 1;
            maxX += 1;
        }

        if (Math.Abs(maxY - minY) < 1e-6)
        {
            if (Math.Abs(minY) < 1e-6)
            {
                minY = 0;
                maxY = 1;
            }
            else
            {
                minY *= 0.8;
                maxY *= 1.2;
            }
        }
        else
        {
            // Give 8% margin on top and bottom
            double range = maxY - minY;
            if (minY >= 0)
            {
                minY = 0; // Baseline at 0 for positive charts
                maxY += range * 0.08;
            }
            else
            {
                minY -= range * 0.05;
                maxY += range * 0.08;
            }
        }
    }

    protected double ToCanvasX(double dataX, double minX, double maxX, Rect plotArea)
    {
        double normalized = (dataX - minX) / (maxX - minX);
        return plotArea.Left + normalized * plotArea.Width;
    }

    protected double ToCanvasY(double dataY, double minY, double maxY, Rect plotArea)
    {
        double normalized = (dataY - minY) / (maxY - minY);
        return plotArea.Bottom - normalized * plotArea.Height;
    }

    protected void DrawGridAndAxes(
        DrawingContext context,
        Rect plotArea,
        double minX,
        double maxX,
        double minY,
        double maxY,
        bool showGrid)
    {
        // 1. Draw horizontal grid lines and Y-axis labels
        int yTicks = 4;
        for (int i = 0; i <= yTicks; i++)
        {
            double val = minY + (maxY - minY) * (i / (double)yTicks);
            double y = ToCanvasY(val, minY, maxY, plotArea);

            if (showGrid)
            {
                context.DrawLine(GridPen, new Point(plotArea.Left, y), new Point(plotArea.Right, y));
            }

            var label = FormatTickValue(val);
            var ft = CreateFormattedText(label, 10, TextBrush);
            context.DrawText(ft, new Point(plotArea.Left - ft.Width - 8, y - ft.Height / 2));
        }

        // 2. Draw zero baseline if data crosses zero
        if (minY < 0 && maxY > 0)
        {
            double zeroY = ToCanvasY(0, minY, maxY, plotArea);
            context.DrawLine(AxisPen, new Point(plotArea.Left, zeroY), new Point(plotArea.Right, zeroY));
        }

        // 3. Draw bounding axis lines
        context.DrawLine(AxisPen, new Point(plotArea.Left, plotArea.Top), new Point(plotArea.Left, plotArea.Bottom));
        context.DrawLine(AxisPen, new Point(plotArea.Left, plotArea.Bottom), new Point(plotArea.Right, plotArea.Bottom));
    }

    protected void DrawXAxisLabels(
        DrawingContext context,
        Rect plotArea,
        IReadOnlyList<ChartDataPoint> points,
        double minX,
        double maxX)
    {
        if (points.Count == 0) return;

        int step = Math.Max(1, (int)Math.Ceiling(points.Count / 8.0));
        for (int i = 0; i < points.Count; i += step)
        {
            var p = points[i];
            double x = ToCanvasX(p.X, minX, maxX, plotArea);
            var label = string.IsNullOrEmpty(p.Label) ? $"{p.X:0.##}" : p.Label;
            if (label.Length > 12) label = label.Substring(0, 10) + "..";

            var ft = CreateFormattedText(label, 9.5, TextBrush);
            context.DrawText(ft, new Point(x - ft.Width / 2, plotArea.Bottom + 6));
        }
    }

    protected FormattedText CreateFormattedText(string text, double fontSize, IBrush brush, FontWeight? weight = null)
    {
        var tf = weight.HasValue ? new Typeface(FontFamily.Default, FontStyle.Normal, weight.Value) : DefaultTypeface;
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            tf,
            fontSize,
            brush);
    }

    protected string FormatTickValue(double value)
    {
        if (Math.Abs(value) >= 1_000_000)
            return $"{value / 1_000_000.0:0.##}M";
        if (Math.Abs(value) >= 1_000)
            return $"{value / 1_000.0:0.##}k";
        if (Math.Abs(value) < 0.01 && value != 0)
            return $"{value:0.###}";
        return Math.Abs(value - Math.Round(value)) < 1e-6 ? $"{value:N0}" : $"{value:0.##}";
    }
}
