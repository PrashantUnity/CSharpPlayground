using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>
/// Pluggable matrix traversal strategies (BFS, DFS, Dijkstra, A*, Flood Fill).
/// Each strategy accepts user-defined predicates for walls, costs, heuristics, and goals.
/// </summary>
public static class MatrixTraversalEngine
{
    public static MatrixTracker RunBfs(
        GridMatrixData grid,
        (int Row, int Col) start,
        Func<GridCell, bool>? isTarget = null,
        Func<GridCell, GridCell, bool>? canVisit = null,
        string? title = null,
        bool fourWay = true)
    {
        var tracker = MatrixTracker.CreateUnlinked(grid, title ?? "BFS Matrix Traversal");
        tracker.SetStart(start.Row, start.Col);

        var queue = new Queue<(int R, int C)>();
        var visited = new bool[grid.Rows, grid.Columns];
        var parent = new Dictionary<(int, int), (int, int)>();

        queue.Enqueue(start);
        visited[start.Row, start.Col] = true;
        tracker.Snapshot($"Initialized BFS at start ({start.Row}, {start.Col})", new { QueueSize = 1 });

        (int R, int C)? targetFound = null;

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            var currCell = grid[curr.R, curr.C];

            tracker.Visit(curr.R, curr.C, $"Exploring cell ({curr.R}, {curr.C}) • Remaining in queue: {queue.Count}");

            if (isTarget != null && isTarget(currCell))
            {
                targetFound = curr;
                tracker.SetTarget(curr.R, curr.C);
                break;
            }

            foreach (var next in tracker.GetNeighbors(curr.R, curr.C, fourWay: fourWay))
            {
                if (visited[next.Row, next.Col]) continue;

                var nextCell = grid[next.Row, next.Col];
                if (nextCell.State == GridCellState.Wall) continue;

                if (canVisit != null && !canVisit(currCell, nextCell)) continue;

                visited[next.Row, next.Col] = true;
                parent[next] = curr;
                queue.Enqueue(next);

                tracker.Enqueue(next.Row, next.Col, from: curr,
                    note: $"Discovered ({next.Row}, {next.Col}) from ({curr.R}, {curr.C})",
                    aux: new { QueueSize = queue.Count });
            }
        }

        if (targetFound.HasValue)
        {
            var path = tracker.TracePath(parent, targetFound.Value, start);
            tracker.MarkPath(path, $"Target reached! Shortest path: {path.Count} steps");
        }

        return tracker;
    }

    public static MatrixTracker RunDfs(
        GridMatrixData grid,
        (int Row, int Col) start,
        Func<GridCell, bool>? isTarget = null,
        Func<GridCell, GridCell, bool>? canVisit = null,
        string? title = null,
        bool fourWay = true)
    {
        var tracker = MatrixTracker.CreateUnlinked(grid, title ?? "DFS Matrix Traversal");
        tracker.SetStart(start.Row, start.Col);

        var visited = new bool[grid.Rows, grid.Columns];
        var parent = new Dictionary<(int, int), (int, int)>();
        (int R, int C)? targetFound = null;

        tracker.Snapshot($"Starting DFS from ({start.Row}, {start.Col})");

        bool DfsInternal((int R, int C) curr)
        {
            visited[curr.R, curr.C] = true;
            var currCell = grid[curr.R, curr.C];
            tracker.Visit(curr.R, curr.C, $"DFS visiting ({curr.R}, {curr.C})");

            if (isTarget != null && isTarget(currCell))
            {
                targetFound = curr;
                tracker.SetTarget(curr.R, curr.C);
                return true;
            }

            foreach (var next in tracker.GetNeighbors(curr.R, curr.C, fourWay: fourWay))
            {
                if (visited[next.Row, next.Col]) continue;

                var nextCell = grid[next.Row, next.Col];
                if (nextCell.State == GridCellState.Wall) continue;
                if (canVisit != null && !canVisit(currCell, nextCell)) continue;

                parent[next] = curr;
                if (DfsInternal(next)) return true;

                // Backtrack
                tracker.Backtrack(next.Row, next.Col, $"Backtracked from ({next.Row}, {next.Col}) to ({curr.R}, {curr.C})");
            }

            return false;
        }

        DfsInternal(start);

        if (targetFound.HasValue)
        {
            var path = tracker.TracePath(parent, targetFound.Value, start);
            tracker.MarkPath(path, $"Target found via DFS path ({path.Count} steps)");
        }

        return tracker;
    }

    public static MatrixTracker RunAStar(
        GridMatrixData grid,
        (int Row, int Col) start,
        (int Row, int Col) target,
        Func<GridCell, GridCell, double>? getCost = null,
        Func<(int R, int C), (int R, int C), double>? heuristic = null,
        string? title = null,
        bool fourWay = true)
    {
        heuristic ??= ((a, b) => Math.Abs(a.R - b.R) + Math.Abs(a.C - b.C));
        getCost ??= ((_, _) => 1.0);

        var tracker = MatrixTracker.CreateUnlinked(grid, title ?? "A* Pathfinding Search");
        tracker.SetStart(start.Row, start.Col);
        tracker.SetTarget(target.Row, target.Col);

        var gScore = new Dictionary<(int, int), double> { [start] = 0.0 };
        var fScore = new Dictionary<(int, int), double> { [start] = heuristic(start, target) };
        var parent = new Dictionary<(int, int), (int, int)>();

        var openSet = new PriorityQueue<(int R, int C), double>();
        openSet.Enqueue(start, fScore[start]);

        var closedSet = new HashSet<(int, int)>();

        tracker.Snapshot($"Initialized A* with Start ({start.Row}, {start.Col}) and Target ({target.Row}, {target.Col})");

        while (openSet.Count > 0)
        {
            var curr = openSet.Dequeue();
            if (closedSet.Contains(curr)) continue;
            closedSet.Add(curr);

            double currentG = gScore[curr];
            tracker.Visit(curr.R, curr.C,
                $"A* evaluating ({curr.R}, {curr.C}) • g={currentG:0.#}, f={fScore.GetValueOrDefault(curr):0.#}",
                subLabel: $"g:{currentG:0.#}");

            if (curr == target)
            {
                var path = tracker.TracePath(parent, target, start);
                tracker.MarkPath(path, $"A* Optimal Path Found! Cost: {currentG:0.#}, Steps: {path.Count}");
                return tracker;
            }

            var currCell = grid[curr.R, curr.C];
            foreach (var next in tracker.GetNeighbors(curr.R, curr.C, fourWay: fourWay))
            {
                if (closedSet.Contains(next)) continue;

                var nextCell = grid[next.Row, next.Col];
                if (nextCell.State == GridCellState.Wall) continue;

                double tentativeG = currentG + getCost(currCell, nextCell);
                if (tentativeG < gScore.GetValueOrDefault(next, double.PositiveInfinity))
                {
                    parent[next] = curr;
                    gScore[next] = tentativeG;
                    double h = heuristic(next, target);
                    double f = tentativeG + h;
                    fScore[next] = f;

                    openSet.Enqueue(next, f);

                    tracker.Enqueue(next.Row, next.Col, from: curr,
                        note: $"A* discovered ({next.Row}, {next.Col}) • f={f:0.#} (g={tentativeG:0.#}, h={h:0.#})",
                        subLabel: $"f:{f:0.#}",
                        aux: new { G = tentativeG, H = h, F = f });
                }
            }
        }

        tracker.Snapshot("A* Search exhausted without reaching target");
        return tracker;
    }

    public static MatrixTracker RunFloodFill(
        GridMatrixData grid,
        (int Row, int Col) start,
        string fillHexColor,
        Func<GridCell, bool>? belongsToRegion = null,
        string? title = null,
        bool fourWay = true)
    {
        var tracker = MatrixTracker.CreateUnlinked(grid, title ?? "Flood Fill Region Coloring");
        if (!grid.IsInBounds(start.Row, start.Col)) return tracker;

        var startCell = grid[start.Row, start.Col];
        string originalVal = startCell.DisplayValue;
        belongsToRegion ??= (cell => cell.DisplayValue == originalVal);

        var queue = new Queue<(int R, int C)>();
        var visited = new bool[grid.Rows, grid.Columns];

        queue.Enqueue(start);
        visited[start.Row, start.Col] = true;

        tracker.Snapshot($"Starting Flood Fill from ({start.Row}, {start.Col}) with color {fillHexColor}");

        int filledCount = 0;
        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            filledCount++;

            tracker.SetCell(curr.R, curr.C, color: fillHexColor);
            tracker.Visit(curr.R, curr.C, $"Colored ({curr.R}, {curr.C}) • Total filled: {filledCount}", color: fillHexColor);

            foreach (var next in tracker.GetNeighbors(curr.R, curr.C, fourWay: fourWay))
            {
                if (visited[next.Row, next.Col]) continue;
                var nextCell = grid[next.Row, next.Col];

                if (belongsToRegion(nextCell))
                {
                    visited[next.Row, next.Col] = true;
                    queue.Enqueue(next);
                    tracker.Enqueue(next.Row, next.Col, from: curr,
                        note: $"Identified region neighbor ({next.Row}, {next.Col})",
                        aux: new { QueueSize = queue.Count });
                }
            }
        }

        tracker.Snapshot($"Flood fill complete! Total cells filled: {filledCount}");
        return tracker;
    }
}
