using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

// The drawable content of each kind of visualizer. Structures are flat (nodes refer to each other by id), so any depth
// fits in JSON and is easy to write from any language.

/// <summary>
/// A grid of values, for matrix, islands and board visualizers. <see cref="Values"/> holds the rows; <see cref="Cells"/>
/// dresses individual cells (only those that differ from plain).
/// </summary>
public sealed class GridState
{
    public List<List<ScalarValue>> Values { get; set; } = [];
    public List<CellSpec>? Cells { get; set; }
    public List<string>? RowHeaders { get; set; }
    public List<string>? ColumnHeaders { get; set; }

    /// <summary>Islands to outline (an islands visualizer finds its own).</summary>
    public List<IslandSpec>? Islands { get; set; }

    /// <summary>Shade alternate cells, like a chess board.</summary>
    public bool? Checkerboard { get; set; }

    /// <summary>The colour scale for <see cref="CellSpec.Heat"/>.</summary>
    public string? ColorMap { get; set; }

    /// <summary>
    /// Read what a plain value says a cell is (default: yes): 1 is land and 0 water, "S" the start, "T" or "E" the
    /// target, "#" or "wall" a wall. A cell's own <see cref="CellSpec.Terrain"/> always wins.
    /// </summary>
    public bool? InferTerrain { get; set; }
}

/// <summary>One grid cell's look. In a step's changes, only the fields given change.</summary>
public sealed class CellSpec
{
    [JsonRequired]
    public int Row { get; set; }
    [JsonRequired]
    public int Col { get; set; }

    public ScalarValue? Value { get; set; }

    /// <summary>What the cell is: land, water, a wall, the start…</summary>
    public CellKind? Terrain { get; set; }

    public ElementState? State { get; set; }

    /// <summary>The island the cell belongs to.</summary>
    public int? Cluster { get; set; }

    public string? Color { get; set; }
    public string? BorderColor { get; set; }
    public string? TextColor { get; set; }

    /// <summary>0 to 1, drawn on the grid's colour scale.</summary>
    public double? Heat { get; set; }

    /// <summary>A small second value in the corner, like a cost or a DP entry.</summary>
    public string? Note { get; set; }

    /// <summary>More facts shown when the pointer rests on the cell.</summary>
    public Dictionary<string, string>? Notes { get; set; }

    /// <summary>Arrows from this cell to others, like the predecessor a path came from.</summary>
    public List<ArrowSpec>? Arrows { get; set; }
}

public sealed class ArrowSpec
{
    [JsonRequired]
    public ElementRef To { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
}

public sealed class IslandSpec
{
    [JsonRequired]
    public int Id { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
    public List<ElementRef> Cells { get; set; } = [];
}

/// <summary>
/// A tree as a list of nodes. A binary node names its children <see cref="TreeNodeSpec.Left"/> and
/// <see cref="TreeNodeSpec.Right"/>; any other node lists them in <see cref="TreeNodeSpec.Children"/>.
/// </summary>
public sealed class TreeState
{
    /// <summary>The root's id (default: the first node).</summary>
    public string? Root { get; set; }

    public List<TreeNodeSpec> Nodes { get; set; } = [];
}

public sealed class TreeNodeSpec
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    public ScalarValue Value { get; set; }
    public string? Left { get; set; }
    public string? Right { get; set; }
    public List<string>? Children { get; set; }
    public ElementState? State { get; set; }
    public string? Color { get; set; }

    /// <summary>A small tag drawn next to the node, like a pointer's name.</summary>
    public string? Pointer { get; set; }

    /// <summary>A second, smaller line of text on the node.</summary>
    public string? Note { get; set; }

    /// <summary>More facts shown when the pointer rests on the node.</summary>
    public Dictionary<string, string>? Notes { get; set; }
}

/// <summary>A linked list as its nodes; each names the next. A cycle is simply a next that points back.</summary>
public sealed class LinkedListState
{
    public List<ListNodeSpec> Nodes { get; set; } = [];

    /// <summary>The first node's id (default: the first node).</summary>
    public string? Head { get; set; }

    /// <summary>The heads of other lists drawn with it, e.g. the two lists being merged.</summary>
    public List<string>? Chains { get; set; }

    /// <summary>Draw each chain on its own row instead of one after another.</summary>
    public bool? StackChains { get; set; }

    /// <summary>
    /// Mark the node a cycle closes on (default: yes); the studio finds the cycle from the next pointers. A node in the
    /// <c>cycle</c> state is always marked.
    /// </summary>
    public bool? MarkCycle { get; set; }
}

public sealed class ListNodeSpec
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    public ScalarValue Value { get; set; }
    public string? Next { get; set; }
    public ElementState? State { get; set; }
    public string? Color { get; set; }

    /// <summary>The list goes on past this node, but no further nodes are drawn.</summary>
    public bool? Truncated { get; set; }
}

/// <summary>An array's values; <see cref="Items"/> dresses individual items. Pointers come with each step.</summary>
public sealed class ArrayState
{
    public List<ScalarValue> Values { get; set; } = [];
    public List<ItemSpec>? Items { get; set; }
}

/// <summary>One array item's or bar's look. In a step's changes, only the fields given change.</summary>
public sealed class ItemSpec
{
    [JsonRequired]
    public int Index { get; set; }
    public ScalarValue? Value { get; set; }
    public ElementState? State { get; set; }
    public string? Color { get; set; }

    /// <summary>A name under the item or bar.</summary>
    public string? Label { get; set; }

    /// <summary>A small tag drawn over the bar, like a pointer's name.</summary>
    public string? Pointer { get; set; }
}

/// <summary>Values drawn as bars, as a sorting algorithm moves them.</summary>
public sealed class BarsState
{
    public List<double?> Values { get; set; } = [];
    public List<ItemSpec>? Items { get; set; }

    /// <summary>The value axis (default: from zero, or the lowest value, to the highest).</summary>
    public double? Min { get; set; }
    public double? Max { get; set; }

    /// <summary>The index under each bar (default: on).</summary>
    public bool? ShowIndices { get; set; }

    /// <summary>A line of text under the bars.</summary>
    public string? Description { get; set; }

    /// <summary>A band drawn across a run of bars, like the water trapped between them.</summary>
    public ShadeSpec? Shade { get; set; }
}

public sealed class ShadeSpec
{
    [JsonRequired]
    public int From { get; set; }
    [JsonRequired]
    public int To { get; set; }
    [JsonRequired]
    public double Level { get; set; }
    public double Floor { get; set; }
    public string? Label { get; set; }
}
