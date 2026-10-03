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
    private readonly IStudioApp _app;

    public ExtensionManager(IStudioApp? app = null)
    {
        _app = app ?? StudioAppContext.Instance;
    }

    public IReadOnlyList<LoadedExtension> LoadedExtensions => _loadedExtensions.Values.ToList();

    public event Action<ExtensionLoadResult>? ExtensionLoaded;
    public event Action<string>? ExtensionUnloaded;

    /// <summary>
    /// Gets all standard directories where extensions may reside, in priority order:
    /// 1. Workspace extensions (if provided): &lt;workspace&gt;/.frysharp/extensions
    /// 2. Application bundled extensions: &lt;AppBase&gt;/extensions
    /// 3. User profile global extensions: ~/.frysharp/extensions
    /// 4. Repository sample extensions (for local execution): samples/extensions
    /// </summary>
    public static IReadOnlyList<string> GetDefaultExtensionSearchDirectories(string? workspacePath = null)
    {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfValid(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var full = Path.GetFullPath(path);
                if (Directory.Exists(full) && seen.Add(full))
                {
                    dirs.Add(full);
                }
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        // Fall back to workspace service if not explicitly specified
        workspacePath ??= StudioAppContext.Instance.WorkspaceService.RootPath;

        // 1. Workspace-level extensions
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            AddIfValid(Path.Combine(workspacePath, ".frysharp", "extensions"));
            AddIfValid(Path.Combine(workspacePath, "extensions"));
            AddIfValid(Path.Combine(workspacePath, "samples", "extensions"));
            if (File.Exists(Path.Combine(workspacePath, "extension.json")))
            {
                var parent = Path.GetDirectoryName(Path.GetFullPath(workspacePath));
                if (!string.IsNullOrEmpty(parent)) AddIfValid(parent);
            }

            // Check locked git packages in .frysharp/extensions-lock.json
            string lockPath = Path.Combine(workspacePath, ".frysharp", "extensions-lock.json");
            if (File.Exists(lockPath))
            {
                try
                {
                    string json = File.ReadAllText(lockPath);
                    var lockfile = JsonSerializer.Deserialize<Packages.WorkspaceExtensionLockfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (lockfile?.Dependencies != null)
                    {
                        foreach (var pkg in lockfile.Dependencies.Values)
                        {
                            if (!string.IsNullOrEmpty(pkg.CacheDirectory))
                            {
                                AddIfValid(pkg.CacheDirectory);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        // 2. Application bundled extensions
        AddIfValid(Path.Combine(AppContext.BaseDirectory, "extensions"));

        // 3. User global extensions (~/.frysharp/extensions)
        AddIfValid(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".frysharp", "extensions"));

        // 4. Development & sample extensions (repo paths & upward directory traversal)
        string? cur = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(cur); i++)
        {
            AddIfValid(Path.Combine(cur, "samples", "extensions"));
            AddIfValid(Path.Combine(cur, "extensions"));
            cur = Path.GetDirectoryName(cur);
        }

        cur = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(cur); i++)
        {
            AddIfValid(Path.Combine(cur, "samples", "extensions"));
            AddIfValid(Path.Combine(cur, "extensions"));
            cur = Path.GetDirectoryName(cur);
        }

        return dirs;
    }

    /// <summary>
    /// Restores any Git packages declared in workspace .frysharp/extensions.json
    /// and dynamically loads them into the studio.
    /// </summary>
    public async Task<IReadOnlyList<ExtensionLoadResult>> RestoreAndLoadWorkspacePackagesAsync(
        string? workspacePath = null,
        Packages.GitPackageService? packageService = null,
        CancellationToken ct = default)
    {
        workspacePath ??= _app.Workspace.RootPath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return Array.Empty<ExtensionLoadResult>();

        packageService ??= (_app as StudioAppContext)?.GitPackageService ?? new Packages.GitPackageService();
        var resolutionResults = await packageService.RestoreWorkspacePackagesAsync(workspacePath, ct);

        var loadResults = new List<ExtensionLoadResult>();
        foreach (var res in resolutionResults)
        {
            if (res.Success && !string.IsNullOrEmpty(res.ExtensionDirectory))
            {
                var loadResult = await LoadExtensionAsync(res.ExtensionDirectory, enableHotReload: false, ct);
                loadResults.Add(loadResult);
            }
        }

        return loadResults;
    }

    /// <summary>
    /// Discovers and loads extensions from all default search directories.
    /// </summary>
    public async Task<IReadOnlyList<ExtensionLoadResult>> DiscoverAndLoadFromDefaultLocationsAsync(
        string? workspacePath = null,
        bool enableHotReload = true,
        CancellationToken ct = default)
    {
        var allResults = new List<ExtensionLoadResult>();
        var searchDirs = GetDefaultExtensionSearchDirectories(workspacePath);

        foreach (var dir in searchDirs)
        {
            var results = await DiscoverAndLoadAllAsync(dir, enableHotReload, ct);
            allResults.AddRange(results);
        }

        return allResults;
    }

    public async Task<IReadOnlyList<ExtensionLoadResult>> DiscoverAndLoadAllAsync(string extensionsRootDirectory, bool enableHotReload = true, CancellationToken ct = default)
    {
        var results = new List<ExtensionLoadResult>();

        if (!Directory.Exists(extensionsRootDirectory))
        {
            return results;
        }

        string rootManifest = Path.Combine(extensionsRootDirectory, "extension.json");
        if (File.Exists(rootManifest))
        {
            var result = await LoadExtensionAsync(extensionsRootDirectory, enableHotReload, ct);
            results.Add(result);
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

        // Ensure lingering language registrations from this extension or previous runs are cleaned up
        _app.Languages.Unregister(manifest.Id);
        if (manifest.Id.EndsWith("-support", StringComparison.OrdinalIgnoreCase))
        {
            _app.Languages.Unregister(manifest.Id[..^8]);
        }
        foreach (var lang in manifest.Languages)
        {
            _app.Languages.Unregister(lang.Id);
        }

        var csFiles = manifest.SourceFiles.Count > 0
            ? manifest.SourceFiles.Select(f => Path.IsPathRooted(f) ? f : Path.Combine(extensionDirectory, f)).Where(File.Exists).ToList()
            : Directory.GetFiles(extensionDirectory, "*.cs", SearchOption.AllDirectories).ToList();

        var alc = new ExtensionLoadContext($"Ext_{manifest.Id}");
        var regBag = new LifetimeRegistrationBag();
        var loadedExt = new LoadedExtension(_app, extensionDirectory, manifest, alc, regBag);

        // Register any declarative language contributions
        foreach (var langContrib in manifest.Languages)
        {
            var langDef = new DeclarativeLanguageDefinition(langContrib, extensionDirectory);
            var regToken = _app.Languages.Register(langDef);
            regBag.Track(regToken);
        }

        // If this is a purely declarative extension (no C# source files), activate directly without compilation
        if (csFiles.Count == 0 && manifest.Languages.Count > 0)
        {
            _loadedExtensions[manifest.Id] = loadedExt;
            result.Success = true;
            result.Extension = loadedExt;

            if (enableHotReload)
            {
                WatchExtensionDirectory(extensionDirectory, manifest.Id);
            }

            ExtensionLoaded?.Invoke(result);
            return result;
        }

        // 2. Compile directory
        var (compileSuccess, peBytes, pdbBytes, diags) = await Task.Run(() =>
        {
            var files = manifest.SourceFiles.Count > 0 ? manifest.SourceFiles : null;
            return _compiler.CompileDirectory(extensionDirectory, files);
        }, ct);

        result.Diagnostics = diags;

        if (!compileSuccess || peBytes == null)
        {
            var diagDetails = string.Join("; ", diags.Select(d => d.Message));
            result.Success = false;
            result.ErrorMessage = string.IsNullOrEmpty(diagDetails)
                ? "Compilation of extension source files failed."
                : $"Compilation failed: {diagDetails}";
            Console.Error.WriteLine($"[ExtensionManager] Failed to compile extension '{manifest.Id}': {result.ErrorMessage}");
            await loadedExt.UnloadAsync();
            ExtensionLoaded?.Invoke(result);
            return result;
        }

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
            result.ErrorMessage = $"Failed to initialize extension: {ex}";
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

            var loaded = _loadedExtensions.Values.ToList();
            _loadedExtensions.Clear();
            foreach (var ext in loaded)
            {
                try
                {
                    ext.UnloadAsync().GetAwaiter().GetResult();
                }
                catch { }
            }
        }
    }
}
