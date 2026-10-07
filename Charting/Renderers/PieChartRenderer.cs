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
/// Pie and donut charts: one slice for each value, sized by its share, from a start angle round as far as the sweep says
/// (180 degrees is a half circle gauge). With several series, one ring for each, the first outermost; every ring is
/// divided on its own.
/// </summary>
public class PieChartRenderer : ChartRendererBase
{
    private readonly bool _isDonut;

    public PieChartRenderer(bool isDonut = false)
    {
        _isDonut = isDonut;
    }

    // The empty gap between rings, in pixels.
    private const double RingGap = 2;

    private sealed record Ring(ChartSeries Series, List<ChartDataPoint> Slices, double Total);

    private (List<Ring> Rings, Point Center, double Outer, double Hole, double Start, double Sweep, Rect Area) Layout(Rect bounds, ChartOptions options)
    {
        var rings = new List<Ring>();
        for (var i = 0; i < options.Series.Count; i++)
        {
            if (options.HiddenSeries?.Contains(i) == true) continue;
            var slices = options.Series[i].Points.Where(p => p.Y > 0).ToList();
            if (slices.Count > 0) rings.Add(new Ring(options.Series[i], slices, slices.Sum(p => p.Y)));
        }

        // Reserve space for the legend on the right
        double legendWidth = Math.Min(160, bounds.Width * 0.35);
        var area = new Rect(bounds.Left + 10, bounds.Top + 10, Math.Max(50, bounds.Width - legendWidth - 20), Math.Max(50, bounds.Height - 20));
        var hole = _isDonut || options.Type == ChartType.Donut ? Math.Clamp(options.Cutout, 0, 0.95) : 0;
        var start = RadialLayout.Radians(options.StartAngle);
        var sweep = Math.Clamp(options.Sweep, 1, 360) * Math.PI / 180;
        var (center, outer) = RadialLayout.Fit(area, start, sweep, hole);
        return (rings, center, outer, hole, start, sweep, area);
    }

    // The radii of ring i of n: the band between the hole and the edge, shared out evenly.
    private static (double Inner, double Outer) RingRadii(double outer, double hole, int index, int rings)
    {
        var band = outer * (1 - hole);
        var thickness = band / rings;
        var ringOuter = outer - index * thickness;
        var ringInner = ringOuter - thickness + (rings > 1 ? RingGap : 0);
        return (Math.Max(outer * hole, ringInner), ringOuter);
    }

    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        var (rings, center, outer, hole, start, sweep, area) = Layout(bounds, options);
        if (rings.Count == 0) return;

        var slicePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 20, 24, 33)), 1.5);
        for (var r = 0; r < rings.Count; r++)
        {
            var (inner, ringOuter) = RingRadii(outer, hole, r, rings.Count);
            var angle = start;
            for (var i = 0; i < rings[r].Slices.Count; i++)
            {
                var p = rings[r].Slices[i];
                var next = angle + p.Y / rings[r].Total * sweep;
                var colour = ChartPaletteService.ParseColor(!string.IsNullOrEmpty(p.CustomColor) ? p.CustomColor : ChartPaletteService.GetSeriesColor(i));
                context.DrawGeometry(new SolidColorBrush(colour), slicePen, RadialLayout.Slice(center, inner, ringOuter, angle, next));
                angle = next;
            }
        }

        // The legend names the slices of the first ring.
        var first = rings[0];
        DrawSliceLegend(context, bounds, area, first.Slices.Select((p, i) =>
        {
            var colour = ChartPaletteService.ParseColor(!string.IsNullOrEmpty(p.CustomColor) ? p.CustomColor : ChartPaletteService.GetSeriesColor(i));
            return ($"{ShortLabel(p.Label, i + 1)} ({p.Y / first.Total * 100.0:0.0}%)", colour);
        }).ToList());
    }

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        var (rings, center, outer, hole, start, sweep, _) = Layout(bounds, options);
        if (rings.Count == 0) return null;

        var dx = pointerPosition.X - center.X;
        var dy = pointerPosition.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        // The angle round from the start, in [0, 2π) so a slice that crosses 12 o'clock is found.
        var along = Math.Atan2(dy, dx) - start;
        along %= 2 * Math.PI;
        if (along < 0) along += 2 * Math.PI;
        if (along > sweep) return null;

        for (var r = 0; r < rings.Count; r++)
        {
            var (inner, ringOuter) = RingRadii(outer, hole, r, rings.Count);
            if (distance < inner || distance > ringOuter) continue;

            var accumulated = 0.0;
            for (var i = 0; i < rings[r].Slices.Count; i++)
            {
                var p = rings[r].Slices[i];
                var slice = p.Y / rings[r].Total * sweep;
                if (along >= accumulated && along < accumulated + slice)
                {
                    var middle = start + accumulated + slice / 2;
                    return new ChartHitTestResult(p, rings[r].Series, RadialLayout.PointAt(center, (inner + ringOuter) / 2, middle));
                }

                accumulated += slice;
            }
        }

        return null;
    }
}
