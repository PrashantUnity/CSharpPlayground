using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

/// <summary>
/// What the Explorer is given to draw. For a workspace that fits the listing limit it is everything (<see cref="IsPartial"/> is
/// false); for a bigger one it is a single folder's direct contents, and each subfolder is listed when it is opened.
/// </summary>
/// <param name="FolderPaths">Folders as paths from the workspace root, always with <c>/</c> separators. All of them, or only this folder's subfolders when partial.</param>
/// <param name="Items">The documents and source files: all of them, or only the ones directly in this folder when partial.</param>
/// <param name="IsPartial">True when this is one folder's contents and the rest of the workspace is listed on demand.</param>
/// <param name="IsTruncated">True when one folder holds more files than the listing limit and only the first ones are here.</param>
public sealed record WorkspaceListing(
    IReadOnlyList<string> FolderPaths,
    IReadOnlyList<WorkspaceItemSummary> Items,
    bool IsPartial,
    bool IsTruncated = false,
    IReadOnlyList<string>? ClosedFolderPaths = null)
{
    /// <summary>
    /// Folders shown but not listed yet (node_modules, bin, .venv, a linked folder): their contents are listed when they
    /// are opened, as VS Code does. Paths from the workspace root.
    /// </summary>
    public IReadOnlyList<string> ClosedFolders => ClosedFolderPaths ?? [];
}
