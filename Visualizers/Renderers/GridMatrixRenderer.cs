using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class GridMatrixRenderer : VisualizerRendererBase
{
    private const double CellGap = 3.0;
    private const double CoordHeaderSize = 22.0;

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var grid = GetEffectiveGrid(options);
        if (grid == null || grid.Rows == 0 || grid.Columns == 0) return;

        double cellSize = ComputeCellSize(bounds, grid, options);
        double gap = CellGap * Math.Max(0.2, options.Zoom);
        double coordSize = ShowCoordinates(grid, options) ? CoordHeaderSize : 0;

        double totalGridWidth = coordSize + grid.Columns * (cellSize + gap);
        double totalGridHeight = coordSize + grid.Rows * (cellSize + gap);

        // Center within bounds + pan offset, never pushing negative without user panning
        double startX = Math.Max(12, (bounds.Width - totalGridWidth) / 2.0) + options.PanOffsetX;
        double startY = Math.Max(12, (bounds.Height - totalGridHeight) / 2.0) + options.PanOffsetY;

        var activeCells = GetActiveCells(options);
        var changed = StepChanges.MatrixCells(PreviousSnapshot<GridMatrixData>(options), grid);
        bool showValues = grid.ShowValues && options.ShowValues;

        // 1. Column coordinate headers
        if (coordSize > 0)
        {
            for (int c = 0; c < grid.Columns; c++)
            {
                string header = c < grid.ColHeaders.Count ? grid.ColHeaders[c] : c.ToString();
                double cx = startX + coordSize + c * (cellSize + gap) + cellSize / 2.0;
                double cy = startY + coordSize / 2.0;
                var ft = CreateFormattedText(header, Math.Max(8, 10 * options.Zoom), MutedTextBrush);
                context.DrawText(ft, new Point(cx - ft.Width / 2.0, cy - ft.Height / 2.0));
            }
        }

        // 2. Row coordinate headers & grid cells
        var pendingArrows = new List<(Point Start, Point End, string? Label, string? Color)>();

        for (int r = 0; r < grid.Rows; r++)
        {
            if (coordSize > 0)
            {
                string header = r < grid.RowHeaders.Count ? grid.RowHeaders[r] : r.ToString();
                double rx = startX + coordSize / 2.0;
                double ry = startY + coordSize + r * (cellSize + gap) + cellSize / 2.0;
                var ft = CreateFormattedText(header, Math.Max(8, 10 * options.Zoom), MutedTextBrush);
                context.DrawText(ft, new Point(rx - ft.Width / 2.0, ry - ft.Height / 2.0));
            }

            for (int c = 0; c < grid.Columns; c++)
            {
                var cell = grid[r, c];
                double x = startX + coordSize + c * (cellSize + gap);
                double y = startY + coordSize + r * (cellSize + gap);
                var cellRect = new Rect(x, y, cellSize, cellSize);

                // Determine brush & border
                var (fillBrush, borderPen) = ResolveCellStyling(cell);

                // Draw cell background
                context.DrawRectangle(fillBrush, borderPen, new RoundedRect(cellRect, 4));
                if (changed.Contains((r, c)))
                {
                    DrawChangedOutline(context, cellRect, 4);
                }

                // Check active halo
                bool isActive = activeCells.Contains((r, c));
                if (isActive)
                {
                    DrawActiveHalo(context, cellRect);
                }

                // Draw cell main text
                if (showValues && !string.IsNullOrEmpty(cell.DisplayValue))
                {
                    var textBrush = DetermineTextBrush(cell);
                    double fontSize = Math.Max(9, (cellSize * 0.42));
                    var ft = CreateFormattedText(cell.DisplayValue, fontSize, textBrush, FontWeight.Bold);
                    context.DrawText(ft, new Point(x + (cellSize - ft.Width) / 2.0, y + (cellSize - ft.Height) / 2.0));
                }

                // Draw cell sub-label (e.g. costs, heuristic, DP value)
                if (!string.IsNullOrEmpty(cell.SubLabel) && cellSize >= 26)
                {
                    var subText = CreateFormattedText(cell.SubLabel, Math.Max(7.5, cellSize * 0.22), MutedTextBrush);
                    context.DrawText(subText, new Point(x + cellSize - subText.Width - 3, y + cellSize - subText.Height - 2));
                }

                // Collect predecessor / directional arrows
                foreach (var arrow in cell.Arrows)
                {
                    if (grid.IsInBounds(arrow.TargetRow, arrow.TargetCol))
                    {
                        double targetX = startX + coordSize + arrow.TargetCol * (cellSize + gap) + cellSize / 2.0;
                        double targetY = startY + coordSize + arrow.TargetRow * (cellSize + gap) + cellSize / 2.0;
                        pendingArrows.Add((
                            new Point(x + cellSize / 2.0, y + cellSize / 2.0),
                            new Point(targetX, targetY),
                            arrow.Label,
                            arrow.ColorHex));
                    }
                }
            }
        }

        // Draw directional arrows
        foreach (var (pStart, pEnd, _, color) in pendingArrows)
        {
            var pen = GetPen(color ?? "#38bdf8", 1.8);
            context.DrawLine(pen, pStart, pEnd);

            // Arrow head
            double angle = Math.Atan2(pEnd.Y - pStart.Y, pEnd.X - pStart.X);
            double headLen = Math.Min(9.0, cellSize * 0.28);
            var pArrow1 = new Point(pEnd.X - headLen * Math.Cos(angle - Math.PI / 6), pEnd.Y - headLen * Math.Sin(angle - Math.PI / 6));
            var pArrow2 = new Point(pEnd.X - headLen * Math.Cos(angle + Math.PI / 6), pEnd.Y - headLen * Math.Sin(angle + Math.PI / 6));
            context.DrawLine(pen, pEnd, pArrow1);
            context.DrawLine(pen, pEnd, pArrow2);
        }
    }

    public override VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options)
    {
        var grid = GetEffectiveGrid(options);
        if (grid == null) return null;

        double cellSize = ComputeCellSize(bounds, grid, options);
        double gap = CellGap * Math.Max(0.2, options.Zoom);
        double coordSize = ShowCoordinates(grid, options) ? CoordHeaderSize : 0;

        double totalGridWidth = coordSize + grid.Columns * (cellSize + gap);
        double totalGridHeight = coordSize + grid.Rows * (cellSize + gap);

        double startX = Math.Max(12, (bounds.Width - totalGridWidth) / 2.0) + options.PanOffsetX;
        double startY = Math.Max(12, (bounds.Height - totalGridHeight) / 2.0) + options.PanOffsetY;

        double relX = pointerPosition.X - (startX + coordSize);
        double relY = pointerPosition.Y - (startY + coordSize);

        if (relX < 0 || relY < 0) return null;

        int c = (int)(relX / (cellSize + gap));
        int r = (int)(relY / (cellSize + gap));

        if (!grid.IsInBounds(r, c)) return null;

        var cell = grid[r, c];
        string details = $"Value: '{cell.DisplayValue}'";
        if (cell.State != GridCellState.Default)
        {
            details += $" • State: {cell.State}";
        }
        else if (cell.Kind != CellKind.Standard)
        {
            details += $" • Kind: {cell.Kind}";
        }

        if (!string.IsNullOrEmpty(cell.SubLabel))
        {
            details += $" • {cell.SubLabel}";
        }

        if (cell.Heat.HasValue)
        {
            details += $" • Heat: {(int)Math.Round(cell.Heat.Value * 100)}%";
        }

        if (cell.ClusterId.HasValue)
        {
            details += $" • Island #{cell.ClusterId.Value}";
        }

        if (cell.Metadata.Count > 0)
        {
            foreach (var kvp in cell.Metadata)
            {
                details += $" • {kvp.Key}: {kvp.Value}";
            }
        }

        return new VisualizerHitTestResult
        {
            Title = $"Cell [{r}, {c}]",
            Details = details,
            Row = r,
            Col = c,
            CanvasPoint = pointerPosition,
            ColorHex = cell.CustomColor
        };
    }

    private static double ComputeCellSize(Rect bounds, GridMatrixData grid, VisualizerOptions options)
    {
        if (bounds.Width <= 20 || bounds.Height <= 20)
        {
            return grid.CellSize * Math.Max(0.2, options.Zoom);
        }

        double coordSize = ShowCoordinates(grid, options) ? CoordHeaderSize : 0;
        double availWidth = Math.Max(40, bounds.Width - coordSize - 32);
        double availHeight = Math.Max(40, bounds.Height - coordSize - 24);

        double maxCellW = (availWidth - (grid.Columns - 1) * CellGap) / Math.Max(1, grid.Columns);
        double maxCellH = (availHeight - (grid.Rows - 1) * CellGap) / Math.Max(1, grid.Rows);

        double fitSize = Math.Min(maxCellW, maxCellH);
        double baseSize = Math.Clamp(fitSize, 26.0, 56.0);

        return Math.Max(12.0, baseSize * Math.Max(0.2, options.Zoom));
    }

    // The header toggles flip the options, so they also reach the per-step snapshots a playback draws.
    private static bool ShowCoordinates(GridMatrixData grid, VisualizerOptions options) => grid.ShowCoordinates && options.ShowCoordinates;

    private static GridMatrixData? GetEffectiveGrid(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.Snapshot is GridMatrixData snapshot)
        {
            return snapshot;
        }
        return options.MatrixData;
    }

    private static HashSet<(int R, int C)> GetActiveCells(VisualizerOptions options)
    {
        var set = new HashSet<(int, int)>();
        var currentStep = options.Sequence?.CurrentStep;
        if (currentStep != null)
        {
            foreach (var cell in currentStep.ActiveCells)
            {
                set.Add(cell);
            }
        }
        return set;
    }

    private static (IBrush Fill, IPen Border) ResolveCellStyling(GridCell cell)
    {
        if (!string.IsNullOrEmpty(cell.CustomColor))
        {
            var brush = GetBrush(cell.CustomColor);
            var pen = GetPen(cell.BorderColor ?? cell.CustomColor, 1.2);
            return (brush, pen);
        }

        if (cell.Heat.HasValue)
        {
            string heatColor = VisualizerPaletteService.GetHeatmapColor(cell.Heat.Value);
            return (GetBrush(heatColor), GetPen("#334155", 1.0));
        }

        if (cell.State != GridCellState.Default)
        {
            var (fill, border, _) = VisualizerPaletteService.GetStateStyling(cell.State);
            return (GetBrush(fill), GetPen(border, 1.2));
        }

        switch (cell.Kind)
        {
            case CellKind.Water:
                return (GetBrush(VisualizerPaletteService.WaterColor), GetPen(VisualizerPaletteService.WaterBorderColor, 1.2));
            case CellKind.Land:
                return (GetBrush(VisualizerPaletteService.UnvisitedLandColor), GetPen(VisualizerPaletteService.UnvisitedLandBorderColor, 1.2));
            case CellKind.Wall:
                return (GetBrush(VisualizerPaletteService.WallColor), GetPen("#27272a", 1.2));
            case CellKind.Path:
                return (GetBrush(VisualizerPaletteService.PathColor), GetPen("#ca8a04", 1.5));
            case CellKind.Start:
                return (GetBrush(VisualizerPaletteService.StartColor), GetPen("#059669", 1.5));
            case CellKind.Target:
                return (GetBrush(VisualizerPaletteService.TargetColor), GetPen("#dc2626", 1.5));
            default:
                return (GetBrush("#1e293b"), GetPen("#334155", 1));
        }
    }

    private static IBrush DetermineTextBrush(GridCell cell)
    {
        if (!string.IsNullOrEmpty(cell.TextColor))
        {
            return GetBrush(cell.TextColor);
        }

        if (cell.State != GridCellState.Default)
        {
            var (_, _, text) = VisualizerPaletteService.GetStateStyling(cell.State);
            return GetBrush(text);
        }

        if (cell.Kind == CellKind.Water)
        {
            return GetBrush(VisualizerPaletteService.WaterTextColor);
        }
        return WhiteBrush;
    }
}
