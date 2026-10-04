using System;
using System.IO;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.State;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;

/// <summary>
/// Represents an active, loaded extension package running inside its own collectible ALC.
/// </summary>
public class LoadedExtension : IExtensionContext, IDisposable
{
    public IStudioApp App { get; }
    public string ExtensionDirectory { get; }
    public ExtensionManifest Manifest { get; }
    public IStateBag State { get; }

    internal ExtensionLoadContext LoadContext { get; }
    internal LifetimeRegistrationBag RegistrationBag { get; }
    internal IExtensionEntryPoint? EntryPoint { get; set; }

    public LoadedExtension(
        IStudioApp app,
        string extensionDirectory,
        ExtensionManifest manifest,
        ExtensionLoadContext loadContext,
        LifetimeRegistrationBag registrationBag)
    {
        App = app;
        ExtensionDirectory = extensionDirectory;
        Manifest = manifest;
        LoadContext = loadContext;
        RegistrationBag = registrationBag;
        State = new InMemoryStateBag();
    }

    public void TrackDisposable(IDisposable disposable)
    {
        RegistrationBag.Track(disposable);
    }

    public T? GetSetting<T>(string key)
    {
        string stateKey = $"ext_setting_{Manifest.Id}_{key}";
        if (State.Contains(stateKey))
        {
            return State.Get<T>(stateKey);
        }

        if (Manifest.Settings.TryGetValue(key, out var def))
        {
            if (def.DefaultValue is T val) return val;
            if (def.DefaultValue is System.Text.Json.JsonElement elem)
            {
                if (typeof(T) == typeof(string) && elem.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return (T)(object)elem.GetString()!;
                }
                if (typeof(T) == typeof(bool) && (elem.ValueKind == System.Text.Json.JsonValueKind.True || elem.ValueKind == System.Text.Json.JsonValueKind.False))
                {
                    return (T)(object)elem.GetBoolean();
                }
                if (typeof(T) == typeof(int) && elem.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return (T)(object)elem.GetInt32();
                }
                return System.Text.Json.JsonSerializer.Deserialize<T>(elem.GetRawText());
            }
        }

        return default;
    }

    public void SetSetting<T>(string key, T value)
    {
        string stateKey = $"ext_setting_{Manifest.Id}_{key}";
        State.Set(stateKey, value);
    }

    public async Task UnloadAsync()
    {
        if (EntryPoint != null)
        {
            try
            {
                await EntryPoint.DeactivateAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadedExtension] Error during DeactivateAsync in {Manifest.Id}: {ex.Message}");
            }
            EntryPoint = null;
        }

        RegistrationBag.Dispose();

        try
        {
            LoadContext.Unload();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadedExtension] Error unloading ALC for {Manifest.Id}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _ = UnloadAsync();
    }
}
