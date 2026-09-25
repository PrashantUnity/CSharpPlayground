using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class LinkedListRenderer : VisualizerRendererBase
{
    private const double BoxWidth = 46.0;
    private const double BoxHeight = 34.0;
    private const double NextSlotWidth = 16.0;
    private const double ArrowGap = 26.0;
    private const double ChainGap = 30.0;
    private const double MarkerWidth = 44.0;
    private const double RowGap = 64.0;

    // EndBoxes: the NULL (or "…") box after each chain's last node, keyed by that node.
    private sealed record Layout(double Scale, Rect[] Cells, Rect? NullBox, Dictionary<int, Rect> EndBoxes);

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var data = GetEffectiveData(options);
        if (data == null || data.Nodes.Count == 0) return;

        var layout = ComputeLayout(bounds, options, data);
        double scale = layout.Scale;
        var activeNodeIds = GetActiveNodeIds(options);
        var changed = StepChanges.LinkedListNodes(PreviousSnapshot<LinkedListData>(options), data);
        var arrowPen = GetPen("#94a3b8", Math.Max(1.0, 1.5 * scale));
        var cyclePen = GetPen("#f43f5e", Math.Max(1.5, 2.0 * scale));
        int last = data.Nodes.Count - 1;

        // 1. Nodes: value box plus next-pointer slot
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            var node = data.Nodes[i];
            var cell = layout.Cells[i];
            bool isActive = activeNodeIds.Contains($"node_{i}") || node.IsActive;
            if (isActive)
            {
                DrawActiveHalo(context, cell);
            }

            var valRect = new Rect(cell.X, cell.Y, BoxWidth * scale, cell.Height);
            var slotRect = new Rect(valRect.Right, cell.Y, NextSlotWidth * scale, cell.Height);
            var fillBrush = node.IsCycleTarget ? GetBrush("#881337")
                : isActive ? GetBrush("#1e3a8a")
                : !string.IsNullOrEmpty(node.Color) ? GetBrush(node.Color)
                : GetBrush("#1e293b");
            var borderPen = node.IsCycleTarget ? GetPen("#f43f5e", 1.8) : (isActive ? GetPen("#60a5fa", 1.8) : GetPen("#475569", 1));
            context.DrawRectangle(fillBrush, borderPen, new RoundedRect(valRect, 4, 0, 0, 4));
            context.DrawRectangle(GetBrush("#0f172a"), borderPen, new RoundedRect(slotRect, 0, 4, 4, 0));
            if (changed.Contains(i))
            {
                DrawChangedOutline(context, cell, 4);
            }

            var ftVal = CreateFormattedText(node.DisplayValue, Math.Max(8, 12 * scale), WhiteBrush, FontWeight.Bold);
            context.DrawText(ftVal, new Point(valRect.Left + (valRect.Width - ftVal.Width) / 2.0, valRect.Top + (valRect.Height - ftVal.Height) / 2.0));

            // A null next pointer in the middle of the drawing is a slash, the usual box-and-pointer notation.
            if (node.NextIndex == null && !node.ContinuesBeyondView && !layout.EndBoxes.ContainsKey(i))
            {
                double inset = 3.5 * scale;
                context.DrawLine(arrowPen, new Point(slotRect.Left + inset, slotRect.Bottom - inset), new Point(slotRect.Right - inset, slotRect.Top + inset));
            }
            else
            {
                context.DrawEllipse(WhiteBrush, null, slotRect.Center, 2.5 * scale, 2.5 * scale);
            }
        }

        // 2. Links, following each node's real next pointer
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            if (data.Nodes[i].NextIndex is not int target) continue;

            bool isCycleLink = data.HasCycle && i == (data.CycleSourceIndex ?? last) && target == data.CycleTargetIndex;
            var labelPoint = DrawLink(context, layout, i, target, isCycleLink ? cyclePen : arrowPen);
            if (isCycleLink)
            {
                var ftCycle = CreateFormattedText("Cycle Loop", Math.Max(7, 9 * scale), cyclePen.Brush ?? WhiteBrush, FontWeight.Bold);
                context.DrawText(ftCycle, new Point(labelPoint.X - ftCycle.Width / 2.0, labelPoint.Y + 4));
            }
        }

        // 3. End of each chain: NULL, or "…" when the list goes on past the drawing limit
        foreach (var (index, endBox) in layout.EndBoxes)
        {
            var endCell = layout.Cells[index];
            var from = new Point(endCell.Right - NextSlotWidth * scale / 2.0, endCell.Center.Y);
            var to = new Point(endBox.Left, endBox.Center.Y);
            context.DrawLine(arrowPen, from, to);
            DrawArrowTip(context, to, 1, 0, 6 * scale, arrowPen.Brush!);
            DrawMarkerBox(context, endBox, data.Nodes[index].ContinuesBeyondView ? "…" : "NULL", scale);
        }

        if (layout.NullBox is { } nullBox)
        {
            DrawMarkerBox(context, nullBox, "null", scale);
        }

        // 4. Named pointers (prev, curr, next, slow, fast…) stacked above the node they point at
        foreach (var group in data.Pointers.GroupBy(p => p.Index))
        {
            Rect? anchor = group.Key >= 0 && group.Key < layout.Cells.Length ? layout.Cells[group.Key] : layout.NullBox;
            if (anchor is not { } rect) continue;

            double centerX = group.Key >= 0 ? rect.Left + BoxWidth * scale / 2.0 : rect.Center.X;
            double bottom = rect.Top - 4 * scale;
            foreach (var pointer in group)
            {
                var ft = CreateFormattedText(pointer.Name, Math.Max(8.5, 10 * scale), WhiteBrush, FontWeight.Bold);
                var badge = new Rect(centerX - (ft.Width + 10) / 2.0, bottom - (ft.Height + 3), ft.Width + 10, ft.Height + 3);
                context.DrawRectangle(GetBrush(pointer.Color), null, badge, 4, 4);
                context.DrawText(ft, new Point(badge.X + 5, badge.Y + 1.5));
                bottom = badge.Top - 2;
            }
        }
    }

    // Straight to the next slot; backward links (reversal, cycles) curve under the row, forward jumps over it.
    private Point DrawLink(DrawingContext context, Layout layout, int from, int to, IPen pen)
    {
        double scale = layout.Scale;
        var fromCell = layout.Cells[from];
        var targetCell = layout.Cells[to];
        var slotCenter = new Point(fromCell.Right - NextSlotWidth * scale / 2.0, fromCell.Center.Y);

        // Into another row (two chains joining): an S-curve from this row's edge to the target's.
        if (Math.Abs(fromCell.Y - targetCell.Y) > 0.5)
        {
            bool down = targetCell.Y > fromCell.Y;
            var exit = new Point(slotCenter.X, down ? fromCell.Bottom : fromCell.Top);
            var entry = new Point(targetCell.Left + BoxWidth * scale / 2.0, down ? targetCell.Top : targetCell.Bottom);
            double pull = (entry.Y - exit.Y) / 2.0;

            var curve = new StreamGeometry();
            using (var ctx = curve.Open())
            {
                ctx.BeginFigure(exit, isFilled: false);
                ctx.CubicBezierTo(new Point(exit.X, exit.Y + pull), new Point(entry.X, entry.Y - pull), entry);
                ctx.EndFigure(isClosed: false);
            }
            context.DrawGeometry(null, pen, curve);
            DrawArrowTip(context, entry, 0, down ? 1 : -1, 7 * scale, pen.Brush!);
            return new Point((exit.X + entry.X) / 2.0, (exit.Y + entry.Y) / 2.0);
        }

        if (to == from + 1)
        {
            var end = new Point(targetCell.Left, targetCell.Center.Y);
            context.DrawLine(pen, slotCenter, end);
            DrawArrowTip(context, end, 1, 0, 6 * scale, pen.Brush!);
            return new Point((slotCenter.X + end.X) / 2.0, end.Y);
        }

        bool below = to < from;
        double bend = (22 + 8 * Math.Min(Math.Abs(to - from), 6)) * scale * (below ? 1 : -1);
        var start = new Point(slotCenter.X, below ? fromCell.Bottom : fromCell.Top);
        var finish = new Point(targetCell.Left + BoxWidth * scale / 2.0, below ? targetCell.Bottom : targetCell.Top);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false);
            ctx.CubicBezierTo(new Point(start.X, start.Y + bend), new Point(finish.X, finish.Y + bend), finish);
            ctx.EndFigure(isClosed: false);
        }
        context.DrawGeometry(null, pen, geometry);
        DrawArrowTip(context, finish, 0, below ? -1 : 1, 7 * scale, pen.Brush!);

        return new Point((start.X + finish.X) / 2.0, start.Y + bend * 0.75);
    }

    private void DrawMarkerBox(DrawingContext context, Rect box, string text, double scale)
    {
        context.DrawRectangle(GetBrush("#0f172a"), GetPen("#334155", 1), new RoundedRect(box, 4));
        var ft = CreateFormattedText(text, Math.Max(7, 10 * scale), MutedTextBrush, FontWeight.Bold);
        context.DrawText(ft, new Point(box.Left + (box.Width - ft.Width) / 2.0, box.Top + (box.Height - ft.Height) / 2.0));
    }

    private static Layout ComputeLayout(Rect bounds, VisualizerOptions options, LinkedListData data)
    {
        double scale = Math.Max(0.2, options.Zoom);
        double cellWidth = (BoxWidth + NextSlotWidth) * scale;
        double cellHeight = BoxHeight * scale;
        double gap = ArrowGap * scale;
        double chainGap = ChainGap * scale;
        double markerWidth = MarkerWidth * scale;
        bool hasNullPointer = data.Pointers.Any(p => p.Index < 0);

        // Rows of node indices: one row, or one per chain when chains are stacked.
        var chainStarts = new HashSet<int>(data.ChainStarts);
        var rows = new List<List<int>> { new() };
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            if (data.StackChains && i > 0 && chainStarts.Contains(i)) rows.Add(new List<int>());
            rows[^1].Add(i);
        }

        // Stacked rows each end in their own NULL box; a single row only after its last node.
        var ends = new HashSet<int>();
        foreach (var row in rows)
        {
            int end = row[^1];
            if (data.Nodes[end].NextIndex == null && (data.StackChains || end == data.Nodes.Count - 1)) ends.Add(end);
        }

        double RowWidth(List<int> row) =>
            row.Count * (cellWidth + gap) - gap
            + (data.StackChains ? 0 : row.Skip(1).Count(chainStarts.Contains) * chainGap)
            + (ends.Contains(row[^1]) ? gap + markerWidth : 0);

        double lead = hasNullPointer ? markerWidth + gap : 0;
        double total = lead + rows.Max(RowWidth);
        double rowPitch = cellHeight + RowGap * scale;
        double height = cellHeight + (rows.Count - 1) * rowPitch;

        double left = Math.Max(20, (bounds.Width - total) / 2.0) + options.PanOffsetX;
        double top = Math.Max(20, (bounds.Height - height) / 2.0) + options.PanOffsetY;

        Rect? nullBox = hasNullPointer ? new Rect(left, top, markerWidth, cellHeight) : null;

        var cells = new Rect[data.Nodes.Count];
        var endBoxes = new Dictionary<int, Rect>();
        for (int r = 0; r < rows.Count; r++)
        {
            double x = left + lead;
            double y = top + r * rowPitch;
            foreach (int i in rows[r])
            {
                if (!data.StackChains && i > 0 && chainStarts.Contains(i)) x += chainGap;
                cells[i] = new Rect(x, y, cellWidth, cellHeight);
                x += cellWidth + gap;
            }

            int end = rows[r][^1];
            if (ends.Contains(end)) endBoxes[end] = new Rect(cells[end].Right + gap, y, markerWidth, cellHeight);
        }

        return new Layout(scale, cells, nullBox, endBoxes);
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
        if (data == null || data.Nodes.Count == 0) return null;

        var layout = ComputeLayout(bounds, options, data);
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            if (!layout.Cells[i].Contains(pointerPosition)) continue;

            var node = data.Nodes[i];
            string next = node.NextIndex is int j ? $"[{j}] {data.Nodes[j].DisplayValue}"
                : node.ContinuesBeyondView ? "(past the drawing limit)"
                : "null";
            var pointerNames = data.Pointers.Where(p => p.Index == i).Select(p => p.Name).ToList();
            string details = $"Index: {i} • Next: {next}";
            if (pointerNames.Count > 0) details += $" • Pointers: {string.Join(", ", pointerNames)}";
            if (node.IsCycleTarget) details += " • Cycle Target";

            return new VisualizerHitTestResult
            {
                Title = $"Linked Node [{node.DisplayValue}]",
                Details = details,
                NodeId = $"node_{i}",
                CanvasPoint = pointerPosition
            };
        }

        return null;
    }

    private static LinkedListData? GetEffectiveData(VisualizerOptions options) =>
        options.Sequence?.CurrentStep?.Snapshot as LinkedListData ?? options.LinkedListData;

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
