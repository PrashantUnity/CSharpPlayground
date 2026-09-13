using System.Collections.Generic;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public interface IScriptStorageService
{
    Task<List<WorkspaceItemSummary>> LoadWorkspaceSummariesAsync();
    Task<ScriptDocumentItem?> LoadScriptAsync(string id);
    Task SaveScriptAsync(ScriptDocumentItem script);
    Task<NotebookDocumentItem?> LoadNotebookAsync(string id);
    Task SaveNotebookAsync(NotebookDocumentItem notebook);
    Task<ScriptDocumentItem> CreateNewScriptAsync(string title = "New Script", string? templateId = null);
    Task<NotebookDocumentItem> CreateNewNotebookAsync(string title = "New Notebook", string? templateId = null);
    Task DeleteItemAsync(string id);

    // Backward compatibility for existing references
    Task<List<ScriptProjectItem>> LoadScriptsAsync();
    Task SaveScriptsAsync(IEnumerable<ScriptProjectItem> scripts);
}
