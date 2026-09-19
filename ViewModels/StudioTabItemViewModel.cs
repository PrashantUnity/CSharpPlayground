using System.Collections.ObjectModel;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// Represents an open script tab in the VS Code editor tab strip.
/// Holds a complete independent state for this tab: its own TextDocument, undo stack,
/// caret position, console output buffer, diagnostics, and execution context.
/// </summary>
public partial class StudioTabItemViewModel : ObservableObject
{
    [ObservableProperty]
    private ScriptDocumentItem _document;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private bool _isDebugging;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _pausedLine = -1;

    [ObservableProperty]
    private string _consoleOutput = string.Empty;

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private string _compilerStatusText = "Ready";

    public TextDocument DocumentModel { get; }

    public int CaretLine { get; set; } = 1;
    public int CaretColumn { get; set; } = 1;
    public int SelectedBottomTabIndex { get; set; } = 1;

    public CancellationTokenSource? ExecutionCts { get; set; }

    public ObservableCollection<DiagnosticItemViewModel> Diagnostics { get; } = new();
    public ObservableCollection<DumpTableResult> DumpResults { get; } = new();
    public ObservableCollection<RichCellOutput> RichOutputs { get; } = new();
    public ObservableCollection<DebugVariableItem> Locals { get; } = new();
    public ObservableCollection<CallStackFrameItem> CallStack { get; } = new();

    public string Id => Document.Id;
    public string Title => Document.Title;

    public Action<StudioTabItemViewModel>? OnSelect { get; set; }
    public Action<StudioTabItemViewModel>? OnClose { get; set; }

    public StudioTabItemViewModel(ScriptDocumentItem document, bool isActive = false)
    {
        _document = document;
        _isActive = isActive;
        DocumentModel = new TextDocument(document.Code ?? string.Empty);
    }

    [RelayCommand]
    public void Select() => OnSelect?.Invoke(this);

    [RelayCommand]
    public void Close() => OnClose?.Invoke(this);

    public void NotifyTitleChanged()
    {
        OnPropertyChanged(nameof(Title));
    }
}
