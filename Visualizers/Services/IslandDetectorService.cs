using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class IslandDetectorService
{
    private static readonly (int dr, int dc)[] Dirs4 = new[]
    {
        (-1, 0), (1, 0), (0, -1), (0, 1)
    };

    private static readonly (int dr, int dc)[] Dirs8 = new[]
    {
        (-1, 0), (1, 0), (0, -1), (0, 1),
        (-1, -1), (-1, 1), (1, -1), (1, 1)
    };

    public static List<IslandData> Detect(GridMatrixData grid, bool fourDirectional = true)
    {
        var (islands, _) = DetectAndGenerateSteps(grid, recordSteps: false, fourDirectional: fourDirectional);
        return islands;
    }

    public static (List<IslandData> Islands, VisualizerSequence Sequence) DetectAndGenerateSteps(
        GridMatrixData grid,
        bool recordSteps = true,
        bool fourDirectional = true)
    {
        var islands = new List<IslandData>();
        var sequence = new VisualizerSequence();
        var dirs = fourDirectional ? Dirs4 : Dirs8;

        int rows = grid.Rows;
        int cols = grid.Columns;
        bool[,] visited = new bool[rows, cols];

        if (recordSteps)
        {
            var initialStep = new VisualizerStep(0, $"🔍 Initializing Island Scan • Grid: {rows}×{cols}", VisualizerKind.Islands)
            {
                Snapshot = grid.Clone()
            };
            initialStep.AuxiliaryInfo["Dimensions"] = $"{rows}×{cols}";
            initialStep.AuxiliaryInfo["Status"] = "Ready";
            sequence.AddStep(initialStep);
        }

        int islandCounter = 0;
        int totalLandCells = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (visited[r, c]) continue;

                var cell = grid[r, c];
                bool isLand = cell.Kind == CellKind.Land ||
                              cell.DisplayValue == "1" ||
                              (cell.RawValue is int i && i == 1) ||
                              (cell.RawValue is bool b && b);

                if (!isLand)
                {
                    visited[r, c] = true;
                    continue;
                }

                // Discovered new island!
                islandCounter++;
                string islandColor = VisualizerPaletteService.GetIslandColor(islandCounter);
                var island = new IslandData(islandCounter, $"Island #{islandCounter}", islandColor);
                int minR = r, maxR = r, minC = c, maxC = c;
                int perimeter = 0;

                var queue = new Queue<(int R, int C)>();
                queue.Enqueue((r, c));
                visited[r, c] = true;
                cell.ClusterId = islandCounter;
                cell.CustomColor = islandColor;
                island.Cells.Add((r, c));
                totalLandCells++;

                if (recordSteps)
                {
                    var step = new VisualizerStep(
                        sequence.TotalSteps,
                        $"🏝️ Discovered Island #{islandCounter} at ({r}, {c}) • Starting BFS...",
                        VisualizerKind.Islands)
                    {
                        Snapshot = grid.Clone()
                    };
                    step.ActiveCells.Add((r, c));
                    step.AuxiliaryInfo["Island"] = $"#{islandCounter}";
                    step.AuxiliaryInfo["Position"] = $"({r}, {c})";
                    step.AuxiliaryInfo["Queue Size"] = queue.Count.ToString();
                    sequence.AddStep(step);
                }

                while (queue.Count > 0)
                {
                    var curr = queue.Dequeue();
                    int currR = curr.R;
                    int currC = curr.C;

                    minR = Math.Min(minR, currR);
                    maxR = Math.Max(maxR, currR);
                    minC = Math.Min(minC, currC);
                    maxC = Math.Max(maxC, currC);

                    // Check neighbors
                    foreach (var (dr, dc) in dirs)
                    {
                        int nr = currR + dr;
                        int nc = currC + dc;

                        if (!grid.IsInBounds(nr, nc))
                        {
                            perimeter++;
                            continue;
                        }

                        var neighborCell = grid[nr, nc];
                        bool neighborIsLand = neighborCell.Kind == CellKind.Land ||
                                              neighborCell.DisplayValue == "1" ||
                                              (neighborCell.RawValue is int ni && ni == 1) ||
                                              (neighborCell.RawValue is bool nb && nb);

                        if (!neighborIsLand)
                        {
                            perimeter++;
                            continue;
                        }

                        if (!visited[nr, nc])
                        {
                            visited[nr, nc] = true;
                            neighborCell.ClusterId = islandCounter;
                            neighborCell.CustomColor = islandColor;
                            island.Cells.Add((nr, nc));
                            queue.Enqueue((nr, nc));
                            totalLandCells++;

                            if (recordSteps)
                            {
                                var step = new VisualizerStep(
                                    sequence.TotalSteps,
                                    $"📍 Island #{islandCounter} expanded to ({nr}, {nc}) • Queue: {queue.Count}",
                                    VisualizerKind.Islands)
                                {
                                    Snapshot = grid.Clone()
                                };
                                step.ActiveCells.Add((nr, nc));
                                step.AuxiliaryInfo["Island"] = $"#{islandCounter}";
                                step.AuxiliaryInfo["Current Cell"] = $"({nr}, {nc})";
                                step.AuxiliaryInfo["Queue Size"] = queue.Count.ToString();
                                step.AuxiliaryInfo["Island Area"] = island.Cells.Count.ToString();
                                sequence.AddStep(step);
                            }
                        }
                    }
                }

                island.Perimeter = perimeter;
                island.BoundingBox = (minR, minC, maxR, maxC);
                islands.Add(island);

                if (recordSteps)
                {
                    var step = new VisualizerStep(
                        sequence.TotalSteps,
                        $"✅ Island #{islandCounter} BFS complete • Area: {island.Area} cells, Perimeter: {island.Perimeter}",
                        VisualizerKind.Islands)
                    {
                        Snapshot = grid.Clone()
                    };
                    step.ActiveCells.AddRange(island.Cells);
                    step.AuxiliaryInfo["Completed Island"] = $"#{islandCounter}";
                    step.AuxiliaryInfo["Area"] = island.Area.ToString();
                    step.AuxiliaryInfo["Perimeter"] = island.Perimeter.ToString();
                    sequence.AddStep(step);
                }
            }
        }

        grid.Islands = islands;

        if (recordSteps)
        {
            var summaryStep = new VisualizerStep(
                sequence.TotalSteps,
                $"🎉 Scan complete: {islands.Count} Islands identified • Total Land: {totalLandCells} cells",
                VisualizerKind.Islands)
            {
                Snapshot = grid.Clone()
            };
            summaryStep.AuxiliaryInfo["Total Islands"] = islands.Count.ToString();
            summaryStep.AuxiliaryInfo["Total Land Cells"] = totalLandCells.ToString();
            sequence.AddStep(summaryStep);
        }

        return (islands, sequence);
    }
}
