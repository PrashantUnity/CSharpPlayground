using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpCodeStudioViewModel
{
    public ObservableCollection<ExplorerItemViewModel> ExplorerRootItems { get; } = new();

    private ExplorerRowList? _explorerRows;

    /// <summary>The Explorer's visible rows as one flat list: what the (virtualized) Explorer binds to.</summary>
    public ExplorerRowList ExplorerRows => _explorerRows ??= new ExplorerRowList(ExplorerRootItems);

    /// <summary>True when the workspace folder holds more files than the Explorer lists; the panel then says so rather than looking complete.</summary>
    [ObservableProperty]
    private bool _isExplorerTruncated;

    public string ExplorerTruncationText =>
        $"Showing the first {_storageService.WorkspaceFileLimit:N0} files. This folder has more: press Ctrl+P to open any file by name, or open a subfolder.";

    // The storage's StructureVersion the tree was last built at (-1: never built). The tree is rebuilt from a full
    // workspace scan only when this has moved, not on every tab switch.
    private long _explorerStructureVersion = -1;

    // The row listing the open document when it isn't part of this workspace (a file opened from elsewhere).
    private ExplorerItemViewModel? _explorerOrphanItem;

    private LazyExplorerTree? _lazyExplorer;

    // The Explorer of a workspace too big to list at once: one folder at a time (see LazyExplorerTree).
    private LazyExplorerTree LazyExplorer => _lazyExplorer ??= new LazyExplorerTree(
        _storageService,
        ExplorerRootItems,
        ExplorerRows,
        (name, path, parent) => CreateFolderItem(name, path, isExpanded: false, parent: parent),
        (summary, name, fullPath, parent) => CreateFileItem(name, summary.Id, parent, fullPath, summary.IsSourceFile ? _languages.Registry.Get(summary.LanguageId) : null));

    // Draws what the storage listed: the whole workspace, or (when it is too big) its top folder, the rest coming as folders open.
    private void ShowExplorerListing(WorkspaceListing listing)
    {
        if (!listing.IsPartial)
        {
            LazyExplorer.Deactivate();
            RebuildExplorerTree(listing.FolderPaths, listing.Items);
            return;
        }

        var openFolders = ExplorerItemViewModel.ExpandedFolderPaths(ExplorerRootItems);
        _explorerOrphanItem = null;
        LazyExplorer.Build(listing, openFolders);
        // Only the top folder itself can be cut off here (a nested one says so in its own rows).
        IsExplorerTruncated = listing.IsTruncated;
        EnsureOpenDocumentListed(sort: false);
        HighlightExplorerItem(Script?.Id);
    }

    public void PopulateExplorerTree()
    {
        try
        {
            var version = _storageService.StructureVersion;
            ShowExplorerListing(_storageService.LoadExplorerListingAsync().GetAwaiter().GetResult());
            _explorerStructureVersion = version;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to populate script explorer tree: {ex.Message}");
            ExplorerRootItems.Clear();
        }
    }

    [RelayCommand]
    public async Task RefreshExplorerAsync()
    {
        try
        {
            // Taken before the scan: a change that lands while it runs leaves the tree stale, so the next check reloads.
            var version = _storageService.StructureVersion;
            ShowExplorerListing(await _storageService.LoadExplorerListingAsync());
            _explorerStructureVersion = version;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to refresh script explorer tree: {ex.Message}");
        }
    }

    /// <summary>
    /// Brings the Explorer up to date without rescanning the workspace when nothing in it has changed: switching a tab
    /// or coming back to this page only moves the highlight (and lists the open document if it lives outside the workspace).
    /// </summary>
    public async Task RefreshExplorerIfStaleAsync()
    {
        if (_explorerStructureVersion != _storageService.StructureVersion)
        {
            await RefreshExplorerAsync();
            return;
        }

        EnsureOpenDocumentListed();
        HighlightExplorerItem(Script?.Id);
    }

    // A page that is not on screen leaves refreshing to its next visit. Assumed on screen until told otherwise (the
    // studio is also used without a host that says so).
    private bool _isPageActive = true;

    // Shown again after other pages (or other programs) may have added, removed or renamed items: catch up.
    public void OnActivated()
    {
        _isPageActive = true;
        _ = RefreshExplorerIfStaleAsync();

        // Reading the file index starts (or refreshes) its background walk, so Go to File is ready when it is used.
        _ = _storageService.FileIndex;
    }

    // Go to File: opens any file of the workspace by its path, whether or not the Explorer has drawn it.
    private async Task OpenWorkspaceFileAsync(string fullPath)
    {
        var result = await _storageService.OpenExternalProjectAsync(fullPath);
        if (!result.Success || string.IsNullOrEmpty(result.PrimaryDocumentId))
        {
            CompilerStatusText = result.Message;
            return;
        }

        if (result.PrimaryDocumentKind == WorkspaceItemKind.Server)
        {
            if (_openServerAction != null && await _storageService.LoadServerDocumentAsync(result.PrimaryDocumentId) is { } server)
            {
                _openServerAction.Invoke(server);
            }

            return;
        }

        if (result.PrimaryDocumentKind == WorkspaceItemKind.Notebook)
        {
            if (_openNotebookAction != null && await _storageService.LoadNotebookAsync(result.PrimaryDocumentId) is { } notebook)
            {
                _openNotebookAction.Invoke(notebook);
            }

            return;
        }

        await SaveDocumentAsync(userAsked: false);
        if (await _storageService.LoadScriptAsync(result.PrimaryDocumentId) is { } loaded)
        {
            await UpdateActiveScriptAsync(loaded);
        }
    }

    public void OnDeactivated() => _isPageActive = false;

    // The open document isn't part of the workspace when it was opened from elsewhere (a loose file, or a tab left over
    // from another folder): the tree lists it at the top for as long as it is the open one.
    private void EnsureOpenDocumentListed(bool sort = true, bool evenIfInWorkspace = false)
    {
        if (_explorerOrphanItem != null && !string.Equals(_explorerOrphanItem.DocumentId, Script?.Id, StringComparison.OrdinalIgnoreCase))
        {
            ExplorerRootItems.Remove(_explorerOrphanItem);
            _explorerOrphanItem = null;
        }

        if (Script == null || string.IsNullOrEmpty(Script.Id) || FindByDocumentId(ExplorerRootItems, Script.Id) != null) return;

        // In a big workspace the document may sit in a folder that has not been listed yet: it is not an outsider.
        if (!evenIfInWorkspace && LazyExplorer.IsActive && _storageService.GetWorkspaceRelativePath(Script.Id) != null) return;

        var sourceLanguage = Script.SourceFilePath != null ? ActiveLanguage : null;
        var fileName = sourceLanguage != null || Script.Title.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase) ? Script.Title : $"{Script.Title}.frycs";
        _explorerOrphanItem = CreateFileItem(fileName, Script.Id, parent: null, fullPath: fileName, sourceLanguage);
        ExplorerRootItems.Add(_explorerOrphanItem);
        if (sort) SortExplorerTree(ExplorerRootItems);
    }

    private void RebuildExplorerTree(IReadOnlyList<string> folderPaths, IReadOnlyList<WorkspaceItemSummary> summaries)
    {
        // Thousands of single changes below; the flat row list is rebuilt once, when the scope ends.
        using var rowsScope = ExplorerRows.Suspend();

        // A refresh keeps the folders the user had open open.
        var expandedFolders = ExplorerItemViewModel.ExpandedFolderPaths(ExplorerRootItems);
        ExplorerRootItems.Clear();
        _explorerOrphanItem = null;
        var folderNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);
        // The file names already in each folder (the root under its own key): a name-by-name scan of the siblings for every
        // file would be quadratic in the size of a folder.
        var rootKey = new object();
        var namesByFolder = new Dictionary<object, HashSet<string>>();

        ExplorerItemViewModel? GetOrCreateFolder(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (folderNodes.TryGetValue(relativePath, out var existing)) return existing;

            var lastSlash = relativePath.LastIndexOf('/');
            var name = lastSlash >= 0 ? relativePath[(lastSlash + 1)..] : relativePath;
            var parentPath = lastSlash >= 0 ? relativePath[..lastSlash] : string.Empty;
            var parent = GetOrCreateFolder(parentPath);

            var node = CreateFolderItem(name, relativePath, isExpanded: expandedFolders.Contains(relativePath), parent: parent);
            AddToTree(parent, node);
            folderNodes[relativePath] = node;
            return node;
        }

        foreach (var path in folderPaths.OrderBy(p => p.Count(c => c == '/')).ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            GetOrCreateFolder(path);
        }

        foreach (var s in summaries.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
        {
            var ext = s.DisplayExtension;
            var name = s.Title.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? s.Title : $"{s.Title}{ext}";

            var parent = GetOrCreateFolder(s.FolderPath);
            var fullPath = string.IsNullOrEmpty(s.FolderPath) ? name : $"{s.FolderPath}/{name}";

            var folderKey = (object?)parent ?? rootKey;
            if (!namesByFolder.TryGetValue(folderKey, out var names))
            {
                namesByFolder[folderKey] = names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!names.Add(name))
            {
                continue;
            }

            var docItem = CreateFileItem(name, s.Id, parent, fullPath, s.IsSourceFile ? _languages.Registry.Get(s.LanguageId) : null);
            AddToTree(parent, docItem);
        }

        EnsureOpenDocumentListed(sort: false);

        SortExplorerTree(ExplorerRootItems);
        HighlightExplorerItem(Script?.Id);
        IsExplorerTruncated = _storageService.IsWorkspaceTruncated;
    }

    private void SortExplorerTree(ObservableCollection<ExplorerItemViewModel> items)
    {
        using var rowsScope = ExplorerRows.Suspend();

        var sorted = items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (!sorted.SequenceEqual(items))
        {
            items.Clear();
            foreach (var item in sorted) items.Add(item);
        }

        foreach (var folder in items.Where(i => i.IsDirectory))
        {
            SortExplorerTree(folder.Children);
        }
    }

    private void DeselectAll(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var it in items)
        {
            it.IsSelected = false;
            if (it.Children.Count > 0) DeselectAll(it.Children);
        }
    }

    private void HighlightExplorerItem(string? documentId)
    {
        DeselectAll(ExplorerRootItems);
        if (string.IsNullOrEmpty(documentId)) return;

        var match = FindByDocumentId(ExplorerRootItems, documentId);
        if (match != null)
        {
            match.IsSelected = true;
            var parent = match.Parent;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }
        }
        else if (LazyExplorer.IsActive)
        {
            _ = RevealInLazyExplorerAsync(documentId);
        }
    }

    // A big workspace lists a folder when it is opened: open the folders down to the document, then highlight it.
    private async Task RevealInLazyExplorerAsync(string documentId)
    {
        var item = await LazyExplorer.RevealAsync(documentId);
        if (!string.Equals(Script?.Id, documentId, StringComparison.OrdinalIgnoreCase)) return;

        if (item == null)
        {
            // Not to be found in the folders (it sits in one the workspace walk leaves out, say): list it at the top, as an outsider.
            EnsureOpenDocumentListed(evenIfInWorkspace: true);
            return;
        }

        DeselectAll(ExplorerRootItems);
        item.IsSelected = true;
    }

    // The row of a document, opening the folders down to it first when the workspace is listed folder by folder.
    private async Task<ExplorerItemViewModel?> FindOrRevealExplorerItemAsync(string documentId)
    {
        var item = FindByDocumentId(ExplorerRootItems, documentId);
        if (item != null || !LazyExplorer.IsActive) return item;

        return await LazyExplorer.RevealAsync(documentId);
    }

    private ExplorerItemViewModel? FindByDocumentId(IEnumerable<ExplorerItemViewModel> items, string documentId)
    {
        foreach (var item in items)
        {
            if (!item.IsDirectory && string.Equals(item.DocumentId, documentId, StringComparison.OrdinalIgnoreCase)) return item;
            var childMatch = FindByDocumentId(item.Children, documentId);
            if (childMatch != null) return childMatch;
        }
        return null;
    }

    private ExplorerItemViewModel? FindSelectedItem(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var item in items)
        {
            if (item.IsSelected) return item;
            var childMatch = FindSelectedItem(item.Children);
            if (childMatch != null) return childMatch;
        }
        return null;
    }

    private void AddToTree(ExplorerItemViewModel? parent, ExplorerItemViewModel child)
    {
        if (parent != null) parent.Children.Add(child);
        else ExplorerRootItems.Add(child);
    }

    private ExplorerItemViewModel CreateFolderItem(string name, string fullPath, bool isExpanded = false, ExplorerItemViewModel? parent = null, bool isExternalGroup = false)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            IsDirectory = true,
            IsExternalGroup = isExternalGroup,
            IsExpanded = isExpanded,
            Parent = parent,
            Depth = (parent?.Depth ?? -1) + 1,
            FullPath = fullPath,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewScriptUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
    }

    /// <param name="sourceLanguage">The language of a plain source file (main.py), or null for a .frycs/.frynb document.</param>
    private ExplorerItemViewModel CreateFileItem(string name, string? documentId, ExplorerItemViewModel? parent, string fullPath, ILanguageDefinition? sourceLanguage = null)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            DocumentId = documentId,
            IsDirectory = false,
            IsSourceFile = sourceLanguage != null,
            LanguageIconKind = sourceLanguage?.IconKind,
            LanguageIconColor = sourceLanguage?.AccentHex,
            FileExtension = Path.GetExtension(name),
            Parent = parent,
            Depth = (parent?.Depth ?? -1) + 1,
            FullPath = fullPath,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewScriptUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
    }

    private void OnExplorerItemClicked(ExplorerItemViewModel item) => _ = SwitchToScriptAsync(item);

    public async Task SwitchToScriptAsync(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
            return;
        }

        if (string.IsNullOrEmpty(item.DocumentId)) return;

        if (item.FileExtension.Equals(".fryserver", StringComparison.OrdinalIgnoreCase))
        {
            if (_openServerAction != null)
            {
                var server = await _storageService.LoadServerDocumentAsync(item.DocumentId);
                if (server != null)
                {
                    _openServerAction.Invoke(server);
                    return;
                }
            }
        }

        if (item.FileExtension.Equals(".frynb", StringComparison.OrdinalIgnoreCase) ||
            item.FileExtension.Equals(".ipynb", StringComparison.OrdinalIgnoreCase))
        {
            if (_openNotebookAction != null)
            {
                var nb = await _storageService.LoadNotebookAsync(item.DocumentId);
                if (nb != null)
                {
                    _openNotebookAction.Invoke(nb);
                    return;
                }
            }
        }

        if (Script != null && string.Equals(Script.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase))
        {
            HighlightExplorerItem(item.DocumentId);
            return;
        }

        await SaveDocumentAsync(userAsked: false);

        var loaded = await _storageService.LoadScriptAsync(item.DocumentId);
        if (loaded == null) return;

        await UpdateActiveScriptAsync(loaded);
    }

    public void DeleteExplorerItem(ExplorerItemViewModel item) => _ = DeleteExplorerItemAsync(item);

    [RelayCommand]
    public async Task DeleteExplorerItemAsync(ExplorerItemViewModel item)
    {
        if (item == null || item.IsExternalGroup) return;

        if (item.IsDirectory)
        {
            try
            {
                await _storageService.DeleteFolderAsync(item.FullPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to delete folder '{item.FullPath}': {ex.Message}");
            }
        }
        else if (!string.IsNullOrEmpty(item.DocumentId))
        {
            try
            {
                await _storageService.DeleteItemAsync(item.DocumentId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to delete '{item.DocumentId}': {ex.Message}");
            }
        }

        if (item.Parent != null)
        {
            var parent = item.Parent;
            parent.Children.Remove(item);
            if (parent.IsExternalGroup && parent.Children.Count == 0)
            {
                ExplorerRootItems.Remove(parent);
            }
        }
        else
        {
            ExplorerRootItems.Remove(item);
        }
    }

    [RelayCommand]
    public async Task OpenExternalProjectAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var result = await _storageService.OpenExternalProjectAsync(path);
            if (!result.Success)
            {
                CompilerStatusText = result.Message;
                return;
            }

            await RefreshExplorerAsync();

            if (!string.IsNullOrEmpty(result.PrimaryDocumentId))
            {
                var loaded = await _storageService.LoadScriptAsync(result.PrimaryDocumentId);
                if (loaded != null)
                {
                    await SaveDocumentAsync(userAsked: false);
                    await UpdateActiveScriptAsync(loaded);
                }
            }

            CompilerStatusText = result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to open external project '{path}': {ex.Message}");
            CompilerStatusText = $"Error opening project: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task NewScript()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : selected?.Parent;

        if (targetFolder != null)
        {
            await NewScriptUnderItemAsync(targetFolder);
            return;
        }

        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Script_{timestamp}";
        ScriptDocumentItem newDoc;
        try
        {
            newDoc = await _storageService.CreateNewScriptAsync(title);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create script: {ex.Message}");
            return;
        }

        await SaveDocumentAsync(userAsked: false);
        await UpdateActiveScriptAsync(newDoc);
        (await FindOrRevealExplorerItemAsync(newDoc.Id))?.StartRename();
    }

    private IReadOnlyList<NewFileOption>? _newFileOptions;

    /// <summary>"New Python File" and so on: one entry per language whose documents are plain source files.</summary>
    public IReadOnlyList<NewFileOption> NewFileOptions => _newFileOptions ??= _languages.Registry.SourceFileLanguages
        .Select(language => new NewFileOption(
            language.Id,
            $"New {language.DisplayName} File",
            language.IconKind,
            language.AccentHex,
            new AsyncRelayCommand<ExplorerItemViewModel?>(target => NewSourceFileAsync(language.Id, target))))
        .ToArray();

    /// <summary>Creates a source file (script_HHmmss.py) in <paramref name="target"/>'s folder, or the selected one, opens it and starts renaming it.</summary>
    public async Task NewSourceFileAsync(string languageId, ExplorerItemViewModel? target = null)
    {
        target ??= FindSelectedItem(ExplorerRootItems);
        var folder = target == null ? null : target.IsDirectory ? target : target.Parent;

        ScriptDocumentItem? newDoc;
        try
        {
            newDoc = await _storageService.CreateNewSourceFileAsync(languageId, folderPath: folder?.FullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CompilerStatusText = $"⚠️ Couldn't create the file: {ex.Message}";
            return;
        }

        if (newDoc == null)
        {
            CompilerStatusText = "⚠️ Couldn't create the file.";
            return;
        }

        await SaveDocumentAsync(userAsked: false);
        await UpdateActiveScriptAsync(newDoc);
        (await FindOrRevealExplorerItemAsync(newDoc.Id))?.StartRename();
    }

    [RelayCommand]
    public async Task NewFolder()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : selected?.Parent;
        await CreateFolderCoreAsync(targetFolder);
    }

    public void NewScriptUnderItem(ExplorerItemViewModel target) => _ = NewScriptUnderItemAsync(target);

    [RelayCommand]
    public async Task NewScriptUnderItemAsync(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : target.Parent;
        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Script_{timestamp}";
        var folderPath = folder?.FullPath;

        ScriptDocumentItem newDoc;
        try
        {
            newDoc = await _storageService.CreateNewScriptAsync(title, folderPath: folderPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create script: {ex.Message}");
            return;
        }

        await SaveDocumentAsync(userAsked: false);
        await UpdateActiveScriptAsync(newDoc);

        var newItem = await FindOrRevealExplorerItemAsync(newDoc.Id);
        newItem?.StartRename();
    }

    public void NewFolderUnderItem(ExplorerItemViewModel target) => _ = NewFolderUnderItemAsync(target);

    [RelayCommand]
    public async Task NewFolderUnderItemAsync(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : target.Parent;
        await CreateFolderCoreAsync(folder);
    }

    private async Task CreateFolderCoreAsync(ExplorerItemViewModel? parentFolder)
    {
        string newRelativePath;
        try
        {
            newRelativePath = await _storageService.CreateFolderAsync(parentFolder?.FullPath, "New Folder");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create folder: {ex.Message}");
            return;
        }

        var name = newRelativePath.Contains('/') ? newRelativePath[(newRelativePath.LastIndexOf('/') + 1)..] : newRelativePath;

        // Under a folder that has not been listed yet, listing it finds the new folder on disk: adding a row too would show it twice.
        if (parentFolder is { ChildrenLoaded: false })
        {
            await LazyExplorer.LoadChildrenAsync(parentFolder);
            parentFolder.IsExpanded = true;
            parentFolder.Children.FirstOrDefault(c => c.IsDirectory && string.Equals(c.FullPath, newRelativePath, StringComparison.OrdinalIgnoreCase))?.StartRename();
            return;
        }

        var newFolder = CreateFolderItem(name, newRelativePath, isExpanded: true, parent: parentFolder);
        AddToTree(parentFolder, newFolder);
        if (parentFolder != null) parentFolder.IsExpanded = true;
        newFolder.StartRename();
    }

    public void DuplicateExplorerItem(ExplorerItemViewModel item) => _ = DuplicateExplorerItemAsync(item);

    [RelayCommand]
    public async Task DuplicateExplorerItemAsync(ExplorerItemViewModel item)
    {
        if (item == null || item.IsDirectory || string.IsNullOrEmpty(item.DocumentId)) return;

        if (item.IsSourceFile)
        {
            await DuplicateSourceFileAsync(item);
            return;
        }

        var parent = item.Parent;
        var originalTitle = item.Name.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase)
            ? item.Name.Substring(0, item.Name.Length - 6)
            : item.Name;
        var copyTitle = $"{originalTitle} Copy";
        var copyFileName = $"{copyTitle}.frycs";

        var isActive = Script != null && string.Equals(Script.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase);
        var origDoc = isActive ? Script : await _storageService.LoadScriptAsync(item.DocumentId);

        var copyDoc = new ScriptDocumentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = copyTitle,
            Category = origDoc?.Category ?? "Custom",
            Description = origDoc?.Description ?? "",
            ExecutionMode = origDoc?.ExecutionMode ?? "Statements",
            Code = origDoc?.Code ?? string.Empty,
            Notes = origDoc?.Notes ?? string.Empty,
            TestCases = origDoc?.TestCases?
                .Select(tc => new TestCaseItem { Name = tc.Name, Input = tc.Input, ExpectedOutput = tc.ExpectedOutput })
                .ToList() ?? new List<TestCaseItem>(),
            Created = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        await _storageService.SaveScriptAsync(copyDoc, parent?.FullPath);

        var copyPath = string.IsNullOrEmpty(parent?.FullPath) ? copyFileName : $"{parent!.FullPath}/{copyFileName}";
        var copyItem = CreateFileItem(copyFileName, copyDoc.Id, parent, copyPath);
        AddToTree(parent, copyItem);
        if (parent != null) parent.IsExpanded = true;

        await SwitchToScriptAsync(copyItem);
    }

    // main.py → main_copy.py next to it, with the text as it is in the editor if it's open.
    private async Task DuplicateSourceFileAsync(ExplorerItemViewModel item)
    {
        var isActive = string.Equals(Script.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase);
        var original = isActive ? Script : await _storageService.LoadScriptAsync(item.DocumentId!);
        if (original == null) return;

        var copy = await _storageService.CreateNewSourceFileAsync(original.LanguageId, $"{Path.GetFileNameWithoutExtension(item.Name)}_copy", item.Parent?.FullPath);
        if (copy == null) return;
        copy.Code = isActive ? Code : original.Code;
        await _storageService.SaveSourceFileAsync(copy, overwriteChangesOnDisk: true);

        await SaveDocumentAsync(userAsked: false);
        await UpdateActiveScriptAsync(copy);
    }

    private void OnItemRenamed(ExplorerItemViewModel item) => _ = OnItemRenamedAsync(item);

    internal async Task OnItemRenamedAsync(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            if (item.IsExternalGroup) return;

            try
            {
                var oldPath = item.FullPath;
                var newPath = await _storageService.RenameFolderAsync(oldPath, item.Name);
                UpdateDescendantFullPaths(item, oldPath, newPath);
                item.FullPath = newPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to rename folder '{item.FullPath}': {ex.Message}");
            }
            return;
        }

        if (string.IsNullOrEmpty(item.DocumentId)) return;

        if (item.IsSourceFile)
        {
            await RenameSourceFileAsync(item);
            return;
        }

        var newTitle = item.Name.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase)
            ? item.Name.Substring(0, item.Name.Length - 6)
            : item.Name;

        if (Script != null && string.Equals(Script.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase))
        {
            Script.Title = newTitle;
            OnPropertyChanged(nameof(Script));
            await SaveAsync();
        }
        else
        {
            var doc = await _storageService.LoadScriptAsync(item.DocumentId);
            if (doc != null)
            {
                doc.Title = newTitle;
                await _storageService.SaveScriptAsync(doc);
            }
        }
    }

    // A source file's name is its file name: renaming moves the file (and its id, which follows the path).
    private async Task RenameSourceFileAsync(ExplorerItemViewModel item)
    {
        var oldId = item.DocumentId!;
        var oldName = Path.GetFileName(item.FullPath);
        try
        {
            var renamed = await _storageService.RenameSourceFileAsync(oldId, item.Name);
            var newName = Path.GetFileName(renamed.FilePath);
            item.DocumentId = renamed.Id;
            item.Name = newName;
            item.FullPath = item.Parent == null ? newName : $"{item.Parent.FullPath}/{newName}";

            // An open tab keeps editing the same file under its new name.
            var tab = OpenTabs.FirstOrDefault(t => string.Equals(t.Id, oldId, StringComparison.OrdinalIgnoreCase));
            var document = tab?.Document ?? (string.Equals(Script.Id, oldId, StringComparison.OrdinalIgnoreCase) ? Script : null);
            if (document != null)
            {
                document.Id = renamed.Id;
                document.Title = newName;
                document.SourceFilePath = renamed.FilePath;
                tab?.NotifyTitleChanged();
                if (ReferenceEquals(document, Script)) OnPropertyChanged(nameof(Script));
                RefreshQuickOpenDocuments();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            item.Name = oldName;
            item.FileExtension = Path.GetExtension(oldName);
            CompilerStatusText = $"⚠️ Couldn't rename {oldName}: {ex.Message}";
        }
    }

    private void UpdateDescendantFullPaths(ExplorerItemViewModel node, string oldPrefix, string newPrefix)
    {
        foreach (var child in node.Children)
        {
            if (child.FullPath.StartsWith(oldPrefix, StringComparison.Ordinal))
            {
                child.FullPath = newPrefix + child.FullPath[oldPrefix.Length..];
            }
            UpdateDescendantFullPaths(child, oldPrefix, newPrefix);
        }
    }

    [RelayCommand]
    public void CopyItemPath(ExplorerItemViewModel item)
    {
        if (item == null) return;
        var path = !string.IsNullOrEmpty(item.FullPath) ? item.FullPath : item.Name;
        CompilerStatusText = $"Path: {path}";
    }

    [RelayCommand]
    public void CollapseAllExplorer()
    {
        foreach (var item in ExplorerRootItems)
        {
            CollapseItemRecursive(item);
        }
    }

    private void CollapseItemRecursive(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = false;
            foreach (var child in item.Children)
            {
                CollapseItemRecursive(child);
            }
        }
    }
}
