using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class TreeRenderer : VisualizerRendererBase
{
    private const double BaseNodeRadius = 22.0;
    private const double BaseSiblingGap = 24.0;
    private const double BaseLevelSpacing = 64.0;

    // Room under a node for its sub-label (the "= 5" of a recursion call), which never shrinks below a readable size.
    private const double MinLevelGap = 20.0;

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var root = GetEffectiveTree(options);
        if (root == null) return;

        var (radius, layoutWidth, layoutHeight) = Layout(root, options.Zoom);
        var (offsetX, offsetY) = ComputeOffsets(root, bounds, options, radius, layoutWidth, layoutHeight);

        var activeNodeIds = GetActiveNodeIds(options);
        var changedNodeIds = StepChanges.TreeNodes(PreviousSnapshot<TreeNodeData>(options), root);
        var branchPen = GetPen("#475569", Math.Max(1.0, 1.5 * options.Zoom));

        // 1. Draw branch connectors
        DrawBranches(context, root, offsetX, offsetY, branchPen);

        // 2. Draw nodes
        DrawNodes(context, root, offsetX, offsetY, radius, activeNodeIds, changedNodeIds, options.Zoom);
    }

    // Spacing grows with the nodes, so zooming in never stacks one level on top of the next.
    internal static (double Radius, double Width, double Height) Layout(TreeNodeData root, double zoom)
    {
        double scale = Math.Max(0.2, zoom);
        double radius = BaseNodeRadius * scale;
        double levelSpacing = Math.Max(BaseLevelSpacing * scale, 2 * radius + MinLevelGap);
        var (width, height) = TreeLayoutEngine.ComputeLayout(root, radius, BaseSiblingGap * scale, levelSpacing);
        return (radius, width, height);
    }

    // Centred when it fits; a tree bigger than the canvas (deep recursion) slides so the current node stays in view.
    private static (double X, double Y) ComputeOffsets(TreeNodeData root, Rect bounds, VisualizerOptions options, double radius, double layoutWidth, double layoutHeight)
    {
        double offsetX = Math.Max(12, (bounds.Width - layoutWidth) / 2.0);
        double offsetY = Math.Max(12, (bounds.Height - layoutHeight) / 2.0);

        if (FindFocus(root, options) is { } focus)
        {
            double margin = radius + 24;
            offsetX -= Math.Max(0, focus.X + offsetX - (bounds.Width - margin));
            offsetY -= Math.Max(0, focus.Y + offsetY - (bounds.Height - margin));
        }

        return (offsetX + options.PanOffsetX, offsetY + options.PanOffsetY);
    }

    private static TreeNodeData? FindFocus(TreeNodeData root, VisualizerOptions options)
    {
        var activeIds = options.Sequence?.CurrentStep?.ActiveNodeIds;
        TreeNodeData? current = null;
        var pending = new Stack<TreeNodeData>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (activeIds != null && activeIds.Contains(node.Id)) return node;
            if (current == null && node.State == TreeNodeState.Current) current = node;
            for (int i = node.Children.Count - 1; i >= 0; i--) pending.Push(node.Children[i]);
        }

        return current;
    }

    private static TreeNodeData? GetEffectiveTree(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.Snapshot is TreeNodeData snapshot)
        {
            return snapshot;
        }
        return options.TreeData;
    }

    private void DrawBranches(DrawingContext context, TreeNodeData node, double ox, double oy, IPen pen)
    {
        var pStart = new Point(node.X + ox, node.Y + oy);
        foreach (var child in node.Children)
        {
            var pEnd = new Point(child.X + ox, child.Y + oy);
            context.DrawLine(pen, pStart, pEnd);
            DrawBranches(context, child, ox, oy, pen);
        }
    }

    private void DrawNodes(
        DrawingContext context,
        TreeNodeData node,
        double ox,
        double oy,
        double radius,
        HashSet<string> activeNodeIds,
        HashSet<string> changedNodeIds,
        double zoom)
    {
        var center = new Point(node.X + ox, node.Y + oy);
        bool isActive = activeNodeIds.Contains(node.Id) || node.IsActive || node.State == TreeNodeState.Current;

        if (isActive)
        {
            DrawActiveCircleHalo(context, center, radius);
        }

        // Resolve styling based on node state or custom color
        (string fillHex, string borderHex, string textHex) styling;
        if (!string.IsNullOrEmpty(node.CustomColor))
        {
            styling = (node.CustomColor, "#64748b", "#ffffff");
        }
        else if (node.State != TreeNodeState.Default)
        {
            styling = VisualizerPaletteService.GetTreeNodeStyling(node.State);
        }
        else if (isActive)
        {
            styling = VisualizerPaletteService.GetTreeNodeStyling(TreeNodeState.Current);
        }
        else if (node.IsVisited)
        {
            styling = VisualizerPaletteService.GetTreeNodeStyling(TreeNodeState.Visited);
        }
        else
        {
            styling = ("#1e293b", "#64748b", "#ffffff");
        }

        var fillBrush = GetBrush(styling.fillHex);
        var borderPen = GetPen(styling.borderHex, isActive ? 2.2 : 1.5);

        context.DrawEllipse(fillBrush, borderPen, center, radius, radius);
        if (changedNodeIds.Contains(node.Id))
        {
            DrawChangedRing(context, center, radius);
        }

        // Value text
        if (!string.IsNullOrEmpty(node.DisplayValue))
        {
            double fontSize = Math.Max(10, 12 * zoom);
            var textBrush = GetBrush(styling.textHex);
            var ft = CreateFormattedText(node.DisplayValue, fontSize, textBrush, FontWeight.Bold);
            context.DrawText(ft, new Point(center.X - ft.Width / 2.0, center.Y - ft.Height / 2.0));
        }

        // Floating PointerLabel badge (e.g. [root], [curr], [p], [q])
        if (!string.IsNullOrEmpty(node.PointerLabel))
        {
            double badgeFontSize = Math.Max(9.0, 10.5 * zoom);
            var badgeFt = CreateFormattedText(node.PointerLabel, badgeFontSize, WhiteBrush, FontWeight.Bold);
            double badgePadX = 6.0;
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

        // SubLabel annotation beneath the node circle (e.g. [0, 20], sum=22)
        if (!string.IsNullOrEmpty(node.SubLabel))
        {
            double subFontSize = Math.Max(8.5, 9.5 * zoom);
            var subFt = CreateFormattedText(node.SubLabel, subFontSize, GetBrush("#94a3b8"), FontWeight.Medium);
            double subX = center.X - subFt.Width / 2.0;
            double subY = center.Y + radius + 3.0;
            context.DrawText(subFt, new Point(subX, subY));
        }

        foreach (var child in node.Children)
        {
            DrawNodes(context, child, ox, oy, radius, activeNodeIds, changedNodeIds, zoom);
        }
    }

    public override VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options)
    {
        var root = GetEffectiveTree(options);
        if (root == null) return null;

        var (radius, layoutWidth, layoutHeight) = Layout(root, options.Zoom);
        var (offsetX, offsetY) = ComputeOffsets(root, bounds, options, radius, layoutWidth, layoutHeight);

        return FindNodeAtPoint(root, pointerPosition, offsetX, oy: offsetY, radius);
    }

    private VisualizerHitTestResult? FindNodeAtPoint(TreeNodeData node, Point pt, double ox, double oy, double radius)
    {
        double cx = node.X + ox;
        double cy = node.Y + oy;
        double dx = pt.X - cx;
        double dy = pt.Y - cy;

        if (dx * dx + dy * dy <= radius * radius)
        {
            string details = $"Depth: {node.Depth} • State: {node.State}";
            if (!string.IsNullOrEmpty(node.PointerLabel)) details += $" • Pointer: {node.PointerLabel}";
            if (!string.IsNullOrEmpty(node.SubLabel)) details += $" • SubLabel: {node.SubLabel}";
            details += $" • Children: {node.Children.Count}";

            return new VisualizerHitTestResult
            {
                Title = $"Node [{node.DisplayValue}]",
                Details = details,
                NodeId = node.Id,
                CanvasPoint = pt,
                ColorHex = node.CustomColor
            };
        }

        foreach (var child in node.Children)
        {
            var res = FindNodeAtPoint(child, pt, ox, oy, radius);
            if (res != null) return res;
        }

        return null;
    }

    private static HashSet<string> GetActiveNodeIds(VisualizerOptions options)
    {
        var set = new HashSet<string>();
        var currentStep = options.Sequence?.CurrentStep;
        if (currentStep != null)
        {
            foreach (var id in currentStep.ActiveNodeIds)
            {
                set.Add(id);
            }
        }
        return set;
    }
}
