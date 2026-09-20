using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class BoardCellArrow
{
    public int TargetRow { get; set; }
    public int TargetCol { get; set; }
    public string? Label { get; set; }
    public string? ColorHex { get; set; }

    public BoardCellArrow Clone()
    {
        return new BoardCellArrow
        {
            TargetRow = TargetRow,
            TargetCol = TargetCol,
            Label = Label,
            ColorHex = ColorHex
        };
    }
}

public class BoardCell
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? SubLabel { get; set; }
    public string? FillColor { get; set; }
    public string? BorderColor { get; set; }
    public string? TextColor { get; set; }
    public bool IsActive { get; set; }
    public bool IsTarget { get; set; }
    public bool IsConflict { get; set; }
    public List<BoardCellArrow> Arrows { get; set; } = new();

    public BoardCell Clone()
    {
        var clone = new BoardCell
        {
            Row = Row,
            Col = Col,
            Value = Value,
            SubLabel = SubLabel,
            FillColor = FillColor,
            BorderColor = BorderColor,
            TextColor = TextColor,
            IsActive = IsActive,
            IsTarget = IsTarget,
            IsConflict = IsConflict
        };
        foreach (var arrow in Arrows)
        {
            clone.Arrows.Add(arrow.Clone());
        }
        return clone;
    }
}

public class BoardVisualizerData
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public BoardCell[,] Cells { get; set; }
    public List<string> RowHeaders { get; set; } = new();
    public List<string> ColHeaders { get; set; } = new();
    public bool IsCheckerboard { get; set; }
    public bool ShowCoordinates { get; set; } = true;
    public string? Title { get; set; }

    public BoardVisualizerData(int rows, int cols, bool checkerboard = false)
    {
        Rows = Math.Max(1, rows);
        Columns = Math.Max(1, cols);
        IsCheckerboard = checkerboard;
        Cells = new BoardCell[Rows, Columns];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                Cells[r, c] = new BoardCell { Row = r, Col = c };
            }
        }
    }

    public BoardCell this[int row, int col]
    {
        get => Cells[row, col];
        set => Cells[row, col] = value;
    }

    public bool IsInBounds(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Columns;

    public BoardVisualizerData Clone()
    {
        var clone = new BoardVisualizerData(Rows, Columns, IsCheckerboard)
        {
            ShowCoordinates = ShowCoordinates,
            Title = Title,
            RowHeaders = new List<string>(RowHeaders),
            ColHeaders = new List<string>(ColHeaders)
        };
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                clone.Cells[r, c] = Cells[r, c].Clone();
            }
        }
        return clone;
    }
}
