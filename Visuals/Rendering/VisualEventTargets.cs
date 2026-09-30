using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>
/// What a click hit, named as the spec names it (a chart's value by series and index, a node or item by the spec's id or
/// index, a cell by row and column), so a program can find it in the data it sent.
/// </summary>
internal static class VisualEventTargets
{
    public static VisualEventTarget Of(ChartHitTestResult hit, ChartOptions drawn, ChartSpec? spec)
    {
        var series = drawn.Series.IndexOf(hit.Series);
        var index = series < 0 ? -1 : hit.Series.Points.IndexOf(hit.Point);
        var id = spec != null && series >= 0 && series < spec.Series.Count && index >= 0 && spec.Series[series].Ids is { } ids && index < ids.Count ? ids[index] : null;
        return new VisualEventTarget
        {
            Series = series >= 0 ? series : null,
            Index = index >= 0 ? index : null,
            Id = id,
            X = hit.Point.X,
            Y = hit.Point.Y,
            Label = NullIfEmpty(hit.Point.Label)
        };
    }

    public static VisualEventTarget? Of(Plot3DHitTestResult hit, Plot3DOptions drawn)
    {
        if (hit.Node is { } node) return new VisualEventTarget { Node = node.Id, X = node.X, Y = node.Y, Z = node.Z, Label = NullIfEmpty(node.Label) };
        if (hit.Point is not { } point) return null;
        var series = hit.Series == null ? -1 : drawn.Series.IndexOf(hit.Series);
        var index = series < 0 ? -1 : hit.Series!.Points.IndexOf(point);
        return new VisualEventTarget
        {
            Series = series >= 0 ? series : null,
            Index = index >= 0 ? index : null,
            X = point.X,
            Y = point.Y,
            Z = point.Z,
            Label = NullIfEmpty(point.Label)
        };
    }

    public static VisualEventTarget? Of(VisualizerHitTestResult hit, VisualizerOptions drawn) => drawn.Kind switch
    {
        VisualizerKind.Matrix or VisualizerKind.Islands or VisualizerKind.Board when hit is { Row: { } row, Col: { } col } => new VisualEventTarget { Cell = (row, col) },
        VisualizerKind.ArrayPointers or VisualizerKind.Bars when hit.Col is { } item => new VisualEventTarget { Item = item },
        VisualizerKind.LinkedList when ListIndex(hit.NodeId) is { } i => new VisualEventTarget { Item = i, Node = ListNodeId(drawn, i) },
        VisualizerKind.Tree or VisualizerKind.Graph when hit.NodeId is { } node => new VisualEventTarget { Node = node },
        _ => null
    };

    // The list renderer names a node by where it is drawn: "node_3".
    private static int? ListIndex(string? nodeId) =>
        nodeId != null && nodeId.StartsWith("node_", StringComparison.Ordinal) && int.TryParse(nodeId.AsSpan(5), out var index) ? index : null;

    // The spec's id of the node drawn there, in the step on screen.
    private static string? ListNodeId(VisualizerOptions drawn, int index)
    {
        var list = drawn.Sequence?.CurrentStep?.Snapshot as LinkedListData ?? drawn.LinkedListData;
        return list != null && index >= 0 && index < list.Nodes.Count ? list.Nodes[index].Id ?? $"n{list.Nodes[index].Index}" : null;
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
