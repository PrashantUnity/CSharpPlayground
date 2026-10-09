using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public abstract class ChartRendererBase : IChartRenderer
{
    protected static bool IsDarkTheme => ThemeService.IsDark;

    private static readonly IPen DarkGridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
    private static readonly IPen LightGridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 1);
    protected static IPen GridPen => IsDarkTheme ? DarkGridPen : LightGridPen;

    private static readonly IPen DarkAxisPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1);
    private static readonly IPen LightAxisPen = new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1);
    protected static IPen AxisPen => IsDarkTheme ? DarkAxisPen : LightAxisPen;

    private static readonly IBrush DarkTextBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
    private static readonly IBrush LightTextBrush = new SolidColorBrush(Color.FromArgb(200, 30, 41, 59));
    protected static IBrush TextBrush => IsDarkTheme ? DarkTextBrush : LightTextBrush;

    protected static readonly Typeface DefaultTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

    // Markers are drawn a little past a fixed range's edge rather than cut in half.
    private const double MarkerRoom = 6;

    // About this many pixels between numbers along the x axis.
    private const double XTickSpacing = 90;

    public abstract void Render(DrawingContext context, Rect bounds, ChartOptions options);
    public abstract ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options);

    /// <summary>
    /// Keeps what is drawn inside the plot (with room for a marker on its edge), so values outside a range the chart
    /// fixes don't spill over the axes.
    /// </summary>
    protected static DrawingContext.PushedState ClipToPlot(DrawingContext context, Rect plotArea) =>
        context.PushClip(plotArea.Inflate(MarkerRoom));

    private static double XPixel(double dataX, double minX, double maxX, Rect plotArea) =>
        plotArea.Left + (dataX - minX) / (maxX - minX) * plotArea.Width;

    /// <summary>
    /// The x axis: the values' own labels (categories) where they have them, otherwise round numbers, whole ones when x
    /// counts the values.
    /// </summary>
    protected void DrawXAxisLabels(
        DrawingContext context,
        Rect plotArea,
        IReadOnlyList<ChartDataPoint> points,
        double minX,
        double maxX)
    {
        if (points.Count == 0) return;

        if (points.All(p => string.IsNullOrEmpty(p.Label)))
        {
            var tickStep = ChartTicks.Step(maxX - minX, Math.Max(2, (int)(plotArea.Width / XTickSpacing)));
            if (points.All(p => !double.IsFinite(p.X) || p.X == Math.Floor(p.X))) tickStep = Math.Max(tickStep, 1);
            foreach (var tick in ChartTicks.Between(minX, maxX, tickStep))
            {
                var ft = CreateFormattedText(Layout.ChartFormat.Tick(tick), 9.5, TextBrush);
                context.DrawText(ft, new Point(XPixel(tick, minX, maxX, plotArea) - ft.Width / 2, plotArea.Bottom + 6));
            }

            return;
        }

        int step = Math.Max(1, (int)Math.Ceiling(points.Count / 8.0));
        for (int i = 0; i < points.Count; i += step)
        {
            // A missing value keeps its label (it names the place of the gap); a missing x has no place.
            var p = points[i];
            if (!double.IsFinite(p.X) || p.X < minX || p.X > maxX) continue;
            double x = XPixel(p.X, minX, maxX, plotArea);
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

    /// <summary>The names of a round chart's slices with their colours, listed to the right of it (the first few that fit).</summary>
    protected void DrawSliceLegend(DrawingContext context, Rect bounds, Rect chartRect, IReadOnlyList<(string Text, Color Color)> entries)
    {
        double legendX = chartRect.Right + 15;
        double legendY = bounds.Top + 20;
        for (int i = 0; i < Math.Min(entries.Count, 10); i++)
        {
            context.DrawRectangle(new SolidColorBrush(entries[i].Color), null, new RoundedRect(new Rect(legendX, legendY + 2, 10, 10), 2, 2));
            var ft = CreateFormattedText(entries[i].Text, 10, TextBrush);
            context.DrawText(ft, new Point(legendX + 16, legendY));
            legendY += 18;
            if (legendY > bounds.Bottom - 18) break;
        }
    }

    /// <summary>The slice label as the legend writes it, cut to fit.</summary>
    protected static string ShortLabel(string? label, int fallbackNumber)
    {
        var text = string.IsNullOrEmpty(label) ? $"Item {fallbackNumber}" : label;
        return text.Length > 12 ? text.Substring(0, 10) + ".." : text;
    }
}
