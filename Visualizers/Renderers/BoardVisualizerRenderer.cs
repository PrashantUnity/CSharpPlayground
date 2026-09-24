using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class BoardVisualizerRenderer : VisualizerRendererBase
{
    private const double CellGap = 2.0;
    private const double HeaderSize = 24.0;

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var board = GetEffectiveBoard(options);
        if (board == null || board.Rows == 0 || board.Columns == 0) return;

        double coordSize = board.ShowCoordinates ? HeaderSize : 0;
        double cellSize = ComputeCellSize(bounds, board, options);
        double gap = CellGap * Math.Max(0.2, options.Zoom);

        double totalWidth = coordSize + board.Columns * (cellSize + gap);
        double totalHeight = coordSize + board.Rows * (cellSize + gap);

        double startX = Math.Max(12, (bounds.Width - totalWidth) / 2.0) + options.PanOffsetX;
        double startY = Math.Max(12, (bounds.Height - totalHeight) / 2.0) + options.PanOffsetY;

        // Draw Column Headers
        if (board.ShowCoordinates)
        {
            for (int c = 0; c < board.Columns; c++)
            {
                string header = c < board.ColHeaders.Count ? board.ColHeaders[c] : c.ToString();
                double cx = startX + coordSize + c * (cellSize + gap) + cellSize / 2.0;
                double cy = startY + coordSize / 2.0;
                var ft = CreateFormattedText(header, Math.Max(9, 11 * options.Zoom), MutedTextBrush);
                context.DrawText(ft, new Point(cx - ft.Width / 2.0, cy - ft.Height / 2.0));
            }
        }

        // Draw Rows & Cells
        var pendingArrows = new List<(Point Start, Point End, string? Label, string? Color)>();
        var changed = StepChanges.BoardCells(PreviousSnapshot<BoardVisualizerData>(options), board);

        for (int r = 0; r < board.Rows; r++)
        {
            if (board.ShowCoordinates)
            {
                string header = r < board.RowHeaders.Count ? board.RowHeaders[r] : r.ToString();
                double rx = startX + coordSize / 2.0;
                double ry = startY + coordSize + r * (cellSize + gap) + cellSize / 2.0;
                var ft = CreateFormattedText(header, Math.Max(9, 11 * options.Zoom), MutedTextBrush);
                context.DrawText(ft, new Point(rx - ft.Width / 2.0, ry - ft.Height / 2.0));
            }

            for (int c = 0; c < board.Columns; c++)
            {
                var cell = board[r, c];
                double x = startX + coordSize + c * (cellSize + gap);
                double y = startY + coordSize + r * (cellSize + gap);
                var cellRect = new Rect(x, y, cellSize, cellSize);

                // Fill & border
                var (fill, border) = ResolveBoardCellStyling(cell, board.IsCheckerboard);
                context.DrawRectangle(fill, border, new RoundedRect(cellRect, 3));
                if (changed.Contains((r, c)))
                {
                    DrawChangedOutline(context, cellRect, 3);
                }

                if (cell.IsActive)
                {
                    DrawActiveHalo(context, cellRect);
                }

                // Cell Value
                if (!string.IsNullOrEmpty(cell.Value))
                {
                    var textBrush = !string.IsNullOrEmpty(cell.TextColor)
                        ? GetBrush(cell.TextColor)
                        : (cell.IsConflict ? GetBrush("#ef4444") : WhiteBrush);

                    double fontSize = Math.Max(10, cellSize * 0.44);
                    var ft = CreateFormattedText(cell.Value, fontSize, textBrush, FontWeight.Bold);
                    context.DrawText(ft, new Point(x + (cellSize - ft.Width) / 2.0, y + (cellSize - ft.Height) / 2.0));
                }

                // Sub-label (e.g. DP cost or state)
                if (!string.IsNullOrEmpty(cell.SubLabel) && cellSize >= 32)
                {
                    var subText = CreateFormattedText(cell.SubLabel, Math.Max(8, cellSize * 0.22), MutedTextBrush);
                    context.DrawText(subText, new Point(x + cellSize - subText.Width - 3, y + cellSize - subText.Height - 2));
                }

                // Collect arrows
                foreach (var arrow in cell.Arrows)
                {
                    if (board.IsInBounds(arrow.TargetRow, arrow.TargetCol))
                    {
                        double targetX = startX + coordSize + arrow.TargetCol * (cellSize + gap) + cellSize / 2.0;
                        double targetY = startY + coordSize + arrow.TargetRow * (cellSize + gap) + cellSize / 2.0;
                        pendingArrows.Add((new Point(x + cellSize / 2.0, y + cellSize / 2.0), new Point(targetX, targetY), arrow.Label, arrow.ColorHex));
                    }
                }
            }
        }

        // Draw pending arrows (e.g. DP transitions)
        foreach (var (pStart, pEnd, label, color) in pendingArrows)
        {
            var pen = GetPen(color ?? "#38bdf8", 1.8);
            context.DrawLine(pen, pStart, pEnd);

            // Arrow head
            double angle = Math.Atan2(pEnd.Y - pStart.Y, pEnd.X - pStart.X);
            double headLen = 8.0;
            var pArrow1 = new Point(pEnd.X - headLen * Math.Cos(angle - Math.PI / 6), pEnd.Y - headLen * Math.Sin(angle - Math.PI / 6));
            var pArrow2 = new Point(pEnd.X - headLen * Math.Cos(angle + Math.PI / 6), pEnd.Y - headLen * Math.Sin(angle + Math.PI / 6));
            context.DrawLine(pen, pEnd, pArrow1);
            context.DrawLine(pen, pEnd, pArrow2);
        }
    }

    public override VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options)
    {
        var board = GetEffectiveBoard(options);
        if (board == null || board.Rows == 0 || board.Columns == 0) return null;

        double coordSize = board.ShowCoordinates ? HeaderSize : 0;
        double cellSize = ComputeCellSize(bounds, board, options);
        double gap = CellGap * Math.Max(0.2, options.Zoom);

        double totalWidth = coordSize + board.Columns * (cellSize + gap);
        double totalHeight = coordSize + board.Rows * (cellSize + gap);

        double startX = Math.Max(12, (bounds.Width - totalWidth) / 2.0) + options.PanOffsetX;
        double startY = Math.Max(12, (bounds.Height - totalHeight) / 2.0) + options.PanOffsetY;

        double relX = pointerPosition.X - (startX + coordSize);
        double relY = pointerPosition.Y - (startY + coordSize);
        if (relX < 0 || relY < 0) return null;

        int c = (int)(relX / (cellSize + gap));
        int r = (int)(relY / (cellSize + gap));

        if (!board.IsInBounds(r, c)) return null;

        var cell = board[r, c];
        return new VisualizerHitTestResult
        {
            Title = $"Square [{r}, {c}]",
            Details = $"Value: '{cell.Value}'{(string.IsNullOrEmpty(cell.SubLabel) ? "" : $" • {cell.SubLabel}")}",
            Row = r,
            Col = c,
            CanvasPoint = pointerPosition,
            ColorHex = cell.FillColor
        };
    }

    private static double ComputeCellSize(Rect bounds, BoardVisualizerData board, VisualizerOptions options)
    {
        if (bounds.Width <= 20 || bounds.Height <= 20) return 38.0 * Math.Max(0.2, options.Zoom);

        double coordSize = board.ShowCoordinates ? HeaderSize : 0;
        double availWidth = Math.Max(40, bounds.Width - coordSize - 32);
        double availHeight = Math.Max(40, bounds.Height - coordSize - 24);

        double maxW = (availWidth - (board.Columns - 1) * CellGap) / Math.Max(1, board.Columns);
        double maxH = (availHeight - (board.Rows - 1) * CellGap) / Math.Max(1, board.Rows);

        double fitSize = Math.Min(maxW, maxH);
        return Math.Clamp(fitSize, 24.0, 56.0) * Math.Max(0.2, options.Zoom);
    }

    private static BoardVisualizerData? GetEffectiveBoard(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.Snapshot is BoardVisualizerData snapshot)
        {
            return snapshot;
        }
        return options.BoardData;
    }

    private static (IBrush Fill, IPen Border) ResolveBoardCellStyling(BoardCell cell, bool isCheckerboard)
    {
        if (!string.IsNullOrEmpty(cell.FillColor))
        {
            return (GetBrush(cell.FillColor), GetPen(cell.BorderColor ?? cell.FillColor, 1.2));
        }

        if (cell.IsConflict)
        {
            return (GetBrush("#450a0a"), GetPen("#ef4444", 1.5));
        }
        if (cell.IsTarget)
        {
            return (GetBrush("#064e3b"), GetPen("#10b981", 1.5));
        }

        if (isCheckerboard)
        {
            bool isDark = (cell.Row + cell.Col) % 2 == 1;
            return isDark
                ? (GetBrush("#1e293b"), GetPen("#334155", 1.0))
                : (GetBrush("#334155"), GetPen("#475569", 1.0));
        }

        return (GetBrush("#111827"), GetPen("#1f2937", 1.0));
    }
}
