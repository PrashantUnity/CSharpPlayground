using System.Diagnostics;
using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

/// <summary>
/// The toolchain the user picked for each language, in <c>toolchains.json</c>. Nothing is read or written until it's
/// used, so creating one never touches the disk.
/// </summary>
public sealed class ToolchainSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly object _gate = new();
    private Dictionary<string, LanguageSettings>? _settings;

    private sealed class LanguageSettings
    {
        public string? SelectedPath { get; set; }
    }

    public ToolchainSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public string? GetSelectedPath(string languageId)
    {
        lock (_gate)
        {
            return Load().TryGetValue(languageId, out var settings) ? settings.SelectedPath : null;
        }
    }

    public void SetSelectedPath(string languageId, string? path)
    {
        lock (_gate)
        {
            var all = Load();
            if (!all.TryGetValue(languageId, out var settings))
            {
                settings = new LanguageSettings();
                all[languageId] = settings;
            }

            settings.SelectedPath = string.IsNullOrWhiteSpace(path) ? null : path;
            Save(all);
        }
    }

    private Dictionary<string, LanguageSettings> Load()
    {
        if (_settings != null) return _settings;
        _settings = new Dictionary<string, LanguageSettings>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(_filePath))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, LanguageSettings>>(File.ReadAllText(_filePath));
                if (loaded != null)
                {
                    foreach (var (key, value) in loaded) _settings[key] = value;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Ignoring unreadable toolchain settings '{_filePath}': {ex.Message}");
        }

        return _settings;
    }

    private void Save(Dictionary<string, LanguageSettings> settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Couldn't save toolchain settings '{_filePath}': {ex.Message}");
        }
    }
}
