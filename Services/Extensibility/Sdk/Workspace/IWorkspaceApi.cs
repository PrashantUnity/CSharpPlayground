using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for interacting with the active workspace, project files, and file system.
/// </summary>
public interface IWorkspaceApi
{
    /// <summary>Absolute path to the currently open workspace root folder, or null if no workspace is open.</summary>
    string? RootPath { get; }

    /// <summary>Whether a workspace folder is currently open.</summary>
    bool HasWorkspace { get; }

    /// <summary>
    /// Finds files matching glob patterns relative to the workspace root (e.g. "**/*.cs", "*.json", "*.*").
    /// </summary>
    IReadOnlyList<string> FindFiles(string searchPattern = "*.*", bool recursive = true);

    /// <summary>Reads text from a file relative to the workspace root (or absolute path).</summary>
    Task<string> ReadTextAsync(string relativeOrAbsolutePath);

    /// <summary>Writes text to a file relative to the workspace root (or absolute path).</summary>
    Task WriteTextAsync(string relativeOrAbsolutePath, string content);

    /// <summary>Checks whether a file exists in the workspace.</summary>
    bool FileExists(string relativeOrAbsolutePath);

    /// <summary>Creates a file with optional initial content in the workspace.</summary>
    Task CreateFileAsync(string relativeOrAbsolutePath, string content = "");

    /// <summary>Deletes a file from the workspace.</summary>
    bool DeleteFile(string relativeOrAbsolutePath);

    /// <summary>Refreshes the file explorer tree view in the Primary Side Bar.</summary>
    void Refresh();

    /// <summary>Opens a folder as the active workspace.</summary>
    Task OpenWorkspaceAsync(string folderPath);
}
