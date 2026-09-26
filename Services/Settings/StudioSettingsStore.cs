using System.Diagnostics;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Settings;

/// <summary>
/// Thread-safe storage service for user IDE preferences, serialized to <c>studio_settings.json</c>.
/// </summary>
public sealed class StudioSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly object _gate = new();
    private StudioSettings? _settings;

    public StudioSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public StudioSettings GetSettings()
    {
        lock (_gate)
        {
            return Load().Clone();
        }
    }

    public void SaveSettings(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            _settings = settings.Clone();
            SaveInternal(_settings);
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
                    _settings = loaded;
                    return _settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Ignoring unreadable studio settings '{_filePath}': {ex.Message}");
        }

        _settings = new StudioSettings();
        return _settings;
    }

    private void SaveInternal(StudioSettings settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Couldn't save studio settings '{_filePath}': {ex.Message}");
        }
    }
}
