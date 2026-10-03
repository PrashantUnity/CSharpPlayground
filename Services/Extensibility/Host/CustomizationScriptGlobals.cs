using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;

/// <summary>
/// Ambient globals available inside user customization scripts.
/// Enables direct access to 'App' and 'Studio' as top-level globals.
/// </summary>
public static class CustomizationScriptGlobals
{
    /// <summary>Root IDE facade.</summary>
    public static IStudioApp App => StudioAppContext.Instance;

    /// <summary>Alias for App.</summary>
    public static IStudioApp Studio => StudioAppContext.Instance;
}
