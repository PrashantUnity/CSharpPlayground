using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Workspace;

/// <summary>
/// Bridge implementation of IWorkspaceApi connecting user scripts with the IDE workspace and file tree.
/// </summary>
public class ExtensibilityWorkspaceService : IWorkspaceApi
{
    public Func<string?>? RootPathResolver { get; set; }
    public Action? RefreshExplorerHandler { get; set; }
    public Func<string, Task>? OpenWorkspaceHandler { get; set; }

    public string? RootPath => RootPathResolver?.Invoke();

    public bool HasWorkspace => !string.IsNullOrWhiteSpace(RootPath) && Directory.Exists(RootPath);

    public IReadOnlyList<string> FindFiles(string searchPattern = "*.*", bool recursive = true)
    {
        var root = RootPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        try
        {
            var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.GetFiles(root, searchPattern, opt)
                .Select(p => Path.GetRelativePath(root, p))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<string> ReadTextAsync(string relativeOrAbsolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeOrAbsolutePath);
        var fullPath = ResolvePath(relativeOrAbsolutePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Workspace file not found: {relativeOrAbsolutePath}", fullPath);
        }
        return await File.ReadAllTextAsync(fullPath);
    }

    public async Task WriteTextAsync(string relativeOrAbsolutePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeOrAbsolutePath);
        var fullPath = ResolvePath(relativeOrAbsolutePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(fullPath, content ?? string.Empty);
    }

    public bool FileExists(string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath)) return false;
        var fullPath = ResolvePath(relativeOrAbsolutePath);
        return File.Exists(fullPath);
    }

    public Task CreateFileAsync(string relativeOrAbsolutePath, string content = "") =>
        WriteTextAsync(relativeOrAbsolutePath, content);

    public bool DeleteFile(string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath)) return false;
        var fullPath = ResolvePath(relativeOrAbsolutePath);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public void Refresh()
    {
        RefreshExplorerHandler?.Invoke();
    }

    public async Task OpenWorkspaceAsync(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (OpenWorkspaceHandler != null)
        {
            await OpenWorkspaceHandler(folderPath);
        }
    }

    private string ResolvePath(string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath)) return relativeOrAbsolutePath;
        var root = RootPath ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(root, relativeOrAbsolutePath));
    }
}
