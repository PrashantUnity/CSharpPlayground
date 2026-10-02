using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;

/// <summary>
/// Bridge service between the public IEditorApi contract and internal editor ViewModels.
/// </summary>
public class ExtensibilityEditorService : IEditorApi
{
    public Func<IDocumentContext?>? ActiveDocumentResolver { get; set; }
    public Func<string, Task>? OpenFileHandler { get; set; }
    public Func<string, string?, Task>? CreateDocumentHandler { get; set; }
    public Action? FormatDocumentHandler { get; set; }
    public Action? SaveDocumentHandler { get; set; }

    public IDocumentContext? ActiveDocument => ActiveDocumentResolver?.Invoke();

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

    public void FormatActiveDocument()
    {
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => FormatDocumentHandler?.Invoke());
        }
        else
        {
            FormatDocumentHandler?.Invoke();
        }
    }

    public void SaveActiveDocument()
    {
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SaveDocumentHandler?.Invoke());
        }
        else
        {
            SaveDocumentHandler?.Invoke();
        }
    }
}
