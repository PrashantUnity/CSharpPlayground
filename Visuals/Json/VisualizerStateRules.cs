namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>The elements a visualizer state has, so highlights, pointers and changes can be checked against them.</summary>
internal sealed class VisualizerElements
{
    public int Rows { get; init; }
    public int Columns { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlySet<string> NodeIds { get; init; } = new HashSet<string>();
    public IReadOnlySet<(string From, string To)> Edges { get; init; } = new HashSet<(string, string)>();

    public bool Has(ElementRef element) => element.Kind switch
    {
        ElementRefKind.Cell => element.Row >= 0 && element.Row < Rows && element.Col >= 0 && element.Col < Columns,
        ElementRefKind.Item => element.Index >= 0 && element.Index < ItemCount,
        ElementRefKind.Node => NodeIds.Contains(element.Id!),
        _ => Edges.Contains((element.Id!, element.To!)) || Edges.Contains((element.To!, element.Id!))
    };
}

/// <summary>What each part of a visualizer state must satisfy; each check returns the elements the part has.</summary>
internal static class VisualizerStateRules
{
    public static VisualizerElements Grid(VisualSpecIssues issues, string path, GridState grid)
    {
        var rows = grid.Values.Count;
        var columns = rows > 0 ? grid.Values[0].Count : 0;
        for (var r = 1; r < rows && !issues.IsFull; r++)
        {
            if (grid.Values[r].Count != columns) issues.Add($"{path}.values[{r}]", $"has {grid.Values[r].Count} cells but row 0 has {columns}; a grid is rectangular");
        }

        var elements = new VisualizerElements { Rows = rows, Columns = columns };
        Cells(issues, $"{path}.cells", grid.Cells, elements);

        for (var i = 0; grid.Islands != null && i < grid.Islands.Count && !issues.IsFull; i++)
        {
            var cells = grid.Islands[i].Cells;
            for (var c = 0; c < cells.Count; c++)
            {
                if (cells[c].Kind != ElementRefKind.Cell || !elements.Has(cells[c])) issues.Add($"{path}.islands[{i}].cells[{c}]", $"{cells[c]} isn't a cell of this {rows} × {columns} grid");
            }
        }

        return elements;
    }

    /// <summary>Cells dressed in a state or changed by a step: each inside the grid.</summary>
    public static void Cells(VisualSpecIssues issues, string path, IReadOnlyList<CellSpec>? cells, VisualizerElements grid)
    {
        for (var i = 0; cells != null && i < cells.Count && !issues.IsFull; i++)
        {
            var cell = cells[i];
            if (!grid.Has(ElementRef.Cell(cell.Row, cell.Col))) issues.Add($"{path}[{i}]", $"[{cell.Row}, {cell.Col}] isn't a cell of this {grid.Rows} × {grid.Columns} grid");
            issues.Finite($"{path}[{i}].heat", cell.Heat);
            for (var a = 0; cell.Arrows != null && a < cell.Arrows.Count; a++)
            {
                var to = cell.Arrows[a].To;
                if (to.Kind != ElementRefKind.Cell || !grid.Has(to)) issues.Add($"{path}[{i}].arrows[{a}].to", $"{to} isn't a cell of this grid");
            }
        }
    }

    public static VisualizerElements Tree(VisualSpecIssues issues, string path, TreeState tree)
    {
        var ids = issues.UniqueIds($"{path}.nodes", tree.Nodes, node => node.Id, "node");
        var byId = tree.Nodes.Where(n => !string.IsNullOrEmpty(n.Id)).GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < tree.Nodes.Count && !issues.IsFull; i++)
        {
            var node = tree.Nodes[i];
            var nodePath = $"{path}.nodes[{i}]";
            if ((node.Left != null || node.Right != null) && node.Children is { Count: > 0 })
            {
                issues.Add(nodePath, "a node has left and right (binary) or children, not both");
            }

            foreach (var (child, field) in ChildrenOf(node))
            {
                if (!ids.Contains(child)) issues.Add($"{nodePath}.{field}", $"there is no node \"{child}\"");
                else if (!parents.TryAdd(child, node.Id)) issues.Add($"{nodePath}.{field}", $"\"{child}\" already has a parent (\"{parents[child]}\"); a tree node has one");
            }
        }

        var root = tree.Root ?? (tree.Nodes.Count > 0 ? tree.Nodes[0].Id : null);
        if (root != null && !ids.Contains(root)) issues.Add($"{path}.root", $"there is no node \"{root}\"");
        else if (root != null && parents.TryGetValue(root, out var rootParent)) issues.Add($"{path}.root", $"\"{root}\" has a parent (\"{rootParent}\"), so it can't be the root");
        else if (root != null && !issues.IsFull) Reachable(issues, path, root, byId, tree.Nodes.Count);

        return new VisualizerElements { NodeIds = ids };
    }

    private static IEnumerable<(string Child, string Field)> ChildrenOf(TreeNodeSpec node)
    {
        if (node.Left != null) yield return (node.Left, "left");
        if (node.Right != null) yield return (node.Right, "right");
        for (var c = 0; node.Children != null && c < node.Children.Count; c++) yield return (node.Children[c], $"children[{c}]");
    }

    // Every node hangs under the root: none is left over (drawn nowhere) and none is its own ancestor.
    private static void Reachable(VisualSpecIssues issues, string path, string root, Dictionary<string, TreeNodeSpec> byId, int nodeCount)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { root };
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            if (!byId.TryGetValue(pending.Pop(), out var node)) continue;
            foreach (var (child, _) in ChildrenOf(node))
            {
                if (byId.ContainsKey(child) && seen.Add(child)) pending.Push(child);
            }
        }

        if (seen.Count < nodeCount)
        {
            var missing = byId.Keys.Where(id => !seen.Contains(id)).Take(3).Select(id => $"\"{id}\"");
            issues.Add($"{path}.nodes", $"{string.Join(", ", missing)} can't be reached from the root \"{root}\"; every node needs a place in the tree");
        }
    }

    public static VisualizerElements Graph(VisualSpecIssues issues, string path, GraphSpec graph)
    {
        var ids = GraphSpecRules.Check(issues, path, graph);
        var edges = new HashSet<(string, string)>(graph.Edges.Select(e => (e.From, e.To)));
        return new VisualizerElements { NodeIds = ids, Edges = edges };
    }

    public static VisualizerElements LinkedList(VisualSpecIssues issues, string path, LinkedListState list)
    {
        var ids = issues.UniqueIds($"{path}.nodes", list.Nodes, node => node.Id, "node");
        for (var i = 0; i < list.Nodes.Count && !issues.IsFull; i++)
        {
            if (list.Nodes[i].Next is { } next && !ids.Contains(next)) issues.Add($"{path}.nodes[{i}].next", $"there is no node \"{next}\"");
        }

        if (list.Head != null && !ids.Contains(list.Head)) issues.Add($"{path}.head", $"there is no node \"{list.Head}\"");
        for (var i = 0; list.Chains != null && i < list.Chains.Count; i++)
        {
            if (!ids.Contains(list.Chains[i])) issues.Add($"{path}.chains[{i}]", $"there is no node \"{list.Chains[i]}\"");
        }

        return new VisualizerElements { NodeIds = ids };
    }

    public static VisualizerElements Array(VisualSpecIssues issues, string path, ArrayState array)
    {
        var elements = new VisualizerElements { ItemCount = array.Values.Count };
        Items(issues, $"{path}.items", array.Items, elements);
        return elements;
    }

    public static VisualizerElements Bars(VisualSpecIssues issues, string path, BarsState bars)
    {
        issues.Finite($"{path}.values", bars.Values);
        issues.Finite($"{path}.min", bars.Min);
        issues.Finite($"{path}.max", bars.Max);
        if (bars.Min is { } min && bars.Max is { } max && min >= max) issues.Add(path, "min must be below max");

        var elements = new VisualizerElements { ItemCount = bars.Values.Count };
        Items(issues, $"{path}.items", bars.Items, elements);
        if (bars.Shade is { } shade)
        {
            if (shade.From < 0 || shade.To >= bars.Values.Count || shade.From > shade.To) issues.Add($"{path}.shade", $"must cover bars from 0 to {bars.Values.Count - 1}, from before to");
            issues.Finite($"{path}.shade.level", shade.Level);
            issues.Finite($"{path}.shade.floor", shade.Floor);
        }

        return elements;
    }

    /// <summary>Items dressed in a state or changed by a step: each inside the array.</summary>
    public static void Items(VisualSpecIssues issues, string path, IReadOnlyList<ItemSpec>? items, VisualizerElements array)
    {
        for (var i = 0; items != null && i < items.Count && !issues.IsFull; i++)
        {
            if (!array.Has(ElementRef.Item(items[i].Index))) issues.Add($"{path}[{i}].index", $"{items[i].Index} is outside the {array.ItemCount} items");
        }
    }

    public static VisualizerElements Canvas(VisualSpecIssues issues, string path, CanvasState canvas)
    {
        if (canvas.Width is { } width && !(double.IsFinite(width) && width > 0)) issues.Add($"{path}.width", "must be a positive number of pixels");
        if (canvas.Height is { } height && !(double.IsFinite(height) && height > 0)) issues.Add($"{path}.height", "must be a positive number of pixels");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < canvas.Shapes.Count && !issues.IsFull; i++)
        {
            var shapePath = $"{path}.shapes[{i}]";
            var shape = canvas.Shapes[i];
            if (shape.Id != null && !ids.Add(shape.Id)) issues.Add($"{shapePath}.id", $"\"{shape.Id}\" is used by another shape; ids must be unique");
            if (shape.Opacity is { } opacity && !(opacity >= 0 && opacity <= 1)) issues.Add($"{shapePath}.opacity", "must be between 0 and 1");
            switch (shape)
            {
                case RectShapeSpec rect when !(rect.Width >= 0 && rect.Height >= 0 && double.IsFinite(rect.Width + rect.Height + rect.X + rect.Y)):
                    issues.Add(shapePath, "a rect needs a finite position and a size of zero or more");
                    break;
                case CircleShapeSpec circle when !(double.IsFinite(circle.X + circle.Y) && circle.Radius is null or > 0):
                    issues.Add(shapePath, "a circle needs a finite centre and a positive radius");
                    break;
                case LineShapeSpec line when !double.IsFinite(line.X1 + line.Y1 + line.X2 + line.Y2):
                case ArrowShapeSpec arrow when !double.IsFinite(arrow.X1 + arrow.Y1 + arrow.X2 + arrow.Y2):
                case TextShapeSpec text when !double.IsFinite(text.X + text.Y):
                    issues.Add(shapePath, "coordinates must be finite numbers");
                    break;
            }
        }

        return new VisualizerElements { NodeIds = ids };
    }
}
