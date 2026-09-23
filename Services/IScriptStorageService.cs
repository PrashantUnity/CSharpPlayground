using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public interface IScriptStorageService
{
    string LibraryRootPath { get; }

    /// <summary>The single active workspace root currently shown in the Explorer: an opened folder, or <see cref="LibraryRootPath"/> when none is open.</summary>
    string ActiveWorkspaceRootPath { get; }
    bool IsExternalWorkspaceActive { get; }
    event Action? ActiveWorkspaceChanged;

    Task<List<WorkspaceItemSummary>> LoadWorkspaceSummariesAsync();
    Task<ScriptDocumentItem?> LoadScriptAsync(string id);
    Task<bool> SaveScriptAsync(ScriptDocumentItem script, string? folderPath = null);
    Task<NotebookDocumentItem?> LoadNotebookAsync(string id);
    Task<bool> SaveNotebookAsync(NotebookDocumentItem notebook, string? folderPath = null);
    Task<ScriptDocumentItem> CreateNewScriptAsync(string title = "New Script", string? templateId = null, string? folderPath = null);
    Task<NotebookDocumentItem> CreateNewNotebookAsync(string title = "New Notebook", string? templateId = null, string? folderPath = null);
    Task DeleteItemAsync(string id);

    Task<List<string>> LoadFolderPathsAsync();
    Task<string> CreateFolderAsync(string? parentFolderPath, string desiredName);
    Task<string> RenameFolderAsync(string folderPath, string newName);
    Task DeleteFolderAsync(string folderPath);

    Task<List<ScriptProjectItem>> LoadScriptsAsync();
    Task SaveScriptsAsync(IEnumerable<ScriptProjectItem> scripts);

    Task<OpenProjectResult> OpenExternalProjectAsync(string path);
}

public record OpenProjectResult(
    bool Success,
    string Message,
    string? PrimaryDocumentId = null,
    WorkspaceItemKind? PrimaryDocumentKind = null,
    int DocumentsLoadedCount = 0);

