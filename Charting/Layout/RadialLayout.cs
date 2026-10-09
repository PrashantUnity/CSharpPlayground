using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;

/// <summary>Where a round chart (pie, donut, polar area, radar) sits in its box, and the arcs and polygons it is drawn with.</summary>
internal static class RadialLayout
{
    /// <summary>How much of the box's half-size the chart's radius takes, leaving room for the stroke.</summary>
    private const double Fill = 0.88;

    /// <summary>A sector's angle in radians from degrees clockwise from 12 o'clock (Avalonia's y points down, so clockwise grows).</summary>
    public static double Radians(double degreesFromTop) => (degreesFromTop - 90) * Math.PI / 180;

    public static Point PointAt(Point center, double radius, double angle) =>
        new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);

    /// <summary>
    /// The centre and outer radius that make a sector of <paramref name="sweep"/> radians from <paramref name="start"/>
    /// (a donut has a hole of <paramref name="hole"/> of the radius) fill <paramref name="box"/>: a full circle is centred,
    /// a half circle sits on the bottom edge of its box, and so on.
    /// </summary>
    public static (Point Center, double Radius) Fit(Rect box, double start, double sweep, double hole)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        void Add(double radius, double angle)
        {
            var x = Math.Cos(angle) * radius;
            var y = Math.Sin(angle) * radius;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        var end = start + sweep;
        foreach (var radius in hole > 0.001 ? new[] { 1.0, hole } : new[] { 1.0 })
        {
            Add(radius, start);
            Add(radius, end);
            // The extremes of a circle that fall inside the sector.
            for (var quarter = Math.Ceiling(start / (Math.PI / 2)); quarter * (Math.PI / 2) <= end; quarter++)
            {
                Add(radius, quarter * (Math.PI / 2));
            }
        }

        if (hole <= 0.001) Add(0, 0); // a pie's slices meet at the centre

        var width = Math.Max(maxX - minX, 1e-6);
        var height = Math.Max(maxY - minY, 1e-6);
        var scale = Fill * Math.Min(box.Width / width, box.Height / height);
        var centre = new Point(box.Center.X - (minX + maxX) / 2 * scale, box.Center.Y - (minY + maxY) / 2 * scale);
        return (centre, scale);
    }

    /// <summary>A ring slice (or a pie slice when <paramref name="inner"/> is 0) between two angles.</summary>
    public static StreamGeometry Slice(Point center, double inner, double outer, double from, double to)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var segments = Math.Max(6, (int)(Math.Abs(to - from) / (Math.PI / 36.0)));
        ctx.BeginFigure(PointAt(center, outer, from), true);
        for (var step = 1; step <= segments; step++) ctx.LineTo(PointAt(center, outer, from + (to - from) * step / segments));

        if (inner > 1e-3)
        {
            ctx.LineTo(PointAt(center, inner, to));
            for (var step = segments - 1; step >= 0; step--) ctx.LineTo(PointAt(center, inner, from + (to - from) * step / segments));
        }
        else
        {
            ctx.LineTo(center);
        }

        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>A closed polygon through the points (a radar's ring or a series' outline).</summary>
    public static StreamGeometry Polygon(IReadOnlyList<Point> points, bool closed)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], closed);
        for (var i = 1; i < points.Count; i++) ctx.LineTo(points[i]);
        ctx.EndFigure(closed);
        return geometry;
    }
}
