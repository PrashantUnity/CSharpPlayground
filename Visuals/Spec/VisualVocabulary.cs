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

    /// <summary>One slice for each value; with several series, one ring for each series (the first outermost).</summary>
    Pie,
    Donut,

    /// <summary>Counts of each series' <c>values</c>, grouped into <c>bins</c> by the studio.</summary>
    Histogram,

    /// <summary>Circles at (x, y) as big as the series' <c>sizes</c>.</summary>
    Bubble,

    /// <summary>Each series is a polygon over spokes: one spoke for each value, named by the first series' <c>labels</c>.</summary>
    Radar,

    /// <summary>A pie whose slices are all the same angle and reach as far out as their value.</summary>
    PolarArea
}

/// <summary>Which way a bar chart's bars run.</summary>
public enum ChartOrientation
{
    /// <summary>Bars stand on the x axis (the default).</summary>
    Vertical,

    /// <summary>Bars lie along the y axis, categories running down it.</summary>
    Horizontal
}

/// <summary>How series that share an axis are piled up.</summary>
public enum ChartStack
{
    /// <summary>Each series is drawn from the baseline (the default).</summary>
    None,

    /// <summary>Each series starts where the one under it ends.</summary>
    Stacked,

    /// <summary>Stacked, and every position scaled to 100%.</summary>
    Percent
}

/// <summary>How an axis maps values to positions.</summary>
public enum AxisScale
{
    /// <summary>Evenly spaced numbers (the default).</summary>
    Linear,

    /// <summary>A power of ten for each step; every value must be above zero.</summary>
    Log,

    /// <summary>Dates and times: x holds Unix milliseconds (UTC), and the ticks fall on calendar units. Only the x axis.</summary>
    Time,

    /// <summary>One slot for each value, named by its label. Only the x axis.</summary>
    Category
}

/// <summary>Which value axis a series is measured on.</summary>
public enum AxisSide
{
    Left,
    Right
}

/// <summary>Where the series legend goes.</summary>
public enum LegendPosition
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>How a line is broken up.</summary>
public enum LineDash
{
    Solid,
    Dashed,
    Dotted
}

/// <summary>How a line gets from one value to the next.</summary>
public enum LineInterpolation
{
    /// <summary>A straight segment (the default).</summary>
    Linear,

    /// <summary>A smooth curve through the values; <c>tension</c> sets how round (it may overshoot between values).</summary>
    Smooth,

    /// <summary>A smooth curve that never overshoots: it only rises or falls where the values do.</summary>
    Monotone
}

/// <summary>Where a stepped line changes level.</summary>
public enum LineStep
{
    /// <summary>Not stepped (the default).</summary>
    None,

    /// <summary>The line holds a value until the next x, then jumps there.</summary>
    Before,

    /// <summary>The line jumps to the next value straight after a point, then runs flat to it.</summary>
    After,

    /// <summary>The line jumps halfway between two x values.</summary>
    Middle
}

/// <summary>The shape of a marker.</summary>
public enum PointShape
{
    Circle,
    Triangle,
    Square,
    Diamond,
    Cross,
    Star
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
