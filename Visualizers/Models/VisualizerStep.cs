namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class VisualizerStep
{
    public int StepIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public VisualizerKind Kind { get; set; }
    public List<(int Row, int Col)> ActiveCells { get; set; } = new();
    public List<string> ActiveNodeIds { get; set; } = new();
    public Dictionary<string, string> AuxiliaryInfo { get; set; } = new();
    public object? Snapshot { get; set; }
    public object? CustomData { get; set; }

    public VisualizerStep() { }

    public VisualizerStep(int stepIndex, string description, VisualizerKind kind = VisualizerKind.Matrix)
    {
        StepIndex = stepIndex;
        Description = description;
        Kind = kind;
    }
}
