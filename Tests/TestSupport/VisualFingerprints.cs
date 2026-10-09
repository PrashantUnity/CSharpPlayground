using System.Globalization;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// What each renderer reads from a model, written out as text, so two models compare equal exactly when they draw the
/// same: a node's effective style and halo (state, else active, else visited), a list's cycle, an array's pointers.
/// </summary>
internal static class VisualFingerprints
{
    private static string N(double value) => double.IsNaN(value) ? "NaN" : Math.Round(value, 9).ToString("R", CultureInfo.InvariantCulture);

    private static string N(double? value) => value is { } v ? N(v) : "-";

    private static string Notes(Dictionary<string, object?> metadata) =>
        string.Join(",", metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));

    public static string Of(ChartOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{o.Title}|{o.Subtitle}|{o.Type}|{o.PrimaryColor}|{o.ShowGrid}|{o.ShowPoints}|{o.ShowStats}|{o.ShowLegend}|{N(o.Width)}|{N(o.Height)}");
        sb.AppendLine($"layout {o.LegendPosition}|{o.Orientation}|{o.Stack}|{N(o.StartAngle)}|{N(o.Sweep)}|{N(o.Cutout)}");
        sb.AppendLine($"x {Axis(o.XAxis)}");
        sb.AppendLine($"y {Axis(o.YAxis)}");
        sb.AppendLine($"y2 {(o.Y2Axis == null ? "none" : Axis(o.Y2Axis))}");
        foreach (var s in o.Series)
        {
            sb.AppendLine($"series {s.Name}|{s.Color}|{N(s.StrokeThickness)}");
            sb.AppendLine($"  style {s.Kind}|{s.Axis}|{s.StackGroup}|{s.Dash}|{s.Interpolation}|{(s.Interpolation == LineInterpolation.Smooth ? N(s.Tension) : "-")}|{s.Step}|{s.Fill}|{s.FillTo}|{s.PointStyle}|{N(s.PointRadius)}|{s.ColorSegments}|{N(s.CornerRadius)}");
            foreach (var p in s.Points) sb.AppendLine($"  {N(p.X)}|{N(p.Y)}|{p.Label}|{p.CustomColor}|{N(p.Size)}|{N(p.From)}");
        }

        return sb.ToString();
    }

    private static string Axis(ChartAxisOptions a) => $"{a.Title}|{N(a.Min)}|{N(a.Max)}|{N(a.SuggestedMin)}|{N(a.SuggestedMax)}|{a.Scale}|{a.Reverse}";

    public static string Of(Plot3DOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{o.Title}|{o.Type}|{o.ColorMap}|{o.PrimaryColor}|{N(o.Width)}|{N(o.Height)}|{o.ShowAxes}|{o.ShowFloorGrid}|{o.ShowBoundingBox}|{o.ShowLabels}|{o.Wireframe}|{o.AutoRotate}");
        sb.AppendLine($"bounds {N(o.MinX)} {N(o.MaxX)} {N(o.MinY)} {N(o.MaxY)} {N(o.MinZ)} {N(o.MaxZ)}");
        foreach (var s in o.Series)
        {
            // The colour drawn: a 3D series without one is drawn in the plot's primary colour.
            sb.AppendLine($"series {s.Name}|{(string.IsNullOrEmpty(s.Color) ? o.PrimaryColor : s.Color)}");
            foreach (var p in s.Points) sb.AppendLine($"  {N(p.X)}|{N(p.Y)}|{N(p.Z)}|{p.Label}|{p.CustomColor}|{N(p.Size)}");
        }

        if (o.Surface is { } surface)
        {
            sb.AppendLine($"surface {surface.ResolutionX}x{surface.ResolutionY} {N(surface.MinX)} {N(surface.MaxX)} {N(surface.MinY)} {N(surface.MaxY)} {N(surface.MinZ)} {N(surface.MaxZ)}");
            for (var i = 0; i < surface.ResolutionX; i++)
            {
                sb.AppendLine("  " + string.Join(" ", Enumerable.Range(0, surface.ResolutionY).Select(j => N(surface.ZValues[i, j]))));
            }
        }

        if (o.Graph is { } graph)
        {
            foreach (var n in graph.Nodes) sb.AppendLine($"node {n.Id}|{n.Label}|{N(n.X)}|{N(n.Y)}|{N(n.Z)}|{n.Color}|{N(n.Radius)}");
            foreach (var e in graph.Edges) sb.AppendLine($"edge {e.FromId}|{e.ToId}|{N(e.Weight)}|{e.Color}|{e.IsDirected}");
        }

        return sb.ToString();
    }

    /// <summary>The visualizer as the control shows it: its header, then its data before any step, then each step.</summary>
    public static string Of(VisualizerOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{o.Title}|{o.Kind}|{o.Summary}|{o.FitOnOpen}|{o.GetSummaryText()}");
        var steps = o.Sequence?.Steps ?? [];
        if (steps.Count == 0) sb.Append(Data(o, null, InitialData(o)));
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            sb.AppendLine($"step {i}: {step.Description}|{string.Join(",", step.AuxiliaryInfo.OrderBy(kv => kv.Key, StringComparer.Ordinal))}|{step.SourceLine}|{step.SourceFile}");
            foreach (var watch in step.Watches) sb.AppendLine($"  watch {watch.Name}|{watch.Kind}|{watch.Count}|{string.Join(",", watch.Items)}");
            sb.Append(Data(o, step, step.Snapshot ?? InitialData(o)));
        }

        return sb.ToString();
    }

    private static object? InitialData(VisualizerOptions o) => o.Kind switch
    {
        VisualizerKind.Matrix or VisualizerKind.Islands => o.MatrixData,
        VisualizerKind.Board => o.BoardData,
        VisualizerKind.Tree => o.TreeData,
        VisualizerKind.Graph => o.GraphData,
        VisualizerKind.LinkedList => o.LinkedListData,
        VisualizerKind.ArrayPointers => o.ArrayData,
        VisualizerKind.Bars => o.BarData,
        _ => o.SceneData
    };

    private static string Data(VisualizerOptions o, VisualizerStep? step, object? data) => data switch
    {
        GridMatrixData grid => Grid(o, step, grid),
        BoardVisualizerData board => Board(o, board),
        TreeNodeData tree => Tree(step, tree),
        GraphData graph => Graph(step, graph),
        LinkedListData list => List(step, list),
        ArrayPointerData array => Array(step, array),
        BarChartVisualizerData bars => Bars(o, bars),
        VisualizerScene scene => Scene(scene),
        null => "  (nothing)\n",
        _ => $"  unknown {data.GetType().Name}\n"
    };

    private static string Grid(VisualizerOptions o, VisualizerStep? step, GridMatrixData grid)
    {
        var sb = new StringBuilder();
        var active = (step?.ActiveCells ?? []).ToHashSet();
        sb.AppendLine($"  grid {grid.Rows}x{grid.Columns} {N(grid.CellSize)} {grid.ShowCoordinates && o.ShowCoordinates} {grid.ShowValues && o.ShowValues} {grid.ColorMap} [{string.Join(",", grid.RowHeaders)}] [{string.Join(",", grid.ColHeaders)}]");
        foreach (var island in grid.Islands) sb.AppendLine($"  island {island.Id}|{island.Label}|{island.Color}|{string.Join(";", island.Cells.Order())}");
        for (var r = 0; r < grid.Rows; r++)
        {
            for (var c = 0; c < grid.Columns; c++)
            {
                var cell = grid[r, c];
                var arrows = string.Join(";", cell.Arrows.Select(a => $"{a.TargetRow},{a.TargetCol},{a.Label},{a.ColorHex}"));
                var halo = active.Contains((r, c)) || cell.IsActive;
                sb.AppendLine($"  {r},{c}: {cell.DisplayValue}|{cell.Kind}|{cell.BaseKind}|{cell.State}|{cell.ClusterId}|{cell.CustomColor}|{cell.BorderColor}|{cell.TextColor}|{N(cell.Heat)}|{cell.SubLabel}|{arrows}|{Notes(cell.Metadata)}|{halo}");
            }
        }

        return sb.ToString();
    }

    private static string Board(VisualizerOptions o, BoardVisualizerData board)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  board {board.Rows}x{board.Columns} {board.IsCheckerboard} {board.ShowCoordinates && o.ShowCoordinates} [{string.Join(",", board.RowHeaders)}] [{string.Join(",", board.ColHeaders)}]");
        for (var r = 0; r < board.Rows; r++)
        {
            for (var c = 0; c < board.Columns; c++)
            {
                var cell = board[r, c];
                var arrows = string.Join(";", cell.Arrows.Select(a => $"{a.TargetRow},{a.TargetCol},{a.Label},{a.ColorHex}"));
                sb.AppendLine($"  {r},{c}: {cell.Value}|{cell.SubLabel}|{cell.FillColor}|{cell.BorderColor}|{cell.TextColor}|{cell.IsTarget}|{cell.IsConflict}|{cell.IsActive}|{arrows}");
            }
        }

        return sb.ToString();
    }

    private static string Tree(VisualizerStep? step, TreeNodeData root)
    {
        var sb = new StringBuilder();
        var active = (step?.ActiveNodeIds ?? []).ToHashSet();
        var pending = new Stack<(TreeNodeData Node, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (node, depth) = pending.Pop();
            var halo = active.Contains(node.Id) || node.IsActive || node.State == TreeNodeState.Current;
            var style = node.State != TreeNodeState.Default ? node.State : halo ? TreeNodeState.Current : node.IsVisited ? TreeNodeState.Visited : TreeNodeState.Default;
            var shape = node.Left != null || node.Right != null ? $"L{(node.Left != null ? 1 : 0)}R{(node.Right != null ? 1 : 0)}" : $"C{node.Children.Count}";
            sb.AppendLine($"  {new string(' ', depth)}{node.Id}|{node.DisplayValue}|{style}|{halo}|{node.PointerLabel}|{node.SubLabel}|{node.CustomColor}|{Notes(node.Metadata)}|{shape}");
            for (var i = node.Children.Count - 1; i >= 0; i--) pending.Push((node.Children[i], depth + 1));
        }

        return sb.ToString();
    }

    private static string Graph(VisualizerStep? step, GraphData graph)
    {
        var sb = new StringBuilder();
        var active = (step?.ActiveNodeIds ?? []).ToHashSet();
        sb.AppendLine($"  graph directed={graph.IsDirected}");
        foreach (var n in graph.Nodes)
        {
            var halo = active.Contains(n.Id) || n.IsActive || n.State == GraphNodeState.Current;
            var style = n.State != GraphNodeState.Default ? n.State : halo ? GraphNodeState.Current : n.IsVisited ? GraphNodeState.Visited : GraphNodeState.Default;
            sb.AppendLine($"  node {n.Id}|{n.Label}|{style}|{halo}|{n.PointerLabel}|{n.SubLabel}|{n.Color}|{Notes(n.Metadata)}|{N(n.PinnedX)}|{N(n.PinnedY)}");
        }

        foreach (var e in graph.Edges) sb.AppendLine($"  edge {e.FromId}|{e.ToId}|{N(e.Weight)}|{e.Label}|{e.IsDirected}|{e.State}|{e.IsActive}|{e.Color}");
        return sb.ToString();
    }

    private static string List(VisualizerStep? step, LinkedListData list)
    {
        var sb = new StringBuilder();
        var active = (step?.ActiveNodeIds ?? []).ToHashSet();
        var source = list.CycleSourceIndex ?? list.Nodes.Count - 1;
        sb.AppendLine($"  list cycle={list.HasCycle} target={list.CycleTargetIndex} source={(list.HasCycle ? source : null)} stack={list.StackChains} chains=[{string.Join(",", list.ChainStarts)}]");
        for (var i = 0; i < list.Nodes.Count; i++)
        {
            var n = list.Nodes[i];
            sb.AppendLine($"  {i}: {n.DisplayValue}|{n.NextIndex}|{n.IsCycleTarget}|{active.Contains($"node_{i}") || n.IsActive}|{n.Color}|{n.ContinuesBeyondView}");
        }

        foreach (var p in list.Pointers) sb.AppendLine($"  pointer {p.Name}|{p.Index}|{p.Color}");
        return sb.ToString();
    }

    private static string Array(VisualizerStep? step, ArrayPointerData array)
    {
        var sb = new StringBuilder();
        var pointers = step?.CustomData as List<PointerMarkerData> ?? array.Pointers;
        var active = (step?.ActiveCells ?? []).Select(c => c.Col).Concat(pointers.Select(p => p.Index)).ToHashSet();
        for (var i = 0; i < array.Items.Count; i++)
        {
            var item = array.Items[i];
            sb.AppendLine($"  [{i}] {item.DisplayValue}|{item.Color}|{active.Contains(i) || item.IsActive}");
        }

        foreach (var p in pointers) sb.AppendLine($"  pointer {p.Name}|{p.Index}|{p.Color}");
        return sb.ToString();
    }

    private static string Bars(VisualizerOptions o, BarChartVisualizerData bars)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  bars {N(bars.MinValue)} {N(bars.MaxValue)} {bars.ShowValues && o.ShowValues} {bars.ShowIndices} {bars.Description} {bars.Shade}");
        foreach (var b in bars.Items) sb.AppendLine($"  [{b.Index}] {N(b.Value)}|{b.DisplayValue}|{b.Label}|{b.PointerLabel}|{b.ColorHex}|{b.IsActive}|{b.IsPivot}|{b.IsSorted}");
        return sb.ToString();
    }

    private static string Scene(VisualizerScene scene)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  scene {N(scene.Width)}x{N(scene.Height)} {scene.BackgroundColor}");
        foreach (var shape in scene.Shapes)
        {
            var common = $"{shape.Id}|{shape.Fill}|{shape.Stroke}|{N(shape.StrokeThickness)}|{N(shape.Opacity)}|{shape.Tooltip}";
            sb.AppendLine(shape switch
            {
                SceneRect r => $"  rect {common}|{N(r.X)},{N(r.Y)},{N(r.Width)},{N(r.Height)}|{N(r.CornerRadius)}|{r.Label}|{r.TextColor}|{N(r.FontSize)}",
                SceneCircle c => $"  circle {common}|{N(c.CenterX)},{N(c.CenterY)}|{N(c.Radius)}|{c.Label}|{c.TextColor}|{N(c.FontSize)}",
                SceneLine l => $"  line {common}|{N(l.X1)},{N(l.Y1)},{N(l.X2)},{N(l.Y2)}|{l.IsDashed}",
                SceneArrow a => $"  arrow {common}|{N(a.StartX)},{N(a.StartY)},{N(a.EndX)},{N(a.EndY)}|{N(a.ArrowHeadSize)}|{a.Label}",
                SceneText t => $"  text {common}|{N(t.X)},{N(t.Y)}|{t.Text}|{N(t.FontSize)}|{t.IsBold}|{t.IsCentered}",
                _ => $"  {shape.GetType().Name}"
            });
        }

        return sb.ToString();
    }
}
