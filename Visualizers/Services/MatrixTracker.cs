using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>
/// Fluent tracker for any user-written matrix traversal or grid algorithm (BFS, DFS, Dijkstra, A*, Backtracking, DP).
/// Empowers developers to record algorithm steps, frontier updates, predecessor arrows, and reconstructed paths with 1-line calls.
/// </summary>
public class MatrixTracker
{
    private readonly MatrixVisualizerRecorder _recorder;
    private readonly bool _linkSourceLines;
    private (int Row, int Col)? _currentCell;

    public VisualizerOptions Options => _recorder.Options;
    public VisualizerSequence Sequence => _recorder.Sequence;
    public GridMatrixData Grid => _recorder.Options.MatrixData!;

    public int Rows => Grid.Rows;
    public int Columns => Grid.Columns;

    public MatrixTracker(
        GridMatrixData grid,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
        : this(grid, title, sourceLine, sourceFile, linkSourceLines: true)
    {
    }

    private MatrixTracker(GridMatrixData grid, string? title, int sourceLine, string sourceFile, bool linkSourceLines)
    {
        _linkSourceLines = linkSourceLines;
        _recorder = VisualizerRecorder.CreateMatrix(grid, title ?? "Matrix Traversal", LineOf(sourceLine), sourceFile);
    }

    public static MatrixTracker Create(
        GridMatrixData grid,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "") =>
        new(grid, title, sourceLine, sourceFile);

    /// <param name="rowHeaders">Labels drawn left of each row (e.g. "nums", "dp"), shown from the very first step.</param>
    /// <param name="columnHeaders">Labels drawn above each column instead of the column numbers.</param>
    public static MatrixTracker Create(
        object rawInput,
        string? title = null,
        MatrixParseOptions? options = null,
        IEnumerable<string>? rowHeaders = null,
        IEnumerable<string>? columnHeaders = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var grid = MatrixDataParser.Parse(rawInput, options);
        if (rowHeaders != null) grid.RowHeaders.AddRange(rowHeaders);
        if (columnHeaders != null) grid.ColHeaders.AddRange(columnHeaders);
        return new MatrixTracker(grid, title, sourceLine, sourceFile);
    }

    /// <summary>A blank grid to fill with SetCell, e.g. a DP table; headers label the rows and columns from the first step.</summary>
    public static MatrixTracker CreateEmpty(
        int rows,
        int cols,
        string? title = null,
        IEnumerable<string>? rowHeaders = null,
        IEnumerable<string>? columnHeaders = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var grid = new GridMatrixData(rows, cols);
        if (rowHeaders != null) grid.RowHeaders.AddRange(rowHeaders);
        if (columnHeaders != null) grid.ColHeaders.AddRange(columnHeaders);
        return new MatrixTracker(grid, title, sourceLine, sourceFile);
    }

    // Built-in algorithms record their own steps; those must not point at lines in the learner's code.
    internal static MatrixTracker CreateUnlinked(GridMatrixData grid, string? title) =>
        new(grid, title, 0, string.Empty, linkSourceLines: false);

    private int LineOf(int sourceLine) => _linkSourceLines ? sourceLine : 0;

    /// <summary>Shows a live queue, stack, set, map or list beneath the grid at every step recorded after this call.</summary>
    public MatrixTracker Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "")
    {
        _recorder.Watch(collection, name);
        return this;
    }

    public bool IsInBounds(int r, int c) => Grid.IsInBounds(r, c);

    public GridCell this[int r, int c] => Grid[r, c];

    public MatrixTracker SetStart(int r, int c, string label = "S")
    {
        if (IsInBounds(r, c))
        {
            Grid[r, c].Kind = CellKind.Start;
            Grid[r, c].DisplayValue = label;
        }
        return this;
    }

    public MatrixTracker SetTarget(int r, int c, string label = "T")
    {
        if (IsInBounds(r, c))
        {
            Grid[r, c].Kind = CellKind.Target;
            Grid[r, c].DisplayValue = label;
        }
        return this;
    }

    public MatrixTracker Visit(
        int r,
        int c,
        string? note = null,
        string? subLabel = null,
        string? color = null,
        object? aux = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (!IsInBounds(r, c)) return this;

        // Transition previous current cell to Visited
        if (_currentCell.HasValue && IsInBounds(_currentCell.Value.Row, _currentCell.Value.Col))
        {
            var prev = Grid[_currentCell.Value.Row, _currentCell.Value.Col];
            if (prev.State == GridCellState.Current)
            {
                prev.State = GridCellState.Visited;
                prev.IsVisited = true;
            }
        }

        var cell = Grid[r, c];
        if (cell.State != GridCellState.Start && cell.State != GridCellState.Target)
        {
            cell.State = GridCellState.Current;
        }
        cell.IsVisited = true;

        if (subLabel != null) cell.SubLabel = subLabel;
        if (color != null) cell.CustomColor = color;

        _currentCell = (r, c);

        string desc = note ?? $"Visiting cell ({r}, {c})";
        var dict = ExtractAux(aux);
        if (!string.IsNullOrEmpty(subLabel)) dict["Cost"] = subLabel;

        _recorder.Step(desc, activeCell: (r, c), auxiliaryInfo: dict, sourceLine: LineOf(sourceLine), sourceFile: sourceFile);
        return this;
    }

    public MatrixTracker Enqueue(
        int r,
        int c,
        (int Row, int Col)? from = null,
        string? note = null,
        string? subLabel = null,
        object? aux = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (!IsInBounds(r, c)) return this;

        var cell = Grid[r, c];
        if (cell.State != GridCellState.Start && cell.State != GridCellState.Target)
        {
            cell.State = GridCellState.Frontier;
        }

        if (subLabel != null) cell.SubLabel = subLabel;

        if (from.HasValue && IsInBounds(from.Value.Row, from.Value.Col))
        {
            cell.Arrows.Add(new GridCellArrow
            {
                TargetRow = from.Value.Row,
                TargetCol = from.Value.Col,
                Label = "from",
                ColorHex = "#38bdf8"
            });
        }

        string desc = note ?? $"Enqueued cell ({r}, {c})";
        var dict = ExtractAux(aux);
        if (from.HasValue) dict["Discovered From"] = $"({from.Value.Row}, {from.Value.Col})";
        if (subLabel != null) dict["Weight/Cost"] = subLabel;

        _recorder.Step(desc, activeCell: (r, c), auxiliaryInfo: dict, sourceLine: LineOf(sourceLine), sourceFile: sourceFile);
        return this;
    }

    public MatrixTracker MarkFrontier(
        int r,
        int c,
        (int Row, int Col)? from = null,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "") =>
        Enqueue(r, c, from, note, sourceLine: sourceLine, sourceFile: sourceFile);

    public MatrixTracker Backtrack(
        int r,
        int c,
        string? note = null,
        GridCellState restoreState = GridCellState.Backtracked,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (!IsInBounds(r, c)) return this;

        var cell = Grid[r, c];
        cell.State = restoreState;

        string desc = note ?? $"Backtracking from ({r}, {c})";
        _recorder.Step(desc, activeCell: (r, c), sourceLine: LineOf(sourceLine), sourceFile: sourceFile);
        return this;
    }

    public MatrixTracker MarkPath(
        IEnumerable<(int Row, int Col)> path,
        string? note = null,
        string? color = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var pathList = new List<(int Row, int Col)>(path);
        foreach (var (r, c) in pathList)
        {
            if (IsInBounds(r, c))
            {
                var cell = Grid[r, c];
                cell.State = GridCellState.Path;
                cell.IsPath = true;
                if (color != null) cell.CustomColor = color;
            }
        }

        string desc = note ?? $"Shortest path discovered ({pathList.Count} steps)";
        var dict = new Dictionary<string, string> { ["Path Length"] = pathList.Count.ToString() };
        _recorder.Step(desc, activeCells: pathList, auxiliaryInfo: dict, sourceLine: LineOf(sourceLine), sourceFile: sourceFile);
        return this;
    }

    public MatrixTracker PointTo(int fromR, int fromC, int toR, int toC, string? label = null, string? color = null)
    {
        if (IsInBounds(fromR, fromC) && IsInBounds(toR, toC))
        {
            Grid[fromR, fromC].Arrows.Add(new GridCellArrow
            {
                TargetRow = toR,
                TargetCol = toC,
                Label = label,
                ColorHex = color ?? "#38bdf8"
            });
        }
        return this;
    }

    public MatrixTracker SetCell(
        int r,
        int c,
        string? val = null,
        GridCellState? state = null,
        string? subLabel = null,
        string? color = null,
        double? heat = null)
    {
        if (!IsInBounds(r, c)) return this;

        var cell = Grid[r, c];
        if (val != null) cell.DisplayValue = val;
        if (state.HasValue) cell.State = state.Value;
        if (subLabel != null) cell.SubLabel = subLabel;
        if (color != null) cell.CustomColor = color;
        if (heat.HasValue) cell.Heat = heat.Value;

        return this;
    }

    public MatrixTracker Snapshot(
        string description,
        object? aux = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var dict = ExtractAux(aux);
        _recorder.Step(description, auxiliaryInfo: dict, sourceLine: LineOf(sourceLine), sourceFile: sourceFile);
        return this;
    }

    public MatrixTracker Step(
        string description,
        Action<GridMatrixData> mutate,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        _recorder.Step(description, mutate, LineOf(sourceLine), sourceFile);
        return this;
    }

    public IEnumerable<(int Row, int Col)> GetNeighbors(int r, int c, bool fourWay = true, bool allowDiagonal = false)
    {
        if (fourWay)
        {
            if (IsInBounds(r - 1, c)) yield return (r - 1, c);
            if (IsInBounds(r + 1, c)) yield return (r + 1, c);
            if (IsInBounds(r, c - 1)) yield return (r, c - 1);
            if (IsInBounds(r, c + 1)) yield return (r, c + 1);
        }

        if (allowDiagonal)
        {
            if (IsInBounds(r - 1, c - 1)) yield return (r - 1, c - 1);
            if (IsInBounds(r - 1, c + 1)) yield return (r - 1, c + 1);
            if (IsInBounds(r + 1, c - 1)) yield return (r + 1, c - 1);
            if (IsInBounds(r + 1, c + 1)) yield return (r + 1, c + 1);
        }
    }

    public List<(int Row, int Col)> TracePath(
        IDictionary<(int Row, int Col), (int Row, int Col)> parentMap,
        (int Row, int Col) target,
        (int Row, int Col) start)
    {
        var path = new List<(int Row, int Col)>();
        var curr = target;

        while (parentMap.TryGetValue(curr, out var parent))
        {
            path.Add(curr);
            curr = parent;
            if (curr == start)
            {
                path.Add(start);
                break;
            }
        }

        path.Reverse();
        return path;
    }

    private static Dictionary<string, string> ExtractAux(object? aux)
    {
        var dict = new Dictionary<string, string>();
        if (aux == null) return dict;

        if (aux is IDictionary<string, string> stringDict)
        {
            foreach (var kvp in stringDict) dict[kvp.Key] = kvp.Value;
            return dict;
        }

        var props = aux.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
        {
            var val = p.GetValue(aux);
            if (val != null) dict[p.Name] = val.ToString()!;
        }

        return dict;
    }

    public VisualizerRecorder ToRecorder() => _recorder;
    public VisualizerOptions ToOptions() => _recorder.Options;
    public VisualizerSequence ToSequence() => _recorder.Sequence;

    public static implicit operator VisualizerOptions(MatrixTracker tracker) => tracker.Options;
    public static implicit operator VisualizerRecorder(MatrixTracker tracker) => tracker.ToRecorder();
}
