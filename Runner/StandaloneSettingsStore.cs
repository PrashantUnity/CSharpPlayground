using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PdfEditorApp.Core.Plugins.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

/// <summary>
/// Standalone file-backed implementation of IPluginSettingsStore for local runner testing.
/// Persists preferences to csharpeditor.runner.settings.json without requiring FryPDF or any host services.
/// </summary>
public class StandaloneSettingsStore : IPluginSettingsStore
{
    private readonly string _filePath;
    private readonly Dictionary<string, Dictionary<string, object>> _data;
    private readonly object _lock = new();

    public StandaloneSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "csharpeditor.runner.settings.json");
        _data = LoadFromFile();
    }

    private Dictionary<string, Dictionary<string, object>> LoadFromFile()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json) ?? new();
            }
        }
        catch
        {
            // Ignore corrupted disk cache and start fresh
        }
        return new();
    }

    public T GetSetting<T>(string pluginId, string key, T defaultValue)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(pluginId, out var dict) && dict.TryGetValue(key, out var val))
            {
                if (val is JsonElement elem)
                {
                    return JsonSerializer.Deserialize<T>(elem.GetRawText()) ?? defaultValue;
                }
                if (val is T typedVal)
                {
                    return typedVal;
                }
            }
            return defaultValue;
        }
    }

    public void SetSetting<T>(string pluginId, string key, T value)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(pluginId, out var dict))
            {
                dict = new();
                _data[pluginId] = dict;
            }
            dict[key] = value!;
            Save();
        }
    }

    public Dictionary<string, object> GetPluginSettings(string pluginId)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(pluginId, out var dict))
            {
                return new(dict);
            }
            return new();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Silently ignore disk errors in runner
            }
        }
    }
}

/// <summary>
/// Minimal IServiceProvider supplying the standalone settings store to the ViewModel.
/// </summary>
public class StandaloneServiceProvider : IServiceProvider
{
    private readonly IPluginSettingsStore _settingsStore;

    public StandaloneServiceProvider(IPluginSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IPluginSettingsStore))
        {
            return _settingsStore;
        }
        return null;
    }
}
