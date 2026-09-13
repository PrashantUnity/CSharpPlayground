using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class CallStackFrameItem : ObservableObject
{
    [ObservableProperty]
    private int _frameIndex;

    [ObservableProperty]
    private string _methodName = "<Top-Level Statements>";

    [ObservableProperty]
    private int _lineNumber;

    [ObservableProperty]
    private int _columnNumber = 1;

    [ObservableProperty]
    private string _fileName = "script.cs";

    [ObservableProperty]
    private bool _isCurrentFrame = true;

    public string LocationDisplay => $"{FileName} : Line {LineNumber}";
}
