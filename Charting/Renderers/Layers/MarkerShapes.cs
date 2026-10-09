using System;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;

/// <summary>Draws a marker of a given shape, about as big as a circle of the same radius.</summary>
internal static class MarkerShapes
{
    public static void Draw(DrawingContext context, PointShape shape, Point center, double radius, IBrush? fill, IPen? pen)
    {
        switch (shape)
        {
            case PointShape.Circle:
                context.DrawEllipse(fill, pen, center, radius, radius);
                break;
            case PointShape.Square:
                context.DrawRectangle(fill, pen, new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
                break;
            case PointShape.Diamond:
                Polygon(context, fill, pen, [new(center.X, center.Y - radius * 1.3), new(center.X + radius * 1.3, center.Y), new(center.X, center.Y + radius * 1.3), new(center.X - radius * 1.3, center.Y)]);
                break;
            case PointShape.Triangle:
                Polygon(context, fill, pen, [new(center.X, center.Y - radius * 1.25), new(center.X + radius * 1.15, center.Y + radius * 0.95), new(center.X - radius * 1.15, center.Y + radius * 0.95)]);
                break;
            case PointShape.Cross:
                // A cross is all stroke: the pen, or the fill as a pen.
                var stroke = pen ?? new Pen(fill, 2);
                context.DrawLine(stroke, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
                context.DrawLine(stroke, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));
                break;
            case PointShape.Star:
                var points = new Point[10];
                for (var k = 0; k < 10; k++)
                {
                    var r = k % 2 == 0 ? radius * 1.4 : radius * 0.6;
                    var angle = -Math.PI / 2 + k * Math.PI / 5;
                    points[k] = new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r);
                }

                Polygon(context, fill, pen, points);
                break;
        }
    }

    private static void Polygon(DrawingContext context, IBrush? fill, IPen? pen, Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++) ctx.LineTo(points[i]);
            ctx.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }
}
