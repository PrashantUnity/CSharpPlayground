using System;
using System.Linq;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;

/// <summary>
/// Bridges IDocumentContext with the active CSharpCodeStudioViewModel editor canvas and tab state.
/// </summary>
public class StudioDocumentContextAdapter : IDocumentContext
{
    private readonly CSharpCodeStudioViewModel _vm;

    public StudioDocumentContextAdapter(CSharpCodeStudioViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string? FilePath => _vm.Script?.SourceFilePath;

    public string Title => _vm.Script?.Title ?? "Untitled";

    public string Text
    {
        get => _vm.Code;
        set => _vm.Code = value ?? string.Empty;
    }

    public string LanguageId => _vm.ActiveLanguage?.Id ?? "csharp";

    public bool IsDirty => _vm.OpenTabs.FirstOrDefault(t => t.Id == _vm.Script?.Id)?.IsDirty ?? false;

    public int CaretLine => _vm.CaretLine;

    public int CaretColumn => _vm.CaretColumn;

    public string SelectedText
    {
        get => _vm.GetSelectedText?.Invoke() ?? string.Empty;
        set => _vm.SetSelectedText?.Invoke(value ?? string.Empty);
    }

    public void Format()
    {
        _vm.FormatCode();
    }

    public void Save()
    {
        _ = _vm.SaveCommand.ExecuteAsync(null);
    }
}
