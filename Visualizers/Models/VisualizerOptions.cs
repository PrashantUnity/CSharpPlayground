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

    /// <summary>A second view of the same visualization: it shares the data and the playback, but zooms and pans on its own.</summary>
    public VisualizerOptions CloneView() => (VisualizerOptions)MemberwiseClone();

    public string GetSummaryText()
    {
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
            return $"Items: {ArrayData.Items.Count} • Pointers: {ArrayData.Pointers.Count}";
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
