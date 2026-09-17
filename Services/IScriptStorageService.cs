using System.Collections.Generic;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public interface IScriptStorageService
{
    // The real OS directory every document/folder lives under — exposed so a native folder-picker
    // dialog can be scoped to browse within it (documents outside this root are never discovered by
    // LoadWorkspaceSummariesAsync, so picking a location outside it would silently orphan a document).
    string LibraryRootPath { get; }

    Task<List<WorkspaceItemSummary>> LoadWorkspaceSummariesAsync();
    Task<ScriptDocumentItem?> LoadScriptAsync(string id);

    // folderPath is only consulted the first time a given script.Id is ever saved (an existing
    // document keeps saving to wherever it already lives, exactly like before) — it exists so a copy
    // of an externally-saved script (see Duplicate) can be placed in that same external folder
    // instead of silently landing back in the library root the first time it's written.
    Task<bool> SaveScriptAsync(ScriptDocumentItem script, string? folderPath = null);
    Task<NotebookDocumentItem?> LoadNotebookAsync(string id);
    Task<bool> SaveNotebookAsync(NotebookDocumentItem notebook, string? folderPath = null);
    Task<ScriptDocumentItem> CreateNewScriptAsync(string title = "New Script", string? templateId = null, string? folderPath = null);
    Task<NotebookDocumentItem> CreateNewNotebookAsync(string title = "New Notebook", string? templateId = null, string? folderPath = null);
    Task DeleteItemAsync(string id);

    // Folder management — folders are real OS directories under the library root.
    // A folder path is a "/"-separated path relative to that root; empty/null means root.
    Task<List<string>> LoadFolderPathsAsync();
    Task<string> CreateFolderAsync(string? parentFolderPath, string desiredName);
    Task<string> RenameFolderAsync(string folderPath, string newName);
    Task DeleteFolderAsync(string folderPath);

    // Backward compatibility for existing references
    Task<List<ScriptProjectItem>> LoadScriptsAsync();
    Task SaveScriptsAsync(IEnumerable<ScriptProjectItem> scripts);
}
