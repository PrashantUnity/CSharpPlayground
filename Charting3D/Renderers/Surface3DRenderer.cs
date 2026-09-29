using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using Vector3D = PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial.Vector3D;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public class Surface3DRenderer : Plot3DRendererBase
{
    private static readonly Dictionary<uint, IBrush> BrushCache = new();
    private static readonly Pen WireframePen = new(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 0.8);

    private static IBrush GetBrush(Color color)
    {
        uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (!BrushCache.TryGetValue(key, out var brush))
        {
            if (BrushCache.Count > 1024) BrushCache.Clear();
            brush = new SolidColorBrush(color);
            BrushCache[key] = brush;
        }
        return brush;
    }

    private readonly struct SurfaceQuad
    {
        public Point P00 { get; }
        public Point P10 { get; }
        public Point P11 { get; }
        public Point P01 { get; }
        public double Depth { get; }
        public Color Color { get; }

        public SurfaceQuad(Point p00, Point p10, Point p11, Point p01, double depth, Color color)
        {
            P00 = p00;
            P10 = p10;
            P11 = p11;
            P01 = p01;
            Depth = depth;
            Color = color;
        }
    }

    public override void Render(DrawingContext context, Rect bounds, Plot3DOptions options)
    {
        if (bounds.Width < 10 || bounds.Height < 10) return;
        if (options.Surface == null || options.Surface.ZValues.Length == 0) return;

        options.RecalculateBounds();
        var projector = new Projector3D(bounds, options);
        RenderEnvironment(context, projector, options);

        var surface = options.Surface;
        int resX = surface.ResolutionX;
        int resY = surface.ResolutionY;
        if (resX < 2 || resY < 2) return;

        double stepX = (surface.MaxX - surface.MinX) / (resX - 1);
        double stepY = (surface.MaxY - surface.MinY) / (resY - 1);

        var worldGrid = new Vector3D[resX, resY];
        var projGrid = new ProjectedPoint3D[resX, resY];

        for (int i = 0; i < resX; i++)
        {
            double x = surface.MinX + i * stepX;
            for (int j = 0; j < resY; j++)
            {
                double y = surface.MinY + j * stepY;
                double z = surface.ZValues[i, j];

                var w = projector.MapDataToWorld(x, z, y);
                worldGrid[i, j] = w;
                projGrid[i, j] = projector.ProjectWorld(w);
            }
        }

        double zRange = Math.Max(1e-9, surface.MaxZ - surface.MinZ);
        var quads = new List<SurfaceQuad>((resX - 1) * (resY - 1));

        for (int i = 0; i < resX - 1; i++)
        {
            for (int j = 0; j < resY - 1; j++)
            {
                var pr00 = projGrid[i, j];
                var pr10 = projGrid[i + 1, j];
                var pr11 = projGrid[i + 1, j + 1];
                var pr01 = projGrid[i, j + 1];

                if (!pr00.IsVisible && !pr10.IsVisible && !pr11.IsVisible && !pr01.IsVisible)
                    continue;

                var w00 = worldGrid[i, j];
                var w10 = worldGrid[i + 1, j];
                var w11 = worldGrid[i + 1, j + 1];
                var w01 = worldGrid[i, j + 1];

                var sideA = w10 - w00;
                var sideB = w01 - w00;
                var normal = Vector3D.Cross(sideA, sideB).Normalized;

                double dot = Vector3D.Dot(normal, LightDirection);
                double diffuse = Math.Max(0.0, dot);
                double lighting = 0.35 + 0.65 * diffuse;

                double avgZ = (surface.ZValues[i, j] + surface.ZValues[i + 1, j] +
                               surface.ZValues[i + 1, j + 1] + surface.ZValues[i, j + 1]) * 0.25;
                double t = (avgZ - surface.MinZ) / zRange;
                var baseColor = ColorMapService.GetColor(options.ColorMap, t);

                var shadedColor = Color.FromRgb(
                    (byte)Math.Clamp(baseColor.R * lighting, 0, 255),
                    (byte)Math.Clamp(baseColor.G * lighting, 0, 255),
                    (byte)Math.Clamp(baseColor.B * lighting, 0, 255)
                );

                double avgDepth = (pr00.Depth + pr10.Depth + pr11.Depth + pr01.Depth) * 0.25;

                quads.Add(new SurfaceQuad(
                    pr00.ScreenPoint, pr10.ScreenPoint, pr11.ScreenPoint, pr01.ScreenPoint,
                    avgDepth, shadedColor));
            }
        }

        // Sort back-to-front for Painter's algorithm
        quads.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        var pen = options.Wireframe ? WireframePen : null;

        foreach (var q in quads)
        {
            var geom = new StreamGeometry();
            using (var gc = geom.Open())
            {
                gc.BeginFigure(q.P00, true);
                gc.LineTo(q.P10);
                gc.LineTo(q.P11);
                gc.LineTo(q.P01);
                gc.EndFigure(true);
            }

            context.DrawGeometry(GetBrush(q.Color), pen, geom);
        }
    }

    public override Plot3DHitTestResult? HitTest(Point pos, Rect bounds, Plot3DOptions options)
    {
        if (bounds.Width < 10 || bounds.Height < 10 || options.Surface == null) return null;

        var projector = new Projector3D(bounds, options);
        var surface = options.Surface;
        int resX = surface.ResolutionX;
        int resY = surface.ResolutionY;
        if (resX < 2 || resY < 2) return null;

        double stepX = (surface.MaxX - surface.MinX) / (resX - 1);
        double stepY = (surface.MaxY - surface.MinY) / (resY - 1);

        double bestDistSq = double.MaxValue;
        Point3D? bestPt = null;
        Point bestPos = default;
        double maxDistSq = 20.0 * 20.0;

        int strideX = Math.Max(1, resX / 30);
        int strideY = Math.Max(1, resY / 30);

        for (int i = 0; i < resX; i += strideX)
        {
            double x = surface.MinX + i * stepX;
            for (int j = 0; j < resY; j += strideY)
            {
                double y = surface.MinY + j * stepY;
                double z = surface.ZValues[i, j];

                var pr = projector.ProjectData(x, z, y);
                if (!pr.IsVisible) continue;

                double dx = pr.ScreenPoint.X - pos.X;
                double dy = pr.ScreenPoint.Y - pos.Y;
                double distSq = dx * dx + dy * dy;

                if (distSq < maxDistSq && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestPt = new Point3D(x, y, z);
                    bestPos = pr.ScreenPoint;
                }
            }
        }

        if (bestPt == null) return null;

        return new Plot3DHitTestResult
        {
            Point = bestPt,
            CanvasPosition = bestPos,
            DisplayText = $"Surface: x={bestPt.X:0.##}, y={bestPt.Y:0.##}, z={bestPt.Z:0.##}",
            DistanceSquared = bestDistSq
        };
    }
}
