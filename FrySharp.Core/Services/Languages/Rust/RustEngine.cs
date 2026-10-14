using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Rust, built with Cargo and run natively for source files: a two-phase build and run, rustc's messages in Problems,
/// interactive standard input, and crates named in <c>// #crate:</c> comments. Notebook cells are built with Cargo as small
/// programs and run, with what earlier cells defined carried over (see <see cref="RustNotebookKernel"/>).
/// </summary>
public sealed class RustEngine : LanguageEngine
{
    private static readonly string[] RustAliases = ["rust", "rs"];
    private static readonly string[] RustExtensions = [".rs"];

    public RustEngine(LanguageEngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        RustRoot = Path.Combine(services.BaseDirectory, "rust");
        var toolchain = new RustToolchainProvider(services.Host, services.Processes, services.ToolchainSettings, RustRoot);
        RustToolchain = toolchain;
        var packages = new RustPackageManager(RustRoot, toolchain, services.Processes, services.Host);
        Packages = packages;
        ScriptRunner = new RustBuildAndRunScriptRunner(services.Host, RustRoot);
        NotebookKernels = new DelegateKernelFactory(context =>
            new RustNotebookKernel(toolchain, services.Processes, services.Host, RustRoot, packages.NotebookDependencies, context));
    }

    /// <summary>Where the engine keeps what it builds: Cargo packages, the shared crate cache, the notebooks' cells.</summary>
    public string RustRoot { get; }

    public RustToolchainProvider RustToolchain { get; }

    public override string Id => LanguageIds.Rust;
    public override string DisplayName => "Rust";
    public override JupyterLanguageInfo Jupyter { get; } = new("rust", "Rust", "rust", "rust", ".rs", "text/rust");
    public override string ShortName => "RS";
    public override IReadOnlyList<string> Aliases => RustAliases;
    public override IReadOnlyList<string> FileExtensions => RustExtensions;
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

    public override string IconKind => "LanguageRust";
    public override string AccentHex => "#DEA584";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "Rust (rustc / Cargo)";

    public override string NewFileTemplate =>
        "// Builds with Cargo and runs with the Rust toolchain installed on this computer (F5).\n" +
        "// Add a crate with a comment such as: // #crate: rand = \"0.8\"\n" +
        "fn main() {\n" +
        "    println!(\"Hello from Rust!\");\n" +
        "}\n";

    public override IToolchainProvider Toolchain => RustToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new RustDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
