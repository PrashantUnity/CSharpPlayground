namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class VisualizerStep
{
    public int StepIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public VisualizerKind Kind { get; set; }
    public List<(int Row, int Col)> ActiveCells { get; set; } = new();
    public List<string> ActiveNodeIds { get; set; } = new();
    public Dictionary<string, string> AuxiliaryInfo { get; set; } = new();
    public List<StepWatch> Watches { get; set; } = new();
    public object? Snapshot { get; set; }
    public object? CustomData { get; set; }

    /// <summary>1-based line of the call that recorded this step; 0 when the step was generated automatically.</summary>
    public int SourceLine { get; set; }

    /// <summary>Compile-time path of that call's source (a notebook cell id), used to find the right editor.</summary>
    public string? SourceFile { get; set; }

    public VisualizerStep() { }

    public VisualizerStep(int stepIndex, string description, VisualizerKind kind = VisualizerKind.Matrix)
    {
        StepIndex = stepIndex;
        Description = description;
        Kind = kind;
    }
}
