using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Go, compiled with the official Go toolchain (go build / go run) and executed natively for source files,
/// supporting two-phase compilation, structured compiler diagnostics in Problems, interactive standard input,
/// and interactive polyglot notebook cells.
/// </summary>
public sealed class GoLanguage : LanguageDefinition
{
    private static readonly string[] GoAliases = ["go", "golang"];
    private static readonly string[] GoExtensions = [".go"];

    public GoLanguage(StudioLanguageServices services)
    {
        var toolchain = new GoToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings,
            Path.Combine(services.BaseDirectory, "go"));
        GoToolchain = toolchain;
        ScriptRunner = new GoBuildAndRunScriptRunner(services.Host);
        Packages = new GoPackageManager(toolchain, services.Processes, services.Host);
        Debugger = new GoDebuggerProvider(toolchain, services.Processes, services.Host, services.AdapterManager);
        NotebookKernels = new DelegateKernelFactory(context =>
            new GoNotebookKernel(toolchain, services.Processes, services.Host, context));
    }

    public GoToolchainProvider GoToolchain { get; }

    public override string Id => LanguageIds.Go;
    public override string DisplayName => "Go";
    public override JupyterLanguageInfo Jupyter { get; } = new("go", "Go", "go", "text/x-gosrc", ".go", "text/x-gosrc");
    public override string ShortName => "GO";
    public override IReadOnlyList<string> Aliases => GoAliases;
    public override IReadOnlyList<string> FileExtensions => GoExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public override bool IsCompiled => true;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput |
        LanguageCapabilities.QuickInfo |
        LanguageCapabilities.Completion |
        LanguageCapabilities.Debugging |
        LanguageCapabilities.Breakpoints |
        LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing |
        LanguageCapabilities.Packages;

    public override string IconKind => "LanguageGo";
    public override string AccentHex => "#00ADD8";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "Go (gc / go toolchain)";

    public override string NewFileTemplate =>
        "// Runs with the Go toolchain installed on this computer (F5).\n" +
        "package main\n\n" +
        "import \"fmt\"\n\n" +
        "func main() {\n" +
        "    fmt.Println(\"Hello from Go!\")\n" +
        "}\n";

    public override IEditorAssistantFactory EditorAssistants { get; } = GoEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => GoSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new GoIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new GoFoldingStrategy();

    public override IToolchainProvider Toolchain => GoToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDebuggerProvider? Debugger { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new GoCompilerDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
