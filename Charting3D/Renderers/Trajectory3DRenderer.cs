using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public class Trajectory3DRenderer : Plot3DRendererBase
{
    private readonly struct Segment3D
    {
        public Point Start { get; }
        public Point End { get; }
        public double Depth { get; }
        public Color Color { get; }

        public Segment3D(Point start, Point end, double depth, Color color)
        {
            Start = start;
            End = end;
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

        var segments = new List<Segment3D>();

        foreach (var s in options.Series)
        {
            int count = s.Points.Count;
            if (count < 2) continue;

            for (int i = 0; i < count - 1; i++)
            {
                var ptA = s.Points[i];
                var ptB = s.Points[i + 1];

                var prA = projector.ProjectData(ptA.X, ptA.Y, ptA.Z);
                var prB = projector.ProjectData(ptB.X, ptB.Y, ptB.Z);

                if (!prA.IsVisible && !prB.IsVisible) continue;

                double t = (double)i / (count - 1);
                var segColor = ColorMapService.GetColor(options.ColorMap, t);
                double avgDepth = (prA.Depth + prB.Depth) * 0.5;

                segments.Add(new Segment3D(prA.ScreenPoint, prB.ScreenPoint, avgDepth, segColor));
            }
        }

        // Sort back-to-front
        segments.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        foreach (var seg in segments)
        {
            var pen = new Pen(new SolidColorBrush(seg.Color), 1.8);
            context.DrawLine(pen, seg.Start, seg.End);
        }

        // Highlight head of each trajectory
        var headHalo = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        var headBrush = new SolidColorBrush(Colors.White);

        foreach (var s in options.Series)
        {
            if (s.Points.Count == 0) continue;
            var last = s.Points[^1];
            var pr = projector.ProjectData(last.X, last.Y, last.Z);
            if (pr.IsVisible)
            {
                context.DrawEllipse(headHalo, null, pr.ScreenPoint, 7, 7);
                context.DrawEllipse(headBrush, null, pr.ScreenPoint, 3.5, 3.5);
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
        double maxDistSq = 16.0 * 16.0;

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
            DisplayText = $"Trajectory: {bestPt.GetFormattedCoordinates()}",
            DistanceSquared = bestDistSq
        };
    }
}
