using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class WatchExpressionItem : ObservableObject
{
    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private string _typeName = string.Empty;

    [ObservableProperty]
    private bool _hasError;
}
