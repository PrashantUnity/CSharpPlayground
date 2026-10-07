using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

/// <summary>
/// Draws every chart with x and y axes: lines, areas, bars, scatter and bubble charts, and any mix of them, on one or two
/// value axes, stacked or not, linear, log, time or category. The axes and the plot are worked out once by
/// <see cref="CartesianPlot"/>; each series is then drawn by the layer for its kind.
/// </summary>
public sealed class CartesianChartRenderer : ChartRendererBase
{
    // Labels on a value axis closer than this are thinned out, so a short chart's labels never overlap.
    private const double MinLabelGap = 14;

    // About this many pixels a label along a horizontal value axis (or a time axis) takes.
    private const double WideLabelGap = 56;

    public override void Render(DrawingContext context, Rect bounds, ChartOptions options)
    {
        var plot = CartesianPlot.Create(options, bounds);
        DrawFrame(context, plot);
        DrawAxisTitles(context, bounds, plot);

        foreach (var layout in plot.Series)
        {
            using (ClipToPlot(context, plot.Area))
            {
                switch (layout.Kind)
                {
                    case ChartType.Bar:
                        BarLayer.Draw(context, plot, layout);
                        break;
                    case ChartType.Scatter:
                        PointLayer.DrawScatter(context, plot, layout);
                        break;
                    case ChartType.Bubble:
                        PointLayer.DrawBubbles(context, plot, layout);
                        break;
                    default:
                        LineLayer.Draw(context, plot, layout);
                        break;
                }
            }
        }

        DrawIndexLabels(context, plot);
    }

    // ── Axes ──

    private void DrawFrame(DrawingContext context, CartesianPlot plot)
    {
        var area = plot.Area;
        if (plot.Horizontal) DrawHorizontalValueAxis(context, plot);
        else DrawVerticalValueAxis(context, plot, plot.Left, AxisSide.Left, plot.Options.ShowGrid);

        if (plot.Right != null && !plot.Horizontal) DrawVerticalValueAxis(context, plot, plot.Right, AxisSide.Right, false);

        // The frame's axis lines
        context.DrawLine(AxisPen, new Point(area.Left, area.Top), new Point(area.Left, area.Bottom));
        context.DrawLine(AxisPen, new Point(area.Left, area.Bottom), new Point(area.Right, area.Bottom));
        if (plot.Right != null) context.DrawLine(AxisPen, new Point(area.Right, area.Top), new Point(area.Right, area.Bottom));
    }

    private void DrawVerticalValueAxis(DrawingContext context, CartesianPlot plot, AxisMap axis, AxisSide side, bool grid)
    {
        var area = plot.Area;
        var last = double.NaN;
        foreach (var tick in axis.Ticks(ChartDataRange.ValueSteps))
        {
            var y = plot.ValuePx(tick.Value, side);
            if (!double.IsFinite(y)) continue;
            if (grid) context.DrawLine(GridPen, new Point(area.Left, y), new Point(area.Right, y));

            if (!double.IsNaN(last) && Math.Abs(last - y) < MinLabelGap) continue;
            var ft = CreateFormattedText(tick.Label, 10, TextBrush);
            var x = side == AxisSide.Left ? area.Left - ft.Width - 8 : area.Right + 8;
            context.DrawText(ft, new Point(x, y - ft.Height / 2));
            last = y;
        }

        // Zero, when the values cross it
        if (axis is LinearAxis && axis.Min < 0 && axis.Max > 0)
        {
            var zero = plot.ValuePx(0, side);
            context.DrawLine(AxisPen, new Point(area.Left, zero), new Point(area.Right, zero));
        }
    }

    private void DrawHorizontalValueAxis(DrawingContext context, CartesianPlot plot)
    {
        var area = plot.Area;
        var last = double.NaN;
        foreach (var tick in plot.Left.Ticks(Math.Max(2, (int)(area.Width / WideLabelGap))))
        {
            var x = plot.ValuePx(tick.Value, AxisSide.Left);
            if (!double.IsFinite(x)) continue;
            if (plot.Options.ShowGrid) context.DrawLine(GridPen, new Point(x, area.Top), new Point(x, area.Bottom));

            var ft = CreateFormattedText(tick.Label, 10, TextBrush);
            if (!double.IsNaN(last) && Math.Abs(last - x) < ft.Width + 8) continue;
            context.DrawText(ft, new Point(x - ft.Width / 2, area.Bottom + 6));
            last = x;
        }

        if (plot.Left is LinearAxis && plot.Left.Min < 0 && plot.Left.Max > 0)
        {
            var zero = plot.ValuePx(0, AxisSide.Left);
            context.DrawLine(AxisPen, new Point(zero, area.Top), new Point(zero, area.Bottom));
        }
    }

    // The axis the values are spread along: categories, dates, numbers or powers of ten, written under (or beside) the plot.
    private void DrawIndexLabels(DrawingContext context, CartesianPlot plot)
    {
        var area = plot.Area;
        if (plot.Series.Count == 0) return;

        if (plot.Horizontal)
        {
            foreach (var tick in plot.Index.Ticks(Math.Max(2, (int)(area.Height / 18))))
            {
                var ft = CreateFormattedText(tick.Label, 9.5, TextBrush);
                context.DrawText(ft, new Point(area.Left - ft.Width - 8, plot.IndexPx(tick.Value) - ft.Height / 2));
            }

            return;
        }

        if (plot.Index is LinearAxis { Reverse: false } linear)
        {
            // Numbers, or the values' own labels where they have them.
            DrawXAxisLabels(context, area, plot.Series[0].Series.Points, linear.Min, linear.Max);
            return;
        }

        var maxTicks = plot.IsCategory ? 10 : Math.Max(2, (int)(area.Width / (plot.Index is TimeAxis ? 110 : WideLabelGap)));
        foreach (var tick in plot.Index.Ticks(maxTicks))
        {
            var ft = CreateFormattedText(tick.Label, 9.5, TextBrush);
            context.DrawText(ft, new Point(plot.IndexPx(tick.Value) - ft.Width / 2, area.Bottom + 6));
        }
    }

    // x under the plot (or down the left side of a horizontal one), y up the left side, the right axis up the right.
    private void DrawAxisTitles(DrawingContext context, Rect bounds, CartesianPlot plot)
    {
        var options = plot.Options;
        var area = plot.Area;
        var bottom = plot.Horizontal ? options.YAxis.Title : options.XAxis.Title;
        var left = plot.Horizontal ? options.XAxis.Title : options.YAxis.Title;

        if (!string.IsNullOrWhiteSpace(bottom))
        {
            var ft = CreateFormattedText(bottom!, 10.5, TextBrush, FontWeight.SemiBold);
            context.DrawText(ft, new Point(area.Center.X - ft.Width / 2, area.Bottom + 22));
        }

        if (!string.IsNullOrWhiteSpace(left)) DrawTurnedTitle(context, left!, bounds.Left + 2, area.Center.Y, true);
        if (plot.Right != null && !string.IsNullOrWhiteSpace(options.Y2Axis?.Title)) DrawTurnedTitle(context, options.Y2Axis!.Title!, bounds.Right - 2, area.Center.Y, false);
    }

    private void DrawTurnedTitle(DrawingContext context, string text, double edge, double centreY, bool leftEdge)
    {
        var ft = CreateFormattedText(text, 10.5, TextBrush, FontWeight.SemiBold);
        var centre = new Point(leftEdge ? edge + ft.Height / 2 : edge - ft.Height / 2, centreY);
        var turn = Matrix.CreateTranslation(-ft.Width / 2, -ft.Height / 2)
                   * Matrix.CreateRotation(leftEdge ? -Math.PI / 2 : Math.PI / 2)
                   * Matrix.CreateTranslation(centre.X, centre.Y);
        using (context.PushTransform(turn))
        {
            context.DrawText(ft, new Point(0, 0));
        }
    }

    // ── Hit testing ──

    // Close enough to a line or scatter point to point at it, in pixels.
    private const double LineReach = 40;
    private const double LineAlongReach = 35;
    private const double ScatterReach = 25;

    public override ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options)
    {
        var plot = CartesianPlot.Create(options, bounds);
        if (!plot.Area.Contains(pointerPosition) || plot.Series.Count == 0) return null;

        (SeriesLayout Layout, int Index, Point At)? best = null;
        var bestDistance = double.MaxValue;

        // A bar: the one in the slot under the pointer whose lane the pointer is in, whatever its height.
        var barFallback = ((SeriesLayout, int, Point)?)null;
        if (plot.Index is CategoryAxis category && plot.Series.Any(l => l.IsBar))
        {
            var along = plot.Horizontal ? pointerPosition.Y - plot.Area.Top : pointerPosition.X - plot.Area.Left;
            var length = plot.Horizontal ? plot.Area.Height : plot.Area.Width;
            var unit = category.Reverse ? 1 - along / length : along / length;
            var slot = (int)Math.Floor(unit * category.Count);
            if (slot >= 0 && slot < category.Count)
            {
                foreach (var layout in plot.Series.Where(l => l.IsBar))
                {
                    if (BarLayer.BarRect(plot, layout, slot) is not { } rect) continue;
                    var centre = new Point(plot.Horizontal ? rect.Right : rect.Center.X, plot.Horizontal ? rect.Center.Y : rect.Top);
                    var lane = plot.Horizontal ? (pointerPosition.Y >= rect.Top && pointerPosition.Y <= rect.Bottom) : (pointerPosition.X >= rect.Left && pointerPosition.X <= rect.Right);
                    if (lane)
                    {
                        best = (layout, slot, centre);
                        bestDistance = 0;
                        break;
                    }

                    barFallback ??= (layout, slot, centre);
                }
            }
        }

        foreach (var layout in plot.Series.Where(l => !l.IsBar))
        {
            var reach = layout.Kind switch { ChartType.Scatter => ScatterReach, ChartType.Bubble => ScatterReach, _ => LineReach };
            foreach (var i in Candidates(plot, layout, pointerPosition))
            {
                if (!layout.IsDrawn(i)) continue;
                var at = plot.At(plot.IndexValue(layout, i), layout.ValueAt(i), layout.Side);
                var dx = pointerPosition.X - at.X;
                var dy = pointerPosition.Y - at.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var limit = layout.Kind == ChartType.Bubble ? Math.Max(reach, PointLayer.Radius(layout.Series, i)) : reach;
                var near = distance <= limit || (layout.IsLineLike && Math.Abs(dx) <= LineAlongReach);
                if (near && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (layout, i, at);
                }
            }
        }

        best ??= barFallback;
        if (best is not { } hit) return null;
        var point = hit.Layout.Series.Points[hit.Index];
        var result = new ChartHitTestResult(point, hit.Layout.Series, hit.At);
        return hit.Layout.Kind is ChartType.Line or ChartType.Area or ChartType.Bar ? WithRows(plot, result, hit.Layout, hit.Index) : result;
    }

    // The values worth measuring against a pointer: all of them, or, where the series is laid out in order along the
    // axis, those at about the pointer.
    private static IEnumerable<int> Candidates(CartesianPlot plot, SeriesLayout layout, Point pointer)
    {
        var points = layout.Series.Points;
        if (points.Count < 64 || plot.Horizontal) return Enumerable.Range(0, points.Count);

        var along = pointer.X;
        int centre;
        if (plot.IsCategory)
        {
            var unit = (along - plot.Area.Left) / plot.Area.Width;
            centre = (int)Math.Floor((plot.Index.Reverse ? 1 - unit : unit) * points.Count);
        }
        else
        {
            if (!Ordered(layout.Series, plot.Index.Reverse)) return Enumerable.Range(0, points.Count);
            centre = NearestByX(plot, layout, along);
        }

        centre = Math.Clamp(centre, 0, points.Count - 1);

        // Enough neighbours to cover the reach even when the values are packed tighter than a pixel.
        var perPixel = Math.Max(1, (int)Math.Ceiling(points.Count / Math.Max(1, plot.Area.Width)));
        var span = perPixel * ((int)LineReach + 2);
        return Enumerable.Range(Math.Max(0, centre - span), Math.Min(points.Count, centre + span + 1) - Math.Max(0, centre - span));
    }

    private static readonly ConditionalWeakTable<ChartSeries, int[]> OrderCache = new();

    // Whether x only goes one way, checked once for as many values as the series has (not at each pointer move).
    private static bool Ordered(ChartSeries series, bool reverse)
    {
        if (!OrderCache.TryGetValue(series, out var cached) || cached[0] != series.Points.Count)
        {
            var increasing = true;
            var decreasing = true;
            for (var i = 1; i < series.Points.Count; i++)
            {
                if (series.Points[i].X < series.Points[i - 1].X) increasing = false;
                if (series.Points[i].X > series.Points[i - 1].X) decreasing = false;
            }

            cached = [series.Points.Count, increasing ? 1 : 0, decreasing ? 1 : 0];
            OrderCache.AddOrUpdate(series, cached);
        }

        return reverse ? cached[2] == 1 : cached[1] == 1;
    }

    private static int NearestByX(CartesianPlot plot, SeriesLayout layout, double pixelX)
    {
        var points = layout.Series.Points;
        int low = 0, high = points.Count - 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            var x = plot.IndexPx(points[mid].X);
            var goesRight = plot.Index.Reverse ? x > pixelX : x < pixelX;
            if (goesRight) low = mid + 1;
            else high = mid;
        }

        return low;
    }

    // Every series that has a value where the hit is, for a tooltip that names them all.
    private ChartHitTestResult WithRows(CartesianPlot plot, ChartHitTestResult hit, SeriesLayout main, int index)
    {
        var rows = new List<ChartTooltipRow>();
        var point = main.Series.Points[index];
        var reference = plot.IndexValue(main, index);

        foreach (var layout in plot.Series.Where(l => l.Kind is ChartType.Line or ChartType.Area or ChartType.Bar))
        {
            var i = plot.IsCategory ? index : NearestByIndexValue(plot, layout, reference);
            if (i < 0 || i >= layout.Series.Points.Count || !double.IsFinite(layout.Series.Points[i].Y)) continue;
            var at = plot.At(plot.IndexValue(layout, i), layout.ValueAt(i), layout.Side);
            var colorText = !string.IsNullOrEmpty(layout.Series.Color) ? layout.Series.Color : ChartPaletteService.GetSeriesColor(layout.Index);
            rows.Add(new ChartTooltipRow($"{layout.Series.Name}: {layout.Series.Points[i].GetFormattedY()}", colorText, at));
        }

        if (rows.Count < 2) return hit;
        var header = string.IsNullOrEmpty(point.Label) ? plot.Index.IsCategory ? $"{point.X:0.##}" : IndexText(plot, reference) : point.Label;
        return new ChartHitTestResult(hit.Point, hit.Series, hit.CanvasPosition) { Header = header, Rows = rows };
    }

    private static string IndexText(CartesianPlot plot, double value) =>
        plot.Index is TimeAxis ? TimeTicks.ToDate(value).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) : ChartFormat.Tick(value);

    // The value of a series closest in x to a place, if it is within reach of it in pixels.
    private static int NearestByIndexValue(CartesianPlot plot, SeriesLayout layout, double reference)
    {
        var points = layout.Series.Points;
        var best = -1;
        var bestGap = double.MaxValue;
        var target = plot.IndexPx(reference);
        for (var i = 0; i < points.Count; i++)
        {
            if (!double.IsFinite(points[i].X)) continue;
            var gap = Math.Abs(plot.IndexPx(points[i].X) - target);
            if (gap < bestGap)
            {
                bestGap = gap;
                best = i;
            }
        }

        return bestGap <= LineAlongReach ? best : -1;
    }
}
