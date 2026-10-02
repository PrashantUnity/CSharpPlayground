using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Storage;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;

/// <summary>
/// High-level coordinator managing startup initialization, live hot-reloading,
/// and in-memory execution of C# customization scripts and themes.
/// </summary>
public class CustomizationManager : IDisposable
{
    private readonly CustomizationStorageService _storage;
    private readonly ScriptCustomizationSession _session;
    private HotReloadWatcher? _watcher;
    private readonly object _lock = new();

    public CustomizationStorageService Storage => _storage;
    public ScriptCustomizationSession Session => _session;
    public bool IsWatching => _watcher?.IsWatching == true;

    public event Action<CustomizationExecutionResult>? CustomizationApplied;

    public CustomizationManager(string? customFolder = null)
    {
        _storage = new CustomizationStorageService(customFolder);
        _session = new ScriptCustomizationSession();
    }

    /// <summary>
    /// Initializes global customization script, applies it if present, and starts hot-reload watching.
    /// </summary>
    public async Task InitializeAsync(bool enableHotReload = true, CancellationToken ct = default)
    {
        string initScript = await _storage.EnsureInitScriptExistsAsync();

        if (!string.IsNullOrWhiteSpace(initScript))
        {
            var result = await _session.ApplyScriptAsync(initScript, ct);
            CustomizationApplied?.Invoke(result);
        }

        if (enableHotReload)
        {
            StartWatcher();
        }
    }

    /// <summary>
    /// Re-reads and applies the global init script live.
    /// </summary>
    public async Task<CustomizationExecutionResult> ReloadAsync(CancellationToken ct = default)
    {
        string? script = await _storage.ReadInitScriptAsync();
        if (string.IsNullOrWhiteSpace(script))
        {
            return new CustomizationExecutionResult { Success = true };
        }

        var result = await _session.ApplyScriptAsync(script, ct);
        CustomizationApplied?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Evaluates an in-memory C# customization script (e.g. from an active editor tab).
    /// </summary>
    public async Task<CustomizationExecutionResult> ApplyCodeAsync(string sourceCode, CancellationToken ct = default)
    {
        var result = await _session.ApplyScriptAsync(sourceCode, ct);
        CustomizationApplied?.Invoke(result);
        return result;
    }

    private HotReloadWatcher? _workspaceWatcher;

    /// <summary>
    /// Loads workspace-level .frysharp/init.csx and starts watching it for hot reload if present.
    /// </summary>
    public async Task<CustomizationExecutionResult?> LoadWorkspaceCustomizationsAsync(string workspaceFolder, bool enableHotReload = true, CancellationToken ct = default)
    {
        _storage.WorkspaceFolder = workspaceFolder;
        string? script = await _storage.ReadWorkspaceInitScriptAsync();
        if (!string.IsNullOrWhiteSpace(script))
        {
            var result = await _session.ApplyScriptAsync(script, ct);
            CustomizationApplied?.Invoke(result);

            if (enableHotReload && _storage.WorkspaceInitScriptPath != null)
            {
                lock (_lock)
                {
                    _workspaceWatcher?.Dispose();
                    _workspaceWatcher = new HotReloadWatcher(_storage.WorkspaceInitScriptPath, async updatedCode =>
                    {
                        var res = await _session.ApplyScriptAsync(updatedCode);
                        CustomizationApplied?.Invoke(res);
                    });
                }
            }
            return result;
        }
        return null;
    }

    private void StartWatcher()
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _watcher = new HotReloadWatcher(_storage.GlobalInitScriptPath, async updatedCode =>
            {
                var result = await _session.ApplyScriptAsync(updatedCode);
                CustomizationApplied?.Invoke(result);
            });
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _watcher = null;
            _workspaceWatcher?.Dispose();
            _workspaceWatcher = null;
            _session.Dispose();
        }
    }
}
