using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class LocalBlindProgressService : IBlindProgressService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<int> _solved = new();
    private readonly HashSet<int> _bookmarked = new();
    private bool _loaded;

    public event Action<int, bool>? SolvedStatusChanged;
    public event Action<int, bool>? BookmarkStatusChanged;

    private class ProgressState
    {
        public List<int> Solved { get; set; } = new();
        public List<int> Bookmarked { get; set; } = new();
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    public LocalBlindProgressService(string? customBaseDir = null)
    {
        var baseDir = !string.IsNullOrEmpty(customBaseDir)
            ? customBaseDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FryPDF",
                "Plugins",
                "com.frypdf.plugin.csharpeditor");

        Directory.CreateDirectory(baseDir);
        _filePath = Path.Combine(baseDir, "blind75_progress.json");
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;
            var state = await ReadStateAsync();
            if (state != null)
            {
                _solved.Clear();
                foreach (var num in state.Solved) _solved.Add(num);

                _bookmarked.Clear();
                foreach (var num in state.Bookmarked) _bookmarked.Add(num);
            }
            _loaded = true;
        }
        catch
        {
            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlySet<int>> GetSolvedProblemNumbersAsync()
    {
        await EnsureLoadedAsync();
        lock (_solved)
        {
            return new HashSet<int>(_solved);
        }
    }

    public async Task<IReadOnlySet<int>> GetBookmarkedProblemNumbersAsync()
    {
        await EnsureLoadedAsync();
        lock (_bookmarked)
        {
            return new HashSet<int>(_bookmarked);
        }
    }

    public bool IsProblemSolved(int problemNumber)
    {
        lock (_solved)
        {
            return _solved.Contains(problemNumber);
        }
    }

    public bool IsProblemBookmarked(int problemNumber)
    {
        lock (_bookmarked)
        {
            return _bookmarked.Contains(problemNumber);
        }
    }

    public async Task SetProblemSolvedAsync(int problemNumber, bool isSolved)
    {
        await EnsureLoadedAsync();
        bool changed;
        lock (_solved)
        {
            changed = isSolved ? _solved.Add(problemNumber) : _solved.Remove(problemNumber);
        }

        if (changed)
        {
            SolvedStatusChanged?.Invoke(problemNumber, isSolved);
            await SaveAsync(state => Apply(state.Solved, problemNumber, isSolved));
        }
    }

    public async Task SetProblemBookmarkedAsync(int problemNumber, bool isBookmarked)
    {
        await EnsureLoadedAsync();
        bool changed;
        lock (_bookmarked)
        {
            changed = isBookmarked ? _bookmarked.Add(problemNumber) : _bookmarked.Remove(problemNumber);
        }

        if (changed)
        {
            BookmarkStatusChanged?.Invoke(problemNumber, isBookmarked);
            await SaveAsync(state => Apply(state.Bookmarked, problemNumber, isBookmarked));
        }
    }

    // Writes one change on top of what the file holds now, not this instance's whole memory: another instance or
    // process (the FryPDF host and the standalone Runner share this file) may have saved since this one loaded, and
    // rewriting everything from memory would undo its changes.
    private async Task SaveAsync(Action<ProgressState> applyChange)
    {
        await _lock.WaitAsync();
        try
        {
            var state = await ReadStateAsync() ?? SnapshotInMemory();
            applyChange(state);
            state.LastUpdatedUtc = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch
        {
            // Best effort persistence
        }
        finally
        {
            _lock.Release();
        }
    }

    // Null when there's no file yet or it can't be read; callers then fall back to what this instance holds, so a save
    // is never dropped because the file was momentarily unreadable.
    private async Task<ProgressState?> ReadStateAsync()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<ProgressState>(await File.ReadAllTextAsync(_filePath));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private ProgressState SnapshotInMemory()
    {
        lock (_solved)
        {
            lock (_bookmarked)
            {
                return new ProgressState { Solved = new List<int>(_solved), Bookmarked = new List<int>(_bookmarked) };
            }
        }
    }

    private static void Apply(List<int> numbers, int problemNumber, bool include)
    {
        numbers.RemoveAll(n => n == problemNumber);
        if (include) numbers.Add(problemNumber);
    }
}
