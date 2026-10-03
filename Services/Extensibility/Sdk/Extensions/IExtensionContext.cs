using System;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Context passed to IExtensionEntryPoint on initialization.
/// Provides access to the ambient StudioApp, extension folder, manifest, and lifecycle disposal tracking.
/// </summary>
public interface IExtensionContext
{
    /// <summary>Root studio facade.</summary>
    IStudioApp App { get; }

    /// <summary>Root folder containing the extension files.</summary>
    string ExtensionDirectory { get; }

    /// <summary>Parsed extension manifest.</summary>
    ExtensionManifest Manifest { get; }

    /// <summary>State store isolated to this extension.</summary>
    IStateBag State { get; }

    /// <summary>Tracks disposables to automatically clean up on extension unload.</summary>
    void TrackDisposable(IDisposable disposable);

    /// <summary>Reads an extension setting value, falling back to default.</summary>
    T? GetSetting<T>(string key);

    /// <summary>Saves an extension setting value.</summary>
    void SetSetting<T>(string key, T value);
}
