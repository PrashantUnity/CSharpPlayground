using System.Diagnostics;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Settings;

/// <summary>
/// The user's studio preferences, kept in <c>studio_settings.json</c>.
/// <list type="bullet">
/// <item><see cref="Update"/> changes the settings in place (read-modify-write, so a change to one setting can never
/// reset another) and writes them a moment later, off the calling thread; many changes in a row are one write.</item>
/// <item><see cref="SaveSettings"/> replaces them and writes at once (kept for callers that need it on disk now).</item>
/// <item>Writes are atomic (a temporary file, then a rename): a crash or a full disk never leaves half a file. A file
/// that can't be read is kept aside as <c>studio_settings.corrupt-…json</c> instead of being overwritten.</item>
/// </list>
/// </summary>
public sealed class StudioSettingsStore
{
    /// <summary>How long after the last <see cref="Update"/> the file is written.</summary>
    public static TimeSpan WriteDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly object _writeGate = new();
    private StudioSettings? _settings;
    private bool _dirty;
    private ITimer? _writeTimer;

    public StudioSettingsStore(string filePath, TimeProvider? time = null)
    {
        _filePath = filePath;
        _time = time ?? TimeProvider.System;
    }

    public string FilePath => _filePath;

    /// <summary>How many times the file was written (tests check that changes in a row are one write).</summary>
    public int WriteCount { get; private set; }

    /// <summary>Whether changes are waiting to be written.</summary>
    public bool HasUnsavedChanges
    {
        get
        {
            lock (_gate) return _dirty;
        }
    }

    public StudioSettings GetSettings()
    {
        lock (_gate)
        {
            return Load().Clone();
        }
    }

    /// <summary>Raised after every change, with a copy of the settings, on the thread that made the change.</summary>
    public event Action<StudioSettings>? SettingsChanged;

    /// <summary>Raised after the settings reached the disk (the "Saved" indicator), on a background thread.</summary>
    public event Action? Saved;

    /// <summary>Changes the settings in place and writes them shortly (see <see cref="WriteDelay"/>).</summary>
    public void Update(Action<StudioSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        StudioSettings copy;
        lock (_gate)
        {
            var settings = Load();
            change(settings);
            _dirty = true;
            copy = settings.Clone();
            _writeTimer ??= _time.CreateTimer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _writeTimer.Change(WriteDelay, Timeout.InfiniteTimeSpan);
        }

        SettingsChanged?.Invoke(copy);
    }

    /// <summary>Replaces the settings and writes them now.</summary>
    public void SaveSettings(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        StudioSettings copy;
        lock (_gate)
        {
            _settings = settings.Clone();
            _dirty = true;
            copy = _settings.Clone();
        }

        Flush();
        SettingsChanged?.Invoke(copy);
    }

    /// <summary>Writes pending changes now (the studio closing, a test reading the file).</summary>
    public void Flush()
    {
        lock (_writeGate)
        {
            string json;
            lock (_gate)
            {
                if (!_dirty || _settings == null) return;
                json = JsonSerializer.Serialize(_settings, JsonOptions);
                _dirty = false;
                _writeTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            if (WriteAtomically(_filePath, json))
            {
                WriteCount++;
                Saved?.Invoke();
            }
            else
            {
                lock (_gate) _dirty = true;
            }
        }
    }

    private StudioSettings Load()
    {
        if (_settings != null) return _settings;
        try
        {
            if (File.Exists(_filePath))
            {
                var text = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<StudioSettings>(text);
                if (loaded != null)
                {
                    loaded.Appearance ??= new AppearanceSettings();
                    loaded.Appearance.Harmony ??= new HarmonyStudioSettings();
                    loaded.Appearance.TokenOverrides ??= new Dictionary<string, string>();
                    loaded.Ai ??= new Models.AI.AiSettings();
                    _settings = loaded;
                    return _settings;
                }
            }
        }
        catch (JsonException ex)
        {
            KeepCorruptFileAside(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Couldn't read studio settings '{_filePath}': {ex.Message}");
        }

        _settings = new StudioSettings();
        return _settings;
    }

    // An unreadable file is the user's settings all the same: keep it next to the new one rather than overwrite it.
    private void KeepCorruptFileAside(Exception ex)
    {
        try
        {
            var aside = Path.Combine(Path.GetDirectoryName(_filePath) ?? ".",
                $"{Path.GetFileNameWithoutExtension(_filePath)}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(_filePath)}");
            File.Move(_filePath, aside, overwrite: true);
            Debug.WriteLine($"[CSharpEditorPlugin] Studio settings '{_filePath}' could not be read ({ex.Message}); kept as '{aside}', using defaults.");
        }
        catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Couldn't keep unreadable settings aside: {moveEx.Message}");
        }
    }

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/> through a temporary file and a rename.</summary>
    internal static bool WriteAtomically(string path, string text)
    {
        var temp = path + ".tmp";
        try
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Couldn't save '{path}': {ex.Message}");
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
            return false;
        }
    }
}
