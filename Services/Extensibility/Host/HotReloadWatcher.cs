using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;

/// <summary>
/// Monitors customization scripts and extension directories on disk.
/// Automatically triggers debounced hot-reload re-compilation when files change.
/// </summary>
public class HotReloadWatcher : IDisposable
{
    private readonly string _targetFilePath;
    private readonly Func<string, Task> _reloadCallback;
    private readonly FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _lock = new();
    private bool _isDisposed;

    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;

    public HotReloadWatcher(string targetFilePath, Func<string, Task> reloadCallback, int debounceMs = 300)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);
        _targetFilePath = Path.GetFullPath(targetFilePath);
        _reloadCallback = reloadCallback ?? throw new ArgumentNullException(nameof(reloadCallback));

        string? dir = Path.GetDirectoryName(_targetFilePath);
        string fileName = Path.GetFileName(_targetFilePath);

        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            _watcher = new FileSystemWatcher(dir, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (_, _) => DebounceTrigger(debounceMs);
            _watcher.Created += (_, _) => DebounceTrigger(debounceMs);
        }
    }

    private void DebounceTrigger(int debounceMs)
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(async _ =>
            {
                await TriggerReloadAsync();
            }, null, debounceMs, Timeout.Infinite);
        }
    }

    public async Task TriggerReloadAsync()
    {
        try
        {
            if (!File.Exists(_targetFilePath)) return;

            // Wait briefly for file write lock to clear if editor is still flushing
            string content = string.Empty;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    content = await File.ReadAllTextAsync(_targetFilePath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(50);
                }
            }

            if (!string.IsNullOrEmpty(content))
            {
                await _reloadCallback(content);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HotReloadWatcher] Error during hot reload: {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _debounceTimer?.Dispose();
            _debounceTimer = null;

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }
    }
}
