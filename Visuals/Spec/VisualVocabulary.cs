using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

// The words every visual spec is written in. In JSON they are camelCase strings ("line", "arrayPointers", "crossEdge").

/// <summary>The three kinds of visual, each with its own spec and MIME type.</summary>
public enum VisualFamily
{
    Chart,
    Plot3D,
    Visualizer
}

/// <summary>How a 2D chart draws its series.</summary>
public enum ChartType
{
    Line,
    Area,
    Bar,
    Scatter,
    Pie,
    Donut,

    /// <summary>Counts of each series' <c>values</c>, grouped into <c>bins</c> by the studio.</summary>
    Histogram
}

/// <summary>How a 3D plot draws its data. Every kind puts data z upward.</summary>
public enum Plot3DType
{
    Scatter,
    Surface,
    Wireframe,
    Trajectory,

    [JsonStringEnumMemberName("graph")]
    Graph3D,
    VoxelBar
}

/// <summary>The colour scale a 3D plot maps heights or values onto.</summary>
public enum ColorMapPreset
{
    Viridis,
    Plasma,
    CoolWarm,
    Turbo,
    Rainbow,
    Ocean,
    Fire
}

/// <summary>What a data-structure visualizer draws.</summary>
public enum VisualizerKind
{
    Matrix,

    /// <summary>A matrix whose islands of land the studio finds, step by step.</summary>
    Islands,
    Tree,
    Graph,
    LinkedList,
    ArrayPointers,
    Bars,
    Board,
    Canvas
}

/// <summary>What a grid cell is, as opposed to what a traversal is doing to it (<see cref="ElementState"/>).</summary>
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

/// <summary>
/// What an algorithm is doing to a cell, node, edge or item. One vocabulary for every visualizer; each draws the states
/// that mean something for it and draws the rest as <see cref="Default"/>.
/// </summary>
public enum ElementState
{
    Default,
    Current,
    Visited,
    Frontier,
    Path,
    Target,
    Start,
    Wall,
    Candidate,
    Matched,
    Backtracked,
    Pruned,
    Swapped,
    Relaxed,
    Cycle,
    Unreachable,
    Rejected,
    CrossEdge,
    Pivot,
    Done
}

/// <summary>The kind of collection a step's watch shows (items in the order they come out).</summary>
public enum WatchKind
{
    Value,
    List,
    Queue,
    Stack,
    PriorityQueue,
    Set,
    Map
}
