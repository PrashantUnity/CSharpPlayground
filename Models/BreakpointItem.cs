using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class BreakpointItem : ObservableObject
{
    [ObservableProperty]
    private int _lineNumber;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private string? _condition;

    [ObservableProperty]
    private int _hitCount;

    [ObservableProperty]
    private bool _isCurrentPausedLine;

    public string DisplayText => string.IsNullOrWhiteSpace(Condition)
        ? $"Line {LineNumber}"
        : $"Line {LineNumber} (when: {Condition})";
}
