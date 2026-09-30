using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public class Scatter3DRenderer : Plot3DRendererBase
{
    private readonly struct ProjectedItem
    {
        public Point3D Point { get; }
        public Series3D Series { get; }
        public Point ScreenPos { get; }
        public double Depth { get; }
        public Color Color { get; }

        public ProjectedItem(Point3D point, Series3D series, Point screenPos, double depth, Color color)
        {
            Point = point;
            Series = series;
            ScreenPos = screenPos;
            Depth = depth;
            Color = color;
        }
    }

    public override void Render(DrawingContext context, Rect bounds, Plot3DOptions options)
    {
        options.RecalculateBounds();
        var projector = new Projector3D(bounds, options);
        RenderEnvironment(context, projector, options);

        if (options.Series.Count == 0) return;

        var items = new List<ProjectedItem>();

        var defaultColor = ParseColor(options.PrimaryColor, Color.FromRgb(78, 201, 176));

        foreach (var s in options.Series)
        {
            var seriesColor = ParseColor(s.Color, defaultColor);

            foreach (var pt in s.Points)
            {
                var pr = projector.ProjectData(pt.X, pt.Y, pt.Z);
                if (!pr.IsVisible) continue;

                var ptColor = !string.IsNullOrEmpty(pt.CustomColor)
                    ? ParseColor(pt.CustomColor, seriesColor)
                    : seriesColor;

                items.Add(new ProjectedItem(pt, s, pr.ScreenPoint, pr.Depth, ptColor));
            }
        }

        // Sort back-to-front (descending depth)
        items.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 20, 20, 25)), 1.0);
        var highlightBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));

        foreach (var item in items)
        {
            double baseRadius = Math.Max(2.5, item.Point.Size * 4.5);
            double depthScale = Math.Clamp(1.0 - item.Depth * 0.35, 0.4, 2.0);
            double r = baseRadius * depthScale;

            var brush = new SolidColorBrush(item.Color);
            context.DrawEllipse(brush, borderPen, item.ScreenPos, r, r);

            // Subtle 3D specular highlight at top-left
            if (r >= 3.5)
            {
                var hlPos = new Point(item.ScreenPos.X - r * 0.3, item.ScreenPos.Y - r * 0.3);
                context.DrawEllipse(highlightBrush, null, hlPos, r * 0.35, r * 0.35);
            }
        }
    }

    public override Plot3DHitTestResult? HitTest(Point pos, Rect bounds, Plot3DOptions options)
    {
        if (options.Series.Count == 0) return null;

        var projector = new Projector3D(bounds, options);
        Point3D? bestPt = null;
        Series3D? bestSeries = null;
        Point bestPos = default;
        double bestDistSq = double.MaxValue;
        double maxDistSq = 18.0 * 18.0;

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

        string display = !string.IsNullOrEmpty(bestPt.Label)
            ? $"{bestPt.Label}: {bestPt.GetFormattedCoordinates()}"
            : $"{bestSeries?.Name ?? "Point"}: {bestPt.GetFormattedCoordinates()}";

        return new Plot3DHitTestResult
        {
            Point = bestPt,
            Series = bestSeries,
            CanvasPosition = bestPos,
            DisplayText = display,
            DistanceSquared = bestDistSq
        };
    }
}
