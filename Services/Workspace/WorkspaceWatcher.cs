using System.Diagnostics;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

/// <summary>
/// Watches a workspace folder and reports, once things have settled, that files under it changed behind the studio's back
/// (git, another editor, a build). Without it the studio would only notice on a rescan, and rescanning the whole workspace
/// on every tab switch is exactly what it no longer does.
/// </summary>
internal sealed class WorkspaceWatcher : IDisposable
{
    // A git checkout or a build writes hundreds of files in a burst: report once, after it has gone quiet.
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

    private readonly string _root;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _settleTimer;
    private readonly Action<bool> _changed;
    private int _structural;

    private WorkspaceWatcher(string root, FileSystemWatcher watcher, Action<bool> changed)
    {
        _root = root;
        _watcher = watcher;
        _changed = changed;
        _settleTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);

        watcher.Created += (_, e) => Report(e.FullPath, structural: true);
        watcher.Deleted += (_, e) => Report(e.FullPath, structural: true);
        watcher.Renamed += (_, e) =>
        {
            Report(e.OldFullPath, structural: true);
            Report(e.FullPath, structural: true);
        };
        watcher.Changed += (_, e) => Report(e.FullPath, structural: false);
        // Events were lost (the operating system's buffer overflowed): anything may have changed.
        watcher.Error += (_, _) => Report(null, structural: true);
    }

    /// <param name="changed">Called on a background thread after a burst of changes; the argument is true when something was created, deleted or renamed.</param>
    /// <returns>The watcher, or null when the folder can't be watched (it is gone, or the platform's watch limit is reached): the workspace then refreshes only when asked.</returns>
    public static WorkspaceWatcher? TryStart(string root, Action<bool> changed)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            var started = new WorkspaceWatcher(root, watcher, changed);
            watcher.EnableRaisingEvents = true;
            return started;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException or InvalidOperationException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Not watching '{root}' for changes: {ex.Message}");
            watcher?.Dispose();
            return null;
        }
    }

    private void Report(string? path, bool structural)
    {
        // Inside the folders the walk leaves out (.git, node_modules...) things change constantly; the Explorer lists them
        // only when opened (as VS Code's watcher, which ignores them too).
        if (path != null && WorkspaceWalker.IsInsideSkippedFolder(path, _root)) return;

        if (structural) Interlocked.Exchange(ref _structural, 1);
        try
        {
            _settleTimer.Change(Settle, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Stopped while an event was arriving.
        }
    }

    private void Flush() => _changed(Interlocked.Exchange(ref _structural, 0) == 1);

    public void Dispose()
    {
        _watcher.Dispose();
        _settleTimer.Dispose();
    }
}
