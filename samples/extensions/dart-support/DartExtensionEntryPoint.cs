using System.Threading.Tasks;
using FrySharp.Sdk;

namespace DartSupportExtension;

/// <summary>
/// Dynamic entry point for the Dart Language Support extension.
/// Registers the full-featured DartLanguage definition with FrySharp's language engine.
/// </summary>
public class DartExtensionEntryPoint : IExtensionEntryPoint
{
    public Task InitializeAsync(IExtensionContext context)
    {
        var dartLanguage = new DartLanguage(context);
        // Automatically tracked by the extension lifetime bag and cleanly unregistered on unload/reload
        context.TrackDisposable(context.App.Languages.Register(dartLanguage));
        return Task.CompletedTask;
    }

    public Task DeactivateAsync() => Task.CompletedTask;
}
