using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// Board visualizers (N-Queens, Sudoku, DP tables with a chess-board look): a <see cref="GridState"/> as a
/// <see cref="BoardVisualizerData"/>, and back. A board draws the target and rejected states, and a highlight.
/// </summary>
internal sealed class BoardKind : IVisualizerKind
{
    public static readonly BoardKind Instance = new();

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.Grid!;
        var (rows, columns) = GridShape.Fit(source.Values, notices);
        var board = new BoardVisualizerData(rows, columns, source.Checkerboard ?? false)
        {
            ShowCoordinates = spec.ShowCoordinates ?? true,
            RowHeaders = source.RowHeaders?.ToList() ?? [],
            ColHeaders = source.ColumnHeaders?.ToList() ?? []
        };

        for (var r = 0; r < rows && r < source.Values.Count; r++)
        {
            for (var c = 0; c < columns && c < source.Values[r].Count; c++)
            {
                board[r, c].Value = source.Values[r][c].IsNull ? string.Empty : source.Values[r][c].ToString();
            }
        }

        foreach (var cell in source.Cells ?? []) ApplyCell(board, cell);
        return board;
    }

    public object? Clone(object? model) => ModelValues.As<BoardVisualizerData>(model).Clone();

    public void Apply(object? model, VisualizerChangesSpec changes)
    {
        foreach (var cell in changes.Cells ?? []) ApplyCell(ModelValues.As<BoardVisualizerData>(model), cell);
    }

    private static void ApplyCell(BoardVisualizerData board, CellSpec spec)
    {
        if (!board.IsInBounds(spec.Row, spec.Col)) return;
        var cell = board[spec.Row, spec.Col];
        if (spec.Value is { } value) cell.Value = value.IsNull ? string.Empty : value.ToString();
        if (spec.State is { } state)
        {
            cell.IsTarget = state == ElementState.Target;
            cell.IsConflict = state == ElementState.Rejected;
        }

        if (spec.Color != null) cell.FillColor = spec.Color;
        if (spec.BorderColor != null) cell.BorderColor = spec.BorderColor;
        if (spec.TextColor != null) cell.TextColor = spec.TextColor;
        if (spec.Note != null) cell.SubLabel = spec.Note;
        if (spec.Arrows != null)
        {
            cell.Arrows = spec.Arrows.Where(a => a.To.Kind == ElementRefKind.Cell)
                .Select(a => new BoardCellArrow { TargetRow = a.To.Row, TargetCol = a.To.Col, Label = a.Label, ColorHex = a.Color })
                .ToList();
        }
    }

    // A board draws highlights and pointers from its cells only.
    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers) { }

    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep)
    {
        if (highlight.Count == 0 && pointers.Count == 0) return model;
        var board = ModelValues.As<BoardVisualizerData>(model).Clone();
        foreach (var cell in highlight.Where(h => h.Kind == ElementRefKind.Cell && board.IsInBounds(h.Row, h.Col)))
        {
            board[cell.Row, cell.Col].IsActive = true;
        }

        foreach (var group in pointers.Where(p => p.At is { Kind: ElementRefKind.Cell }).GroupBy(p => (p.At!.Value.Row, p.At!.Value.Col)))
        {
            if (!board.IsInBounds(group.Key.Row, group.Key.Col)) continue;
            var cell = board[group.Key.Row, group.Key.Col];
            var names = ModelValues.JoinNames(group);
            cell.SubLabel = string.IsNullOrEmpty(cell.SubLabel) ? names : $"{cell.SubLabel} · {names}";
        }

        return board;
    }

    public void Assign(VisualizerOptions options, object? model) => options.BoardData = ModelValues.As<BoardVisualizerData>(model);

    public object? InitialModel(VisualizerOptions options) => options.BoardData;

    public VisualizerState ToState(object? model)
    {
        var board = ModelValues.As<BoardVisualizerData>(model);
        var state = new GridState
        {
            Checkerboard = board.IsCheckerboard ? true : null,
            RowHeaders = board.RowHeaders.Count > 0 ? board.RowHeaders.ToList() : null,
            ColumnHeaders = board.ColHeaders.Count > 0 ? board.ColHeaders.ToList() : null,
            Cells = []
        };

        for (var r = 0; r < board.Rows; r++)
        {
            var row = new List<ScalarValue>(board.Columns);
            for (var c = 0; c < board.Columns; c++)
            {
                var cell = board[r, c];
                row.Add(ScalarValue.FromText(cell.Value));
                var overlay = new CellSpec
                {
                    Row = r,
                    Col = c,
                    State = cell.IsConflict ? ElementState.Rejected : cell.IsTarget ? ElementState.Target : null,
                    Color = ModelValues.NullIfEmpty(cell.FillColor),
                    BorderColor = ModelValues.NullIfEmpty(cell.BorderColor),
                    TextColor = ModelValues.NullIfEmpty(cell.TextColor),
                    Note = ModelValues.NullIfEmpty(cell.SubLabel),
                    Arrows = cell.Arrows.Count == 0 ? null : cell.Arrows
                        .Select(a => new ArrowSpec { To = ElementRef.Cell(a.TargetRow, a.TargetCol), Label = a.Label, Color = a.ColorHex })
                        .ToList()
                };

                if (overlay.State != null || overlay.Color != null || overlay.BorderColor != null || overlay.TextColor != null ||
                    overlay.Note != null || overlay.Arrows != null)
                {
                    state.Cells.Add(overlay);
                }
            }

            state.Values.Add(row);
        }

        return new VisualizerState { Grid = state };
    }

    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next) =>
        previous.Grid is { } before && next.Grid is { } after ? StateDiff.Cells(before, after) : null;

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers)
    {
        var board = ModelValues.As<BoardVisualizerData>(model);
        for (var r = 0; r < board.Rows; r++)
        {
            for (var c = 0; c < board.Columns; c++)
            {
                if (board[r, c].IsActive) highlight.Add(ElementRef.Cell(r, c));
            }
        }
    }
}
