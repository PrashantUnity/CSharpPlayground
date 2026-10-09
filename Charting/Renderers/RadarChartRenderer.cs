using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

/// <summary>
/// A radar chart: one spoke for each value (named by the first series' labels), each series a polygon whose corners are
/// its values along the spokes, on rings that mark the value scale. A missing value breaks the outline there.
/// </summary>
public sealed class RadarChartRenderer : ChartRendererBase
{
    private const double LabelRoom = 30;
    private static readonly IBrush MarkerFill = new SolidColorBrush(Color.FromArgb(255, 15, 20, 28));

    private sealed record RadarLayout(List<(ChartSeries Series, int Index)> Visible, int Spokes, Point Center, double Radius, (double Min, double Max, double Step) Scale);

    private static RadarLayout? Layout(Rect bounds, ChartOptions options)
    {
        var visible = Enumerable.Range(0, options.Series.Count)
            .Where(i => options.HiddenSeries?.Contains(i) != true && options.Series[i].Points.Count > 0)
            .Select(i => (Series: options.Series[i], Index: i)).ToList();
        if (visible.Count == 0) return null;

        var spokes = visible.Max(v => v.Series.Points.Count);
        var values = visible.SelectMany(v => v.Series.Points).Where(p => double.IsFinite(p.Y)).Select(p => p.Y).DefaultIfEmpty(0).ToList();
        var scale = ChartDataRange.ValueAxis(values.Min(), values.Max(), options.YAxis.Min, options.YAxis.Max, false, options.YAxis.SuggestedMin, options.YAxis.SuggestedMax);
        var radius = Math.Max(10, Math.Min(bounds.Width, bounds.Height) / 2 - LabelRoom);
        return new RadarLayout(visible, spokes, bounds.Center, radius, scale);
    }

    // The direction of spoke k, the first straight up.
    private static double Angle(RadarLayout l, int k) => RadialLayout.Radians(360.0 * k / l.Spokes);

    private static Point At(RadarLayout l, int spoke, double value)
    {
        var reach = l.Radius * (value - l.Scale.Min) / (l.Scale.Max - l.Scale.Min);
        return RadialLayout.PointAt(l.Center, Math.Max(0, reach), Angle(l, spoke));
    }

    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        if (Layout(bounds, options) is not { } l) return;

        // Rings at the ticks of the value scale, with the spokes and their names.
        var last = double.NaN;
        foreach (var tick in ChartTicks.Between(l.Scale.Min, l.Scale.Max, l.Scale.Step))
        {
            var ring = Enumerable.Range(0, l.Spokes).Select(k => At(l, k, tick)).ToList();
            if (options.ShowGrid && l.Spokes > 2) context.DrawGeometry(null, GridPen, RadialLayout.Polygon(ring, true));
            var radius = ring[0].Y - l.Center.Y;
            if (!double.IsNaN(last) && Math.Abs(last - radius) < 14) continue;
            var text = CreateFormattedText(ChartFormat.Tick(tick), 9, TextBrush);
            context.DrawText(text, new Point(l.Center.X + 4, ring[0].Y - text.Height / 2));
            last = radius;
        }

        var names = l.Visible[0].Series.Points;
        for (var k = 0; k < l.Spokes; k++)
        {
            if (options.ShowGrid) context.DrawLine(GridPen, l.Center, RadialLayout.PointAt(l.Center, l.Radius, Angle(l, k)));
            var name = CreateFormattedText(ShortLabel(k < names.Count ? names[k].Label : string.Empty, k + 1), 9.5, TextBrush);
            var tip = RadialLayout.PointAt(l.Center, l.Radius + 10, Angle(l, k));
            var left = Math.Cos(Angle(l, k)) < -0.2 ? -name.Width : Math.Cos(Angle(l, k)) > 0.2 ? 0 : -name.Width / 2;
            var up = Math.Sin(Angle(l, k)) < -0.2 ? -name.Height : Math.Sin(Angle(l, k)) > 0.2 ? 0 : -name.Height / 2;
            context.DrawText(name, new Point(tip.X + left, tip.Y + up));
        }

        foreach (var (series, index) in l.Visible) DrawSeries(context, l, series, index);
    }

    private static void DrawSeries(DrawingContext context, RadarLayout l, ChartSeries series, int index)
    {
        var colourText = string.IsNullOrEmpty(series.Color) ? ChartPaletteService.GetSeriesColor(index) : series.Color;
        var colour = ChartPaletteService.ParseColor(colourText);
        var brush = new SolidColorBrush(colour);
        var width = series.StrokeThickness > 0 ? series.StrokeThickness : 2;
        var pen = series.Dash switch
        {
            LineDash.Dashed => new Pen(brush, width, new DashStyle([4, 3], 0)),
            LineDash.Dotted => new Pen(brush, width, new DashStyle([0.1, 2.2], 0), PenLineCap.Round),
            _ => new Pen(brush, width)
        };

        var runs = ChartPoints.Runs(series.Points).Where(r => r.Start < l.Spokes).ToList();
        var whole = runs.Count == 1 && runs[0].Length == series.Points.Count && series.Points.Count >= 3 && series.Points.Count == l.Spokes;

        if (whole)
        {
            // Every value present: a closed polygon, filled unless the series asks not to.
            var outline = series.Points.Select((p, k) => At(l, k, p.Y)).ToList();
            var geometry = RadialLayout.Polygon(outline, true);
            if (series.Fill ?? true) context.DrawGeometry(new SolidColorBrush(Color.FromArgb(55, colour.R, colour.G, colour.B)), null, geometry);
            context.DrawGeometry(null, pen, geometry);
        }
        else
        {
            foreach (var (start, length) in runs.Where(r => r.Length > 1))
            {
                var open = Enumerable.Range(start, length).Select(k => At(l, k, series.Points[k].Y)).ToList();
                context.DrawGeometry(null, pen, RadialLayout.Polygon(open, false));
            }
        }

        var radius = series.PointRadius ?? 3.5;
        var markerPen = new Pen(brush, 2);
        for (var k = 0; k < Math.Min(series.Points.Count, l.Spokes); k++)
        {
            if (!double.IsFinite(series.Points[k].Y)) continue;
            MarkerShapes.Draw(context, series.PointStyle, At(l, k, series.Points[k].Y), radius, MarkerFill, markerPen);
        }
    }

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        if (Layout(bounds, options) is not { } l) return null;

        ChartHitTestResult? best = null;
        var bestDistance = 25.0;
        foreach (var (series, _) in l.Visible)
        {
            for (var k = 0; k < Math.Min(series.Points.Count, l.Spokes); k++)
            {
                var p = series.Points[k];
                if (!double.IsFinite(p.Y)) continue;
                var at = At(l, k, p.Y);
                var distance = Math.Sqrt((pointerPosition.X - at.X) * (pointerPosition.X - at.X) + (pointerPosition.Y - at.Y) * (pointerPosition.Y - at.Y));
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    var name = string.IsNullOrEmpty(l.Visible[0].Series.Points.ElementAtOrDefault(k)?.Label) ? $"{k + 1}" : l.Visible[0].Series.Points[k].Label;
                    best = new ChartHitTestResult(p, series, at, $"{series.Name} • {name}: {p.GetFormattedY()}");
                }
            }
        }

        return best;
    }
}
