using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Go, compiled with the official Go toolchain and run natively for source files: a two-phase build and run, the compiler's messages
/// in Problems, interactive standard input, and modules added with <c>%go get</c>. Notebook cells are built as small programs and run, with
/// what earlier cells declared and made carried over (see <see cref="GoNotebookKernel"/>).
/// </summary>
public sealed class GoEngine : LanguageEngine
{
    private static readonly string[] GoAliases = ["go", "golang"];
    private static readonly string[] GoExtensions = [".go"];

    public GoEngine(LanguageEngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        GoRoot = Path.Combine(services.BaseDirectory, "go");
        var toolchain = new GoToolchainProvider(services.Host, services.Processes, services.ToolchainSettings, GoRoot);
        GoToolchain = toolchain;
        var packages = new GoPackageManager(GoRoot, toolchain, services.Processes, services.Host);
        Packages = packages;
        ScriptRunner = new GoBuildAndRunScriptRunner(services.Host, GoRoot);
        NotebookKernels = new DelegateKernelFactory(context =>
            new GoNotebookKernel(toolchain, services.Processes, services.Host, GoRoot, packages.NotebookDependencies, context));
    }

    /// <summary>Where the engine keeps what it builds: the shared build cache, the fry module, the notebooks' cells and the modules they added.</summary>
    public string GoRoot { get; }

    public GoToolchainProvider GoToolchain { get; }

    public override string Id => LanguageIds.Go;
    public override string DisplayName => "Go";
    public override JupyterLanguageInfo Jupyter { get; } = new("go", "Go", "go", "go", ".go", "text/x-gosrc");
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

    public override IToolchainProvider Toolchain => GoToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new GoCompilerDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
