using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

public interface IRecentWorkspaceService
{
    Task<IReadOnlyList<RecentWorkspaceItem>> LoadRecentWorkspacesAsync();
    Task<RecentWorkspaceItem> RecordWorkspaceOpenedAsync(string path, RecentWorkspaceKind? explicitKind = null);
    Task TogglePinAsync(string idOrPath);
    Task RemoveRecentAsync(string idOrPath);
    string? DetectGitBranch(string folderPath);
}

public class RecentWorkspaceService : IRecentWorkspaceService
{
    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<RecentWorkspaceItem> _items = new();
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RecentWorkspaceService(string storageFilePath)
    {
        _storageFilePath = storageFilePath;
    }

    public async Task<IReadOnlyList<RecentWorkspaceItem>> LoadRecentWorkspacesAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_loaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _loaded = true;
            }

            return _items
                .OrderByDescending(i => i.IsPinned)
                .ThenByDescending(i => i.LastOpenedUtc)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RecentWorkspaceItem> RecordWorkspaceOpenedAsync(string path, RecentWorkspaceKind? explicitKind = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        var fullPath = Path.GetFullPath(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_loaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _loaded = true;
            }

            var isNotebook = fullPath.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase) ||
                             fullPath.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase);

            var isScript = !isNotebook && (
                fullPath.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase) ||
                fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                fullPath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase));

            var kind = explicitKind ?? (isNotebook ? RecentWorkspaceKind.StandaloneNotebook : (isScript ? RecentWorkspaceKind.StandaloneScript : RecentWorkspaceKind.ProjectWorkspace));

            var existing = _items.FirstOrDefault(i => string.Equals(i.Path, fullPath, StringComparison.OrdinalIgnoreCase));

            string name;
            if (isNotebook || isScript)
            {
                name = Path.GetFileName(fullPath);
            }
            else
            {
                name = Path.GetFileName(fullPath);
                if (string.IsNullOrWhiteSpace(name)) name = fullPath;
            }

            string? branch = null;
            if (kind == RecentWorkspaceKind.ProjectWorkspace)
            {
                branch = DetectGitBranch(fullPath);
            }

            if (existing != null)
            {
                existing.LastOpenedUtc = DateTime.UtcNow;
                existing.Name = name;
                existing.Kind = kind;
                if (!string.IsNullOrEmpty(branch)) existing.GitBranch = branch;
                _items.Remove(existing);
                _items.Insert(0, existing);
                await SaveToDiskAsync().ConfigureAwait(false);
                return existing;
            }

            var newItem = new RecentWorkspaceItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Path = fullPath,
                Kind = kind,
                LastOpenedUtc = DateTime.UtcNow,
                GitBranch = branch,
                IsPinned = false
            };

            _items.Insert(0, newItem);
            if (_items.Count > 50)
            {
                _items.RemoveAt(_items.Count - 1);
            }

            await SaveToDiskAsync().ConfigureAwait(false);
            return newItem;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task TogglePinAsync(string idOrPath)
    {
        if (string.IsNullOrWhiteSpace(idOrPath)) return;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_loaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _loaded = true;
            }

            var item = _items.FirstOrDefault(i =>
                string.Equals(i.Id, idOrPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Path, idOrPath, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                await SaveToDiskAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveRecentAsync(string idOrPath)
    {
        if (string.IsNullOrWhiteSpace(idOrPath)) return;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_loaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _loaded = true;
            }

            var removed = _items.RemoveAll(i =>
                string.Equals(i.Id, idOrPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Path, idOrPath, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                await SaveToDiskAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public string? DetectGitBranch(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return null;

            var gitPath = Path.Combine(folderPath, ".git");
            string headPath;

            if (Directory.Exists(gitPath))
            {
                headPath = Path.Combine(gitPath, "HEAD");
            }
            else if (File.Exists(gitPath))
            {
                var line = File.ReadAllLines(gitPath).FirstOrDefault()?.Trim();
                if (line != null && line.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                {
                    var relativeOrAbsolute = line["gitdir:".Length..].Trim();
                    var resolved = Path.IsPathRooted(relativeOrAbsolute)
                        ? relativeOrAbsolute
                        : Path.Combine(folderPath, relativeOrAbsolute);
                    headPath = Path.Combine(resolved, "HEAD");
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            if (!File.Exists(headPath)) return null;

            var headContent = File.ReadAllText(headPath).Trim();
            if (headContent.StartsWith("ref: refs/heads/", StringComparison.OrdinalIgnoreCase))
            {
                return headContent["ref: refs/heads/".Length..].Trim();
            }

            // Detached HEAD: return short commit hash
            if (headContent.Length >= 7 && headContent.All(c => Uri.IsHexDigit(c)))
            {
                return headContent[..7];
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadFromDiskAsync()
    {
        _items.Clear();
        if (!File.Exists(_storageFilePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(_storageFilePath).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<List<RecentWorkspaceItem>>(json, JsonOptions);
            if (loaded != null)
            {
                _items.AddRange(loaded.Where(i => !string.IsNullOrWhiteSpace(i.Path)));
            }
        }
        catch
        {
            // Gracefully ignore corrupt file
        }
    }

    private async Task SaveToDiskAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_items, JsonOptions);
            await File.WriteAllTextAsync(_storageFilePath, json).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
