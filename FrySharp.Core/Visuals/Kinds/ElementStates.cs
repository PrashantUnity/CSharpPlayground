using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// The spec's one state vocabulary against each model's own. Every model state has a spec name; a spec state a model
/// can't draw becomes the nearest one it can, or the default.
/// </summary>
internal static class ElementStates
{
    public static GridCellState ToGrid(ElementState state) => state switch
    {
        ElementState.Current => GridCellState.Current,
        ElementState.Visited or ElementState.Done => GridCellState.Visited,
        ElementState.Frontier => GridCellState.Frontier,
        ElementState.Path or ElementState.Matched => GridCellState.Path,
        ElementState.Target => GridCellState.Target,
        ElementState.Start => GridCellState.Start,
        ElementState.Wall => GridCellState.Wall,
        ElementState.Candidate or ElementState.Pivot => GridCellState.Candidate,
        ElementState.Backtracked or ElementState.Pruned or ElementState.Rejected => GridCellState.Backtracked,
        _ => GridCellState.Default
    };

    public static ElementState FromGrid(GridCellState state) => state switch
    {
        GridCellState.Current => ElementState.Current,
        GridCellState.Visited => ElementState.Visited,
        GridCellState.Frontier => ElementState.Frontier,
        GridCellState.Path => ElementState.Path,
        GridCellState.Target => ElementState.Target,
        GridCellState.Start => ElementState.Start,
        GridCellState.Wall => ElementState.Wall,
        GridCellState.Candidate => ElementState.Candidate,
        GridCellState.Backtracked => ElementState.Backtracked,
        _ => ElementState.Default // Custom: the cell's colour says it
    };

    public static TreeNodeState ToTree(ElementState state) => state switch
    {
        ElementState.Current => TreeNodeState.Current,
        ElementState.Visited or ElementState.Relaxed or ElementState.Done => TreeNodeState.Visited,
        ElementState.Target or ElementState.Cycle => TreeNodeState.Target,
        ElementState.Path => TreeNodeState.Path,
        ElementState.Matched => TreeNodeState.Matched,
        ElementState.Candidate or ElementState.Frontier or ElementState.Pivot => TreeNodeState.Candidate,
        ElementState.Backtracked => TreeNodeState.Backtracked,
        ElementState.Pruned or ElementState.Rejected or ElementState.Unreachable => TreeNodeState.Pruned,
        ElementState.Swapped => TreeNodeState.Swapped,
        _ => TreeNodeState.Default
    };

    public static ElementState FromTree(TreeNodeState state) => state switch
    {
        TreeNodeState.Current => ElementState.Current,
        TreeNodeState.Visited => ElementState.Visited,
        TreeNodeState.Target => ElementState.Target,
        TreeNodeState.Path => ElementState.Path,
        TreeNodeState.Matched => ElementState.Matched,
        TreeNodeState.Candidate => ElementState.Candidate,
        TreeNodeState.Backtracked => ElementState.Backtracked,
        TreeNodeState.Pruned => ElementState.Pruned,
        TreeNodeState.Swapped => ElementState.Swapped,
        _ => ElementState.Default
    };

    public static GraphNodeState ToGraphNode(ElementState state) => state switch
    {
        ElementState.Current or ElementState.Start => GraphNodeState.Current,
        ElementState.Visited or ElementState.Backtracked or ElementState.Done => GraphNodeState.Visited,
        ElementState.Target => GraphNodeState.Target,
        ElementState.Path or ElementState.Matched => GraphNodeState.Path,
        ElementState.Frontier or ElementState.Candidate or ElementState.Pivot => GraphNodeState.Frontier,
        ElementState.Relaxed => GraphNodeState.Relaxed,
        ElementState.Cycle => GraphNodeState.Cycle,
        ElementState.Unreachable or ElementState.Pruned or ElementState.Rejected => GraphNodeState.Unreachable,
        _ => GraphNodeState.Default
    };

    public static ElementState FromGraphNode(GraphNodeState state) => state switch
    {
        GraphNodeState.Current => ElementState.Current,
        GraphNodeState.Visited => ElementState.Visited,
        GraphNodeState.Target => ElementState.Target,
        GraphNodeState.Path => ElementState.Path,
        GraphNodeState.Frontier => ElementState.Frontier,
        GraphNodeState.Relaxed => ElementState.Relaxed,
        GraphNodeState.Cycle => ElementState.Cycle,
        GraphNodeState.Unreachable => ElementState.Unreachable,
        _ => ElementState.Default
    };

    public static GraphEdgeState ToGraphEdge(ElementState state) => state switch
    {
        ElementState.Current or ElementState.Frontier or ElementState.Candidate or ElementState.Pivot => GraphEdgeState.Active,
        ElementState.Visited or ElementState.Backtracked or ElementState.Done => GraphEdgeState.Visited,
        ElementState.Relaxed => GraphEdgeState.Relaxed,
        ElementState.Path or ElementState.Matched or ElementState.Target => GraphEdgeState.Path,
        ElementState.Rejected or ElementState.Cycle or ElementState.Pruned or ElementState.Unreachable => GraphEdgeState.Rejected,
        ElementState.CrossEdge => GraphEdgeState.CrossEdge,
        _ => GraphEdgeState.Default
    };

    public static ElementState FromGraphEdge(GraphEdgeState state) => state switch
    {
        GraphEdgeState.Active => ElementState.Current,
        GraphEdgeState.Visited => ElementState.Visited,
        GraphEdgeState.Relaxed => ElementState.Relaxed,
        GraphEdgeState.Path => ElementState.Path,
        GraphEdgeState.Rejected => ElementState.Rejected,
        GraphEdgeState.CrossEdge => ElementState.CrossEdge,
        _ => ElementState.Default
    };

    /// <summary>Null for the default, so a state with nothing to say isn't written.</summary>
    public static ElementState? OrNull(ElementState state) => state == ElementState.Default ? null : state;
}
