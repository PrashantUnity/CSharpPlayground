using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Entry point contract implemented by multi-file extensions.
/// </summary>
public interface IExtensionEntryPoint
{
    /// <summary>Invoked when the extension is activated into the studio.</summary>
    Task InitializeAsync(IExtensionContext context);

    /// <summary>Invoked when the extension is unloaded or reloaded.</summary>
    Task DeactivateAsync();
}
