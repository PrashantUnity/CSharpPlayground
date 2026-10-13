using PdfEditorApp.Plugins.CSharpEditor.Services.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

/// <summary>
/// The desktop studio's <see cref="ScriptHostProfile"/>: scripts see <see cref="Display"/>, the charts, visualizers and
/// customization API, and the Avalonia types that <c>Display.Control</c> and <c>Display.Animate</c> take. Every desktop
/// entry point (the FryPDF plugin, the standalone Runner, UiSnapshots and the tests) calls <see cref="Configure"/> before
/// anything compiles.
/// </summary>
public static class DesktopScriptHost
{
    public static ScriptHostProfile Profile { get; } = new(
        "desktop",
        [
            typeof(Display.Display).Assembly,               // The plugin: Display, Dump, ScriptHelpers, charts
            typeof(ScriptHostProfile).Assembly,             // FrySharp.Core
            typeof(Avalonia.AvaloniaObject).Assembly,       // Avalonia.Base
            typeof(Avalonia.Controls.Control).Assembly,     // Avalonia.Controls
            typeof(Avalonia.Media.Imaging.Bitmap).Assembly, // Avalonia media types
            typeof(FrySharp.Sdk.IStudioApp).Assembly,       // FrySharp.Sdk (customization scripts)
        ],
        [
            .. ScriptHostProfile.SystemNamespaces,

            // For Display.Control(...) and the drawing callbacks of Display.Animate(...).
            "Avalonia.Animation",
            "Avalonia.Controls",
            "Avalonia.Media",
            "Avalonia.Media.Immutable", // ImmutableSolidColorBrush and ImmutablePen: drawing resources a script can make off the UI thread
            "Avalonia.Media.Imaging",
            "Avalonia.Threading",

            "PdfEditorApp.Plugins.CSharpEditor.Services",
            "PdfEditorApp.Plugins.CSharpEditor.Services.Display",
            "PdfEditorApp.Plugins.CSharpEditor.Services.Execution",
            "PdfEditorApp.Plugins.CSharpEditor.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec",
            "PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction",
            "PdfEditorApp.Plugins.CSharpEditor.Charting.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Charting.Services",
            "PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services",
            "PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial",
            "PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts",
            "PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services",
            "FrySharp.Sdk",
            "PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility",
        ],
        [
            // Check(...), Show(...) and Format(...) need no prefix.
            "PdfEditorApp.Plugins.CSharpEditor.Services.ScriptHelpers",
            "PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host.CustomizationScriptGlobals",
        ],
        new DesktopScriptHostDisplay());

    /// <summary>Makes scripts compile against the desktop profile. Safe to call from every entry point.</summary>
    public static void Configure() => ScriptHost.Configure(Profile);
}
