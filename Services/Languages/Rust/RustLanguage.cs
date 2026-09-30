using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Rust, built with Cargo and run natively for source files: a two-phase build and run, rustc's messages in Problems,
/// interactive standard input, and crates named in <c>// #crate:</c> comments.
/// </summary>
public sealed class RustLanguage : LanguageDefinition
{
    private static readonly string[] RustAliases = ["rust", "rs"];
    private static readonly string[] RustExtensions = [".rs"];

    public RustLanguage(StudioLanguageServices services)
    {
        var rustRoot = Path.Combine(services.BaseDirectory, "rust");
        var toolchain = new RustToolchainProvider(services.Host, services.Processes, services.ToolchainSettings, rustRoot);
        RustToolchain = toolchain;
        var packages = new RustPackageManager(rustRoot, toolchain, services.Processes, services.Host);
        Packages = packages;
        Debugger = new RustDebuggerProvider(rustRoot, toolchain, services.Processes, services.Host, services.AdapterManager);
        ScriptRunner = new RustBuildAndRunScriptRunner(services.Host, rustRoot);
        NotebookKernels = new DelegateKernelFactory(context =>
            new RustNotebookKernel(toolchain, services.Processes, services.Host, rustRoot, packages.NotebookDependencies, context));
    }

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

    public override IEditorAssistantFactory EditorAssistants { get; } = RustEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => RustSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new BraceIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new RustFoldingStrategy();

    public override IToolchainProvider Toolchain => RustToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new RustDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override IDebuggerProvider? Debugger { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
