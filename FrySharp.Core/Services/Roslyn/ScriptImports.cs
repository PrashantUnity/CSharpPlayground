namespace PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

/// <summary>
/// The namespaces user C# sees without a using directive, the same everywhere it runs: a script, a program with a Main,
/// a notebook cell and a watch expression in the debugger. Code that compiles in one of them compiles in all of them.
/// They come from the host's <see cref="ScriptHostProfile"/>, read once.
/// </summary>
public static class ScriptImports
{
    private static readonly Lazy<string> GlobalUsings = new(() => string.Join("\n",
        Namespaces.Select(name => $"global using {name};")
            .Concat(StaticTypes.Select(type => $"global using static {type};"))));

    private static readonly Lazy<IReadOnlyList<string>> ScriptingImportList = new(() => [.. Namespaces, .. StaticTypes]);

    /// <summary>The imported namespaces.</summary>
    public static IReadOnlyList<string> Namespaces => ScriptHost.Profile.Namespaces;

    /// <summary>Types imported with <c>using static</c>, e.g. <c>Check(...)</c> on the desktop.</summary>
    public static IReadOnlyList<string> StaticTypes => ScriptHost.Profile.StaticTypes;

    /// <summary>
    /// The imports as <c>global using</c> directives, for code compiled as a file. A user's own <c>using System;</c> then
    /// repeats a global using, which the compiler only notes (CS8933), instead of warning about a duplicate (CS0105).
    /// </summary>
    public static string GlobalUsingDirectives => GlobalUsings.Value;

    /// <summary>The imports for Roslyn scripting (<c>ScriptOptions.WithImports</c>), where a type name is a static import.</summary>
    public static IReadOnlyList<string> ScriptingImports => ScriptingImportList.Value;
}
