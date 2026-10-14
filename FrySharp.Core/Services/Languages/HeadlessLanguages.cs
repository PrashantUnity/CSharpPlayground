using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// The languages a host without the desktop runs (the web server, the CLI): each one whose engine lives in Core. A
/// language joins them when its engine moves here from the desktop plugin.
/// </summary>
public static class HeadlessLanguages
{
    /// <summary>
    /// The files that run as C#. The C# engine's own extension is <c>.frycs</c>, a FrySharp document (JSON); a headless host
    /// runs plain C# files.
    /// </summary>
    public static IReadOnlySet<string> CSharpSourceExtensions { get; } =
        new HashSet<string>([".cs", ".csx"], StringComparer.OrdinalIgnoreCase);

    public static LanguageRegistry<ILanguageEngine> Create(LanguageEngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var registry = new LanguageRegistry<ILanguageEngine>();
        registry.Register(new CSharpEngine());
        registry.Register(new PythonEngine(services));
        registry.Register(new JavaScriptEngine(services));
        registry.Register(new SqlEngine(services));
        registry.Register(new FSharpEngine(services));
        registry.Register(new JavaEngine(services));
        registry.Register(new RustEngine(services));
        registry.Register(new GoEngine(services));
        registry.Register(new CppEngine(services));
        return registry;
    }

    /// <summary>The language a file runs as, or null when none of <paramref name="languages"/> runs it.</summary>
    public static ILanguageEngine? LanguageOf(this LanguageRegistry<ILanguageEngine> languages, string path) =>
        CSharpSourceExtensions.Contains(Path.GetExtension(path))
            ? languages.Get(LanguageIds.CSharp)
            : languages.FindSourceFileLanguage(path);
}
