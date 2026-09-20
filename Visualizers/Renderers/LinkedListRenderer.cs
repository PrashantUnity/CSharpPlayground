using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class LinkedListRenderer : VisualizerRendererBase
{
    private const double BoxWidth = 46.0;
    private const double BoxHeight = 34.0;
    private const double NextSlotWidth = 16.0;
    private const double ArrowGap = 26.0;

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var data = GetEffectiveData(options);
        if (data == null || data.Nodes.Count == 0) return;

        double scale = Math.Max(0.2, options.Zoom);
        double totalCellWidth = (BoxWidth + NextSlotWidth) * scale;
        double gap = ArrowGap * scale;
        double cellHeight = BoxHeight * scale;

        double totalChainWidth = data.Nodes.Count * (totalCellWidth + gap) + (data.HasCycle ? 0 : 50 * scale);
        double startX = Math.Max(20, bounds.Left + (bounds.Width - totalChainWidth) / 2.0 + options.PanOffsetX);
        double startY = Math.Max(20, bounds.Top + (bounds.Height - cellHeight) / 2.0 + options.PanOffsetY);

        var activeNodeIds = GetActiveNodeIds(options);
        var arrowPen = GetPen("#94a3b8", Math.Max(1.0, 1.5 * scale));
        var cyclePen = GetPen("#f43f5e", Math.Max(1.5, 2.0 * scale));

        var nodePositions = new List<Rect>();

        for (int i = 0; i < data.Nodes.Count; i++)
        {
            var node = data.Nodes[i];
            double x = startX + i * (totalCellWidth + gap);
            double y = startY;
            var cellRect = new Rect(x, y, totalCellWidth, cellHeight);
            nodePositions.Add(cellRect);

            bool isActive = activeNodeIds.Contains($"node_{i}") || node.IsActive;

            if (isActive)
            {
                DrawActiveHalo(context, cellRect);
            }

            // 1. Draw value section
            var valRect = new Rect(x, y, BoxWidth * scale, cellHeight);
            var fillBrush = node.IsCycleTarget ? GetBrush("#881337") : (isActive ? GetBrush("#1e3a8a") : GetBrush("#1e293b"));
            var borderPen = node.IsCycleTarget ? GetPen("#f43f5e", 1.8) : (isActive ? GetPen("#60a5fa", 1.8) : GetPen("#475569", 1));
            context.DrawRectangle(fillBrush, borderPen, new RoundedRect(valRect, 4, 0, 0, 4));

            var ftVal = CreateFormattedText(node.DisplayValue, Math.Max(8, 12 * scale), WhiteBrush, FontWeight.Bold);
            context.DrawText(ftVal, new Point(valRect.Left + (valRect.Width - ftVal.Width) / 2.0, valRect.Top + (valRect.Height - ftVal.Height) / 2.0));

            // 2. Draw next-pointer slot
            var nextRect = new Rect(x + BoxWidth * scale, y, NextSlotWidth * scale, cellHeight);
            var nextSlotBrush = GetBrush("#0f172a");
            context.DrawRectangle(nextSlotBrush, borderPen, new RoundedRect(nextRect, 0, 4, 4, 0));

            // Dot inside pointer slot
            var dotCenter = new Point(nextRect.Left + nextRect.Width / 2.0, nextRect.Top + nextRect.Height / 2.0);
            context.DrawEllipse(WhiteBrush, null, dotCenter, 2.5 * scale, 2.5 * scale);

            // 3. Draw forward arrow
            if (i < data.Nodes.Count - 1)
            {
                var arrowStart = dotCenter;
                var arrowEnd = new Point(x + totalCellWidth + gap, y + cellHeight / 2.0);
                context.DrawLine(arrowPen, arrowStart, arrowEnd);
                DrawArrowTip(context, arrowEnd, 1, 0, 6 * scale, arrowPen.Brush!);
            }
        }

        // Draw Null terminator or Cycle return arc
        if (!data.HasCycle && data.Nodes.Count > 0)
        {
            var lastRect = nodePositions[^1];
            double nullX = lastRect.Right + gap;
            var nullRect = new Rect(nullX, startY, 44 * scale, cellHeight);
            context.DrawRectangle(GetBrush("#0f172a"), GetPen("#334155", 1), new RoundedRect(nullRect, 4));

            var ftNull = CreateFormattedText("NULL", Math.Max(7, 10 * scale), MutedTextBrush, FontWeight.Bold);
            context.DrawText(ftNull, new Point(nullRect.Left + (nullRect.Width - ftNull.Width) / 2.0, nullRect.Top + (nullRect.Height - ftNull.Height) / 2.0));

            var arrowStart = new Point(lastRect.Right, startY + cellHeight / 2.0);
            var arrowEnd = new Point(nullRect.Left, startY + cellHeight / 2.0);
            context.DrawLine(arrowPen, arrowStart, arrowEnd);
            DrawArrowTip(context, arrowEnd, 1, 0, 6 * scale, arrowPen.Brush!);
        }
        else if (data.HasCycle && data.CycleTargetIndex.HasValue && data.CycleTargetIndex.Value < nodePositions.Count)
        {
            // Draw curved cycle return arc
            var lastRect = nodePositions[^1];
            var targetRect = nodePositions[data.CycleTargetIndex.Value];

            var arcStart = new Point(lastRect.Right, lastRect.Center.Y);
            var arcEnd = new Point(targetRect.Left + (BoxWidth * scale) / 2.0, targetRect.Bottom);

            double arcDrop = 40.0 * scale;
            var cp1 = new Point(arcStart.X + 24 * scale, arcStart.Y + arcDrop);
            var cp2 = new Point(arcEnd.X, arcEnd.Y + arcDrop);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(arcStart, isFilled: false);
                ctx.CubicBezierTo(cp1, cp2, arcEnd);
                ctx.EndFigure(isClosed: false);
            }
            context.DrawGeometry(null, cyclePen, geometry);
            DrawArrowTip(context, arcEnd, 0, -1, 7 * scale, cyclePen.Brush!);

            var ftCycle = CreateFormattedText("Cycle Loop", Math.Max(7, 9 * scale), cyclePen.Brush ?? WhiteBrush, FontWeight.Bold);
            context.DrawText(ftCycle, new Point((arcStart.X + arcEnd.X) / 2.0 - ftCycle.Width / 2.0, arcStart.Y + arcDrop + 4));
        }
    }

    private void DrawArrowTip(DrawingContext context, Point tip, double dirX, double dirY, double size, IBrush brush)
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
        var data = GetEffectiveData(options);
        if (data == null) return null;

        double scale = Math.Max(0.2, options.Zoom);
        double totalCellWidth = (BoxWidth + NextSlotWidth) * scale;
        double gap = ArrowGap * scale;
        double cellHeight = BoxHeight * scale;

        double totalChainWidth = data.Nodes.Count * (totalCellWidth + gap) + (data.HasCycle ? 0 : 50 * scale);
        double startX = Math.Max(20, bounds.Left + (bounds.Width - totalChainWidth) / 2.0 + options.PanOffsetX);
        double startY = Math.Max(20, bounds.Top + (bounds.Height - cellHeight) / 2.0 + options.PanOffsetY);

        for (int i = 0; i < data.Nodes.Count; i++)
        {
            double x = startX + i * (totalCellWidth + gap);
            var rect = new Rect(x, startY, totalCellWidth, cellHeight);

            if (rect.Contains(pointerPosition))
            {
                var node = data.Nodes[i];
                return new VisualizerHitTestResult
                {
                    Title = $"Linked Node [{node.DisplayValue}]",
                    Details = $"Index: {node.Index} • Next: {(node.NextIndex.HasValue ? $"[{node.NextIndex.Value}]" : "null")}{(node.IsCycleTarget ? " • Cycle Target" : "")}",
                    NodeId = $"node_{i}",
                    CanvasPoint = pointerPosition
                };
            }
        }

        return null;
    }

    private static LinkedListData? GetEffectiveData(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.CustomData is LinkedListData custom)
        {
            return custom;
        }
        return options.LinkedListData;
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
