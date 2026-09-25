using System;
using System.Collections;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public class MatrixParseOptions
{
    public double CellSize { get; set; } = 38.0;
    public bool ShowCoordinates { get; set; } = true;
    public bool ShowValues { get; set; } = true;
    public Func<object?, GridCellState?>? StateClassifier { get; set; }
    public Func<object?, string?>? ValueFormatter { get; set; }
    public Func<object?, string?>? SubLabelExtractor { get; set; }
    public Func<object?, double?>? HeatExtractor { get; set; }
    public bool AutoHeatmap { get; set; }
    public bool DetectIslands { get; set; }
    public char WallChar { get; set; } = '#';
    public char StartChar { get; set; } = 'S';
    public char TargetChar { get; set; } = 'T';
}

public static class MatrixDataParser
{
    public static GridMatrixData Parse(object? input, double cellSize = 38.0)
    {
        return Parse(input, new MatrixParseOptions { CellSize = cellSize });
    }

    public static GridMatrixData Parse(object? input, MatrixParseOptions? options)
    {
        options ??= new MatrixParseOptions();

        if (input == null)
        {
            return new GridMatrixData(1, 1) { CellSize = options.CellSize };
        }

        if (input is GridMatrixData alreadyMatrix)
        {
            return alreadyMatrix;
        }

        GridMatrixData grid;

        // Case 1: 2D Multi-dimensional array (e.g. T[,])
        if (input is Array array2D && array2D.Rank == 2)
        {
            int rows = array2D.GetLength(0);
            int cols = array2D.GetLength(1);
            grid = new GridMatrixData(rows, cols)
            {
                CellSize = options.CellSize,
                ShowCoordinates = options.ShowCoordinates,
                ShowValues = options.ShowValues
            };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    object? val = array2D.GetValue(r, c);
                    ConfigureCell(grid[r, c], val, options);
                }
            }
        }
        // Case 2: String array (e.g. string[] { "11000", "11000", "00100" })
        else if (input is string[] stringRows && stringRows.Length > 0)
        {
            int rows = stringRows.Length;
            int cols = 0;
            for (int r = 0; r < rows; r++)
            {
                if (stringRows[r].Length > cols) cols = stringRows[r].Length;
            }
            cols = Math.Max(1, cols);

            grid = new GridMatrixData(rows, cols)
            {
                CellSize = options.CellSize,
                ShowCoordinates = options.ShowCoordinates,
                ShowValues = options.ShowValues
            };

            for (int r = 0; r < rows; r++)
            {
                string rowStr = stringRows[r];
                for (int c = 0; c < cols; c++)
                {
                    char ch = c < rowStr.Length ? rowStr[c] : ' ';
                    ConfigureCell(grid[r, c], ch, options);
                }
            }
        }
        // Case 3: Jagged array or IEnumerable of IEnumerables (e.g. char[][], List<List<T>>)
        else if (input is IEnumerable outerEnum)
        {
            var rowList = new List<List<object?>>();
            foreach (var item in outerEnum)
            {
                if (item is string str && !(item is char[]))
                {
                    var chars = new List<object?>();
                    foreach (char ch in str) chars.Add(ch);
                    rowList.Add(chars);
                }
                else if (item is IEnumerable innerEnum)
                {
                    var innerList = new List<object?>();
                    foreach (var cellVal in innerEnum)
                    {
                        innerList.Add(cellVal);
                    }
                    rowList.Add(innerList);
                }
            }

            if (rowList.Count > 0)
            {
                int rows = rowList.Count;
                int cols = 0;
                foreach (var r in rowList)
                {
                    if (r.Count > cols) cols = r.Count;
                }
                cols = Math.Max(1, cols);

                grid = new GridMatrixData(rows, cols)
                {
                    CellSize = options.CellSize,
                    ShowCoordinates = options.ShowCoordinates,
                    ShowValues = options.ShowValues
                };

                for (int r = 0; r < rows; r++)
                {
                    var rowData = rowList[r];
                    for (int c = 0; c < cols; c++)
                    {
                        object? val = c < rowData.Count ? rowData[c] : null;
                        ConfigureCell(grid[r, c], val, options);
                    }
                }
            }
            else
            {
                grid = new GridMatrixData(1, 1) { CellSize = options.CellSize };
                ConfigureCell(grid[0, 0], input, options);
            }
        }
        else
        {
            grid = new GridMatrixData(1, 1) { CellSize = options.CellSize };
            ConfigureCell(grid[0, 0], input, options);
        }

        // Optional: Auto-normalize numeric values for continuous heatmap
        if (options.AutoHeatmap)
        {
            ApplyAutoHeatmap(grid);
        }

        return grid;
    }

    private static void ConfigureCell(GridCell cell, object? val, MatrixParseOptions options)
    {
        cell.RawValue = val;
        cell.DisplayValue = options.ValueFormatter?.Invoke(val) ?? val?.ToString() ?? string.Empty;

        if (options.SubLabelExtractor != null)
        {
            cell.SubLabel = options.SubLabelExtractor(val);
        }

        if (options.HeatExtractor != null)
        {
            cell.Heat = options.HeatExtractor(val);
        }

        if (val == null)
        {
            cell.State = GridCellState.Default;
            return;
        }

        // Custom classifier takes highest priority
        if (options.StateClassifier != null)
        {
            var customState = options.StateClassifier(val);
            if (customState.HasValue)
            {
                cell.State = customState.Value;
                return;
            }
        }

        // Semantic defaults
        switch (val)
        {
            case char ch:
                if (ch == options.StartChar || ch == 'S') cell.Kind = CellKind.Start;
                else if (ch == options.TargetChar || ch == 'E' || ch == 'T') cell.Kind = CellKind.Target;
                else if (ch == options.WallChar || ch == 'W' || ch == 'X') cell.Kind = CellKind.Wall;
                else if (ch == '.') cell.State = GridCellState.Default;
                else if (ch == '1') { cell.Kind = CellKind.Land; cell.State = GridCellState.Default; }
                else if (ch == '0') { cell.Kind = CellKind.Water; cell.State = GridCellState.Default; }
                else cell.State = GridCellState.Default;
                break;

            case int i:
                if (i == 1) { cell.Kind = CellKind.Land; cell.State = GridCellState.Default; }
                else if (i == 0) { cell.Kind = CellKind.Water; cell.State = GridCellState.Default; }
                else if (i < 0) cell.Kind = CellKind.Wall;
                else cell.State = GridCellState.Default;
                break;

            case bool b:
                cell.Kind = b ? CellKind.Land : CellKind.Water;
                cell.DisplayValue = b ? "1" : "0";
                break;

            case string s:
                var trimmed = s.Trim();
                if (trimmed.Equals("start", StringComparison.OrdinalIgnoreCase) || trimmed == "S") cell.Kind = CellKind.Start;
                else if (trimmed.Equals("end", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("target", StringComparison.OrdinalIgnoreCase) || trimmed == "T" || trimmed == "E") cell.Kind = CellKind.Target;
                else if (trimmed == "#" || trimmed.Equals("wall", StringComparison.OrdinalIgnoreCase)) cell.Kind = CellKind.Wall;
                else if (trimmed == "1") { cell.Kind = CellKind.Land; cell.State = GridCellState.Default; }
                else if (trimmed == "0") { cell.Kind = CellKind.Water; cell.State = GridCellState.Default; }
                else if (trimmed.Equals("land", StringComparison.OrdinalIgnoreCase)) { cell.Kind = CellKind.Land; cell.State = GridCellState.Default; }
                else if (trimmed.Equals("water", StringComparison.OrdinalIgnoreCase)) { cell.Kind = CellKind.Water; cell.State = GridCellState.Default; }
                else cell.State = GridCellState.Default;
                break;

            default:
                cell.State = GridCellState.Default;
                break;
        }
    }

    private static void ApplyAutoHeatmap(GridMatrixData grid)
    {
        double min = double.MaxValue;
        double max = double.MinValue;
        bool found = false;

        for (int r = 0; r < grid.Rows; r++)
        {
            for (int c = 0; c < grid.Columns; c++)
            {
                if (grid[r, c].RawValue is IConvertible conv && conv is not bool and not char)
                {
                    try
                    {
                        double d = Convert.ToDouble(conv);
                        if (d < min) min = d;
                        if (d > max) max = d;
                        found = true;
                    }
                    catch { }
                }
            }
        }

        if (found && Math.Abs(max - min) > 0.0001)
        {
            for (int r = 0; r < grid.Rows; r++)
            {
                for (int c = 0; c < grid.Columns; c++)
                {
                    if (grid[r, c].RawValue is IConvertible conv && conv is not bool and not char)
                    {
                        try
                        {
                            double d = Convert.ToDouble(conv);
                            grid[r, c].Heat = (d - min) / (max - min);
                        }
                        catch { }
                    }
                }
            }
        }
    }
}

public class MatrixBuilder
{
    private readonly GridMatrixData _grid;

    public MatrixBuilder(GridMatrixData grid)
    {
        _grid = grid;
    }

    public MatrixBuilder(int rows, int cols) : this(new GridMatrixData(rows, cols))
    {
    }

    public static MatrixBuilder Create(int rows, int cols) => new(rows, cols);

    public static MatrixBuilder From(object input, MatrixParseOptions? options = null)
    {
        var parsed = MatrixDataParser.Parse(input, options);
        return new MatrixBuilder(parsed);
    }

    public MatrixBuilder WithWall(int r, int c)
    {
        if (_grid.IsInBounds(r, c)) _grid[r, c].Kind = CellKind.Wall;
        return this;
    }

    public MatrixBuilder WithStart(int r, int c, string label = "S")
    {
        if (_grid.IsInBounds(r, c))
        {
            _grid[r, c].Kind = CellKind.Start;
            _grid[r, c].DisplayValue = label;
        }
        return this;
    }

    public MatrixBuilder WithTarget(int r, int c, string label = "T")
    {
        if (_grid.IsInBounds(r, c))
        {
            _grid[r, c].Kind = CellKind.Target;
            _grid[r, c].DisplayValue = label;
        }
        return this;
    }

    public MatrixBuilder WithCell(int r, int c, Action<GridCell> configure)
    {
        if (_grid.IsInBounds(r, c)) configure(_grid[r, c]);
        return this;
    }

    public MatrixBuilder WithHeaders(IEnumerable<string> rowHeaders, IEnumerable<string> colHeaders)
    {
        _grid.RowHeaders = new List<string>(rowHeaders);
        _grid.ColHeaders = new List<string>(colHeaders);
        return this;
    }

    public GridMatrixData Build() => _grid;
}
