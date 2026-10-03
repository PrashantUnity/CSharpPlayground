using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;

public sealed class LockedGitPackage
{
    public string PackageId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ResolvedRef { get; set; }
    public string CommitSha { get; set; } = string.Empty;
    public string SubPath { get; set; } = string.Empty;
    public string CacheDirectory { get; set; } = string.Empty;
    public string? Version { get; set; }
    public DateTimeOffset InstalledAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Workspace-level package lockfile stored in .frysharp/extensions-lock.json.
/// Similar to Unity's Packages/packages-lock.json, records exact immutable commit SHAs
/// and resolved cache paths for deterministic team environments.
/// </summary>
public sealed class WorkspaceExtensionLockfile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public Dictionary<string, LockedGitPackage> Dependencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<WorkspaceExtensionLockfile> LoadAsync(string lockfilePath, CancellationToken ct = default)
    {
        if (!File.Exists(lockfilePath))
        {
            return new WorkspaceExtensionLockfile();
        }

        try
        {
            string json = await File.ReadAllTextAsync(lockfilePath, ct);
            return JsonSerializer.Deserialize<WorkspaceExtensionLockfile>(json, JsonOptions) ?? new WorkspaceExtensionLockfile();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkspaceExtensionLockfile] Failed to load {lockfilePath}: {ex.Message}");
            return new WorkspaceExtensionLockfile();
        }
    }

    public async Task SaveAsync(string lockfilePath, CancellationToken ct = default)
    {
        string? dir = Path.GetDirectoryName(lockfilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(this, JsonOptions);
        await File.WriteAllTextAsync(lockfilePath, json, ct);
    }
}
