using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// The tests exercise the desktop studio, so scripts compile against the desktop profile, as they do in the app. It is set
/// when the test assembly loads: before any test, whichever runs first.
/// </summary>
internal static class DesktopScriptHostSetup
{
    [ModuleInitializer]
    internal static void Configure() => DesktopScriptHost.Configure();
}
