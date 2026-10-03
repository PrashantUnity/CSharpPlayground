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

    public int LineCount => string.IsNullOrEmpty(_vm.Code) ? 1 : _vm.Code.Split('\n').Length;

    public string GetLineText(int lineNumber)
    {
        if (lineNumber < 1 || string.IsNullOrEmpty(_vm.Code)) return string.Empty;
        var lines = _vm.Code.Split('\n');
        return lineNumber <= lines.Length ? lines[lineNumber - 1].TrimEnd('\r') : string.Empty;
    }

    public void SetCaret(int line, int column)
    {
        _vm.SetCaretPosition(line, column);
    }

    public void SetSelection(int startLine, int startColumn, int endLine, int endColumn)
    {
        _vm.SetEditorSelection?.Invoke(startLine, startColumn, endLine, endColumn);
    }

    public void InsertText(string text)
    {
        if (_vm.InsertEditorText != null)
        {
            _vm.InsertEditorText(text);
        }
        else
        {
            SelectedText = text;
        }
    }

    public void ScrollToLine(int lineNumber)
    {
        _vm.ScrollEditorToLine?.Invoke(lineNumber);
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
