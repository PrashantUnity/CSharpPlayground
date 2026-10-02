using System;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;

/// <summary>
/// Bridges IDocumentContext with the active CSharpNotebookStudioViewModel and its selected cell.
/// </summary>
public class NotebookDocumentContextAdapter : IDocumentContext
{
    private readonly CSharpNotebookStudioViewModel _vm;

    public NotebookDocumentContextAdapter(CSharpNotebookStudioViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string? FilePath => _vm.ActiveTab?.FilePath;

    public string Title => _vm.ActiveTab != null
        ? $"{_vm.ActiveTab.Title} ({_vm.ActiveTab.ActiveCellBadgeText})"
        : "Untitled Notebook";

    public string Text
    {
        get => _vm.ActiveTab?.ActiveCell?.Source ?? string.Empty;
        set
        {
            if (_vm.ActiveTab?.ActiveCell is { } cell)
            {
                cell.Source = value ?? string.Empty;
            }
        }
    }

    public string LanguageId => _vm.ActiveTab?.ActiveCell?.EffectiveLanguageDefinition?.Id ?? "csharp";

    public bool IsDirty => _vm.ActiveTab?.IsModified ?? false;

    public int CaretLine => 1;

    public int CaretColumn => 1;

    public string SelectedText
    {
        get => string.Empty;
        set { }
    }

    public void Format()
    {
        _vm.ActiveTab?.ActiveCell?.FormatCode();
    }

    public void Save()
    {
        _ = _vm.SaveAsync();
    }
}
