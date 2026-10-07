using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// The languages the studio runs and what they share: the registry, the machine they're found on, the process
/// launcher, the saved toolchain choices and the plugin's data folder. The host creates one and hands it to both
/// studios and the Hub. Creating one touches nothing: folders are made and programs are looked for only when a
/// language first needs them, so tests can build view models without it reaching the user's data.
/// </summary>
public sealed class StudioLanguageServices
{
    private static readonly Lazy<StudioLanguageServices> LazyDefault = new(() => new StudioLanguageServices());

    /// <summary>The services over the real plugin data folder, for view models created without the host's instance.</summary>
    public static StudioLanguageServices Default => LazyDefault.Value;

    /// <param name="baseDirectory">Where toolchain settings, studio environments and kernel files live (tests pass a temp folder).</param>
    /// <param name="configure">Registers more languages, e.g. a test's fake one.</param>
    /// <param name="registerBuiltInLanguages">False for a registry with only what <paramref name="configure"/> adds.</param>
    public StudioLanguageServices(
        string? baseDirectory = null,
        IHostEnvironment? host = null,
        IProcessLauncher? processLauncher = null,
        Action<StudioLanguageServices, LanguageRegistry>? configure = null,
        bool registerBuiltInLanguages = true)
    {
        BaseDirectory = string.IsNullOrWhiteSpace(baseDirectory) ? DefaultBaseDirectory() : baseDirectory;
        Host = host ?? new HostEnvironment();
        Processes = processLauncher ?? new ProcessLauncher();
        ToolchainSettings = new ToolchainSettingsStore(Path.Combine(BaseDirectory, "toolchains.json"));
        StudioSettings = new Settings.StudioSettingsStore(Path.Combine(BaseDirectory, "studio_settings.json"));
        ThemeLibrary = new Extensibility.Theming.ThemeLibraryStore(Path.Combine(BaseDirectory, "themes"));
        LayoutLibrary = new Extensibility.Theming.Layout.LayoutLibraryStore(Path.Combine(BaseDirectory, "layouts"));
        Registry = new LanguageRegistry();

        AdapterManager = new Debugging.Dap.DapAdapterManager(Processes, Host);

        if (registerBuiltInLanguages)
        {
            var roslynCompiler = new RoslynCompilerService();
            var execEngine = new ScriptExecutionEngine();
            var scriptDebugger = new ScriptDebuggerService(roslynCompiler, execEngine);
            var csharpDebugger = new Debugging.CSharpDebuggerProvider(
                roslynCompiler,
                scriptDebugger,
                execEngine,
                Processes,
                Host,
                Path.Combine(BaseDirectory, "csharp_debug"),
                AdapterManager);

            Registry.Register(new CSharpLanguage(csharpDebugger));
            Registry.Register(new PythonLanguage(this));
            Registry.Register(new JavaScriptLanguage(this));
            Registry.Register(new JavaLanguage(this));
            Registry.Register(new CppLanguage(this));
            Registry.Register(new GoLanguage(this));
            Registry.Register(new FSharpLanguage(this));
            Registry.Register(new SqlLanguage(this));
            Registry.Register(new RustLanguage(this));
            Registry.Register(new Text.TextLanguage());
        }

        configure?.Invoke(this, Registry);
    }

    public string BaseDirectory { get; }
    public IHostEnvironment Host { get; }
    public IProcessLauncher Processes { get; }
    public Debugging.Dap.DapAdapterManager AdapterManager { get; }
    public ToolchainSettingsStore ToolchainSettings { get; }
    public Settings.StudioSettingsStore StudioSettings { get; }

    /// <summary>The user's saved themes (as many as they like, each named), one file each under <c>themes/</c>.</summary>
    public Extensibility.Theming.ThemeLibraryStore ThemeLibrary { get; }

    /// <summary>The user's saved layouts (Settings → Layout &amp; Typography).</summary>
    public Extensibility.Theming.Layout.LayoutLibraryStore LayoutLibrary { get; }
    public LanguageRegistry Registry { get; }

    public ILanguageDefinition CSharp => Registry.Get(LanguageIds.CSharp) ?? throw new InvalidOperationException("C# isn't registered.");

    /// <summary>The language of a document; C# when it names none the registry knows.</summary>
    public ILanguageDefinition LanguageOf(ScriptDocumentItem document)
    {
        if (!string.IsNullOrEmpty(document.LanguageId) && Registry.Get(document.LanguageId) is { } lang)
            return lang;

        if (document.SourceFilePath != null && Registry.FindSourceFileLanguage(document.SourceFilePath) is { } sourceLang)
            return sourceLang;

        if (document.SourceFilePath != null && Registry.Get(LanguageIds.Text) is { } textLang)
            return textLang;

        return CSharp;
    }

    /// <summary>The plugin's own data folder, e.g. ~/Library/Application Support/FryPDF/Plugins/com.frypdf.plugin.csharpeditor.</summary>
    public static string DefaultBaseDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FryPDF",
        "Plugins",
        "com.frypdf.plugin.csharpeditor");
}
