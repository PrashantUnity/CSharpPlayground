using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// Represents an open script tab in the VS Code editor tab strip.
/// </summary>
public partial class StudioTabItemViewModel : ObservableObject
{
    [ObservableProperty]
    private ScriptDocumentItem _document;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDirty;

    public string Id => Document.Id;
    public string Title => Document.Title;

    public Action<StudioTabItemViewModel>? OnSelect { get; set; }
    public Action<StudioTabItemViewModel>? OnClose { get; set; }

    public StudioTabItemViewModel(ScriptDocumentItem document, bool isActive = false)
    {
        _document = document;
        _isActive = isActive;
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
