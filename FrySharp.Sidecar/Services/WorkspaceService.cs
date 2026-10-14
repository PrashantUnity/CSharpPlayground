using FrySharp.Sidecar.Hosting;
using FrySharp.Sidecar.Protocol;
using Microsoft.Extensions.Logging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Services;

/// <summary>
/// The workspace folder shared by the sidecar: one file index (for Go to File and Find in Files), kept current by a
/// watcher, folders listed one level at a time, and atomic file operations.
/// </summary>
public sealed class WorkspaceService : IDisposable
{
    public const int MaxFolderEntries = 5000;

    private readonly WorkspaceFileIndex _index = new(WorkspaceWalker.IsExplorerFile);
    private readonly WorkspaceWatcher? _watcher;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(SidecarOptions options, ILogger<WorkspaceService> logger)
    {
        _logger = logger;
        Paths = new WorkspacePaths(options.WorkspaceRoot);
        _ = _index.RebuildAsync(Paths.Root);
        _watcher = WorkspaceWatcher.TryStart(Paths.Root, OnChangedOnDisk);
        if (_watcher == null) _logger.LogWarning("Not watching {Root} for changes; workspace refreshes when requested.", Paths.Root);
    }

    public WorkspacePaths Paths { get; }

    /// <summary>Raised after files changed on disk; true when files or folders came or went.</summary>
    public event Action<bool>? ChangedOnDisk;

    private void OnChangedOnDisk(bool structural)
    {
        if (structural) _ = _index.RebuildAsync(Paths.Root);
        ChangedOnDisk?.Invoke(structural);
    }

    /// <summary>One folder's folders and files, folders first, each group by name; hidden ones left out.</summary>
    public Task<FolderListing> ListFolderAsync(string path, CancellationToken ct) => Task.Run(() =>
    {
        var folder = ResolveExisting(path, allowRoot: true, mustBeFolder: true);
        var entries = new List<FolderEntry>();
        var truncated = false;
        var info = new DirectoryInfo(folder);
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0, RecurseSubdirectories = false };

        foreach (var entry in info.EnumerateFileSystemInfos("*", options))
        {
            ct.ThrowIfCancellationRequested();
            if (entries.Count >= MaxFolderEntries)
            {
                truncated = true;
                break;
            }

            if (entry.LinkTarget != null && !Paths.TryResolve(Paths.ToRelative(entry.FullName), out _)) continue;

            if (entry is DirectoryInfo directory)
            {
                if (WorkspaceWalker.IsHiddenInExplorer(directory.Name)) continue;
                entries.Add(new FolderEntry(directory.Name, Paths.ToRelative(directory.FullName), true, null, null));
            }
            else if (WorkspaceWalker.IsExplorerFile(entry.FullName))
            {
                var file = (FileInfo)entry;
                entries.Add(new FolderEntry(file.Name, Paths.ToRelative(file.FullName), false, file.Length, file.LastWriteTimeUtc));
            }
        }

        entries.Sort((a, b) => a.IsFolder != b.IsFolder
            ? (a.IsFolder ? -1 : 1)
            : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return new FolderListing(Paths.ToRelative(folder), entries, truncated);
    }, ct);

    public FindFilesResult FindFiles(string query, int limit)
    {
        var matches = string.IsNullOrWhiteSpace(query)
            ? []
            : _index.Find(query, Math.Clamp(limit, 1, 500)).Select(m => new FileMatch(m.RelativePath, m.Name)).ToList();
        return new FindFilesResult(matches, _index.IsBuilding);
    }

    public async Task<SearchSummary> SearchAsync(SearchParams request, IReadOnlyDictionary<string, string>? openDocuments,
        Action<string, IReadOnlyList<SearchHit>> onFile, CancellationToken ct)
    {
        var matcher = new TextMatcher(request.Query, request.MatchCase, request.WholeWord, request.IsRegex);
        if (!matcher.IsValid)
        {
            throw new LocalRpcException(matcher.Error ?? "Type something to search for.") { ErrorCode = ErrorCodes.Invalid };
        }

        var summary = await WorkspaceTextSearch.RunAsync(_index.Paths, Paths.Root, matcher, openDocuments,
            hits => onFile(hits.RelativePath, hits.Hits.Select(h => new SearchHit(h.Line, h.Column, h.Length, h.Text)).ToList()),
            ct).ConfigureAwait(false);
        return new SearchSummary(summary.FilesSearched, summary.FilesWithMatches, summary.Matches, summary.HitLimit);
    }

    public async Task<string> CreateFileAsync(string path, string content, CancellationToken ct)
    {
        var full = Resolve(path);
        if (File.Exists(full) || Directory.Exists(full)) throw Error(ErrorCodes.AlreadyExists, $"'{path}' already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(content.AsMemory(), ct).ConfigureAwait(false);
        }
        return Paths.ToRelative(full);
    }

    public string CreateFolder(string path)
    {
        var full = Resolve(path);
        if (File.Exists(full) || Directory.Exists(full)) throw Error(ErrorCodes.AlreadyExists, $"'{path}' already exists.");
        Directory.CreateDirectory(full);
        return Paths.ToRelative(full);
    }

    public void Delete(string path)
    {
        var full = Resolve(path);
        var info = new FileInfo(full);
        if (info.LinkTarget != null)
        {
            info.Delete();
        }
        else if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
        else if (File.Exists(full))
        {
            File.Delete(full);
        }
        else
        {
            throw Error(ErrorCodes.NotFound, $"'{path}' doesn't exist.");
        }
    }

    public string Rename(string from, string to)
    {
        var source = Resolve(from);
        var target = Resolve(to);
        if (!File.Exists(source) && !Directory.Exists(source)) throw Error(ErrorCodes.NotFound, $"'{from}' doesn't exist.");

        var caseOnly = string.Equals(source, target, StringComparison.OrdinalIgnoreCase) && !string.Equals(source, target, StringComparison.Ordinal);
        if (!caseOnly && (File.Exists(target) || Directory.Exists(target))) throw Error(ErrorCodes.AlreadyExists, $"'{to}' already exists.");
        if (Directory.Exists(source) && target.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw Error(ErrorCodes.Invalid, "A folder can't be moved into itself.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (Directory.Exists(source)) Directory.Move(source, target);
        else File.Move(source, target, overwrite: false);
        return Paths.ToRelative(target);
    }

    public string Resolve(string? path, bool allowRoot = false) =>
        Paths.TryResolve(path, out var full, allowRoot)
            ? full
            : throw Error(ErrorCodes.AccessDenied, $"'{path}' is not a path inside the workspace.");

    private string ResolveExisting(string? path, bool allowRoot, bool mustBeFolder)
    {
        var full = Resolve(path, allowRoot);
        if (mustBeFolder ? !Directory.Exists(full) : !File.Exists(full)) throw Error(ErrorCodes.NotFound, $"'{path}' doesn't exist.");
        return full;
    }

    private static LocalRpcException Error(int code, string message) => new(message) { ErrorCode = code };

    public void Dispose() => _watcher?.Dispose();
}
