using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// What changed between two states of the same visualizer, as a step's changes. A change can set a field, or reset a
/// state or terrain to its default, but it can't unset a colour or a note: then, and whenever the structure differs,
/// the diff is null and the step carries a whole state. An empty result means nothing changed.
/// </summary>
internal static class StateDiff
{
    private static readonly MatrixParseOptions TerrainRules = new();

    /// <summary>Tracks whether every difference so far can be said as a change.</summary>
    private sealed class Expressible
    {
        public bool Ok { get; private set; } = true;

        // A field that is set in the new state can be sent; one that is only unset can't.
        public T? Set<T>(T? before, T? after) where T : class
        {
            if (Equals(before, after)) return null;
            if (after == null) Ok = false;
            return after;
        }

        public T? Set<T>(T? before, T? after) where T : struct
        {
            if (Nullable.Equals(before, after)) return null;
            if (after == null) Ok = false;
            return after;
        }
    }

    public static VisualizerChangesSpec? Cells(GridState a, GridState b)
    {
        if (a.Values.Count != b.Values.Count || a.Values.Where((row, r) => row.Count != b.Values[r].Count).Any()) return null;
        if (!SameList(a.RowHeaders, b.RowHeaders) || !SameList(a.ColumnHeaders, b.ColumnHeaders) || a.ColorMap != b.ColorMap ||
            a.Checkerboard != b.Checkerboard || a.InferTerrain != b.InferTerrain || !SameIslands(a.Islands, b.Islands))
        {
            return null;
        }

        // Every cell of every step is looked at, so the overlays are found by position rather than looked up.
        var width = b.Values.Count == 0 ? 0 : b.Values.Max(row => row.Count);
        var before = Overlays(a.Cells, b.Values.Count, width);
        var after = Overlays(b.Cells, b.Values.Count, width);
        var infers = b.InferTerrain != false;
        var ok = new Expressible();
        var changes = new List<CellSpec>();
        for (var r = 0; r < b.Values.Count; r++)
        {
            var (rowBefore, rowAfter) = (a.Values[r], b.Values[r]);
            for (var c = 0; c < rowAfter.Count; c++)
            {
                var x = before?[r * width + c];
                var y = after?[r * width + c];
                var (valueBefore, valueAfter) = (rowBefore[c], rowAfter[c]);

                // A plain cell whose value stayed the same stayed the same (what it is follows from its value): most cells.
                var valueChanged = valueBefore != valueAfter;
                if (x == null && y == null && !valueChanged) continue;

                // A change can't say "no value" (null means unchanged), so a value that became null takes a whole state.
                if (valueChanged && valueAfter.Kind == ScalarKind.Null) return null;

                // Changes don't read what a value says a cell is: a value that says something else sends the terrain too.
                var terrainBefore = x?.Terrain ?? (infers ? Inferred(valueBefore) : CellKind.Standard);
                var terrainAfter = y?.Terrain ?? (infers ? Inferred(valueAfter) : CellKind.Standard);
                // A wall, start, target or path cell has that state until told otherwise, so states compare as shown.
                var stateBefore = x?.State ?? ImpliedState(terrainBefore);
                var stateAfter = y?.State ?? ImpliedState(terrainAfter);
                var change = new CellSpec
                {
                    Row = r,
                    Col = c,
                    Value = valueChanged ? valueAfter : null,
                    Terrain = terrainBefore == terrainAfter ? null : terrainAfter,
                    State = stateBefore == stateAfter && terrainBefore == terrainAfter ? null : stateAfter,
                    Cluster = ok.Set(x?.Cluster, y?.Cluster),
                    Color = ok.Set(x?.Color, y?.Color),
                    BorderColor = ok.Set(x?.BorderColor, y?.BorderColor),
                    TextColor = ok.Set(x?.TextColor, y?.TextColor),
                    Heat = ok.Set(x?.Heat, y?.Heat),
                    Note = ok.Set(x?.Note, y?.Note),
                    Notes = SameNotes(x?.Notes, y?.Notes) ? null : ok.Set(x?.Notes, y?.Notes),
                    Arrows = SameArrows(x?.Arrows, y?.Arrows) ? null : ok.Set(x?.Arrows, y?.Arrows is { Count: > 0 } arrows ? arrows : null)
                };

                if (!ok.Ok) return null;
                if (!IsEmpty(change)) changes.Add(change);
            }
        }

        return new VisualizerChangesSpec { Cells = changes.Count > 0 ? changes : null };
    }

    // A state's overlays by position (row by row, `width` a row); null when it has none.
    private static CellSpec?[]? Overlays(List<CellSpec>? cells, int rows, int width)
    {
        if (cells is not { Count: > 0 }) return null;
        var byPosition = new CellSpec?[rows * width];
        foreach (var cell in cells)
        {
            if (cell.Row >= 0 && cell.Row < rows && cell.Col >= 0 && cell.Col < width) byPosition[cell.Row * width + cell.Col] = cell;
        }

        return byPosition;
    }

    /// <summary>The state a cell's terrain gives it (a wall is in the wall state), as setting the terrain does.</summary>
    internal static ElementState ImpliedState(CellKind terrain) => terrain switch
    {
        CellKind.Wall => ElementState.Wall,
        CellKind.Start => ElementState.Start,
        CellKind.Target => ElementState.Target,
        CellKind.Path => ElementState.Path,
        _ => ElementState.Default
    };

    private static CellKind Inferred(ScalarValue value)
    {
        var cell = new GridCell();
        MatrixDataParser.InferTerrain(cell, ModelValues.ToObject(value), TerrainRules);
        return cell.BaseKind;
    }

    private static bool IsEmpty(CellSpec c) =>
        c.Value == null && c.Terrain == null && c.State == null && c.Cluster == null && c.Color == null && c.BorderColor == null &&
        c.TextColor == null && c.Heat == null && c.Note == null && c.Notes == null && c.Arrows == null;

    /// <summary>Array items or bars, when there are as many of them and nothing but their values and looks changed.</summary>
    public static VisualizerChangesSpec? Items(IReadOnlyList<ScalarValue> a, IReadOnlyList<ScalarValue> b, List<ItemSpec>? itemsA, List<ItemSpec>? itemsB)
    {
        if (a.Count != b.Count) return null;
        var before = (itemsA ?? []).ToDictionary(i => i.Index);
        var after = (itemsB ?? []).ToDictionary(i => i.Index);
        var ok = new Expressible();
        var changes = new List<ItemSpec>();
        for (var i = 0; i < b.Count; i++)
        {
            before.TryGetValue(i, out var x);
            after.TryGetValue(i, out var y);
            var valueChanged = a[i] != b[i];
            if (x == null && y == null && !valueChanged) continue;
            if (valueChanged && b[i].Kind == ScalarKind.Null) return null; // null means unchanged, so this takes a whole state

            var change = new ItemSpec
            {
                Index = i,
                Value = valueChanged ? b[i] : null,
                State = x?.State == y?.State ? null : y?.State ?? ElementState.Default,
                Color = ok.Set(x?.Color, y?.Color),
                Label = ok.Set(x?.Label, y?.Label),
                Pointer = ok.Set(x?.Pointer, y?.Pointer)
            };

            if (!ok.Ok) return null;
            if (change.Value != null || change.State != null || change.Color != null || change.Label != null || change.Pointer != null) changes.Add(change);
        }

        return new VisualizerChangesSpec { Items = changes.Count > 0 ? changes : null };
    }

    /// <summary>
    /// A node's look, by id, as a change (only what differs), or null when nothing differs; clears
    /// <paramref name="expressible"/> when a difference can't be said as a change.
    /// </summary>
    public static NodeChangeSpec? Node(string id, ScalarValue valueA, ScalarValue valueB, ElementState? stateA, ElementState? stateB,
        string? colorA, string? colorB, string? pointerA, string? pointerB, string? noteA, string? noteB, ref bool expressible)
    {
        var ok = new Expressible();
        var valueChanged = valueA != valueB;
        if (valueChanged && valueB.Kind == ScalarKind.Null) expressible = false; // null means unchanged
        var change = new NodeChangeSpec
        {
            Id = id,
            Value = valueChanged ? valueB : null,
            State = stateA == stateB ? null : stateB ?? ElementState.Default,
            Color = ok.Set(colorA, colorB),
            Pointer = ok.Set(pointerA, pointerB),
            Note = ok.Set(noteA, noteB)
        };

        expressible &= ok.Ok;
        return change.Value == null && change.State == null && change.Color == null && change.Pointer == null && change.Note == null ? null : change;
    }

    public static EdgeChangeSpec? Edge(GraphEdgeSpec a, GraphEdgeSpec b, ref bool expressible)
    {
        var ok = new Expressible();
        var change = new EdgeChangeSpec
        {
            From = b.From,
            To = b.To,
            Weight = ok.Set(a.Weight, b.Weight),
            Label = ok.Set(a.Label, b.Label),
            State = a.State == b.State ? null : b.State ?? ElementState.Default,
            Color = ok.Set(a.Color, b.Color)
        };

        expressible &= ok.Ok;
        return change.Weight == null && change.Label == null && change.State == null && change.Color == null ? null : change;
    }

    public static bool SameList<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b) =>
        (a == null || a.Count == 0) && (b == null || b.Count == 0) || a != null && b != null && a.SequenceEqual(b);

    public static bool SameNotes(Dictionary<string, string>? a, Dictionary<string, string>? b) =>
        (a == null || a.Count == 0) && (b == null || b.Count == 0) ||
        a != null && b != null && a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    private static bool SameArrows(List<ArrowSpec>? a, List<ArrowSpec>? b) =>
        SameList(a?.Select(x => (x.To, x.Label, x.Color)).ToList(), b?.Select(x => (x.To, x.Label, x.Color)).ToList());

    private static bool SameIslands(List<IslandSpec>? a, List<IslandSpec>? b) =>
        SameList(a?.Select(Key).ToList(), b?.Select(Key).ToList());

    private static string Key(IslandSpec island) => $"{island.Id}|{island.Label}|{island.Color}|{string.Join(";", island.Cells)}";
}
