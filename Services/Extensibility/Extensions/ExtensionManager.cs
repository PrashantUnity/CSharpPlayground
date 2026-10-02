using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;

public class ExtensionLoadResult
{
    public string ExtensionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<DiagnosticItem> Diagnostics { get; set; } = Array.Empty<DiagnosticItem>();
    public LoadedExtension? Extension { get; set; }
}

/// <summary>
/// Discovers, compiles, loads, unloads, and hot-reloads multi-file extensions.
/// </summary>
public class ExtensionManager : IDisposable
{
    private readonly ExtensionCompiler _compiler = new();
    private readonly ConcurrentDictionary<string, LoadedExtension> _loadedExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, System.Threading.Timer> _debounceTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public IReadOnlyList<LoadedExtension> LoadedExtensions => _loadedExtensions.Values.ToList();

    public event Action<ExtensionLoadResult>? ExtensionLoaded;
    public event Action<string>? ExtensionUnloaded;

    public async Task<IReadOnlyList<ExtensionLoadResult>> DiscoverAndLoadAllAsync(string extensionsRootDirectory, bool enableHotReload = true, CancellationToken ct = default)
    {
        var results = new List<ExtensionLoadResult>();

        if (!Directory.Exists(extensionsRootDirectory))
        {
            return results;
        }

        foreach (var subDir in Directory.GetDirectories(extensionsRootDirectory))
        {
            string manifestFile = Path.Combine(subDir, "extension.json");
            if (File.Exists(manifestFile))
            {
                var result = await LoadExtensionAsync(subDir, enableHotReload, ct);
                results.Add(result);
            }
        }

        return results;
    }

    public async Task<ExtensionLoadResult> LoadExtensionAsync(string extensionDirectory, bool enableHotReload = true, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionDirectory);

        string manifestPath = Path.Combine(extensionDirectory, "extension.json");
        if (!File.Exists(manifestPath))
        {
            return new ExtensionLoadResult
            {
                Success = false,
                ErrorMessage = $"extension.json not found in {extensionDirectory}"
            };
        }

        ExtensionManifest manifest;
        try
        {
            string json = await File.ReadAllTextAsync(manifestPath, ct);
            manifest = JsonSerializer.Deserialize<ExtensionManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize extension.json");
        }
        catch (Exception ex)
        {
            return new ExtensionLoadResult
            {
                Success = false,
                ErrorMessage = $"Invalid manifest: {ex.Message}"
            };
        }

        var result = new ExtensionLoadResult
        {
            ExtensionId = manifest.Id
        };

        // 1. Unload old version if already loaded
        await UnloadExtensionAsync(manifest.Id);

        // 2. Compile directory
        var (compileSuccess, peBytes, pdbBytes, diags) = await Task.Run(() =>
        {
            var files = manifest.SourceFiles.Count > 0 ? manifest.SourceFiles : null;
            return _compiler.CompileDirectory(extensionDirectory, files);
        }, ct);

        result.Diagnostics = diags;

        if (!compileSuccess || peBytes == null)
        {
            result.Success = false;
            result.ErrorMessage = "Compilation of extension source files failed.";
            ExtensionLoaded?.Invoke(result);
            return result;
        }

        // 3. Load into isolated collectible ALC
        var alc = new ExtensionLoadContext($"Ext_{manifest.Id}");
        var regBag = new LifetimeRegistrationBag();
        var loadedExt = new LoadedExtension(StudioAppContext.Instance, extensionDirectory, manifest, alc, regBag);

        try
        {
            using var peStream = new MemoryStream(peBytes);
            using var pdbStream = pdbBytes != null ? new MemoryStream(pdbBytes) : null;

            var assembly = pdbStream != null
                ? alc.LoadFromStream(peStream, pdbStream)
                : alc.LoadFromStream(peStream);

            Type? entryType = null;

            if (!string.IsNullOrWhiteSpace(manifest.MainEntryClass))
            {
                entryType = assembly.GetType(manifest.MainEntryClass);
            }

            entryType ??= assembly.GetTypes().FirstOrDefault(t =>
                typeof(IExtensionEntryPoint).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            if (entryType != null)
            {
                if (Activator.CreateInstance(entryType) is IExtensionEntryPoint entry)
                {
                    loadedExt.EntryPoint = entry;
                    await entry.InitializeAsync(loadedExt);
                }
            }

            _loadedExtensions[manifest.Id] = loadedExt;
            result.Success = true;
            result.Extension = loadedExt;

            if (enableHotReload)
            {
                WatchExtensionDirectory(extensionDirectory, manifest.Id);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to initialize extension: {ex.Message}";
            await loadedExt.UnloadAsync();
        }

        ExtensionLoaded?.Invoke(result);
        return result;
    }

    public async Task<bool> UnloadExtensionAsync(string extensionId)
    {
        if (string.IsNullOrWhiteSpace(extensionId)) return false;

        if (_loadedExtensions.TryRemove(extensionId, out var loaded))
        {
            await loaded.UnloadAsync();
            ExtensionUnloaded?.Invoke(extensionId);
            return true;
        }

        return false;
    }

    public async Task<ExtensionLoadResult> ReloadExtensionAsync(string extensionId, CancellationToken ct = default)
    {
        if (_loadedExtensions.TryGetValue(extensionId, out var loaded))
        {
            string dir = loaded.ExtensionDirectory;
            return await LoadExtensionAsync(dir, enableHotReload: true, ct);
        }

        return new ExtensionLoadResult
        {
            ExtensionId = extensionId,
            Success = false,
            ErrorMessage = $"Extension '{extensionId}' is not currently loaded."
        };
    }

    private void WatchExtensionDirectory(string directory, string extensionId)
    {
        if (_watchers.ContainsKey(extensionId)) return;

        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            void OnChanged(object sender, FileSystemEventArgs e)
            {
                if (e.Name?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true ||
                    e.Name?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    DebounceReload(extensionId);
                }
            }

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;

            _watchers[extensionId] = watcher;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExtensionManager] Failed to watch {directory}: {ex.Message}");
        }
    }

    private void DebounceReload(string extensionId)
    {
        lock (_lock)
        {
            if (_debounceTimers.TryRemove(extensionId, out var existing))
            {
                existing.Dispose();
            }

            var timer = new System.Threading.Timer(async _ =>
            {
                await ReloadExtensionAsync(extensionId);
            }, null, 300, Timeout.Infinite);

            _debounceTimers[extensionId] = timer;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();

            foreach (var timer in _debounceTimers.Values)
            {
                timer.Dispose();
            }
            _debounceTimers.Clear();

            foreach (var ext in _loadedExtensions.Values)
            {
                _ = ext.UnloadAsync();
            }
            _loadedExtensions.Clear();
        }
    }
}
