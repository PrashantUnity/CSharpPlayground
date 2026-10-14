namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>Roughly how much a spec has to draw, counted without building it: views build big ones off the UI thread.</summary>
public static class VisualSize
{
    /// <summary>Above this many elements a view prepares the drawing in the background.</summary>
    public const int BackgroundThreshold = 20_000;

    public static long Of(VisualSpec spec) => spec switch
    {
        ChartSpec chart => chart.Series.Sum(s => (long)s.Y.Count + (s.Values?.Count ?? 0)),
        Plot3DSpec plot => plot.Series.Sum(s => (long)s.X.Count)
                           + (plot.Surface?.Z.Sum(row => (long)row.Count) ?? 0)
                           + (plot.Graph is { } graph ? graph.Nodes.Count + graph.Edges.Count : 0),
        VisualizerSpec visualizer => Of(visualizer.State) + visualizer.Steps.Count,
        _ => 0
    };

    private static long Of(VisualizerState state) =>
        (state.Grid?.Values.Sum(row => (long)row.Count) ?? 0)
        + (state.Tree?.Nodes.Count ?? 0)
        + (state.Graph is { } graph ? graph.Nodes.Count + graph.Edges.Count : 0)
        + (state.LinkedList?.Nodes.Count ?? 0)
        + (state.Array?.Values.Count ?? 0)
        + (state.Bars?.Values.Count ?? 0)
        + (state.Canvas?.Shapes.Count ?? 0);
}
