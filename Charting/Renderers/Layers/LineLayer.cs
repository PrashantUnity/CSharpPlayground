using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;

/// <summary>
/// Draws a line or area series: straight, stepped or curved segments, solid, dashed or dotted, filled to the baseline, to
/// the pile under it or to another series, with markers where there is room for them. A missing value breaks the line; a
/// run longer than the plot has pixel columns is drawn through the few values that give the same picture.
/// </summary>
internal static class LineLayer
{
    private static readonly IBrush MarkerFill = new SolidColorBrush(Color.FromArgb(255, 15, 20, 28));

    public static void Draw(DrawingContext context, CartesianPlot plot, SeriesLayout layout)
    {
        var series = layout.Series;
        var options = plot.Options;
        var colorText = string.IsNullOrEmpty(series.Color) ? ChartPaletteService.GetSeriesColor(layout.Index) : series.Color;
        var baseColor = ChartPaletteService.ParseColor(colorText);
        var strokeBrush = new SolidColorBrush(baseColor);
        var pen = StrokePen(strokeBrush, series);

        var runs = ChartPoints.Runs(series.Points).ToList();
        var columns = plot.Horizontal ? plot.Area.Height : plot.Area.Width;
        var drawn = runs.Select(r => ChartPoints.ForLineAt(
            r.Start, r.Length, columns,
            i => plot.IndexPx(plot.IndexValue(layout, i)),
            layout.ValueAt,
            plot.IsCategory || IsIncreasing(series.Points, r.Start, r.Length))).ToList();

        var fills = series.Fill ?? layout.Kind == ChartType.Area;
        var curve = Curve.Of(series, plot);
        if (fills)
        {
            var other = series.FillTo is { } to ? plot.Series.FirstOrDefault(l => l.Index == to) : null;
            foreach (var indices in drawn.Where(d => d.Count > 0))
            {
                var geometry = FillGeometry(plot, layout, other, indices, curve);
                var brush = other == null && !layout.IsStacked ? GradientFill(baseColor) : new SolidColorBrush(Color.FromArgb(70, baseColor.R, baseColor.G, baseColor.B));
                context.DrawGeometry(brush, null, geometry);
            }
        }

        foreach (var indices in drawn.Where(d => d.Count > 1))
        {
            if (series.ColorSegments) DrawSegments(context, plot, layout, indices, pen, colorText);
            else context.DrawGeometry(null, pen, LineGeometry(Points(plot, layout, indices), curve));
        }

        DrawMarkers(context, plot, layout, runs, strokeBrush);
    }

    private static Pen StrokePen(IBrush brush, ChartSeries series)
    {
        var width = series.StrokeThickness > 0 ? series.StrokeThickness : 2.5;
        return series.Dash switch
        {
            LineDash.Dashed => new Pen(brush, width, new DashStyle([4, 3], 0)),
            LineDash.Dotted => new Pen(brush, width, new DashStyle([0.1, 2.2], 0), PenLineCap.Round),
            _ => new Pen(brush, width)
        };
    }

    private static IBrush GradientFill(Color color) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new GradientStop(Color.FromArgb(90, color.R, color.G, color.B), 0.0),
            new GradientStop(Color.FromArgb(10, color.R, color.G, color.B), 1.0)
        }
    };

    private static bool IsIncreasing(IReadOnlyList<ChartDataPoint> points, int start, int length)
    {
        for (var i = start + 1; i < start + length; i++)
        {
            if (points[i].X < points[i - 1].X) return false;
        }

        return true;
    }

    private static List<Point> Points(CartesianPlot plot, SeriesLayout layout, IReadOnlyList<int> indices) =>
        indices.Select(i => plot.At(plot.IndexValue(layout, i), layout.ValueAt(i), layout.Side)).ToList();

    // Segments in the colour of the value they end at (a value without its own keeps the series colour).
    private static void DrawSegments(DrawingContext context, CartesianPlot plot, SeriesLayout layout, IReadOnlyList<int> indices, Pen pen, string seriesColor)
    {
        var points = layout.Series.Points;
        for (var k = 1; k < indices.Count; k++)
        {
            var color = points[indices[k]].CustomColor ?? seriesColor;
            var segment = new Pen(new SolidColorBrush(ChartPaletteService.ParseColor(color)), pen.Thickness, pen.DashStyle, pen.LineCap);
            var from = plot.At(plot.IndexValue(layout, indices[k - 1]), layout.ValueAt(indices[k - 1]), layout.Side);
            var to = plot.At(plot.IndexValue(layout, indices[k]), layout.ValueAt(indices[k]), layout.Side);
            context.DrawLine(segment, from, to);
        }
    }

    private static void DrawMarkers(DrawingContext context, CartesianPlot plot, SeriesLayout layout, List<(int Start, int Length)> runs, IBrush strokeBrush)
    {
        var series = layout.Series;
        var length = plot.Horizontal ? plot.Area.Height : plot.Area.Width;

        // Markers where asked for and there is room for them (values packed tighter than a marker would only thicken the
        // line), and always for a value between two gaps, which has no line to show it: a marker where there is room, a
        // dot where there isn't. A value with its own colour is marked in it.
        if (series.Points.Count * ChartPoints.MarkerSpacing <= length)
        {
            var radius = series.PointRadius ?? 3.5;
            var pointStroke = new Pen(strokeBrush, 2.0);
            foreach (var (start, count) in runs)
            {
                if (count > 1 && !plot.Options.ShowPoints) continue;
                for (var i = start; i < start + count; i++)
                {
                    var p = series.Points[i];
                    var stroke = string.IsNullOrEmpty(p.CustomColor) ? pointStroke : new Pen(new SolidColorBrush(ChartPaletteService.ParseColor(p.CustomColor)), 2.0);
                    MarkerShapes.Draw(context, series.PointStyle, plot.At(plot.IndexValue(layout, i), layout.ValueAt(i), layout.Side), radius, MarkerFill, stroke);
                }
            }

            return;
        }

        var isolated = runs.Where(r => r.Length == 1).Select(r => r.Start).ToList();
        var cells = new HashSet<(long, long)>();
        foreach (var i in isolated)
        {
            var at = plot.At(plot.IndexValue(layout, i), layout.ValueAt(i), layout.Side);
            if (cells.Add(((long)Math.Floor(at.X / ChartPoints.ScatterCell), (long)Math.Floor(at.Y / ChartPoints.ScatterCell)))) context.DrawEllipse(strokeBrush, null, at, 1.5, 1.5);
        }
    }

    // One run's line through the points given (in order).
    private static StreamGeometry LineGeometry(IReadOnlyList<Point> points, Curve curve)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], false);
        curve.Append(ctx, points);
        ctx.EndFigure(false);
        return geometry;
    }

    // The area between a run and what it is filled to: the baseline, the pile under it, or another series.
    private static StreamGeometry FillGeometry(CartesianPlot plot, SeriesLayout layout, SeriesLayout? other, IReadOnlyList<int> indices, Curve curve)
    {
        var top = Points(plot, layout, indices);
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(top[0], true);
        curve.Append(ctx, top);

        List<Point> bottom;
        Curve bottomCurve = curve;
        if (other != null)
        {
            // The other series at the same places, where it has values.
            var shared = indices.Where(other.IsDrawn).ToList();
            bottom = Points(plot, other, shared);
            bottomCurve = Curve.Of(other.Series, plot).Reversed();
        }
        else if (layout.IsStacked)
        {
            bottom = indices.Select(i => plot.At(plot.IndexValue(layout, i), layout.BaseAt(i, plot.Baseline(layout.Side)), layout.Side)).ToList();
        }
        else
        {
            var baseline = plot.Baseline(layout.Side);
            bottom = indices.Select(i => plot.At(plot.IndexValue(layout, i), baseline, layout.Side)).ToList();
            bottomCurve = Curve.Straight;
        }

        if (bottom.Count == 0)
        {
            ctx.EndFigure(true);
            return geometry;
        }

        bottom.Reverse();
        ctx.LineTo(bottom[0]);
        bottomCurve.Append(ctx, bottom);
        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>How a line gets from one point to the next: straight, in steps, or along a curve.</summary>
    internal readonly record struct Curve(LineInterpolation Interpolation, double Tension, LineStep Step, Rect Limits)
    {
        public static Curve Straight => new(LineInterpolation.Linear, 0, LineStep.None, default);

        public static Curve Of(ChartSeries series, CartesianPlot plot) => new(series.Interpolation, series.Tension, series.Step, plot.Area);

        // The same path walked the other way round: a step that comes before a value comes after it.
        public Curve Reversed() => this with
        {
            Step = Step switch { LineStep.Before => LineStep.After, LineStep.After => LineStep.Before, var other => other }
        };

        /// <summary>Continues a figure begun at <c>points[0]</c> through the rest.</summary>
        public void Append(StreamGeometryContext ctx, IReadOnlyList<Point> points)
        {
            if (points.Count < 2) return;
            if (Step != LineStep.None)
            {
                for (var k = 1; k < points.Count; k++) StepTo(ctx, points[k - 1], points[k]);
                return;
            }

            switch (Interpolation)
            {
                case LineInterpolation.Smooth when points.Count > 2:
                    Smooth(ctx, points);
                    break;
                case LineInterpolation.Monotone when points.Count > 2 && StrictlyOneWay(points):
                    Monotone(ctx, points);
                    break;
                default:
                    for (var k = 1; k < points.Count; k++) ctx.LineTo(points[k]);
                    break;
            }
        }

        private void StepTo(StreamGeometryContext ctx, Point from, Point to)
        {
            switch (Step)
            {
                case LineStep.Before:
                    ctx.LineTo(new Point(to.X, from.Y));
                    break;
                case LineStep.After:
                    ctx.LineTo(new Point(from.X, to.Y));
                    break;
                default:
                    var middle = (from.X + to.X) / 2;
                    ctx.LineTo(new Point(middle, from.Y));
                    ctx.LineTo(new Point(middle, to.Y));
                    break;
            }

            ctx.LineTo(to);
        }

        // A cardinal spline: each value's control points lie along the line between its neighbours, as far as the tension says.
        private void Smooth(StreamGeometryContext ctx, IReadOnlyList<Point> p)
        {
            var n = p.Count;
            var before = new Point[n];
            var after = new Point[n];
            before[0] = after[0] = p[0];
            before[n - 1] = after[n - 1] = p[n - 1];
            for (var i = 1; i < n - 1; i++)
            {
                var d01 = Distance(p[i - 1], p[i]);
                var d12 = Distance(p[i], p[i + 1]);
                var total = d01 + d12;
                if (total < 1e-9)
                {
                    before[i] = after[i] = p[i];
                    continue;
                }

                var fa = Tension * d01 / total;
                var fb = Tension * d12 / total;
                var dx = p[i + 1].X - p[i - 1].X;
                var dy = p[i + 1].Y - p[i - 1].Y;
                before[i] = Cap(new Point(p[i].X - fa * dx, p[i].Y - fa * dy));
                after[i] = Cap(new Point(p[i].X + fb * dx, p[i].Y + fb * dy));
            }

            for (var i = 0; i < n - 1; i++) ctx.CubicBezierTo(after[i], before[i + 1], p[i + 1]);
        }

        // A curve through the values that never rises or falls between two values that do not (Fritsch-Carlson).
        private static void Monotone(StreamGeometryContext ctx, IReadOnlyList<Point> p)
        {
            var n = p.Count;
            var delta = new double[n - 1];
            var m = new double[n];
            for (var k = 0; k < n - 1; k++) delta[k] = (p[k + 1].Y - p[k].Y) / (p[k + 1].X - p[k].X);
            m[0] = delta[0];
            m[n - 1] = delta[n - 2];
            for (var k = 1; k < n - 1; k++) m[k] = delta[k - 1] * delta[k] <= 0 ? 0 : (delta[k - 1] + delta[k]) / 2;
            for (var k = 0; k < n - 1; k++)
            {
                if (delta[k] == 0)
                {
                    m[k] = m[k + 1] = 0;
                    continue;
                }

                var alpha = m[k] / delta[k];
                var beta = m[k + 1] / delta[k];
                var s = alpha * alpha + beta * beta;
                if (s > 9)
                {
                    var tau = 3 / Math.Sqrt(s);
                    m[k] = tau * alpha * delta[k];
                    m[k + 1] = tau * beta * delta[k];
                }
            }

            for (var k = 0; k < n - 1; k++)
            {
                var dx = p[k + 1].X - p[k].X;
                ctx.CubicBezierTo(new Point(p[k].X + dx / 3, p[k].Y + m[k] * dx / 3), new Point(p[k + 1].X - dx / 3, p[k + 1].Y - m[k + 1] * dx / 3), p[k + 1]);
            }
        }

        private Point Cap(Point point) => Limits.Width <= 0 ? point : new Point(Math.Clamp(point.X, Limits.Left, Limits.Right), Math.Clamp(point.Y, Limits.Top, Limits.Bottom));

        // The curve needs x to run one way only (a fill walks its other edge backwards).
        private static bool StrictlyOneWay(IReadOnlyList<Point> points)
        {
            var sign = Math.Sign(points[1].X - points[0].X);
            if (sign == 0) return false;
            for (var i = 1; i < points.Count; i++)
            {
                if (Math.Sign(points[i].X - points[i - 1].X) != sign) return false;
            }

            return true;
        }

        private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
