using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public interface IScriptStorageService
{
    string LibraryRootPath { get; }

    /// <summary>The single active workspace root currently shown in the Explorer: an opened folder, or <see cref="LibraryRootPath"/> when none is open.</summary>
    string ActiveWorkspaceRootPath { get; }
    bool IsExternalWorkspaceActive { get; }
    event Action? ActiveWorkspaceChanged;

    /// <summary>
    /// Moves whenever the set or arrangement of items changes (one created, deleted, renamed or moved, or another folder
    /// opened). A consumer that remembers the value it loaded at can skip reloading the workspace while it hasn't moved.
    /// </summary>
    long StructureVersion { get; }

    /// <summary>Moves on every change <see cref="StructureVersion"/> reports, and on every save (titles and modified times change).</summary>
    long ContentVersion { get; }

    /// <summary>
    /// Raised (on a background thread) once files changed outside the studio (git, another editor) have settled; the
    /// versions above have already moved. Only raised where the storage was told to watch for changes.
    /// </summary>
    event Action? ExternalChangeDetected;

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

    /// <summary>The languages whose plain source files (main.py) the workspace lists alongside its documents.</summary>
    LanguageRegistry Languages { get; }

    /// <summary>Creates a source file of a language (script_HHmmss.py, or <paramref name="fileName"/>) with its starter text.</summary>
    Task<ScriptDocumentItem?> CreateNewSourceFileAsync(string languageId, string? fileName = null, string? folderPath = null);

    /// <summary>Renames a source file on disk; its id changes with its path.</summary>
    Task<SourceFileRename> RenameSourceFileAsync(string id, string newFileName);

    /// <summary>
    /// Writes a source file's text. Without <paramref name="overwriteChangesOnDisk"/> it writes only when the text changed
    /// and nothing else changed the file since it was read (<see cref="SaveScriptAsync"/> saves source files this way).
    /// </summary>
    Task<bool> SaveSourceFileAsync(ScriptDocumentItem document, bool overwriteChangesOnDisk);
}

public record OpenProjectResult(
    bool Success,
    string Message,
    string? PrimaryDocumentId = null,
    WorkspaceItemKind? PrimaryDocumentKind = null,
    int DocumentsLoadedCount = 0);

/// <summary>A renamed source file's new id and path.</summary>
public sealed record SourceFileRename(string Id, string FilePath);
