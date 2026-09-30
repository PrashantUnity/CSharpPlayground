using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public class Graph3DRenderer : Plot3DRendererBase
{
    private readonly struct ProjectedNode
    {
        public Graph3DNode Node { get; }
        public Point ScreenPos { get; }
        public double Depth { get; }
        public Color Color { get; }

        public ProjectedNode(Graph3DNode node, Point screenPos, double depth, Color color)
        {
            Node = node;
            ScreenPos = screenPos;
            Depth = depth;
            Color = color;
        }
    }

    public override void Render(DrawingContext context, Rect bounds, Plot3DOptions options)
    {
        if (options.Graph == null || options.Graph.Nodes.Count == 0) return;

        bool allZero = true;
        foreach (var node in options.Graph.Nodes)
        {
            if (Math.Abs(node.X) > 1e-4 || Math.Abs(node.Y) > 1e-4 || Math.Abs(node.Z) > 1e-4)
            {
                allZero = false;
                break;
            }
        }
        if (allZero)
        {
            PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts.ForceDirected3DLayout.ComputeLayout(options.Graph);
        }

        options.RecalculateBounds();
        var projector = new Projector3D(bounds, options);
        RenderEnvironment(context, projector, options);

        var graph = options.Graph;
        var nodeMap = new Dictionary<string, (Graph3DNode node, ProjectedPoint3D pr)>();

        var defaultNodeColor = ParseColor(options.PrimaryColor, Color.FromRgb(78, 201, 176));

        // 1. Project all nodes
        var projectedNodes = new List<ProjectedNode>(graph.Nodes.Count);
        foreach (var node in graph.Nodes)
        {
            var pr = projector.ProjectData(node.X, node.Y, node.Z);
            nodeMap[node.Id] = (node, pr);

            if (pr.IsVisible)
            {
                var col = !string.IsNullOrEmpty(node.Color)
                    ? ParseColor(node.Color, defaultNodeColor)
                    : defaultNodeColor;
                projectedNodes.Add(new ProjectedNode(node, pr.ScreenPoint, pr.Depth, col));
            }
        }

        // 2. Draw Edges
        var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(140, 100, 149, 237)), 1.4);
        var arrowBrush = new SolidColorBrush(Color.FromArgb(180, 100, 149, 237));

        foreach (var edge in graph.Edges)
        {
            if (!nodeMap.TryGetValue(edge.FromId, out var u) || !nodeMap.TryGetValue(edge.ToId, out var v))
                continue;

            if (!u.pr.IsVisible && !v.pr.IsVisible) continue;

            var p1 = u.pr.ScreenPoint;
            var p2 = v.pr.ScreenPoint;

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-3) continue;

            var currentEdgePen = !string.IsNullOrEmpty(edge.Color)
                ? new Pen(new SolidColorBrush(ParseColor(edge.Color, Colors.CornflowerBlue)), 1.4)
                : edgePen;

            context.DrawLine(currentEdgePen, p1, p2);

            // Draw arrowhead if directed
            if (edge.IsDirected && dist > 15)
            {
                double nodeRadius = v.node.Radius;
                double endX = p2.X - (dx / dist) * nodeRadius;
                double endY = p2.Y - (dy / dist) * nodeRadius;
                DrawArrowHead(context, new Point(endX, endY), dx / dist, dy / dist, 7.0, arrowBrush);
            }
        }

        // 3. Draw Nodes (Depth sorted back-to-front)
        projectedNodes.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 20, 24, 34)), 1.2);
        var highlightBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        var textBrush = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));

        foreach (var pNode in projectedNodes)
        {
            double depthScale = Math.Clamp(1.0 - pNode.Depth * 0.35, 0.4, 2.0);
            double r = Math.Max(3.0, pNode.Node.Radius * depthScale);

            var fillBrush = new SolidColorBrush(pNode.Color);
            context.DrawEllipse(fillBrush, borderPen, pNode.ScreenPos, r, r);

            // Specular highlight
            if (r >= 4.0)
            {
                var hlPos = new Point(pNode.ScreenPos.X - r * 0.3, pNode.ScreenPos.Y - r * 0.3);
                context.DrawEllipse(highlightBrush, null, hlPos, r * 0.35, r * 0.35);
            }

            // Node label
            if (options.ShowLabels && !string.IsNullOrEmpty(pNode.Node.Label) && r >= 5.0)
            {
                var ft = new FormattedText(
                    pNode.Node.Label,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    LabelTypeface,
                    10.5,
                    textBrush);
                context.DrawText(ft, new Point(pNode.ScreenPos.X + r + 3, pNode.ScreenPos.Y - ft.Height * 0.5));
            }
        }
    }

    private static void DrawArrowHead(DrawingContext context, Point tip, double nx, double ny, double size, IBrush brush)
    {
        double px = -ny;
        double py = nx;

        var p1 = new Point(tip.X - nx * size + px * (size * 0.5), tip.Y - ny * size + py * (size * 0.5));
        var p2 = new Point(tip.X - nx * size - px * (size * 0.5), tip.Y - ny * size - py * (size * 0.5));

        var geom = new StreamGeometry();
        using (var gc = geom.Open())
        {
            gc.BeginFigure(tip, true);
            gc.LineTo(p1);
            gc.LineTo(p2);
            gc.EndFigure(true);
        }
        context.DrawGeometry(brush, null, geom);
    }

    public override Plot3DHitTestResult? HitTest(Point pos, Rect bounds, Plot3DOptions options)
    {
        if (options.Graph == null || options.Graph.Nodes.Count == 0) return null;

        var projector = new Projector3D(bounds, options);
        Graph3DNode? bestNode = null;
        Point bestPos = default;
        double bestDistSq = double.MaxValue;
        double maxDistSq = 22.0 * 22.0;

        foreach (var node in options.Graph.Nodes)
        {
            var pr = projector.ProjectData(node.X, node.Y, node.Z);
            if (!pr.IsVisible) continue;

            double dx = pr.ScreenPoint.X - pos.X;
            double dy = pr.ScreenPoint.Y - pos.Y;
            double distSq = dx * dx + dy * dy;

            if (distSq < maxDistSq && distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestNode = node;
                bestPos = pr.ScreenPoint;
            }
        }

        if (bestNode == null) return null;

        return new Plot3DHitTestResult
        {
            Node = bestNode,
            CanvasPosition = bestPos,
            DisplayText = $"Node [{bestNode.Id}] {bestNode.Label} ({bestNode.X:0.##}, {bestNode.Y:0.##}, {bestNode.Z:0.##})",
            DistanceSquared = bestDistSq
        };
    }
}
