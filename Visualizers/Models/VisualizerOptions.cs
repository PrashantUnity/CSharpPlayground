namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class VisualizerOptions
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public VisualizerKind Kind { get; set; } = VisualizerKind.Matrix;
    public string Theme { get; set; } = "Dark";
    public double CellSize { get; set; } = 38.0;
    public bool ShowCoordinates { get; set; } = true;
    public bool ShowValues { get; set; } = true;
    public double Zoom { get; set; } = 1.0;
    public double PanOffsetX { get; set; } = 0.0;
    public double PanOffsetY { get; set; } = 0.0;

    public GridMatrixData? MatrixData { get; set; }
    public TreeNodeData? TreeData { get; set; }
    public GraphData? GraphData { get; set; }
    public LinkedListData? LinkedListData { get; set; }
    public ArrayPointerData? ArrayData { get; set; }
    public BarChartVisualizerData? BarData { get; set; }
    public BoardVisualizerData? BoardData { get; set; }
    public VisualizerScene? SceneData { get; set; }

    public VisualizerSequence? Sequence { get; set; }

    /// <summary>
    /// Shrink the drawing to fit when it first appears, if it would be cut off at 100% and fits at 50% or more.
    /// Bigger drawings stay at 100% (trees then follow the current node); the Fit button still fits anything.
    /// </summary>
    public bool FitOnOpen { get; set; } = true;

    /// <summary>Text for the header's stats pill when the data type alone can't describe it (e.g. "Intervals: 4").</summary>
    public string? Summary { get; set; }

    /// <summary>A second view of the same visualization: it shares the data and the playback, but zooms and pans on its own.</summary>
    public VisualizerOptions CloneView() => (VisualizerOptions)MemberwiseClone();

    public string GetSummaryText()
    {
        if (!string.IsNullOrEmpty(Summary))
        {
            return Summary;
        }
        if (MatrixData != null)
        {
            if (MatrixData.Islands.Count > 0)
                return $"Grid: {MatrixData.Rows}×{MatrixData.Columns} • Islands: {MatrixData.Islands.Count}";
            return $"Grid: {MatrixData.Rows}×{MatrixData.Columns}";
        }
        if (TreeData != null)
        {
            return $"Nodes: {CountTreeNodes(TreeData)}";
        }
        if (GraphData != null)
        {
            return $"Nodes: {GraphData.Nodes.Count} • Edges: {GraphData.Edges.Count}";
        }
        if (LinkedListData != null)
        {
            return $"Nodes: {LinkedListData.Nodes.Count}{(LinkedListData.HasCycle ? " • Cycle Detected" : "")}";
        }
        if (ArrayData != null)
        {
            // Pointers usually arrive per step, so a count of the up-front ones would read "Pointers: 0" mid-walk.
            return ArrayData.Pointers.Count > 0
                ? $"Items: {ArrayData.Items.Count} • Pointers: {ArrayData.Pointers.Count}"
                : $"Items: {ArrayData.Items.Count}";
        }
        if (BarData != null)
        {
            return $"Bars: {BarData.Items.Count} • Range: [{BarData.MinValue:0.#}, {BarData.MaxValue:0.#}]";
        }
        if (BoardData != null)
        {
            return $"Board: {BoardData.Rows}×{BoardData.Columns}";
        }
        if (SceneData != null)
        {
            return $"Shapes: {SceneData.Shapes.Count}";
        }
        return "Ready";
    }

    private static int CountTreeNodes(TreeNodeData node)
    {
        int count = 1;
        foreach (var c in node.Children) count += CountTreeNodes(c);
        return count;
    }
}
