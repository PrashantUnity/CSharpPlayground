using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

/// <summary>
/// A polar area chart: a pie whose slices all take the same angle and reach out as far as their value, on circles that mark
/// the value scale. It draws the first series that is shown.
/// </summary>
public sealed class PolarAreaChartRenderer : ChartRendererBase
{
    private sealed record PolarLayout(ChartSeries Series, List<ChartDataPoint> Points, Point Center, double Outer, double Start, double Sweep, (double Min, double Max, double Step) Scale, Rect Area);

    private PolarLayout? Layout(Rect bounds, ChartOptions options)
    {
        var index = Enumerable.Range(0, options.Series.Count).FirstOrDefault(i => options.HiddenSeries?.Contains(i) != true && options.Series[i].Points.Count > 0, -1);
        if (index < 0) return null;

        var series = options.Series[index];
        var points = series.Points.Where(p => double.IsFinite(p.Y)).ToList();
        if (points.Count == 0) return null;

        double legendWidth = Math.Min(160, bounds.Width * 0.35);
        var area = new Rect(bounds.Left + 10, bounds.Top + 10, Math.Max(50, bounds.Width - legendWidth - 20), Math.Max(50, bounds.Height - 20));
        var start = RadialLayout.Radians(options.StartAngle);
        var sweep = Math.Clamp(options.Sweep, 1, 360) * Math.PI / 180;
        var (center, outer) = RadialLayout.Fit(area, start, sweep, 0);
        var scale = ChartDataRange.ValueAxis(0, points.Max(p => p.Y), options.YAxis.Min, options.YAxis.Max, true, options.YAxis.SuggestedMin, options.YAxis.SuggestedMax);
        return new PolarLayout(series, points, center, outer, start, sweep, scale, area);
    }

    private static double Reach(PolarLayout l, double value) =>
        l.Outer * Math.Clamp((value - l.Scale.Min) / (l.Scale.Max - l.Scale.Min), 0, 1);

    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        if (Layout(bounds, options) is not { } l) return;

        // The value scale: a circle at each tick, labelled where the first slice starts.
        var last = double.NaN;
        foreach (var tick in ChartTicks.Between(l.Scale.Min, l.Scale.Max, l.Scale.Step))
        {
            var radius = Reach(l, tick);
            if (radius < 1) continue;
            if (options.ShowGrid) context.DrawGeometry(null, GridPen, RadialLayout.Slice(l.Center, radius - 0.5, radius + 0.5, l.Start, l.Start + l.Sweep));
            var label = CreateFormattedText(ChartFormat.Tick(tick), 9, TextBrush);
            var at = RadialLayout.PointAt(l.Center, radius, l.Start);
            if (!double.IsNaN(last) && Math.Abs(last - radius) < 14) continue;
            context.DrawText(label, new Point(at.X + 3, at.Y - label.Height - 1));
            last = radius;
        }

        var slice = l.Sweep / l.Points.Count;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 20, 24, 33)), 1.5);
        for (var i = 0; i < l.Points.Count; i++)
        {
            var colour = ColourOf(l.Points[i], i);
            var from = l.Start + i * slice;
            context.DrawGeometry(new SolidColorBrush(Color.FromArgb(170, colour.R, colour.G, colour.B)), new Pen(new SolidColorBrush(colour), 1.5),
                RadialLayout.Slice(l.Center, 0, Math.Max(0.5, Reach(l, l.Points[i].Y)), from, from + slice));
        }

        DrawSliceLegend(context, bounds, l.Area, l.Points.Select((p, i) => ($"{ShortLabel(p.Label, i + 1)} ({p.GetFormattedY()})", ColourOf(p, i))).ToList());
    }

    private static Color ColourOf(ChartDataPoint point, int index) =>
        ChartPaletteService.ParseColor(!string.IsNullOrEmpty(point.CustomColor) ? point.CustomColor : ChartPaletteService.GetSeriesColor(index));

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        if (Layout(bounds, options) is not { } l) return null;

        var dx = pointerPosition.X - l.Center.X;
        var dy = pointerPosition.Y - l.Center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var along = (Math.Atan2(dy, dx) - l.Start) % (2 * Math.PI);
        if (along < 0) along += 2 * Math.PI;
        if (along > l.Sweep) return null;

        var slice = l.Sweep / l.Points.Count;
        var i = Math.Min(l.Points.Count - 1, (int)(along / slice));
        var reach = Reach(l, l.Points[i].Y);
        if (distance > reach) return null;

        return new ChartHitTestResult(l.Points[i], l.Series, RadialLayout.PointAt(l.Center, reach / 2, l.Start + (i + 0.5) * slice));
    }
}
