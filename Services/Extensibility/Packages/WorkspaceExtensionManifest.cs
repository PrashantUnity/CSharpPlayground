using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;

/// <summary>
/// Workspace-level extension manifest stored in .frysharp/extensions.json.
/// Similar to Unity's Packages/manifest.json, maps package identifiers to Git URLs or local paths.
/// </summary>
public sealed class WorkspaceExtensionManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public Dictionary<string, string> Dependencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<WorkspaceExtensionManifest> LoadAsync(string manifestPath, CancellationToken ct = default)
    {
        if (!File.Exists(manifestPath))
        {
            return new WorkspaceExtensionManifest();
        }

        try
        {
            string json = await File.ReadAllTextAsync(manifestPath, ct);
            return JsonSerializer.Deserialize<WorkspaceExtensionManifest>(json, JsonOptions) ?? new WorkspaceExtensionManifest();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkspaceExtensionManifest] Failed to load {manifestPath}: {ex.Message}");
            return new WorkspaceExtensionManifest();
        }
    }

    public async Task SaveAsync(string manifestPath, CancellationToken ct = default)
    {
        string? dir = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(this, JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, ct);
    }
}
