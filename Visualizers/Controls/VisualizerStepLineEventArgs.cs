using Avalonia.Interactivity;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;

public sealed class VisualizerStepLineEventArgs : RoutedEventArgs
{
    public VisualizerStepLineEventArgs(int line, string? sourceFile, bool reveal)
        : base(InteractiveVisualizerControl.StepSourceLineChangedEvent)
    {
        Line = line;
        SourceFile = sourceFile;
        Reveal = reveal;
    }

    /// <summary>1-based source line of the current step; 0 clears the highlight.</summary>
    public int Line { get; }

    public string? SourceFile { get; }

    /// <summary>True when the learner explicitly asked to jump to the line.</summary>
    public bool Reveal { get; }
}
