using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class DiagnosticItemViewModel : ObservableObject
{
    public DiagnosticItem Model { get; }

    public string Id => Model.Id;
    public string Message => Model.Message;
    public int Line => Model.Line;
    public int Column => Model.Column;
    public string LocationString => Model.LocationString;
    public string SeverityIconKind => Model.SeverityIconKind;
    public string SeverityColorHex => Model.SeverityColorHex;
    public Microsoft.CodeAnalysis.DiagnosticSeverity Severity => Model.Severity;

    private readonly Action<int, int>? _onNavigate;

    public DiagnosticItemViewModel(DiagnosticItem model, Action<int, int>? onNavigate = null)
    {
        Model = model;
        _onNavigate = onNavigate;
    }

    /// <summary>What the fix button says, e.g. "Install numpy", or null when there's no fix.</summary>
    public string? QuickFixLabel { get; init; }

    public IAsyncRelayCommand? QuickFixCommand { get; init; }

    public bool HasQuickFix => QuickFixCommand != null;

    [RelayCommand]
    private void Navigate()
    {
        _onNavigate?.Invoke(Line, Column);
    }
}
