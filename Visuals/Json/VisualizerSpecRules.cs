namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>
/// What a visualizer spec must satisfy beyond its JSON shape: a state of the part its kind draws, and steps whose
/// changes, highlights and pointers name elements of the latest full state.
/// </summary>
internal static class VisualizerSpecRules
{
    public static void Check(VisualSpecIssues issues, VisualizerSpec spec)
    {
        var kind = VisualJsonErrors.JsonName(spec.Kind);
        CheckOptions(issues, spec, kind);
        if (spec.CellSize is { } cellSize && !(double.IsFinite(cellSize) && cellSize > 0)) issues.Add("$.cellSize", "must be a positive number of pixels");

        var elements = State(issues, "$.state", spec.Kind, kind, spec.State) ?? new VisualizerElements();
        Pointers(issues, "$.pointers", spec.Pointers, spec.Kind, elements);
        References(issues, "$.highlight", spec.Highlight, spec.Kind, elements);

        for (var i = 0; i < spec.Steps.Count && !issues.IsFull; i++)
        {
            var path = $"$.steps[{i}]";
            var step = spec.Steps[i];
            if (step.State != null) elements = State(issues, $"{path}.state", spec.Kind, kind, step.State) ?? elements;
            if (step.Changes != null) Changes(issues, $"{path}.changes", spec.Kind, kind, step.Changes, elements);
            References(issues, $"{path}.highlight", step.Highlight, spec.Kind, elements);
            Pointers(issues, $"{path}.pointers", step.Pointers, spec.Kind, elements);
            if (step.Line is < 1) issues.Add($"{path}.line", "lines count from 1");
            for (var w = 0; step.Watches != null && w < step.Watches.Count; w++)
            {
                if (step.Watches[w].Count < step.Watches[w].Items.Count) issues.Add($"{path}.watches[{w}].count", "can't be less than the items shown");
            }
        }
    }

    private static void CheckOptions(VisualSpecIssues issues, VisualizerSpec spec, string kind)
    {
        if (spec.Islands is { IsEmpty: false } && spec.Kind != VisualizerKind.Islands) issues.Add("$.islands", $"is for islands visualizers, not {kind}");
        if (spec.Traversal != null && spec.Kind != VisualizerKind.Tree) issues.Add("$.traversal", $"is for tree visualizers, not {kind}");
        if (spec.DetectCycle == true && spec.Kind != VisualizerKind.LinkedList) issues.Add("$.detectCycle", $"is for linked-list visualizers, not {kind}");
        if (spec.Steps.Count > 0 && (spec.Traversal != null || spec.DetectCycle == true))
        {
            issues.Add("$.steps", "the studio records the traversal or cycle detection itself; leave steps out, or leave out traversal and detectCycle");
        }
    }

    /// <summary>The part of the state a kind draws, as it is named in JSON.</summary>
    public static string PartName(VisualizerKind kind) => kind switch
    {
        VisualizerKind.Matrix or VisualizerKind.Islands or VisualizerKind.Board => "grid",
        VisualizerKind.Tree => "tree",
        VisualizerKind.Graph => "graph",
        VisualizerKind.LinkedList => "linkedList",
        VisualizerKind.ArrayPointers => "array",
        VisualizerKind.Bars => "bars",
        _ => "canvas"
    };

    // Checks a state and returns the elements it has; null when it can't be the state of this kind.
    private static VisualizerElements? State(VisualSpecIssues issues, string path, VisualizerKind kind, string kindName, VisualizerState state)
    {
        var part = PartName(kind);
        if (state.PartCount != 1)
        {
            issues.Add(path, state.PartCount == 0
                ? $"a {kindName} visualizer draws {path}.{part}; the state is empty"
                : $"give only one part, {part}: a {kindName} visualizer draws that");
        }

        DrawnFields(issues, path, kind, kindName, state);
        return kind switch
        {
            VisualizerKind.Matrix or VisualizerKind.Islands or VisualizerKind.Board => state.Grid is { } grid ? VisualizerStateRules.Grid(issues, $"{path}.grid", grid) : Missing(),
            VisualizerKind.Tree => state.Tree is { } tree ? VisualizerStateRules.Tree(issues, $"{path}.tree", tree) : Missing(),
            VisualizerKind.Graph => state.Graph is { } graph ? VisualizerStateRules.Graph(issues, $"{path}.graph", graph) : Missing(),
            VisualizerKind.LinkedList => state.LinkedList is { } list ? VisualizerStateRules.LinkedList(issues, $"{path}.linkedList", list) : Missing(),
            VisualizerKind.ArrayPointers => state.Array is { } array ? VisualizerStateRules.Array(issues, $"{path}.array", array) : Missing(),
            VisualizerKind.Bars => state.Bars is { } bars ? VisualizerStateRules.Bars(issues, $"{path}.bars", bars) : Missing(),
            _ => state.Canvas is { } canvas ? VisualizerStateRules.Canvas(issues, $"{path}.canvas", canvas) : Missing()
        };

        VisualizerElements? Missing()
        {
            if (state.PartCount > 0) issues.Add($"{path}.{part}", $"a {kindName} visualizer draws {part}");
            return null;
        }
    }

    // A field its kind never draws is an error, not something silently left out of the picture.
    private static void DrawnFields(VisualSpecIssues issues, string path, VisualizerKind kind, string kindName, VisualizerState state)
    {
        if (state.Grid is { } grid && kind == VisualizerKind.Board)
        {
            if (grid.Islands != null) issues.Add($"{path}.grid.islands", "a board has no islands; they are for matrix and islands visualizers");
            if (grid.ColorMap != null) issues.Add($"{path}.grid.colorMap", "a board has no heat colours; colour cells with color");
            if (grid.InferTerrain != null) issues.Add($"{path}.grid.inferTerrain", "a board has no terrain; it is for matrix and islands visualizers");
            BoardCells(issues, $"{path}.grid.cells", grid.Cells);
        }
        else if (state.Grid is { Checkerboard: not null })
        {
            issues.Add($"{path}.grid.checkerboard", $"is for board visualizers, not {kindName}");
        }

        for (var i = 0; state.Array?.Items != null && i < state.Array.Items.Count; i++)
        {
            ArrayItem(issues, $"{path}.array.items[{i}]", state.Array.Items[i]);
        }

        for (var i = 0; state.Graph != null && i < state.Graph.Nodes.Count; i++)
        {
            var node = state.Graph.Nodes[i];
            if (node.Z != null || node.Size != null) issues.Add($"{path}.graph.nodes[{i}]", "z and size are for 3D graphs; a graph visualizer places nodes by x and y");
        }
    }

    private static void BoardCells(VisualSpecIssues issues, string path, IReadOnlyList<CellSpec>? cells)
    {
        for (var i = 0; cells != null && i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.Terrain != null || cell.Cluster != null || cell.Heat != null || cell.Notes != null)
            {
                issues.Add($"{path}[{i}]", "a board cell has a value, colours, a note, arrows and a state (target or rejected); terrain, clusters, heat and notes are for matrix visualizers");
            }
        }
    }

    private static void ArrayItem(VisualSpecIssues issues, string path, ItemSpec item)
    {
        if (item.Label != null || item.Pointer != null) issues.Add(path, "an array item shows its value and index; labels and pointer tags are for bars (point at items with pointers)");
    }

    private static void Changes(VisualSpecIssues issues, string path, VisualizerKind kind, string kindName, VisualizerChangesSpec changes, VisualizerElements elements)
    {
        var part = PartName(kind);
        var allowsCells = part == "grid";
        var allowsItems = part is "array" or "bars";
        var allowsNodes = part is "tree" or "graph" or "linkedList";
        var allowsEdges = part == "graph";

        if (changes.Cells != null && !allowsCells) issues.Add($"{path}.cells", $"a {kindName} visualizer has no cells to change");
        if (changes.Items != null && !allowsItems) issues.Add($"{path}.items", $"a {kindName} visualizer has no items to change");
        if (changes.Nodes != null && !allowsNodes) issues.Add($"{path}.nodes", $"a {kindName} visualizer has no nodes to change");
        if (changes.Edges != null && !allowsEdges) issues.Add($"{path}.edges", $"a {kindName} visualizer has no edges to change");
        if (part == "canvas") issues.Add(path, "a canvas step draws a whole new state; changes are for grids, arrays, bars, trees, graphs and lists");

        if (allowsCells) VisualizerStateRules.Cells(issues, $"{path}.cells", changes.Cells, elements);
        if (allowsCells && kind == VisualizerKind.Board) BoardCells(issues, $"{path}.cells", changes.Cells);
        if (allowsItems) VisualizerStateRules.Items(issues, $"{path}.items", changes.Items, elements);
        for (var i = 0; part == "array" && changes.Items != null && i < changes.Items.Count; i++) ArrayItem(issues, $"{path}.items[{i}]", changes.Items[i]);
        for (var i = 0; allowsNodes && changes.Nodes != null && i < changes.Nodes.Count; i++)
        {
            if (!elements.Has(ElementRef.Node(changes.Nodes[i].Id))) issues.Add($"{path}.nodes[{i}].id", $"there is no node \"{changes.Nodes[i].Id}\"");
        }

        for (var i = 0; allowsEdges && changes.Edges != null && i < changes.Edges.Count; i++)
        {
            var edge = changes.Edges[i];
            if (!elements.Has(ElementRef.Edge(edge.From, edge.To))) issues.Add($"{path}.edges[{i}]", $"there is no edge from \"{edge.From}\" to \"{edge.To}\"");
        }
    }

    private static void Pointers(VisualSpecIssues issues, string path, IReadOnlyList<PointerSpec>? pointers, VisualizerKind kind, VisualizerElements elements)
    {
        for (var i = 0; pointers != null && i < pointers.Count && !issues.IsFull; i++)
        {
            var pointer = pointers[i];
            if (string.IsNullOrEmpty(pointer.Name)) issues.Add($"{path}[{i}].name", "a pointer needs a name");
            if (PartName(kind) == "canvas") issues.Add($"{path}[{i}]", "a canvas has no pointers; draw a shape instead");
            else if (pointer.At is not { } at) continue; // the variable is null
            else if (at.Kind == ElementRefKind.Item && at.Index >= -1 && at.Index <= elements.ItemCount) continue; // one past either end is where loops stop
            else Reference(issues, $"{path}[{i}].at", at, kind, elements);
        }
    }

    private static void References(VisualSpecIssues issues, string path, IReadOnlyList<ElementRef>? references, VisualizerKind kind, VisualizerElements elements)
    {
        if (references is { Count: > 0 } && PartName(kind) == "canvas")
        {
            issues.Add(path, "a canvas draws its shapes as they are; draw the shape you want to stand out in another colour");
            return;
        }

        for (var i = 0; references != null && i < references.Count && !issues.IsFull; i++)
        {
            Reference(issues, $"{path}[{i}]", references[i], kind, elements);
        }
    }

    // An element of the right sort for the kind (a cell of a grid, an item of an array, a node or edge of a graph...) that exists.
    private static void Reference(VisualSpecIssues issues, string path, ElementRef element, VisualizerKind kind, VisualizerElements elements)
    {
        var part = PartName(kind);
        var expected = part switch
        {
            "grid" => ElementRefKind.Cell,
            "array" or "bars" => ElementRefKind.Item,
            _ => ElementRefKind.Node
        };

        if (element.Kind != expected && !(part == "graph" && element.Kind == ElementRefKind.Edge))
        {
            var what = expected switch
            {
                ElementRefKind.Cell => "a cell, like [2, 3]",
                ElementRefKind.Item => "an item index, like 3",
                _ => part == "graph" ? "a node id, like \"a\", or an edge, like {\"from\": \"a\", \"to\": \"b\"}" : "a node id, like \"a\""
            };
            issues.Add(path, $"must be {what} in a {VisualJsonErrors.JsonName(kind)} visualizer");
        }
        else if (!elements.Has(element))
        {
            issues.Add(path, element.Kind switch
            {
                ElementRefKind.Cell => $"{element} isn't a cell of this {elements.Rows} × {elements.Columns} grid",
                ElementRefKind.Item => $"{element.Index} is outside the {elements.ItemCount} items",
                ElementRefKind.Node => $"there is no {(part == "canvas" ? "shape" : "node")} \"{element.Id}\"",
                _ => $"there is no edge from \"{element.Id}\" to \"{element.To}\""
            });
        }
    }
}
