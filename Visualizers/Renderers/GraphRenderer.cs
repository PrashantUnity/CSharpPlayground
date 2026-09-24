using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class GraphRenderer : VisualizerRendererBase
{
    private const double BaseNodeRadius = 18.0;

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var graph = GetEffectiveGraph(options);
        if (graph == null || graph.Nodes.Count == 0) return;

        double radius = BaseNodeRadius * Math.Max(0.2, options.Zoom);
        GraphLayoutEngine.ComputeLayout(graph, bounds.Width, bounds.Height);

        var nodeMap = new Dictionary<string, GraphNodeData>();
        foreach (var node in graph.Nodes) nodeMap[node.Id] = node;

        var activeNodeIds = GetActiveNodeIds(options);
        var changedNodeIds = StepChanges.GraphNodes(PreviousSnapshot<GraphData>(options), graph);

        // 1. Draw edges
        foreach (var edge in graph.Edges)
        {
            if (!nodeMap.TryGetValue(edge.FromId, out var u) || !nodeMap.TryGetValue(edge.ToId, out var v))
                continue;

            var (ux, uy) = Project(u, bounds, options);
            var (vx, vy) = Project(v, bounds, options);

            double dx = vx - ux;
            double dy = vy - uy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-4) continue;

            // Trim edge so it terminates at the boundary of node v
            double endX = vx - (dx / dist) * radius;
            double endY = vy - (dy / dist) * radius;
            double startX = ux + (dx / dist) * radius;
            double startY = uy + (dy / dist) * radius;

            var (edgeColor, thickness, isDashed) = VisualizerPaletteService.GetGraphEdgeStyling(edge.State, edge.IsActive);
            var pen = GetPen(edgeColor, thickness * Math.Max(1.0, options.Zoom));

            context.DrawLine(pen, new Point(startX, startY), new Point(endX, endY));

            // Arrow head if directed
            if (edge.IsDirected)
            {
                DrawArrowHead(context, new Point(endX, endY), dx / dist, dy / dist, Math.Max(6, 8 * options.Zoom), pen.Brush!);
            }

            // Weight label
            if (edge.Weight.HasValue)
            {
                double midX = (startX + endX) / 2.0;
                double midY = (startY + endY) / 2.0;
                var ft = CreateFormattedText(edge.Weight.Value.ToString(), Math.Max(8, 9.5 * options.Zoom), MutedTextBrush);
                context.DrawText(ft, new Point(midX - ft.Width / 2.0, midY - ft.Height - 2));
            }
        }

        // 2. Draw nodes
        foreach (var node in graph.Nodes)
        {
            var (centerX, centerY) = Project(node, bounds, options);
            var center = new Point(centerX, centerY);
            bool isActive = activeNodeIds.Contains(node.Id) || node.IsActive || node.State == GraphNodeState.Current;

            if (isActive)
            {
                DrawActiveCircleHalo(context, center, radius);
            }

            (string fillHex, string borderHex, string textHex) styling;
            if (!string.IsNullOrEmpty(node.Color))
            {
                styling = (node.Color, "#64748b", "#ffffff");
            }
            else if (node.State != GraphNodeState.Default)
            {
                styling = VisualizerPaletteService.GetGraphNodeStyling(node.State);
            }
            else if (isActive)
            {
                styling = VisualizerPaletteService.GetGraphNodeStyling(GraphNodeState.Current);
            }
            else if (node.IsVisited)
            {
                styling = VisualizerPaletteService.GetGraphNodeStyling(GraphNodeState.Visited);
            }
            else
            {
                styling = ("#1e293b", "#475569", "#ffffff");
            }

            var fillBrush = GetBrush(styling.fillHex);
            var borderPen = GetPen(styling.borderHex, isActive ? 2.2 : 1.4);
            context.DrawEllipse(fillBrush, borderPen, center, radius, radius);
            if (changedNodeIds.Contains(node.Id))
            {
                DrawChangedRing(context, center, radius);
            }

            if (!string.IsNullOrEmpty(node.Label))
            {
                double fontSize = Math.Max(8, 11 * options.Zoom);
                var textBrush = GetBrush(styling.textHex);
                var ft = CreateFormattedText(node.Label, fontSize, textBrush, FontWeight.Bold);
                context.DrawText(ft, new Point(center.X - ft.Width / 2.0, center.Y - ft.Height / 2.0));
            }

            // Floating PointerLabel badge pill (e.g. [src], [curr], [u], [v])
            if (!string.IsNullOrEmpty(node.PointerLabel))
            {
                double badgeFontSize = Math.Max(8.5, 10.0 * options.Zoom);
                var badgeFt = CreateFormattedText(node.PointerLabel, badgeFontSize, WhiteBrush, FontWeight.Bold);
                double badgePadX = 5.0;
                double badgePadY = 2.0;
                double badgeW = badgeFt.Width + badgePadX * 2;
                double badgeH = badgeFt.Height + badgePadY * 2;
                double badgeX = center.X - badgeW / 2.0;
                double badgeY = center.Y - radius - badgeH - 3.0;

                var badgeRect = new Rect(badgeX, badgeY, badgeW, badgeH);
                string pointerColor = VisualizerPaletteService.GetPointerColor(node.PointerLabel);
                var badgeBg = GetBrush(pointerColor);
                context.DrawRectangle(badgeBg, null, badgeRect, 4, 4);
                context.DrawText(badgeFt, new Point(badgeX + badgePadX, badgeY + badgePadY));
            }

            // SubLabel annotation beneath the node (e.g. d=4, in=0)
            if (!string.IsNullOrEmpty(node.SubLabel))
            {
                double subFontSize = Math.Max(8.0, 9.0 * options.Zoom);
                var subFt = CreateFormattedText(node.SubLabel, subFontSize, GetBrush("#94a3b8"), FontWeight.Medium);
                double subX = center.X - subFt.Width / 2.0;
                double subY = center.Y + radius + 3.0;
                context.DrawText(subFt, new Point(subX, subY));
            }
        }
    }

    // The layout fills the canvas at 100%; zoom spreads nodes out around the canvas centre, then pan applies.
    private static (double X, double Y) Project(GraphNodeData node, Rect bounds, VisualizerOptions options)
    {
        double cx = bounds.Width / 2.0;
        double cy = bounds.Height / 2.0;
        return (cx + (node.X - cx) * options.Zoom + options.PanOffsetX,
                cy + (node.Y - cy) * options.Zoom + options.PanOffsetY);
    }

    private static GraphData? GetEffectiveGraph(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.Snapshot is GraphData snapshot)
        {
            return snapshot;
        }
        return options.GraphData;
    }

    private void DrawArrowHead(DrawingContext context, Point tip, double dirX, double dirY, double size, IBrush brush)
    {
        double normX = -dirY;
        double normY = dirX;

        var p1 = tip;
        var p2 = new Point(tip.X - dirX * size + normX * (size / 2.0), tip.Y - dirY * size + normY * (size / 2.0));
        var p3 = new Point(tip.X - dirX * size - normX * (size / 2.0), tip.Y - dirY * size - normY * (size / 2.0));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(p1, isFilled: true);
            ctx.LineTo(p2);
            ctx.LineTo(p3);
            ctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(brush, null, geometry);
    }

    public override VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options)
    {
        var graph = GetEffectiveGraph(options);
        if (graph == null) return null;

        double radius = BaseNodeRadius * Math.Max(0.2, options.Zoom);
        GraphLayoutEngine.ComputeLayout(graph, bounds.Width, bounds.Height);

        foreach (var node in graph.Nodes)
        {
            var (cx, cy) = Project(node, bounds, options);
            double dx = pointerPosition.X - cx;
            double dy = pointerPosition.Y - cy;

            if (dx * dx + dy * dy <= radius * radius)
            {
                string details = $"State: {node.State}";
                if (!string.IsNullOrEmpty(node.PointerLabel)) details += $" • Pointer: {node.PointerLabel}";
                if (!string.IsNullOrEmpty(node.SubLabel)) details += $" • Info: {node.SubLabel}";
                details += $" • ID: {node.Id}";

                return new VisualizerHitTestResult
                {
                    Title = $"Vertex [{node.Label}]",
                    Details = details,
                    NodeId = node.Id,
                    CanvasPoint = pointerPosition,
                    ColorHex = node.Color
                };
            }
        }

        return null;
    }

    private static HashSet<string> GetActiveNodeIds(VisualizerOptions options)
    {
        var set = new HashSet<string>();
        var currentStep = options.Sequence?.CurrentStep;
        if (currentStep != null)
        {
            foreach (var id in currentStep.ActiveNodeIds) set.Add(id);
        }
        return set;
    }
}
