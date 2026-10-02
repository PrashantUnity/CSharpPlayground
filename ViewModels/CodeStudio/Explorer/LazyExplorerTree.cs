using System.Collections.ObjectModel;
using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer;

/// <summary>
/// The Explorer of a workspace too big to list at once (more files than the listing limit): it draws one folder at a time. The
/// top folder is listed when the tree is built and each subfolder when it is first opened, so nothing is cut off and opening a
/// project of any size costs a single folder. A folder that has not been listed holds one "Loading..." row, which makes it
/// expandable; the open document's folders are opened down to it (<see cref="RevealAsync"/>), and a rebuilt tree lists the
/// folders that were open again. Both studios use it, each supplying its own way to make a row.
/// </summary>
public sealed class LazyExplorerTree
{
    private readonly IScriptStorageService _storage;
    private readonly ObservableCollection<ExplorerItemViewModel> _roots;
    private readonly ExplorerRowList _rows;
    private readonly Func<string, string, ExplorerItemViewModel?, ExplorerItemViewModel> _createFolder;
    private readonly Func<WorkspaceItemSummary, string, string, ExplorerItemViewModel?, ExplorerItemViewModel> _createFile;

    // Folders being listed right now (so opening one that is already on its way waits for that listing).
    private readonly Dictionary<ExplorerItemViewModel, Task> _loading = new();
    private HashSet<string> _openAfterListing = new(StringComparer.OrdinalIgnoreCase);
    private int _generation;

    /// <param name="createFolder">Makes a folder row: its name, its path from the workspace root, and its parent (null at the top).</param>
    /// <param name="createFile">Makes a document row: what the workspace knows about it, its display name, its path from the root, and its parent.</param>
    public LazyExplorerTree(
        IScriptStorageService storage,
        ObservableCollection<ExplorerItemViewModel> roots,
        ExplorerRowList rows,
        Func<string, string, ExplorerItemViewModel?, ExplorerItemViewModel> createFolder,
        Func<WorkspaceItemSummary, string, string, ExplorerItemViewModel?, ExplorerItemViewModel> createFile)
    {
        _storage = storage;
        _roots = roots;
        _rows = rows;
        _createFolder = createFolder;
        _createFile = createFile;
    }

    /// <summary>True while the Explorer shows a big workspace one folder at a time.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Goes back to the ordinary tree (the workspace fits the listing limit, or another folder was opened).</summary>
    public void Deactivate()
    {
        IsActive = false;
        _generation++;
        _loading.Clear();
        _openAfterListing.Clear();
    }

    /// <summary>
    /// Replaces the tree with the workspace's top folder. The folders in <paramref name="openFolders"/> (paths from the root) are
    /// opened again, each listed as it is reached.
    /// </summary>
    public void Build(WorkspaceListing top, IEnumerable<string> openFolders)
    {
        IsActive = true;
        _generation++;
        _loading.Clear();
        _openAfterListing = new HashSet<string>(openFolders, StringComparer.OrdinalIgnoreCase);

        using var scope = _rows.Suspend();
        _roots.Clear();
        AddListing(null, top);
    }

    /// <summary>Lists <paramref name="folder"/>'s contents (once); completes when they are in the tree.</summary>
    public Task LoadChildrenAsync(ExplorerItemViewModel folder)
    {
        if (folder.ChildrenLoaded) return Task.CompletedTask;
        if (_loading.TryGetValue(folder, out var running)) return running;

        var done = new TaskCompletionSource();
        _loading[folder] = done.Task;
        _ = ListAsync();
        return done.Task;

        async Task ListAsync()
        {
            var generation = _generation;
            try
            {
                WorkspaceListing listing;
                try
                {
                    listing = await _storage.ListFolderAsync(folder.FullPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CSharpEditorPlugin] Listing '{folder.FullPath}' failed: {ex.Message}");
                    listing = new WorkspaceListing([], [], IsPartial: true);
                }

                // The tree was rebuilt (or dropped) while this folder was being listed: its rows are gone.
                if (generation != _generation) return;

                _rows.ChangeChildren(folder, () =>
                {
                    folder.Children.Clear();
                    AddListing(folder, listing);
                    folder.ChildrenLoaded = true;
                });
            }
            finally
            {
                if (generation == _generation) _loading.Remove(folder);
                done.SetResult();
            }
        }
    }

    /// <summary>
    /// Opens the folders down to a document (listing each) and returns its row, or null when the document is not in this workspace
    /// or is not there any more.
    /// </summary>
    public async Task<ExplorerItemViewModel?> RevealAsync(string documentId)
    {
        var relative = _storage.GetWorkspaceRelativePath(documentId);
        if (relative == null) return null;

        var segments = relative.Split('/');

        // A rebuild of the tree while a folder is being listed drops the rows being opened: start again on the new tree.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var generation = _generation;
            IEnumerable<ExplorerItemViewModel> level = _roots;
            var rebuilt = false;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                var folder = level.FirstOrDefault(c => c.IsDirectory && string.Equals(c.Name, segments[i], StringComparison.OrdinalIgnoreCase));
                if (folder == null) return null;

                await LoadChildrenAsync(folder);
                if (generation != _generation)
                {
                    rebuilt = true;
                    break;
                }

                folder.IsExpanded = true;
                level = folder.Children;
            }

            if (rebuilt) continue;
            return level.FirstOrDefault(c => !c.IsDirectory && string.Equals(c.DocumentId, documentId, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    // Adds one folder's contents under `parent` (the top when null): subfolders first, then documents, each sorted by name.
    private void AddListing(ExplorerItemViewModel? parent, WorkspaceListing listing)
    {
        var target = parent?.Children ?? _roots;
        var folders = new List<ExplorerItemViewModel>();
        foreach (var path in listing.FolderPaths)
        {
            var folder = _createFolder(path[(path.LastIndexOf('/') + 1)..], path, parent);
            folder.ChildrenLoaded = false;
            folder.LoadChildrenRequested = f => _ = LoadChildrenAsync(f);
            folder.Children.Add(Placeholder("Loading...", folder));
            folders.Add(folder);
        }

        var files = new List<ExplorerItemViewModel>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var summary in listing.Items)
        {
            var extension = summary.DisplayExtension;
            var name = summary.Title.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? summary.Title : $"{summary.Title}{extension}";
            if (!names.Add(name)) continue;

            files.Add(_createFile(summary, name, parent == null ? name : $"{parent.FullPath}/{name}", parent));
        }

        foreach (var folder in folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)) target.Add(folder);
        foreach (var file in files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)) target.Add(file);
        if (listing.IsTruncated) target.Add(Placeholder("More files than are listed here. Ctrl+P opens any file by name.", parent));

        // Folders that were open before the tree was rebuilt open again, which lists them.
        foreach (var folder in folders)
        {
            if (_openAfterListing.Remove(folder.FullPath)) folder.IsExpanded = true;
        }
    }

    private static ExplorerItemViewModel Placeholder(string text, ExplorerItemViewModel? parent) => new()
    {
        Name = text,
        IsPlaceholder = true,
        Parent = parent,
        Depth = (parent?.Depth ?? -1) + 1,
        LanguageIconKind = "DotsHorizontal",
        LanguageIconColor = "#8B949E"
    };
}
