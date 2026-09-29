using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using Vector3D = PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial.Vector3D;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public class VoxelBar3DRenderer : Plot3DRendererBase
{
    private readonly struct VoxelBar
    {
        public Point3D Point { get; }
        public Series3D Series { get; }
        public double Depth { get; }
        public Color Color { get; }
        public Vector3D BaseCenter { get; }
        public Vector3D TopCenter { get; }
        public double Radius { get; }

        public VoxelBar(Point3D point, Series3D series, double depth, Color color, in Vector3D baseCenter, in Vector3D topCenter, double radius)
        {
            Point = point;
            Series = series;
            Depth = depth;
            Color = color;
            BaseCenter = baseCenter;
            TopCenter = topCenter;
            Radius = radius;
        }
    }

    public override void Render(DrawingContext context, Rect bounds, Plot3DOptions options)
    {
        options.RecalculateBounds();
        var projector = new Projector3D(bounds, options);
        RenderEnvironment(context, projector, options);

        if (options.Series.Count == 0) return;

        var bars = new List<VoxelBar>();
        var defaultColor = ParseColor(options.PrimaryColor, Color.FromRgb(78, 201, 176));

        double box = Projector3D.WorldBoxSize * 0.5;
        double barWidth = 0.5;

        foreach (var s in options.Series)
        {
            var sColor = ParseColor(s.Color, defaultColor);

            foreach (var pt in s.Points)
            {
                var topWorld = projector.MapDataToWorld(pt.X, pt.Y, pt.Z);
                var baseWorld = new Vector3D(topWorld.X, -box, topWorld.Z);

                var prTop = projector.ProjectWorld(topWorld);
                if (!prTop.IsVisible) continue;

                var col = !string.IsNullOrEmpty(pt.CustomColor)
                    ? ParseColor(pt.CustomColor, sColor)
                    : sColor;

                bars.Add(new VoxelBar(pt, s, prTop.Depth, col, baseWorld, topWorld, barWidth));
            }
        }

        // Sort back-to-front
        bars.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 1.0);

        foreach (var bar in bars)
        {
            double r = bar.Radius;
            var wBase = bar.BaseCenter;
            var wTop = bar.TopCenter;

            // 4 Base corners & 4 Top corners
            var cTop0 = projector.ProjectWorld(new Vector3D(wTop.X - r, wTop.Y, wTop.Z - r));
            var cTop1 = projector.ProjectWorld(new Vector3D(wTop.X + r, wTop.Y, wTop.Z - r));
            var cTop2 = projector.ProjectWorld(new Vector3D(wTop.X + r, wTop.Y, wTop.Z + r));
            var cTop3 = projector.ProjectWorld(new Vector3D(wTop.X - r, wTop.Y, wTop.Z + r));

            var cBase0 = projector.ProjectWorld(new Vector3D(wBase.X - r, wBase.Y, wBase.Z - r));
            var cBase1 = projector.ProjectWorld(new Vector3D(wBase.X + r, wBase.Y, wBase.Z - r));
            var cBase2 = projector.ProjectWorld(new Vector3D(wBase.X + r, wBase.Y, wBase.Z + r));
            var cBase3 = projector.ProjectWorld(new Vector3D(wBase.X - r, wBase.Y, wBase.Z + r));

            // Side faces
            DrawQuad(context, cBase0.ScreenPoint, cBase1.ScreenPoint, cTop1.ScreenPoint, cTop0.ScreenPoint,
                Modulate(bar.Color, 0.65), borderPen);
            DrawQuad(context, cBase1.ScreenPoint, cBase2.ScreenPoint, cTop2.ScreenPoint, cTop1.ScreenPoint,
                Modulate(bar.Color, 0.8), borderPen);
            DrawQuad(context, cBase2.ScreenPoint, cBase3.ScreenPoint, cTop3.ScreenPoint, cTop2.ScreenPoint,
                Modulate(bar.Color, 0.7), borderPen);
            DrawQuad(context, cBase3.ScreenPoint, cBase0.ScreenPoint, cTop0.ScreenPoint, cTop3.ScreenPoint,
                Modulate(bar.Color, 0.55), borderPen);

            // Top face (brightest)
            DrawQuad(context, cTop0.ScreenPoint, cTop1.ScreenPoint, cTop2.ScreenPoint, cTop3.ScreenPoint,
                Modulate(bar.Color, 1.05), borderPen);
        }
    }

    private static void DrawQuad(DrawingContext context, Point p0, Point p1, Point p2, Point p3, Color color, Pen pen)
    {
        var geom = new StreamGeometry();
        using (var gc = geom.Open())
        {
            gc.BeginFigure(p0, true);
            gc.LineTo(p1);
            gc.LineTo(p2);
            gc.LineTo(p3);
            gc.EndFigure(true);
        }
        context.DrawGeometry(new SolidColorBrush(color), pen, geom);
    }

    private static Color Modulate(Color c, double factor)
    {
        return Color.FromRgb(
            (byte)Math.Clamp(c.R * factor, 0, 255),
            (byte)Math.Clamp(c.G * factor, 0, 255),
            (byte)Math.Clamp(c.B * factor, 0, 255)
        );
    }

    public override Plot3DHitTestResult? HitTest(Point pos, Rect bounds, Plot3DOptions options)
    {
        if (options.Series.Count == 0) return null;

        var projector = new Projector3D(bounds, options);
        Point3D? bestPt = null;
        Series3D? bestSeries = null;
        Point bestPos = default;
        double bestDistSq = double.MaxValue;
        double maxDistSq = 20.0 * 20.0;

        foreach (var s in options.Series)
        {
            foreach (var pt in s.Points)
            {
                var pr = projector.ProjectData(pt.X, pt.Y, pt.Z);
                if (!pr.IsVisible) continue;

                double dx = pr.ScreenPoint.X - pos.X;
                double dy = pr.ScreenPoint.Y - pos.Y;
                double distSq = dx * dx + dy * dy;

                if (distSq < maxDistSq && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestPt = pt;
                    bestSeries = s;
                    bestPos = pr.ScreenPoint;
                }
            }
        }

        if (bestPt == null) return null;

        return new Plot3DHitTestResult
        {
            Point = bestPt,
            Series = bestSeries,
            CanvasPosition = bestPos,
            DisplayText = $"Bar [{bestPt.Label}]: {bestPt.GetFormattedCoordinates()}",
            DistanceSquared = bestDistSq
        };
    }
}
