using PdfEditorApp.Plugins.CSharpEditor.Services.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

/// <summary>
/// The namespaces user C# sees without a using directive, the same everywhere it runs: a script, a program with a Main,
/// a notebook cell and a watch expression in the debugger. Code that compiles in one of them compiles in all of them.
/// The studio's own controls are not here: user code draws through <see cref="Display"/>, not by building them.
/// </summary>
public static class ScriptImports
{
    /// <summary>The imported namespaces.</summary>
    public static IReadOnlyList<string> Namespaces { get; } =
    [
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Data",
        "System.Diagnostics",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Text",
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "System.Threading.Tasks",

        // For Display.Control(...) and the drawing callbacks of Display.Animate(...).
        "Avalonia.Animation",
        "Avalonia.Controls",
        "Avalonia.Media",
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
        "PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility"
    ];

    /// <summary>Types imported with <c>using static</c>: <c>Check(...)</c>, <c>Show(...)</c> and <c>Format(...)</c> need no prefix.</summary>
    public static IReadOnlyList<string> StaticTypes { get; } =
    [
        "PdfEditorApp.Plugins.CSharpEditor.Services.ScriptHelpers",
        "PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host.CustomizationScriptGlobals"
    ];

    /// <summary>
    /// The imports as <c>global using</c> directives, for code compiled as a file. A user's own <c>using System;</c> then
    /// repeats a global using, which the compiler only notes (CS8933), instead of warning about a duplicate (CS0105).
    /// </summary>
    public static string GlobalUsingDirectives { get; } = string.Join("\n",
        Namespaces.Select(name => $"global using {name};")
            .Concat(StaticTypes.Select(type => $"global using static {type};")));

    /// <summary>The imports for Roslyn scripting (<c>ScriptOptions.WithImports</c>), where a type name is a static import.</summary>
    public static IReadOnlyList<string> ScriptingImports { get; } = [.. Namespaces, .. StaticTypes];
}
