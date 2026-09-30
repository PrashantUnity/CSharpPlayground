using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>Matrix and islands visualizers: a <see cref="GridState"/> as a <see cref="GridMatrixData"/>, and back.</summary>
internal sealed class GridKind : IVisualizerKind
{
    public static readonly GridKind Instance = new();
    private static readonly MatrixParseOptions TerrainRules = new();

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.Grid!;
        var (rows, columns) = GridShape.Fit(source.Values, notices);
        var grid = new GridMatrixData(rows, columns)
        {
            CellSize = spec.CellSize ?? VisualizerRenderDefaults.CellSize,
            ShowCoordinates = spec.ShowCoordinates ?? true,
            ShowValues = spec.ShowValues ?? true,
            ColorMap = source.ColorMap,
            RowHeaders = source.RowHeaders?.ToList() ?? [],
            ColHeaders = source.ColumnHeaders?.ToList() ?? []
        };

        for (var r = 0; r < rows && r < source.Values.Count; r++)
        {
            for (var c = 0; c < columns && c < source.Values[r].Count; c++)
            {
                var cell = grid[r, c];
                cell.DisplayValue = source.Values[r][c].ToString();
                cell.RawValue = ModelValues.ToObject(source.Values[r][c]);
                if (source.InferTerrain != false) MatrixDataParser.InferTerrain(cell, cell.RawValue, TerrainRules);
            }
        }

        foreach (var cell in source.Cells ?? []) ApplyCell(grid, cell);
        foreach (var island in source.Islands ?? []) grid.Islands.Add(IslandShape.ToModel(island, grid));
        return grid;
    }

    public object? Clone(object? model) => ModelValues.As<GridMatrixData>(model).Clone();

    public void Apply(object? model, VisualizerChangesSpec changes)
    {
        foreach (var cell in changes.Cells ?? []) ApplyCell(ModelValues.As<GridMatrixData>(model), cell);
    }

    private static void ApplyCell(GridMatrixData grid, CellSpec spec)
    {
        if (!grid.IsInBounds(spec.Row, spec.Col)) return;
        var cell = grid[spec.Row, spec.Col];
        if (spec.Value is { } value)
        {
            cell.DisplayValue = value.ToString();
            cell.RawValue = ModelValues.ToObject(value);
        }

        // What the cell is before what is happening to it: setting the kind resets the state a wall or start implies.
        if (spec.Terrain is { } terrain) cell.Kind = terrain;
        if (spec.State is { } state) cell.State = ElementStates.ToGrid(state);
        if (spec.Cluster is { } cluster) cell.ClusterId = cluster;
        if (spec.Color != null) cell.CustomColor = spec.Color;
        if (spec.BorderColor != null) cell.BorderColor = spec.BorderColor;
        if (spec.TextColor != null) cell.TextColor = spec.TextColor;
        if (spec.Heat is { } heat) cell.Heat = heat;
        if (spec.Note != null) cell.SubLabel = spec.Note;
        if (spec.Notes != null) cell.Metadata = ModelValues.ToMetadata(spec.Notes);
        if (spec.Arrows != null)
        {
            cell.Arrows = spec.Arrows.Where(a => a.To.Kind == ElementRefKind.Cell)
                .Select(a => new GridCellArrow { TargetRow = a.To.Row, TargetCol = a.To.Col, Label = a.Label, ColorHex = a.Color })
                .ToList();
        }
    }

    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers)
    {
        foreach (var cell in highlight.Where(h => h.Kind == ElementRefKind.Cell)) step.ActiveCells.Add((cell.Row, cell.Col));
    }

    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep)
    {
        var flagged = onStep ? [] : highlight.Where(h => h.Kind == ElementRefKind.Cell).ToList();
        if (flagged.Count == 0 && pointers.Count == 0) return model;
        var grid = ModelValues.As<GridMatrixData>(model).Clone();
        foreach (var cell in flagged.Where(h => grid.IsInBounds(h.Row, h.Col)))
        {
            grid[cell.Row, cell.Col].IsActive = true;
        }

        // A grid has no pointer slots: a pointer's name joins the cell's small label.
        foreach (var group in pointers.Where(p => p.At is { Kind: ElementRefKind.Cell }).GroupBy(p => (p.At!.Value.Row, p.At!.Value.Col)))
        {
            if (!grid.IsInBounds(group.Key.Row, group.Key.Col)) continue;
            var cell = grid[group.Key.Row, group.Key.Col];
            var names = ModelValues.JoinNames(group);
            cell.SubLabel = string.IsNullOrEmpty(cell.SubLabel) ? names : $"{cell.SubLabel} · {names}";
        }

        return grid;
    }

    public void Assign(VisualizerOptions options, object? model) => options.MatrixData = ModelValues.As<GridMatrixData>(model);

    public object? InitialModel(VisualizerOptions options) => options.MatrixData;

    public VisualizerState ToState(object? model)
    {
        var grid = ModelValues.As<GridMatrixData>(model);
        var state = new GridState
        {
            // The C# side already read what each value says the cell is; the terrain is written out cell by cell.
            InferTerrain = false,
            ColorMap = grid.ColorMap,
            RowHeaders = grid.RowHeaders.Count > 0 ? grid.RowHeaders.ToList() : null,
            ColumnHeaders = grid.ColHeaders.Count > 0 ? grid.ColHeaders.ToList() : null,
            Cells = []
        };

        for (var r = 0; r < grid.Rows; r++)
        {
            var row = new List<ScalarValue>(grid.Columns);
            for (var c = 0; c < grid.Columns; c++)
            {
                var cell = grid[r, c];
                row.Add(ModelValues.ToScalar(cell.RawValue, cell.DisplayValue));
                if (Describe(cell) is { } overlay) state.Cells.Add(overlay);
            }

            state.Values.Add(row);
        }

        if (grid.Islands.Count > 0) state.Islands = grid.Islands.Select(IslandShape.ToSpec).ToList();
        return new VisualizerState { Grid = state };
    }

    // A cell's look beyond its value; null for a plain cell (most of them, so it is told without building anything).
    private static CellSpec? Describe(GridCell cell)
    {
        // Only a state the terrain doesn't already give is written, and then even "default" (a wall whose state was cleared).
        var stateShown = cell.State != ImpliedState(cell.BaseKind);
        var plain = cell.BaseKind == CellKind.Standard && !stateShown && cell.ClusterId == null && string.IsNullOrEmpty(cell.CustomColor) &&
                    string.IsNullOrEmpty(cell.BorderColor) && string.IsNullOrEmpty(cell.TextColor) && cell.Heat == null &&
                    string.IsNullOrEmpty(cell.SubLabel) && cell.Metadata.Count == 0 && cell.Arrows.Count == 0;
        if (plain) return null;

        return new CellSpec
        {
            Row = cell.Row,
            Col = cell.Col,
            Terrain = cell.BaseKind == CellKind.Standard ? null : cell.BaseKind,
            State = stateShown ? ElementStates.FromGrid(cell.State) : null,
            Cluster = cell.ClusterId,
            Color = ModelValues.NullIfEmpty(cell.CustomColor),
            BorderColor = ModelValues.NullIfEmpty(cell.BorderColor),
            TextColor = ModelValues.NullIfEmpty(cell.TextColor),
            Heat = cell.Heat,
            Note = ModelValues.NullIfEmpty(cell.SubLabel),
            Notes = ModelValues.ToNotes(cell.Metadata),
            Arrows = cell.Arrows.Count == 0 ? null : cell.Arrows
                .Select(a => new ArrowSpec { To = ElementRef.Cell(a.TargetRow, a.TargetCol), Label = a.Label, Color = a.ColorHex })
                .ToList()
        };
    }

    // Setting a cell's kind sets the state it implies; that state needn't be written again.
    private static GridCellState ImpliedState(CellKind kind) => kind switch
    {
        CellKind.Wall => GridCellState.Wall,
        CellKind.Start => GridCellState.Start,
        CellKind.Target => GridCellState.Target,
        CellKind.Path => GridCellState.Path,
        _ => GridCellState.Default
    };

    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next) =>
        previous.Grid is { } before && next.Grid is { } after ? StateDiff.Cells(before, after) : null;

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers)
    {
        var grid = ModelValues.As<GridMatrixData>(model);
        var seen = new HashSet<(int, int)>();
        foreach (var (row, col) in step?.ActiveCells ?? [])
        {
            if (seen.Add((row, col))) highlight.Add(ElementRef.Cell(row, col));
        }

        for (var r = 0; r < grid.Rows; r++)
        {
            for (var c = 0; c < grid.Columns; c++)
            {
                if (grid[r, c].IsActive && seen.Add((r, c))) highlight.Add(ElementRef.Cell(r, c));
            }
        }
    }
}

/// <summary>What a grid draws within the cell limit: the rows (and columns) that fit.</summary>
internal static class GridShape
{
    public static (int Rows, int Columns) Fit(List<List<ScalarValue>> values, ICollection<string> notices)
    {
        var rows = Math.Max(1, values.Count);
        var columns = Math.Max(1, values.Count > 0 ? values[0].Count : 1);
        if (columns > VisualLimits.MaxGridCells)
        {
            notices.Add(Notices.ShowingFirst(VisualLimits.MaxGridCells, columns, "columns"));
            columns = VisualLimits.MaxGridCells;
        }

        var fitting = Math.Max(1, VisualLimits.MaxGridCells / columns);
        if (rows > fitting)
        {
            notices.Add(Notices.ShowingFirst(fitting, rows, "rows"));
            rows = fitting;
        }

        return (rows, columns);
    }
}

/// <summary>An island between the spec and the model; the studio works out its size and outline from its cells.</summary>
internal static class IslandShape
{
    public static IslandData ToModel(IslandSpec spec, GridMatrixData grid)
    {
        var cells = spec.Cells.Where(c => c.Kind == ElementRefKind.Cell && grid.IsInBounds(c.Row, c.Col)).Select(c => (c.Row, c.Col)).ToList();
        var set = cells.ToHashSet();
        var perimeter = cells.Sum(c => new[] { (c.Row - 1, c.Col), (c.Row + 1, c.Col), (c.Row, c.Col - 1), (c.Row, c.Col + 1) }.Count(n => !set.Contains(n)));
        return new IslandData
        {
            Id = spec.Id,
            Label = spec.Label ?? $"Island #{spec.Id}",
            Color = spec.Color ?? VisualizerPaletteService.GetIslandColor(spec.Id),
            Cells = cells,
            Perimeter = perimeter,
            BoundingBox = cells.Count == 0 ? (0, 0, 0, 0) : (cells.Min(c => c.Row), cells.Min(c => c.Col), cells.Max(c => c.Row), cells.Max(c => c.Col))
        };
    }

    public static IslandSpec ToSpec(IslandData island) => new()
    {
        Id = island.Id,
        Label = ModelValues.NullIfEmpty(island.Label),
        Color = island.Color,
        Cells = island.Cells.Select(c => ElementRef.Cell(c.Row, c.Col)).ToList()
    };
}

/// <summary>What a visualizer looks like where its spec says nothing.</summary>
internal static class VisualizerRenderDefaults
{
    public const double CellSize = 38.0;
}
