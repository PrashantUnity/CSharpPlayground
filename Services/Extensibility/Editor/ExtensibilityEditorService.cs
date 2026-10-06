using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;

/// <summary>
/// Bridge service between the public IEditorApi contract and internal editor ViewModels.
/// </summary>
public class ExtensibilityEditorService : IEditorApi
{
    public Func<IDocumentContext?>? ActiveDocumentResolver { get; set; }
    public Func<IReadOnlyList<IDocumentContext>>? OpenDocumentsResolver { get; set; }
    public Func<string, Task>? OpenFileHandler { get; set; }
    public Func<string, string?, Task>? CreateDocumentHandler { get; set; }
    public Func<IDocumentContext, Task>? CloseDocumentHandler { get; set; }
    public Func<Task>? CloseActiveDocumentHandler { get; set; }
    public Action<IDocumentContext>? SwitchToDocumentHandler { get; set; }
    public Action? FormatDocumentHandler { get; set; }
    public Action? SaveDocumentHandler { get; set; }

    public event Action<IDocumentContext?>? ActiveDocumentChanged;

    public void NotifyActiveDocumentChanged(IDocumentContext? doc)
    {
        UiDispatchHelper.RunOnUi(() => ActiveDocumentChanged?.Invoke(doc));
    }

    public IDocumentContext? ActiveDocument => ActiveDocumentResolver?.Invoke();

    public IReadOnlyList<IDocumentContext> OpenDocuments =>
        OpenDocumentsResolver?.Invoke() ?? (ActiveDocument != null ? [ActiveDocument] : Array.Empty<IDocumentContext>());

    public async Task OpenFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (OpenFileHandler != null)
        {
            await OpenFileHandler(filePath);
        }
    }

    public async Task CreateDocumentAsync(string languageId = "csharp", string? initialCode = null)
    {
        if (CreateDocumentHandler != null)
        {
            await CreateDocumentHandler(languageId, initialCode);
        }
    }

    public async Task CloseDocumentAsync(IDocumentContext document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (CloseDocumentHandler != null)
        {
            await CloseDocumentHandler(document);
        }
    }

    public async Task CloseActiveDocumentAsync()
    {
        if (CloseActiveDocumentHandler != null)
        {
            await CloseActiveDocumentHandler();
        }
    }

    public void SwitchToDocument(IDocumentContext document)
    {
        ArgumentNullException.ThrowIfNull(document);
        UiDispatchHelper.RunOnUi(() => SwitchToDocumentHandler?.Invoke(document));
    }

    public void FormatActiveDocument()
    {
        UiDispatchHelper.RunOnUi(() => FormatDocumentHandler?.Invoke());
    }

    public void SaveActiveDocument()
    {
        UiDispatchHelper.RunOnUi(() => SaveDocumentHandler?.Invoke());
    }
}
