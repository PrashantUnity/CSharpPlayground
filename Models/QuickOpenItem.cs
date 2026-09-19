using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum QuickOpenItemKind
{
    Document,
    Command,
    LineJump,
    Template,
    Recent
}

public partial class QuickOpenItem : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _iconKind = "FileCodeOutline";

    [ObservableProperty]
    private string _iconColorHex = "#58A6FF";

    [ObservableProperty]
    private string _shortcutHint = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public QuickOpenItemKind Kind { get; set; } = QuickOpenItemKind.Document;

    public Action? ExecuteAction { get; set; }

    public Func<Task>? ExecuteAsyncAction { get; set; }

    public object? Tag { get; set; }

    public int Score { get; set; }
}
