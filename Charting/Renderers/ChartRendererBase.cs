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

    // The height an axis title takes beside its axis.
    private const double AxisTitleSize = 16;

    // Markers are drawn a little past a fixed range's edge rather than cut in half.
    private const double MarkerRoom = 6;

    // About this many pixels between numbers along the x axis.
    private const double XTickSpacing = 90;

    public abstract void Render(DrawingContext context, Rect bounds, ChartOptions options);
    public abstract ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options);

    /// <summary>Where the data is drawn: inside room for the tick labels, and for the axis titles when there are any.</summary>
    protected Rect GetPlotArea(Rect bounds, ChartOptions options)
    {
        double leftPadding = 55 + (HasText(options.YAxisTitle) ? AxisTitleSize : 0);
        double rightPadding = 20;
        double topPadding = 20;
        double bottomPadding = 30 + (HasText(options.XAxisTitle) ? AxisTitleSize : 0);

        return new Rect(
            bounds.Left + leftPadding,
            bounds.Top + topPadding,
            Math.Max(10, bounds.Width - leftPadding - rightPadding),
            Math.Max(10, bounds.Height - topPadding - bottomPadding));
    }

    /// <summary>The range the axes show (<see cref="ChartDataRange"/>); bars pass <paramref name="zeroBaseline"/>.</summary>
    protected void GetDataBounds(
        ChartOptions options,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY,
        bool zeroBaseline = false)
    {
        (minX, maxX, minY, maxY, _) = ChartDataRange.Of(options, zeroBaseline);
    }

    /// <summary>
    /// Keeps what is drawn inside the plot (with room for a marker on its edge), so values outside a range the chart
    /// fixes don't spill over the axes.
    /// </summary>
    protected static DrawingContext.PushedState ClipToPlot(DrawingContext context, Rect plotArea) =>
        context.PushClip(plotArea.Inflate(MarkerRoom));

    /// <summary>The axis titles: x centred under its labels, y turned up the left side.</summary>
    protected void DrawAxisTitles(DrawingContext context, Rect bounds, Rect plotArea, ChartOptions options)
    {
        if (HasText(options.XAxisTitle))
        {
            var ft = CreateFormattedText(options.XAxisTitle!, 10.5, TextBrush, FontWeight.SemiBold);
            context.DrawText(ft, new Point(plotArea.Center.X - ft.Width / 2, plotArea.Bottom + 22));
        }

        if (HasText(options.YAxisTitle))
        {
            var ft = CreateFormattedText(options.YAxisTitle!, 10.5, TextBrush, FontWeight.SemiBold);
            var center = new Point(bounds.Left + 2 + ft.Height / 2, plotArea.Center.Y);
            var turn = Matrix.CreateTranslation(-ft.Width / 2, -ft.Height / 2)
                       * Matrix.CreateRotation(-Math.PI / 2)
                       * Matrix.CreateTranslation(center.X, center.Y);
            using (context.PushTransform(turn))
            {
                context.DrawText(ft, new Point(0, 0));
            }
        }
    }

    private static bool HasText(string? text) => !string.IsNullOrWhiteSpace(text);

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

    // Labels on the value axis closer than this are thinned out, so a short chart's labels never overlap.
    private const double MinLabelGap = 14;

    /// <summary>Grid lines and labels at the value axis's round ticks, the zero line, and the frame.</summary>
    private protected void DrawGridAndAxes(DrawingContext context, Rect plotArea, ChartDataRange range, bool showGrid)
    {
        double lastLabel = double.PositiveInfinity;
        foreach (var val in ChartTicks.Between(range.MinY, range.MaxY, range.StepY))
        {
            double y = ToCanvasY(val, range.MinY, range.MaxY, plotArea);
            if (showGrid)
            {
                context.DrawLine(GridPen, new Point(plotArea.Left, y), new Point(plotArea.Right, y));
            }

            if (lastLabel - y < MinLabelGap) continue;
            var ft = CreateFormattedText(FormatTickValue(val), 10, TextBrush);
            context.DrawText(ft, new Point(plotArea.Left - ft.Width - 8, y - ft.Height / 2));
            lastLabel = y;
        }

        // Zero, when the values cross it
        if (range.MinY < 0 && range.MaxY > 0)
        {
            double zeroY = ToCanvasY(0, range.MinY, range.MaxY, plotArea);
            context.DrawLine(AxisPen, new Point(plotArea.Left, zeroY), new Point(plotArea.Right, zeroY));
        }

        // The frame's axis lines
        context.DrawLine(AxisPen, new Point(plotArea.Left, plotArea.Top), new Point(plotArea.Left, plotArea.Bottom));
        context.DrawLine(AxisPen, new Point(plotArea.Left, plotArea.Bottom), new Point(plotArea.Right, plotArea.Bottom));
    }

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
                var ft = CreateFormattedText(FormatTickValue(tick), 9.5, TextBrush);
                context.DrawText(ft, new Point(ToCanvasX(tick, minX, maxX, plotArea) - ft.Width / 2, plotArea.Bottom + 6));
            }

            return;
        }

        int step = Math.Max(1, (int)Math.Ceiling(points.Count / 8.0));
        for (int i = 0; i < points.Count; i += step)
        {
            // A missing value keeps its label (it names the place of the gap); a missing x has no place.
            var p = points[i];
            if (!double.IsFinite(p.X) || p.X < minX || p.X > maxX) continue;
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
