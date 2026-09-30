using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>
/// A data-structure visualizer (<c>application/vnd.fry.visualizer.v1+json</c>): a grid, tree, graph, list, array, bars,
/// board or canvas in <see cref="State"/>, and optionally the <see cref="Steps"/> of an algorithm walking over it.
/// </summary>
public sealed class VisualizerSpec : VisualSpec
{
    public VisualizerSpec() : base(VisualFamily.Visualizer) { }

    public VisualizerKind Kind { get; set; } = VisualizerKind.Matrix;

    /// <summary>The header's summary pill (default: worked out from the data, e.g. "Nodes: 7").</summary>
    public string? Summary { get; set; }

    /// <summary>What the steps start from, and what is drawn when there are no steps.</summary>
    public VisualizerState State { get; set; } = new();

    /// <summary>Pointers drawn when there are no steps (each step shows its own).</summary>
    public List<PointerSpec>? Pointers { get; set; }

    /// <summary>Elements lit up when there are no steps (each step lights up its own).</summary>
    public List<ElementRef>? Highlight { get; set; }

    public List<VisualizerStepSpec> Steps { get; set; } = [];

    /// <summary>Row and column numbers around a grid (default: on).</summary>
    public bool? ShowCoordinates { get; set; }

    /// <summary>The values inside cells and bars (default: on).</summary>
    public bool? ShowValues { get; set; }

    /// <summary>A grid cell's size in pixels at 100% (default: 38).</summary>
    public double? CellSize { get; set; }

    /// <summary>Shrink the drawing to fit when it first appears, if it would otherwise be cut off (default: on).</summary>
    public bool? FitOnOpen { get; set; }

    /// <summary>For <see cref="VisualizerKind.Islands"/>: how the studio finds the islands and records its search.</summary>
    public IslandsOptionsSpec? Islands { get; set; }

    /// <summary>For a tree without steps: have the studio record this traversal.</summary>
    public TreeTraversal? Traversal { get; set; }

    /// <summary>For a linked list without steps: have the studio record Floyd's cycle detection.</summary>
    public bool? DetectCycle { get; set; }
}

/// <summary>A traversal the studio can record over a tree.</summary>
public enum TreeTraversal
{
    Preorder,
    Inorder,
    Postorder,
    LevelOrder
}

public sealed class IslandsOptionsSpec : IOptionalSpec
{
    /// <summary>Only up, down, left and right count as touching (default: yes; no adds the diagonals).</summary>
    public bool? FourDirectional { get; set; }

    /// <summary>Record the search step by step (default: yes).</summary>
    public bool? RecordSteps { get; set; }

    [JsonIgnore]
    public bool IsEmpty => FourDirectional == null && RecordSteps == null;
}

/// <summary>
/// The drawable content of a visualizer or of one step. Exactly one part is set, the one its kind uses:
/// <see cref="Grid"/> for matrix, islands and board; <see cref="Array"/> for arrayPointers; the rest by name.
/// </summary>
public sealed class VisualizerState
{
    public GridState? Grid { get; set; }
    public TreeState? Tree { get; set; }
    public GraphSpec? Graph { get; set; }
    public LinkedListState? LinkedList { get; set; }
    public ArrayState? Array { get; set; }
    public BarsState? Bars { get; set; }
    public CanvasState? Canvas { get; set; }

    /// <summary>How many parts are set; a valid state has exactly one.</summary>
    [JsonIgnore]
    public int PartCount =>
        (Grid != null ? 1 : 0) + (Tree != null ? 1 : 0) + (Graph != null ? 1 : 0) + (LinkedList != null ? 1 : 0) +
        (Array != null ? 1 : 0) + (Bars != null ? 1 : 0) + (Canvas != null ? 1 : 0);
}

/// <summary>
/// One step of an algorithm. It shows a new <see cref="State"/>, or the previous state with <see cref="Changes"/>
/// applied, or (with neither) the previous state as it was. Its highlights, pointers and notes belong to it alone.
/// </summary>
public sealed class VisualizerStepSpec
{
    public string? Description { get; set; }

    public VisualizerState? State { get; set; }
    public VisualizerChangesSpec? Changes { get; set; }

    /// <summary>Elements lit up in this step, e.g. the cell being visited or the current window of an array.</summary>
    public List<ElementRef>? Highlight { get; set; }

    public List<PointerSpec>? Pointers { get; set; }

    /// <summary>Facts about this step shown beside the drawing, e.g. <c>{"sum": "12"}</c>.</summary>
    public Dictionary<string, string>? Notes { get; set; }

    /// <summary>Collections the algorithm keeps (a queue, a stack, a set), as they were at this step.</summary>
    public List<WatchSpec>? Watches { get; set; }

    /// <summary>The 1-based line of code that recorded the step, to highlight it in the editor.</summary>
    public int? Line { get; set; }

    /// <summary>Which code that line is in (a notebook cell), when it isn't the code that showed the visualizer.</summary>
    public string? Source { get; set; }
}

/// <summary>
/// What a step changes in the previous state, element by element; only the fields given change. Adding or removing
/// elements takes a whole new state.
/// </summary>
public sealed class VisualizerChangesSpec
{
    /// <summary>Grid cells, by row and column.</summary>
    public List<CellSpec>? Cells { get; set; }

    /// <summary>Array items and bars, by index.</summary>
    public List<ItemSpec>? Items { get; set; }

    /// <summary>Tree, graph and list nodes, by id.</summary>
    public List<NodeChangeSpec>? Nodes { get; set; }

    /// <summary>Graph edges, by their two ends.</summary>
    public List<EdgeChangeSpec>? Edges { get; set; }
}

public sealed class NodeChangeSpec
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    public ScalarValue? Value { get; set; }
    public ElementState? State { get; set; }
    public string? Color { get; set; }
    public string? Pointer { get; set; }
    public string? Note { get; set; }
}

public sealed class EdgeChangeSpec
{
    [JsonRequired]
    public string From { get; set; } = string.Empty;
    [JsonRequired]
    public string To { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public string? Label { get; set; }
    public ElementState? State { get; set; }
    public string? Color { get; set; }
}

/// <summary>A collection the algorithm keeps, as it was at a step: its first items in the order they come out.</summary>
public sealed class WatchSpec
{
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    public WatchKind Kind { get; set; } = WatchKind.Value;
    public List<string> Items { get; set; } = [];

    /// <summary>How many items it held; <see cref="Items"/> may show fewer.</summary>
    public int Count { get; set; }
}
