using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Vector3D = PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial.Vector3D;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public abstract class Plot3DRendererBase : IPlot3DRenderer
{
    protected static bool IsDarkTheme => ThemeService.IsDark;

    protected static readonly Typeface LabelTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    private static readonly IBrush DarkAxisLabelBrush = new SolidColorBrush(Color.FromArgb(200, 210, 220, 235));
    private static readonly IBrush LightAxisLabelBrush = new SolidColorBrush(Color.FromArgb(200, 30, 41, 59));
    protected static IBrush AxisLabelBrush => IsDarkTheme ? DarkAxisLabelBrush : LightAxisLabelBrush;

    private static readonly Pen DarkFloorGridPen = new(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1.0);
    private static readonly Pen LightFloorGridPen = new(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 1.0);
    protected static Pen FloorGridPen => IsDarkTheme ? DarkFloorGridPen : LightFloorGridPen;

    private static readonly Pen DarkBoundingBoxPen = new(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1.0);
    private static readonly Pen LightBoundingBoxPen = new(new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)), 1.0);
    protected static Pen BoundingBoxPen => IsDarkTheme ? DarkBoundingBoxPen : LightBoundingBoxPen;

    protected static readonly Pen AxisXPen = new(new SolidColorBrush(Color.FromArgb(220, 224, 108, 117)), 1.8);
    protected static readonly Pen AxisYPen = new(new SolidColorBrush(Color.FromArgb(220, 152, 195, 121)), 1.8);
    protected static readonly Pen AxisZPen = new(new SolidColorBrush(Color.FromArgb(220, 97, 175, 239)), 1.8);

    public static readonly Vector3D LightDirection = new Vector3D(0.5, 1.0, 0.8).Normalized;

    public abstract void Render(DrawingContext context, Rect bounds, Plot3DOptions options);
    public abstract Plot3DHitTestResult? HitTest(Point pos, Rect bounds, Plot3DOptions options);

    protected void RenderEnvironment(DrawingContext context, Projector3D projector, Plot3DOptions options)
    {
        double box = Projector3D.WorldBoxSize * 0.5; // 4.0 -> [-4, 4]

        // 1. Draw floor grid at bottom plane (Y = -box)
        if (options.ShowFloorGrid)
        {
            int gridSteps = 8;
            double step = (box * 2.0) / gridSteps;
            double floorY = -box;

            for (int i = 0; i <= gridSteps; i++)
            {
                double coord = -box + i * step;

                // Line along X
                var p1 = projector.ProjectWorld(new Vector3D(-box, floorY, coord));
                var p2 = projector.ProjectWorld(new Vector3D(box, floorY, coord));
                if (p1.IsVisible || p2.IsVisible)
                {
                    context.DrawLine(FloorGridPen, p1.ScreenPoint, p2.ScreenPoint);
                }

                // Line along Z
                var p3 = projector.ProjectWorld(new Vector3D(coord, floorY, -box));
                var p4 = projector.ProjectWorld(new Vector3D(coord, floorY, box));
                if (p3.IsVisible || p4.IsVisible)
                {
                    context.DrawLine(FloorGridPen, p3.ScreenPoint, p4.ScreenPoint);
                }
            }
        }

        // 2. Bounding Box Cage
        if (options.ShowBoundingBox)
        {
            DrawBoundingBox(context, projector, box);
        }

        // 3. Axes
        if (options.ShowAxes)
        {
            DrawAxes(context, projector, box, options);
        }
    }

    private void DrawBoundingBox(DrawingContext context, Projector3D projector, double box)
    {
        var corners = new Vector3D[8]
        {
            new(-box, -box, -box), new(box, -box, -box),
            new(box, -box, box), new(-box, -box, box),
            new(-box, box, -box), new(box, box, -box),
            new(box, box, box), new(-box, box, box)
        };

        var projected = new ProjectedPoint3D[8];
        for (int i = 0; i < 8; i++)
        {
            projected[i] = projector.ProjectWorld(corners[i]);
        }

        // Top square edges
        DrawCageEdge(context, projected[4], projected[5]);
        DrawCageEdge(context, projected[5], projected[6]);
        DrawCageEdge(context, projected[6], projected[7]);
        DrawCageEdge(context, projected[7], projected[4]);

        // 4 Vertical pillars
        DrawCageEdge(context, projected[0], projected[4]);
        DrawCageEdge(context, projected[1], projected[5]);
        DrawCageEdge(context, projected[2], projected[6]);
        DrawCageEdge(context, projected[3], projected[7]);
    }

    private static void DrawCageEdge(DrawingContext context, in ProjectedPoint3D a, in ProjectedPoint3D b)
    {
        if (a.IsVisible || b.IsVisible)
        {
            context.DrawLine(BoundingBoxPen, a.ScreenPoint, b.ScreenPoint);
        }
    }

    // The data's axes, from the corner where x, y and z are smallest, each named by its title (x, y or z without one).
    // They follow the projector, so z is the upward one for every kind of plot.
    private void DrawAxes(DrawingContext context, Projector3D projector, double box, Plot3DOptions options)
    {
        const double reach = 0.6; // past the box, so the label clears it
        var origin = projector.MapDataToWorld(options.MinX, options.MinY, options.MinZ);
        var span = (x: options.MaxX - options.MinX, y: options.MaxY - options.MinY, z: options.MaxZ - options.MinZ);
        Vector3D End(double dx, double dy, double dz)
        {
            var end = projector.MapDataToWorld(options.MinX + dx * span.x, options.MinY + dy * span.y, options.MinZ + dz * span.z);
            return end + (end - origin).Normalized * reach;
        }

        var pOrigin = projector.ProjectWorld(origin);
        DrawAxis(context, pOrigin, projector.ProjectWorld(End(1, 0, 0)), AxisXPen, options.XAxisTitle, "X", Color.FromRgb(224, 108, 117));
        DrawAxis(context, pOrigin, projector.ProjectWorld(End(0, 1, 0)), AxisYPen, options.YAxisTitle, "Y", Color.FromRgb(152, 195, 121));
        DrawAxis(context, pOrigin, projector.ProjectWorld(End(0, 0, 1)), AxisZPen, options.ZAxisTitle, "Z", Color.FromRgb(97, 175, 239));
    }

    private static void DrawAxis(DrawingContext context, in ProjectedPoint3D from, in ProjectedPoint3D to, Pen pen, string? title, string name, Color color)
    {
        context.DrawLine(pen, from.ScreenPoint, to.ScreenPoint);
        DrawAxisLabel(context, string.IsNullOrWhiteSpace(title) ? name : title, to.ScreenPoint, color);
    }

    protected static void DrawAxisLabel(DrawingContext context, string text, Point pos, Color color)
    {
        var brush = new SolidColorBrush(color);
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            11,
            brush);
        context.DrawText(ft, new Point(pos.X + 4, pos.Y - ft.Height * 0.5));
    }

    protected static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        if (Color.TryParse(hex, out var c)) return c;
        return fallback;
    }
}
