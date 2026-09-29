using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using Vector3D = PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial.Vector3D;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public abstract class Plot3DRendererBase : IPlot3DRenderer
{
    protected static readonly Typeface LabelTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    protected static readonly IBrush AxisLabelBrush = new SolidColorBrush(Color.FromArgb(200, 210, 220, 235));
    protected static readonly IBrush FloorGridBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    protected static readonly IBrush BoundingBoxBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));

    protected static readonly Pen FloorGridPen = new(FloorGridBrush, 1.0);
    protected static readonly Pen BoundingBoxPen = new(BoundingBoxBrush, 1.0);

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

    private void DrawAxes(DrawingContext context, Projector3D projector, double box, Plot3DOptions options)
    {
        var origin = new Vector3D(-box, -box, -box);
        var xEnd = new Vector3D(box + 0.6, -box, -box);
        var yEnd = new Vector3D(-box, box + 0.6, -box);
        var zEnd = new Vector3D(-box, -box, box + 0.6);

        var pOrigin = projector.ProjectWorld(origin);
        var pX = projector.ProjectWorld(xEnd);
        var pY = projector.ProjectWorld(yEnd);
        var pZ = projector.ProjectWorld(zEnd);

        // X Axis
        context.DrawLine(AxisXPen, pOrigin.ScreenPoint, pX.ScreenPoint);
        DrawAxisLabel(context, "X", pX.ScreenPoint, Color.FromRgb(224, 108, 117));

        // Y Axis
        context.DrawLine(AxisYPen, pOrigin.ScreenPoint, pY.ScreenPoint);
        DrawAxisLabel(context, "Y", pY.ScreenPoint, Color.FromRgb(152, 195, 121));

        // Z Axis
        context.DrawLine(AxisZPen, pOrigin.ScreenPoint, pZ.ScreenPoint);
        DrawAxisLabel(context, "Z", pZ.ScreenPoint, Color.FromRgb(97, 175, 239));
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
