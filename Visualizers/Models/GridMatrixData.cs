using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public enum CellKind
{
    Standard,
    Land,
    Water,
    Wall,
    Path,
    Start,
    Target
}

public enum GridCellState
{
    Default,
    Start,
    Target,
    Wall,
    Visited,
    Current,
    Frontier,
    Path,
    Backtracked,
    Candidate,
    Custom
}

public class GridCellArrow
{
    public int TargetRow { get; set; }
    public int TargetCol { get; set; }
    public string? Label { get; set; }
    public string? ColorHex { get; set; }

    public GridCellArrow Clone() => new()
    {
        TargetRow = TargetRow,
        TargetCol = TargetCol,
        Label = Label,
        ColorHex = ColorHex
    };
}

public class GridCell
{
    private CellKind _kind = CellKind.Standard;
    private GridCellState _state = GridCellState.Default;

    public int Row { get; set; }
    public int Col { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public object? RawValue { get; set; }
    public string? SubLabel { get; set; }

    public CellKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            _state = value switch
            {
                CellKind.Wall => GridCellState.Wall,
                CellKind.Start => GridCellState.Start,
                CellKind.Target => GridCellState.Target,
                CellKind.Path => GridCellState.Path,
                _ => _state == GridCellState.Default ? GridCellState.Default : _state
            };
        }
    }

    public GridCellState State
    {
        get => _state;
        set
        {
            _state = value;
            _kind = value switch
            {
                GridCellState.Wall => CellKind.Wall,
                GridCellState.Start => CellKind.Start,
                GridCellState.Target => CellKind.Target,
                GridCellState.Path => CellKind.Path,
                _ => _kind
            };
        }
    }

    public int? ClusterId { get; set; }
    public string? CustomColor { get; set; }
    public string? BorderColor { get; set; }
    public string? TextColor { get; set; }
    public double? Heat { get; set; }

    public bool IsVisited { get; set; }
    public bool IsActive { get; set; }
    public bool IsPath { get; set; }

    public List<GridCellArrow> Arrows { get; set; } = new();
    public Dictionary<string, object?> Metadata { get; set; } = new();

    public GridCell Clone()
    {
        var clone = new GridCell
        {
            Row = Row,
            Col = Col,
            DisplayValue = DisplayValue,
            RawValue = RawValue,
            SubLabel = SubLabel,
            Kind = Kind,
            State = State,
            ClusterId = ClusterId,
            CustomColor = CustomColor,
            BorderColor = BorderColor,
            TextColor = TextColor,
            Heat = Heat,
            IsVisited = IsVisited,
            IsActive = IsActive,
            IsPath = IsPath
        };

        foreach (var arrow in Arrows)
        {
            clone.Arrows.Add(arrow.Clone());
        }

        foreach (var kvp in Metadata)
        {
            clone.Metadata[kvp.Key] = kvp.Value;
        }

        return clone;
    }
}

public class GridMatrixData
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public GridCell[,] Cells { get; set; }
    public List<IslandData> Islands { get; set; } = new();
    public List<string> RowHeaders { get; set; } = new();
    public List<string> ColHeaders { get; set; } = new();
    public string? ColorMap { get; set; }
    public double CellSize { get; set; } = 36.0;
    public bool ShowCoordinates { get; set; } = true;
    public bool ShowValues { get; set; } = true;

    public GridMatrixData(int rows, int cols)
    {
        Rows = Math.Max(1, rows);
        Columns = Math.Max(1, cols);
        Cells = new GridCell[Rows, Columns];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                Cells[r, c] = new GridCell { Row = r, Col = c };
            }
        }
    }

    public GridCell this[int row, int col]
    {
        get => Cells[row, col];
        set => Cells[row, col] = value;
    }

    public bool IsInBounds(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Columns;

    public GridMatrixData Clone()
    {
        var copy = new GridMatrixData(Rows, Columns)
        {
            CellSize = CellSize,
            ShowCoordinates = ShowCoordinates,
            ShowValues = ShowValues,
            ColorMap = ColorMap,
            RowHeaders = new List<string>(RowHeaders),
            ColHeaders = new List<string>(ColHeaders)
        };

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                copy.Cells[r, c] = Cells[r, c].Clone();
            }
        }

        foreach (var island in Islands)
        {
            copy.Islands.Add(new IslandData
            {
                Id = island.Id,
                Label = island.Label,
                Color = island.Color,
                Perimeter = island.Perimeter,
                BoundingBox = island.BoundingBox,
                Cells = new List<(int Row, int Col)>(island.Cells)
            });
        }

        return copy;
    }
}
